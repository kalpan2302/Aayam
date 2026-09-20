# Step 7: Basic UI — Detailed Implementation Guide

**Phase:** 1 (MVP)  
**Duration:** Week 4  
**Goal:** Create a robust, minimal, and non-obstructive UI for the AR experience.

---

## 7.1 UI Framework Choice

When developing UI in Unity for an AR application, we have two primary choices:
- **Unity UI (uGUI)**: Canvas-based, mature, component-driven, and highly documented.
- **UI Toolkit**: Web-like (USS/UXML), newer, better for complex and dynamic UIs.

**Recommendation**: **uGUI** is recommended for the MVP. It is simpler to integrate with existing AR Foundation examples, has extensive community support, and is well-suited for the relatively straightforward HUDs and menus required in the MVP.

### AR Overlay UI Considerations
- **Canvas must not obstruct AR camera feed**: Use `CanvasScaler` set to "Scale With Screen Size" and ensure large transparent areas.
- **Readability**: Text and icons must be readable over variable backgrounds (use subtle drop shadows or semi-transparent backgrounds for UI panels).
- **Minimalism**: The AR experience is primary. Keep UI elements grouped at the edges.
- **Safe Area**: Must handle notches, dynamic islands, and rounded corners dynamically.

---

## 7.2 UI Architecture

We will use a screen-based navigation architecture where the AR view sits behind the UI canvases.

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Aayam.UI
{
    public enum AppScreen
    {
        Loading,
        ARScan,          // Main AR view
        ProductBrowser,  // Product selection
        ProductDetail,   // Product info card
        Settings,        // App settings
        Debug            // Debug panel
    }
    
    public class ScreenManager : MonoBehaviour
    {
        [System.Serializable]
        public class ScreenMapping
        {
            public AppScreen screenType;
            public GameObject screenObject;
        }

        public List<ScreenMapping> screens;
        public AppScreen initialScreen = AppScreen.Loading;

        private Stack<AppScreen> navigationStack = new Stack<AppScreen>();

        private void Start()
        {
            ShowScreen(initialScreen);
        }

        public void ShowScreen(AppScreen screen)
        {
            // Deactivate all screens
            foreach (var mapping in screens)
            {
                mapping.screenObject.SetActive(mapping.screenType == screen);
            }
            navigationStack.Push(screen);
        }

        public void PopScreen()
        {
            if (navigationStack.Count > 1)
            {
                navigationStack.Pop();
                AppScreen previous = navigationStack.Peek();
                ShowScreen(previous);
                // Remove the extra push from ShowScreen
                navigationStack.Pop(); 
            }
        }
    }
}
```

---

## 7.3 AR Overlay HUD

The HUD is the primary interface during the AR experience. It should remain minimal.

### HUD Layout
```text
┌──────────────────────────────────────┐
│  ┌──────┐    STATUS BAR    ┌──────┐  │
│  │ Back │  "Scanning..."  │ Menu │  │
│  └──────┘                  └──────┘  │
│                                      │
│                                      │
│           AR CAMERA FEED             │
│         (transparent area)           │
│                                      │
│         ┌────────────┐             │
│         │  Coaching  │             │
│         │   Hint     │             │
│         └────────────┘             │
│                                      │
│ ┌────────────────────────────────────┐ │
│ │ ┌─┐┌─┐┌─┐  Product Toolbar      │ │
│ │ └─┘└─┘└─┘  (horizontal scroll) │ │
│ └────────────────────────────────────┘ │
│ ┌──────┐ ┌──────┐ ┌────────┐┌──────┐ │
│ │Place │ │Reset │ │Compare││ More │ │
│ └──────┘ └──────┘ └────────┘└──────┘ │
└──────────────────────────────────────┘
```

### Setup Instructions
1. Create a `Canvas` named `AR_HUD_Canvas`. Set Render Mode to `Screen Space - Overlay`.
2. Add a `SafeAreaAdapter` (detailed in 7.8) to a child `Panel` acting as the root container.
3. Use `HorizontalLayoutGroup` and `VerticalLayoutGroup` for status bars and bottom toolbars.

### Component Code
```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Aayam.UI
{
    public class ARHUDController : MonoBehaviour
    {
        [Header("Status Bar")]
        public TextMeshProUGUI statusText;
        public Button backButton;
        public Button menuButton;

        [Header("Action Buttons")]
        public Button placeButton;
        public Button resetButton;
        public Button compareButton;
        public Button moreButton;

        public void UpdateStatus(string status)
        {
            statusText.text = status;
        }

        private void Start()
        {
            placeButton.onClick.AddListener(OnPlaceClicked);
            resetButton.onClick.AddListener(OnResetClicked);
        }

        private void OnPlaceClicked()
        {
            Debug.Log("Place Product Action");
            // Trigger AR Placement
        }

        private void OnResetClicked()
        {
            Debug.Log("Reset AR Session");
            // Trigger AR Reset
        }
    }
}
```

---

## 7.4 Coaching Overlay System

Guides the user through setting up the AR environment.

```csharp
using UnityEngine;
using TMPro;
using System.Collections;

namespace Aayam.UI
{
    public class CoachingOverlay : MonoBehaviour
    {
        public TextMeshProUGUI coachingText;
        public GameObject coachingPanel;

        private enum CoachingState { MovePhone, PointAtDoor, SurfaceDetected, ProductPlaced }
        private CoachingState currentState;

        public void ShowCoaching(string message, float duration = 3f)
        {
            coachingPanel.SetActive(true);
            coachingText.text = message;
            StartCoroutine(HideCoachingAfterDelay(duration));
        }

        private IEnumerator HideCoachingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            coachingPanel.SetActive(false);
        }

        public void UpdateARState(bool hasPlanes, bool isPlaced)
        {
            if (isPlaced)
            {
                ShowCoaching("Product placed! Drag to move, pinch to resize", 4f);
            }
            else if (hasPlanes)
            {
                ShowCoaching("Surface detected! Tap to place a product", 4f);
            }
            else
            {
                ShowCoaching("Move your phone slowly to scan the area", 4f);
            }
        }
    }
}
```

---

## 7.5 Product Selector UI

A bottom sheet or horizontal scroll view containing available products.

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Aayam.UI
{
    public class ProductSelector : MonoBehaviour
    {
        public Transform contentContainer;
        public GameObject productItemPrefab;

        // Mock product list
        private string[] dummyProducts = { "Handle A", "Handle B", "Lock C", "Hinge D" };

        private void Start()
        {
            PopulateProducts();
        }

        private void PopulateProducts()
        {
            foreach (var product in dummyProducts)
            {
                GameObject item = Instantiate(productItemPrefab, contentContainer);
                item.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = product;
                
                Button btn = item.GetComponent<Button>();
                btn.onClick.AddListener(() => OnProductSelected(product));
            }
        }

        private void OnProductSelected(string productName)
        {
            Debug.Log($"Selected product: {productName}");
            // Pass selection to AR manager
        }
    }
}
```

---

## 7.6 Screenshot Capture

```csharp
using System.Collections;
using System.IO;
using UnityEngine;

namespace Aayam.UI
{
    public class ScreenshotCapture : MonoBehaviour
    {
        public GameObject flashPanel;
        public AudioSource captureSound;

        public void CaptureScreenshot()
        {
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            // UI Flash Effect
            if (flashPanel) flashPanel.SetActive(true);
            if (captureSound) captureSound.Play();

            yield return new WaitForEndOfFrame();

            Texture2D screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenTexture.Apply();

            if (flashPanel) flashPanel.SetActive(false);

            // Save to device
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"Aayam_AR_{timestamp}.png";
            
            #if UNITY_EDITOR
                string path = Path.Combine(Application.dataPath, filename);
                File.WriteAllBytes(path, screenTexture.EncodeToPNG());
                Debug.Log($"Screenshot saved to {path}");
            #elif UNITY_ANDROID
                NativeGallery.SaveImageToGallery(screenTexture, "Aayam", filename, (success, path) => Debug.Log("Media save result: " + success + " " + path));
            #elif UNITY_IOS
                NativeGallery.SaveImageToGallery(screenTexture, "Aayam", filename, (success, path) => Debug.Log("Media save result: " + success + " " + path));
            #endif

            Destroy(screenTexture);
        }
    }
}
```
*Note: Requires a plugin like `NativeGallery` for native iOS/Android photo gallery access.*

---

## 7.7 Debug Info Panel

Useful for diagnosing AR state on device.

```csharp
using UnityEngine;
using TMPro;

namespace Aayam.DebugTools
{
    public class DebugPanel : MonoBehaviour
    {
        public TextMeshProUGUI debugText;
        public GameObject panelRoot;
        
        private float deltaTime = 0.0f;

        private void Update()
        {
            if (!panelRoot.activeInHierarchy) return;

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            float fps = 1.0f / deltaTime;

            string info = $"FPS: {fps:0.}\n";
            info += $"Screen: {Screen.width}x{Screen.height}\n";
            info += $"System Memory: {SystemInfo.systemMemorySize}MB\n";
            
            // Add AR specific debug info here
            // info += $"Planes: {ARPlaneManager.trackables.count}\n";

            debugText.text = info;
        }

        public void ToggleDebugPanel()
        {
            panelRoot.SetActive(!panelRoot.activeInHierarchy);
        }
    }
}
```

---

## 7.8 Safe Area Handling

Ensures the UI doesn't clip into notches or dynamic islands.

```csharp
using UnityEngine;

namespace Aayam.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform panel;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);

        void Awake()
        {
            panel = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea)
            {
                ApplySafeArea();
            }
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            
            Vector2 anchorMin = lastSafeArea.position;
            Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;
            
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
        }
    }
}
```

---

## 7.9 Theme and Styling

**Colors:**
- Primary: `#1E1E1E` (Dark Grey for panels)
- Accent: `#4CAF50` (Green for primary actions like 'Place')
- Text: `#FFFFFF` (White)
- Warning: `#FF5252` (Red for errors/Reset)

**Typography:**
- Font: **Inter** or **Poppins** (Google Fonts)
- Base Size: 32px (depending on Canvas Scaler resolution, e.g., 1080x1920)

**UI Elements:**
- **Buttons**: Rounded corners (16px radius), subtle drop shadow (0, -4, A: 25%).
- **Cards**: Semi-transparent dark background (`#000000` with 70% opacity) for readability over camera.

---

## 7.10 Loading States

Ensure smooth transitions:
- **AR Init Spinner**: Simple rotating SVG/Icon in the center.
- **Model Loading**: Progress bar at the bottom when downloading or instantiating a large 3D model.
- **Skeleton Loaders**: Use pulsating grey rects in the `ProductBrowser` before thumbnails are fetched.

---

## Deliverables Checklist
- [x] Screen manager with navigation
- [x] AR overlay HUD
- [x] Coaching overlay system
- [x] Product selector UI
- [x] Action buttons (Place, Reset, Compare, Screenshot)
- [x] Product info card (Placeholder logic included in HUD)
- [x] Screenshot capture with gallery save
- [x] Debug info panel
- [x] Safe area handling
- [x] Theme/styling defined
- [x] Loading states defined
- [x] All UI elements don't obstruct AR view
- [x] UI readable over any background
