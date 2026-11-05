using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Aayam.AR.Core;
using Aayam.AR.Interaction;
using Aayam.AR.Planes;
using Aayam.Models;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Top-level placement state machine.
    /// Handles the full lifecycle:
    ///   Idle → Aiming → Placing → Placed
    ///
    /// Supports multiple simultaneous placed objects up to MaxObjects.
    /// Integrates with PlacementReticle, DoorSurfacePlacer, AnchorController, PlacementHistory.
    /// </summary>
    public class ObjectPlacementController : MonoBehaviour
    {
        // ─── State ────────────────────────────────────────────────
        public enum PlacementState { Idle, Aiming, Placing, Placed }

        public PlacementState CurrentState { get; private set; } = PlacementState.Idle;
        public int PlacedObjectCount => _placedObjects.Count;

        // ─── Inspector ────────────────────────────────────────────
        [Header("Dependencies")]
        [SerializeField] private PlacementReticle   _reticle;
        [SerializeField] private AnchorController   _anchorController;
        [SerializeField] private DoorSurfacePlacer  _doorPlacer;
        [SerializeField] private PlacementHistory   _history;
        [SerializeField] private FloorPlaneDetector _floorDetector;

        [Header("Configuration")]
        [SerializeField] private int _maxObjects = 5;

        // 2026-11-03: a brief mandatory pan window before the first tap of
        // each placement is accepted. ARKit's visual-inertial SLAM (no
        // LiDAR on the test device) builds its map from whatever the
        // camera has already seen - giving it a few seconds of fresh
        // feature data on THIS wall before the anchor is created measurably
        // reduces how much that anchor's pose gets corrected afterward (the
        // "placed product drifts" report traced to a real ARKit map
        // correction, not a code bug - see AnchorController's doc comment,
        // which adds the complementary AnchoredPoseSmoother fix for
        // whatever correction still happens despite this).
        [SerializeField] private float _scanPromptSeconds = 3f;

        // 2026-10-25, Phase 4 Step 3 wiring: zone-aware placement for door
        // accessory products (handle/lock/peephole/hinge categories). Both
        // optional (null-checked below) - without them, placement is
        // completely unchanged from today for every product.
        [Header("Door Zone Wiring (optional)")]
        [SerializeField] private DoorCoordinateSystem _doorCoordinateSystem;
        [SerializeField] private DoorGeometryState _doorGeometry;
        [SerializeField] private Camera _arCamera;

        // ─── Private State ────────────────────────────────────────
        private GameObject _pendingPrefab;
        private string     _pendingProductId;
        private float       _pendingAnchorHeightMm = -1f;
        private string      _pendingProductCategory = "";
        private MaterialVariantData[] _pendingMaterialVariants;
        private string _pendingDisplayName = "";
        private string _pendingPriceFormatted = "";
        private readonly List<PlacedProduct> _placedObjects = new();
        private float _aimingStartTime;

        // ─── Start Placement ──────────────────────────────────────

        /// <summary>
        /// Begin the placement flow for a given product prefab (or an
        /// already-loaded runtime model instance - either works, since this
        /// just clones whatever it's given at placement time).
        /// anchorHeightMm is the product's real mounting height from the floor
        /// (mm, from the backend's product_3d_assets.anchor_y_mm), or -1 if
        /// unknown - see DoorSurfacePlacer.SnapToStandardHeight.
        /// The user will aim the reticle and tap to place.
        /// </summary>
        public void StartPlacement(GameObject prefab, string productId = "", float anchorHeightMm = -1f,
                                    string productCategory = "", MaterialVariantData[] materialVariants = null,
                                    string displayName = "", string priceFormatted = "")
        {
            if (_placedObjects.Count >= _maxObjects)
            {
                Debug.LogWarning($"[Placement] Max objects ({_maxObjects}) reached.");
                return;
            }

            _pendingPrefab           = prefab;
            _pendingProductId        = productId;
            _pendingAnchorHeightMm   = anchorHeightMm;
            _pendingProductCategory  = productCategory;
            _pendingMaterialVariants = materialVariants;
            _pendingDisplayName      = displayName;
            _pendingPriceFormatted   = priceFormatted;
            CurrentState             = PlacementState.Aiming;
            _aimingStartTime         = Time.time;

            _reticle?.Show();
            Debug.Log($"[Placement] Aiming for product: {productId}");
        }

        /// <summary>True for a brief window after StartPlacement - taps are
        /// ignored and the HUD should prompt the user to pan the phone (see
        /// _scanPromptSeconds above for why).</summary>
        public bool IsScanPromptActive => CurrentState == PlacementState.Aiming &&
            Time.time - _aimingStartTime < _scanPromptSeconds;

        public float ScanSecondsRemaining => Mathf.Max(0f, _scanPromptSeconds - (Time.time - _aimingStartTime));

        /// <summary>Cancel an in-progress placement.</summary>
        public void CancelPlacement()
        {
            if (CurrentState != PlacementState.Aiming) return;
            _pendingPrefab = null;
            CurrentState   = PlacementState.Idle;
            _reticle?.Hide();
            Debug.Log("[Placement] Placement cancelled.");
        }

        // ─── Update ───────────────────────────────────────────────

        private void Update()
        {
            if (CurrentState != PlacementState.Aiming) return;
            if (IsScanPromptActive) return;

            // Detect tap (touch or mouse for editor)
            bool tapped = false;
            Vector2 screenPos = default;

#if UNITY_EDITOR
            tapped = Input.GetMouseButtonDown(0) &&
                     !EventSystem.current.IsPointerOverGameObject();
            screenPos = Input.mousePosition;
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                tapped = touch.phase == TouchPhase.Began &&
                         !EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                screenPos = touch.position;
            }
#endif

            if (!tapped) return;

            if (TryGetPlacementPose(screenPos, out Pose pose))
                ExecutePlacement(pose);
        }

        /// <summary>
        /// Prefers PlacementReticle (ARKit plane tracking). Falls back to
        /// raycasting directly against the already-solved door plane
        /// (same TryIntersectDoorPlane method DoorEdgeCalibrationUI already
        /// uses successfully) when the reticle isn't valid - confirmed live
        /// on-device that ARKit's own plane tracking can fail to find any
        /// valid surface on this door even while our separate PnP door
        /// solve tracks it fine the whole time (188 consecutive tap
        /// attempts, reticleValid=false every time, Phase 6 on-device
        /// debugging). Door-plane orientation stands in for a surface
        /// normal since there's no ARKit plane to read one from.
        /// </summary>
        private static ProductHeightType ToHeightType(DoorComponentZones.ZoneType zoneType) => zoneType switch
        {
            DoorComponentZones.ZoneType.Handle   => ProductHeightType.Handle,
            DoorComponentZones.ZoneType.Lock     => ProductHeightType.Lock,
            DoorComponentZones.ZoneType.Peephole => ProductHeightType.Peephole,
            _                                     => ProductHeightType.Custom,
        };

        private bool TryGetPlacementPose(Vector2 screenPos, out Pose pose)
        {
            if (_reticle != null && _reticle.IsValidSurface)
            {
                pose = _reticle.CurrentPose;
                return true;
            }

            if (_doorCoordinateSystem != null && _doorCoordinateSystem.HasOrigin && _arCamera != null)
            {
                Ray ray = _arCamera.ScreenPointToRay(screenPos);
                Transform doorOrigin = _doorCoordinateSystem.DoorOrigin;
                if (DoorEdgeCalibrator.TryIntersectDoorPlane(ray, doorOrigin.position, doorOrigin.forward, out Vector3 worldPoint))
                {
                    // DoorSurfacePlacer.CalculatePlacementPose (run next, in
                    // ExecutePlacement) reads the surface normal off
                    // hitPose.up - matching AR Foundation's own raycast-hit
                    // convention, NOT DoorCoordinateSystem's "Z = normal"
                    // convention. Feeding doorOrigin.rotation directly (as
                    // a first attempt did) put the door's HEIGHT axis where
                    // CalculatePlacementPose expected the normal, producing
                    // a sideways placement - confirmed live on-device
                    // ("placed but in wrong direction", Phase 6 debugging).
                    // FromToRotation guarantees result.up == the true
                    // normal (doorOrigin.forward) exactly.
                    Quaternion hitRotation = Quaternion.FromToRotation(Vector3.up, doorOrigin.forward);
                    pose = new Pose(worldPoint, hitRotation);
                    return true;
                }
            }

            pose = default;
            return false;
        }

        // ─── Execute Placement ────────────────────────────────────

        // async void, not Awaitable - called from Update() (a sync call
        // site that can't await), matching this project's established
        // fire-and-forget convention (e.g. ProductSelector's handlers).
        private async void ExecutePlacement(Pose rawPose)
        {
            CurrentState = PlacementState.Placing;

            // 1. Correct pose for vertical surface
            Pose finalPose = _doorPlacer != null
                ? _doorPlacer.CalculatePlacementPose(rawPose)
                : rawPose;

            // 1b. Phase 4 Step 3: if this product is a door accessory
            // (handle/lock/peephole/hinge category) and the door's origin +
            // geometry are both known, clamp the tapped position into that
            // component's valid zone - the user still aims and taps exactly
            // as before, this just corrects the final position rather than
            // replacing it with ProductAutoPlacer's zone-center (preserves
            // user intent - e.g. picking an exact handle height within its
            // 100mm band - while still guaranteeing a physically valid
            // placement). Skips the generic floor-relative height snap below
            // for these products: the zone's Y-range is the correct, tighter
            // constraint and the two would otherwise fight.
            bool zoneClamped = false;
            if (_doorCoordinateSystem != null && _doorCoordinateSystem.HasOrigin &&
                _doorGeometry != null && _doorGeometry.HasGeometry &&
                DoorComponentZones.TryMapProductCategory(_pendingProductCategory, out var zoneType))
            {
                Vector3 doorSpace = _doorCoordinateSystem.WorldToDoorSpace(finalPose.position);
                Vector2 clamped = DoorComponentZones.ClampToZone(
                    new Vector2(doorSpace.x, doorSpace.y),
                    DoorComponentZones.GetZone(zoneType, _doorGeometry.DoorWidthMeters,
                        _doorGeometry.DoorHeightMeters, _doorGeometry.OpeningDirection.HingeOnLeft));
                Vector3 clampedDoorSpace = new Vector3(clamped.x, clamped.y, doorSpace.z);
                finalPose.position = _doorCoordinateSystem.DoorToWorldSpace(clampedDoorSpace);
                zoneClamped = true;

                var validation = ProductPlacementValidator.Validate(clampedDoorSpace, zoneType,
                    _doorGeometry.DoorWidthMeters, _doorGeometry.DoorHeightMeters, _doorGeometry.OpeningDirection.HingeOnLeft);
                Debug.Log($"[Placement] Zone-clamped '{_pendingProductCategory}' to {zoneType}: " +
                          $"tapped doorSpace={doorSpace:F3} -> clamped={clampedDoorSpace:F3} " +
                          $"(validate Ok={validation.Ok} {validation.Reason})");

                // 2026-11-03: the zone clamp's Y came straight from the
                // PnP-solved door origin's local axes, which DoorPnPWorldAnchor's
                // own doc comment already documents as having an accepted
                // positional precision ceiling (ML corner detection, not
                // fixable in software - see that file). Confirmed live
                // on-device: a tap aimed at the real handle solved to door-space
                // Y=1.273m, clamped into the zone's [0.95,1.05] band, and the
                // product still rendered up near the door's TOP edge - the
                // clamp was correct, but door-space Y itself didn't track
                // real-world height. FloorPlaneDetector's horizontal-up plane
                // is a completely independent AR Foundation measurement (not
                // derived from the same PnP solve), so prefer it for the
                // vertical component whenever it's available - X stays
                // zone-clamped (the smaller, already-accepted residual).
                // Hinge is skipped: its Y legitimately spans the whole door,
                // there's no single "real" height to snap to.
                bool hasFloor = _floorDetector != null && _floorDetector.HasFloor;
                if (zoneType != DoorComponentZones.ZoneType.Hinge && _doorPlacer != null && hasFloor)
                {
                    finalPose = _pendingAnchorHeightMm >= 0f
                        ? _doorPlacer.SnapToStandardHeight(finalPose, _floorDetector.FloorWorldPosition, _pendingAnchorHeightMm)
                        : _doorPlacer.SnapToStandardHeight(finalPose, _floorDetector.FloorWorldPosition, ToHeightType(zoneType));
                    Debug.Log($"[Placement] Floor-snapped height for {zoneType}: floorY={_floorDetector.FloorWorldPosition.y:F3} " +
                              $"anchorHeightMm={_pendingAnchorHeightMm} -> finalY={finalPose.position.y:F3}");
                }
                else
                {
                    Debug.Log($"[Placement] No floor-snap applied for {zoneType} (hasFloor={hasFloor}) - " +
                              "keeping zone-clamped door-space Y as-is.");
                }
            }

            // 1c. Snap vertical height to this product's real anchor_y_mm, if
            // known and a floor reference has been detected (Phase 2 Step 7).
            // Horizontal position stays wherever the user aimed. Skipped for
            // zone-clamped products (1b already picked the correct height).
            if (!zoneClamped && _doorPlacer != null && _floorDetector != null && _floorDetector.HasFloor)
            {
                finalPose = _doorPlacer.SnapToStandardHeight(
                    finalPose, _floorDetector.FloorWorldPosition, _pendingAnchorHeightMm);
            }

            // 2. Create anchor - a REAL ARKit-tracked one (see
            // AnchorController.CreateAnchorAsync's own doc comment for why
            // this must go through ARAnchorManager rather than a plain
            // AddComponent<ARAnchor>() call).
            var anchor = _anchorController != null
                ? await _anchorController.CreateAnchorAsync(finalPose)
                : null;
            if (anchor == null)
            {
                Debug.LogError("[Placement] Anchor creation failed — reverting to Aiming.");
                CurrentState = PlacementState.Aiming;
                return;
            }

            // 3. Instantiate product
            var instance = Instantiate(_pendingPrefab, finalPose.position, finalPose.rotation);
            instance.name = $"Product_{_pendingProductId}_{_placedObjects.Count}";
            // Instantiate() preserves the source's active state onto the
            // clone - callers routinely hand this a template they've
            // deliberately deactivated (ProductSelector.LoadAndPlaceAsync's
            // loaded model, ProductSelector.CreatePlaceholderModelPrefab's
            // stub), so without this the PLACED instance would silently
            // inherit that and never render. Found 2026-10-25 while
            // building an on-device test path for Phase 4 Step 3's zone
            // wiring - confirmed via grep that nothing anywhere in this
            // class or AnchorController ever reactivated a placed instance.
            instance.SetActive(true);

            // 3b. Phase 6 Step 6: wire up finish switching, if this product has any.
            if (_pendingMaterialVariants != null && _pendingMaterialVariants.Length > 0)
            {
                var switcher = instance.GetComponent<MaterialSwitcher>();
                if (switcher == null) switcher = instance.AddComponent<MaterialSwitcher>();
                switcher.Initialize(instance.GetComponentsInChildren<Renderer>(), _pendingMaterialVariants);
            }

            // 4. Attach to anchor
            _anchorController.AttachObjectToAnchor(instance, anchor);

            // 5. Register in placed list
            var placed = new PlacedProduct
            {
                Instance       = instance,
                ProductId      = _pendingProductId,
                PlacedPose     = finalPose,
                DisplayName    = _pendingDisplayName,
                PriceFormatted = _pendingPriceFormatted,
            };
            _placedObjects.Add(placed);

            // 6. Record in undo history
            _history?.PushPlaceAction(placed, _anchorController);

            // 7. Feedback
            PlacementFeedback.PlaySuccessFeedback();

            // 8. Transition
            CurrentState = PlacementState.Placed;
            _reticle?.Hide();

            AREventBus.RaiseObjectPlaced(finalPose.position, finalPose.rotation);
            Debug.Log($"[Placement] '{instance.name}' placed at {finalPose.position:F3}");
        }

        // ─── Remove ───────────────────────────────────────────────

        /// <summary>Remove the most recently placed object.</summary>
        public void RemoveLastPlaced()
        {
            if (_placedObjects.Count == 0) return;
            var last = _placedObjects[^1];
            RemovePlaced(last);
        }

        /// <summary>Remove a specific placed product.</summary>
        public void RemovePlaced(PlacedProduct product)
        {
            _anchorController?.RemoveAnchorForObject(product.Instance);
            Destroy(product.Instance);
            _placedObjects.Remove(product);

            CurrentState = _placedObjects.Count > 0 ? PlacementState.Placed : PlacementState.Idle;
            AREventBus.RaiseObjectRemoved();
            Debug.Log($"[Placement] Removed '{product.ProductId}'.");
        }

        /// <summary>Remove all placed objects and reset state.</summary>
        public void RemoveAll()
        {
            foreach (var p in new List<PlacedProduct>(_placedObjects))
                RemovePlaced(p);
        }

        // ─── Query ────────────────────────────────────────────────

        public IReadOnlyList<PlacedProduct> GetPlacedObjects() => _placedObjects;
    }

    // ─── Data Types ───────────────────────────────────────────

    [System.Serializable]
    public class PlacedProduct
    {
        public GameObject Instance;
        public string     ProductId;
        public Pose       PlacedPose;

        // Phase 6 Step 7: for comparison-view labels.
        public string     DisplayName;
        public string     PriceFormatted;
    }
}
