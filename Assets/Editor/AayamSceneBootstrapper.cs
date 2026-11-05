using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

using Unity.InferenceEngine;

using Aayam.AR.Core;
using Aayam.AR.Planes;
using Aayam.AR.Placement;
using Aayam.Services;
using Aayam.UI;
using Aayam.Utilities;

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
            WireVisualDoorDetection(baseRig, systems);
            WireVisualDoorSegmentation(baseRig, systems);
            WireDoorPnPWorldAnchor(baseRig, systems);
            WireDoorAnchorSystems(baseRig, systems);
            WireDoorEdgeCalibration(baseRig, systems);
            WireLightEstimation(baseRig);
            WireDoorShadowReceiver(baseRig);
            WireFinishPicker(baseRig, systems);
            WirePerformanceProfiler();
            WireFitoraRedesign(ui.productSelector);
            WireFitoraAuth();

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
            public Aayam.Services.ModelLoader ModelLoaderInstance;
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
            AssignMaterialIfPresent(reticle, "_reticleMaterialTemplate", "Assets/_Project/Materials/ReticleDefault.mat");

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
            SetPrivateField(placementController, "_arCamera", rig.CameraGO.GetComponent<Camera>());

            var modelCache = FindOrAddComponent<ModelCache>(root, "ModelCache");
            var modelLoader = FindOrAddComponent<Aayam.Services.ModelLoader>(root, "ModelLoader");
            SetPrivateField(modelLoader, "_modelCache", modelCache);
            AssignMaterialIfPresent(modelLoader, "_placeholderMaterialTemplate", "Assets/_Project/Materials/PlaceholderCube.mat");
            var modelInstanceManager = FindOrAddComponent<ModelInstanceManager>(root, "ModelInstanceManager");
            var capabilityChecker = FindOrAddComponent<DeviceCapabilityChecker>(root, "DeviceCapabilityChecker");
            SetPrivateField(modelInstanceManager, "_capabilityChecker", capabilityChecker);
            var lodSetup = FindOrAddComponent<LODSetup>(root, "LODSetup");
            SetPrivateField(modelInstanceManager, "_lodSetup", lodSetup);

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
                AppScreen.SignIn, AppScreen.SignUp, AppScreen.Profile,
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

            // 2026-10-29, Phase 6: every HUD button/text above defaulted to
            // (0,0) - all seven elements stacked exactly on top of each
            // other, with only the last-created (ScreenshotButton) sibling
            // actually visible/clickable. Confirmed live on-device (user
            // report: "no place button"). Laid out as a real top bar +
            // bottom action bar below - see the Y-budget this shares with
            // the debug-label stack and the Calibration/Hinge/FinishPicker
            // panels, coordinated across WireVisualDoorDetection (ML),
            // WireVisualDoorSegmentation (Geo), WireDoorPnPWorldAnchor
            // (PnP), WirePerformanceProfiler, and WireDoorEdgeCalibration/
            // WireFinishPicker (unchanged) so nothing overlaps top-to-bottom.
            statusText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 850);
            backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-420, 850);
            menuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(420, 850);

            placeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-315, -850);
            placeButtonLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-315, -850);
            resetButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-105, -850);
            compareButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(105, -850);
            screenshotButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(315, -850);
            scanButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20);

            // 2026-11-04: Fitora redesign pass - restyle every HUD button
            // (previously flat gray Image + default Text) to the shared
            // card/shadow look. Place/Screenshot are the primary actions on
            // this screen (terracotta fill); the rest are secondary (white
            // surface, dark text) - matches the reference's button hierarchy.
            foreach (var btn in new[] { backButton, menuButton, resetButton, compareButton, scanButton })
                FitoraTheme.ApplyButtonStyle(btn, FitoraTheme.Surface, FitoraTheme.TextPrimary);
            foreach (var btn in new[] { placeButton, screenshotButton })
                FitoraTheme.ApplyButtonStyle(btn, FitoraTheme.Primary, FitoraTheme.TextOnPrimary);
            statusText.color = FitoraTheme.TextPrimary;
            statusText.fontStyle = FontStyle.Bold;
            placeButtonLabel.color = FitoraTheme.TextOnPrimary;
            placeButtonLabel.fontStyle = FontStyle.Bold;

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

            // Phase 6 Step 7: comparison view, driven by the same compareButton.
            var comparisonController = rig.CameraGO.GetComponent<ProductComparisonController>();
            if (comparisonController == null) comparisonController = rig.CameraGO.AddComponent<ProductComparisonController>();
            SetPrivateField(comparisonController, "_placementController", systems.PlacementController);
            SetPrivateField(comparisonController, "_arCamera", rig.CameraGO.GetComponent<Camera>());
            SetPrivateField(hud, "_comparisonController", comparisonController);

            // Phase 6 Step 8: ScreenshotCapture was never wired anywhere in
            // this bootstrapper at all (explicitly marked "Phase 1 feature,
            // out of scope" in an earlier pass) - confirmed live on-device
            // that the Screenshot button was a silent no-op (ARHUDController
            // null-checks _screenshot, so nothing logs, nothing errors,
            // nothing happens). Flash panel is a simple full-stretch Image,
            // hidden by default; audio/watermark/hide-for-capture stay
            // unassigned (all null-checked in ScreenshotCapture itself) -
            // a real logo sprite for the watermark needs supplying by hand.
            var flashPanelGO = FindOrCreateFullStretchChild(canvasGO.transform, "ScreenshotFlashPanel");
            var flashImage = flashPanelGO.GetComponent<Image>();
            if (flashImage == null) flashImage = flashPanelGO.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0.9f);
            flashPanelGO.SetActive(false);

            var screenshotCapture = rig.CameraGO.GetComponent<ScreenshotCapture>();
            if (screenshotCapture == null) screenshotCapture = rig.CameraGO.AddComponent<ScreenshotCapture>();
            SetPrivateField(screenshotCapture, "_flashPanel", flashImage);
            SetPrivateField(hud, "_screenshot", screenshotCapture);
            // ShareLastScreenshot() has been fully built (NativeShare call,
            // gated for iOS/Android) since it was wired, but nothing in the
            // HUD ever called it - there's no dedicated Share button, only
            // this auto-share flag (defaulted false). Flipping it on is the
            // one-line fix that actually exercises the mechanism from the
            // existing Screenshot button rather than adding new UI.
            SetPrivateField(screenshotCapture, "_autoShareAfterCapture", true);

            // --- ProductSelector, under the ProductBrowser screen ---
            var browserRoot = screenRoots[AppScreen.ProductBrowser];

            // 2026-11-01, Phase 6: this tray had a HorizontalLayoutGroup but
            // no ScrollRect/Viewport/Mask at all - cards past whatever fit
            // in the tray's width simply rendered off-screen with no way to
            // reach them. Confirmed live on-device (20 real catalog
            // products, only ~4 fit in view, "can't scroll"). Standard
            // Unity ScrollRect + Viewport(RectMask2D) + Content
            // (HorizontalLayoutGroup + ContentSizeFitter) setup below.
            // Clean up the stale copy from before this ScrollRect existed,
            // which lived directly under browserRoot (no scrolling) and is
            // now orphaned dead weight.
            var staleCardContent = browserRoot.transform.Find("CardContent");
            if (staleCardContent != null) Object.DestroyImmediate(staleCardContent.gameObject);

            var scrollViewGO = FindOrCreateFullStretchChild(browserRoot.transform, "ProductScrollView");
            var scrollRect = scrollViewGO.GetComponent<ScrollRect>();
            if (scrollRect == null) scrollRect = scrollViewGO.AddComponent<ScrollRect>();

            var viewportTransform = scrollViewGO.transform.Find("Viewport");
            GameObject viewportGO;
            if (viewportTransform == null)
            {
                viewportGO = new GameObject("Viewport", typeof(RectTransform));
                viewportGO.transform.SetParent(scrollViewGO.transform, worldPositionStays: false);
                viewportGO.AddComponent<RectMask2D>();
                // RectMask2D needs a Graphic to mask against - fully
                // transparent, purely structural, not meant to be seen.
                viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            }
            else
            {
                viewportGO = viewportTransform.gameObject;
            }
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            // Same bottom-tray bounds the old (non-scrolling) contentRect used.
            viewportRect.anchorMin = new Vector2(0, 0);
            viewportRect.anchorMax = new Vector2(1, 0);
            viewportRect.pivot = new Vector2(0.5f, 0);
            viewportRect.sizeDelta = new Vector2(0, 300);
            viewportRect.anchoredPosition = new Vector2(0, 20);

            var contentContainer = FindOrCreateFullStretchChild(viewportGO.transform, "CardContent");
            var contentRect = contentContainer.GetComponent<RectTransform>();
            // Left-pivoted, NOT stretched - ScrollRect pans this via its own
            // anchoredPosition.x, and ContentSizeFitter below grows its
            // width to fit however many cards actually exist.
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(0, 1);
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;

            var layout = contentContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = contentContainer.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
            }
            var sizeFitter = contentContainer.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null) sizeFitter = contentContainer.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

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

        // ─── Visual Door Detection (Phase 3) ─────────────────────────────

        private const string DoorHandleModelPath = "Assets/_Project/MLModels/DoorHandleDetector.onnx";

        private static void WireVisualDoorDetection(BaseRig rig, Systems systems)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(DoorHandleModelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[AayamSceneBootstrapper] No ModelAsset at '{DoorHandleModelPath}' - " +
                                  "skipping VisualDoorDetector wiring. Run Phase 3 Step 1-4 first.");
                return;
            }

            var visualDetector = rig.CameraGO.GetComponent<VisualDoorDetector>();
            if (visualDetector == null)
                visualDetector = rig.CameraGO.AddComponent<VisualDoorDetector>();
            SetPrivateField(visualDetector, "_modelAsset", modelAsset);

            var root = FindOrCreateRoot(RootName);
            var coordinator = FindOrAddComponent<DoorConfidenceCoordinator>(root, "DoorConfidenceCoordinator");
            SetPrivateField(coordinator, "_geometricDetector", systems.DoorDetector);
            SetPrivateField(coordinator, "_visualDetector", visualDetector);

            // Step 7 accuracy check has no way to verify without this - latency
            // was logged, but nothing showed what the model actually detected.
            // Parented to the Canvas root, NOT Screen_ARScan: ScreenManager.Start()
            // deactivates every screen except _initialScreen (ProductBrowser here),
            // so anything living inside Screen_ARScan is hidden until the user
            // navigates there - confirmed live on-device 2026-10-11 (StatusText,
            // also a Screen_ARScan child, was equally invisible). Canvas-rooted
            // means it shows regardless of which screen is current, same as the
            // AR camera background and PlacementReticle already do.
            var canvasRoot = GameObject.Find(UIRootName);
            if (canvasRoot != null)
            {
                // Clean up the stale copy from the first (wrong) attempt, which
                // lived under Screen_ARScan and is now orphaned dead weight.
                var staleCopy = GameObject.Find("Screen_" + AppScreen.ARScan)?.transform.Find("MLDetectionDebugText");
                if (staleCopy != null) Object.DestroyImmediate(staleCopy.gameObject);

                var debugText = FindOrCreateUIText(canvasRoot.transform, "MLDetectionDebugText", "ML: no detection");
                var debugRect = debugText.GetComponent<RectTransform>();
                debugRect.sizeDelta = new Vector2(600, 80);
                debugRect.anchoredPosition = new Vector2(0, 760); // Phase 6 layout pass
                debugText.fontSize = 28;
                debugText.color = Color.cyan;

                var debugLabel = debugText.gameObject.GetComponent<VisualDetectionDebugLabel>();
                if (debugLabel == null) debugLabel = debugText.gameObject.AddComponent<VisualDetectionDebugLabel>();
                SetPrivateField(debugLabel, "_visualDetector", visualDetector);
            }

            Debug.Log($"[AayamSceneBootstrapper] VisualDoorDetector attached to '{rig.CameraGO.name}', " +
                      "DoorConfidenceCoordinator wired, on-screen ML detection label wired.");
        }

        // ─── Visual Door Segmentation (Phase 3 Step 8) ───────────────────

        private const string DoorHandleSegModelPath = "Assets/_Project/MLModels/DoorHandleSegmenter.onnx";

        private static void WireVisualDoorSegmentation(BaseRig rig, Systems systems)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(DoorHandleSegModelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[AayamSceneBootstrapper] No ModelAsset at '{DoorHandleSegModelPath}' - " +
                                  "skipping VisualDoorSegmenter wiring. Run Phase 3 Step 8's training/export first.");
                return;
            }

            var segmenter = rig.CameraGO.GetComponent<VisualDoorSegmenter>();
            if (segmenter == null)
                segmenter = rig.CameraGO.AddComponent<VisualDoorSegmenter>();
            SetPrivateField(segmenter, "_modelAsset", modelAsset);

            // Canvas-rooted, not Screen_ARScan - same reasoning as the Step 7
            // ML detection label above (ScreenManager hides non-current screens).
            var canvasRoot = GameObject.Find(UIRootName);
            if (canvasRoot != null)
            {
                var debugText = FindOrCreateUIText(canvasRoot.transform, "DoorGeometryDebugText", "Geo: no detection");
                var debugRect = debugText.GetComponent<RectTransform>();
                // Grown from 80 (fit for 1-2 lines) to fit the 4 raw-corner
                // lines added 2026-10-21 - same "box too small, text
                // silently clipped" mistake already made once on the PnP
                // label, not repeating it here. Overflow set explicitly too.
                debugRect.sizeDelta = new Vector2(600, 160);
                debugRect.anchoredPosition = new Vector2(0, 620); // below the Step 7 label at y=760 (Phase 6 layout pass)
                debugText.fontSize = 28;
                debugText.color = Color.yellow;
                debugText.verticalOverflow = VerticalWrapMode.Overflow;

                var debugLabel = debugText.gameObject.GetComponent<DoorGeometryDebugLabel>();
                if (debugLabel == null) debugLabel = debugText.gameObject.AddComponent<DoorGeometryDebugLabel>();
                SetPrivateField(debugLabel, "_segmenter", segmenter);
            }

            var refiner = rig.CameraGO.GetComponent<HandleWorldAnchorRefiner>();
            if (refiner == null) refiner = rig.CameraGO.AddComponent<HandleWorldAnchorRefiner>();
            SetPrivateField(refiner, "_geometricDetector", systems.DoorDetector);
            SetPrivateField(refiner, "_segmenter", segmenter);
            AssignMaterialIfPresent(refiner, "_markerMaterialTemplate", "Assets/_Project/Materials/DoorPnPMarker.mat");
            // Disabled, not removed - found 2026-10-21 actively confusing Phase 4
            // verification: this Phase 3 diagnostic subscribes to the SAME
            // OnDoorGeometry event DoorPnPWorldAnchor does and renders its own
            // marker with _markerColor defaulting to Color.magenta - every
            // "still magenta" sighting chased throughout that day's on-device
            // testing was very likely THIS marker, not a shader bug in
            // DoorPnPWorldAnchor at all. Its own class doc already called
            // itself "a visual stepping stone, not yet wired into actual
            // product placement" - it was never meant to keep running once
            // Phase 4 had its own verified marker. Kept in the hierarchy
            // (disabled) rather than deleted in case Option B's on-device
            // verification ever needs an A/B comparison against it again.
            refiner.enabled = false;

            Debug.Log($"[AayamSceneBootstrapper] VisualDoorSegmenter attached to '{rig.CameraGO.name}', " +
                      "on-screen geometry debug label wired, HandleWorldAnchorRefiner wired but DISABLED " +
                      "(see comment above - it was the real source of the recurring magenta marker).");
        }

        // ─── Door PnP World Anchor (Phase 4 Step 1, Option B) ────────────

        private static void WireDoorPnPWorldAnchor(BaseRig rig, Systems systems)
        {
            var segmenter = rig.CameraGO.GetComponent<VisualDoorSegmenter>();
            if (segmenter == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] No VisualDoorSegmenter on the camera rig - " +
                                  "skipping DoorPnPWorldAnchor wiring. Run WireVisualDoorSegmentation first.");
                return;
            }

            var coordinateSystem = rig.CameraGO.GetComponent<DoorCoordinateSystem>();
            if (coordinateSystem == null)
                coordinateSystem = rig.CameraGO.AddComponent<DoorCoordinateSystem>();

            var geometryState = rig.CameraGO.GetComponent<DoorGeometryState>();
            if (geometryState == null) geometryState = rig.CameraGO.AddComponent<DoorGeometryState>();

            var anchor = rig.CameraGO.GetComponent<DoorPnPWorldAnchor>();
            if (anchor == null) anchor = rig.CameraGO.AddComponent<DoorPnPWorldAnchor>();
            SetPrivateField(anchor, "_segmenter", segmenter);
            SetPrivateField(anchor, "_coordinateSystem", coordinateSystem);
            SetPrivateField(anchor, "_geometryState", geometryState);

            AssignMaterialIfPresent(anchor, "_markerMaterialTemplate", "Assets/_Project/Materials/DoorPnPMarker.mat");

            var canvasRoot = GameObject.Find(UIRootName);
            if (canvasRoot != null)
            {
                var debugText = FindOrCreateUIText(canvasRoot.transform, "DoorPnPDebugText", "PnP: no pose yet");
                var debugRect = debugText.GetComponent<RectTransform>();
                // Height was 80 (fit for the original 2-line label) - the
                // 2026-10-21 intrinsics-diagnostic line made it 3 lines, and
                // every on-device recording since then still showed only 2
                // lines despite the code clearly running (marker color/
                // behavior changes from other same-build fixes were visible).
                // Legacy Text defaults to clipping overflow rather than
                // erroring, so the 3rd line was silently invisible instead
                // of a visible bug - same class of "looks like nothing
                // happened" failure as the stale-build confusion earlier
                // this phase, but this time it really was running, just cut
                // off. 140 fits 3 lines at this font size with margin;
                // Overflow set explicitly so a future 4th line doesn't
                // repeat this silently either.
                // 4 lines as of 2026-10-21's viewport-projection diagnostic -
                // verticalOverflow=Overflow below means a short box wouldn't
                // actually clip text anymore, but sized properly anyway so
                // nothing visually overlaps.
                debugRect.sizeDelta = new Vector2(600, 185);
                // Moved down from 460 - the Geo label above grew to fit its
                // new 4-line corner dump (2026-10-21) and the two boxes'
                // vertical spans started overlapping. Stack, top to bottom:
                // ML y=700 h=80 [660,740], Geo y=560 h=160 [480,640],
                // PnP y=360 h=185 [267.5,452.5] - checked for overlap by
                // computing all three spans this time instead of eyeballing.
                debugRect.anchoredPosition = new Vector2(0, 420); // Phase 6 layout pass
                debugText.fontSize = 28;
                debugText.color = Color.cyan;
                debugText.verticalOverflow = VerticalWrapMode.Overflow;

                var debugLabel = debugText.gameObject.GetComponent<DoorPnPDebugLabel>();
                if (debugLabel == null) debugLabel = debugText.gameObject.AddComponent<DoorPnPDebugLabel>();
                SetPrivateField(debugLabel, "_anchor", anchor);
            }

            // 2026-10-25, Phase 4 Step 3 wiring: give the already-live
            // placement controller (built earlier in BuildPlacementSystems)
            // access to this door's coordinate system + geometry, so
            // ExecutePlacement can zone-clamp door-accessory products. Both
            // fields are optional/null-checked there - this is additive.
            if (systems.PlacementController != null)
            {
                SetPrivateField(systems.PlacementController, "_doorCoordinateSystem", coordinateSystem);
                SetPrivateField(systems.PlacementController, "_doorGeometry", geometryState);
            }

            Debug.Log($"[AayamSceneBootstrapper] DoorCoordinateSystem + DoorGeometryState + DoorPnPWorldAnchor " +
                      $"attached to '{rig.CameraGO.name}', on-screen PnP pose label wired, ObjectPlacementController " +
                      "zone-wiring attached. See docs/phases/phase-4-implementation-plan.md for verification history.");
        }

        // ─── Door Anchor Drift/Persistence/Registry (Phase 4 Step 4) ─────

        private static void WireDoorAnchorSystems(BaseRig rig, Systems systems)
        {
            var coordinateSystem = rig.CameraGO.GetComponent<DoorCoordinateSystem>();
            if (coordinateSystem == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] No DoorCoordinateSystem on the camera rig - " +
                                  "skipping Step 4 wiring. Run WireDoorPnPWorldAnchor first.");
                return;
            }

            var drift = rig.CameraGO.GetComponent<DoorAnchorDriftCompensator>();
            if (drift == null) drift = rig.CameraGO.AddComponent<DoorAnchorDriftCompensator>();
            SetPrivateField(drift, "_coordinateSystem", coordinateSystem);

            var persistence = rig.CameraGO.GetComponent<DoorAnchorPersistence>();
            if (persistence == null) persistence = rig.CameraGO.AddComponent<DoorAnchorPersistence>();
            SetPrivateField(persistence, "_coordinateSystem", coordinateSystem);
            SetPrivateField(persistence, "_anchorManager", rig.AnchorManager);

            // DoorRegistry registration happens at runtime now, in
            // DoorCoordinateSystem.Awake() - not here. An Editor-bootstrapper-
            // time Register() call would only affect this Editor process,
            // never the actual built app (caught and fixed 2026-10-21).

            // 2026-10-28: wire both back onto the already-existing
            // DoorPnPWorldAnchor (created earlier in WireDoorPnPWorldAnchor,
            // which runs before this method - these components didn't exist
            // yet at that point, so the back-reference has to happen here
            // instead). HandleGeometry now calls UpdateDoorAnchor every
            // solve and SaveCurrentOrigin once detection has been stable for
            // a while - see its own comment for the exact trigger.
            var anchor = rig.CameraGO.GetComponent<DoorPnPWorldAnchor>();
            if (anchor != null)
            {
                SetPrivateField(anchor, "_driftCompensator", drift);
                SetPrivateField(anchor, "_anchorPersistence", persistence);
            }

            Debug.Log("[AayamSceneBootstrapper] DoorAnchorDriftCompensator + DoorAnchorPersistence " +
                      "attached and wired into DoorPnPWorldAnchor's live confidence source " +
                      "(VisualDoorSegmenter.DoorResult.Confidence). See " +
                      "docs/phases/phase-4-implementation-plan.md for verification history.");
        }

        // ─── Door Edge Calibration + Hinge Side UI (Phase 4 Steps 5-6) ───

        private static void WireDoorEdgeCalibration(BaseRig rig, Systems systems)
        {
            var coordinateSystem = rig.CameraGO.GetComponent<DoorCoordinateSystem>();
            var geometryState = rig.CameraGO.GetComponent<DoorGeometryState>();
            if (coordinateSystem == null || geometryState == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] No DoorCoordinateSystem/DoorGeometryState on the " +
                                  "camera rig - skipping calibration/hinge UI wiring. Run WireDoorPnPWorldAnchor first.");
                return;
            }

            // Nested inside Screen_ARScan, not Canvas-rooted: this is an
            // opt-in interactive flow that only makes sense while actively
            // scanning, unlike the always-visible diagnostic debug labels
            // elsewhere in this file (same reasoning as ARHUDController_Host,
            // which lives here too).
            var arScanRoot = GameObject.Find("Screen_" + AppScreen.ARScan);
            if (arScanRoot == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] Screen_ARScan not found - skipping " +
                                  "calibration/hinge UI wiring. Run BuildUI first.");
                return;
            }

            // ── Edge calibration panel ── pixel anchoredPosition offsets
            // from canvas center, matching this file's established
            // convention (see the debug-text y=700/560/360 stack) - placed
            // low on screen, clear of that diagnostic stack (down to
            // y=267.5) and the product tray at the bottom.
            var calibHost = FindOrCreateFullStretchChild(arScanRoot.transform, "DoorEdgeCalibrationUI_Host");
            var calibStatus = FindOrCreateUIText(calibHost.transform, "CalibrationStatusText", "Calibration: not started");
            var calibStatusRect = calibStatus.GetComponent<RectTransform>();
            calibStatusRect.sizeDelta = new Vector2(600, 80);
            calibStatusRect.anchoredPosition = new Vector2(0, -160);
            var startButton = FindOrCreateUIButton(calibHost.transform, "StartCalibrationButton", "Calibrate Door");
            startButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -230);
            var measurementField = FindOrCreateUIInputField(calibHost.transform, "MeasurementInputField", "e.g. 0.75");
            measurementField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -300);
            var confirmButton = FindOrCreateUIButton(calibHost.transform, "ConfirmCalibrationButton", "Confirm");
            confirmButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -370);

            var calibrationUI = rig.CameraGO.GetComponent<DoorEdgeCalibrationUI>();
            if (calibrationUI == null) calibrationUI = rig.CameraGO.AddComponent<DoorEdgeCalibrationUI>();
            SetPrivateField(calibrationUI, "_doorCoordinateSystem", coordinateSystem);
            SetPrivateField(calibrationUI, "_doorGeometry", geometryState);
            SetPrivateField(calibrationUI, "_startButton", startButton);
            SetPrivateField(calibrationUI, "_statusText", calibStatus);
            SetPrivateField(calibrationUI, "_measurementInputField", measurementField);
            SetPrivateField(calibrationUI, "_confirmButton", confirmButton);
            AssignMaterialIfPresent(calibrationUI, "_markerMaterialTemplate", "Assets/_Project/Materials/DoorPnPMarker.mat");

            // ── Hinge side panel ──
            var hingeHost = FindOrCreateFullStretchChild(arScanRoot.transform, "DoorHingeSideUI_Host");
            var hingeStatus = FindOrCreateUIText(hingeHost.transform, "HingeStatusText", "Hinge: not confirmed");
            var hingeStatusRect = hingeStatus.GetComponent<RectTransform>();
            hingeStatusRect.sizeDelta = new Vector2(600, 60);
            hingeStatusRect.anchoredPosition = new Vector2(0, -440);
            var hingeLeftButton = FindOrCreateUIButton(hingeHost.transform, "HingeLeftButton", "Hinge Left");
            hingeLeftButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, -500);
            var hingeRightButton = FindOrCreateUIButton(hingeHost.transform, "HingeRightButton", "Hinge Right");
            hingeRightButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(90, -500);

            var hingeUI = rig.CameraGO.GetComponent<DoorHingeSideUI>();
            if (hingeUI == null) hingeUI = rig.CameraGO.AddComponent<DoorHingeSideUI>();
            SetPrivateField(hingeUI, "_doorGeometry", geometryState);
            SetPrivateField(hingeUI, "_hingeLeftButton", hingeLeftButton);
            SetPrivateField(hingeUI, "_hingeRightButton", hingeRightButton);
            SetPrivateField(hingeUI, "_statusText", hingeStatus);

            Debug.Log("[AayamSceneBootstrapper] DoorEdgeCalibrationUI + DoorHingeSideUI attached to " +
                      $"'{rig.CameraGO.name}', panels wired inside Screen_ARScan. See " +
                      "docs/phases/phase-4-implementation-plan.md for verification history.");

            // 2026-11-04: both panels above were always-visible, permanently
            // cluttering the main AR view with raw calibration/hinge controls
            // in every on-device screenshot this whole phase - confirmed via
            // every single test screenshot/recording taken this session.
            // They're real, occasionally-needed tools, not debug-only - so
            // collapse them behind a small toggle rather than hiding them
            // outright or deleting them.
            CollapseBehindToggle(arScanRoot.transform, new[] { calibHost, hingeHost }, "AdvancedTools");
        }

        /// <summary>Reparents the given panels under a shared container that starts
        /// hidden, with a small icon button (top-right, under the existing Menu
        /// button) toggling it open/closed.</summary>
        private static void CollapseBehindToggle(Transform screenRoot, GameObject[] panels, string name)
        {
            var panelHost = FindOrCreateFullStretchChild(screenRoot, name + "Panel");
            foreach (var panel in panels)
                panel.transform.SetParent(panelHost.transform, worldPositionStays: false);
            panelHost.SetActive(false);

            var toggleGO = FindOrCreateFullStretchChild(screenRoot, name + "ToggleButton");
            var toggleRect = toggleGO.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1, 1);
            toggleRect.pivot = new Vector2(1, 1);
            toggleRect.sizeDelta = new Vector2(80, 80);
            toggleRect.anchoredPosition = new Vector2(-30, -120);
            var toggleImage = toggleGO.GetComponent<Image>();
            if (toggleImage == null) toggleImage = toggleGO.AddComponent<Image>();
            FitoraTheme.ApplyCardStyle(toggleImage, FitoraTheme.Surface, 40f);
            FitoraTheme.AddShadow(toggleRect, 40f);

            var iconGO = FindOrCreateFullStretchChild(toggleGO.transform, "Icon");
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(36, 36);
            var iconImage = iconGO.GetComponent<Image>();
            if (iconImage == null) iconImage = iconGO.AddComponent<Image>();
            iconImage.sprite = FitoraIcons.Get(FitoraIcons.Icon.Tools, FitoraTheme.TextSecondary);
            iconImage.raycastTarget = false;

            var toggleButton = toggleGO.GetComponent<Button>();
            if (toggleButton == null) toggleButton = toggleGO.AddComponent<Button>();
            toggleButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(() => panelHost.SetActive(!panelHost.activeSelf));
        }

        // ─── Environment Lighting (Phase 6 Step 2) ───────────────────────

        /// <summary>
        /// LightEstimationController is a complete, already-written
        /// implementation (see its own class doc) - it just has never been
        /// attached to anything. It self-finds a Light via
        /// FindFirstObjectByType if left unassigned, but this wires it
        /// explicitly (matching every other component here) rather than
        /// leaning on that runtime fallback.
        /// </summary>
        private static void WireLightEstimation(BaseRig rig)
        {
            var light = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (light == null) light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] No Light found in the scene - " +
                                  "LightEstimationController attached but left unassigned " +
                                  "(it will self-find at runtime, but add a Directional Light for clarity).");
            }

            var lightEstimation = rig.CameraGO.GetComponent<LightEstimationController>();
            if (lightEstimation == null) lightEstimation = rig.CameraGO.AddComponent<LightEstimationController>();
            SetPrivateField(lightEstimation, "_directionalLight", light);

            Debug.Log($"[AayamSceneBootstrapper] LightEstimationController attached to '{rig.CameraGO.name}', " +
                      $"targeting '{(light != null ? light.name : "none")}'.");
        }

        // ─── Shadows (Phase 6 Step 4) ─────────────────────────────────────

        private static void WireDoorShadowReceiver(BaseRig rig)
        {
            var coordinateSystem = rig.CameraGO.GetComponent<DoorCoordinateSystem>();
            var geometryState = rig.CameraGO.GetComponent<DoorGeometryState>();
            if (coordinateSystem == null || geometryState == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] No DoorCoordinateSystem/DoorGeometryState on the " +
                                  "camera rig - skipping shadow receiver wiring. Run WireDoorPnPWorldAnchor first.");
                return;
            }

            var receiver = rig.CameraGO.GetComponent<DoorShadowReceiver>();
            if (receiver == null) receiver = rig.CameraGO.AddComponent<DoorShadowReceiver>();
            SetPrivateField(receiver, "_doorCoordinateSystem", coordinateSystem);
            SetPrivateField(receiver, "_doorGeometry", geometryState);
            AssignMaterialIfPresent(receiver, "_shadowReceiverMaterial", "Assets/_Project/Materials/ARShadowReceiver.mat");

            Debug.Log($"[AayamSceneBootstrapper] DoorShadowReceiver attached to '{rig.CameraGO.name}'.");
        }

        // ─── Fitora redesign (2026-11-04) ────────────────────────────────

        /// <summary>
        /// Applies the Fitora visual redesign (FitoraTheme/FitoraIcons) on top
        /// of the plain placeholder UI BuildUI created - app-wide cream
        /// background, a Home header (wordmark + search bar), a bottom tab
        /// bar, and restyled product cards. Re-finds its targets by name
        /// (GameObject.Find) rather than threading BuildUI's locals through,
        /// matching every other Wire* method's established convention.
        /// First pass of a larger reference-matched redesign - covers Home/
        /// ProductBrowser concretely; the remaining reference screens (Scan
        /// results, dedicated Product Detail, Quotes, Profile) follow in
        /// later passes.
        /// </summary>
        private static void WireFitoraRedesign(ProductSelector productSelector)
        {
            var canvasGO = GameObject.Find(UIRootName);
            if (canvasGO == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] Canvas not found - skipping Fitora redesign. Run BuildUI first.");
                return;
            }

            // App-wide cream background - first sibling so every screen draws on top of it.
            var bgGO = canvasGO.transform.Find("AppBackground")?.gameObject
                       ?? new GameObject("AppBackground", typeof(RectTransform));
            bgGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);
            bgGO.transform.SetAsFirstSibling();
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.GetComponent<Image>();
            if (bgImage == null) bgImage = bgGO.AddComponent<Image>();
            bgImage.color = FitoraTheme.Background;
            bgImage.raycastTarget = false;

            var browserRoot = GameObject.Find("Screen_" + AppScreen.ProductBrowser);
            if (browserRoot == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] Screen_ProductBrowser not found - skipping Home redesign.");
                return;
            }

            BuildHomeHeader(browserRoot.transform, productSelector);
            BuildBottomTabBar(browserRoot.transform, canvasGO.transform);
            RestyleProductScrollView(browserRoot.transform);
            RestyleProductCardPrefab();

            Debug.Log("[AayamSceneBootstrapper] Fitora redesign applied to Home/ProductBrowser.");
        }

        private static void BuildHomeHeader(Transform browserRoot, ProductSelector productSelector)
        {
            var headerGO = FindOrCreateFullStretchChild(browserRoot, "HomeHeader");
            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 280);
            headerRect.anchoredPosition = Vector2.zero;

            var wordmark = FindOrCreateUIText(headerGO.transform, "Wordmark", "Fitora");
            var wordmarkRect = wordmark.GetComponent<RectTransform>();
            wordmarkRect.anchorMin = new Vector2(0, 1);
            wordmarkRect.anchorMax = new Vector2(0, 1);
            wordmarkRect.pivot = new Vector2(0, 1);
            wordmarkRect.sizeDelta = new Vector2(400, 80);
            wordmarkRect.anchoredPosition = new Vector2(FitoraTheme.PageMargin, -24);
            wordmark.fontSize = 54;
            wordmark.fontStyle = FontStyle.Bold;
            wordmark.color = FitoraTheme.Primary;
            wordmark.alignment = TextAnchor.MiddleLeft;
            wordmark.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Search bar: rounded white pill, shadowed, with a search icon + placeholder text.
            var searchBarGO = FindOrCreateFullStretchChild(headerGO.transform, "SearchBar");
            var searchRect = searchBarGO.GetComponent<RectTransform>();
            searchRect.anchorMin = new Vector2(0, 1);
            searchRect.anchorMax = new Vector2(1, 1);
            searchRect.pivot = new Vector2(0.5f, 1);
            searchRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 100);
            searchRect.anchoredPosition = new Vector2(0, -130);
            var searchImage = searchBarGO.GetComponent<Image>();
            if (searchImage == null) searchImage = searchBarGO.AddComponent<Image>();
            FitoraTheme.ApplyCardStyle(searchImage, FitoraTheme.Surface, FitoraTheme.ButtonCorner);
            FitoraTheme.AddShadow(searchRect, FitoraTheme.ButtonCorner);

            var searchIconGO = FindOrCreateFullStretchChild(searchBarGO.transform, "SearchIcon");
            var searchIconRect = searchIconGO.GetComponent<RectTransform>();
            searchIconRect.anchorMin = new Vector2(0, 0.5f);
            searchIconRect.anchorMax = new Vector2(0, 0.5f);
            searchIconRect.pivot = new Vector2(0.5f, 0.5f);
            searchIconRect.sizeDelta = new Vector2(40, 40);
            searchIconRect.anchoredPosition = new Vector2(56, 0);
            var searchIconImage = searchIconGO.GetComponent<Image>();
            if (searchIconImage == null) searchIconImage = searchIconGO.AddComponent<Image>();
            searchIconImage.sprite = FitoraIcons.Get(FitoraIcons.Icon.Search, FitoraTheme.TextSecondary);
            searchIconImage.raycastTarget = false;

            // Real InputField, not placeholder Text - 2026-11-04, wired to
            // ProductSelector.SearchAsync so typing here actually filters
            // the catalog against the backend's own ?search= param
            // (confirmed live: already implemented server-side, just never
            // called from the client before).
            var searchField = FindOrCreateUIInputField(searchBarGO.transform, "SearchField", "Search products...");
            // FindOrCreateUIInputField defaults to ContentType.DecimalNumber -
            // correct for its original use (the calibration measurement
            // field), wrong here (would block typing any letters at all).
            searchField.contentType = InputField.ContentType.Standard;
            var searchFieldRect = searchField.GetComponent<RectTransform>();
            searchFieldRect.anchorMin = Vector2.zero;
            searchFieldRect.anchorMax = Vector2.one;
            searchFieldRect.offsetMin = new Vector2(96, 0);
            searchFieldRect.offsetMax = new Vector2(-24, 0);
            var searchFieldImage = searchField.GetComponent<Image>();
            if (searchFieldImage != null) searchFieldImage.color = new Color(0, 0, 0, 0); // bar behind it already styled
            var searchFieldText = searchField.textComponent;
            if (searchFieldText != null)
            {
                searchFieldText.alignment = TextAnchor.MiddleLeft;
                searchFieldText.color = FitoraTheme.TextPrimary;
                searchFieldText.fontSize = 32;
            }
            if (productSelector != null)
            {
                searchField.onEndEdit.RemoveAllListeners();
                searchField.onEndEdit.AddListener(productSelector.SearchAsync);
            }
        }

        private static readonly (FitoraIcons.Icon icon, string label, AppScreen? target)[] TabBarEntries =
        {
            (FitoraIcons.Icon.Home, "Home", AppScreen.ProductBrowser),
            (FitoraIcons.Icon.Explore, "Explore", AppScreen.ProductBrowser),
            (FitoraIcons.Icon.Scan, "Scan", AppScreen.ARScan),
            (FitoraIcons.Icon.Quotes, "Quotes", null),   // no Quotes screen yet - see class doc
            (FitoraIcons.Icon.Profile, "Profile", null), // no Profile screen yet - see class doc
        };

        private static void BuildBottomTabBar(Transform browserRoot, Transform canvasRoot)
        {
            var barGO = FindOrCreateFullStretchChild(browserRoot, "BottomTabBar");
            var barRect = barGO.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(1, 0);
            barRect.pivot = new Vector2(0.5f, 0);
            barRect.sizeDelta = new Vector2(0, 160);
            barRect.anchoredPosition = Vector2.zero;
            var barImage = barGO.GetComponent<Image>();
            if (barImage == null) barImage = barGO.AddComponent<Image>();
            barImage.color = FitoraTheme.Surface;
            FitoraTheme.AddShadow(barRect, cornerRadius: 0f, offsetY: 3f, extraPadding: 0f);

            var layout = barGO.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = barGO.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var screenManagerGO = canvasRoot.Find("ScreenManagerHost");
            var screenManager = screenManagerGO != null ? screenManagerGO.GetComponent<ScreenManager>() : null;

            foreach (var entry in TabBarEntries)
            {
                var tabGO = FindOrCreateFullStretchChild(barGO.transform, "Tab_" + entry.label);
                var tabRect = tabGO.GetComponent<RectTransform>();
                tabRect.sizeDelta = new Vector2(0, 140);

                var iconGO = FindOrCreateFullStretchChild(tabGO.transform, "Icon");
                var iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.sizeDelta = new Vector2(44, 44);
                iconRect.anchoredPosition = new Vector2(0, -24);
                var iconImage = iconGO.GetComponent<Image>();
                if (iconImage == null) iconImage = iconGO.AddComponent<Image>();
                iconImage.sprite = FitoraIcons.Get(entry.icon, entry.label == "Home" ? FitoraTheme.Primary : FitoraTheme.TextSecondary);
                iconImage.raycastTarget = false;

                var labelText = FindOrCreateUIText(tabGO.transform, "Label", entry.label);
                var labelRect = labelText.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0, 0);
                labelRect.anchorMax = new Vector2(1, 0);
                labelRect.pivot = new Vector2(0.5f, 0);
                labelRect.sizeDelta = new Vector2(0, 32);
                labelRect.anchoredPosition = new Vector2(0, 14);
                labelText.fontSize = 20;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = entry.label == "Home" ? FitoraTheme.Primary : FitoraTheme.TextSecondary;

                var button = tabGO.GetComponent<Button>();
                if (button == null) button = tabGO.AddComponent<Button>();
                var targetScreen = entry.target;
                var mgr = screenManager;
                string label = entry.label;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (targetScreen.HasValue) mgr?.ShowScreen(targetScreen.Value);
                    else Debug.Log($"[BottomTabBar] '{label}' has no screen yet - coming in a later redesign pass.");
                });
            }
        }

        private static void RestyleProductScrollView(Transform browserRoot)
        {
            var scrollView = browserRoot.Find("ProductScrollView");
            if (scrollView == null) return;
            var svRect = scrollView.GetComponent<RectTransform>();
            // Raise it off the very bottom edge to clear the new tab bar
            // (was anchoredPosition.y=20 when the screen had no header/tab bar).
            svRect.anchoredPosition = new Vector2(svRect.anchoredPosition.x, 180);

            var sectionLabel = FindOrCreateUIText(browserRoot, "TrendingLabel", "Trending Products");
            var sectionRect = sectionLabel.GetComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0, 0);
            sectionRect.anchorMax = new Vector2(1, 0);
            sectionRect.pivot = new Vector2(0.5f, 0);
            sectionRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 50);
            sectionRect.anchoredPosition = new Vector2(0, 500);
            sectionLabel.fontSize = 34;
            sectionLabel.fontStyle = FontStyle.Bold;
            sectionLabel.color = FitoraTheme.TextPrimary;
            sectionLabel.alignment = TextAnchor.MiddleLeft;
        }

        private static void RestyleProductCardPrefab()
        {
            const string path = "Assets/_Project/Prefabs/UI/ProductCard.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return; // BuildOrLoadProductCardPrefab creates it first, in BuildUI

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var bg = instance.GetComponent<Image>();
            FitoraTheme.ApplyCardStyle(bg, FitoraTheme.Surface, FitoraTheme.CardCorner);
            FitoraTheme.AddShadow(instance.GetComponent<RectTransform>(), FitoraTheme.CardCorner);

            var thumbRect = instance.transform.Find("Thumbnail")?.GetComponent<RectTransform>();
            if (thumbRect != null)
            {
                // Inset slightly so the thumbnail doesn't bleed past the card's rounded corners.
                thumbRect.anchorMin = new Vector2(0.06f, 0.42f);
                thumbRect.anchorMax = new Vector2(0.94f, 0.94f);
                thumbRect.offsetMin = thumbRect.offsetMax = Vector2.zero;
            }

            var nameText = instance.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.color = FitoraTheme.TextPrimary;
                nameText.fontStyle = FontStyle.Bold;
                nameText.alignment = TextAnchor.MiddleLeft;
            }
            var priceText = instance.transform.Find("PriceText")?.GetComponent<Text>();
            if (priceText != null)
            {
                priceText.color = FitoraTheme.Primary;
                priceText.fontStyle = FontStyle.Bold;
                priceText.alignment = TextAnchor.MiddleLeft;
            }
            var compatText = instance.transform.Find("CompatText")?.GetComponent<Text>();
            if (compatText != null) compatText.alignment = TextAnchor.MiddleLeft;

            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
        }

        // ─── Fitora auth (2026-11-04) ──────────────────────────────────

        /// <summary>Builds Screen_SignIn, Screen_SignUp, Screen_Profile and
        /// wires the bottom tab bar's Profile button to actually navigate
        /// (it was a "coming later" no-op until this ran).</summary>
        private static void WireFitoraAuth()
        {
            var canvasGO = GameObject.Find(UIRootName);
            var screenManagerGO = canvasGO != null ? canvasGO.transform.Find("ScreenManagerHost") : null;
            var screenManager = screenManagerGO != null ? screenManagerGO.GetComponent<ScreenManager>() : null;
            if (screenManager == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] ScreenManager not found - skipping auth screens.");
                return;
            }

            var signInRoot = GameObject.Find("Screen_" + AppScreen.SignIn);
            var signUpRoot = GameObject.Find("Screen_" + AppScreen.SignUp);
            var profileRoot = GameObject.Find("Screen_" + AppScreen.Profile);
            if (signInRoot == null || signUpRoot == null || profileRoot == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] Screen_SignIn/SignUp/Profile not found - " +
                                  "add them to BuildUI's screenNames first.");
                return;
            }

            BuildAuthForm(signInRoot.transform, isSignUpMode: false, screenManager);
            BuildAuthForm(signUpRoot.transform, isSignUpMode: true, screenManager);
            BuildProfileScreen(profileRoot.transform, screenManager);

            // Profile tab: go straight to the profile if already signed in,
            // otherwise to sign-in - TabBarEntries' static target can't
            // express that branch, so this button is rewired here instead.
            var tabBar = GameObject.Find("Screen_" + AppScreen.ProductBrowser)?.transform.Find("BottomTabBar");
            var profileTab = tabBar != null ? tabBar.Find("Tab_Profile") : null;
            var profileButton = profileTab != null ? profileTab.GetComponent<Button>() : null;
            if (profileButton != null)
            {
                profileButton.onClick.RemoveAllListeners();
                profileButton.onClick.AddListener(() =>
                    screenManager.ShowScreen(Aayam.Services.AuthSession.IsLoggedIn ? AppScreen.Profile : AppScreen.SignIn));
            }

            Debug.Log("[AayamSceneBootstrapper] Fitora auth screens (SignIn/SignUp/Profile) wired.");
        }

        private static void BuildAuthForm(Transform screenRoot, bool isSignUpMode, ScreenManager screenManager)
        {
            var host = FindOrCreateFullStretchChild(screenRoot, "AuthForm");

            var title = FindOrCreateUIText(host.transform, "Title", isSignUpMode ? "Create account" : "Welcome back");
            PositionAndStyleAuthLabel(title, yOffset: 420, fontSize: 52, bold: true, FitoraTheme.TextPrimary);

            var subtitle = FindOrCreateUIText(host.transform, "Subtitle",
                isSignUpMode ? "Sign up to save spaces and request quotes." : "Sign in to your Fitora account.");
            PositionAndStyleAuthLabel(subtitle, yOffset: 350, fontSize: 28, bold: false, FitoraTheme.TextSecondary);

            GameObject nameContainer = null;
            InputField nameField = null;
            float fieldY = 230;
            if (isSignUpMode)
            {
                nameContainer = BuildAuthField(host.transform, "NameField", "Full name", InputField.ContentType.Standard, fieldY, out nameField);
                fieldY -= 130;
            }
            var emailContainer = BuildAuthField(host.transform, "EmailField", "Email", InputField.ContentType.EmailAddress, fieldY, out var emailField);
            fieldY -= 130;
            var passwordContainer = BuildAuthField(host.transform, "PasswordField", "Password", InputField.ContentType.Password, fieldY, out var passwordField);
            fieldY -= 110;

            var errorText = FindOrCreateUIText(host.transform, "ErrorText", "");
            PositionAndStyleAuthLabel(errorText, yOffset: fieldY, fontSize: 24, bold: false, new Color(0.75f, 0.2f, 0.15f));
            errorText.gameObject.SetActive(false);
            fieldY -= 90;

            var submitButton = FindOrCreateUIButton(host.transform, "SubmitButton", isSignUpMode ? "Sign Up" : "Sign In");
            var submitRect = submitButton.GetComponent<RectTransform>();
            submitRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 110);
            submitRect.anchorMin = new Vector2(0, 1);
            submitRect.anchorMax = new Vector2(1, 1);
            submitRect.pivot = new Vector2(0.5f, 1);
            submitRect.anchoredPosition = new Vector2(0, fieldY);
            FitoraTheme.ApplyButtonStyle(submitButton, FitoraTheme.Primary, FitoraTheme.TextOnPrimary);
            var submitLabel = submitButton.GetComponentInChildren<Text>();
            fieldY -= 150;

            var switchButton = FindOrCreateUIButton(host.transform, "SwitchModeButton",
                isSignUpMode ? "Already have an account? Sign In" : "Don't have an account? Sign Up");
            var switchRect = switchButton.GetComponent<RectTransform>();
            switchRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 70);
            switchRect.anchorMin = new Vector2(0, 1);
            switchRect.anchorMax = new Vector2(1, 1);
            switchRect.pivot = new Vector2(0.5f, 1);
            switchRect.anchoredPosition = new Vector2(0, fieldY);
            var switchImage = switchButton.GetComponent<Image>();
            if (switchImage != null) switchImage.color = new Color(0, 0, 0, 0);
            var switchLabel = switchButton.GetComponentInChildren<Text>();
            if (switchLabel != null) { switchLabel.color = FitoraTheme.Primary; switchLabel.fontSize = 26; }

            var form = screenRoot.gameObject.GetComponent<AuthFormUI>();
            if (form == null) form = screenRoot.gameObject.AddComponent<AuthFormUI>();
            SetPrivateField(form, "_isSignUpMode", isSignUpMode);
            SetPrivateField(form, "_screenManager", screenManager);
            SetPrivateField(form, "_nameFieldContainer", nameContainer);
            SetPrivateField(form, "_nameField", nameField);
            SetPrivateField(form, "_emailField", emailField);
            SetPrivateField(form, "_passwordField", passwordField);
            SetPrivateField(form, "_submitButton", submitButton);
            SetPrivateField(form, "_submitButtonLabel", submitLabel);
            SetPrivateField(form, "_errorText", errorText);
            SetPrivateField(form, "_switchModeButton", switchButton);
        }

        private static GameObject BuildAuthField(Transform parent, string name, string placeholder,
            InputField.ContentType contentType, float yOffset, out InputField field)
        {
            var container = FindOrCreateFullStretchChild(parent, name);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(0.5f, 1);
            containerRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 100);
            containerRect.anchoredPosition = new Vector2(0, yOffset);

            var bgImage = container.GetComponent<Image>();
            if (bgImage == null) bgImage = container.AddComponent<Image>();
            FitoraTheme.ApplyCardStyle(bgImage, FitoraTheme.SurfaceAlt, FitoraTheme.ButtonCorner);

            field = FindOrCreateUIInputField(container.transform, "Field", placeholder);
            field.contentType = contentType;
            var fieldRect = field.GetComponent<RectTransform>();
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = new Vector2(32, 0);
            fieldRect.offsetMax = new Vector2(-32, 0);
            var fieldImage = field.GetComponent<Image>();
            if (fieldImage != null) fieldImage.color = new Color(0, 0, 0, 0);
            if (field.textComponent != null)
            {
                field.textComponent.alignment = TextAnchor.MiddleLeft;
                field.textComponent.color = FitoraTheme.TextPrimary;
                field.textComponent.fontSize = 30;
            }
            return container;
        }

        private static void PositionAndStyleAuthLabel(Text text, float yOffset, int fontSize, bool bold, Color color)
        {
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 60);
            rect.anchoredPosition = new Vector2(0, yOffset);
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
        }

        private static void BuildProfileScreen(Transform screenRoot, ScreenManager screenManager)
        {
            var host = FindOrCreateFullStretchChild(screenRoot, "ProfileContent");

            var avatarGO = FindOrCreateFullStretchChild(host.transform, "Avatar");
            var avatarRect = avatarGO.GetComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 1f);
            avatarRect.pivot = new Vector2(0.5f, 1f);
            avatarRect.sizeDelta = new Vector2(160, 160);
            avatarRect.anchoredPosition = new Vector2(0, -80);
            var avatarImage = avatarGO.GetComponent<Image>();
            if (avatarImage == null) avatarImage = avatarGO.AddComponent<Image>();
            FitoraTheme.ApplyCardStyle(avatarImage, FitoraTheme.PrimaryLight, 80f);
            var avatarIconGO = FindOrCreateFullStretchChild(avatarGO.transform, "Icon");
            var avatarIconRect = avatarIconGO.GetComponent<RectTransform>();
            avatarIconRect.anchorMin = avatarIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarIconRect.sizeDelta = new Vector2(80, 80);
            var avatarIconImage = avatarIconGO.GetComponent<Image>();
            if (avatarIconImage == null) avatarIconImage = avatarIconGO.AddComponent<Image>();
            avatarIconImage.sprite = FitoraIcons.Get(FitoraIcons.Icon.Profile, FitoraTheme.Primary);

            var nameText = FindOrCreateUIText(host.transform, "NameText", "");
            PositionAndStyleAuthLabel(nameText, yOffset: -280, fontSize: 40, bold: true, FitoraTheme.TextPrimary);
            nameText.alignment = TextAnchor.MiddleCenter;

            var emailText = FindOrCreateUIText(host.transform, "EmailText", "");
            PositionAndStyleAuthLabel(emailText, yOffset: -340, fontSize: 28, bold: false, FitoraTheme.TextSecondary);
            emailText.alignment = TextAnchor.MiddleCenter;

            var logoutButton = FindOrCreateUIButton(host.transform, "LogoutButton", "Log Out");
            var logoutRect = logoutButton.GetComponent<RectTransform>();
            logoutRect.anchorMin = new Vector2(0, 1);
            logoutRect.anchorMax = new Vector2(1, 1);
            logoutRect.pivot = new Vector2(0.5f, 1);
            logoutRect.sizeDelta = new Vector2(-FitoraTheme.PageMargin * 2, 100);
            logoutRect.anchoredPosition = new Vector2(0, -440);
            FitoraTheme.ApplyButtonStyle(logoutButton, FitoraTheme.Surface, FitoraTheme.Primary);

            var profileUI = screenRoot.gameObject.GetComponent<ProfileScreenUI>();
            if (profileUI == null) profileUI = screenRoot.gameObject.AddComponent<ProfileScreenUI>();
            SetPrivateField(profileUI, "_screenManager", screenManager);
            SetPrivateField(profileUI, "_nameText", nameText);
            SetPrivateField(profileUI, "_emailText", emailText);
            SetPrivateField(profileUI, "_logoutButton", logoutButton);
        }

        // ─── Material Variants (Phase 6 Step 6) ──────────────────────────

        private static void WireFinishPicker(BaseRig rig, Systems systems)
        {
            var arScanRoot = GameObject.Find("Screen_" + AppScreen.ARScan);
            if (arScanRoot == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] Screen_ARScan not found - skipping " +
                                  "finish picker wiring. Run BuildUI first.");
                return;
            }

            var host = FindOrCreateFullStretchChild(arScanRoot.transform, "FinishPickerUI_Host");

            // 2026-11-04: replaced the single "Next Finish" cycle button with
            // a real swatch row - one tappable, color-matched button per
            // finish, built at runtime by FinishPickerUI itself (the variant
            // list isn't known until a product is placed, same reasoning as
            // ProductSelector building ProductCard instances at runtime
            // rather than at edit time). Clean up the stale cycle button if
            // this scene was wired before this change.
            var staleCycleButton = host.transform.Find("CycleFinishButton");
            if (staleCycleButton != null) Object.DestroyImmediate(staleCycleButton.gameObject);

            var label = FindOrCreateUIText(host.transform, "FinishLabel", "Finish: -");
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -620);

            var swatchRowGO = FindOrCreateFullStretchChild(host.transform, "FinishSwatchRow");
            var swatchRowRect = swatchRowGO.GetComponent<RectTransform>();
            swatchRowRect.anchorMin = new Vector2(0.5f, 0f);
            swatchRowRect.anchorMax = new Vector2(0.5f, 0f);
            swatchRowRect.pivot = new Vector2(0.5f, 0.5f);
            swatchRowRect.sizeDelta = new Vector2(600, 80);
            swatchRowRect.anchoredPosition = new Vector2(0, -680);

            var swatchLayout = swatchRowGO.GetComponent<HorizontalLayoutGroup>();
            if (swatchLayout == null) swatchLayout = swatchRowGO.AddComponent<HorizontalLayoutGroup>();
            swatchLayout.spacing = 16;
            swatchLayout.childControlWidth = false;
            swatchLayout.childControlHeight = false;
            swatchLayout.childAlignment = TextAnchor.MiddleCenter;

            var picker = rig.CameraGO.GetComponent<FinishPickerUI>();
            if (picker == null) picker = rig.CameraGO.AddComponent<FinishPickerUI>();
            SetPrivateField(picker, "_placementController", systems.PlacementController);
            SetPrivateField(picker, "_swatchContainer", swatchRowRect);
            SetPrivateField(picker, "_finishLabel", label);

            Debug.Log($"[AayamSceneBootstrapper] FinishPickerUI (swatch row) attached to '{rig.CameraGO.name}', " +
                      "panel wired inside Screen_ARScan.");
        }

        // ─── Performance (Phase 6 Step 9) ────────────────────────────────

        /// <summary>
        /// Canvas-rooted, not Screen_ARScan-scoped - same "always visible
        /// diagnostic" reasoning as the ML/Geo/PnP debug-text stack (see
        /// their own y=700/560/360 stack below). Placed at y=150, its own
        /// new slot below that stack; PerformanceProfiler's own
        /// #if !UNITY_EDITOR &amp;&amp; !DEVELOPMENT_BUILD already disables the
        /// overlay in release builds, so no extra gating needed here.
        /// </summary>
        private static void WirePerformanceProfiler()
        {
            var canvasRoot = GameObject.Find(UIRootName);
            if (canvasRoot == null)
            {
                Debug.LogWarning("[AayamSceneBootstrapper] " + UIRootName + " not found - skipping " +
                                  "performance profiler wiring. Run BuildUI first.");
                return;
            }

            var perfText = FindOrCreateUIText(canvasRoot.transform, "PerformanceProfilerText", "PERFORMANCE: collecting...");
            var perfRect = perfText.GetComponent<RectTransform>();
            perfRect.sizeDelta = new Vector2(600, 260);
            perfRect.anchoredPosition = new Vector2(0, 180); // Phase 6 layout pass
            perfText.fontSize = 24;
            perfText.color = Color.white;
            perfText.verticalOverflow = VerticalWrapMode.Overflow;

            var profiler = perfText.gameObject.GetComponent<PerformanceProfiler>();
            if (profiler == null) profiler = perfText.gameObject.AddComponent<PerformanceProfiler>();
            SetPrivateField(profiler, "_displayText", perfText);

            Debug.Log("[AayamSceneBootstrapper] PerformanceProfiler attached under " + UIRootName + ".");
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

        private static InputField FindOrCreateUIInputField(Transform parent, string name, string placeholder)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<InputField>();

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 60);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            SetTextRect(text, Vector2.zero, Vector2.one);

            var field = go.AddComponent<InputField>();
            field.textComponent = text;
            field.contentType = InputField.ContentType.DecimalNumber;
            field.placeholder = FindOrCreateUIText(go.transform, "Placeholder", placeholder);
            SetTextRect((Text)field.placeholder, Vector2.zero, Vector2.one);
            ((Text)field.placeholder).color = new Color(0f, 0f, 0f, 0.4f);
            return field;
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

        /// <summary>
        /// Loads a Material asset and assigns it to a serialized field, or
        /// warns and leaves the field unset if the asset is missing -
        /// shared by every CreatePrimitive-based marker/reticle/placeholder
        /// in this project, all of which hit the same "no explicit material
        /// -> CreatePrimitive's default -> missing-shader fallback" issue
        /// independently (found and fixed project-wide 2026-10-21).
        /// </summary>
        private static void AssignMaterialIfPresent(object target, string fieldName, string assetPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                SetPrivateField(target, fieldName, material);
            }
            else
            {
                Debug.LogWarning($"[AayamSceneBootstrapper] No material at '{assetPath}' - " +
                                  $"{target.GetType().Name}.{fieldName} left unassigned, will fall back " +
                                  "to CreatePrimitive's default material (has shown as the missing-shader " +
                                  "fallback on this project before).");
            }
        }
    }
}
