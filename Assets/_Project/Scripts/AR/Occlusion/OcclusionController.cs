using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Configures AROcclusionManager based on device capabilities.
    ///
    /// Tier resolution:
    ///   LiDAR  → EnvironmentDepth.Best + Human.Best + Meshing
    ///   ToF    → EnvironmentDepth.Medium + Human.Medium
    ///   None   → Plane-based fallback (OcclusionPlane shader)
    ///
    /// Monitors FPS and auto-downgrades occlusion quality when performance drops.
    /// </summary>
    [RequireComponent(typeof(AROcclusionManager))]
    public class OcclusionController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────
        [Header("Components")]
        [SerializeField] private AROcclusionManager      _occlusionManager;
        [SerializeField] private PlaneOcclusionController _planeOcclusionFallback;

        [Header("Performance")]
        [Tooltip("If FPS drops below this for several seconds, auto-downgrade.")]
        [SerializeField] private float _minFpsThreshold   = 45f;
        [SerializeField] private float _fpsCheckInterval   = 2f;  // Check every 2s, not every frame

        // ─── State ────────────────────────────────────────────────
        private bool  _occlusionEnabled = true;
        private float _fpsAccumulator;
        private int   _fpsFrameCount;
        private float _fpsNextCheck;

        public bool HasEnvironmentDepth { get; private set; }
        public bool HasHumanOcclusion   { get; private set; }

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_occlusionManager == null)
                _occlusionManager = GetComponent<AROcclusionManager>();
        }

        private void Start()
        {
            DetectCapabilities();
            ConfigureOcclusion();
        }

        private void Update()
        {
            TrackFps();
        }

        // ─── Capability Detection ─────────────────────────────────

        private void DetectCapabilities()
        {
            // LiDAR / ToF check: use subsystem descriptor
            var descriptors = new System.Collections.Generic.List<XROcclusionSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);

            foreach (var desc in descriptors)
            {
                if (desc.environmentDepthImageSupported != Supported.Unsupported)
                    HasEnvironmentDepth = true;
                if (desc.humanSegmentationDepthImageSupported != Supported.Unsupported)
                    HasHumanOcclusion = true;
            }

            Debug.Log($"[Occlusion] EnvDepth={HasEnvironmentDepth}, Human={HasHumanOcclusion}");
        }

        // ─── Configuration ────────────────────────────────────────

        public void ConfigureOcclusion()
        {
            if (!_occlusionEnabled)
            {
                DisableAll();
                return;
            }

            if (HasEnvironmentDepth && HasHumanOcclusion)
            {
                // LiDAR / full depth
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
                _occlusionManager.requestedHumanDepthMode       = HumanSegmentationDepthMode.Best;
                _occlusionManager.requestedHumanStencilMode     = HumanSegmentationStencilMode.Best;
                _occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                SetPlaneFallback(false);
                Debug.Log("[Occlusion] Mode: Full (Env+Human) — Best quality.");
            }
            else if (HasEnvironmentDepth)
            {
                // ToF / depth only
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Medium;
                _occlusionManager.requestedHumanDepthMode       = HumanSegmentationDepthMode.Disabled;
                _occlusionManager.requestedHumanStencilMode     = HumanSegmentationStencilMode.Disabled;
                SetPlaneFallback(false);
                Debug.Log("[Occlusion] Mode: Environment depth only (Medium).");
            }
            else
            {
                // No depth sensor — plane-based fallback
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                _occlusionManager.requestedHumanDepthMode       = HumanSegmentationDepthMode.Disabled;
                _occlusionManager.requestedHumanStencilMode     = HumanSegmentationStencilMode.Disabled;
                SetPlaneFallback(true);
                Debug.Log("[Occlusion] Mode: Plane-based fallback.");
            }
        }

        private void DisableAll()
        {
            _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
            _occlusionManager.requestedHumanDepthMode       = HumanSegmentationDepthMode.Disabled;
            _occlusionManager.requestedHumanStencilMode     = HumanSegmentationStencilMode.Disabled;
            SetPlaneFallback(false);
        }

        private void SetPlaneFallback(bool enable)
        {
            if (_planeOcclusionFallback == null) return;
            _planeOcclusionFallback.enabled = enable;
            if (enable) _planeOcclusionFallback.Initialize();
        }

        // ─── Performance Monitor ──────────────────────────────────

        private void TrackFps()
        {
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;

            if (Time.time < _fpsNextCheck) return;
            _fpsNextCheck = Time.time + _fpsCheckInterval;

            float avgFps = _fpsFrameCount / Mathf.Max(_fpsAccumulator, 0.001f);
            _fpsAccumulator = 0f;
            _fpsFrameCount  = 0;

            if (_occlusionEnabled && avgFps < _minFpsThreshold)
            {
                AutoDowngrade();
            }
        }

        private void AutoDowngrade()
        {
            if (_occlusionManager.requestedEnvironmentDepthMode == EnvironmentDepthMode.Best)
            {
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                Debug.LogWarning("[Occlusion] Performance low — downgraded to Fastest.");
            }
            else if (_occlusionManager.requestedEnvironmentDepthMode == EnvironmentDepthMode.Medium)
            {
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                Debug.LogWarning("[Occlusion] Performance low — downgraded to Fastest.");
            }
            else if (_occlusionManager.requestedEnvironmentDepthMode == EnvironmentDepthMode.Fastest)
            {
                // Already at lowest depth mode — fall back to planes
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                SetPlaneFallback(true);
                Debug.LogWarning("[Occlusion] Performance critical — fell back to plane occlusion.");
            }
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Toggle occlusion on/off (e.g. from settings UI).</summary>
        public void ToggleOcclusion(bool enable)
        {
            _occlusionEnabled = enable;
            ConfigureOcclusion();
        }

        public bool IsOcclusionEnabled => _occlusionEnabled;
    }
}
