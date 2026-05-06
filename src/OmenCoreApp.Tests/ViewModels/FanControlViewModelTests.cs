using System;
using System.IO;
using FluentAssertions;
using OmenCore.Models;
using OmenCore.Services;
using OmenCore.Services.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace OmenCoreApp.Tests.ViewModels
{
    [Collection("Config Isolation")]
    public class FanControlViewModelTests
    {
        public FanControlViewModelTests()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "OmenCoreTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tmp);
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", tmp);
        }
        private class TestFanController : OmenCore.Hardware.IFanController
        {
            public bool IsAvailable => true;
            public string Status => "Test";
            public string Backend => "Test";
            public int LastSetPercent { get; private set; } = -1;
            public int SetCallCount { get; private set; } = 0;

            public bool ApplyPreset(FanPreset preset)
            {
                return true;
            }

            public bool ApplyCustomCurve(System.Collections.Generic.IEnumerable<FanCurvePoint> curve) => true;
            public bool SetFanSpeed(int percent) { LastSetPercent = percent; SetCallCount++; return true; }
            public bool SetFanSpeeds(int cpuPercent, int gpuPercent) { LastSetPercent = System.Math.Max(cpuPercent, gpuPercent); SetCallCount++; return true; }
            public bool SetMaxFanSpeed(bool enabled) => true;
            public bool SetPerformanceMode(string modeName) => true;
            public bool RestoreAutoControl() => true;
            public System.Collections.Generic.IEnumerable<FanTelemetry> ReadFanSpeeds() => new System.Collections.Generic.List<FanTelemetry>();
            public void ApplyMaxCooling() { LastSetPercent = 100; SetCallCount++; }
            public void ApplyAutoMode() { }
            public void ApplyQuietMode() { }
            public bool ResetEcToDefaults() => true;
            public bool ApplyThrottlingMitigation() => true;
            public void Dispose() { }
            public bool VerifyMaxApplied(out string details) { details = ""; return true; }
        }

        private sealed class TestFanVerificationService : IFanVerificationService
        {
            public bool IsAvailable { get; set; }

            public Task<FanApplyResult> ApplyAndVerifyFanSpeedAsync(int fanIndex, int targetPercent, System.Threading.CancellationToken ct = default)
                => Task.FromResult(new FanApplyResult());

            public Task<FanApplyResult> ApplyWithEnhancedVerificationAsync(int fanIndex, int targetPercent, bool autoRevertOnFailure = true, System.Threading.CancellationToken ct = default)
                => Task.FromResult(new FanApplyResult());

            public Task<FanCalibrationResult> PerformFanCalibrationAsync(int fanIndex, System.Threading.CancellationToken ct = default)
                => Task.FromResult(new FanCalibrationResult());

            public (int rpm, int level) GetCurrentFanState(int fanIndex) => (0, 0);

            public (int rpm, int level, RpmSource source) GetCurrentFanStateWithSource(int fanIndex) => (0, 0, RpmSource.Estimated);

            public Task<(int avg, int min, int max)> GetStableFanRpmAsync(int fanIndex, int samples = 3, System.Threading.CancellationToken ct = default)
                => Task.FromResult((0, 0, 0));
        }

        private static OmenCore.ViewModels.FanControlViewModel CreateViewModel(IFanVerificationService? verificationService = null)
        {
            var logging = new LoggingService();
            logging.Initialize();

            var configService = new ConfigurationService();
            var hwMonitor = new OmenCore.Hardware.LibreHardwareMonitorImpl();
            var thermalProvider = new OmenCore.Hardware.ThermalSensorProvider(hwMonitor);
            var controller = new TestFanController();
            var notificationService = new NotificationService(logging);
            var fanService = new FanService(controller, thermalProvider, logging, notificationService, 1000, new ResumeRecoveryDiagnosticsService());

            return new OmenCore.ViewModels.FanControlViewModel(fanService, configService, logging, verificationService);
        }

        private static FanService GetFanService(OmenCore.ViewModels.FanControlViewModel vm)
        {
            var field = typeof(OmenCore.ViewModels.FanControlViewModel)
                .GetField("_fanService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.Should().NotBeNull();
            return field!.GetValue(vm).Should().BeOfType<FanService>().Subject;
        }

        [Fact]
        public void SettingTransitionProperties_PersistsToConfig_And_AppliesToService()
        {
            var logging = new LoggingService();
            logging.Initialize();

            var configService = new ConfigurationService();
            var hwMonitor = new OmenCore.Hardware.LibreHardwareMonitorImpl();
            var thermalProvider = new OmenCore.Hardware.ThermalSensorProvider(hwMonitor);
            var controller = new TestFanController();
            var notificationService = new NotificationService(logging);
            var fanService = new FanService(controller, thermalProvider, logging, notificationService, 1000, new ResumeRecoveryDiagnosticsService());

            var vm = new OmenCore.ViewModels.FanControlViewModel(fanService, configService, logging)
            {
                SmoothingDurationMs = 1500,
                SmoothingStepMs = 100,
                ImmediateApplyOnApply = true
            };

            var loaded = configService.Load();
            loaded.FanTransition.SmoothingDurationMs.Should().Be(1500);
            loaded.FanTransition.SmoothingStepMs.Should().Be(100);
            loaded.FanTransition.ApplyImmediatelyOnUserAction.Should().BeTrue();

            // FanService should reflect the settings
            fanService.SmoothingDurationMs.Should().Be(1500);
            fanService.SmoothingStepMs.Should().Be(100);

            logging.Dispose();
        }

        [Fact]
        public void FanCalibrationStatusText_WhenVerificationServiceMissing_ShowsInitializationReason()
        {
            var vm = CreateViewModel();

            vm.IsFanCalibrationAvailable.Should().BeFalse();
            vm.FanCalibrationStatusText.Should().Contain("not initialized");
            vm.FanCalibrationUnavailableReason.Should().Contain("not initialized");
        }

        [Fact]
        public void FanCalibrationStatusText_WhenVerificationBackendInactive_ShowsBackendContext()
        {
            var verifier = new TestFanVerificationService { IsAvailable = false };
            var vm = CreateViewModel(verifier);

            vm.IsFanCalibrationAvailable.Should().BeFalse();
            vm.FanCalibrationStatusText.Should().Contain("backend is inactive");
            vm.FanCalibrationStatusText.Should().Contain("active fan backend");
        }

        [Fact]
        public void FanCalibrationUnavailableReason_WhenVerificationAvailable_ReportsAvailable()
        {
            var verifier = new TestFanVerificationService { IsAvailable = true };
            var vm = CreateViewModel(verifier);

            vm.IsFanCalibrationAvailable.Should().BeTrue();
            vm.FanCalibrationUnavailableReason.Should().Contain("available");
        }

        [Fact]
        public void QuickFanModeCommands_DoNotChangeUiState_DuringDiagnosticMode()
        {
            var vm = CreateViewModel();
            var fanService = GetFanService(vm);

            fanService.EnterDiagnosticMode();
            try
            {
                var beforeMode = vm.ActiveFanMode;

                vm.ApplyGamingModeCommand.Execute(null);
                vm.ApplyFanMode("Extreme");
                vm.ApplyQuietModeCommand.Execute(null);

                vm.ActiveFanMode.Should().Be(beforeMode,
                    "quick fan mode commands should be ignored while diagnostics own the fans");
            }
            finally
            {
                fanService.ExitDiagnosticMode();
            }
        }
    }
}
