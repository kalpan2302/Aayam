using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Manages the lifecycle of AR anchors. Anchors keep placed objects
    /// stable as the AR session refines its understanding of the environment.
    /// Without anchors, objects drift away from their physical position.
    /// </summary>
    public class AnchorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARAnchorManager _anchorManager;

        // Track which anchor belongs to which placed object
        private readonly Dictionary<GameObject, ARAnchor> _objectAnchorMap = new();

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_anchorManager == null)
                _anchorManager = FindFirstObjectByType<ARAnchorManager>();

            if (_anchorManager == null)
                Debug.LogError("[AnchorController] ARAnchorManager not found in scene.");
        }

        // ─── Create ───────────────────────────────────────────────

        /// <summary>
        /// Creates a REAL ARKit-tracked anchor at the specified pose via
        /// ARAnchorManager.TryAddAnchorAsync - ARKit continuously refines
        /// this anchor's pose as its own world-tracking SLAM map updates,
        /// which is what keeps a placed object visually fixed in the real
        /// world as the camera moves.
        ///
        /// A previous version of this method called
        /// `anchorGO.AddComponent&lt;ARAnchor&gt;()` directly on a plain
        /// GameObject - that creates an inert component with NO connection
        /// to the underlying AR platform's anchor tracking at all, so its
        /// transform was set once at placement time and never corrected
        /// again. Confirmed live on-device (Phase 6 testing, screen
        /// recording): placed objects visibly drifted/jumped away from
        /// their real-world position while panning the camera - exactly
        /// the symptom this class's own doc comment warns a MISSING anchor
        /// would cause ("Without anchors, objects drift away from their
        /// physical position"). This was a pre-existing bug, not something
        /// introduced this phase - it was never caught before because this
        /// was the first time in the project's history an object was
        /// successfully placed at all (PlacementReticle's ARKit
        /// plane-tracking dependency blocked every attempt before Phase 6's
        /// door-plane placement fallback).
        ///
        /// Returns null if creation fails.
        /// </summary>
        public async Awaitable<ARAnchor> CreateAnchorAsync(Pose pose)
        {
            if (_anchorManager == null)
            {
                Debug.LogError("[AnchorController] No ARAnchorManager - cannot create a real anchor.");
                return null;
            }

            Result<ARAnchor> result = await _anchorManager.TryAddAnchorAsync(pose);
            if (!result.status.IsSuccess())
            {
                Debug.LogError($"[AnchorController] TryAddAnchorAsync failed: {result.status}");
                return null;
            }

            Debug.Log($"[AnchorController] Anchor created at {pose.position:F3}");
            return result.value;
        }

        // ─── Attach ───────────────────────────────────────────────

        /// <summary>
        /// Attaches a placed object to an anchor so it tracks the anchor's
        /// pose. Previously did this via direct Transform parenting
        /// (SetParent + reset local pose) - that makes the rendered object
        /// snap 1:1 to every pose correction ARKit's SLAM map makes as it
        /// refines, which showed up live on-device as a visible jump when
        /// panning revealed a lot of new scene content on a textureless
        /// door (2026-11-03; a single correctly-created anchor was
        /// confirmed, so this is real platform tracking behavior, not a
        /// bug in anchor creation). AnchoredPoseSmoother Lerp/Slerps toward
        /// the anchor's live pose instead, turning that correction into a
        /// glide - see its own doc comment.
        /// </summary>
        public void AttachObjectToAnchor(GameObject obj, ARAnchor anchor)
        {
            if (obj == null || anchor == null) return;

            var smoother = obj.GetComponent<AnchoredPoseSmoother>();
            if (smoother == null) smoother = obj.AddComponent<AnchoredPoseSmoother>();
            smoother.Initialize(anchor.transform);

            _objectAnchorMap[obj] = anchor;
            Debug.Log($"[AnchorController] '{obj.name}' attached to anchor (pose-smoothed).");
        }

        // ─── Remove ───────────────────────────────────────────────

        /// <summary>
        /// Detaches an object from its anchor and destroys the anchor.
        /// </summary>
        public void RemoveAnchorForObject(GameObject obj)
        {
            if (!_objectAnchorMap.TryGetValue(obj, out ARAnchor anchor)) return;

            _objectAnchorMap.Remove(obj);

            // obj is no longer a Transform child of anchor (AttachObjectToAnchor
            // now smooths toward it instead of parenting - see that method's
            // doc comment), so there's nothing to unparent. Just stop the
            // smoother from chasing a transform about to be destroyed.
            if (obj != null)
            {
                var smoother = obj.GetComponent<AnchoredPoseSmoother>();
                if (smoother != null) Destroy(smoother);
            }

            if (anchor != null)
                Destroy(anchor.gameObject);

            Debug.Log($"[AnchorController] Anchor removed for '{obj?.name}'.");
        }

        /// <summary>Destroy all anchors and detach all tracked objects.</summary>
        public void RemoveAllAnchors()
        {
            foreach (var (obj, anchor) in _objectAnchorMap)
            {
                if (obj != null)
                {
                    var smoother = obj.GetComponent<AnchoredPoseSmoother>();
                    if (smoother != null) Destroy(smoother);
                }
                if (anchor != null) Destroy(anchor.gameObject);
            }
            _objectAnchorMap.Clear();
            Debug.Log("[AnchorController] All anchors removed.");
        }

        // ─── Query ────────────────────────────────────────────────

        /// <summary>Returns the anchor associated with a placed object, or null.</summary>
        public ARAnchor GetAnchorFor(GameObject obj)
        {
            _objectAnchorMap.TryGetValue(obj, out var anchor);
            return anchor;
        }

        /// <summary>Number of currently active anchors.</summary>
        public int AnchorCount => _objectAnchorMap.Count;
    }
}
