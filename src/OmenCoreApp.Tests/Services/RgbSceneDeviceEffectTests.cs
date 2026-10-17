using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Models;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RgbSceneDeviceEffectTests
    {
        [Theory]
        [InlineData(RgbSceneEffect.Wave, DojoKeyboardMcu.Effect.Wave)]
        [InlineData(RgbSceneEffect.Spectrum, DojoKeyboardMcu.Effect.ColorCycle)]
        [InlineData(RgbSceneEffect.Breathing, DojoKeyboardMcu.Effect.Breathing)]
        public void AnimatedScenes_MapToTheKeyboardsOwnEffect(RgbSceneEffect effect, DojoKeyboardMcu.Effect expected)
        {
            // 8E9A (#142): Wave and Spectrum scenes used to arrive as a plain colour on the HP keyboard.
            var scene = new RgbScene { Effect = effect, PrimaryColor = "#FF8000", Speed = 80 };

            RgbSceneService.TryBuildDeviceEffect(scene, out var record).Should().BeTrue();

            record.Effect.Should().Be(expected);
            record.Speed.Should().Be(DojoKeyboardMcu.EffectSpeed.Fast);
            record.Colors.Should().Equal(((byte)255, (byte)128, (byte)0));
        }

        [Theory]
        [InlineData(RgbSceneEffect.Static)]
        [InlineData(RgbSceneEffect.Gradient)]
        [InlineData(RgbSceneEffect.Off)]
        [InlineData(RgbSceneEffect.Ambient)]
        public void ColourScenes_StayOnTheStaticFill(RgbSceneEffect effect)
        {
            RgbSceneService.TryBuildDeviceEffect(new RgbScene { Effect = effect }, out _).Should().BeFalse();
        }
    }
}
