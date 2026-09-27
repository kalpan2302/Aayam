using UnityEngine;
using UnityEngine.UI;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Debug overlay that visualises the depth texture as a colour heatmap.
    /// Red = near, green = mid, blue = far.
    /// Toggle on/off from the debug panel.
    ///
    /// Setup:
    ///   1. Add a RawImage to the canvas (half or quarter screen, bottom-left corner).
    ///   2. Create a Material using the Aayam/AR/Debug/DepthVisualizer shader.
    ///   3. Assign both to this script.
    /// </summary>
    public class DepthVisualizer : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RawImage  _debugOverlay;
        [SerializeField] private Material  _depthVisMaterial;

        [Header("Depth")]
        [SerializeField] private DepthTextureHandler _depthHandler;

        [Header("Range")]
        [SerializeField] private float _minDepth = 0.1f;
        [SerializeField] private float _maxDepth = 5.0f;

        // ─── State ────────────────────────────────────────────────
        private bool _isShowing;

        private static readonly int MinDepthProp = Shader.PropertyToID("_MinDepth");
        private static readonly int MaxDepthProp = Shader.PropertyToID("_MaxDepth");

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            enabled = false;
            _debugOverlay?.gameObject.SetActive(false);
            return;
#endif
            _debugOverlay?.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isShowing || _debugOverlay == null || _depthHandler == null) return;

            var tex = _depthHandler.CurrentDepthTexture;
            if (tex == null) return;

            _debugOverlay.texture  = tex;
            _debugOverlay.material = _depthVisMaterial;

            if (_depthVisMaterial != null)
            {
                _depthVisMaterial.SetFloat(MinDepthProp, _minDepth);
                _depthVisMaterial.SetFloat(MaxDepthProp, _maxDepth);
            }
        }

        // ─── Public API ───────────────────────────────────────────

        public void Toggle()
        {
            _isShowing = !_isShowing;
            _debugOverlay?.gameObject.SetActive(_isShowing);
        }

        public void Show() { _isShowing = true;  _debugOverlay?.gameObject.SetActive(true);  }
        public void Hide() { _isShowing = false; _debugOverlay?.gameObject.SetActive(false); }
    }
}
