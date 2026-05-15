using System.Diagnostics;

namespace OmenCore.Linux.Desktop;

/// <summary>
/// Best-effort desktop launcher abstraction for Linux URL/path opening.
/// </summary>
public static class LinuxDesktopLauncher
{
    private static readonly (string FileName, string Args)[] UrlLaunchers =
    {
        ("xdg-open", "{0}"),
        ("gio", "open {0}"),
        ("kde-open5", "{0}"),
        ("kde-open", "{0}"),
        ("gnome-open", "{0}"),
        ("firefox", "{0}"),
        ("chromium", "{0}"),
        ("google-chrome", "{0}"),
        ("brave-browser", "{0}")
    };

    private static readonly (string FileName, string Args)[] PathLaunchers =
    {
        ("xdg-open", "{0}"),
        ("gio", "open {0}"),
        ("kde-open5", "{0}"),
        ("kde-open", "{0}"),
        ("nautilus", "{0}"),
        ("thunar", "{0}"),
        ("pcmanfm", "{0}")
    };

    public static bool TryOpenUrl(string url, out string? reason)
    {
        var escaped = Quote(url);
        return TryLaunch(escaped, UrlLaunchers, out reason);
    }

    public static bool TryOpenPath(string absolutePath, out string? reason)
    {
        var escaped = Quote(absolutePath);
        return TryLaunch(escaped, PathLaunchers, out reason);
    }

    public static string DetectDesktopEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        env = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        return "unknown";
    }

    private static bool TryLaunch(string escapedArgument, IEnumerable<(string FileName, string Args)> launchers, out string? reason)
    {
        foreach (var launcher in launchers)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = launcher.FileName,
                    Arguments = string.Format(launcher.Args, escapedArgument),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                if (process == null)
                {
                    continue;
                }

                if (!process.WaitForExit(1200))
                {
                    reason = null;
                    return true;
                }

                if (process.ExitCode == 0)
                {
                    reason = null;
                    return true;
                }
            }
            catch
            {
                // Try next launcher.
            }
        }

        reason = $"No desktop launcher succeeded (DE={DetectDesktopEnvironment()}).";
        return false;
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
