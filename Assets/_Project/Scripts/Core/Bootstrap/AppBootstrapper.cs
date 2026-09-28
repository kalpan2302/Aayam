using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aayam.Core.Bootstrap
{
    /// <summary>
    /// Application entry point. Attach to a GameObject in the Bootstrap scene.
    /// Initializes all core services, loads configuration, then transitions to the AR scene.
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string _arSceneName = "ARScene";
        [SerializeField] private bool _showSplashScreen = true;
        [SerializeField] private float _minimumSplashDuration = 1.5f;

        private float _startTime;

        private void Awake()
        {
            // Ensure only one bootstrapper exists
            DontDestroyOnLoad(gameObject);
            _startTime = Time.time;
        }

        private void Start()
        {
            InitializeApp();
        }

        private async void InitializeApp()
        {
            Debug.Log("[Bootstrapper] Starting Aayam initialization...");

            // Step 1: Initialize the service locator
            ServiceLocator.Initialize();
            Debug.Log("[Bootstrapper] ServiceLocator initialized.");

            // Step 2: Load app configuration
            var config = AppConfig.Load();
            ServiceLocator.Register(config);
            Debug.Log($"[Bootstrapper] AppConfig loaded. Version: {config.AppVersion}");

            // Step 3: Register core services
            RegisterCoreServices();

            // Step 4: Wait for minimum splash duration (if enabled)
            if (_showSplashScreen)
            {
                float elapsed = Time.time - _startTime;
                if (elapsed < _minimumSplashDuration)
                {
                    float remaining = _minimumSplashDuration - elapsed;
                    await System.Threading.Tasks.Task.Delay(
                        (int)(remaining * 1000)
                    );
                }
            }

            // Step 5: Transition to AR scene
            Debug.Log($"[Bootstrapper] Loading {_arSceneName}...");
            SceneManager.LoadScene(_arSceneName);
        }

        private void RegisterCoreServices()
        {
            // Register event bus
            var eventBus = new EventBus();
            ServiceLocator.Register(eventBus);
            Debug.Log("[Bootstrapper] EventBus registered.");

            // Future services will be registered here:
            // - ProductService
            // - ModelLoaderService
            // - CompatibilityService
            // - AnalyticsService
        }
    }
}

