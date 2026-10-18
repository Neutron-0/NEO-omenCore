using FluentAssertions;
using OmenCore.Services.KeyboardLighting;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class HidCandidateFormatTests
    {
        [Fact]
        public void CandidateLine_CarriesIdsNameAndReportLengths()
        {
            HidPerKeyBackend.FormatCandidate(0x03F0, 0x0547, "OMEN keyboard", 65, 65, 0)
                .Should().Be("03F0:0547 'OMEN keyboard' in=65 out=65 feature=0");
            HidPerKeyBackend.FormatCandidate(0x048D, 0x5702, null, 0, -1, 0).Should().Contain("'unknown'").And.Contain("out=-1");
        }
    }
}
