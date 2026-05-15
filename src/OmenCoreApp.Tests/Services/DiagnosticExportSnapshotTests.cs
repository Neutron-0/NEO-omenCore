using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Models;
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
        public async Task RgbControlPathFile_IsIncludedInExport()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            zipPath.Should().NotBeNullOrEmpty();

            if (File.Exists(zipPath))
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries
                    .FirstOrDefault(e => e.Name.Equals("rgb-control-path.txt", StringComparison.OrdinalIgnoreCase));
                entry.Should().NotBeNull("rgb-control-path.txt must be present in the diagnostic bundle");
            }
            else
            {
                var txtPath = Path.Combine(zipPath, "rgb-control-path.txt");
                File.Exists(txtPath).Should().BeTrue("rgb-control-path.txt must be written to the export folder");
            }
        }

        [Fact]
        public async Task TuningSafetyFile_IsIncludedInExport()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            zipPath.Should().NotBeNullOrEmpty();

            if (File.Exists(zipPath))
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries
                    .FirstOrDefault(e => e.Name.Equals("tuning-safety.txt", StringComparison.OrdinalIgnoreCase));
                entry.Should().NotBeNull("tuning-safety.txt must be present in the diagnostic bundle");
            }
            else
            {
                var txtPath = Path.Combine(zipPath, "tuning-safety.txt");
                File.Exists(txtPath).Should().BeTrue("tuning-safety.txt must be written to the export folder");
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

        [Fact]
        public async Task RgbControlPathFile_ContainsExpectedSections_WhenServicesUnavailable()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "rgb-control-path.txt");

            content.Should().Contain("RGB CONTROL PATH SNAPSHOT");
            content.Should().Contain("[HP Keyboard]");
            content.Should().Contain("Keyboard lighting service unavailable.");
            content.Should().Contain("[External RGB Providers]");
            content.Should().Contain("RGB manager unavailable or not initialized yet.");
            content.Should().Contain("[Known HP/OMEN RGB Conflict Processes]");
        }

        [Fact]
        public async Task TuningSafetyFile_ContainsSavedSafetyStateWithoutHardwareProviders()
        {
            var config = DefaultConfiguration.Create();
            config.EnableStartupHardwareRestore = true;
            config.AllowStartupRestoreOnOmen16OrVictus = false;
            config.LastPerformanceModeName = "Performance";
            config.LastGpuPowerBoostLevel = "Maximum";
            config.LastCpuPl1Watts = 55;
            config.LastCpuPl2Watts = 80;
            config.LastTccOffset = 10;
            config.Undervolt = new UndervoltPreferences
            {
                ApplyOnStartup = true,
                PendingTestApply = true,
                StartupPendingConfirmation = true,
                RespectExternalControllers = true,
                DefaultOffset = new UndervoltOffset { CoreMv = -42, CacheMv = -30 },
                EnablePerCoreUndervolt = true,
                PerCoreOffsetsMv = new int?[] { -15, null, -20 },
                LastConfirmedOffset = new UndervoltOffset { CoreMv = -35, CacheMv = -25 },
                LastConfirmedAtUtc = new DateTime(2026, 5, 8, 1, 2, 3, DateTimeKind.Utc)
            };
            config.GpuOc = new GpuOcSettings
            {
                ApplyOnStartup = true,
                PendingTestApply = true,
                StartupPendingConfirmation = true,
                CoreClockOffsetMHz = 100,
                MemoryClockOffsetMHz = 250,
                PowerLimitPercent = 110,
                VoltageOffsetMv = 5,
                LastConfirmedCoreClockOffsetMHz = 80,
                LastConfirmedMemoryClockOffsetMHz = 200,
                LastConfirmedPowerLimitPercent = 105,
                LastConfirmedVoltageOffsetMv = 0,
                LastConfirmedAtUtc = new DateTime(2026, 5, 8, 4, 5, 6, DateTimeKind.Utc)
            };
            config.LastGpuOcProfileName = "Gaming";
            config.AmdPowerLimits = new AmdPowerLimits { StapmLimitWatts = 20, TempLimitC = 95 };
            new ConfigurationService().Save(config);

            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "tuning-safety.txt");

            content.Should().Contain("TUNING SAFETY SNAPSHOT");
            content.Should().Contain("Purpose: Export saved tuning safety state without waking System Control");
            content.Should().Contain("EnableStartupHardwareRestore: yes");
            content.Should().Contain("LastGpuPowerBoostLevel: Maximum");
            content.Should().Contain("LastCpuPl1Watts: 55 W");
            content.Should().Contain("RecoveryRequiredOnNextStartup: yes");
            content.Should().Contain("SavedDefaultOffset: Core -42 mV, Cache -30 mV");
            content.Should().Contain("SavedPerCoreOffsets: 2/3 active");
            content.Should().Contain("SavedCoreClockOffsetMHz: +100");
            content.Should().Contain("SavedPowerLimitPercent: 110%");
            content.Should().Contain("SelectedGpuOcProfile: Gaming");
            content.Should().Contain("StapmLimitWatts: 20 W");
        }

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

        /// <summary>
        /// Issue #129/#128/#130 + RC Diagnostics: Bounded performance snapshot must include
        /// amplification classification (Dispatcher and Projection) for triage
        /// so field analysts can quickly assess CPU dispatch overhead.
        /// </summary>
        [Fact]
        public async Task BoundedPerformanceFile_IncludesAmplificationClassification()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "runtime-performance-bounded.txt");

            content.Should().Contain("DispatcherAmplificationClass:", "dispatcher amplification classification required");
            content.Should().Contain("ProjectionAmplificationClass:", "projection amplification classification required");
            // Classes should be something like "Nominal", "Elevated", or "High"
            (content.Contains("Nominal") || content.Contains("Elevated") || content.Contains("High"))
                .Should().BeTrue("amplification should have a classification value");
        }

        /// <summary>
        /// Issue #129/#128/#130 + RC Diagnostics: Bounded snapshot must include
        /// acceptance ratio classifications (Dashboard, General, Main) for acceptance quality assessment.
        /// </summary>
        [Fact]
        public async Task BoundedPerformanceFile_IncludesAcceptanceClassification()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "runtime-performance-bounded.txt");

            content.Should().Contain("MainProjectionAcceptanceClass:", "main acceptance classification required");
            content.Should().Contain("DashboardAcceptanceClass:", "dashboard acceptance classification required");
            content.Should().Contain("GeneralAcceptanceClass:", "general acceptance classification required");
        }

        /// <summary>
        /// Issue #129/#128/#130 + RC Diagnostics: Bounded snapshot must include
        /// cache-hit ratio classifications (Tray and Popup) for rendering optimization assessment.
        /// </summary>
        [Fact]
        public async Task BoundedPerformanceFile_IncludesCacheHitClassification()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "runtime-performance-bounded.txt");

            content.Should().Contain("TrayRenderCacheClass:", "tray cache hit classification required");
            content.Should().Contain("PopupRenderCacheClass:", "popup cache hit classification required");
            content.Should().Contain("TrayRenderCacheHitRatio:", "tray cache hit ratio required");
            content.Should().Contain("PopupRenderCacheHitRatio:", "popup cache hit ratio required");
        }

        /// <summary>
        /// Issue #129/#128/#130 + RC Diagnostics: Bounded snapshot must include
        /// scenario assessment to contextualize performance readings (e.g., "Minimized", "Focused", "Tray").
        /// </summary>
        [Fact]
        public async Task BoundedPerformanceFile_IncludesScenarioAssessment()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "runtime-performance-bounded.txt");

            content.Should().Contain("ScenarioAssessment:", "scenario assessment required for context");
            // The assessment should have some description (even if it's empty or "unknown")
            var assessmentLine = content.Split('\n').FirstOrDefault(l => l.Contains("ScenarioAssessment:"));
            assessmentLine.Should().NotBeNull();
        }

        /// <summary>
        /// Issue #129: MonitoringSource in the bounded snapshot must include CPU thermal authority
        /// when collected, so field diagnostics can correlate performance with authority state.
        /// </summary>
        [Fact]
        public async Task BoundedPerformanceFile_MonitoringSource_IncludesCpuAuthority()
        {
            var svc = new DiagnosticExportService(_logging, _tempDir);
            var zipPath = await svc.CollectAndExportAsync();

            string content = ReadFileFromExport(zipPath, "runtime-performance-bounded.txt");

            // MonitoringSource line should be present and contain CPU Authority info
            var monitoringLine = content.Split('\n').FirstOrDefault(l => l.Contains("MonitoringSource:"));
            monitoringLine.Should().NotBeNull("MonitoringSource must be in runtime state summary");
            monitoringLine.Should().Contain("CPU Authority", "CPU thermal authority source must be included in MonitoringSource");
        }
    }
}
