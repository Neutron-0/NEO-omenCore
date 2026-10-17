using System.Collections.Generic;
using System.Linq;
using OmenCore.Models;

namespace OmenCore.Hardware
{
    /// <summary>
    /// Wording for the capability card's "Real fan RPM readback" row. A live reading wins over the database flag:
    /// if any fan is currently reporting an estimate, the card says so even when the database claims real RPM.
    /// </summary>
    public static class RpmReadbackLabel
    {
        public static string Describe(bool databaseSaysReal, IEnumerable<RpmSource>? liveSources)
        {
            var live = liveSources?.Where(s => s != RpmSource.Unknown).ToList() ?? new List<RpmSource>();
            if (live.Count == 0) return databaseSaysReal ? "✓ Supported" : "✗ Estimated from fan level";
            return live.Any(s => s == RpmSource.Estimated) ? "✗ Estimated from fan level" : "✓ Supported";
        }
    }
}
