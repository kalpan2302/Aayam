using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System;
using System.Collections;

namespace Aayam.AR.Core
{
    /// <summary>
    /// AR session states for Aayam application.
    /// More granular than Unity's built-in ARSessionState.
    /// </summary>
    public enum SessionState
    {
        None,
        CheckingAvailability,
        NotSupported,
        NeedsInstall,
        Installing,
        Ready,
        SessionInitializing,
        SessionTracking,
        Paused,
        Error
    }

    /// <summary>
    /// Manages the entire AR session lifecycle: availability check, install,
    /// start, pause/resume, reset, tracking loss, and error handling.
    /// Attach to the same GameObject as ARSession.
    /// </summary>
    [RequireComponent(typeof(ARSession))]
    public class ARSessionController : MonoBehaviour
    {
        // ─── Events ───────────────────────────────────────────────
        public static event Action<SessionState> OnSessionStateChanged;
        public static event Action<string> OnSessionError;

        // ─── Inspector ────────────────────────────────────────────
        [Header("Configuration")]
        [SerializeField] private ARConfiguration _config;

        [Header("References")]
        [SerializeField] private GameObject _trackingLostUI;

        // ─── State ────────────────────────────────────────────────
        private ARSession _arSession;
        private SessionState _currentState = SessionState.None;
        private float _trackingLostTimer;
        private bool _wasTracking;

        public SessionState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState == value) return;

                var previous = _currentState;
                _currentState = value;

                Debug.Log($"[ARSession] {previous} → {_currentState}");
                OnSessionStateChanged?.Invoke(_currentState);
                AREventBus.RaiseSessionStateChanged(_currentState);
            }
        }

        public bool IsTracking => _currentState == SessionState.SessionTracking;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _arSession = GetComponent<ARSession>();
            ARSession.stateChanged += HandleARSessionStateChanged;
        }

        private void OnDestroy()
        {
            ARSession.stateChanged -= HandleARSessionStateChanged;
        }

        private IEnumerator Start()
        {
            yield return InitializeSession();
        }

        private void Update()
        {
            MonitorTrackingQuality();
        }

        // ─── Initialization ───────────────────────────────────────

        private IEnumerator InitializeSession()
        {
            CurrentState = SessionState.CheckingAvailability;

            // Step 1: Check if AR is available on this device
            if (ARSession.state == ARSessionState.None ||
                ARSession.state == ARSessionState.CheckingAvailability)
            {
                yield return ARSession.CheckAvailability();
            }

            // Step 2: Handle unsupported devices
            if (ARSession.state == ARSessionState.Unsupported)
            {
                CurrentState = SessionState.NotSupported;
                HandleError("AR is not supported on this device. " +
                            "Please use a device with ARKit (iOS) or ARCore (Android) support.");
                yield break;
            }

            // Step 3: Install AR services if needed (Android only, typically)
            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                CurrentState = SessionState.NeedsInstall;
                Debug.Log("[ARSession] AR services need to be installed...");

                CurrentState = SessionState.Installing;
                yield return ARSession.Install();

                // Check if install succeeded
                if (ARSession.state == ARSessionState.NeedsInstall)
                {
                    HandleError("AR services could not be installed. " +
                                "Please install Google Play Services for AR.");
                    yield break;
                }
            }

            // Step 4: Start the session
            if (ARSession.state >= ARSessionState.Ready)
            {
                CurrentState = SessionState.Ready;
                _arSession.enabled = true;
                Debug.Log("[ARSession] AR session enabled and ready.");
            }
        }

        // ─── State Handling ───────────────────────────────────────

        private void HandleARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            switch (args.state)
            {
                case ARSessionState.SessionInitializing:
                    CurrentState = SessionState.SessionInitializing;
                    break;

                case ARSessionState.SessionTracking:
                    CurrentState = SessionState.SessionTracking;
                    _wasTracking = true;
                    _trackingLostTimer = 0f;

                    if (_trackingLostUI != null)
                        _trackingLostUI.SetActive(false);

                    AREventBus.RaiseTrackingRecovered();
                    break;

                case ARSessionState.Unsupported:
                    CurrentState = SessionState.NotSupported;
                    break;

                case ARSessionState.None:
                case ARSessionState.CheckingAvailability:
                    // Transient states, ignore
                    break;
            }
        }

        // ─── Tracking Quality Monitor ─────────────────────────────

        private void MonitorTrackingQuality()
        {
            // If we were tracking but AR session is no longer in tracking state
            if (_wasTracking && ARSession.state != ARSessionState.SessionTracking &&
                _currentState == SessionState.SessionTracking)
            {
                _trackingLostTimer += Time.deltaTime;

                float timeout = _config != null
                    ? _config.TrackingLostTimeout
                    : 5f;

                if (_trackingLostTimer >= timeout)
                {
                    Debug.LogWarning("[ARSession] Tracking lost for extended period.");
                    AREventBus.RaiseTrackingLost();

                    if (_trackingLostUI != null)
                        _trackingLostUI.SetActive(true);
                }
            }
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Pause the AR session (e.g., when app is backgrounded).
        /// </summary>
        public void PauseSession()
        {
            if (_arSession != null && _arSession.enabled)
            {
                _arSession.enabled = false;
                CurrentState = SessionState.Paused;
                Debug.Log("[ARSession] Session paused.");
            }
        }

        /// <summary>
        /// Resume a paused AR session.
        /// </summary>
        public void ResumeSession()
        {
            if (_arSession != null && !_arSession.enabled)
            {
                _arSession.enabled = true;
                Debug.Log("[ARSession] Session resumed.");
                // State will transition back to Tracking via HandleARSessionStateChanged
            }
        }

        /// <summary>
        /// Reset the AR session. Destroys all tracked data (planes, anchors).
        /// Use when user wants to start over.
        /// </summary>
        public void ResetSession()
        {
            if (_arSession != null)
            {
                _arSession.Reset();
                _wasTracking = false;
                _trackingLostTimer = 0f;
                Debug.Log("[ARSession] Session reset.");
            }
        }

        // ─── App Lifecycle ────────────────────────────────────────

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                PauseSession();
            }
            else
            {
                ResumeSession();
            }
        }

        // ─── Error Handling ───────────────────────────────────────

        private void HandleError(string message)
        {
            CurrentState = SessionState.Error;
            Debug.LogError($"[ARSession] ERROR: {message}");
            OnSessionError?.Invoke(message);
        }
    }
}
