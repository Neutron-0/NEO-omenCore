using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Models;
using OmenCore.Services;
using OmenCore.Services.Diagnostics;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RuntimePollingCoordinatorTests
    {
        private sealed class AdaptiveBridgeSpy : IHardwareMonitorBridge, IAdaptiveSamplingBridge
        {
            public bool StaticTraySamplingEnabled { get; private set; }
            public int StaticTraySamplingModeSetCalls { get; private set; }
            public string MonitoringSource => "AdaptiveBridgeSpy";

            public Task<MonitoringSample> ReadSampleAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new MonitoringSample
                {
                    CpuTemperatureC = 50,
                    GpuTemperatureC = 55,
                    CpuLoadPercent = 20,
                    GpuLoadPercent = 25
                });
            }

            public Task<bool> TryRestartAsync() => Task.FromResult(true);

            public void SetStaticTraySamplingMode(bool enabled)
            {
                StaticTraySamplingModeSetCalls++;
                StaticTraySamplingEnabled = enabled;
            }
        }

        [Fact]
        public void Coordinator_EnablesAndDisablesStaticTraySampling_AsCadenceStateChanges()
        {
            var bridge = new AdaptiveBridgeSpy();
            var logging = new LoggingService();
            logging.Initialize();
            var monitoring = new HardwareMonitoringService(
                bridge,
                logging,
                new MonitoringPreferences(),
                new ResumeRecoveryDiagnosticsService());
            var coordinator = new RuntimePollingCoordinator(monitoring, logging);

            coordinator.SetLowOverheadMode(true);
            coordinator.SetUiWindowActive(false);
            coordinator.SetTrayOnlyMode(true);

            bridge.StaticTraySamplingEnabled.Should().BeTrue();

            coordinator.SetOverlayRealtimeMode(true);
            bridge.StaticTraySamplingEnabled.Should().BeFalse();

            coordinator.SetOverlayRealtimeMode(false);
            bridge.StaticTraySamplingEnabled.Should().BeTrue();
        }

        [Fact]
        public void Coordinator_SkipsDuplicateModeWrites_ForLowOverheadFlag()
        {
            var bridge = new AdaptiveBridgeSpy();
            var logging = new LoggingService();
            logging.Initialize();
            var monitoring = new HardwareMonitoringService(
                bridge,
                logging,
                new MonitoringPreferences(),
                new ResumeRecoveryDiagnosticsService());
            var coordinator = new RuntimePollingCoordinator(monitoring, logging);

            coordinator.SetLowOverheadMode(true);
            var callsAfterFirstEnable = bridge.StaticTraySamplingModeSetCalls;

            coordinator.SetLowOverheadMode(true);

            bridge.StaticTraySamplingModeSetCalls.Should().Be(callsAfterFirstEnable);
        }
    }
}
