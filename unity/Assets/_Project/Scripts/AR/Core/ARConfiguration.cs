using UnityEngine;

namespace Aayam.AR.Core
{
    /// <summary>
    /// ScriptableObject for AR configuration.
    /// Create via Assets menu: Aayam > AR Configuration.
    /// Allows tuning AR parameters without modifying code.
    /// </summary>
    [CreateAssetMenu(fileName = "ARConfiguration", menuName = "Aayam/AR Configuration")]
    public class ARConfiguration : ScriptableObject
    {
        // ─── Timeouts ─────────────────────────────────────────────
        [Header("Timeouts")]
        [Tooltip("Seconds before prompting user to move device to find planes.")]
        public float PlaneDetectionTimeout = 10f;

        [Tooltip("Seconds of tracking loss before showing critical error.")]
        public float TrackingLostTimeout = 5f;

        // ─── Plane Detection ──────────────────────────────────────
        [Header("Plane Detection")]
        [Tooltip("Maximum number of planes to track simultaneously.")]
        public int MaxTrackedPlanes = 20;

        [Tooltip("Minimum plane area (m²) to consider valid.")]
        public float MinPlaneArea = 0.1f;

        [Tooltip("Disable plane detection after target surface is found.")]
        public bool AutoDisablePlaneDetection = false;

        // ─── Feature Toggles ──────────────────────────────────────
        [Header("Feature Toggles")]
        public bool EnableLightEstimation = true;
        public bool EnableOcclusion = true;
        public bool EnablePointClouds = false;
        public bool EnableDepthPlacement = true;
        public bool EnableMeshing = false;

        // ─── Placement ────────────────────────────────────────────
        [Header("Placement")]
        [Tooltip("Maximum number of objects that can be placed simultaneously.")]
        public int MaxPlacedObjects = 5;

        [Tooltip("Minimum distance from camera for placement (meters).")]
        public float MinPlacementDistance = 0.3f;

        [Tooltip("Maximum distance from camera for placement (meters).")]
        public float MaxPlacementDistance = 5.0f;

        [Tooltip("Offset from surface to prevent z-fighting (meters).")]
        public float SurfaceOffset = 0.001f;

        // ─── Interaction ──────────────────────────────────────────
        [Header("Interaction")]
        [Tooltip("Enable scaling of placed objects.")]
        public bool EnableScaling = false; // Off by default — products must be real-size

        [Tooltip("Minimum scale factor (1.0 = real size).")]
        public float MinScale = 0.8f;

        [Tooltip("Maximum scale factor (1.0 = real size).")]
        public float MaxScale = 1.2f;

        [Tooltip("Snap rotation to nearest N degrees (0 = free rotation).")]
        public float RotationSnapDegrees = 0f;

        // ─── Door Detection (Heuristic) ──────────────────────────
        [Header("Door Detection Heuristics")]
        [Tooltip("Minimum door width in meters.")]
        public float MinDoorWidth = 0.6f;

        [Tooltip("Maximum door width in meters.")]
        public float MaxDoorWidth = 1.3f;

        [Tooltip("Minimum door height in meters.")]
        public float MinDoorHeight = 1.7f;

        [Tooltip("Maximum door height in meters.")]
        public float MaxDoorHeight = 2.5f;

        // ─── Debug ────────────────────────────────────────────────
        [Header("Debug")]
        public bool DebugMode = false;
        public bool ShowPlaneVisualization = false;
        public bool ShowFeaturePoints = false;
        public bool ShowPlaneClassification = false;
        public bool ShowDepthOverlay = false;
        public bool LogAREvents = true;

        // ─── AR Layer ─────────────────────────────────────────────
        [Header("Layers")]
        [Tooltip("Layer for AR-interactable objects (for raycasting).")]
        public LayerMask ARInteractableLayer;
    }
}
