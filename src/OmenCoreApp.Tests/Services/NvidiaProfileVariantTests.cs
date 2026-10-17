using FluentAssertions;
using NvpwrControlBlackwell;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    /// <summary>
    /// The HP OMEN board with an RTX 5070 Ti Laptop (PCI subsystem 8E71103C) has a 115 W stock limit. The Prophecy standalone 1.2.5
    /// found it; the generic profile says 140 W, which is the "115 W against 140 W" case in the roadmap's K.7.
    /// </summary>
    public class NvidiaProfileVariantTests
    {
        private const string Omen = @"PCI\VEN_10DE&DEV_2F58&SUBSYS_8E71103C";
        private const string OtherBoard = @"PCI\VEN_10DE&DEV_2F58&SUBSYS_1234103C";
        private const string Reference = @"PCI\VEN_10DE&DEV_2F58&SUBSYS_803F17AA";

        [Fact]
        public void HpOmen5070Ti_UsesA115WattStock()
        {
            var p = GpuProfiles.Detect("NVIDIA GeForce RTX 5070 Ti Laptop GPU", Omen);

            p.Should().NotBeNull();
            p!.StockPowerW.Should().Be(115);
            p.MinW.Should().Be(115);
            p.IsInRange(115).Should().BeTrue();
            p.IsReferenceValidated(140).Should().BeFalse();
            p.DisplayName.Should().Contain("HP OMEN 115 W");
        }

        [Theory]
        [InlineData(OtherBoard)]
        [InlineData(Reference)]
        public void OtherRtx5070TiBoards_KeepTheGenericStock(string pnp)
        {
            var p = GpuProfiles.Detect("NVIDIA GeForce RTX 5070 Ti Laptop GPU", pnp);

            p.Should().NotBeNull();
            p!.StockPowerW.Should().Be(140);
            p.MinW.Should().Be(140);
        }

        [Fact]
        public void TheVariantDoesNotLeakToOtherGpus()
        {
            // Same subsystem id on a different GPU model must not pick up the 5070 Ti variant.
            var p = GpuProfiles.Detect("NVIDIA GeForce RTX 5060 Laptop GPU", @"PCI\VEN_10DE&DEV_2D19&SUBSYS_8E71103C");

            (p == null || p.Id != "5070ti").Should().BeTrue();
        }
    }
}
