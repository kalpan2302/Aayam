using Aayam.AR.Core;

namespace Aayam.Services
{
    /// <summary>
    /// Phase 6 Step 4: decides whether a placed product should cast/receive
    /// real-time shadows, gated by DeviceCapabilityChecker.QualityTier - its
    /// own doc comment already names "visualization" as an intended consumer
    /// (as opposed to OcclusionQualityManager's tier, which gates a
    /// different signal: occlusion-feature availability, not general
    /// rendering cost).
    /// </summary>
    public static class ShadowPolicy
    {
        /// <summary>Low-tier devices skip real-time shadows entirely for perf (LODSetup.OptimizeRenderers handles the rest of that tradeoff).</summary>
        public static bool ShouldCastShadows(DeviceQualityTier tier) => tier != DeviceQualityTier.Low;
    }
}
