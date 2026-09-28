using System;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Core
{
    /// <summary>
    /// Static event bus specifically for AR subsystem events.
    /// Provides decoupled communication between AR components and UI/logic.
    /// Separate from the general EventBus in Core to avoid AR assembly dependency issues.
    /// </summary>
    public static class AREventBus
    {
        // ─── Session Events ───────────────────────────────────────
        public static event Action<SessionState> OnSessionStateChanged;
        public static event Action<string> OnSessionError;

        // ─── Tracking Events ──────────────────────────────────────
        public static event Action OnTrackingLost;
        public static event Action OnTrackingRecovered;

        // ─── Plane Events ─────────────────────────────────────────
        public static event Action<ARPlane> OnPlaneAdded;
        public static event Action<ARPlane> OnPlaneUpdated;
        public static event Action<ARPlane> OnPlaneRemoved;

        // ─── Environment Events ───────────────────────────────────
        public static event Action<float> OnLightEstimationUpdated;

        // ─── Placement Events ─────────────────────────────────────
        public static event Action<UnityEngine.Vector3, UnityEngine.Quaternion> OnObjectPlaced;
        public static event Action OnObjectRemoved;

        // ─── Invokers ─────────────────────────────────────────────

        public static void RaiseSessionStateChanged(SessionState state)
            => OnSessionStateChanged?.Invoke(state);

        public static void RaiseSessionError(string error)
            => OnSessionError?.Invoke(error);

        public static void RaiseTrackingLost()
            => OnTrackingLost?.Invoke();

        public static void RaiseTrackingRecovered()
            => OnTrackingRecovered?.Invoke();

        public static void RaisePlaneAdded(ARPlane plane)
            => OnPlaneAdded?.Invoke(plane);

        public static void RaisePlaneUpdated(ARPlane plane)
            => OnPlaneUpdated?.Invoke(plane);

        public static void RaisePlaneRemoved(ARPlane plane)
            => OnPlaneRemoved?.Invoke(plane);

        public static void RaiseLightEstimationUpdated(float intensity)
            => OnLightEstimationUpdated?.Invoke(intensity);

        public static void RaiseObjectPlaced(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
            => OnObjectPlaced?.Invoke(position, rotation);

        public static void RaiseObjectRemoved()
            => OnObjectRemoved?.Invoke();

        /// <summary>
        /// Clear all subscribers. Call on session teardown to prevent memory leaks.
        /// </summary>
        public static void ClearAll()
        {
            OnSessionStateChanged = null;
            OnSessionError = null;
            OnTrackingLost = null;
            OnTrackingRecovered = null;
            OnPlaneAdded = null;
            OnPlaneUpdated = null;
            OnPlaneRemoved = null;
            OnLightEstimationUpdated = null;
            OnObjectPlaced = null;
            OnObjectRemoved = null;
        }
    }
}

