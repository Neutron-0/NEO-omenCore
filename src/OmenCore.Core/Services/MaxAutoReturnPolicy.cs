using System;

namespace OmenCore.Services
{
    /// <summary>How Max fan mode may hand control back to the BIOS by itself. Off by default.</summary>
    public enum MaxAutoReturnMode
    {
        Off,
        /// <summary>CPU and GPU both under <see cref="MaxAutoReturnPolicy.CoolTempC"/> for <see cref="MaxAutoReturnPolicy.CoolSeconds"/>.</summary>
        Cooldown,
        /// <summary>A fixed time after Max was switched on.</summary>
        Timer
    }

    /// <summary>
    /// The decision only: should Max return to Auto now? Pure and clock-driven so it can be tested. The caller does the release
    /// through the normal Max-to-Auto preset path (never a bare SetFanMax(false)).
    /// Rule taken from the behaviour documented for Ohman (documentation only, no code).
    /// </summary>
    public sealed class MaxAutoReturnPolicy
    {
        public const double CoolTempC = 60;
        public static readonly TimeSpan CoolSeconds = TimeSpan.FromMinutes(2);

        private DateTime? _maxSinceUtc;
        private DateTime? _coolSinceUtc;

        /// <summary>
        /// Call once per monitor tick. <paramref name="hottestC"/> is the higher of the CPU and GPU readings; there is no separate
        /// chassis sensor, so this is a deliberately cautious stand-in. A reading of 0 or less is treated as "unknown" and never counts as cool.
        /// </summary>
        public bool ShouldReturn(DateTime nowUtc, bool maxActive, double hottestC, MaxAutoReturnMode mode, int timerMinutes)
        {
            if (!maxActive || mode == MaxAutoReturnMode.Off)
            {
                _maxSinceUtc = null;
                _coolSinceUtc = null;
                return false;
            }

            _maxSinceUtc ??= nowUtc;

            if (mode == MaxAutoReturnMode.Timer)
            {
                return nowUtc - _maxSinceUtc.Value >= TimeSpan.FromMinutes(Math.Clamp(timerMinutes, 1, 240));
            }

            if (hottestC > 0 && hottestC < CoolTempC)
            {
                _coolSinceUtc ??= nowUtc;
                return nowUtc - _coolSinceUtc.Value >= CoolSeconds;
            }

            _coolSinceUtc = null; // warmer again (or unknown): the cool-down starts over
            return false;
        }

        /// <summary>Forget everything, for example after the release happened.</summary>
        public void Reset()
        {
            _maxSinceUtc = null;
            _coolSinceUtc = null;
        }
    }
}
