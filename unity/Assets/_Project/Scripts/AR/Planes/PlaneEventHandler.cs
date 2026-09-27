using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Aayam.AR.Core;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Bridges the ARPlaneManager events into the AREventBus and exposes typed
    /// lists of added/updated/removed planes each frame.
    /// Attach to the same GameObject as ARPlaneManager (XR Origin).
    /// </summary>
    public class PlaneEventHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARPlaneManager _planeManager;

        // ─── Events ───────────────────────────────────────────────
        public event Action<ARPlane> OnPlaneDiscovered;
        public event Action<ARPlane> OnPlaneChanged;
        public event Action<ARPlane> OnPlaneLost;

        // ─── State ────────────────────────────────────────────────
        public int TotalPlaneCount { get; private set; }
        public int VerticalPlaneCount { get; private set; }

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_planeManager == null)
                _planeManager = GetComponent<ARPlaneManager>();

            if (_planeManager == null)
                Debug.LogError("[PlaneEventHandler] ARPlaneManager not found.");
        }

        private void OnEnable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged += HandlePlanesChanged;
        }

        private void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged -= HandlePlanesChanged;
        }

        // ─── Event Handler ────────────────────────────────────────

        private void HandlePlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
            {
                TotalPlaneCount++;
                if (IsVertical(plane)) VerticalPlaneCount++;

                OnPlaneDiscovered?.Invoke(plane);
                AREventBus.RaisePlaneAdded(plane);

                if (AppConfig.Load().VerboseLogging)
                    Debug.Log($"[Planes] Added: {plane.trackableId} | {plane.alignment} | {plane.extents.x * 2f:F2}×{plane.extents.y * 2f:F2}m");
            }

            foreach (var plane in args.updated)
            {
                OnPlaneChanged?.Invoke(plane);
                AREventBus.RaisePlaneUpdated(plane);
            }

            foreach (var plane in args.removed)
            {
                TotalPlaneCount = Mathf.Max(0, TotalPlaneCount - 1);
                if (IsVertical(plane)) VerticalPlaneCount = Mathf.Max(0, VerticalPlaneCount - 1);

                OnPlaneLost?.Invoke(plane);
                AREventBus.RaisePlaneRemoved(plane);

                if (AppConfig.Load().VerboseLogging)
                    Debug.Log($"[Planes] Removed: {plane.trackableId}");
            }
        }

        // ─── Utility ──────────────────────────────────────────────

        /// <summary>Toggle plane detection on/off (saves battery once door is locked).</summary>
        public void SetDetectionEnabled(bool enabled)
        {
            if (_planeManager != null)
            {
                _planeManager.enabled = enabled;
                Debug.Log($"[Planes] Detection {(enabled ? "enabled" : "disabled")}.");
            }
        }

        /// <summary>Hide all existing plane visualizations (keep tracking active).</summary>
        public void HideAllPlanes()
        {
            foreach (var plane in _planeManager.trackables)
            {
                var vis = plane.GetComponent<PlaneVisualizationController>();
                vis?.SetVisible(false);
            }
        }

        /// <summary>Show all plane visualizations.</summary>
        public void ShowAllPlanes()
        {
            foreach (var plane in _planeManager.trackables)
            {
                var vis = plane.GetComponent<PlaneVisualizationController>();
                vis?.SetVisible(true);
            }
        }

        private static bool IsVertical(ARPlane plane)
        {
            if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
                return true;
            return Mathf.Abs(Vector3.Dot(plane.normal, Vector3.up)) < 0.15f;
        }
    }
}
