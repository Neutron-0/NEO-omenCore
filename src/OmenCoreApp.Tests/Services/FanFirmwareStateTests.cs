using System;
using System.Text;
using FluentAssertions;
using OmenCore.Services.Diagnostics;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class FanFirmwareStateTests
    {
        [Fact]
        public void Section_RendersEachProbe()
        {
            var sb = new StringBuilder();

            DiagnosticExportService.AppendFanFirmwareState(sb, new (string, Func<string?>)[]
            {
                ("FirmwareMaxFlag", () => "True"),
                ("FanLevelReadback", () => "fan1=20 fan2=20"),
            });

            var text = sb.ToString();
            text.Should().Contain("[Fan Firmware State]");
            text.Should().Contain("FirmwareMaxFlag: True");
            text.Should().Contain("FanLevelReadback: fan1=20 fan2=20");
        }

        [Fact]
        public void Section_ANullAnswerIsUnavailable_AThrowingProbeIsOneLine_AndTheRestStillRender()
        {
            var sb = new StringBuilder();

            DiagnosticExportService.AppendFanFirmwareState(sb, new (string, Func<string?>)[]
            {
                ("ThermalPolicy", () => null),
                ("FirmwareMaxFlag", () => throw new InvalidOperationException("WMI refused")),
                ("MaxFanLevel", () => "55"),
            });

            var text = sb.ToString();
            text.Should().Contain("ThermalPolicy: <unavailable>");
            text.Should().Contain("FirmwareMaxFlag: <read failed: WMI refused>");
            text.Should().Contain("MaxFanLevel: 55");
        }
    }
}
