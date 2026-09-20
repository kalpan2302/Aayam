using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Core
{
    /// <summary>
    /// Applies AR light estimation data to a scene directional light.
    /// Makes virtual objects respond to real-world lighting for realism.
    /// Attach to the AR Camera (same GameObject as ARCameraManager).
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class LightEstimationController : MonoBehaviour
    {
        [Header("Target Light")]
        [SerializeField] private Light _directionalLight;

        [Header("Settings")]
        [SerializeField] private bool _estimateBrightness = true;
        [SerializeField] private bool _estimateColorTemperature = true;
        [SerializeField] private bool _estimateDirection = true;
        [SerializeField] private bool _estimateColor = true;

        [Header("Smoothing")]
        [Tooltip("Smooth transitions to avoid flickering")]
        [SerializeField] [Range(0f, 1f)] private float _smoothFactor = 0.1f;

        private ARCameraManager _cameraManager;
        private float _targetIntensity = 1f;
        private float _targetColorTemp = 6500f;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();

            // Auto-find directional light if not assigned
            if (_directionalLight == null)
            {
                _directionalLight = FindFirstObjectByType<Light>();
                if (_directionalLight != null)
                {
                    Debug.Log($"[LightEstimation] Auto-found light: {_directionalLight.name}");
                }
                else
                {
                    Debug.LogWarning("[LightEstimation] No directional light found. " +
                                     "Create one or assign it in the inspector.");
                }
            }
        }

        private void OnEnable()
        {
            if (_cameraManager != null)
                _cameraManager.frameReceived += OnCameraFrameReceived;
        }

        private void OnDisable()
        {
            if (_cameraManager != null)
                _cameraManager.frameReceived -= OnCameraFrameReceived;
        }

        private void Update()
        {
            if (_directionalLight == null) return;

            // Smooth intensity transitions to prevent flickering
            _directionalLight.intensity = Mathf.Lerp(
                _directionalLight.intensity,
                _targetIntensity,
                _smoothFactor
            );

            _directionalLight.colorTemperature = Mathf.Lerp(
                _directionalLight.colorTemperature,
                _targetColorTemp,
                _smoothFactor
            );
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_directionalLight == null) return;

            var estimation = args.lightEstimation;

            // Brightness / Intensity
            if (_estimateBrightness && estimation.averageBrightness.HasValue)
            {
                _targetIntensity = estimation.averageBrightness.Value;
            }
            if (_estimateBrightness && estimation.averageMainLightBrightness.HasValue)
            {
                _targetIntensity = estimation.averageMainLightBrightness.Value;
            }

            // Color Temperature
            if (_estimateColorTemperature && estimation.averageColorTemperature.HasValue)
            {
                _targetColorTemp = estimation.averageColorTemperature.Value;
            }

            // Main Light Direction
            if (_estimateDirection && estimation.mainLightDirection.HasValue)
            {
                _directionalLight.transform.rotation =
                    Quaternion.LookRotation(estimation.mainLightDirection.Value);
            }

            // Main Light Color
            if (_estimateColor && estimation.mainLightColor.HasValue)
            {
                _directionalLight.color = estimation.mainLightColor.Value;
            }

            // Publish to event bus for other systems
            AREventBus.RaiseLightEstimationUpdated(_targetIntensity);
        }
    }
}
