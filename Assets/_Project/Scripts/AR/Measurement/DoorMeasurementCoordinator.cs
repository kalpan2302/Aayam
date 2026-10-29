using UnityEngine;
using Aayam.AR.Core;
using Aayam.AR.Occlusion;
using Aayam.AR.Planes;
using Aayam.Models;

namespace Aayam.AR.Measurement
{
    /// <summary>
    /// Phase 5 Stage 4: the depth-sensor measurement path. Device-capability-
    /// gated, NOT end-to-end device-verifiable on Kalpan's current iPhone 17
    /// (no LiDAR) - see docs/phases/phase-5-implementation-plan.md. Falls
    /// back to nothing itself; callers without depth should use Stage 3's
    /// DoorEdgeCalibrationUI tap-to-calibrate flow instead.
    ///
    /// The PnP-solved door corners (DoorCoordinateSystem/DoorGeometryState)
    /// are used only to AIM each depth sample at the right screen point -
    /// the actual 3D corner position used for width/height comes from
    /// Camera.ScreenToWorldPoint(screenPoint, depthMeters), independent of
    /// the PnP solve, so this is a genuinely independent sensor-based
    /// measurement rather than a re-statement of the PnP geometry.
    /// </summary>
    public class DoorMeasurementCoordinator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private OcclusionController _occlusionController;
        [SerializeField] private DeviceCapabilityChecker _capabilityChecker;
        [SerializeField] private DepthTextureHandler _depthTextureHandler;
        [SerializeField] private DoorCoordinateSystem _doorCoordinateSystem;
        [SerializeField] private DoorGeometryState _doorGeometry;
        [SerializeField] private Camera _arCamera;

        private DepthMeasurementSampler _sampler;

        private void Awake()
        {
            _sampler = new DepthMeasurementSampler(_depthTextureHandler);
        }

        // ─── Pure branching logic (EditMode-testable, no scene needed) ──

        /// <summary>True if the depth-sensor path should be attempted instead of Stage 3's tap-to-calibrate fallback.</summary>
        public static bool ShouldUseDepthSensor(bool hasEnvironmentDepth) => hasEnvironmentDepth;

        /// <summary>Roadmap Step 6: full LiDAR+meshing tiers to LiDAR; any other depth-capable tier (ToF/AR-depth-only) to ARDepth.</summary>
        public static MeasurementSource SourceForTier(DeviceQualityTier tier) =>
            tier == DeviceQualityTier.Ultra ? MeasurementSource.LiDAR : MeasurementSource.ARDepth;

        /// <summary>Roadmap Step 6's own confidence bands (90-95% / 75-85%) - point estimates.</summary>
        public static float ConfidenceForSource(MeasurementSource source) => source switch
        {
            MeasurementSource.LiDAR => 0.925f,
            MeasurementSource.ARDepth => 0.80f,
            _ => 0.5f,
        };

        // ─── Measurement ────────────────────────────────────────────────

        /// <summary>
        /// Attempts an independent, depth-sensor-based width/height
        /// measurement of the already-solved door. Returns null if depth
        /// isn't available on this device, the door hasn't been solved
        /// yet, or a depth sample failed - callers should fall back to
        /// Stage 3's tap-to-calibrate flow in any of those cases.
        /// </summary>
        public DoorProperties TryMeasure()
        {
            if (_occlusionController == null || !ShouldUseDepthSensor(_occlusionController.HasEnvironmentDepth))
                return null;
            if (_doorCoordinateSystem == null || !_doorCoordinateSystem.HasOrigin ||
                _doorGeometry == null || !_doorGeometry.HasGeometry)
                return null;
            if (_arCamera == null) return null;

            float width = _doorGeometry.DoorWidthMeters;
            float height = _doorGeometry.DoorHeightMeters;

            // Door-space corners per DoorCoordinateSystem's own convention:
            // origin at bottom-left, X = width axis, Y = height axis.
            Vector3 bottomLeftGuess = _doorCoordinateSystem.DoorToWorldSpace(new Vector3(0, 0, 0));
            Vector3 bottomRightGuess = _doorCoordinateSystem.DoorToWorldSpace(new Vector3(width, 0, 0));
            Vector3 topLeftGuess = _doorCoordinateSystem.DoorToWorldSpace(new Vector3(0, height, 0));

            if (!TryReconstructFromDepth(bottomLeftGuess, out Vector3 bottomLeft)) return null;
            if (!TryReconstructFromDepth(bottomRightGuess, out Vector3 bottomRight)) return null;
            if (!TryReconstructFromDepth(topLeftGuess, out Vector3 topLeft)) return null;

            var tier = _capabilityChecker != null ? _capabilityChecker.QualityTier : DeviceQualityTier.Medium;
            var source = SourceForTier(tier);
            float confidence = ConfidenceForSource(source);

            return new DoorProperties
            {
                WidthMm = Vector3.Distance(bottomLeft, bottomRight) * 1000f,
                HeightMm = Vector3.Distance(bottomLeft, topLeft) * 1000f,
                WidthConfidence = confidence,
                HeightConfidence = confidence,
                Source = source,
            };
        }

        private bool TryReconstructFromDepth(Vector3 worldGuess, out Vector3 depthBasedWorldPoint)
        {
            depthBasedWorldPoint = Vector3.zero;

            Vector3 screenGuess = _arCamera.WorldToScreenPoint(worldGuess);
            if (screenGuess.z <= 0f) return false; // behind the camera

            if (!_sampler.TrySampleDepthMeters(screenGuess, out float depthMeters)) return false;

            depthBasedWorldPoint = _arCamera.ScreenToWorldPoint(new Vector3(screenGuess.x, screenGuess.y, depthMeters));
            return true;
        }
    }
}
