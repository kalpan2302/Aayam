using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Attached to each AR plane prefab instance.
    /// Displays different materials based on plane type (floor, wall, door candidate).
    /// Responds to boundary changes and external classification updates.
    /// </summary>
    [RequireComponent(typeof(ARPlane), typeof(MeshRenderer))]
    public class PlaneVisualizationController : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material _horizontalPlaneMaterial;  // Blue tint — floor/table
        [SerializeField] private Material _verticalPlaneMaterial;    // Green tint — walls
        [SerializeField] private Material _doorCandidateMaterial;    // Gold/amber — door candidate

        [Header("Visibility")]
        [SerializeField] private bool _showInRelease = false;

        private ARPlane _arPlane;
        private MeshRenderer _meshRenderer;
        private bool _isDoorCandidate;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _arPlane = GetComponent<ARPlane>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            _arPlane.boundaryChanged += OnBoundaryChanged;
            RefreshMaterial();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            _arPlane.boundaryChanged -= OnBoundaryChanged;
        }

        // ─── Callbacks ────────────────────────────────────────────

        private void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs args)
        {
            RefreshMaterial();
        }

        // ─── Material ─────────────────────────────────────────────

        private void RefreshMaterial()
        {
            if (_meshRenderer == null) return;

            if (_isDoorCandidate)
            {
                SetMaterial(_doorCandidateMaterial);
                return;
            }

            // Use native classification first
            switch (_arPlane.classification)
            {
                case PlaneClassification.Door:
                    SetMaterial(_doorCandidateMaterial);
                    return;
                case PlaneClassification.Floor:
                case PlaneClassification.Table:
                case PlaneClassification.Seat:
                    SetMaterial(_horizontalPlaneMaterial);
                    return;
                case PlaneClassification.Wall:
                case PlaneClassification.Ceiling:
                    SetMaterial(_verticalPlaneMaterial);
                    return;
            }

            // Fallback: use alignment
            if (_arPlane.alignment == PlaneAlignment.Vertical)
                SetMaterial(_verticalPlaneMaterial);
            else
                SetMaterial(_horizontalPlaneMaterial);
        }

        private void SetMaterial(Material mat)
        {
            if (mat != null)
                _meshRenderer.material = mat;
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Mark or unmark this plane as a door candidate (changes highlight).</summary>
        public void SetAsDoorCandidate(bool isCandidate)
        {
            _isDoorCandidate = isCandidate;
            RefreshMaterial();
        }

        /// <summary>Show or hide the plane visualization mesh.</summary>
        public void SetVisible(bool visible)
        {
            _meshRenderer.enabled = visible;
        }

        // ─── Debug ────────────────────────────────────────────────

        private void ApplyVisibility()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (!_showInRelease)
                _meshRenderer.enabled = false;
#endif
        }
    }
}
