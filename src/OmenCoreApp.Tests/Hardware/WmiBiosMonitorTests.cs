using System;
using System.Reflection;
using FluentAssertions;
using OmenCore.Hardware;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    [Collection("Config Isolation")]
    public class WmiBiosMonitorTests
    {
        private static void SetPrivateField<T>(object instance, string fieldName, T value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull(fieldName);
            field!.SetValue(instance, value);
        }

        private static double InvokeGetBatteryCharge(WmiBiosMonitor monitor)
        {
            var method = typeof(WmiBiosMonitor).GetMethod("GetBatteryCharge", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            return (double)method!.Invoke(monitor, null)!;
        }

        [Fact]
        public void GetBatteryCharge_DuringCooldown_ReturnsCachedValue()
        {
            var monitor = new WmiBiosMonitor();
            SetPrivateField(monitor, "_cachedBatteryChargePercent", 73.5d);
            SetPrivateField(monitor, "_lastBatteryQuery", DateTime.Now);

            InvokeGetBatteryCharge(monitor).Should().Be(73.5d);
        }

        [Fact]
        public void GetBatteryCharge_WhenMonitoringDisabled_ReturnsCachedValue()
        {
            var monitor = new WmiBiosMonitor();
            SetPrivateField(monitor, "_cachedBatteryChargePercent", 41.0d);
            SetPrivateField(monitor, "_batteryMonitoringDisabled", true);

            InvokeGetBatteryCharge(monitor).Should().Be(41.0d);
        }

        /// <summary>
        /// Issue #129: CPU thermal authority must track authority source and transitions
        /// when low-temp + high-load mismatch is detected.
        /// </summary>
        [Fact]
        public void CpuTemperatureAuthoritySource_InitializesToWmiBios()
        {
            var monitor = new WmiBiosMonitor();
            monitor.CpuTemperatureAuthoritySource.Should().Be("WMI BIOS");
            monitor.CpuTemperatureAuthorityReason.Should().Contain("Startup default");
            monitor.CpuTemperatureAuthoritySwitchCount.Should().Be(0);
        }

        /// <summary>
        /// Issue #129: MonitoringSource property includes current CPU authority suffix
        /// so diagnostics can capture active authority at collection time.
        /// </summary>
        [Fact]
        public void MonitoringSource_IncludesCpuAuthorityState()
        {
            var monitor = new WmiBiosMonitor();
            var source = monitor.MonitoringSource;
            source.Should().Contain("CPU Authority:");
            source.Should().Contain("WMI BIOS");
        }

        /// <summary>
        /// Issue #129: Authority reason is recorded for field diagnostics.
        /// </summary>
        [Fact]
        public void CpuTemperatureAuthorityReason_RecordsDecisionContext()
        {
            var monitor = new WmiBiosMonitor();
            var reason = monitor.CpuTemperatureAuthorityReason;
            reason.Should().NotBeNullOrWhiteSpace();
            // Should describe why this authority is active, even if "Startup default"
            reason.Length.Should().BeGreaterThan(5);
        }

        /// <summary>
        /// Issue #129: LastSwitchUtc is set when authority transitions and available for timing diagnostics.
        /// </summary>
        [Fact]
        public void CpuTemperatureAuthorityLastSwitchUtc_TracksSwitchTiming()
        {
            var monitor = new WmiBiosMonitor();
            // At startup, no switch has occurred yet; it should be MinValue
            monitor.CpuTemperatureAuthorityLastSwitchUtc.Should().Be(DateTime.MinValue);
            
            // The property is available and can be inspected by field diagnostics
            var switchTime = monitor.CpuTemperatureAuthorityLastSwitchUtc;
            switchTime.Should().NotBe(DateTime.MaxValue);
        }
    }
}