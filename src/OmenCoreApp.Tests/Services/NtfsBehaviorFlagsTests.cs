// BUG-3820-005 (Optimizer "Disable Last Access Timestamps" always reported itself as
// failed): fsutil behavior set disablelastaccess <0-3> stores the mode in the low 2
// bits and ORs in 0x80000000 to mark the value as explicitly configured, so a
// successful "disable" apply leaves the registry at 0x80000001, not 1. Both
// OptimizationVerifier.VerifyLastAccessDisabled() and StorageOptimizer.IsLastAccessDisabled()
// compared the raw DWORD to exactly 1 and always reported the optimization as inactive
// right after a successful apply. These tests pin down the corrected decoding logic.

using FluentAssertions;
using OmenCore.Services.SystemOptimizer;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class NtfsBehaviorFlagsTests
    {
        [Theory]
        [InlineData(1, true)] // disabled, never explicitly flagged (e.g. direct registry edit)
        [InlineData(2, true)] // system-managed disabled, not explicitly flagged
        [InlineData(unchecked((int)0x80000001), true)] // disabled, explicitly set via fsutil - the actual bug case
        [InlineData(unchecked((int)0x80000002), true)] // system-managed disabled, explicitly set via fsutil
        public void IsLastAccessDisabled_TrueForDisabledModes(int rawValue, bool expected)
        {
            NtfsBehaviorFlags.IsLastAccessDisabled(rawValue).Should().Be(expected);
        }

        [Theory]
        [InlineData(0)] // enabled
        [InlineData(3)] // enabled except on system volume
        [InlineData(unchecked((int)0x80000000))] // enabled, explicitly set via fsutil
        [InlineData(unchecked((int)0x80000003))] // enabled except system volume, explicitly set via fsutil
        public void IsLastAccessDisabled_FalseForEnabledModes(int rawValue)
        {
            NtfsBehaviorFlags.IsLastAccessDisabled(rawValue).Should().BeFalse();
        }

        [Fact]
        public void ExtractMode_IgnoresExplicitConfigurationFlag()
        {
            NtfsBehaviorFlags.ExtractMode(unchecked((int)0x80000001)).Should().Be(1);
            NtfsBehaviorFlags.ExtractMode(1).Should().Be(1);
        }
    }
}
