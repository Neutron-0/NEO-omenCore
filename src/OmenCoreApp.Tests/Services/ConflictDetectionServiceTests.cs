using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class ConflictDetectionServiceTests
    {
        [Fact]
        public void KnownConflicts_IncludesOhman_AsAFanAndPerformanceWriter()
        {
            ConflictDetectionService.KnownConflicts.Should().ContainKey("Ohman");

            var ohman = ConflictDetectionService.KnownConflicts["Ohman"];
            ohman.ProcessNames.Should().Contain("Ohman");
            ohman.Impact.Should().Be(ConflictSeverity.Medium);
        }

        [Fact]
        public void KnownConflicts_KeepsOmenGamingHub()
        {
            ConflictDetectionService.KnownConflicts["OmenHub"].ProcessNames.Should().Contain("OGHAgent");
        }
    }
}
