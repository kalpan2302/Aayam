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
        // Dev default: the backend runs locally and serves both the API and
        // static assets (see backend/app/main.py's /static mount) from the
        // same host - no AWS/CDN account needed yet (Phase 2 Step 3).
        // Port 8010, not 8000 - this machine runs another, unrelated Docker
        // container on 8000. On a physical device, "localhost" means the
        // device itself, not your laptop - use your laptop's LAN IP (e.g.
        // "http://192.168.1.23:8010") or a tunnel instead.
        [Header("API")]
        public string ApiBaseUrl = "http://localhost:8010";
        public string CdnBaseUrl = "http://localhost:8010";
        public float ApiTimeout = 30f;
        // 2026-10-29, Phase 5: the backend's verify_api_key dependency
        // (app/api/deps.py) existed since before Phase 5 but no route
        // required it until MeasurementApiService's POST endpoints - this
        // is the first place the Unity client actually needs to send one.
        // Matches the backend's dev default (app/config.py's Settings.API_KEY)
        // - change both together if this is ever rotated.
        public string ApiKey = "dev-api-key-change-me";

        [Header("Measurement")]
        // Phase 5 Step 7: best-effort backend sync for measurement history -
        // local save always happens regardless (MeasurementHistoryStore),
        // this only gates whether it ALSO tries the network call.
        public bool EnableMeasurementBackendSync = true;

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

