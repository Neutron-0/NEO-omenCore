namespace OmenCore.Linux.Hardware;

/// <summary>
/// Other fan services write <c>pwm1_enable</c> too, and two writers undo each other. This only looks: it reads
/// <c>/proc/*/comm</c> (no external process) and never stops or kills anything.
/// </summary>
public static class FanWriterScanner
{
    public static readonly string[] KnownWriters = { "victus-fan", "victus-backend", "omen-fan-control", "omenctld" };

    // The kernel truncates a process name (comm) to 15 characters, so "omen-fan-control" shows as "omen-fan-contro".
    private const int CommLength = 15;

    /// <summary>The known fan writers present in <paramref name="processNames"/>, in the order of <see cref="KnownWriters"/>.</summary>
    public static IReadOnlyList<string> Match(IEnumerable<string> processNames)
    {
        var comms = new HashSet<string>(processNames.Select(n => n.Trim()), StringComparer.Ordinal);
        return KnownWriters
            .Where(w => comms.Contains(w.Length > CommLength ? w[..CommLength] : w))
            .ToArray();
    }

    /// <summary>Scans a <c>/proc</c> tree. Unreadable entries (processes that exit while we look) are skipped.</summary>
    public static IReadOnlyList<string> Scan(string procRoot = "/proc")
    {
        if (!Directory.Exists(procRoot)) return Array.Empty<string>();

        var names = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(procRoot))
        {
            if (!int.TryParse(Path.GetFileName(dir), out _)) continue;
            try { names.Add(File.ReadAllText(Path.Combine(dir, "comm"))); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return Match(names);
    }
}
