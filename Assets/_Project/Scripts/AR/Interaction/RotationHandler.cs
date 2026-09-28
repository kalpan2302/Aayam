using UnityEngine;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Handles two-finger twist gesture to rotate the selected object
    /// around its local Y axis (the axis perpendicular to the door surface).
    /// Optional snap to 90° increments when the gesture ends.
    /// </summary>
    [RequireComponent(typeof(GestureDetector), typeof(ObjectSelector))]
    public class RotationHandler : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float _sensitivity = 1.5f;

        [Header("Snapping")]
        [SerializeField] private bool  _snapOnRelease   = false;
        [SerializeField] private float _snapAngle        = 90f;
        [SerializeField] private float _snapThresholdDeg = 15f;   // within 15° → snap

        // ─── State ────────────────────────────────────────────────
        private GestureDetector _gestures;
        private ObjectSelector  _selector;
        private Transform       _activeObject;
        private bool            _isTwisting;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _gestures = GetComponent<GestureDetector>();
            _selector = GetComponent<ObjectSelector>();
        }

        private void OnEnable()
        {
            _selector.OnObjectSelected   += obj => _activeObject = obj;
            _selector.OnObjectDeselected += ()  => { _activeObject = null; _isTwisting = false; };

            _gestures.OnTwist   += HandleTwist;
            _gestures.OnDragEnd += HandleGestureEnd;
        }

        private void OnDisable()
        {
            _gestures.OnTwist   -= HandleTwist;
            _gestures.OnDragEnd -= HandleGestureEnd;
        }

        // ─── Gesture Handlers ─────────────────────────────────────

        private void HandleTwist(float angleDelta)
        {
            if (_activeObject == null) return;
            _isTwisting = true;

            // Rotate around the object's local Y axis (perpendicular to door surface)
            float step = -angleDelta * _sensitivity;
            _activeObject.Rotate(Vector3.up, step, Space.Self);
        }

        private void HandleGestureEnd()
        {
            if (!_isTwisting || _activeObject == null) return;
            _isTwisting = false;

            if (_snapOnRelease)
                SnapRotation();
        }

        // ─── Snap ─────────────────────────────────────────────────

        private void SnapRotation()
        {
            Vector3 euler    = _activeObject.localEulerAngles;
            float   snapped  = SnapToNearest(euler.y, _snapAngle);
            float   distance = Mathf.Abs(Mathf.DeltaAngle(euler.y, snapped));

            if (distance < _snapThresholdDeg)
            {
                _activeObject.localEulerAngles = new Vector3(euler.x, snapped, euler.z);
                HapticFeedback.PlaySnap();
            }
        }

        private static float SnapToNearest(float angle, float snap)
        {
            return Mathf.Round(angle / snap) * snap;
        }
    }
}
