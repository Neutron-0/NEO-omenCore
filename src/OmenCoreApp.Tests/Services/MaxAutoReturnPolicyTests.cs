using System;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class MaxAutoReturnPolicyTests
    {
        private static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Off_NeverReturns()
        {
            var p = new MaxAutoReturnPolicy();

            p.ShouldReturn(T0, true, 40, MaxAutoReturnMode.Off, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddHours(5), true, 40, MaxAutoReturnMode.Off, 15).Should().BeFalse();
        }

        [Fact]
        public void Cooldown_ReturnsAfterTwoMinutesUnderSixty()
        {
            var p = new MaxAutoReturnPolicy();

            p.ShouldReturn(T0, true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddSeconds(119), true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddSeconds(120), true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeTrue();
        }

        [Fact]
        public void Cooldown_ResetsIfTheTemperatureRises()
        {
            var p = new MaxAutoReturnPolicy();
            p.ShouldReturn(T0, true, 55, MaxAutoReturnMode.Cooldown, 15);
            p.ShouldReturn(T0.AddSeconds(100), true, 61, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();

            // 100 s of cool time before the spike no longer counts: it must be a fresh two minutes.
            p.ShouldReturn(T0.AddSeconds(110), true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddSeconds(229), true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddSeconds(230), true, 55, MaxAutoReturnMode.Cooldown, 15).Should().BeTrue();
        }

        [Fact]
        public void Cooldown_AnUnknownReadingIsNeverCool()
        {
            var p = new MaxAutoReturnPolicy();

            p.ShouldReturn(T0, true, 0, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddMinutes(10), true, 0, MaxAutoReturnMode.Cooldown, 15).Should().BeFalse();
        }

        [Fact]
        public void Timer_ReturnsAfterTheConfiguredMinutes_EvenWhenHot()
        {
            var p = new MaxAutoReturnPolicy();

            p.ShouldReturn(T0, true, 90, MaxAutoReturnMode.Timer, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddMinutes(14), true, 90, MaxAutoReturnMode.Timer, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddMinutes(15), true, 90, MaxAutoReturnMode.Timer, 15).Should().BeTrue();
        }

        [Fact]
        public void Timer_RestartsWhenMaxIsTurnedOffAndOnAgain()
        {
            var p = new MaxAutoReturnPolicy();
            p.ShouldReturn(T0, true, 70, MaxAutoReturnMode.Timer, 15);

            p.ShouldReturn(T0.AddMinutes(10), false, 70, MaxAutoReturnMode.Timer, 15).Should().BeFalse(); // Max was switched off
            p.ShouldReturn(T0.AddMinutes(12), true, 70, MaxAutoReturnMode.Timer, 15).Should().BeFalse(); // switched on again: a new timer
            p.ShouldReturn(T0.AddMinutes(26), true, 70, MaxAutoReturnMode.Timer, 15).Should().BeFalse();
            p.ShouldReturn(T0.AddMinutes(27), true, 70, MaxAutoReturnMode.Timer, 15).Should().BeTrue();
        }
    }
}
