using UnityEngine;

namespace Aayam.UI
{
    /// <summary>
    /// Adjusts a RectTransform to respect the device's Safe Area
    /// (notches, Dynamic Island, home indicator, rounded corners).
    ///
    /// Attach to the root Panel inside any Canvas.
    /// Works with Screen.safeArea which Unity updates correctly on iOS and Android.
    /// Rechecks every frame — handles device orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        [Header("Axes")]
        [Tooltip("Apply safe area inset on horizontal axis (left/right).")]
        [SerializeField] private bool _applyHorizontal = true;
        [Tooltip("Apply safe area inset on vertical axis (top/bottom).")]
        [SerializeField] private bool _applyVertical   = true;

        // ─── State ────────────────────────────────────────────────
        private RectTransform _rt;
        private Rect          _lastSafeArea = Rect.zero;
        private Vector2       _lastScreenSize;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Re-apply if safe area or screen size changed (orientation change)
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize)
                Apply();
        }

        // ─── Application ──────────────────────────────────────────

        private void Apply()
        {
            _lastSafeArea  = Screen.safeArea;
            _lastScreenSize = new Vector2(Screen.width, Screen.height);

            if (Screen.width == 0 || Screen.height == 0) return;

            Rect safeArea = _lastSafeArea;

            // Calculate normalised anchor values
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Optionally clamp to [0, 1] on disabled axes
            if (!_applyHorizontal)
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
            }
            if (!_applyVertical)
            {
                anchorMin.y = 0f;
                anchorMax.y = 1f;
            }

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
        }

#if UNITY_EDITOR
        // Redraw in editor when values change
        private void OnValidate()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            Apply();
        }
#endif
    }
}
