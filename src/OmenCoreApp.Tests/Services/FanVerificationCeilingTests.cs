using FluentAssertions;
using OmenCore.Models;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    /// <summary>
    /// A 100% request that settles just short of the table ceiling is the firmware's own maximum
    /// (8E35 at 48/55, 88F7 at 46/55); one stuck at the 60% step's level (88F8, 33/55) is not.
    /// </summary>
    public class FanVerificationCeilingTests
    {
        private static FanApplyResult Result(int requested, int expected, int actual, RpmSource source = RpmSource.Estimated, int rpm = 0) => new()
        {
            RequestedPercent = requested,
            ExpectedLevel = expected,
            ActualLevelAfter = actual,
            RpmSource = source,
            ActualRpmAfter = rpm == 0 ? actual * 100 : rpm
        };

        [Theory]
        [InlineData(48)] // 8E35 (#195)
        [InlineData(46)] // 88F7 (#215)
        [InlineData(44)] // exactly 80%
        public void NearCeilingPlateauAt100_IsTheBoardsMaximum(int actual)
        {
            FanVerificationService.IsFirmwareCeilingPlateau(Result(100, 55, actual)).Should().BeTrue();
        }

        [Fact]
        public void MaxStuckAtThe60PercentLevel_StillFails()
        {
            // 88F8 (#207): Max never rose past 33/55.
            FanVerificationService.IsFirmwareCeilingPlateau(Result(100, 55, 33)).Should().BeFalse();
        }

        [Fact]
        public void BelowOneHundredPercentRequests_AreNeverRelaxed()
        {
            FanVerificationService.IsFirmwareCeilingPlateau(Result(60, 33, 30)).Should().BeFalse();
        }

        [Fact]
        public void PhysicalTachometerReadingZero_Contradicts_AndFails()
        {
            var r = Result(100, 55, 48, RpmSource.EcDirect);
            r.ActualRpmAfter = 0;
            FanVerificationService.IsFirmwareCeilingPlateau(r).Should().BeFalse();
        }

        private static FanApplyResult Step(int actual, bool passed, int expected = 55) =>
            new()
            {
                RequestedPercent = 100, ExpectedLevel = expected, ActualLevelAfter = actual, RpmSource = RpmSource.Estimated,
                ActualRpmAfter = actual * 100, VerificationPassed = passed, VerificationEvidence = passed ? "FirmwareCeiling" : "None"
            };

        [Fact]
        public void Reconcile_88F8_CpuFanAt43_PassesBesideTheGpuFanAt46()
        {
            // #207: CPU fan settled at 43/55 (78%) after Max and a direct write; the GPU fan reached 46/55 and passed.
            var cpu = Step(43, passed: false);
            var gpu = Step(46, passed: true);

            FanVerificationService.ReconcileCeilingAcrossFans(new[] { cpu, gpu }).Should().Be(1);

            cpu.VerificationPassed.Should().BeTrue();
            cpu.VerificationEvidence.Should().Be("FirmwareCeiling");
        }

        [Fact]
        public void Reconcile_BothFansStuckLow_StillFail()
        {
            // 88F8 before the fix: Max never rose past the 60% step's level on either fan.
            var a = Step(33, passed: false);
            var b = Step(33, passed: false);

            FanVerificationService.ReconcileCeilingAcrossFans(new[] { a, b }).Should().Be(0);

            a.VerificationPassed.Should().BeFalse();
            b.VerificationPassed.Should().BeFalse();
        }

        [Fact]
        public void Reconcile_AFanFarBelowItsSibling_StillFails()
        {
            var low = Step(30, passed: false);
            var high = Step(48, passed: true);

            FanVerificationService.ReconcileCeilingAcrossFans(new[] { low, high }).Should().Be(0);
            low.VerificationPassed.Should().BeFalse();
        }

        [Fact]
        public void Reconcile_PhysicalTachometerAtZero_IsNotUpgraded()
        {
            var stalled = Step(43, passed: false);
            stalled.RpmSource = RpmSource.EcDirect;
            stalled.ActualRpmAfter = 0;
            var ok = Step(46, passed: true);

            FanVerificationService.ReconcileCeilingAcrossFans(new[] { stalled, ok }).Should().Be(0);
        }

        [Fact]
        public void Reconcile_NeverTouchesStepsBelowOneHundredPercent()
        {
            var a = Step(20, passed: false); a.RequestedPercent = 60;
            var b = Step(21, passed: true); b.RequestedPercent = 60;

            FanVerificationService.ReconcileCeilingAcrossFans(new[] { a, b }).Should().Be(0);
        }

        [Fact]
        public void RefusedWrite_IsReportedAsRefused_NotAsUnavailableRpm()
        {
            // 878A (#218): "WMI fan control failed for fan 0, level 16" - nothing was written, so nothing could be read.
            var r = new FanApplyResult { RequestedPercent = 30, ExpectedLevel = 16, RpmSource = RpmSource.Unknown, WmiCallSucceeded = false };

            r.RpmDisplay.Should().Be("firmware refused level 16");
        }

        [Fact]
        public void AcceptedWriteWithNoTachometer_StillSaysUnavailable()
        {
            var r = new FanApplyResult { RequestedPercent = 30, ExpectedLevel = 16, RpmSource = RpmSource.Unknown, WmiCallSucceeded = true };

            r.RpmDisplay.Should().Be("Physical RPM unavailable");
        }
    }
}
