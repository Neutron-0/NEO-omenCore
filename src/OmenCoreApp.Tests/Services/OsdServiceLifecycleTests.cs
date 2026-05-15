using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    [Collection("Config Isolation")]
    public class OsdServiceLifecycleTests : IDisposable
    {
        private readonly string _tempDir;

        public OsdServiceLifecycleTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "omen_test_config_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", _tempDir);
        }

        public void Dispose()
        {
            try
            {
                Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", null);
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
            }
        }

        [Fact]
        public void SetModeMethods_UpdateCachedValues_EvenWithoutActiveOverlay()
        {
            var service = CreateService();

            service.SetCurrentMode("Balanced");
            service.SetPerformanceMode("Performance");
            service.SetFanMode("Gaming");

            GetPrivateField<string>(service, "_lastCurrentMode").Should().Be("Balanced");
            GetPrivateField<string>(service, "_lastPerformanceMode").Should().Be("Performance");
            GetPrivateField<string>(service, "_lastFanMode").Should().Be("Gaming");

            service.Dispose();
        }

        [Fact]
        public void SetModeMethods_NormalizeNullInputs_ToEmptyCachedValues()
        {
            var service = CreateService();

            service.SetCurrentMode(null!);
            service.SetPerformanceMode(null!);
            service.SetFanMode(null!);

            GetPrivateField<string>(service, "_lastCurrentMode").Should().Be(string.Empty);
            GetPrivateField<string>(service, "_lastPerformanceMode").Should().Be(string.Empty);
            GetPrivateField<string>(service, "_lastFanMode").Should().Be(string.Empty);

            service.Dispose();
        }

        [Fact]
        public void VisibilityChanged_Event_OnlyFires_OnActualTransition()
        {
            var service = CreateService();
            var changeCount = 0;
            var lastValue = false;

            service.VisibilityChanged += (_, visible) =>
            {
                changeCount++;
                lastValue = visible;
            };

            InvokePrivateMethod(service, "NotifyVisibilityChanged", true);
            InvokePrivateMethod(service, "NotifyVisibilityChanged", true);
            InvokePrivateMethod(service, "NotifyVisibilityChanged", false);
            InvokePrivateMethod(service, "NotifyVisibilityChanged", false);

            changeCount.Should().Be(2, "duplicate visibility values should not re-emit events");
            lastValue.Should().BeFalse();

            service.Dispose();
        }

        [Fact]
        public void StartHotkeyRetryTimer_ReplacesExistingTimerInstance()
        {
            var service = CreateService();

            InvokePrivateMethod(service, "StartHotkeyRetryTimer", (uint)0x0002, (uint)0x70, "Ctrl+F1");
            var firstTimer = GetPrivateField<System.Threading.Timer>(service, "_retryTimer");
            firstTimer.Should().NotBeNull();

            InvokePrivateMethod(service, "StartHotkeyRetryTimer", (uint)0x0004, (uint)0x71, "Shift+F2");
            var secondTimer = GetPrivateField<System.Threading.Timer>(service, "_retryTimer");
            secondTimer.Should().NotBeNull();
            secondTimer.Should().NotBeSameAs(firstTimer, "a new retry cycle should replace the previous timer");

            service.Dispose();
        }

        [Fact]
        public void Dispose_ClearsRetryTimerReference()
        {
            var service = CreateService();

            InvokePrivateMethod(service, "StartHotkeyRetryTimer", (uint)0x0002, (uint)0x70, "Ctrl+F1");
            GetPrivateField<System.Threading.Timer>(service, "_retryTimer").Should().NotBeNull();

            service.Dispose();

            GetPrivateField<System.Threading.Timer?>(service, "_retryTimer").Should().BeNull();
        }

        [Fact]
        public void Shutdown_WhenVisible_EmitsHiddenTransitionAndClearsVisibilityFlag()
        {
            var service = CreateService();
            var changes = 0;
            var lastVisible = true;

            service.VisibilityChanged += (_, visible) =>
            {
                changes++;
                lastVisible = visible;
            };

            SetPrivateField(service, "_isVisible", true);

            service.Shutdown();

            changes.Should().Be(1, "shutdown should emit one hidden transition when previously visible");
            lastVisible.Should().BeFalse();
            service.IsVisible.Should().BeFalse();

            service.Dispose();
        }

        [Fact]
        public void Shutdown_WhenAlreadyHidden_DoesNotEmitDuplicateHiddenTransition()
        {
            var service = CreateService();
            var changes = 0;

            service.VisibilityChanged += (_, _) => changes++;

            service.Shutdown();

            changes.Should().Be(0, "shutdown from hidden state should not emit duplicate visibility events");
            service.IsVisible.Should().BeFalse();

            service.Dispose();
        }

        private static OsdService CreateService()
        {
            var config = new ConfigurationService();
            var logging = new LoggingService();
            return new OsdService(config, logging);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull($"private method {methodName} should exist");
            method!.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull($"private field {fieldName} should exist");
            return (T)field!.GetValue(target)!;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull($"private field {fieldName} should exist");
            field!.SetValue(target, value);
        }
    }
}