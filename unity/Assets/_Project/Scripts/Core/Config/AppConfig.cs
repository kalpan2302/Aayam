using UnityEngine;

namespace Aayam.Core.Bootstrap
{
    /// <summary>
    /// Application configuration. Loaded at startup from a ScriptableObject
    /// or JSON config file. Provides centralized access to app-wide settings.
    /// </summary>
    [System.Serializable]
    public class AppConfig
    {
        // ─── App Identity ─────────────────────────────────────────────
        public string AppVersion => Application.version;
        public string AppName => "Aayam";
        public string BundleId => Application.identifier;

        // ─── API Configuration ────────────────────────────────────────
        [Header("API")]
        public string ApiBaseUrl = "https://api.aayam.app/v1";
        public string CdnBaseUrl = "https://cdn.aayam.app";
        public float ApiTimeout = 30f;

        // ─── AR Configuration ─────────────────────────────────────────
        [Header("AR")]
        public float PlaneDetectionTimeout = 10f;
        public float TrackingLostTimeout = 5f;
        public bool EnableLightEstimation = true;
        public bool EnableOcclusion = true;
        public bool EnableDepthPlacement = true;
        public int MaxPlacedObjects = 5;

        // ─── Model Loading ────────────────────────────────────────────
        [Header("Models")]
        public int ModelCacheMaxSizeMB = 200;
        public float ModelLoadTimeoutSeconds = 10f;
        public string ModelCachePath => $"{Application.persistentDataPath}/models";

        // ─── Debug ────────────────────────────────────────────────────
        [Header("Debug")]
        public bool DebugMode = false;
        public bool ShowFpsCounter = false;
        public bool ShowPlaneVisualization = false;
        public bool ShowDepthVisualization = false;
        public bool VerboseLogging = false;

        // ─── Analytics ────────────────────────────────────────────────
        [Header("Analytics")]
        public bool EnableAnalytics = true;
        public string AnalyticsProjectId = "";

        /// <summary>
        /// Load configuration. For MVP, returns default values.
        /// Later: load from ScriptableObject, remote config, or JSON.
        /// </summary>
        public static AppConfig Load()
        {
            // Try to load from Resources (ScriptableObject wrapper)
            // For now, return default config
            var config = new AppConfig();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            config.DebugMode = true;
            config.ShowFpsCounter = true;
            config.VerboseLogging = true;
            config.ShowPlaneVisualization = true;
#endif

            Debug.Log($"[AppConfig] Loaded. Debug={config.DebugMode}, API={config.ApiBaseUrl}");
            return config;
        }

        /// <summary>
        /// Check if the device is suitable for the full AR experience.
        /// </summary>
        public bool IsFullARSupported()
        {
            // Minimum requirements check
            return SystemInfo.supportsGyroscope &&
                   SystemInfo.deviceType == DeviceType.Handheld;
        }
    }
}

