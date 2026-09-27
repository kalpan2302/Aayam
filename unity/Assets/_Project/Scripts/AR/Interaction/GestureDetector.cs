using System;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Gesture types recognised by the system.
    /// </summary>
    public enum GestureType
    {
        None, Tap, DoubleTap, LongPress, Drag, Pinch, Twist, TwoFingerDrag
    }

    /// <summary>
    /// Converts raw touch data (Unity Input System Enhanced Touch) into
    /// clean, debounced gesture events used by Drag/Rotate/Scale handlers.
    ///
    /// Single-finger:  Tap | DoubleTap | LongPress | Drag
    /// Two-finger:     Pinch | Twist | TwoFingerDrag
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        // ─── Events ───────────────────────────────────────────────
        public event Action<Vector2>         OnTap;
        public event Action<Vector2>         OnDoubleTap;
        public event Action<Vector2>         OnLongPress;
        public event Action<Vector2, Vector2> OnDragStart;   // (startPos, currentPos)
        public event Action<Vector2>         OnDragUpdate;   // delta in screen pixels
        public event Action                  OnDragEnd;
        public event Action<float>           OnPinch;        // scale factor (relative, not absolute)
        public event Action<float>           OnTwist;        // angle delta in degrees

        // ─── Thresholds ───────────────────────────────────────────
        [Header("Tap")]
        [SerializeField] private float _tapMaxDuration   = 0.2f;
        [SerializeField] private float _doubleTapWindow  = 0.3f;
        [SerializeField] private float _longPressTime    = 0.5f;

        [Header("Drag")]
        [SerializeField] private float _dragThresholdPx  = 10f;

        [Header("Pinch / Twist")]
        [SerializeField] private float _pinchThresholdPx = 5f;
        [SerializeField] private float _twistThresholdDeg = 1f;

        // ─── State ────────────────────────────────────────────────
        private float   _touchStartTime;
        private Vector2 _touchStartPos;
        private bool    _isDragging;
        private bool    _isLongPressTriggered;
        private float   _lastTapTime;

        private float   _initPinchDist;
        private float   _initTwistAngle;

        // ─── Lifecycle ────────────────────────────────────────────

        private void OnEnable()  => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            int count = Touch.activeTouches.Count;

            if (count == 1)
                HandleSingleTouch(Touch.activeTouches[0]);
            else if (count == 2)
                HandleDoubleTouch(Touch.activeTouches[0], Touch.activeTouches[1]);
            else if (count == 0)
                ResetState();
        }

        // ─── Single Touch ─────────────────────────────────────────

        private void HandleSingleTouch(Touch t)
        {
            switch (t.phase)
            {
                case TouchPhase.Began:
                    _touchStartTime       = Time.time;
                    _touchStartPos        = t.screenPosition;
                    _isDragging           = false;
                    _isLongPressTriggered = false;
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    float dist = Vector2.Distance(t.screenPosition, _touchStartPos);

                    if (!_isDragging && dist > _dragThresholdPx)
                    {
                        _isDragging = true;
                        OnDragStart?.Invoke(_touchStartPos, t.screenPosition);
                    }
                    else if (_isDragging)
                    {
                        OnDragUpdate?.Invoke(t.delta);
                    }
                    else if (!_isLongPressTriggered && Time.time - _touchStartTime > _longPressTime)
                    {
                        _isLongPressTriggered = true;
                        OnLongPress?.Invoke(_touchStartPos);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (_isDragging)
                    {
                        OnDragEnd?.Invoke();
                    }
                    else if (!_isLongPressTriggered && Time.time - _touchStartTime < _tapMaxDuration)
                    {
                        if (Time.time - _lastTapTime < _doubleTapWindow && _lastTapTime > 0f)
                        {
                            OnDoubleTap?.Invoke(t.screenPosition);
                            _lastTapTime = 0f;
                        }
                        else
                        {
                            OnTap?.Invoke(t.screenPosition);
                            _lastTapTime = Time.time;
                        }
                    }
                    ResetState();
                    break;
            }
        }

        // ─── Two-Finger Touch ─────────────────────────────────────

        private void HandleDoubleTouch(Touch t1, Touch t2)
        {
            // Cancel any single-finger drag
            if (_isDragging)
            {
                OnDragEnd?.Invoke();
                _isDragging = false;
            }

            bool eitherBegan = t1.phase == TouchPhase.Began || t2.phase == TouchPhase.Began;

            if (eitherBegan)
            {
                _initPinchDist  = Vector2.Distance(t1.screenPosition, t2.screenPosition);
                Vector2 diff    = t1.screenPosition - t2.screenPosition;
                _initTwistAngle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                return;
            }

            if (t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved)
            {
                // ── Pinch
                float currentDist = Vector2.Distance(t1.screenPosition, t2.screenPosition);
                float pinchDelta  = currentDist - _initPinchDist;

                if (Mathf.Abs(pinchDelta) > _pinchThresholdPx)
                {
                    float scaleFactor = currentDist / Mathf.Max(_initPinchDist, 0.001f);
                    OnPinch?.Invoke(scaleFactor);
                    _initPinchDist = currentDist;
                }

                // ── Twist
                Vector2 currentDiff  = t1.screenPosition - t2.screenPosition;
                float   currentAngle = Mathf.Atan2(currentDiff.y, currentDiff.x) * Mathf.Rad2Deg;
                float   angleDelta   = Mathf.DeltaAngle(_initTwistAngle, currentAngle);

                if (Mathf.Abs(angleDelta) > _twistThresholdDeg)
                {
                    OnTwist?.Invoke(angleDelta);
                    _initTwistAngle = currentAngle;
                }
            }
        }

        // ─── Reset ────────────────────────────────────────────────

        private void ResetState()
        {
            _isDragging           = false;
            _isLongPressTriggered = false;
        }
    }
}
