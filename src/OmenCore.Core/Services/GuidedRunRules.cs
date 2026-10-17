namespace OmenCore.Services
{
    /// <summary>Small decisions for the guided fan run that must not depend on a window (K.6, 8BC2).</summary>
    public static class GuidedRunRules
    {
        /// <summary>Thermal protection ramps the fans near this temperature; a run should not start above it.</summary>
        public const double StartBelowC = 85;

        /// <summary>A step that failed while thermal protection was driving the fans did not measure the fan, it measured the protection.</summary>
        public static bool IsInterruptedByThermalProtection(bool stepPassed, bool thermalProtectionActive) =>
            !stepPassed && thermalProtectionActive;

        /// <summary>True when the machine is too hot to begin. Unknown (0 or less) readings do not block a run.</summary>
        public static bool NeedsCoolDown(double hottestC) => hottestC >= StartBelowC;
    }
}
