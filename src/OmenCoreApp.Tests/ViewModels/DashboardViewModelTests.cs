using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Models;
using OmenCore.Services;
using OmenCore.Services.Diagnostics;
using OmenCore.ViewModels;
using Xunit;

namespace OmenCoreApp.Tests.ViewModels
{
    [Collection("Config Isolation")]
    public class DashboardViewModelTests
    {
        private sealed class MonitoringBridgeStub : IHardwareMonitorBridge
        {
            public string MonitoringSource => "DashboardTestStub";

            public Task<MonitoringSample> ReadSampleAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new MonitoringSample());
            }

            public Task<bool> TryRestartAsync() => Task.FromResult(true);
        }

        [Fact]
        public void OnSampleUpdated_WhenDashboardProjectionDisabled_KeepsOnlyLatestQueuedSample()
        {
            RuntimeUiPerformanceCounters.ResetForTests();

            var tmp = Path.Combine(Path.GetTempPath(), "OmenCoreTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tmp);
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", tmp);

            var logging = new LoggingService();
            logging.Initialize();

            try
            {
                var monitoring = new HardwareMonitoringService(
                    new MonitoringBridgeStub(),
                    logging,
                    new MonitoringPreferences(),
                    new ResumeRecoveryDiagnosticsService());
                using var vm = new DashboardViewModel(monitoring);
                vm.SetTelemetryProjectionEnabled(false);

                var onSampleUpdated = typeof(DashboardViewModel).GetMethod("OnSampleUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
                onSampleUpdated.Should().NotBeNull();

                var first = new MonitoringSample
                {
                    Timestamp = DateTime.UtcNow.AddSeconds(-1),
                    CpuTemperatureC = 61,
                    GpuTemperatureC = 54,
                    CpuLoadPercent = 18,
                    GpuLoadPercent = 27,
                    CpuPowerWatts = 34,
                    GpuPowerWatts = 46,
                    Fan1Rpm = 2200,
                    Fan2Rpm = 1800
                };

                var second = new MonitoringSample
                {
                    Timestamp = DateTime.UtcNow,
                    CpuTemperatureC = 67,
                    GpuTemperatureC = 58,
                    CpuLoadPercent = 31,
                    GpuLoadPercent = 42,
                    CpuPowerWatts = 39,
                    GpuPowerWatts = 51,
                    Fan1Rpm = 2400,
                    Fan2Rpm = 2000
                };

                onSampleUpdated!.Invoke(vm, new object?[] { null, first });
                onSampleUpdated.Invoke(vm, new object?[] { null, second });

                vm.LatestMonitoringSample.Should().BeNull("hidden dashboard projection should not update visible-state bindings");
                vm.ThermalSamples.Should().BeEmpty("hidden dashboard projection should not mutate chart history");
                vm.FilteredThermalSamples.Should().BeEmpty("hidden dashboard projection should not churn filtered chart collections");

                var queuedSampleField = typeof(DashboardViewModel).GetField("_queuedSample", BindingFlags.Instance | BindingFlags.NonPublic);
                queuedSampleField.Should().NotBeNull();
                queuedSampleField!.GetValue(vm).Should().BeSameAs(second, "hidden dashboard work should retain only the latest sample for later resume");

                var counters = RuntimeUiPerformanceCounters.GetSnapshot();
                counters.DashboardSamplesReceived.Should().Be(2);
                counters.DashboardSamplesProjected.Should().Be(0);
                counters.DashboardSamplesSkipped.Should().Be(2);
            }
            finally
            {
                logging.Dispose();
            }
        }
    }
}
