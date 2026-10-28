using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 4: "Relocalization: Save spatial anchors relative to the
    /// door. If tracking is lost, restoring the anchor restores the
    /// coordinate system." Same ARAnchor pattern AR/Placement/AnchorController
    /// already uses for placed objects, applied to the door origin itself.
    ///
    /// Caller decides when the origin is trustworthy enough to save (e.g.
    /// after Step 1's pose has visually stabilized) - this class doesn't
    /// gate on anything itself, same "caller is responsible for stability
    /// validation" convention as DoorCoordinateSystem.SetOriginFromWorldPose.
    /// </summary>
    public class DoorAnchorPersistence : MonoBehaviour
    {
        [SerializeField] private ARAnchorManager _anchorManager;
        [SerializeField] private DoorCoordinateSystem _coordinateSystem;

        private ARAnchor _savedAnchor;

        public bool HasSavedAnchor => _savedAnchor != null;

        private void Awake()
        {
            if (_anchorManager == null)
                _anchorManager = FindFirstObjectByType<ARAnchorManager>();
            if (_coordinateSystem == null)
                _coordinateSystem = GetComponent<DoorCoordinateSystem>();
        }

        /// <summary>Persists the door origin's current pose as an ARAnchor.</summary>
        public bool SaveCurrentOrigin()
        {
            if (_coordinateSystem == null || !_coordinateSystem.HasOrigin)
            {
                Debug.LogWarning("[DoorAnchorPersistence] No door origin to save.");
                return false;
            }

            var originTransform = _coordinateSystem.DoorOrigin;
            var anchorGO = new GameObject("DoorOrigin_SavedAnchor");
            anchorGO.transform.SetPositionAndRotation(originTransform.position, originTransform.rotation);

            _savedAnchor = anchorGO.AddComponent<ARAnchor>();
            if (_savedAnchor == null)
            {
                Debug.LogError("[DoorAnchorPersistence] Failed to create ARAnchor.");
                Destroy(anchorGO);
                return false;
            }

            Debug.Log($"[DoorAnchorPersistence] Door origin saved at {originTransform.position:F3}");
            return true;
        }

        /// <summary>
        /// Restores the door coordinate system from the saved anchor instead
        /// of re-running PnP from scratch - for use after tracking loss/regain.
        /// </summary>
        public bool RestoreFromSavedAnchor()
        {
            if (_savedAnchor == null)
            {
                Debug.LogWarning("[DoorAnchorPersistence] No saved anchor to restore from.");
                return false;
            }
            if (_coordinateSystem == null) return false;

            _coordinateSystem.SetOriginFromWorldPose(_savedAnchor.transform.position, _savedAnchor.transform.rotation, null);
            Debug.Log("[DoorAnchorPersistence] Door origin restored from saved anchor.");
            return true;
        }

        private void OnDestroy()
        {
            if (_savedAnchor != null) Destroy(_savedAnchor.gameObject);
        }
    }
}
