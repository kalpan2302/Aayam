using UnityEngine;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Occlusion quality tiers mapped to device capabilities.
    /// </summary>
    public enum OcclusionQualityTier
    {
        Low,     // Plane-based only
        Medium,  // Plane-based + human stencil (if available)
        High,    // Environment depth + human occlusion
        Ultra    // Full depth + human + meshing (LiDAR)
    }

    /// <summary>
    /// Auto-detects the optimal occlusion tier and applies settings.
    /// Can be overridden manually from a settings UI.
    ///
    /// Tier mapping:
    ///   Ultra  → LiDAR devices (iPhone Pro, iPad Pro)
    ///   High   → ToF / depth sensor devices
    ///   Medium → Human stencil available, no depth
    ///   Low    → Basic AR, no depth, no human segmentation
    /// </summary>
    public class OcclusionQualityManager : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private OcclusionController _occlusionController;
        [SerializeField] private MeshingController   _meshingController;

        // ─── State ────────────────────────────────────────────────
        public OcclusionQualityTier CurrentTier { get; private set; }
        public OcclusionQualityTier DetectedTier { get; private set; }

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            DetectedTier = DetermineAutoTier();
            ApplyTier(DetectedTier);
        }

        // ─── Auto Detection ───────────────────────────────────────

        private OcclusionQualityTier DetermineAutoTier()
        {
            if (_occlusionController == null)
                return OcclusionQualityTier.Low;

            bool hasDepth = _occlusionController.HasEnvironmentDepth;
            bool hasHuman = _occlusionController.HasHumanOcclusion;
            bool hasLiDAR = hasDepth && hasHuman &&
                            Application.platform == RuntimePlatform.IPhonePlayer;

            if (hasLiDAR)  return OcclusionQualityTier.Ultra;
            if (hasDepth)  return OcclusionQualityTier.High;
            if (hasHuman)  return OcclusionQualityTier.Medium;
            return OcclusionQualityTier.Low;
        }

        // ─── Apply ────────────────────────────────────────────────

        /// <summary>Apply a specific quality tier. Call from settings UI or auto-detection.</summary>
        public void ApplyTier(OcclusionQualityTier tier)
        {
            CurrentTier = tier;

            switch (tier)
            {
                case OcclusionQualityTier.Ultra:
                    _occlusionController?.ToggleOcclusion(true);
                    _meshingController?.EnableMeshing(true);
                    break;

                case OcclusionQualityTier.High:
                    _occlusionController?.ToggleOcclusion(true);
                    _meshingController?.EnableMeshing(false);
                    break;

                case OcclusionQualityTier.Medium:
                case OcclusionQualityTier.Low:
                    _occlusionController?.ToggleOcclusion(true);
                    _meshingController?.EnableMeshing(false);
                    break;
            }

            Debug.Log($"[OcclusionQuality] Applied tier: {tier} (Detected: {DetectedTier})");
        }

        /// <summary>Reset to auto-detected tier.</summary>
        public void ResetToAuto()
        {
            ApplyTier(DetectedTier);
        }

        /// <summary>Completely disable all occlusion (for performance testing).</summary>
        public void DisableAll()
        {
            _occlusionController?.ToggleOcclusion(false);
            _meshingController?.EnableMeshing(false);
            CurrentTier = OcclusionQualityTier.Low;
            Debug.Log("[OcclusionQuality] All occlusion disabled.");
        }
    }
}
