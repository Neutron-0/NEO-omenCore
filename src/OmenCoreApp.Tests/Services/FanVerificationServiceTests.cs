using System.Reflection;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class FanVerificationServiceTests
    {
        [Fact]
        public void VerifyAppliedState_Passes_WhenLevelReadbackMatches_ButRpmCurveDoesNot()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var service = new FanVerificationService(wmiBios: null, fanService: null, logging);

            var result = new FanApplyResult
            {
                RequestedPercent = 60,
                ExpectedRpm = 3600,
                ActualRpmAfter = 2000,
                RpmSource = OmenCore.Models.RpmSource.WmiBios,
                ExpectedLevel = 33,
                ActualLevelAfter = 33,
                LevelReadbackMatched = true,
                WmiCallSucceeded = true
            };

            var method = typeof(FanVerificationService).GetMethod("VerifyAppliedState", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var verified = (bool)method!.Invoke(service, new object[] { result })!;
            verified.Should().BeTrue("matching WMI level readback should count as successful application even when the shared RPM curve is off for this model");
        }

        [Fact]
        public void VerifyAppliedState_Fails_WhenHighTargetHasOnlyLevelEvidence_AndRpmIsTooLow()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var service = new FanVerificationService(wmiBios: null, fanService: null, logging);

            var result = new FanApplyResult
            {
                RequestedPercent = 60,
                ExpectedRpm = 3600,
                ActualRpmAfter = 1600,
                RpmSource = OmenCore.Models.RpmSource.WmiBios,
                ExpectedLevel = 33,
                ActualLevelAfter = 33,
                LevelReadbackMatched = true,
                WmiCallSucceeded = true
            };

            var method = typeof(FanVerificationService).GetMethod("VerifyAppliedState", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var verified = (bool)method!.Invoke(service, new object[] { result })!;
            verified.Should().BeFalse("high-target level-only evidence should not pass when RPM is far below expected response");
        }

        [Fact]
        public void VerificationScore_UsesLevelReadbackFloor_WhenCommandApplied()
        {
            var result = new FanApplyResult
            {
                RequestedPercent = 60,
                ExpectedRpm = 3600,
                ActualRpmAfter = 2000,
                ExpectedLevel = 33,
                ActualLevelAfter = 33,
                LevelReadbackMatched = true,
                WmiCallSucceeded = true,
                VerificationPassed = true,
                RpmStandardDeviation = 50,
                ActualRpmBefore = 2000
            };

            result.VerificationScore.Should().BeGreaterThanOrEqualTo(60,
                "a matched firmware level should not be reported as a Poor/near-failed result solely because the generic expected RPM curve was too aggressive");
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(0, 2, true)]
        [InlineData(33, 33, true)]
        [InlineData(33, 35, true)]
        [InlineData(33, 36, true)]   // actualLevel > expectedLevel: fan coasting down, not a failure
        [InlineData(55, 53, true)]
        [InlineData(55, 51, false)]
        public void IsLevelReadbackMatch_UsesSmallToleranceWindow(int expectedLevel, int actualLevel, bool expectedMatch)
        {
            var result = new FanApplyResult
            {
                RequestedPercent = expectedLevel >= 55 ? 100 : 60,
                ExpectedLevel = expectedLevel,
                ActualLevelAfter = actualLevel
            };

            var method = typeof(FanVerificationService).GetMethod("IsLevelReadbackMatch", BindingFlags.Static | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var matched = (bool)method!.Invoke(null, new object[] { result })!;
            matched.Should().Be(expectedMatch);
        }

        [Fact]
        public void DeviationPercent_Is100_WhenExpectedRpmIsZeroButActualIsNonZero()
        {
            var result = new FanApplyResult
            {
                ExpectedRpm = 0,
                ActualRpmAfter = 3100
            };

            result.DeviationPercent.Should().Be(100,
                "expected 0 RPM with non-zero measured RPM should report a clear mismatch instead of 0% deviation");
        }

        [Fact]
        public void RpmDisplay_LabelsFanLevelDerivedValuesAsEstimates()
        {
            var result = new FanApplyResult
            {
                ActualRpmAfter = 3300,
                RpmSource = OmenCore.Models.RpmSource.Estimated
            };

            result.RpmDisplay.Should().Be("~3300 RPM (fan-level estimate)");
        }

        [Fact]
        public void FanTelemetry_DisplayLabelsEstimatedRpm()
        {
            var telemetry = new OmenCore.Models.FanTelemetry
            {
                SpeedRpm = 3300,
                RpmSource = OmenCore.Models.RpmSource.Estimated,
                RpmState = OmenCore.Models.TelemetryDataState.Valid
            };

            telemetry.DisplayRpmText.Should().Be("~3300 RPM (estimated)");
        }

        [Fact]
        public void VerifyAppliedState_AcceptsMatchingLevelWhenRpmIsOnlyEstimated()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var service = new FanVerificationService(wmiBios: null, fanService: null, logging);
            var result = new FanApplyResult
            {
                RequestedPercent = 60,
                ExpectedRpm = 3600,
                ActualRpmAfter = 3300,
                ExpectedLevel = 33,
                ActualLevelAfter = 33,
                LevelReadbackMatched = true,
                RpmSource = OmenCore.Models.RpmSource.Estimated,
                WmiCallSucceeded = true
            };

            var method = typeof(FanVerificationService).GetMethod("VerifyAppliedState", BindingFlags.Instance | BindingFlags.NonPublic);
            var verified = (bool)method!.Invoke(service, new object[] { result })!;

            verified.Should().BeTrue("matching level readback is the only independent evidence when displayed RPM is derived from that same level");
        }

        [Fact]
        public void VerifyAppliedState_DoesNotTreatEstimatedRpmAsPhysicalEvidence()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var service = new FanVerificationService(wmiBios: null, fanService: null, logging);
            var result = new FanApplyResult
            {
                RequestedPercent = 100,
                ExpectedRpm = 6000,
                ActualRpmAfter = 6000,
                ExpectedLevel = 55,
                ActualLevelAfter = 33,
                LevelReadbackMatched = false,
                RpmSource = OmenCore.Models.RpmSource.Estimated,
                WmiCallSucceeded = true
            };

            var method = typeof(FanVerificationService).GetMethod("VerifyAppliedState", BindingFlags.Instance | BindingFlags.NonPublic);
            var verified = (bool)method!.Invoke(service, new object[] { result })!;

            verified.Should().BeFalse("a level-derived estimate cannot independently verify physical fan response");
        }

        [Fact]
        public void VerificationMismatch_DoesNotDescribeEstimatedValueAsExpectedPhysicalRpm()
        {
            var result = new FanApplyResult
            {
                ExpectedRpm = 6000,
                ActualRpmAfter = 3300,
                ExpectedLevel = 55,
                ActualLevelAfter = 33,
                RpmSource = OmenCore.Models.RpmSource.Estimated
            };
            var method = typeof(FanVerificationService).GetMethod("DescribeVerificationMismatch", BindingFlags.Static | BindingFlags.NonPublic);

            var description = (string)method!.Invoke(null, new object[] { result })!;

            description.Should().Contain("expected level 55");
            description.Should().Contain("fan-level estimate");
            description.Should().NotContain("expected ~6000 RPM");
        }

        [Fact]
        public void VerificationFailureTip_DoesNotReferenceUnavailableOghBackendSwitch()
        {
            var method = typeof(FanVerificationService).GetMethod("GetVerificationFailureTip", BindingFlags.Static | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var tip = (string)method!.Invoke(null, null)!;

            tip.Should().Contain("Restore OEM Auto");
            tip.Should().Contain("export diagnostics");
            tip.Should().NotContain("switching to OGH proxy backend");
            tip.Should().NotContain("switch to OGH proxy backend");
        }

        [Fact]
        public void RestoreFanControlAfterCalibration_ReturnsFalse_WhenNoBackendAvailable()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var service = new FanVerificationService(wmiBios: null, fanService: null, logging);

            service.RestoreFanControlAfterCalibration().Should().BeFalse();
        }
    }
}
