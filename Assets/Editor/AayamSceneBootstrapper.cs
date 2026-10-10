using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

using Aayam.AR.Core;
using Aayam.AR.Planes;
using Aayam.AR.Placement;
using Aayam.Services;
using Aayam.UI;

namespace Aayam.Editor
{
    /// <summary>
    /// One-shot scene wiring for Phase 2 Steps 5-7 (catalogue -> download ->
    /// AR placement -> anchor-height snap -> QR scan). Builds the "plumbing":
    /// GameObjects, components, and their Inspector cross-references.
    /// Deliberately does NOT build real UI visual design (layout, styling,
    /// icons) - that's a design/taste decision left for you to do by hand in
    /// the Editor afterward. Also deliberately does NOT touch Phase 1's
    /// drag/rotate/scale interaction, occlusion, depth, or meshing systems -
    /// out of scope for this pass, not required for Steps 5-7.
    ///
    /// Requires a base AR Foundation rig (ARSession, an XR Origin with an AR
    /// Camera carrying ARCameraManager) to already exist in the open scene -
    /// this script adds the Aayam-specific layer on top of that, it doesn't
    /// try to build the base rig itself. If that's missing, it aborts with a
    /// clear error rather than guessing at something this risky to get wrong
    /// blind.
    ///
    /// Run via menu: Aayam/Setup/Build Test Scene Wiring
    /// Idempotent-ish: finds existing GameObjects by name/type before
    /// creating new ones, so re-running after a partial failure is safe, but
    /// it is NOT a substitute for checking the result in the Editor - this
    /// was written without the ability to compile or run it.
    /// </summary>
    public static class AayamSceneBootstrapper
    {
        private const string RootName = "AayamSystems";
        private const string UIRootName = "AayamUI";

        [MenuItem("Aayam/Setup/Build Test Scene Wiring", false, 10)]
        public static void BuildTestSceneWiring()
        {
            var baseRig = FindBaseRig();
            if (baseRig == null) return; // error already logged

            var systems = BuildPlacementSystems(baseRig);
            var ui = BuildUI(systems, baseRig);
            WireQRScanner(baseRig, ui.productSelector);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[AayamSceneBootstrapper]</color> Done. " +
                      "Save the scene (Ctrl/Cmd+S), then build the real UI visual " +
                      "design on top of the placeholder screens this created.");
        }

        // ─── Base Rig Discovery ─────────────────────────────────────

        private class BaseRig
        {
            public ARSession Session;
            public GameObject CameraGO;
            public ARCameraManager CameraManager;
            public ARPlaneManager PlaneManager;
            public ARRaycastManager RaycastManager;
            public ARAnchorManager AnchorManager;
        }

        private static BaseRig FindBaseRig()
        {
            var missing = new List<string>();

            var session = Object.FindAnyObjectByType<ARSession>();
            if (session == null) missing.Add("ARSession");

            var cameraManager = Object.FindAnyObjectByType<ARCameraManager>();
            if (cameraManager == null) missing.Add("ARCameraManager (on the XR Origin's AR Camera)");

            if (missing.Count > 0)
            {
                Debug.LogError(
                    "[AayamSceneBootstrapper] Missing base AR Foundation rig: " +
                    string.Join(", ", missing) +
                    ". This script only adds the Aayam-specific layer on top of an " +
                    "existing AR session - set up an XR Origin (AR) with an AR Session " +
                    "first (e.g. via the AR Foundation samples / XR Origin (AR) prefab), " +
                    "then re-run this.");
                return null;
            }

            var rig = new BaseRig
            {
                Session = session,
                CameraGO = cameraManager.gameObject,
                CameraManager = cameraManager,
            };

            // ARPlaneManager/ARRaycastManager/ARAnchorManager: look for existing
            // instances anywhere; if missing, add to the XR Origin (or the camera's
            // parent chain) rather than guessing at a brand-new GameObject.
            rig.PlaneManager = Object.FindAnyObjectByType<ARPlaneManager>();
            rig.RaycastManager = Object.FindAnyObjectByType<ARRaycastManager>();
            rig.AnchorManager = Object.FindAnyObjectByType<ARAnchorManager>();

            GameObject managerHost = FindXROriginGameObject() ?? rig.Session.gameObject;

            if (rig.PlaneManager == null)
            {
                rig.PlaneManager = managerHost.AddComponent<ARPlaneManager>();
                Debug.Log($"[AayamSceneBootstrapper] Added missing ARPlaneManager to '{managerHost.name}'.");
            }
            if (rig.RaycastManager == null)
            {
                rig.RaycastManager = managerHost.AddComponent<ARRaycastManager>();
                Debug.Log($"[AayamSceneBootstrapper] Added missing ARRaycastManager to '{managerHost.name}'.");
            }
            if (rig.AnchorManager == null)
            {
                rig.AnchorManager = managerHost.AddComponent<ARAnchorManager>();
                Debug.Log($"[AayamSceneBootstrapper] Added missing ARAnchorManager to '{managerHost.name}'.");
            }

            // ARSessionController requires being on the same GameObject as ARSession.
            if (rig.Session.GetComponent<ARSessionController>() == null)
            {
                rig.Session.gameObject.AddComponent<ARSessionController>();
                Debug.Log("[AayamSceneBootstrapper] Added ARSessionController next to ARSession.");
            }

            return rig;
        }

        private static GameObject FindXROriginGameObject()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            return origin != null ? origin.gameObject : null;
        }

        // ─── Placement Systems (AR/Planes + AR/Placement + Services) ────

        private class Systems
        {
            public DoorCandidateDetector DoorDetector;
            public ObjectPlacementController PlacementController;
            public ModelLoader ModelLoaderInstance;
            public ModelInstanceManager ModelInstanceManagerInstance;
        }

        private static Systems BuildPlacementSystems(BaseRig rig)
        {
            var root = FindOrCreateRoot(RootName);

            var verticalFilter = FindOrAddComponent<VerticalPlaneFilter>(root, "VerticalPlaneFilter");
            var floorDetector = FindOrAddComponent<FloorPlaneDetector>(root, "FloorPlaneDetector");
            var boundaryAnalyzer = FindOrAddComponent<PlaneBoundaryAnalyzer>(root, "PlaneBoundaryAnalyzer");

            var doorDetector = FindOrAddComponent<DoorCandidateDetector>(root, "DoorCandidateDetector");
            SetPrivateField(doorDetector, "_verticalFilter", verticalFilter);
            SetPrivateField(doorDetector, "_boundaryAnalyzer", boundaryAnalyzer);

            var reticle = FindOrAddComponent<PlacementReticle>(root, "PlacementReticle");
            SetPrivateField(reticle, "_raycastManager", rig.RaycastManager);
            SetPrivateField(reticle, "_planeManager", rig.PlaneManager);
            // _reticlePrefab left unassigned on purpose - PlacementReticle falls
            // back to a default sphere when null (see its Start()).

            var anchorController = FindOrAddComponent<AnchorController>(root, "AnchorController");
            // AnchorController self-wires _anchorManager via FindFirstObjectByType
            // in its own Awake() - nothing to set here, just needs to exist.

            var doorPlacer = FindOrAddComponent<DoorSurfacePlacer>(root, "DoorSurfacePlacer");
            var history = FindOrAddComponent<PlacementHistory>(root, "PlacementHistory");

            var placementController = FindOrAddComponent<ObjectPlacementController>(root, "ObjectPlacementController");
            SetPrivateField(placementController, "_reticle", reticle);
            SetPrivateField(placementController, "_anchorController", anchorController);
            SetPrivateField(placementController, "_doorPlacer", doorPlacer);
            SetPrivateField(placementController, "_history", history);
            SetPrivateField(placementController, "_floorDetector", floorDetector);

            var modelCache = FindOrAddComponent<ModelCache>(root, "ModelCache");
            var modelLoader = FindOrAddComponent<ModelLoader>(root, "ModelLoader");
            SetPrivateField(modelLoader, "_modelCache", modelCache);
            var modelInstanceManager = FindOrAddComponent<ModelInstanceManager>(root, "ModelInstanceManager");

            Debug.Log("[AayamSceneBootstrapper] Placement systems wired under '" + RootName + "'.");

            return new Systems
            {
                DoorDetector = doorDetector,
                PlacementController = placementController,
                ModelLoaderInstance = modelLoader,
                ModelInstanceManagerInstance = modelInstanceManager,
            };
        }

        // ─── UI (Canvas, ScreenManager, HUD, ProductSelector) ────────────

        private class UIResult
        {
            public ProductSelector productSelector;
        }

        private static UIResult BuildUI(Systems systems, BaseRig rig)
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            GameObject canvasGO = GameObject.Find(UIRootName);
            Canvas canvas;
            if (canvasGO == null)
            {
                canvasGO = new GameObject(UIRootName);
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvas = canvasGO.GetComponent<Canvas>();
            }

            // One empty full-stretch RectTransform per AppScreen - placeholder
            // roots so ScreenManager/navigation is testable before real UI exists.
            var screenNames = new[]
            {
                AppScreen.Loading, AppScreen.ARScan, AppScreen.ProductBrowser,
                AppScreen.ProductDetail, AppScreen.QRScanner, AppScreen.Settings, AppScreen.Debug,
            };
            var screenRoots = new Dictionary<AppScreen, GameObject>();
            foreach (var screen in screenNames)
                screenRoots[screen] = FindOrCreateFullStretchChild(canvasGO.transform, "Screen_" + screen);

            var screenManager = FindOrAddComponent<ScreenManager>(canvasGO, "ScreenManagerHost");
            var screenEntries = new List<ScreenManager.ScreenEntry>();
            foreach (var screen in screenNames)
                screenEntries.Add(new ScreenManager.ScreenEntry { Screen = screen, Root = screenRoots[screen] });
            SetPrivateField(screenManager, "_screens", screenEntries);
            // Default to ProductBrowser, not Loading, so Play immediately shows the
            // catalogue instead of a screen with nothing on it - no loading logic
            // actually runs in this codebase yet. Change in the Inspector if you
            // build a real loading flow later.
            SetPrivateField(screenManager, "_initialScreen", AppScreen.ProductBrowser);

            // --- ARHUDController, under the ARScan screen ---
            var hudRoot = screenRoots[AppScreen.ARScan];
            var statusText = FindOrCreateUIText(hudRoot.transform, "StatusText", "Status");
            var placeButtonLabel = FindOrCreateUIText(hudRoot.transform, "PlaceButtonLabel", "Place");
            var backButton = FindOrCreateUIButton(hudRoot.transform, "BackButton", "Back");
            var menuButton = FindOrCreateUIButton(hudRoot.transform, "MenuButton", "Menu");
            var placeButton = FindOrCreateUIButton(hudRoot.transform, "PlaceButton", "");
            var resetButton = FindOrCreateUIButton(hudRoot.transform, "ResetButton", "Reset");
            var compareButton = FindOrCreateUIButton(hudRoot.transform, "CompareButton", "Compare");
            var screenshotButton = FindOrCreateUIButton(hudRoot.transform, "ScreenshotButton", "Screenshot");
            var scanButton = FindOrCreateUIButton(hudRoot.transform, "ScanButton", "Scan QR");
            // Other HUD buttons don't set an explicit position (they all stack at
            // the root's center by default) - nudge this one clear so it's
            // actually clickable on its own instead of fighting for the same rect.
            scanButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 220);

            var hud = FindOrAddComponent<ARHUDController>(hudRoot, "ARHUDController_Host");
            SetPrivateField(hud, "_statusText", statusText);
            SetPrivateField(hud, "_backButton", backButton);
            SetPrivateField(hud, "_menuButton", menuButton);
            SetPrivateField(hud, "_placeButton", placeButton);
            SetPrivateField(hud, "_resetButton", resetButton);
            SetPrivateField(hud, "_compareButton", compareButton);
            SetPrivateField(hud, "_screenshotButton", screenshotButton);
            SetPrivateField(hud, "_scanButton", scanButton);
            SetPrivateField(hud, "_placeButtonLabel", placeButtonLabel);
            SetPrivateField(hud, "_screenManager", screenManager);
            SetPrivateField(hud, "_sessionController", rig.Session.GetComponent<ARSessionController>());
            SetPrivateField(hud, "_doorDetector", systems.DoorDetector);
            SetPrivateField(hud, "_placementController", systems.PlacementController);
            // _coaching, _screenshot left unassigned - CoachingOverlay/ScreenshotCapture
            // are Phase 1 features out of scope here; ARHUDController null-checks both.

            // --- ProductSelector, under the ProductBrowser screen ---
            var browserRoot = screenRoots[AppScreen.ProductBrowser];
            var contentContainer = FindOrCreateFullStretchChild(browserRoot.transform, "CardContent");
            var layout = contentContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = contentContainer.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
            }

            GameObject cardPrefab = BuildOrLoadProductCardPrefab();

            var productSelector = FindOrAddComponent<ProductSelector>(browserRoot, "ProductSelector_Host");
            SetPrivateField(productSelector, "_contentContainer", contentContainer.transform);
            SetPrivateField(productSelector, "_productCardPrefab", cardPrefab);
            SetPrivateField(productSelector, "_hud", hud);
            SetPrivateField(productSelector, "_modelLoader", systems.ModelLoaderInstance);
            SetPrivateField(productSelector, "_modelInstanceManager", systems.ModelInstanceManagerInstance);

            Debug.Log("[AayamSceneBootstrapper] UI skeleton wired under '" + UIRootName + "'.");

            return new UIResult { productSelector = productSelector };
        }

        private static GameObject BuildOrLoadProductCardPrefab()
        {
            const string dir = "Assets/_Project/Prefabs/UI";
            const string path = dir + "/ProductCard.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            }

            var cardGO = new GameObject("ProductCard", typeof(RectTransform));
            var cardRect = cardGO.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(220, 280);

            var bg = cardGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var thumbnail = FindOrCreateFullStretchChild(cardGO.transform, "Thumbnail");
            var thumbRect = thumbnail.GetComponent<RectTransform>();
            thumbRect.anchorMin = new Vector2(0, 0.4f);
            thumbRect.anchorMax = new Vector2(1, 1);
            thumbRect.offsetMin = thumbRect.offsetMax = Vector2.zero;
            var thumbnailImage = thumbnail.AddComponent<Image>();

            var nameText = FindOrCreateUIText(cardGO.transform, "NameText", "Product Name");
            SetTextRect(nameText, new Vector2(0, 0.25f), new Vector2(1, 0.4f));
            var priceText = FindOrCreateUIText(cardGO.transform, "PriceText", "₹0");
            SetTextRect(priceText, new Vector2(0, 0.12f), new Vector2(1, 0.25f));
            var compatText = FindOrCreateUIText(cardGO.transform, "CompatText", "");
            SetTextRect(compatText, new Vector2(0, 0), new Vector2(1, 0.12f));

            var selectButton = cardGO.AddComponent<Button>();
            var selectedIndicator = FindOrCreateFullStretchChild(cardGO.transform, "SelectedIndicator");
            var indicatorImage = selectedIndicator.AddComponent<Image>();
            indicatorImage.color = new Color(1f, 1f, 1f, 0.15f);
            selectedIndicator.SetActive(false);

            var card = cardGO.AddComponent<ProductCard>();
            SetPrivateField(card, "_thumbnail", thumbnailImage);
            SetPrivateField(card, "_nameText", nameText);
            SetPrivateField(card, "_priceText", priceText);
            SetPrivateField(card, "_compatText", compatText);
            SetPrivateField(card, "_selectButton", selectButton);
            SetPrivateField(card, "_selectedIndicator", selectedIndicator);

            var prefab = PrefabUtility.SaveAsPrefabAsset(cardGO, path);
            Object.DestroyImmediate(cardGO);
            Debug.Log($"[AayamSceneBootstrapper] Created placeholder product card prefab at {path}.");
            return prefab;
        }

        private static void SetTextRect(Text text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // ─── QR Scanner ───────────────────────────────────────────────

        private static void WireQRScanner(BaseRig rig, ProductSelector productSelector)
        {
            var scanner = rig.CameraGO.GetComponent<QRScanner>();
            if (scanner == null)
                scanner = rig.CameraGO.AddComponent<QRScanner>();
            SetPrivateField(scanner, "_productSelector", productSelector);
            Debug.Log($"[AayamSceneBootstrapper] QRScanner attached to '{rig.CameraGO.name}' (the real AR camera).");
        }

        // ─── Generic Helpers ────────────────────────────────────────────

        private static GameObject FindOrCreateRoot(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go;
        }

        private static T FindOrAddComponent<T>(GameObject parent, string childName) where T : Component
        {
            Transform existing = parent.transform.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (go.transform.parent != parent.transform)
                go.transform.SetParent(parent.transform, worldPositionStays: false);

            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        private static GameObject FindOrCreateFullStretchChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static Text FindOrCreateUIText(Transform parent, string name, string defaultValue)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Text>();

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var text = go.AddComponent<Text>();
            text.text = defaultValue;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static Button FindOrCreateUIButton(Transform parent, string name, string label)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(140, 60);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            var button = go.AddComponent<Button>();

            if (!string.IsNullOrEmpty(label))
            {
                var text = FindOrCreateUIText(go.transform, "Label", label);
                SetTextRect(text, Vector2.zero, Vector2.one);
            }
            return button;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[AayamSceneBootstrapper] Field '{fieldName}' not found on " +
                                $"{target.GetType().Name} - script signature may have changed since this was written.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
