using FluentAssertions;
using OmenCore.Hardware;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    public class SensorAliasTrackerTests
    {
        [Fact]
        public void IdenticalPair_IsReportedOnce_AfterThirtyReads()
        {
            var t = new SensorAliasTracker();

            for (var i = 1; i < SensorAliasTracker.ReportAfterReads; i++) t.Observe(56, 56).Should().BeFalse();
            t.Observe(56, 56).Should().BeTrue();
            t.Observe(56, 56).Should().BeFalse("it is reported once per session");
        }

        [Fact]
        public void AnyDifference_StartsTheCountAgain()
        {
            var t = new SensorAliasTracker();
            for (var i = 0; i < SensorAliasTracker.ReportAfterReads - 1; i++) t.Observe(60, 60);

            t.Observe(60, 63).Should().BeFalse();
            for (var i = 1; i < SensorAliasTracker.ReportAfterReads; i++) t.Observe(61, 61).Should().BeFalse();
            t.Observe(61, 61).Should().BeTrue();
        }

        [Fact]
        public void UnknownReadings_AreNotAnAlias()
        {
            var t = new SensorAliasTracker();
            for (var i = 0; i < 100; i++) t.Observe(0, 0).Should().BeFalse();
        }
    }
}
