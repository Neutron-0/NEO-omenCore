using System;
using System.Windows.Media;
using OmenCore.Models;
using OmenCore.Services;
using OmenCore.Utils;

namespace OmenCore.ViewModels
{
    /// <summary>
    /// ViewModel for the simplified General view with paired Performance + Fan profiles.
    /// Each profile combines a performance mode with an optimal fan configuration.
    /// </summary>
    public class GeneralViewModel : ViewModelBase
    {
        private readonly FanService _fanService;
        private readonly PerformanceModeService _performanceModeService;
        private readonly LoggingService _logging;
        private readonly ConfigurationService _configService;
        private readonly SystemInfoService? _systemInfoService;
        private FanControlViewModel? _fanControlViewModel;
        private SystemControlViewModel? _systemControlViewModel;

        private string _currentPerformanceMode = "Default";
        private string _currentFanMode = "Auto";
        private string _selectedProfile = "Balanced";
        private double _cpuTemp;
        private double _gpuTemp;
        private int _cpuFanPercent;
        private int _gpuFanPercent;
        private int _cpuFanRpm;
        private int _gpuFanRpm;
        
        // v2.6.1: Additional telemetry for enhanced General tab
        private double _cpuLoad;
        private double _gpuLoad;
        private double _gpuPowerWatts;
        private double _cpuPowerWatts;
        private double _ramUsedGb;
        private double _ramTotalGb;
        private MonitoringSample? _lastProjectedSample;
        private DateTime _lastUiProjectionUtc = DateTime.MinValue;
        private bool _telemetryProjectionEnabled = true;

        private static readonly TimeSpan UiProjectionMinInterval = TimeSpan.FromMilliseconds(750);
        private const double UiProjectionTempDelta = 0.5;
        private const double UiProjectionLoadDelta = 2.0;
        private const double UiProjectionPowerDelta = 1.0;
        private const int UiProjectionFanRpmDelta = 120;

        public GeneralViewModel(
            FanService fanService,
            PerformanceModeService performanceModeService,
            ConfigurationService configService,
            LoggingService logging,
            SystemInfoService? systemInfoService = null)
        {
            _fanService = fanService;
            _performanceModeService = performanceModeService;
            _configService = configService;
            _logging = logging;
            _systemInfoService = systemInfoService;

            // Initial load
            LoadCurrentState();
        }
        
        /// <summary>
        /// Sets the FanControlViewModel reference for syncing preset selections.
        /// Call this after both ViewModels are created.
        /// </summary>
        public void SetFanControlViewModel(FanControlViewModel? fanControlViewModel)
        {
            _fanControlViewModel = fanControlViewModel;
        }
        
        /// <summary>
        /// Sets the SystemControlViewModel reference for syncing performance mode UI.
        /// v2.8.6: Fixes quick profile switching not updating the OMEN tab display.
        /// </summary>
        public void SetSystemControlViewModel(SystemControlViewModel? systemControlViewModel)
        {
            _systemControlViewModel = systemControlViewModel;
        }
        
        /// <summary>
        /// Returns the brand logo path based on detected system type.
        /// HP Spectre systems get the Spectre logo, others get OMEN logo.
        /// </summary>
        public string BrandLogoPath
        {
            get
            {
                try
                {
                    var sysInfo = _systemInfoService?.GetSystemInfo();
                    if (sysInfo?.IsHpSpectre == true)
                    {
                        return "pack://application:,,,/Assets/spectre.png";
                    }
                }
                catch
                {
                    // Fallback to OMEN logo on any detection failure
                }
                return "pack://application:,,,/Assets/omen.png";
            }
        }

        public void SetTelemetryProjectionEnabled(bool enabled)
        {
            _telemetryProjectionEnabled = enabled;
        }

        #region Properties

        public string CurrentPerformanceMode
        {
            get => _currentPerformanceMode;
            set { _currentPerformanceMode = value; OnPropertyChanged(); }
        }

        public string CurrentFanMode
        {
            get => _currentFanMode;
            set { _currentFanMode = value; OnPropertyChanged(); }
        }

        public string SelectedProfile
        {
            get => _selectedProfile;
            set { _selectedProfile = value; OnPropertyChanged(); UpdateProfileIndicators(); }
        }

        public double CpuTemp
        {
            get => _cpuTemp;
            set
            {
                var sanitized = double.IsFinite(value) ? Math.Max(0, Math.Min(125, value)) : _cpuTemp;
                _cpuTemp = sanitized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuTempDisplay));
                OnPropertyChanged(nameof(IsCpuTempAvailable));
            }
        }

        public double GpuTemp
        {
            get => _gpuTemp;
            set
            {
                var sanitized = double.IsFinite(value) ? Math.Max(0, Math.Min(125, value)) : _gpuTemp;
                _gpuTemp = sanitized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuTempDisplay));
                OnPropertyChanged(nameof(IsGpuTempAvailable));
            }
        }

        public int CpuFanPercent
        {
            get => _cpuFanPercent;
            set { _cpuFanPercent = value; OnPropertyChanged(); }
        }

        public int GpuFanPercent
        {
            get => _gpuFanPercent;
            set { _gpuFanPercent = value; OnPropertyChanged(); }
        }

        public int CpuFanRpm
        {
            get => _cpuFanRpm;
            set { _cpuFanRpm = value; OnPropertyChanged(); }
        }

        public int GpuFanRpm
        {
            get => _gpuFanRpm;
            set { _gpuFanRpm = value; OnPropertyChanged(); }
        }
        
        // v2.6.1: Additional telemetry properties
        public double CpuLoad
        {
            get => _cpuLoad;
            set
            {
                var sanitized = double.IsFinite(value) ? Math.Max(0, Math.Min(100, value)) : _cpuLoad;
                _cpuLoad = sanitized;
                OnPropertyChanged();
            }
        }
        
        public double GpuLoad
        {
            get => _gpuLoad;
            set
            {
                var sanitized = double.IsFinite(value) ? Math.Max(0, Math.Min(100, value)) : _gpuLoad;
                _gpuLoad = sanitized;
                OnPropertyChanged();
            }
        }
        
        public double GpuPowerWatts
        {
            get => _gpuPowerWatts;
            set { _gpuPowerWatts = value; OnPropertyChanged(); }
        }
        
        public double CpuPowerWatts
        {
            get => _cpuPowerWatts;
            set { _cpuPowerWatts = value; OnPropertyChanged(); }
        }
        
        public double RamUsedGb
        {
            get => _ramUsedGb;
            set { _ramUsedGb = value; OnPropertyChanged(); }
        }
        
        public double RamTotalGb
        {
            get => _ramTotalGb;
            set { _ramTotalGb = value; OnPropertyChanged(); }
        }
        
        public double RamPercent => RamTotalGb > 0 ? (RamUsedGb / RamTotalGb) * 100 : 0;

        // Temperature display helpers — mirror DashboardViewModel for consistent "—°C" treatment
        // across all views. Used by any binding that needs a formatted string rather than a raw double.
        public string CpuTempDisplay => CpuTemp > 0 ? $"{CpuTemp:F0}°C" : "—°C";
        public string GpuTempDisplay => GpuTemp > 0 ? $"{GpuTemp:F0}°C" : "—°C";
        public bool IsCpuTempAvailable => CpuTemp > 0;
        public bool IsGpuTempAvailable => GpuTemp > 0;

        // Profile selection indicators
        public bool IsPerformanceSelected => SelectedProfile == "Performance";
        public bool IsBalancedSelected => SelectedProfile == "Balanced";
        public bool IsQuietSelected => SelectedProfile == "Quiet";
        public bool IsCustomSelected => SelectedProfile == "Custom";

        // Border colors for selection highlighting
        public Brush SelectedProfileBorder_Performance => IsPerformanceSelected ? (Brush)new BrushConverter().ConvertFrom("#FF005C")! : Brushes.Transparent;
        public Brush SelectedProfileBorder_Balanced => IsBalancedSelected ? (Brush)new BrushConverter().ConvertFrom("#FF005C")! : Brushes.Transparent;
        public Brush SelectedProfileBorder_Quiet => IsQuietSelected ? (Brush)new BrushConverter().ConvertFrom("#FF005C")! : Brushes.Transparent;
        public Brush SelectedProfileBorder_Custom => IsCustomSelected ? (Brush)new BrushConverter().ConvertFrom("#FF005C")! : Brushes.Transparent;

        #endregion

        #region Profile Application Methods

        /// <summary>
        /// Performance profile: Maximum power + aggressive cooling
        /// </summary>
        public void ApplyPerformanceProfile()
        {
            try
            {
                _logging.Info("Applying Performance profile (Max Power + Max Cooling)");

                // Apply Performance mode
                _performanceModeService.SetPerformanceMode("Performance");

                // Apply Max fan mode
                _fanService.ApplyMaxCooling();
                
                // Sync fan preset UI if available
                _fanControlViewModel?.SelectPresetByNameNoApply("Max");
                
                // v2.8.6: Sync OMEN tab performance mode display
                _systemControlViewModel?.SelectModeByNameNoApply("Performance");

                SelectedProfile = "Performance";
                CurrentPerformanceMode = "Performance";
                CurrentFanMode = "Max";

                _logging.Info("Performance profile applied successfully");
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to apply Performance profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Balanced profile: Default power + auto fans
        /// </summary>
        public void ApplyBalancedProfile()
        {
            try
            {
                _logging.Info("Applying Balanced profile (Default Power + Auto Cooling)");

                // Apply Default mode
                _performanceModeService.SetPerformanceMode("Default");

                // Apply Auto fan mode
                _fanService.ApplyAutoMode();
                
                // Sync fan preset UI if available
                _fanControlViewModel?.SelectPresetByNameNoApply("Auto");
                
                // v2.8.6: Sync OMEN tab performance mode display
                _systemControlViewModel?.SelectModeByNameNoApply("Balanced");

                SelectedProfile = "Balanced";
                CurrentPerformanceMode = "Default";
                CurrentFanMode = "Auto";

                _logging.Info("Balanced profile applied successfully");
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to apply Balanced profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Quiet profile: Power saver + silent fans
        /// </summary>
        public void ApplyQuietProfile()
        {
            try
            {
                _logging.Info("Applying Quiet profile (Power Saver + Silent Cooling)");

                // Apply Quiet/PowerSaver mode
                _performanceModeService.SetPerformanceMode("Quiet");

                // Apply Quiet fan mode
                _fanService.ApplyQuietMode();
                
                // Sync fan preset UI if available (use Auto as closest built-in)
                _fanControlViewModel?.SelectPresetByNameNoApply("Auto");
                
                // v2.8.6: Sync OMEN tab performance mode display
                _systemControlViewModel?.SelectModeByNameNoApply("Quiet");

                SelectedProfile = "Quiet";
                CurrentPerformanceMode = "Quiet";
                CurrentFanMode = "Quiet";

                _logging.Info("Quiet profile applied successfully");
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to apply Quiet profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Custom profile: User-defined settings (redirect to Advanced tab)
        /// </summary>
        public void ApplyCustomProfile()
        {
            try
            {
                _logging.Info("Custom profile selected - use Advanced tab for manual control");

                SelectedProfile = "Custom";
                CurrentPerformanceMode = "Custom";
                CurrentFanMode = "Custom";

                // Note: Actual custom settings are applied via the Advanced tab
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to set Custom profile: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private void LoadCurrentState()
        {
            try
            {
                // Load current performance mode
                var perfMode = _performanceModeService.GetCurrentMode();
                CurrentPerformanceMode = perfMode ?? "Default";

                // Load current fan mode
                var fanMode = _fanService.GetCurrentFanMode();
                CurrentFanMode = fanMode ?? "Auto";

                // Determine which profile matches current state
                DetermineActiveProfile();

                // Update telemetry
                UpdateTelemetry();
                UpdateTemperatures();
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to load current state: {ex.Message}");
            }
        }

        private void DetermineActiveProfile()
        {
            // First try to match using the saved fan preset name from config
            var savedPreset = _configService.Config?.LastFanPresetName;
            if (!string.IsNullOrEmpty(savedPreset))
            {
                SelectedProfile = FanModeNameResolver.ResolveGeneralProfileFromPresetName(savedPreset);
                if (SelectedProfile != "Custom")
                    return;
            }
            
            // Fallback: Match current state to a profile
            if (CurrentPerformanceMode == "Performance" && CurrentFanMode == "Max")
                SelectedProfile = "Performance";
            else if (CurrentPerformanceMode == "Quiet" && CurrentFanMode == "Quiet")
                SelectedProfile = "Quiet";
            else if (CurrentPerformanceMode == "Default" && CurrentFanMode == "Auto")
                SelectedProfile = "Balanced";
            else
                SelectedProfile = "Custom";
        }

        private void UpdateProfileIndicators()
        {
            OnPropertyChanged(nameof(IsPerformanceSelected));
            OnPropertyChanged(nameof(IsBalancedSelected));
            OnPropertyChanged(nameof(IsQuietSelected));
            OnPropertyChanged(nameof(IsCustomSelected));
            OnPropertyChanged(nameof(SelectedProfileBorder_Performance));
            OnPropertyChanged(nameof(SelectedProfileBorder_Balanced));
            OnPropertyChanged(nameof(SelectedProfileBorder_Quiet));
            OnPropertyChanged(nameof(SelectedProfileBorder_Custom));
        }

        private void UpdateTelemetry()
        {
            try
            {
                var telemetry = _fanService.FanTelemetry;
                if (telemetry.Count >= 1)
                {
                    CpuFanPercent = telemetry[0].DutyCyclePercent;
                    CpuFanRpm = telemetry[0].SpeedRpm;
                }
                if (telemetry.Count >= 2)
                {
                    GpuFanPercent = telemetry[1].DutyCyclePercent;
                    GpuFanRpm = telemetry[1].SpeedRpm;
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[GeneralVM] Fan speed update error: {ex.Message}"); 
            }
        }

        private void UpdateTemperatures()
        {
            try
            {
                var samples = _fanService.ThermalSamples;
                if (samples.Count > 0)
                {
                    var latest = samples[^1];
                    CpuTemp = latest.CpuCelsius;
                    GpuTemp = latest.GpuCelsius;
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[GeneralVM] Temperature update error: {ex.Message}"); 
            }
        }
        
        /// <summary>
        /// v2.6.1: Update from MainViewModel's monitoring sample for enhanced telemetry
        /// </summary>
        public void UpdateFromMonitoringSample(MonitoringSample? sample)
        {
            if (sample == null) return;
            RuntimeUiPerformanceCounters.RecordGeneralSampleReceived();
            if (!_telemetryProjectionEnabled)
            {
                RuntimeUiPerformanceCounters.RecordGeneralSampleSkipped();
                   // v3.6.2: Track hidden-surface suppression
                   RuntimeUiPerformanceCounters.RecordHiddenSurfaceSampleSkipped();
                return;
            }

            if (!ShouldProjectMonitoringSample(sample))
            {
                RuntimeUiPerformanceCounters.RecordGeneralSampleSkipped();
                return;
            }
            
            try
            {
                if (sample.CpuTemperatureState == TelemetryDataState.Valid ||
                    sample.CpuTemperatureState == TelemetryDataState.Stale)
                {
                    CpuTemp = sample.CpuTemperatureC;
                }

                if (sample.GpuTemperatureState == TelemetryDataState.Valid ||
                    sample.GpuTemperatureState == TelemetryDataState.Stale ||
                    sample.GpuTemperatureState == TelemetryDataState.Inactive)
                {
                    GpuTemp = sample.GpuTemperatureC;
                }

                CpuLoad = sample.CpuLoadPercent;
                GpuLoad = sample.GpuLoadPercent;
                GpuPowerWatts = sample.GpuPowerWatts;
                CpuPowerWatts = sample.CpuPowerWatts;
                RamUsedGb = sample.RamUsageGb;
                RamTotalGb = sample.RamTotalGb;

                if (sample.Fan1RpmState == TelemetryDataState.Valid ||
                    sample.Fan1RpmState == TelemetryDataState.Stale)
                {
                    CpuFanRpm = sample.Fan1Rpm;
                    CpuFanPercent = EstimateFanPercent(sample.Fan1Rpm);
                }

                if (sample.Fan2RpmState == TelemetryDataState.Valid ||
                    sample.Fan2RpmState == TelemetryDataState.Stale)
                {
                    GpuFanRpm = sample.Fan2Rpm;
                    GpuFanPercent = sample.GpuFanPercent > 0
                        ? (int)Math.Clamp(Math.Round(sample.GpuFanPercent), 0, 100)
                        : EstimateFanPercent(sample.Fan2Rpm);
                }

                OnPropertyChanged(nameof(RamPercent));
                _lastProjectedSample = new MonitoringSample(sample);
                _lastUiProjectionUtc = DateTime.UtcNow;
                RuntimeUiPerformanceCounters.RecordGeneralSampleProjected();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeneralVM] Monitoring sample update error: {ex.Message}");
            }
        }

        private bool ShouldProjectMonitoringSample(MonitoringSample sample)
        {
            if (_lastProjectedSample == null)
            {
                return true;
            }

            var elapsed = DateTime.UtcNow - _lastUiProjectionUtc;
            if (elapsed >= UiProjectionMinInterval)
            {
                return true;
            }

            var previous = _lastProjectedSample;

            return Math.Abs(sample.CpuTemperatureC - previous.CpuTemperatureC) >= UiProjectionTempDelta
                || Math.Abs(sample.GpuTemperatureC - previous.GpuTemperatureC) >= UiProjectionTempDelta
                || Math.Abs(sample.CpuLoadPercent - previous.CpuLoadPercent) >= UiProjectionLoadDelta
                || Math.Abs(sample.GpuLoadPercent - previous.GpuLoadPercent) >= UiProjectionLoadDelta
                || Math.Abs(sample.CpuPowerWatts - previous.CpuPowerWatts) >= UiProjectionPowerDelta
                || Math.Abs(sample.GpuPowerWatts - previous.GpuPowerWatts) >= UiProjectionPowerDelta
                || Math.Abs(sample.Fan1Rpm - previous.Fan1Rpm) >= UiProjectionFanRpmDelta
                || Math.Abs(sample.Fan2Rpm - previous.Fan2Rpm) >= UiProjectionFanRpmDelta
                || sample.CpuTemperatureState != previous.CpuTemperatureState
                || sample.GpuTemperatureState != previous.GpuTemperatureState
                || sample.Fan1RpmState != previous.Fan1RpmState
                || sample.Fan2RpmState != previous.Fan2RpmState;
        }

        private static int EstimateFanPercent(int rpm)
        {
            if (rpm <= 0) return 0;
            if (rpm >= 5500) return 100;
            return Math.Clamp((int)Math.Round(rpm / 55.0), 0, 100);
        }

        #endregion
    }
}
