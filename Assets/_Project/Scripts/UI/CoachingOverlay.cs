using System.Collections;
using UnityEngine;
using TMPro;

namespace Aayam.UI
{
    /// <summary>
    /// Contextual coaching overlay that guides users through the AR flow.
    ///
    /// States (auto-driven by AR events or called manually):
    ///   MovePhone      → "Move your phone slowly to scan the area"
    ///   DoorDetected   → "Door detected! Tap Place to add hardware"
    ///   SurfaceFound   → "Surface found! Tap to place a product"
    ///   ProductPlaced  → "Drag to move • Twist to rotate"
    ///   TrackingLost   → "Point camera at textured surfaces"
    ///
    /// Auto-hides after a configurable duration.
    /// </summary>
    public class CoachingOverlay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject          _panel;
        [SerializeField] private TextMeshProUGUI     _messageText;
        [SerializeField] private UnityEngine.UI.Image _icon;

        [Header("Timing")]
        [SerializeField] private float _defaultDuration  = 3.5f;
        [SerializeField] private float _fadeDuration      = 0.3f;

        [Header("Icons (optional)")]
        [SerializeField] private Sprite _moveIcon;
        [SerializeField] private Sprite _doorIcon;
        [SerializeField] private Sprite _placeIcon;
        [SerializeField] private Sprite _successIcon;

        // ─── State ────────────────────────────────────────────────
        private Coroutine _hideCoroutine;
        private CanvasGroup _canvasGroup;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = _panel?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _panel != null)
                _canvasGroup = _panel.AddComponent<CanvasGroup>();

            _panel?.SetActive(false);
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Show a custom coaching message for a given duration.</summary>
        public void ShowCoaching(string message, float duration = -1f, Sprite icon = null)
        {
            if (duration < 0f) duration = _defaultDuration;

            if (_messageText != null) _messageText.text = message;
            if (_icon != null && icon != null) _icon.sprite = icon;

            _panel?.SetActive(true);

            // Cancel any pending hide
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(duration));
        }

        /// <summary>Update coaching based on the current AR state automatically.</summary>
        public void UpdateFromARState(bool hasDoorCandidate, bool hasPlanes, bool hasPlacedObjects)
        {
            if (hasPlacedObjects)
            {
                ShowCoaching("Drag to move • Pinch to rotate", 5f, _successIcon);
            }
            else if (hasDoorCandidate)
            {
                ShowCoaching("Door detected! Tap Place to add hardware", 4f, _doorIcon);
            }
            else if (hasPlanes)
            {
                ShowCoaching("Surface found! Now point at a door", 4f, _placeIcon);
            }
            else
            {
                ShowCoaching("Move your phone slowly to scan the area", 4f, _moveIcon);
            }
        }

        /// <summary>Immediately hide the coaching panel.</summary>
        public void Hide()
        {
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _panel?.SetActive(false);
        }

        // ─── Coroutines ───────────────────────────────────────────

        private IEnumerator HideAfterDelay(float delay)
        {
            // Fade in
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                float t = 0f;
                while (t < _fadeDuration)
                {
                    t += Time.deltaTime;
                    _canvasGroup.alpha = Mathf.Clamp01(t / _fadeDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 1f;
            }

            yield return new WaitForSeconds(delay);

            // Fade out
            if (_canvasGroup != null)
            {
                float t = 0f;
                while (t < _fadeDuration)
                {
                    t += Time.deltaTime;
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(t / _fadeDuration);
                    yield return null;
                }
            }

            _panel?.SetActive(false);
            _hideCoroutine = null;
        }
    }
}
