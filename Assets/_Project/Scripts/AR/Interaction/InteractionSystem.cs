using UnityEngine;

namespace Aayam.AR.Interaction
{
    // â”€â”€â”€ Interaction State Machine â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public enum InteractionState
    {
        Idle,
        Selecting,
        Selected,
        Moving,
        Rotating,
        Scaling,
        Locked
    }

    /// <summary>
    /// Central state machine that coordinates all interaction handlers.
    /// Prevents conflicting simultaneous operations (e.g., scale while locked).
    /// Drives which handlers are active at any moment.
    /// </summary>
    public class InteractionStateMachine : MonoBehaviour
    {
        public InteractionState CurrentState { get; private set; } = InteractionState.Idle;

        public event System.Action<InteractionState, InteractionState> OnStateChanged;

        [Header("Handlers (auto-found if not assigned)")]
        [SerializeField] private DragHandler     _dragHandler;
        [SerializeField] private RotationHandler _rotationHandler;
        [SerializeField] private ScaleHandler    _scaleHandler;

        // â”€â”€â”€ Transitions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void ChangeState(InteractionState newState)
        {
            if (CurrentState == newState) return;

            var previous = CurrentState;
            ExitState(CurrentState);
            CurrentState = newState;
            EnterState(CurrentState);

            OnStateChanged?.Invoke(previous, CurrentState);
            Debug.Log($"[Interaction] {previous} â†’ {CurrentState}");
        }

        // â”€â”€â”€ Guard Checks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public bool CanMove()   => CurrentState == InteractionState.Selected;
        public bool CanRotate() => CurrentState == InteractionState.Selected ||
                                   CurrentState == InteractionState.Moving;
        public bool CanScale()  => CurrentState == InteractionState.Selected;
        public bool IsLocked()  => CurrentState == InteractionState.Locked;

        // â”€â”€â”€ State Entry/Exit â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void EnterState(InteractionState state)
        {
            switch (state)
            {
                case InteractionState.Idle:
                    SetHandlersEnabled(false, false, false);
                    break;
                case InteractionState.Selected:
                    SetHandlersEnabled(true, true, true);
                    break;
                case InteractionState.Moving:
                    SetHandlersEnabled(true, false, false);
                    break;
                case InteractionState.Rotating:
                    SetHandlersEnabled(false, true, false);
                    break;
                case InteractionState.Scaling:
                    SetHandlersEnabled(false, false, true);
                    break;
                case InteractionState.Locked:
                    SetHandlersEnabled(false, false, false);
                    break;
            }
        }

        private void ExitState(InteractionState state) { /* cleanup per-state if needed */ }

        private void SetHandlersEnabled(bool drag, bool rotate, bool scale)
        {
            if (_dragHandler     != null) _dragHandler.enabled     = drag;
            if (_rotationHandler != null) _rotationHandler.enabled = rotate;
            if (_scaleHandler    != null) _scaleHandler.enabled    = scale;
        }
    }

    // â”€â”€â”€ Lock Controller â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Attach to a placed product. Locks it in place so it can't be accidentally moved.
    /// Disables the collider so raycasting can't select it while locked.
    /// Shows/hides a lock icon UI element.
    /// </summary>
    public class ObjectLockController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _lockIndicator;   // UI icon or world-space icon
        [SerializeField] private Collider   _objectCollider;

        [Header("Settings")]
        [SerializeField] private bool _startLocked = false;

        public bool IsLocked { get; private set; }

        private void Start()
        {
            if (_objectCollider == null)
                _objectCollider = GetComponentInChildren<Collider>();

            if (_startLocked) Lock();
        }

        public void Lock()
        {
            IsLocked = true;
            if (_objectCollider  != null) _objectCollider.enabled = false;
            if (_lockIndicator   != null) _lockIndicator.SetActive(true);
            HapticFeedback.PlaySnap();
            Debug.Log($"[Lock] '{name}' locked.");
        }

        public void Unlock()
        {
            IsLocked = false;
            if (_objectCollider  != null) _objectCollider.enabled = true;
            if (_lockIndicator   != null) _lockIndicator.SetActive(false);
            HapticFeedback.PlaySnap();
            Debug.Log($"[Lock] '{name}' unlocked.");
        }

        public void ToggleLock()
        {
            if (IsLocked) Unlock();
            else          Lock();
        }
    }

    // â”€â”€â”€ Snap Controller â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Snaps placed objects to standard door hardware heights during drag.
    /// Fires a haptic tick when within snap threshold.
    /// </summary>
    public class SnapController : MonoBehaviour
    {
        [Header("Standard Heights (meters from floor)")]
        [SerializeField] private float _handleHeight   = 1.0f;
        [SerializeField] private float _lockHeight     = 1.0f;
        [SerializeField] private float _peepholeHeight = 1.5f;
        [SerializeField] private float _knockerHeight  = 1.4f;

        [Header("Snap Settings")]
        [SerializeField] private float _snapThresholdM = 0.08f;   // within 80mm â†’ snap

        private bool _lastFrameSnapped;

        /// <summary>
        /// Call during drag with the proposed world-space position.
        /// Returns snapped position (or unmodified if not within threshold).
        /// </summary>
        public Vector3 ApplyHeightSnap(Vector3 proposedPos, string productCategory)
        {
            float target = GetTargetHeight(productCategory);
            float dist   = Mathf.Abs(proposedPos.y - target);

            if (dist < _snapThresholdM)
            {
                if (!_lastFrameSnapped)
                {
                    HapticFeedback.PlaySnap();
                    _lastFrameSnapped = true;
                }
                return new Vector3(proposedPos.x, target, proposedPos.z);
            }

            _lastFrameSnapped = false;
            return proposedPos;
        }

        private float GetTargetHeight(string category) => (category?.ToLower()) switch
        {
            "peephole" => _peepholeHeight,
            "knocker"  => _knockerHeight,
            "lock"     => _lockHeight,
            _          => _handleHeight,
        };
    }

    // â”€â”€â”€ Haptic Feedback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Thin cross-platform haptic wrapper.
    /// iOS: UIImpactFeedbackGenerator equivalents via UnityEngine.iOS.Haptic.
    /// Android: Handheld.Vibrate() as fallback.
    /// </summary>
    public static class HapticFeedback
    {
        public static void PlayTap()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void PlaySnap()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void PlaySuccess()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void PlayError()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
