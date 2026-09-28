using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Fallback occlusion for devices without depth sensors.
    /// Applies a depth-only material (OcclusionPlane shader) to detected AR planes.
    /// These invisible meshes write to the Z-buffer so virtual objects behind them
    /// are correctly hidden.
    ///
    /// Special handling for doors: hardware placed ON the door is offset slightly
    /// along the normal so it isn't occluded by its own mounting surface.
    /// </summary>
    [RequireComponent(typeof(ARPlaneManager))]
    public class PlaneOcclusionController : MonoBehaviour
    {
        [Header("Material")]
        [SerializeField] private Material _occlusionMaterial;   // Assign Aayam/AR/OcclusionPlane

        [Header("Settings")]
        [Tooltip("Skip horizontal planes to avoid occluding objects on tables/floors")]
        [SerializeField] private bool _verticalOnly = false;

        // ─── State ────────────────────────────────────────────────
        private ARPlaneManager _planeManager;
        private readonly HashSet<TrackableId> _processedPlanes = new();

        // ─── Lifecycle ────────────────────────────────────────────

        public void Initialize()
        {
            _planeManager = GetComponent<ARPlaneManager>();

            // Apply to already-detected planes
            foreach (var plane in _planeManager.trackables)
                ApplyOcclusionMaterial(plane);
        }

        private void OnEnable()
        {
            if (_planeManager == null)
                _planeManager = GetComponent<ARPlaneManager>();
            _planeManager.planesChanged += OnPlanesChanged;
        }

        private void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged -= OnPlanesChanged;
        }

        // ─── Plane Events ─────────────────────────────────────────

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
                ApplyOcclusionMaterial(plane);

            foreach (var plane in args.updated)
                ApplyOcclusionMaterial(plane);

            foreach (var plane in args.removed)
                _processedPlanes.Remove(plane.trackableId);
        }

        // ─── Material Application ─────────────────────────────────

        private void ApplyOcclusionMaterial(ARPlane plane)
        {
            if (_occlusionMaterial == null) return;
            if (_processedPlanes.Contains(plane.trackableId)) return;

            // Optionally skip horizontal planes
            if (_verticalOnly && plane.alignment != PlaneAlignment.Vertical)
                return;

            var renderer = plane.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            renderer.material = _occlusionMaterial;
            renderer.enabled  = true;   // Must be enabled to write to Z-buffer

            _processedPlanes.Add(plane.trackableId);
        }

        // ─── Door-Specific Handling ───────────────────────────────

        /// <summary>
        /// Call after placing hardware on a door plane.
        /// Nudges the hardware slightly forward along the door normal
        /// so it isn't occluded by the door's own occlusion mesh.
        /// </summary>
        public static void PreventSelfOcclusion(ARPlane doorPlane, GameObject hardware, float offsetMeters = 0.01f)
        {
            if (doorPlane == null || hardware == null) return;
            hardware.transform.position += doorPlane.normal * offsetMeters;
        }

        /// <summary>
        /// Removes the occlusion material from a specific plane
        /// (useful when the plane IS the door and we don't want it to occlude our product).
        /// </summary>
        public void DisableOcclusionForPlane(ARPlane plane)
        {
            if (plane == null) return;

            var renderer = plane.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            _processedPlanes.Remove(plane.trackableId);
        }
    }
}
