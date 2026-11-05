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
        // 2026-11-04: visual rebrand to "Fitora" (see FitoraTheme/FitoraIcons) -
        // app identity (bundle ID com.kalpan2302.aayam, project/repo name,
        // backend package names) intentionally left as Aayam. Renaming those
        // would force a fresh device install + developer-trust cycle for no
        // visual benefit; only what the user actually sees changes here.
        public string AppName => "Fitora";
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
        // TEMP for this device-test pass only (2026-10-29, Phase 6) -
        // localhost means the device itself on a physical phone. Revert
        // to "http://localhost:8010" before committing.
        // 2026-11-03: .102 was stale - this Mac's DHCP lease moved to .104
        // sometime after 10-29, silently breaking the on-device backend
        // fetch (ProductSelector swallows the failure and falls back to its
        // 5-item LoadStubProducts() list - confirmed live on-device: the
        // "only 5 products" the stub list's exact names matched, not the
        // real 20-product catalog the backend actually serves).
        // 2026-11-04: happened AGAIN one day later - .104 was already stale,
        // moved to .103 (confirmed via `ipconfig getifaddr en0` + curl).
        // Two DHCP changes in two days means hardcoding an IP here is not a
        // durable fix - this machine's Bonjour hostname (`scutil --get
        // LocalHostName` -> "hemutindia5") didn't resolve via curl from the
        // Mac itself when tried, so ".local" isn't a proven alternative yet
        // either. Real fix is a DHCP reservation/static IP for this Mac on
        // the router (outside this app) so the address stops moving; until
        // then, re-check with `ipconfig getifaddr en0` any time products
        // silently fall back to the 5-item stub list again.
        public string ApiBaseUrl = "http://192.168.0.103:8010";
        public string CdnBaseUrl = "http://192.168.0.103:8010";
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

