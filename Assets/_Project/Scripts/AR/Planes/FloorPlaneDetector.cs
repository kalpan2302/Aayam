using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Tracks detected horizontal-up planes and exposes a floor world-Y
    /// reference: the lowest such plane, which is the standard AR heuristic
    /// for "the floor" (a table or shelf would track as horizontal-up too,
    /// but is normally higher than the true floor).
    ///
    /// Used by DoorSurfacePlacer.SnapToStandardHeight to convert a product's
    /// anchor_y_mm (height from floor, per the backend's product_3d_assets
    /// table) into a world-space placement height. Mirrors VerticalPlaneFilter's
    /// structure for the horizontal case.
    /// </summary>
    public class FloorPlaneDetector : MonoBehaviour
    {
        private readonly List<ARPlane> _horizontalUpPlanes = new();
        private ARPlaneManager _planeManager;

        public bool HasFloor => _horizontalUpPlanes.Count > 0;

        /// <summary>World position of the lowest tracked horizontal-up plane.
        /// Only .y is meaningful as "the floor height" - check HasFloor first.</summary>
        public Vector3 FloorWorldPosition
        {
            get
            {
                float lowestY = float.MaxValue;
                Vector3 pos = Vector3.zero;
                foreach (var plane in _horizontalUpPlanes)
                {
                    float y = plane.transform.position.y;
                    if (y < lowestY)
                    {
                        lowestY = y;
                        pos = plane.transform.position;
                    }
                }
                return pos;
            }
        }

        // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            _planeManager = FindFirstObjectByType<ARPlaneManager>();
            if (_planeManager == null)
                Debug.LogError("[FloorPlaneDetector] No ARPlaneManager found in scene.");
        }

        private void OnEnable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged += OnPlanesChanged;
        }

        private void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged -= OnPlanesChanged;
        }

        // â”€â”€â”€ Plane Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
            {
                if (IsHorizontalUp(plane) && !_horizontalUpPlanes.Contains(plane))
                    _horizontalUpPlanes.Add(plane);
            }

            foreach (var plane in args.updated)
            {
                bool was = _horizontalUpPlanes.Contains(plane);
                bool isNow = IsHorizontalUp(plane);
                if (isNow && !was) _horizontalUpPlanes.Add(plane);
                else if (!isNow && was) _horizontalUpPlanes.Remove(plane);
            }

            foreach (var plane in args.removed)
                _horizontalUpPlanes.Remove(plane);
        }

        private static bool IsHorizontalUp(ARPlane plane) => plane.alignment == PlaneAlignment.HorizontalUp;
    }
}
