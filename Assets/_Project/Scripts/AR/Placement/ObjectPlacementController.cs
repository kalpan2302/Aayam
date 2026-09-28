using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Aayam.AR.Core;

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

        [Header("Configuration")]
        [SerializeField] private int _maxObjects = 5;

        // ─── Private State ────────────────────────────────────────
        private GameObject _pendingPrefab;
        private string     _pendingProductId;
        private readonly List<PlacedProduct> _placedObjects = new();

        // ─── Start Placement ──────────────────────────────────────

        /// <summary>
        /// Begin the placement flow for a given product prefab.
        /// The user will aim the reticle and tap to place.
        /// </summary>
        public void StartPlacement(GameObject prefab, string productId = "")
        {
            if (_placedObjects.Count >= _maxObjects)
            {
                Debug.LogWarning($"[Placement] Max objects ({_maxObjects}) reached.");
                return;
            }

            _pendingPrefab     = prefab;
            _pendingProductId  = productId;
            CurrentState       = PlacementState.Aiming;

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
