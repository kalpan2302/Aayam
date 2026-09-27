using UnityEngine;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Pinch-to-scale with hard limits to preserve 1:1 real-world size accuracy.
    ///
    /// Aayam design principle: products must appear at their real physical size
    /// so customers know exactly how they'll look on the door.
    /// Scaling is OFF by default. Can be toggled on for a "preview" mode.
    ///
    /// Provides ResetToRealScale() to snap back to 1:1.
    /// </summary>
    [RequireComponent(typeof(GestureDetector), typeof(ObjectSelector))]
    public class ScaleHandler : MonoBehaviour
    {
        [Header("Scale Constraints")]
        [Tooltip("Allow pinch scaling at all? Recommended OFF for production accuracy.")]
        [SerializeField] private bool _allowScaling = false;

        [Tooltip("Minimum scale relative to real size (0.8 = 80% of real size).")]
        [SerializeField] [Range(0.5f, 1f)]  private float _minScale = 0.8f;

        [Tooltip("Maximum scale relative to real size (1.2 = 120% of real size).")]
        [SerializeField] [Range(1f, 2f)]    private float _maxScale = 1.2f;

        // ─── State ────────────────────────────────────────────────
        private GestureDetector _gestures;
        private ObjectSelector  _selector;
        private Transform       _activeObject;
        private Vector3         _realWorldScale;   // The product's correct 1:1 scale

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _gestures = GetComponent<GestureDetector>();
            _selector = GetComponent<ObjectSelector>();
        }

        private void OnEnable()
        {
            _selector.OnObjectSelected += obj =>
            {
                _activeObject    = obj;
                _realWorldScale  = obj.localScale;    // Assume the prefab is set to real-world scale
            };
            _selector.OnObjectDeselected += () => _activeObject = null;
            _gestures.OnPinch += HandlePinch;
        }

        private void OnDisable() => _gestures.OnPinch -= HandlePinch;

        // ─── Gesture Handler ──────────────────────────────────────

        private void HandlePinch(float scaleFactor)
        {
            if (_activeObject == null || !_allowScaling) return;

            Vector3 newScale = _activeObject.localScale * scaleFactor;

            // Clamp to min/max relative to the real-world scale
            float ratio = newScale.x / _realWorldScale.x;
            if (ratio >= _minScale && ratio <= _maxScale)
            {
                _activeObject.localScale = newScale;
            }
            else
            {
                // Clamp exactly to limit
                float clampedRatio = Mathf.Clamp(ratio, _minScale, _maxScale);
                _activeObject.localScale = _realWorldScale * clampedRatio;
            }
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Snap the selected object back to its exact 1:1 real-world scale.</summary>
        public void ResetToRealScale()
        {
            if (_activeObject != null && _realWorldScale != Vector3.zero)
            {
                _activeObject.localScale = _realWorldScale;
                HapticFeedback.PlaySnap();
                Debug.Log($"[Scale] Reset to real scale: {_realWorldScale}");
            }
        }

        /// <summary>Enable/disable scaling at runtime (e.g., via a UI toggle).</summary>
        public void SetScalingEnabled(bool enabled) => _allowScaling = enabled;
    }
}
