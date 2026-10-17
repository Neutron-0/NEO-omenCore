namespace OmenCore.Hardware
{
    /// <summary>
    /// Capability-card wording for the PL1/PL2 row. A live MSR readback of 0 W / 0 W beats the database flag
    /// (8BC2 showed "Supported" while the log said the read returned 0 W / 0 W).
    /// </summary>
    public static class PowerLimitReadbackLabel
    {
        public static string Describe(bool databaseSaysSupported, bool liveReadbackWasZero)
        {
            if (!databaseSaysSupported) return "✗ Not supported";
            return liveReadbackWasZero ? "✗ Firmware-managed (readback 0 W)" : "✓ Supported";
        }
    }
}
