using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

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
        /// Creates an ARAnchor at the specified pose.
        /// Returns null if creation fails.
        /// </summary>
        public ARAnchor CreateAnchor(Pose pose)
        {
            // Instantiate a new GameObject at the anchor position
            var anchorGO = new GameObject($"Anchor_{_objectAnchorMap.Count}");
            anchorGO.transform.SetPositionAndRotation(pose.position, pose.rotation);

            var anchor = anchorGO.AddComponent<ARAnchor>();

            if (anchor == null)
            {
                Debug.LogError("[AnchorController] Failed to add ARAnchor component.");
                Destroy(anchorGO);
                return null;
            }

            Debug.Log($"[AnchorController] Anchor created at {pose.position:F3}");
            return anchor;
        }

        // ─── Attach ───────────────────────────────────────────────

        /// <summary>
        /// Parents a placed object to an anchor so it moves with the tracked surface.
        /// </summary>
        public void AttachObjectToAnchor(GameObject obj, ARAnchor anchor)
        {
            if (obj == null || anchor == null) return;

            obj.transform.SetParent(anchor.transform, worldPositionStays: true);

            // Reset local transforms — position and rotation handled by anchor parent
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            _objectAnchorMap[obj] = anchor;
            Debug.Log($"[AnchorController] '{obj.name}' attached to anchor.");
        }

        // ─── Remove ───────────────────────────────────────────────

        /// <summary>
        /// Detaches an object from its anchor and destroys the anchor.
        /// </summary>
        public void RemoveAnchorForObject(GameObject obj)
        {
            if (!_objectAnchorMap.TryGetValue(obj, out ARAnchor anchor)) return;

            _objectAnchorMap.Remove(obj);

            // Unparent before destroying anchor
            if (obj != null)
                obj.transform.SetParent(null);

            if (anchor != null)
                Destroy(anchor.gameObject);

            Debug.Log($"[AnchorController] Anchor removed for '{obj?.name}'.");
        }

        /// <summary>Destroy all anchors and detach all tracked objects.</summary>
        public void RemoveAllAnchors()
        {
            foreach (var (obj, anchor) in _objectAnchorMap)
            {
                if (obj  != null) obj.transform.SetParent(null);
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
