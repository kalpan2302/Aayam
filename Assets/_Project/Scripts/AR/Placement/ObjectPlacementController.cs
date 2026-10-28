using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Aayam.AR.Core;
using Aayam.AR.Planes;

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

        // 2026-10-25, Phase 4 Step 3 wiring: zone-aware placement for door
        // accessory products (handle/lock/peephole/hinge categories). Both
        // optional (null-checked below) - without them, placement is
        // completely unchanged from today for every product.
        [Header("Door Zone Wiring (optional)")]
        [SerializeField] private DoorCoordinateSystem _doorCoordinateSystem;
        [SerializeField] private DoorGeometryState _doorGeometry;

        // ─── Private State ────────────────────────────────────────
        private GameObject _pendingPrefab;
        private string     _pendingProductId;
        private float       _pendingAnchorHeightMm = -1f;
        private string      _pendingProductCategory = "";
        private readonly List<PlacedProduct> _placedObjects = new();

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
        public void StartPlacement(GameObject prefab, string productId = "", float anchorHeightMm = -1f, string productCategory = "")
        {
            if (_placedObjects.Count >= _maxObjects)
            {
                Debug.LogWarning($"[Placement] Max objects ({_maxObjects}) reached.");
                return;
            }

            _pendingPrefab          = prefab;
            _pendingProductId       = productId;
            _pendingAnchorHeightMm  = anchorHeightMm;
            _pendingProductCategory = productCategory;
            CurrentState            = PlacementState.Aiming;

            _reticle?.Show();
            Debug.Log($"[Placement] Aiming for product: {productId}");
        }

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

            // Detect tap (touch or mouse for editor)
            bool tapped = false;

#if UNITY_EDITOR
            tapped = Input.GetMouseButtonDown(0) &&
                     !EventSystem.current.IsPointerOverGameObject();
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                tapped = touch.phase == TouchPhase.Began &&
                         !EventSystem.current.IsPointerOverGameObject(touch.fingerId);
            }
#endif

            if (tapped && _reticle != null && _reticle.IsValidSurface)
                ExecutePlacement(_reticle.CurrentPose);
        }

        // ─── Execute Placement ────────────────────────────────────

        private void ExecutePlacement(Pose rawPose)
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

            // 2. Create anchor
            var anchor = _anchorController?.CreateAnchor(finalPose);
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

            // 4. Attach to anchor
            _anchorController.AttachObjectToAnchor(instance, anchor);

            // 5. Register in placed list
            var placed = new PlacedProduct
            {
                Instance    = instance,
                ProductId   = _pendingProductId,
                PlacedPose  = finalPose
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
    }
}
