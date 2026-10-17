using System.Collections.Generic;
using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Models;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    /// <summary>
    /// #227 (8BC2), #226 (8BD4), #217 (88EE), #143 (8DCD): after OmenCore hands fans back to BIOS Auto on a V1 board that skips the
    /// (0, 0) floor clear, the level-20 hint is never undone and the fans sit near 2000 RPM at 80 to 96 C. The opt-in switch skips it.
    /// </summary>
    public class WmiFanControllerLevelHintTests
    {
        private sealed class Bios : IHpWmiBios
        {
            private bool _max;
            public List<(byte, byte)> LevelCalls { get; } = new();
            public bool IsAvailable => true;
            public string Status => "fake";
            public HpWmiBios.ThermalPolicyVersion ThermalPolicy => HpWmiBios.ThermalPolicyVersion.V1;
            public int FanCount => 2;
            public int MaxFanLevel => 55;
            public (int fan1Rpm, int fan2Rpm)? GetFanRpmDirect() => null;
            public (byte fan1, byte fan2)? GetFanLevel() => _max ? ((byte)55, (byte)55) : ((byte)20, (byte)20);
            public bool SetFanMax(bool enabled) { _max = enabled; return true; }
            public bool SetFanLevel(byte fan1, byte fan2) { LevelCalls.Add((fan1, fan2)); return true; }
            public bool SetFanMode(HpWmiBios.FanMode mode) => true;
            public double? GetTemperature() => 45.0;
            public double? GetGpuTemperature() => 50.0;
            public void ExtendFanCountdown() { }
            public (bool customTgp, bool ppab, int dState)? GetGpuPower() => null;
            public bool SetGpuPower(HpWmiBios.GpuPowerLevel level) => true;
            public HpWmiBios.GpuMode? GetGpuMode() => null;
            public void Dispose() { }
        }

        private static List<(byte, byte)> LevelCallsAfterMaxThenAuto(bool skipHint)
        {
            var bios = new Bios();
            using var controller = new WmiFanController(null, null, 0, injectedWmiBios: bios,
                strictFanModeReadback: false, allowV1AutoModeFloorClear: false, skipV1LevelHint: skipHint);

            controller.ApplyPreset(new FanPreset { Name = "Max" });
            bios.LevelCalls.Clear();
            controller.ApplyPreset(new FanPreset { Name = "Auto", Mode = FanMode.Auto });
            return bios.LevelCalls;
        }

        [Fact]
        public void Default_StillSendsTheLevelTwentyHint_ToKeepCurrentBehaviour()
        {
            LevelCallsAfterMaxThenAuto(skipHint: false).Should().Contain(((byte)20, (byte)20));
        }

        [Fact]
        public void OptIn_SendsNoFanLevelWriteWhenHandingBackToAuto()
        {
            LevelCallsAfterMaxThenAuto(skipHint: true).Should().BeEmpty(
                "a manual level left behind is what pins the fans while the BIOS is meant to be in charge");
        }
    }
}
