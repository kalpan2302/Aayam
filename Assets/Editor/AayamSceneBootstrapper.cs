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

            var modelCache = FindOrAddComponent<ModelCache>(root, "ModelCache");
            var modelLoader = FindOrAddComponent<Aayam.Services.ModelLoader>(root, "ModelLoader");
            SetPrivateField(modelLoader, "_modelCache", modelCache);
            AssignMaterialIfPresent(modelLoader, "_placeholderMaterialTemplate", "Assets/_Project/Materials/PlaceholderCube.mat");
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
            // Confine to a bottom tray, not the full screen - ProductSelector's own
            // doc comment calls this "a tray at the bottom of the AR HUD". Left
            // full-stretch (the FindOrCreateFullStretchChild default), the
            // HorizontalLayoutGroup's childControlHeight below stretches every card
            // to full screen height instead of its intended 220x280, which renders
            // as thin vertical bars with text pushed down near the real screen edge.
            var contentRect = contentContainer.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0);
            contentRect.pivot = new Vector2(0.5f, 0);
            contentRect.sizeDelta = new Vector2(0, 300);
            contentRect.anchoredPosition = new Vector2(0, 20);

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
                debugRect.anchoredPosition = new Vector2(0, 700);
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
                debugRect.anchoredPosition = new Vector2(0, 560); // below the Step 7 label at y=700
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
                debugRect.anchoredPosition = new Vector2(0, 360);
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
