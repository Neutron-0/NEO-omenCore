using FluentAssertions;
using OmenCore.Utils;
using Xunit;

namespace OmenCoreApp.Tests.Utils
{
    /// <summary>GitHub #142: a profile set to Extended showed Medium in the tray because the menu had no Extended item.</summary>
    public class TrayGpuPowerHeaderTests
    {
        [Fact]
        public void ExtendedItem_IsTicked_OnlyWhenExtendedIsActive()
        {
            TrayIconService.GpuPowerExtendedHeader("Extended").Should().StartWith("✓").And.Contain("Extended");
            TrayIconService.GpuPowerExtendedHeader("Medium").Should().NotContain("✓").And.Contain("Extended");
        }
    }
}
