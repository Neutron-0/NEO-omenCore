using FluentAssertions;
using OmenCore.Models;
using Xunit;

namespace OmenCoreApp.Tests.Models
{
    /// <summary>
    /// Unit tests for the bidirectional fan/performance mode mapping used by the linked
    /// sync pipeline (field evidence: cohort A/B desync, 3.6.1-FIELD-EVIDENCE.md §A).
    /// Covers fan→performance and performance→fan canonical mappings, alias normalization,
    /// and edge cases that could cause sync mismatch or ping-pong.
    /// </summary>
    public class FanPerformanceLinkMapperTests
    {
        #region Fan → Performance

        [Theory]
        [InlineData("Auto", "Balanced")]
        [InlineData("auto", "Balanced")]
        [InlineData("Balanced", "Balanced")]
        [InlineData("balanced", "Balanced")]
        [InlineData("", "Balanced")]
        [InlineData(null, "Balanced")]
        public void MapFanModeToPerformanceMode_AutoAliases_ReturnBalanced(string? fanMode, string expected)
        {
            var result = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fanMode);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Quiet", "Quiet")]
        [InlineData("quiet", "Quiet")]
        [InlineData("Silent", "Quiet")]
        [InlineData("silent", "Quiet")]
        public void MapFanModeToPerformanceMode_QuietAliases_ReturnQuiet(string fanMode, string expected)
        {
            var result = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fanMode);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Gaming", "Performance")]
        [InlineData("gaming", "Performance")]
        [InlineData("Extreme", "Performance")]
        [InlineData("extreme", "Performance")]
        [InlineData("Max", "Performance")]
        [InlineData("max", "Performance")]
        [InlineData("Maximum", "Performance")]
        [InlineData("Performance", "Performance")]
        [InlineData("Turbo", "Performance")]
        [InlineData("turbo", "Performance")]
        public void MapFanModeToPerformanceMode_PerformanceAliases_ReturnPerformance(string fanMode, string expected)
        {
            var result = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fanMode);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("My Custom Curve")]
        [InlineData("Manual")]
        [InlineData("Custom")]
        public void MapFanModeToPerformanceMode_CustomOrManualPresets_ReturnBalanced(string fanMode)
        {
            // Custom/manual presets have no canonical performance mapping; fall back to Balanced
            var result = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fanMode);
            result.Should().Be("Balanced");
        }

        #endregion

        #region Performance → Fan

        [Theory]
        [InlineData("Balanced", "Auto")]
        [InlineData("balanced", "Auto")]
        [InlineData("", "Auto")]
        [InlineData(null, "Auto")]
        public void MapPerformanceModeToFanMode_BalancedOrEmpty_ReturnAuto(string? performanceMode, string expected)
        {
            var result = FanPerformanceLinkMapper.MapPerformanceModeToFanMode(performanceMode);
            result.Should().Be(expected);
        }

        [Fact]
        public void MapPerformanceModeToFanMode_Quiet_ReturnQuiet()
        {
            FanPerformanceLinkMapper.MapPerformanceModeToFanMode("Quiet").Should().Be("Quiet");
            FanPerformanceLinkMapper.MapPerformanceModeToFanMode("quiet").Should().Be("Quiet");
        }

        [Fact]
        public void MapPerformanceModeToFanMode_Performance_ReturnExtreme()
        {
            FanPerformanceLinkMapper.MapPerformanceModeToFanMode("Performance").Should().Be("Extreme");
            FanPerformanceLinkMapper.MapPerformanceModeToFanMode("performance").Should().Be("Extreme");
        }

        [Theory]
        [InlineData("Turbo")]
        [InlineData("Unknown")]
        [InlineData("Silent")]
        public void MapPerformanceModeToFanMode_UnrecognizedAliases_ReturnAuto(string performanceMode)
        {
            // Unrecognized performance modes (aliases not yet normalized) fall back to Auto
            // to avoid silent mapping errors that could change fan behavior unexpectedly.
            var result = FanPerformanceLinkMapper.MapPerformanceModeToFanMode(performanceMode);
            result.Should().Be("Auto");
        }

        #endregion

        #region Round-trip consistency

        [Theory]
        [InlineData("Auto", "Balanced", "Auto")]
        [InlineData("Quiet", "Quiet", "Quiet")]
        [InlineData("Extreme", "Performance", "Extreme")]
        [InlineData("Gaming", "Performance", "Extreme")]
        [InlineData("Max", "Performance", "Extreme")]
        public void RoundTrip_FanToPerformanceToFan_IsStable(string startFan, string expectedPerf, string expectedFanAfterRoundtrip)
        {
            // Fan → Performance
            var perf = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(startFan);
            perf.Should().Be(expectedPerf);

            // Performance → Fan (second hop)
            var fanBack = FanPerformanceLinkMapper.MapPerformanceModeToFanMode(perf);
            fanBack.Should().Be(expectedFanAfterRoundtrip);
        }

        [Theory]
        [InlineData("Balanced", "Auto", "Balanced")]
        [InlineData("Quiet", "Quiet", "Quiet")]
        [InlineData("Performance", "Extreme", "Performance")]
        public void RoundTrip_PerformanceToFanToPerformance_IsStable(string startPerf, string expectedFan, string expectedPerfAfterRoundtrip)
        {
            // Performance → Fan
            var fan = FanPerformanceLinkMapper.MapPerformanceModeToFanMode(startPerf);
            fan.Should().Be(expectedFan);

            // Fan → Performance (second hop)
            var perfBack = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fan);
            perfBack.Should().Be(expectedPerfAfterRoundtrip);
        }

        [Theory]
        [InlineData("Auto")]
        [InlineData("Quiet")]
        [InlineData("Extreme")]
        public void DoubleRoundTrip_DoesNotDiverge(string startFan)
        {
            // A double round-trip (Fan→Perf→Fan→Perf) should converge to the same result
            // as a single round-trip, confirming the guard against recursive sync ping-pong.
            var perf1 = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(startFan);
            var fan1 = FanPerformanceLinkMapper.MapPerformanceModeToFanMode(perf1);
            var perf2 = FanPerformanceLinkMapper.MapFanModeToPerformanceMode(fan1);

            perf2.Should().Be(perf1, because: "double round-trip must converge (no ping-pong drift)");
        }

        #endregion
    }
}
