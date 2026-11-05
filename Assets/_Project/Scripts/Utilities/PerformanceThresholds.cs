namespace Aayam.Utilities
{
    /// <summary>
    /// Phase 6 Step 9's actual roadmap targets (60 FPS, &lt;200MB) - extracted
    /// from PerformanceProfiler.SaveReport's inlined comparisons so the
    /// pass/fail logic is testable without a live Profiler session. The
    /// previous inlined thresholds (55 FPS, &lt;400MB) didn't match the
    /// roadmap and are now corrected.
    /// </summary>
    public static class PerformanceThresholds
    {
        public const float TargetFps = 60f;
        public const long MemoryBudgetMB = 200;

        public static bool FpsMeetsTarget(float minFps) => minFps >= TargetFps;
        public static bool MemoryWithinBudget(long allocatedMB) => allocatedMB < MemoryBudgetMB;
    }
}
