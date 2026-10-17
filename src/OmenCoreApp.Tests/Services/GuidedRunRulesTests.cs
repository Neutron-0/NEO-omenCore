using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class GuidedRunRulesTests
    {
        [Theory]
        [InlineData(false, true, true)]   // failed while thermal protection was active: interrupted, not a fan failure
        [InlineData(false, false, false)] // a plain failure stays a failure
        [InlineData(true, true, false)]   // a pass is a pass even if protection was also active
        [InlineData(true, false, false)]
        public void InterruptedOnlyWhenAFailureCoincidesWithThermalProtection(bool passed, bool protection, bool expected)
        {
            GuidedRunRules.IsInterruptedByThermalProtection(passed, protection).Should().Be(expected);
        }

        [Theory]
        [InlineData(84.9, false)]
        [InlineData(85.0, true)]
        [InlineData(96.0, true)]
        [InlineData(0.0, false)] // unknown reading does not block
        public void RunWaitsForCoolDownAtEightyFiveDegrees(double hottest, bool expected)
        {
            GuidedRunRules.NeedsCoolDown(hottest).Should().Be(expected);
        }
    }
}
