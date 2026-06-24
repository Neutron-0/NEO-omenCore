using FluentAssertions;
using OmenCore.Models;
using Xunit;

namespace OmenCoreApp.Tests.Models
{
    public class StartupRestorePolicyTests
    {
        [Fact]
        public void IsEnabled_ReturnsFalseForEveryCategory_WhenGlobalGateDisabled()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = false,
                StartupRestoreFansEnabled = true,
                StartupRestorePerformanceEnabled = true,
                StartupRestoreRgbEnabled = true,
                StartupRestoreTuningEnabled = true
            };

            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Fans).Should().BeFalse();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Performance).Should().BeFalse();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Rgb).Should().BeFalse();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Tuning).Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_TreatsMissingCategoryFlagsAsEnabled_WhenGlobalGateEnabled()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = true,
                StartupRestoreFansEnabled = null,
                StartupRestorePerformanceEnabled = null,
                StartupRestoreRgbEnabled = null,
                StartupRestoreTuningEnabled = null
            };

            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Fans).Should().BeTrue();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Performance).Should().BeTrue();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Rgb).Should().BeTrue();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Tuning).Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_HonorsExplicitCategoryOptOuts()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = true,
                StartupRestoreFansEnabled = true,
                StartupRestorePerformanceEnabled = false,
                StartupRestoreRgbEnabled = false,
                StartupRestoreTuningEnabled = true
            };

            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Fans).Should().BeTrue();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Performance).Should().BeFalse();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Rgb).Should().BeFalse();
            StartupRestorePolicy.IsEnabled(config, StartupRestoreCategory.Tuning).Should().BeTrue();
            StartupRestorePolicy.BuildSummary(config).Should().Be("Fans=on; Performance=off; RGB=off; Tuning=on");
        }

        [Theory]
        [InlineData("OMEN 16-xd0xxx", true)]
        [InlineData("HP OMEN Gaming Laptop 16-ap0xxx", true)]
        [InlineData("OMEN by HP Laptop 16-xd0xxx", true)]
        [InlineData("OMEN MAX Gaming Laptop 16-ah0xxx", true)]
        [InlineData("Victus by HP Gaming Laptop 15-fa2xxx", true)]
        [InlineData("Victus by HP Gaming Laptop 16-r1xxx", true)]
        [InlineData("OMEN by HP Laptop 17-ck1xxx", false)]
        [InlineData("HyperX OMEN MAX Gaming Laptop 16t-ah100", true)]
        [InlineData(null, false)]
        public void IsSensitiveModel_FlagsOmen16BoardPatternsAndAnyVictus(string? model, bool expected)
        {
            StartupRestorePolicy.IsSensitiveModel(model).Should().Be(expected);
        }

        [Fact]
        public void DescribeTuningStartupReapplyState_NotConfirmed_DoesNotCheckGates()
        {
            var config = new AppConfig { EnableStartupHardwareRestore = false };

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: false, model: "Victus by HP Gaming Laptop 15-fa2xxx")
                .Should().Contain("Not confirmed");
        }

        [Fact]
        public void DescribeTuningStartupReapplyState_Confirmed_BlockedByGlobalGate()
        {
            var config = new AppConfig { EnableStartupHardwareRestore = false };

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: true, model: "OMEN Laptop 15-ek0xxx")
                .Should().Contain("Blocked").And.Contain("Startup Hardware Restore");
        }

        [Fact]
        public void DescribeTuningStartupReapplyState_Confirmed_BlockedByTuningCategoryGate()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = true,
                StartupRestoreTuningEnabled = false
            };

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: true, model: "OMEN Laptop 15-ek0xxx")
                .Should().Contain("Blocked").And.Contain("Tuning category");
        }

        [Fact]
        public void DescribeTuningStartupReapplyState_Confirmed_BlockedBySensitiveModelOverride()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = true,
                StartupRestoreTuningEnabled = true,
                AllowStartupRestoreOnOmen16OrVictus = false
            };

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: true, model: "Victus by HP Gaming Laptop 15-fa2xxx")
                .Should().Contain("Blocked").And.Contain("sensitive-model");
        }

        [Fact]
        public void DescribeTuningStartupReapplyState_Confirmed_EnabledWhenAllGatesPass()
        {
            var config = new AppConfig
            {
                EnableStartupHardwareRestore = true,
                StartupRestoreTuningEnabled = true,
                AllowStartupRestoreOnOmen16OrVictus = true
            };

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: true, model: "Victus by HP Gaming Laptop 15-fa2xxx")
                .Should().Contain("Enabled");

            StartupRestorePolicy.DescribeTuningStartupReapplyState(config, confirmedForStartup: true, model: "OMEN Laptop 15-ek0xxx")
                .Should().Contain("Enabled");
        }
    }
}
