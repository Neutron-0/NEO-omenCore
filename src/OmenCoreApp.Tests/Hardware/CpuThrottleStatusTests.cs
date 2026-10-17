using FluentAssertions;
using OmenCore.Hardware;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    public class CpuThrottleStatusTests
    {
        [Fact]
        public void Zero_IsNotThrottling()
        {
            var s = CpuThrottleStatus.Decode(0);

            s.AnyThrottling.Should().BeFalse();
            s.Thermal.Should().BeFalse();
            s.PowerLimit.Should().BeFalse();
        }

        [Theory]
        [InlineData(1UL << 0, true, false, false, false)]   // bit 0: thermal status
        [InlineData(1UL << 6, false, true, false, false)]   // bit 6: threshold #1
        [InlineData(1UL << 8, false, false, true, false)]   // bit 8: threshold #2
        [InlineData(1UL << 10, false, false, false, true)]  // bit 10: power limitation
        public void EachBit_MapsToItsOwnFlag(ulong value, bool thermal, bool t1, bool t2, bool power)
        {
            CpuThrottleStatus.Decode(value).Should().Be(new CpuThrottleStatus(thermal, t1, t2, power));
        }

        [Fact]
        public void LogBits_AreNotTreatedAsCurrentThrottling()
        {
            // Bits 1, 3 and 11 are the sticky log copies; they stay set after the event and must not read as throttling now.
            CpuThrottleStatus.Decode((1UL << 1) | (1UL << 3) | (1UL << 11)).AnyThrottling.Should().BeFalse();
        }

        [Fact]
        public void ReadingValidAndDigitalReadout_AreIgnored()
        {
            // Bit 31 (reading valid) and bits 16 to 22 (digital readout) say nothing about throttling.
            CpuThrottleStatus.Decode((1UL << 31) | (0x2AUL << 16)).AnyThrottling.Should().BeFalse();
        }
    }
}
