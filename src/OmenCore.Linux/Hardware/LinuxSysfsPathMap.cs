namespace OmenCore.Linux.Hardware;

/// <summary>
/// Centralized Linux sysfs path normalization for hp-wmi and ACPI capability probing.
/// </summary>
public static class LinuxSysfsPathMap
{
    public const string EcIoPath = "/sys/kernel/debug/ec/ec0/io";
    public const string HpWmiRoot = "/sys/devices/platform/hp-wmi";
    public const string HpWmiHwmonRoot = "/sys/devices/platform/hp-wmi/hwmon";
    public const string AcpiPlatformProfilePath = "/sys/firmware/acpi/platform_profile";
    public const string AcpiPlatformProfileChoicesPath = "/sys/firmware/acpi/platform_profile_choices";
    public const string KeyboardBacklightPath = "/sys/class/leds/hp::kbd_backlight";

    public static readonly string[] ThermalProfilePaths =
    {
        "/sys/firmware/acpi/platform_profile",
        "/sys/devices/platform/hp-wmi/thermal_profile",
        "/sys/devices/platform/hp-wmi/thermal-profile",
        "/sys/devices/platform/hp-wmi/platform_profile",
        "/sys/devices/platform/hp-wmi/platform-profile",
        "/sys/devices/platform/hp-wmi/performance_profile",
        "/sys/devices/platform/hp-wmi/performance-profile"
    };

    public static readonly string[] ThermalProfileChoicePaths =
    {
        "/sys/firmware/acpi/platform_profile_choices",
        "/sys/devices/platform/hp-wmi/platform_profile_choices",
        "/sys/devices/platform/hp-wmi/platform-profile-choices",
        "/sys/devices/platform/hp-wmi/thermal_profile_choices",
        "/sys/devices/platform/hp-wmi/thermal-profile-choices"
    };

    public static readonly string[] PlatformProfilePaths =
    {
        "/sys/devices/platform/hp-wmi/platform_profile",
        "/sys/devices/platform/hp-wmi/platform-profile"
    };

    public static readonly string[] HpWmiThermalProfilePaths =
    {
        "/sys/devices/platform/hp-wmi/thermal_profile",
        "/sys/devices/platform/hp-wmi/thermal-profile"
    };

    public static readonly string[] HpWmiPlatformProfileChoicePaths =
    {
        "/sys/devices/platform/hp-wmi/platform_profile_choices",
        "/sys/devices/platform/hp-wmi/platform-profile-choices"
    };

    public static readonly string[] HpWmiThermalProfileChoicePaths =
    {
        "/sys/devices/platform/hp-wmi/thermal_profile_choices",
        "/sys/devices/platform/hp-wmi/thermal-profile-choices"
    };

    public static string? ResolveFirstExistingFile(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? ResolveThermalProfilePath() => ResolveFirstExistingFile(ThermalProfilePaths);

    public static string? ResolveThermalProfileChoicesPath() => ResolveFirstExistingFile(ThermalProfileChoicePaths);

    public static bool AnyPathExists(IEnumerable<string> candidates) => candidates.Any(File.Exists);

    public static IEnumerable<string> EnumerateHpWmiHwmonDirectories()
    {
        if (!Directory.Exists(HpWmiHwmonRoot))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetDirectories(HpWmiHwmonRoot, "hwmon*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string? ResolveHpWmiFanTargetPath(int fanIndex)
    {
        foreach (var hwmonDir in EnumerateHpWmiHwmonDirectories())
        {
            var candidate = Path.Combine(hwmonDir, $"fan{fanIndex}_target");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool HasHpWmiFanTarget(int fanIndex) => ResolveHpWmiFanTargetPath(fanIndex) != null;

    public static string? ResolveHpWmiPwmEnablePath(int index)
    {
        foreach (var hwmonDir in EnumerateHpWmiHwmonDirectories())
        {
            var candidate = Path.Combine(hwmonDir, $"pwm{index}_enable");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool HasHpWmiPwmEnable(int index) => ResolveHpWmiPwmEnablePath(index) != null;
}
