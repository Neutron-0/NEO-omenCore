using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using OmenCore.Models;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class PowerAutomationServiceTests
    {
        [Fact]
        public void BuiltInPerformanceCurve_ReservesMaxForHighThermals()
        {
            var curve = FanModeNameResolver.BuildBuiltInCurve("Performance", FanMode.Performance)
                .OrderBy(p => p.TemperatureC)
                .ToList();

            curve.Where(p => p.TemperatureC <= 80).Should().OnlyContain(p => p.FanPercent < 90,
                "power automation fallback Performance/Gaming should not silently behave like Max at moderate temperatures");
            curve.Single(p => p.FanPercent == 100).TemperatureC.Should().BeGreaterThanOrEqualTo(90);
        }
    }
}
