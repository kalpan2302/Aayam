using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Continuously raycasts from screen center against AR planes and feature points,
    /// updating the reticle position/orientation every frame.
    /// Colour-codes the reticle: green = valid vertical surface, red = invalid.
    /// </summary>
    public class PlacementReticle : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────
        [Header("AR Components")]
        [SerializeField] private ARRaycastManager _raycastManager;
        [SerializeField] private ARPlaneManager   _planeManager;

        [Header("Reticle Visuals")]
        [SerializeField] private GameObject _reticlePrefab;
        [SerializeField] private Color      _validColor   = new Color(0.2f, 1f, 0.4f, 0.9f);
        [SerializeField] private Color      _invalidColor = new Color(1f, 0.3f, 0.3f, 0.9f);

        [Header("Settings")]
        [SerializeField] private float _maxDistance     = 4.0f;
        [SerializeField] private bool  _verticalOnly    = true;   // Doors are vertical
        [SerializeField] private float _positionSmoothing = 12f;  // Lerp speed

        // ─── State ────────────────────────────────────────────────
        public Pose  CurrentPose      { get; private set; }
        public bool  IsValidSurface   { get; private set; }
        public bool  IsVisible        { get; private set; }

        private GameObject    _reticleInstance;
        private MeshRenderer  _reticleRenderer;
        private MaterialPropertyBlock _propBlock;

        private readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            if (_reticlePrefab != null)
            {
                _reticleInstance = Instantiate(_reticlePrefab);
                _reticleRenderer = _reticleInstance.GetComponentInChildren<MeshRenderer>();
                _propBlock = new MaterialPropertyBlock();
                _reticleInstance.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[PlacementReticle] No reticle prefab assigned — using default sphere.");
                _reticleInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _reticleInstance.transform.localScale = Vector3.one * 0.05f;
                Destroy(_reticleInstance.GetComponent<Collider>());
                _reticleRenderer = _reticleInstance.GetComponent<MeshRenderer>();
                _propBlock = new MaterialPropertyBlock();
                _reticleInstance.SetActive(false);
            }
        }

        private void Update()
        {
            UpdateReticlePose();
        }

        // ─── Raycast Update ───────────────────────────────────────

        private void UpdateReticlePose()
        {
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _hits.Clear();

            bool hit = _raycastManager.Raycast(
                screenCenter,
                _hits,
                TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint
            );

            IsValidSurface = false;

            if (hit && _hits.Count > 0)
            {
                ARRaycastHit bestHit = _hits[0];

                if (bestHit.distance <= _maxDistance)
                {
                    bool isVertical = IsVerticalHit(bestHit);
                    IsValidSurface = !_verticalOnly || isVertical;

                    CurrentPose = bestHit.pose;
                    UpdateReticleTransform();
                    SetReticleColor(IsValidSurface ? _validColor : _invalidColor);
                }
            }

            SetVisible(IsValidSurface);
        }

        // ─── Vertical Check ───────────────────────────────────────

        private bool IsVerticalHit(ARRaycastHit hit)
        {
            if ((hit.hitType & TrackableType.PlaneWithinPolygon) != 0 && _planeManager != null)
            {
                var plane = _planeManager.GetPlane(hit.trackableId);
                if (plane != null)
                    return plane.alignment == PlaneAlignment.Vertical;
            }
            // For feature points, check the surface normal
            Vector3 normal = hit.pose.up;
            return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.25f;
        }

        // ─── Visuals ──────────────────────────────────────────────

        private void UpdateReticleTransform()
        {
            // Smooth position to reduce jitter
            _reticleInstance.transform.position = Vector3.Lerp(
                _reticleInstance.transform.position,
                CurrentPose.position,
                Time.deltaTime * _positionSmoothing
            );

            // Align to surface normal (pose rotation already encodes this)
            _reticleInstance.transform.rotation = CurrentPose.rotation;
        }

        private void SetReticleColor(Color color)
        {
            if (_reticleRenderer == null) return;
            _reticleRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorProp, color);
            _reticleRenderer.SetPropertyBlock(_propBlock);
        }

        private void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            IsVisible = visible;
            _reticleInstance?.SetActive(visible);
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Force-hide the reticle (e.g., when object is placed).</summary>
        public void Hide() => SetVisible(false);

        /// <summary>Resume reticle updating (e.g., when starting new placement).</summary>
        public void Show() => enabled = true;
    }
}
