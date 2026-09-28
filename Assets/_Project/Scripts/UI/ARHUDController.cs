using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aayam.AR.Core;
using Aayam.AR.Planes;
using Aayam.AR.Placement;

namespace Aayam.UI
{
    /// <summary>
    /// Controls the AR HUD: status bar, action buttons, and coaching integration.
    ///
    /// HUD Layout:
    ///   TOP:    [Back]  Status Text  [Menu]
    ///   CENTER: AR camera (transparent)
    ///   MID:    Coaching hint (contextual)
    ///   BOTTOM: [Place] [Reset] [Compare] [Screenshot]
    ///
    /// Wires to ARSessionController, DoorCandidateDetector, ObjectPlacementController.
    /// </summary>
    public class ARHUDController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────
        [Header("Status Bar")]
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Button          _backButton;
        [SerializeField] private Button          _menuButton;

        [Header("Action Buttons")]
        [SerializeField] private Button _placeButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _compareButton;
        [SerializeField] private Button _screenshotButton;

        [Header("Button Labels")]
        [SerializeField] private TextMeshProUGUI _placeButtonLabel;

        [Header("Dependencies")]
        [SerializeField] private ScreenManager              _screenManager;
        [SerializeField] private ARSessionController        _sessionController;
        [SerializeField] private DoorCandidateDetector      _doorDetector;
        [SerializeField] private ObjectPlacementController  _placementController;
        [SerializeField] private CoachingOverlay            _coaching;
        [SerializeField] private ScreenshotCapture          _screenshot;

        // ─── State ────────────────────────────────────────────────
        private bool _isPlacingMode = false;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            // Wire buttons
            _backButton?.onClick.AddListener(OnBackClicked);
            _menuButton?.onClick.AddListener(OnMenuClicked);
            _placeButton?.onClick.AddListener(OnPlaceClicked);
            _resetButton?.onClick.AddListener(OnResetClicked);
            _compareButton?.onClick.AddListener(OnCompareClicked);
            _screenshotButton?.onClick.AddListener(OnScreenshotClicked);

            // Subscribe to AR events
            AREventBus.OnSessionStateChanged += OnSessionStateChanged;
            AREventBus.OnTrackingLost        += OnTrackingLost;
            AREventBus.OnTrackingRecovered   += OnTrackingRecovered;
            AREventBus.OnObjectPlaced        += (p, r) => OnObjectPlaced();

            // Initial state
            UpdateStatus("Initializing AR…");
            SetPlaceButtonEnabled(false);
        }

        private void OnDestroy()
        {
            AREventBus.OnSessionStateChanged -= OnSessionStateChanged;
            AREventBus.OnTrackingLost        -= OnTrackingLost;
            AREventBus.OnTrackingRecovered   -= OnTrackingRecovered;
        }

        private void Update()
        {
            RefreshPlaceButton();
        }

        // ─── AR Event Handlers ────────────────────────────────────

        private void OnSessionStateChanged(SessionState state)
        {
            UpdateStatus(state switch
            {
                SessionState.CheckingAvailability => "Checking AR support…",
                SessionState.Installing           => "Installing AR services…",
                SessionState.Ready                => "Ready",
                SessionState.SessionInitializing  => "Starting AR…",
                SessionState.SessionTracking      => "Scan your door",
                SessionState.Paused               => "AR Paused",
                SessionState.Error                => "AR Error",
                SessionState.NotSupported         => "AR Not Supported",
                _                                 => ""
            });
        }

        private void OnTrackingLost()
        {
            UpdateStatus("Tracking lost — move phone slowly");
            SetPlaceButtonEnabled(false);
        }

        private void OnTrackingRecovered()
        {
            UpdateStatus("Scan your door");
        }

        private void OnObjectPlaced()
        {
            _isPlacingMode = false;
            UpdateStatus("Product placed!");
            SetPlaceButtonLabel("Add Another");
            _coaching?.ShowCoaching("Drag to reposition • Twist to rotate", 4f);
        }

        // ─── Button Handlers ──────────────────────────────────────

        private void OnBackClicked()
        {
            if (_isPlacingMode)
            {
                _placementController?.CancelPlacement();
                _isPlacingMode = false;
                SetPlaceButtonLabel("Place");
            }
            else
            {
                _screenManager?.GoBack();
            }
        }

        private void OnMenuClicked()
        {
            _screenManager?.ShowScreen(AppScreen.Settings);
        }

        private void OnPlaceClicked()
        {
            if (!_isPlacingMode)
            {
                // Go to product browser first, then placement starts after product selection
                _screenManager?.ShowScreen(AppScreen.ProductBrowser);
            }
            else
            {
                _placementController?.CancelPlacement();
                _isPlacingMode = false;
                SetPlaceButtonLabel("Place");
            }
        }

        private void OnResetClicked()
        {
            _placementController?.RemoveAll();
            _sessionController?.ResetSession();
            UpdateStatus("AR reset — scan your door again");
            SetPlaceButtonLabel("Place");
            _isPlacingMode = false;
        }

        private void OnCompareClicked()
        {
            // Placeholder — Phase 6 (comparison view)
            Debug.Log("[HUD] Compare clicked — not yet implemented.");
        }

        private void OnScreenshotClicked()
        {
            _screenshot?.CaptureScreenshot();
        }

        // ─── Helpers ──────────────────────────────────────────────

        public void UpdateStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private void SetPlaceButtonEnabled(bool enabled)
        {
            if (_placeButton != null)
                _placeButton.interactable = enabled;
        }

        private void SetPlaceButtonLabel(string label)
        {
            if (_placeButtonLabel != null)
                _placeButtonLabel.text = label;
        }

        /// <summary>Enable place button when a confident door candidate exists.</summary>
        private void RefreshPlaceButton()
        {
            bool canPlace = _doorDetector != null && _doorDetector.HasConfidentCandidate;
            SetPlaceButtonEnabled(canPlace);
        }

        /// <summary>Called externally when a product has been selected and placement should start.</summary>
        public void BeginPlacementFor(GameObject prefab, string productId)
        {
            _placementController?.StartPlacement(prefab, productId);
            _isPlacingMode = true;
            SetPlaceButtonLabel("Cancel");
            UpdateStatus("Aim at door and tap to place");
            _screenManager?.ShowScreen(AppScreen.ARScan);
        }
    }
}
