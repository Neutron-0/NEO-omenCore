using System;
using System.Text.RegularExpressions;

namespace OmenCore.Models
{
    public enum StartupRestoreCategory
    {
        Fans,
        Performance,
        Rgb,
        Tuning
    }

    public static class StartupRestorePolicy
    {
        public static bool IsEnabled(AppConfig config, StartupRestoreCategory category)
        {
            if (!config.EnableStartupHardwareRestore)
            {
                return false;
            }

            return category switch
            {
                StartupRestoreCategory.Fans => config.StartupRestoreFansEnabled ?? true,
                StartupRestoreCategory.Performance => config.StartupRestorePerformanceEnabled ?? true,
                StartupRestoreCategory.Rgb => config.StartupRestoreRgbEnabled ?? true,
                StartupRestoreCategory.Tuning => config.StartupRestoreTuningEnabled ?? true,
                _ => true
            };
        }

        private static readonly Regex Omen16BoardPattern = new(@"\b16t?-[a-z0-9]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// OMEN 16 and Victus systems require the extra <see cref="AppConfig.AllowStartupRestoreOnOmen16OrVictus"/>
        /// override before any startup hardware restore runs, regardless of category gates.
        /// Real HP WMI model strings for OMEN 16-class laptops vary by OEM naming
        /// ("OMEN 16-xd0xxx", "OMEN Gaming Laptop 16-ap0xxx", "OMEN by HP Laptop 16-..."), so this
        /// matches any 16(t)-inch OMEN board pattern instead of the literal "OMEN 16" substring.
        /// Under-matching here is the unsafe direction, so this intentionally stays permissive.
        /// </summary>
        public static bool IsSensitiveModel(string? model)
        {
            if (string.IsNullOrEmpty(model))
            {
                return false;
            }

            if (model.Contains("Victus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return model.Contains("OMEN", StringComparison.OrdinalIgnoreCase) &&
                   Omen16BoardPattern.IsMatch(model);
        }

        /// <summary>
        /// Explains, in user-facing terms, why a confirmed tuning value (e.g. a GPU OC profile
        /// confirmed via Test Apply -> Keep) will or will not be reapplied at OmenCore startup.
        /// Saving or selecting a value never implies startup authorization on its own.
        /// </summary>
        public static string DescribeTuningStartupReapplyState(AppConfig config, bool confirmedForStartup, string? model)
        {
            if (!confirmedForStartup)
            {
                return "Not confirmed - use Test Apply, then Keep, to enable startup reapply";
            }

            if (!config.EnableStartupHardwareRestore)
            {
                return "Blocked - Startup Hardware Restore is disabled in Settings";
            }

            if (!IsEnabled(config, StartupRestoreCategory.Tuning))
            {
                return "Blocked - Tuning category restore is disabled in Settings";
            }

            if (IsSensitiveModel(model) && !config.AllowStartupRestoreOnOmen16OrVictus)
            {
                return "Blocked - sensitive-model safety override required for this OMEN 16/Victus system";
            }

            return "Enabled - will reapply the confirmed values after launch";
        }

        public static string BuildSummary(AppConfig config)
        {
            if (!config.EnableStartupHardwareRestore)
            {
                return "Disabled";
            }

            return $"Fans={Format(IsEnabled(config, StartupRestoreCategory.Fans))}; " +
                   $"Performance={Format(IsEnabled(config, StartupRestoreCategory.Performance))}; " +
                   $"RGB={Format(IsEnabled(config, StartupRestoreCategory.Rgb))}; " +
                   $"Tuning={Format(IsEnabled(config, StartupRestoreCategory.Tuning))}";
        }

        private static string Format(bool enabled) => enabled ? "on" : "off";
    }
}
