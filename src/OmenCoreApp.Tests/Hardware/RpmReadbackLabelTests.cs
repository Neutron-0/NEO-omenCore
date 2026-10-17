using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Models;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    public class RpmReadbackLabelTests
    {
        [Fact] public void LiveEstimateBeatsDatabaseClaim() =>
            RpmReadbackLabel.Describe(true, new[] { RpmSource.Estimated }).Should().Contain("Estimated");

        [Fact] public void LiveRealReadingBeatsDatabaseFalse() =>
            RpmReadbackLabel.Describe(false, new[] { RpmSource.WmiBios }).Should().Contain("Supported");

        [Theory] [InlineData(true, "Supported")] [InlineData(false, "Estimated")]
        public void NoLiveData_FallsBackToDatabase(bool db, string text) =>
            RpmReadbackLabel.Describe(db, new[] { RpmSource.Unknown }).Should().Contain(text);
    }
}
