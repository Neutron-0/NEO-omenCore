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

        [Fact]
        public void PowerTransitions_ListEventsWithNamesAndReasons()
        {
            var sb = new StringBuilder();
            var t = new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);

            DiagnosticExportService.AppendPowerTransitions(sb, () => new[]
            {
                new DiagnosticExportService.PowerTransition(t, 42, "Sleep Reason: Button or Lid"),
                new DiagnosticExportService.PowerTransition(t.AddHours(1), 107, "Wake Source: Power Button"),
            });

            var text = sb.ToString();
            text.Should().Contain("[Power Transitions]");
            text.Should().Contain("42 entering sleep | Sleep Reason: Button or Lid");
            text.Should().Contain("107 resumed from sleep | Wake Source: Power Button");
        }

        [Fact]
        public void PowerTransitions_EmptyAndThrowing_AreOneLineEach()
        {
            var empty = new StringBuilder();
            DiagnosticExportService.AppendPowerTransitions(empty, () => Array.Empty<DiagnosticExportService.PowerTransition>());
            empty.ToString().Should().Contain("none recorded");

            var failing = new StringBuilder();
            DiagnosticExportService.AppendPowerTransitions(failing, () => throw new UnauthorizedAccessException("denied"));
            failing.ToString().Should().Contain("<read failed: denied>");
        }

        [Fact]
        public void CpuThrottle_IsReservedUntilItExists()
        {
            var sb = new StringBuilder();
            DiagnosticExportService.AppendCpuThrottle(sb);
            sb.ToString().Should().Contain("[CPU Throttle]").And.Contain("not available");
        }
    }
}
