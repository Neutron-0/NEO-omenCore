using FluentAssertions;
using OmenCore.Hardware;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    /// <summary>
    /// Validates capability visibility gating for undervolt, RGB, and GPU power controls.
    /// Covers field-reported unsupported feature leakage on AMD models and driver-present/
    /// runtime-blocked scenarios (field evidence: 3.6.1-FIELD-EVIDENCE.md §J, cohort A 8BCD).
    /// </summary>
    public class DeviceCapabilitiesTests
    {
        #region Undervolt gating

        [Fact]
        public void ShowUndervolt_IsFalse_WhenRuntimeNotReady_EvenIfDriverCapabilityIsTrue()
        {
            var caps = new DeviceCapabilities
            {
                CanUndervolt = true,
                UndervoltRuntimeReady = false,
                UndervoltBlockReason = "MSR backend unavailable"
            };

            caps.ShowUndervolt.Should().BeFalse("runtime readiness must gate undervolt controls to avoid false actionable UI");
        }

        [Fact]
        public void ShowUndervolt_IsTrue_WhenCapabilityAndRuntimeAreReady()
        {
            var caps = new DeviceCapabilities
            {
                CanUndervolt = true,
                UndervoltRuntimeReady = true
            };

            caps.ShowUndervolt.Should().BeTrue();
        }

        [Fact]
        public void ShowUndervolt_IsFalse_WhenCapabilityIsFalse()
        {
            var caps = new DeviceCapabilities
            {
                CanUndervolt = false,
                UndervoltRuntimeReady = true
            };

            caps.ShowUndervolt.Should().BeFalse();
        }

        [Fact]
        public void ShowUndervolt_IsFalse_WhenModelConfigExplicitlyDisablesIt_EvenIfRuntimeIsReady()
        {
            // AMD models set SupportsUndervolt = false in ModelCapabilityDatabase.
            // The capability gate must respect this to prevent Intel-style MSR controls
            // from being shown on AMD laptops (field evidence: cohort A 8BCD AMD Ryzen).
            var amdModelConfig = new ModelCapabilities
            {
                SupportsUndervolt = false
            };

            var caps = new DeviceCapabilities
            {
                CanUndervolt = true,
                UndervoltRuntimeReady = true,
                ModelConfig = amdModelConfig
            };

            caps.ShowUndervolt.Should().BeFalse("AMD models must not show Intel-style undervolt controls");
        }

        [Fact]
        public void ShowUndervolt_BlockReason_IsPopulated_WhenRuntimeBlocked()
        {
            const string expectedReason = "PawnIO MSR write failed HRESULT 0x80070002";
            var caps = new DeviceCapabilities
            {
                CanUndervolt = true,
                UndervoltRuntimeReady = false,
                UndervoltBlockReason = expectedReason
            };

            caps.UndervoltBlockReason.Should().Be(expectedReason);
            caps.ShowUndervolt.Should().BeFalse();
        }

        #endregion

        #region RGB lighting gating

        [Fact]
        public void ShowRgbLighting_IsFalse_WhenNoRuntimeOrModelRgb()
        {
            var caps = new DeviceCapabilities
            {
                HasZoneLighting = false,
                HasPerKeyLighting = false,
                IsKnownModel = true,
                ModelConfig = new ModelCapabilities
                {
                    HasFourZoneRgb = false,
                    HasPerKeyRgb = false
                }
            };

            caps.ShowRgbLighting.Should().BeFalse("no RGB capability should hide lighting controls");
        }

        [Fact]
        public void ShowRgbLighting_IsTrue_WhenRuntimeDetectsZoneLighting()
        {
            var caps = new DeviceCapabilities
            {
                HasZoneLighting = true,
                HasPerKeyLighting = false
            };

            caps.ShowRgbLighting.Should().BeTrue();
        }

        [Fact]
        public void ShowRgbLighting_IsTrue_WhenKnownModelHasFourZoneRgb()
        {
            var caps = new DeviceCapabilities
            {
                HasZoneLighting = false,
                HasPerKeyLighting = false,
                IsKnownModel = true,
                ModelConfig = new ModelCapabilities
                {
                    HasFourZoneRgb = true,
                    HasPerKeyRgb = false
                }
            };

            caps.ShowRgbLighting.Should().BeTrue();
        }

        [Fact]
        public void ShowRgbLighting_IsFalse_WhenUnknownModelButModelConfigHasRgb()
        {
            // Unknown/unverified models should not show OMEN keyboard RGB controls
            // even if a model config entry suggests RGB — runtime detection must confirm.
            var caps = new DeviceCapabilities
            {
                HasZoneLighting = false,
                HasPerKeyLighting = false,
                IsKnownModel = false,
                ModelConfig = new ModelCapabilities
                {
                    HasFourZoneRgb = true
                }
            };

            caps.ShowRgbLighting.Should().BeFalse("unknown model must not show OMEN RGB controls without runtime confirmation");
        }

        #endregion

        #region GPU power boost gating

        [Fact]
        public void ShowGpuPowerBoost_IsTrue_WhenRuntimeDetectsGpuPowerControl()
        {
            var caps = new DeviceCapabilities
            {
                HasGpuPowerControl = true
            };

            caps.ShowGpuPowerBoost.Should().BeTrue();
        }

        [Fact]
        public void ShowGpuPowerBoost_IsTrue_WhenKnownModelSupportsIt()
        {
            var caps = new DeviceCapabilities
            {
                HasGpuPowerControl = false,
                IsKnownModel = true,
                ModelConfig = new ModelCapabilities
                {
                    SupportsGpuPowerBoost = true
                }
            };

            caps.ShowGpuPowerBoost.Should().BeTrue();
        }

        [Fact]
        public void ShowGpuPowerBoost_IsFalse_WhenUnknownModelAndNoRuntimeDetection()
        {
            var caps = new DeviceCapabilities
            {
                HasGpuPowerControl = false,
                IsKnownModel = false,
                ModelConfig = new ModelCapabilities
                {
                    SupportsGpuPowerBoost = true
                }
            };

            caps.ShowGpuPowerBoost.Should().BeFalse("unknown model must not show GPU power boost without runtime confirmation");
        }

        #endregion
    }
}
