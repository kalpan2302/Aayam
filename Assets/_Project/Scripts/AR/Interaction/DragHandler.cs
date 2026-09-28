using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Surface-constrained drag: moves the selected object by continuously raycasting
    /// the touch position against AR planes, keeping it flush to the wall/door.
    /// Smoothed with Lerp to avoid jittery movement.
    /// </summary>
    [RequireComponent(typeof(GestureDetector), typeof(ObjectSelector))]
    public class DragHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARRaycastManager _arRaycastManager;

        [Header("Settings")]
        [SerializeField] private float _positionSmoothing = 18f;
        [SerializeField] private float _surfaceOffset     = 0.005f;

        // ─── State ────────────────────────────────────────────────
        private GestureDetector _gestures;
        private ObjectSelector  _selector;
        private Transform       _activeObject;
        private Vector3         _targetPosition;
        private Quaternion      _targetRotation;
        private bool            _isDragging;

        private readonly List<ARRaycastHit> _hits = new();

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _gestures = GetComponent<GestureDetector>();
            _selector = GetComponent<ObjectSelector>();
        }

        private void OnEnable()
        {
            _selector.OnObjectSelected   += obj => { _activeObject = obj; };
            _selector.OnObjectDeselected += ()  => { _activeObject = null; _isDragging = false; };

            _gestures.OnDragStart  += HandleDragStart;
            _gestures.OnDragUpdate += HandleDragUpdate;
            _gestures.OnDragEnd    += HandleDragEnd;
        }

        private void OnDisable()
        {
            _gestures.OnDragStart  -= HandleDragStart;
            _gestures.OnDragUpdate -= HandleDragUpdate;
            _gestures.OnDragEnd    -= HandleDragEnd;
        }

        // ─── Gesture Handlers ─────────────────────────────────────

        private void HandleDragStart(Vector2 startPos, Vector2 currentPos)
        {
            if (_activeObject == null) return;
            _isDragging     = true;
            _targetPosition = _activeObject.position;
            _targetRotation = _activeObject.rotation;
        }

        private void HandleDragUpdate(Vector2 delta)
        {
            if (!_isDragging || _activeObject == null) return;

            // Raycast from the current touch position against vertical planes
            if (Touch.activeTouches.Count == 0) return;
            Vector2 touchPos = Touch.activeTouches[0].screenPosition;

            _hits.Clear();
            if (_arRaycastManager.Raycast(touchPos, _hits, TrackableType.PlaneWithinPolygon))
            {
                var hit    = _hits[0];
                var normal = hit.pose.up;   // AR Foundation: 'up' = plane normal

                // Keep product flush: position = hit point + small offset along normal
                _targetPosition = hit.pose.position + normal * _surfaceOffset;

                // Keep product upright on the surface
                Vector3 projectedUp = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
                if (projectedUp.sqrMagnitude > 0.001f)
                    _targetRotation = Quaternion.LookRotation(normal, projectedUp);
            }
        }

        private void HandleDragEnd()
        {
            _isDragging = false;
        }

        // ─── Smooth Update ────────────────────────────────────────

        private void Update()
        {
            if (_activeObject == null || !_isDragging) return;

            float t = Time.deltaTime * _positionSmoothing;
            _activeObject.position = Vector3.Lerp(_activeObject.position, _targetPosition, t);
            _activeObject.rotation = Quaternion.Slerp(_activeObject.rotation, _targetRotation, t);
        }
    }
}
