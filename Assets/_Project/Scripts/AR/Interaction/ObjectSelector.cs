using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Selects AR-placed objects via physics raycasting on tap.
    /// Applies/removes an Outline component to give visual selection feedback.
    /// Deselects on tap-empty-space or explicit call.
    /// </summary>
    [RequireComponent(typeof(GestureDetector))]
    public class ObjectSelector : MonoBehaviour
    {
        // ─── Events ───────────────────────────────────────────────
        public event Action<Transform> OnObjectSelected;
        public event Action            OnObjectDeselected;

        // ─── Inspector ────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private Camera _arCamera;
        [SerializeField] private LayerMask _selectableLayer;

        [Header("Highlight")]
        [SerializeField] private Color _outlineColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private float _outlineWidth = 3f;

        // ─── State ────────────────────────────────────────────────
        public Transform CurrentSelection { get; private set; }
        public bool HasSelection => CurrentSelection != null;

        private GestureDetector _gestureDetector;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _gestureDetector = GetComponent<GestureDetector>();

            if (_arCamera == null)
                _arCamera = Camera.main;
        }

        private void OnEnable()  => _gestureDetector.OnTap += HandleTap;
        private void OnDisable() => _gestureDetector.OnTap -= HandleTap;

        // ─── Tap Handling ─────────────────────────────────────────

        private void HandleTap(Vector2 screenPos)
        {
            // Ignore taps over UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = _arCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance: 15f, _selectableLayer))
            {
                SelectObject(hit.transform.root);   // Always select the root object
            }
            else
            {
                DeselectCurrent();
            }
        }

        // ─── Selection Logic ──────────────────────────────────────

        private void SelectObject(Transform obj)
        {
            if (CurrentSelection == obj) return;

            DeselectCurrent();

            CurrentSelection = obj;
            ApplyOutline(obj, true);
            OnObjectSelected?.Invoke(CurrentSelection);

            Debug.Log($"[ObjectSelector] Selected: {obj.name}");
        }

        public void DeselectCurrent()
        {
            if (CurrentSelection == null) return;

            ApplyOutline(CurrentSelection, false);
            CurrentSelection = null;
            OnObjectDeselected?.Invoke();

            Debug.Log("[ObjectSelector] Deselected.");
        }

        // ─── Outline Helper ───────────────────────────────────────
        // Uses a simple scale-based outline (no external package needed).
        // Replace with Outline shader or Quick Outline package for production.

        private void ApplyOutline(Transform obj, bool enable)
        {
            // Try to find or add an OutlineMarker component
            var marker = obj.GetComponentInChildren<SelectionOutlineMarker>();
            if (marker == null && enable)
            {
                // Create a simple outline by adding a slightly scaled duplicate mesh overlay
                marker = obj.gameObject.AddComponent<SelectionOutlineMarker>();
                marker.Initialize(_outlineColor, _outlineWidth);
            }

            if (marker != null)
                marker.SetActive(enable);
        }
    }

    // ─── Lightweight Outline Marker ───────────────────────────────

    /// <summary>
    /// Minimal selection highlight: scales the object slightly and tints it.
    /// Replace with a proper outline shader (Shader Graph / Quick Outline) later.
    /// </summary>
    public class SelectionOutlineMarker : MonoBehaviour
    {
        private Color  _outlineColor;
        private float  _outlineWidth;
        private Vector3 _originalScale;

        public void Initialize(Color color, float width)
        {
            _outlineColor  = color;
            _outlineWidth  = width;
            _originalScale = transform.localScale;
        }

        public void SetActive(bool active)
        {
            // Simple highlight: tiny scale boost + emission tint
            transform.localScale = active
                ? _originalScale * (1f + _outlineWidth * 0.005f)
                : _originalScale;

            // Apply emission tint to all renderers
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor",
                    active ? _outlineColor * 0.4f : Color.black);
            }
        }
    }
}
