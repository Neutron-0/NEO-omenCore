namespace OmenCore.Hardware
{
    /// <summary>
    /// Decoded Intel IA32_THERM_STATUS (MSR 0x19C), read only. Only bits that were cross-checked are decoded:
    /// bit 0 (thermal status, the Linux kernel names it PROCHOT), bit 6 and bit 8 (thermal threshold #1 and #2 status) and
    /// bit 10 (power limitation status), against the Linux kernel's arch/x86/include/asm/msr-index.h
    /// (THERM_STATUS_PROCHOT, THERM_STATUS_THRESHOLD0, THERM_STATUS_THRESHOLD1, THERM_STATUS_POWER_LIMIT).
    /// The Intel SDM (Vol. 3, IA32_THERM_STATUS) is the primary source; its section number was not checked here.
    /// These are "right now" status bits, not the sticky log bits. AMD is not covered.
    /// </summary>
    public readonly record struct CpuThrottleStatus(bool Thermal, bool Threshold1, bool Threshold2, bool PowerLimit)
    {
        public const uint ThermStatusMsr = 0x19C;

        public static CpuThrottleStatus Decode(ulong thermStatus) => new(
            Thermal: (thermStatus & (1UL << 0)) != 0,
            Threshold1: (thermStatus & (1UL << 6)) != 0,
            Threshold2: (thermStatus & (1UL << 8)) != 0,
            PowerLimit: (thermStatus & (1UL << 10)) != 0);

        public bool AnyThrottling => Thermal || PowerLimit;

        public override string ToString() =>
            $"thermal={(Thermal ? "yes" : "no")} power-limit={(PowerLimit ? "yes" : "no")} " +
            $"threshold1={(Threshold1 ? "yes" : "no")} threshold2={(Threshold2 ? "yes" : "no")}";
    }
}
