using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

namespace Aayam.AR.Core
{
    /// <summary>
    /// Detects device AR capabilities at runtime.
    /// Used to enable/disable features (occlusion, depth placement, meshing)
    /// based on what the hardware actually supports.
    /// </summary>
    public class DeviceCapabilityChecker : MonoBehaviour
    {
        // --- Public API ---
        public DeviceCapabilities Capabilities { get; private set; } = new DeviceCapabilities();

        /// <summary>
        /// Quality tier based on device capabilities.
        /// Used by other systems (occlusion, visualization) to set quality.
        /// </summary>
        public DeviceQualityTier QualityTier { get; private set; } = DeviceQualityTier.Medium;

        // --- Lifecycle ---

        private void Start()
        {
            CheckCapabilities();
            DetermineQualityTier();
        }

        // --- Capability Detection ---

        public void CheckCapabilities()
        {
            Capabilities.DeviceModel = SystemInfo.deviceModel;
            Capabilities.OSVersion = SystemInfo.operatingSystem;

            // General AR support
            Capabilities.SupportsAR = ARSession.state >= ARSessionState.Ready;
            Capabilities.SupportsWorldTracking = Capabilities.SupportsAR;

            // Plane detection
            var planeDescriptors = new List<XRPlaneSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(planeDescriptors);
            Capabilities.SupportsPlaneDetection = planeDescriptors.Count > 0;
            Capabilities.SupportsVerticalPlanes = planeDescriptors.Count > 0;

            // Occlusion & depth
            var occlusionDescriptors = new List<XROcclusionSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(occlusionDescriptors);
            if (occlusionDescriptors.Count > 0)
            {
                Capabilities.SupportsOcclusion = true;
                Capabilities.SupportsDepth = true;

                // In AR Foundation 6.x, capability queries are done via the subsystem
                // at runtime rather than via descriptor properties. For now, we assume
                // occlusion support implies depth and human occlusion on capable devices.
                Capabilities.SupportsHumanOcclusion = true;
                Capabilities.SupportsLiDAR = IsLiDARDevice();
            }

            // Face tracking
            var faceDescriptors = new List<XRFaceSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(faceDescriptors);
            Capabilities.SupportsFaceTracking = faceDescriptors.Count > 0;

            // Image tracking (for QR codes)
            var imageDescriptors = new List<XRImageTrackingSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(imageDescriptors);
            Capabilities.SupportsImageTracking = imageDescriptors.Count > 0;

            // Light estimation
            var cameraDescriptors = new List<XRCameraSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(cameraDescriptors);
            Capabilities.SupportsLightEstimation = cameraDescriptors.Count > 0;

            // Meshing (LiDAR scene reconstruction)
            // In AR Foundation 6.x, use ARMeshManager availability
            Capabilities.SupportsMeshing = Capabilities.SupportsLiDAR;

            LogCapabilities();
        }

        /// <summary>
        /// Heuristic check for LiDAR-capable devices (iPhone 12 Pro+, iPad Pro 2020+).
        /// </summary>
        private bool IsLiDARDevice()
        {
#if UNITY_IOS && !UNITY_EDITOR
            string model = SystemInfo.deviceModel;
            // iPhone 12 Pro+, iPhone 13 Pro+, iPhone 14 Pro+, iPhone 15 Pro+, iPad Pro (2020+)
            if (model.Contains("iPhone13,3") || model.Contains("iPhone13,4") || // 12 Pro/Max
                model.Contains("iPhone14,2") || model.Contains("iPhone14,3") || // 13 Pro/Max
                model.Contains("iPhone15,2") || model.Contains("iPhone15,3") || // 14 Pro/Max
                model.Contains("iPhone16,1") || model.Contains("iPhone16,2") || // 15 Pro/Max
                model.Contains("iPad8") || model.Contains("iPad13"))            // iPad Pro
                return true;
#endif
            return false;
        }

        // --- Quality Tier ---

        private void DetermineQualityTier()
        {
            if (Capabilities.SupportsLiDAR && Capabilities.SupportsMeshing)
            {
                QualityTier = DeviceQualityTier.Ultra;
            }
            else if (Capabilities.SupportsDepth && Capabilities.SupportsOcclusion)
            {
                QualityTier = DeviceQualityTier.High;
            }
            else if (Capabilities.SupportsPlaneDetection)
            {
                QualityTier = DeviceQualityTier.Medium;
            }
            else
            {
                QualityTier = DeviceQualityTier.Low;
            }

            Debug.Log($"[DeviceCapabilities] Quality Tier: {QualityTier}");
        }

        // --- Logging ---

        private void LogCapabilities()
        {
            Debug.Log("===================================");
            Debug.Log("     DEVICE CAPABILITY REPORT");
            Debug.Log("===================================");
            Debug.Log($"Device:           {Capabilities.DeviceModel}");
            Debug.Log($"OS:               {Capabilities.OSVersion}");
            Debug.Log($"AR Supported:     {Capabilities.SupportsAR}");
            Debug.Log($"Plane Detection:  {Capabilities.SupportsPlaneDetection}");
            Debug.Log($"Vertical Planes:  {Capabilities.SupportsVerticalPlanes}");
            Debug.Log($"Depth:            {Capabilities.SupportsDepth}");
            Debug.Log($"LiDAR/ToF:        {Capabilities.SupportsLiDAR}");
            Debug.Log($"Occlusion:        {Capabilities.SupportsOcclusion}");
            Debug.Log($"Human Occlusion:  {Capabilities.SupportsHumanOcclusion}");
            Debug.Log($"Light Estimation: {Capabilities.SupportsLightEstimation}");
            Debug.Log($"Image Tracking:   {Capabilities.SupportsImageTracking}");
            Debug.Log($"Meshing:          {Capabilities.SupportsMeshing}");
            Debug.Log("===================================");
        }
    }

    // --- Data Types ---

    [System.Serializable]
    public class DeviceCapabilities
    {
        public bool SupportsAR;
        public bool SupportsWorldTracking;
        public bool SupportsPlaneDetection;
        public bool SupportsVerticalPlanes;
        public bool SupportsDepth;
        public bool SupportsLiDAR;
        public bool SupportsOcclusion;
        public bool SupportsHumanOcclusion;
        public bool SupportsFaceTracking;
        public bool SupportsImageTracking;
        public bool SupportsLightEstimation;
        public bool SupportsMeshing;
        public string DeviceModel;
        public string OSVersion;
    }

    public enum DeviceQualityTier
    {
        /// <summary>LiDAR + Meshing (iPhone Pro, iPad Pro)</summary>
        Ultra,
        /// <summary>Depth + Occlusion (flagship phones)</summary>
        High,
        /// <summary>Plane detection, no depth (mid-range ARCore)</summary>
        Medium,
        /// <summary>Basic AR only (older devices)</summary>
        Low
    }
}
