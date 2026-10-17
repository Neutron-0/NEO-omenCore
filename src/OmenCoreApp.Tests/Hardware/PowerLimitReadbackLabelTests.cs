using FluentAssertions;
using OmenCore.Hardware;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    public class PowerLimitReadbackLabelTests
    {
        [Fact]
        public void ZeroReadback_BeatsDatabaseSupported() =>
            PowerLimitReadbackLabel.Describe(true, true).Should().Contain("Firmware-managed");

        [Fact]
        public void NormalReadback_KeepsSupported() =>
            PowerLimitReadbackLabel.Describe(true, false).Should().Contain("Supported");

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DatabaseUnsupported_StaysUnsupported(bool zero) =>
            PowerLimitReadbackLabel.Describe(false, zero).Should().Contain("Not supported");
    }
}
