using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Maintains a live list of detected vertical planes.
    /// Provides convenience methods: largest plane, closest to camera, best door match.
    /// </summary>
    public class VerticalPlaneFilter : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Max dot product with Vector3.up to consider a plane vertical (lower = stricter)")]
        [SerializeField] [Range(0f, 0.3f)] private float _verticalThreshold = 0.15f;

        // ─── State ────────────────────────────────────────────────
        public IReadOnlyList<ARPlane> VerticalPlanes => _verticalPlanes;
        private readonly List<ARPlane> _verticalPlanes = new();

        private ARPlaneManager _planeManager;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _planeManager = FindFirstObjectByType<ARPlaneManager>();
            if (_planeManager == null)
                Debug.LogError("[VerticalPlaneFilter] No ARPlaneManager found in scene.");
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

        // ─── Plane Events ─────────────────────────────────────────

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
            {
                if (IsVertical(plane) && !_verticalPlanes.Contains(plane))
                    _verticalPlanes.Add(plane);
            }

            // Updated planes may have changed alignment — re-evaluate
            foreach (var plane in args.updated)
            {
                bool wasVertical = _verticalPlanes.Contains(plane);
                bool isVertical  = IsVertical(plane);

                if (isVertical && !wasVertical)
                    _verticalPlanes.Add(plane);
                else if (!isVertical && wasVertical)
                    _verticalPlanes.Remove(plane);
            }

            foreach (var plane in args.removed)
                _verticalPlanes.Remove(plane);
        }

        // ─── Classification ───────────────────────────────────────

        private bool IsVertical(ARPlane plane)
        {
            // Use AR Foundation's built-in alignment first
            if (plane.alignment == PlaneAlignment.Vertical)
                return true;

            // Fallback: dot product check
            float dot = Mathf.Abs(Vector3.Dot(plane.normal, Vector3.up));
            return dot < _verticalThreshold;
        }

        // ─── Query API ────────────────────────────────────────────

        /// <summary>Return the largest vertical plane by area.</summary>
        public ARPlane GetLargest()
        {
            ARPlane largest = null;
            float maxArea = 0f;

            foreach (var p in _verticalPlanes)
            {
                float area = p.extents.x * p.extents.y;
                if (area > maxArea)
                {
                    maxArea = area;
                    largest = p;
                }
            }

            return largest;
        }

        /// <summary>Return the vertical plane closest to the given world position.</summary>
        public ARPlane GetClosestTo(Vector3 worldPos)
        {
            ARPlane closest = null;
            float minDist = float.MaxValue;

            foreach (var p in _verticalPlanes)
            {
                float dist = Vector3.Distance(p.transform.position, worldPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p;
                }
            }

            return closest;
        }

        /// <summary>Return the vertical plane most directly in front of the camera.</summary>
        public ARPlane GetMostCentral(Camera camera)
        {
            if (camera == null || _verticalPlanes.Count == 0) return null;

            ARPlane best = null;
            float bestDot = -1f;

            Vector3 camForward = camera.transform.forward;

            foreach (var p in _verticalPlanes)
            {
                Vector3 dirToPlane = (p.transform.position - camera.transform.position).normalized;
                float dot = Vector3.Dot(camForward, dirToPlane);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = p;
                }
            }

            return best;
        }
    }
}
