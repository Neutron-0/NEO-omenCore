using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services.Rgb;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RgbManagerTests
    {
        [Fact]
        public async Task SyncStaticColorAsync_ReportsProviderFailuresWithoutStoppingOtherProviders()
        {
            var manager = new RgbManager();
            var workingProvider = new TestRgbProvider("working");
            var failingProvider = new TestRgbProvider("failing")
            {
                ThrowOnStaticColor = true
            };
            RgbSyncEventArgs? completed = null;

            manager.RegisterProvider(workingProvider);
            manager.RegisterProvider(failingProvider);
            manager.SyncCompleted += (_, args) => completed = args;

            await manager.SyncStaticColorAsync(Color.FromArgb(0x12, 0x34, 0x56));

            workingProvider.LastEffect.Should().Be("static:#123456");
            completed.Should().NotBeNull();
            completed!.ProvidersAffected.Should().Be(2);
            completed.ProvidersSucceeded.Should().Be(1);
            completed.ProvidersFailed.Should().Be(1);
        }

        [Fact]
        public void GetStatus_IncludesProviderConnectionStatusAndDetail()
        {
            var manager = new RgbManager();
            manager.RegisterProvider(new TestRgbProvider("keyboard")
            {
                Detail = "4-zone keyboard connected"
            });

            var status = manager.GetStatus();

            status.ProviderStatuses.Should().ContainSingle(provider =>
                provider.ProviderId == "keyboard" &&
                provider.ConnectionStatus == RgbProviderConnectionStatus.Connected &&
                provider.StatusDetail == "4-zone keyboard connected");
        }

        [Fact]
        public async Task InitializeAllAsync_WhenCalledTwice_InitializesProvidersOnlyOnce()
        {
            var manager = new RgbManager();
            var provider = new TestRgbProvider("keyboard")
            {
                IsAvailable = false,
                AvailableAfterInitialize = true
            };

            manager.RegisterProvider(provider);

            await manager.InitializeAllAsync();
            await manager.InitializeAllAsync();

            provider.InitializeCount.Should().Be(1);
            provider.IsAvailable.Should().BeTrue();
        }

        [Fact]
        public async Task SyncStaticColorAsync_LazilyInitializesProviderBeforeFirstWrite()
        {
            var manager = new RgbManager();
            var provider = new TestRgbProvider("keyboard")
            {
                IsAvailable = false,
                AvailableAfterInitialize = true
            };

            manager.RegisterProvider(provider);

            await manager.SyncStaticColorAsync(Color.FromArgb(0xAA, 0xBB, 0xCC));

            provider.InitializeCount.Should().Be(1);
            provider.LastEffect.Should().Be("static:#AABBCC");
        }

        /// <summary>
        /// Issue #130: Static RGB path must remain available even if some providers
        /// don't support certain effects; static color should always work.
        /// </summary>
        [Fact]
        public async Task SyncStaticColorAsync_AlwaysAvailable_DoesNotFailOnPartialSupport()
        {
            var manager = new RgbManager();
            var staticOnlyProvider = new TestRgbProvider("static-only");
            staticOnlyProvider.SupportedEffects = new[] { RgbEffectType.Static };

            manager.RegisterProvider(staticOnlyProvider);

            await manager.SyncStaticColorAsync(Color.FromArgb(0xFF, 0x00, 0x00));

            staticOnlyProvider.LastEffect.Should().Be("static:#FF0000");
        }

        /// <summary>
        /// Issue #130: When an effect is requested but no provider supports it,
        /// ApplyEffectToAllAsync should exit cleanly without crashing, and log
        /// the unsupported reason.
        /// </summary>
        [Fact]
        public async Task ApplyEffectToAllAsync_UnsupportedEffect_SkipsCleanlyWithStatus()
        {
            var manager = new RgbManager();
            var staticOnlyProvider = new TestRgbProvider("keyboard")
            {
                SupportedEffects = new[] { RgbEffectType.Static }
            };

            manager.RegisterProvider(staticOnlyProvider);
            RgbSyncEventArgs? completed = null;
            manager.SyncCompleted += (_, args) => completed = args;

            // Request breathing effect which this provider doesn't support
            await manager.ApplyEffectToAllAsync("effect:breathing");

            // Should complete without throwing, but with 0 providers succeeded
            completed.Should().NotBeNull("completion event should be raised");
            completed!.ProvidersAffected.Should().Be(0, "no providers support breathing in this scenario");
            completed.ProvidersFailed.Should().Be(0);
        }

        /// <summary>
        /// Issue #130: When multiple providers exist and some support an effect,
        /// only the supporting providers should execute; unsupported ones skipped cleanly.
        /// </summary>
        [Fact]
        public async Task ApplyEffectToAllAsync_PartialSupport_OnlyCallsSupportingProviders()
        {
            var manager = new RgbManager();
            var staticProvider = new TestRgbProvider("provider1");
            staticProvider.SupportedEffects = new[] { RgbEffectType.Static };
            var breathingProvider = new TestRgbProvider("provider2");
            breathingProvider.SupportedEffects = new[] { RgbEffectType.Breathing, RgbEffectType.Spectrum };

            manager.RegisterProvider(staticProvider);
            manager.RegisterProvider(breathingProvider);
            RgbSyncEventArgs? completed = null;
            manager.SyncCompleted += (_, args) => completed = args;

            // Request breathing effect
            await manager.ApplyEffectToAllAsync("effect:breathing");

            // Only breathingProvider should have been called
            staticProvider.LastEffect.Should().BeNull("static-only provider should be skipped");
            breathingProvider.LastEffect.Should().Be("effect:breathing");
            completed.Should().NotBeNull();
            completed!.ProvidersAffected.Should().Be(1, "only breathing provider is affected");
        }

        [Fact]
        public async Task ApplyEffectToAllAsync_BreathingPayload_SkipsStaticOnlyProviders()
        {
            var manager = new RgbManager();
            var staticProvider = new TestRgbProvider("static")
            {
                SupportedEffects = new[] { RgbEffectType.Static }
            };
            var breathingProvider = new TestRgbProvider("breathing")
            {
                SupportedEffects = new[] { RgbEffectType.Breathing }
            };

            manager.RegisterProvider(staticProvider);
            manager.RegisterProvider(breathingProvider);

            await manager.ApplyEffectToAllAsync("breathing:#FF0000");

            staticProvider.LastEffect.Should().BeNull("static-only provider should not receive breathing payloads");
            breathingProvider.LastEffect.Should().Be("breathing:#FF0000");
        }

        [Fact]
        public async Task ApplyEffectToAllAsync_OffEffect_OnlyCallsOffCapableProviders()
        {
            var manager = new RgbManager();
            var staticProvider = new TestRgbProvider("static")
            {
                SupportedEffects = new[] { RgbEffectType.Static }
            };
            var offProvider = new TestRgbProvider("off")
            {
                SupportedEffects = new[] { RgbEffectType.Off }
            };

            manager.RegisterProvider(staticProvider);
            manager.RegisterProvider(offProvider);

            await manager.ApplyEffectToAllAsync("off");

            staticProvider.LastEffect.Should().BeNull("providers without Off support should be skipped");
            offProvider.LastEffect.Should().Be("off");
        }

        private sealed class TestRgbProvider : IRgbProvider
        {
            public TestRgbProvider(string id)
            {
                ProviderId = id;
                ProviderName = id;
            }

            public string ProviderName { get; }
            public string ProviderId { get; }
            public bool IsAvailable { get; set; } = true;
            public bool IsConnected => IsAvailable;
            public int DeviceCount => IsAvailable ? 1 : 0;
            public RgbProviderConnectionStatus ConnectionStatus =>
                IsAvailable ? RgbProviderConnectionStatus.Connected : RgbProviderConnectionStatus.Disabled;
            public string StatusDetail => Detail;
            public string Detail { get; set; } = "1 device connected";
            public bool ThrowOnStaticColor { get; set; }
            public bool AvailableAfterInitialize { get; set; } = true;
            public int InitializeCount { get; private set; }
            public string? LastEffect { get; private set; }
            public IReadOnlyList<RgbEffectType> SupportedEffects { get; set; } =
                new[] { RgbEffectType.Static, RgbEffectType.Breathing, RgbEffectType.Spectrum, RgbEffectType.Off };

            public Task InitializeAsync()
            {
                InitializeCount++;
                IsAvailable = AvailableAfterInitialize;
                return Task.CompletedTask;
            }

            public Task ApplyEffectAsync(string effectId)
            {
                LastEffect = effectId;
                return Task.CompletedTask;
            }

            public Task SetStaticColorAsync(Color color)
            {
                if (ThrowOnStaticColor)
                    throw new InvalidOperationException("device write failed");

                LastEffect = $"static:#{color.R:X2}{color.G:X2}{color.B:X2}";
                return Task.CompletedTask;
            }

            public Task SetBreathingEffectAsync(Color color)
            {
                LastEffect = $"breathing:#{color.R:X2}{color.G:X2}{color.B:X2}";
                return Task.CompletedTask;
            }

            public Task SetSpectrumEffectAsync()
            {
                LastEffect = "spectrum";
                return Task.CompletedTask;
            }

            public Task TurnOffAsync()
            {
                LastEffect = "off";
                return Task.CompletedTask;
            }
        }
    }
}
