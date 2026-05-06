using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services;
using OmenCore.Services.Diagnostics;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    /// <summary>
    /// Tests for DiagnosticExportService monitoring-cadence-hold snapshot content.
    /// Runs a real export with null optional services and inspects the resulting ZIP.
    /// </summary>
    public class DiagnosticExportSnapshotTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly LoggingService _logging;

        public DiagnosticExportSnapshotTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "OmenCoreDiagTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", _tempDir);

            _logging = new LoggingService();
            _logging.Initialize();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", null);
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task MonitoringCadenceHoldFile_IsIncludedInExport()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            zipPath.Should().NotBeNullOrEmpty();

            // The export might fall back to a directory if ZIP fails; handle both.
            if (File.Exists(zipPath))
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries
                    .FirstOrDefault(e => e.Name.Equals("monitoring-cadence-hold.txt", StringComparison.OrdinalIgnoreCase));
                entry.Should().NotBeNull("monitoring-cadence-hold.txt must be present in the diagnostic bundle");
            }
            else
            {
                var txtPath = Path.Combine(zipPath, "monitoring-cadence-hold.txt");
                File.Exists(txtPath).Should().BeTrue("monitoring-cadence-hold.txt must be written to the export folder");
            }
        }

        [Fact]
        public async Task ResourceFootprintFile_IsIncludedInExport()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            zipPath.Should().NotBeNullOrEmpty();

            if (File.Exists(zipPath))
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries
                    .FirstOrDefault(e => e.Name.Equals("resource-footprint.txt", StringComparison.OrdinalIgnoreCase));
                entry.Should().NotBeNull("resource-footprint.txt must be present in the diagnostic bundle");
            }
            else
            {
                var txtPath = Path.Combine(zipPath, "resource-footprint.txt");
                File.Exists(txtPath).Should().BeTrue("resource-footprint.txt must be written to the export folder");
            }
        }

        [Fact]
        public async Task ResourceFootprintFile_ContainsLightweightBaselineSections()
        {
            const string timerName = "Test_ResourceFootprint_Timer";
            BackgroundTimerRegistry.Register(
                timerName,
                "DiagnosticExportSnapshotTests",
                "resource footprint test timer",
                1234,
                BackgroundTimerTier.Optional);

            try
            {
                var svc = new DiagnosticExportService(_logging, _tempDir);
                var zipPath = await svc.CollectAndExportAsync();

                string content = ReadFileFromExport(zipPath, "resource-footprint.txt");

                content.Should().Contain("RESOURCE FOOTPRINT SNAPSHOT");
                content.Should().Contain("[OmenCore App]");
                content.Should().Contain("AverageCpuPercentSinceStart");
                content.Should().Contain("[Hardware Worker Processes]");
                content.Should().Contain("[Monitoring Cadence]");
                content.Should().Contain("Monitoring service unavailable");
                content.Should().Contain("[Fan Activity Blockers]");
                content.Should().Contain("Fan service unavailable");
                content.Should().Contain("[Background Timers]");
                content.Should().Contain(timerName);
                content.Should().Contain("[Managed Runtime]");
                content.Should().Contain("[Optional Subsystem Load Hints]");
            }
            finally
            {
                BackgroundTimerRegistry.Unregister(timerName);
            }
        }

        [Fact]
        public async Task MonitoringCadenceHoldFile_ContainsExpectedSections_WhenServicesUnavailable()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "monitoring-cadence-hold.txt");

            content.Should().Contain("MONITORING CADENCE + FAN HOLD SNAPSHOT",
                "header section marker must be present");
            content.Should().Contain("Monitoring service unavailable",
                "null HardwareMonitoringService must produce an explicit unavailability notice");
            content.Should().Contain("Fan service unavailable",
                "null FanService must produce an explicit unavailability notice");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static string ReadFileFromExport(string exportPath, string fileName)
        {
            if (File.Exists(exportPath))
            {
                using var archive = ZipFile.OpenRead(exportPath);
                var entry = archive.Entries
                    .First(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            else
            {
                return File.ReadAllText(Path.Combine(exportPath, fileName));
            }
        }
    }
}
