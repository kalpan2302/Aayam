using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Aayam.UI
{
    /// <summary>
    /// Renders a 3D model in a RenderTexture for a product preview card (UI RawImage).
    /// Supports orbital rotation via drag, auto-spin, and zoom.
    ///
    /// Setup:
    ///   1. Create a dedicated Preview Camera (culling mask = UI3D layer only).
    ///   2. Set its Target Texture to a RenderTexture asset (e.g., 512×512 R8G8B8A8).
    ///   3. Assign the RenderTexture to a UI RawImage to display in the canvas.
    ///   4. Assign this script's previewCamera + modelContainer + rawImage fields.
    /// </summary>
    public class ModelPreview : MonoBehaviour,
        UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler
    {
        [Header("References")]
        [SerializeField] private Camera    _previewCamera;
        [SerializeField] private Transform _modelContainer;   // Parent for preview model instance
        [SerializeField] private RawImage  _displayImage;     // UI element showing the RenderTexture

        [Header("Rotation")]
        [SerializeField] private float _dragSensitivity = 0.5f;
        [SerializeField] private bool  _autoRotate      = true;
        [SerializeField] private float _autoRotateSpeed = 20f;

        [Header("Camera Distance")]
        [SerializeField] private float _cameraDistance     = 1.5f;
        [SerializeField] private float _minCameraDistance  = 0.5f;
        [SerializeField] private float _maxCameraDistance  = 4.0f;

        [Header("Loading UI")]
        [SerializeField] private GameObject _loadingSpinner;

        // ─── State ────────────────────────────────────────────────
        private GameObject _currentModel;
        private bool       _isDragging;
        private int        _previewLayer;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _previewLayer = LayerMask.NameToLayer("UI");
            if (_previewLayer < 0)
            {
                // Fallback — use layer 5 (Unity's built-in "UI" layer)
                _previewLayer = 5;
            }
        }

        private void Update()
        {
            if (_autoRotate && !_isDragging && _currentModel != null)
            {
                _modelContainer.Rotate(Vector3.up, _autoRotateSpeed * Time.deltaTime, Space.World);
            }
        }

        // ─── Model Display ────────────────────────────────────────

        /// <summary>
        /// Display a model in the preview window.
        /// Handles async loading from the ModelLoader if given a URL.
        /// </summary>
        public void DisplayModel(GameObject modelInstance)
        {
            ClearPreview();

            _currentModel = modelInstance;
            _currentModel.transform.SetParent(_modelContainer, worldPositionStays: false);
            _currentModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            SetLayerRecursive(_currentModel, _previewLayer);
            NormalizePreviewScale(_currentModel);
            FitCameraToModel(_currentModel);

            _loadingSpinner?.SetActive(false);
            _displayImage?.gameObject.SetActive(true);

            Debug.Log($"[ModelPreview] Displaying model: {modelInstance.name}");
        }

        /// <summary>Show spinner while model is loading.</summary>
        public void ShowLoading()
        {
            _loadingSpinner?.SetActive(true);
            _displayImage?.gameObject.SetActive(false);
        }

        /// <summary>Destroy current preview model and hide display.</summary>
        public void ClearPreview()
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
            _loadingSpinner?.SetActive(false);
        }

        // ─── Drag Rotation ────────────────────────────────────────

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isDragging = true;
            if (_currentModel == null) return;

            float rotY = -eventData.delta.x * _dragSensitivity;
            float rotX =  eventData.delta.y * _dragSensitivity;

            _modelContainer.Rotate(Vector3.up,   rotY, Space.World);
            _modelContainer.Rotate(Vector3.right, rotX, Space.World);
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isDragging = false;
        }

        // ─── Helpers ──────────────────────────────────────────────

        /// <summary>Scale model to fit within a 1-unit preview cube.</summary>
        private void NormalizePreviewScale(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float maxDim    = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale     = maxDim > 0f ? 1f / maxDim : 1f;

            model.transform.localScale    = Vector3.one * scale;
            model.transform.localPosition = -bounds.center * scale;
        }

        /// <summary>Position preview camera to frame the model nicely.</summary>
        private void FitCameraToModel(GameObject model)
        {
            if (_previewCamera == null) return;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float radius = bounds.extents.magnitude;
            float fov    = _previewCamera.fieldOfView * Mathf.Deg2Rad;
            float dist   = Mathf.Clamp(radius / Mathf.Sin(fov * 0.5f), _minCameraDistance, _maxCameraDistance);

            _previewCamera.transform.position =
                _modelContainer.position + new Vector3(0, 0, -dist);
            _previewCamera.transform.LookAt(_modelContainer.position + bounds.center);
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
