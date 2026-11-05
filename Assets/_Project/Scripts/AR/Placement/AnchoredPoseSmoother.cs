using UnityEngine;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Smooths a placed product's rendered pose toward its ARAnchor's live
    /// tracked pose, rather than making the product a direct Transform child
    /// of the anchor. ARKit/ARCore anchors get corrected - sometimes
    /// visibly - as the platform's own SLAM map refines (new visual data
    /// entering frame, loop closure, etc.); that's real platform behavior,
    /// not something this app can prevent. Confirmed live on-device
    /// (2026-11-03): a single, correctly-created anchor (AnchorController's
    /// TryAddAnchorAsync path, exactly one anchor in the session log) still
    /// visibly jumped when panning across a textureless door revealed a lot
    /// of new scene content at once. Parenting directly under the anchor
    /// means the rendered object snaps 1:1 to every such correction; this
    /// component instead Lerp/Slerps toward the anchor's pose every frame,
    /// turning a correction into a brief glide. It doesn't change where the
    /// object ultimately ends up - only how getting there looks.
    /// </summary>
    public class AnchoredPoseSmoother : MonoBehaviour
    {
        [Tooltip("Per-frame interpolation factor toward the anchor's live pose. " +
                 "Matches LightEstimationController's own Lerp-based smoothing convention.")]
        [SerializeField, Range(0f, 1f)] private float _smoothFactor = 0.15f;

        private Transform _anchorTransform;

        /// <summary>Snaps immediately to the anchor's current pose (no pop-in), then smooths from there on.</summary>
        public void Initialize(Transform anchorTransform)
        {
            _anchorTransform = anchorTransform;
            if (_anchorTransform != null)
                transform.SetPositionAndRotation(_anchorTransform.position, _anchorTransform.rotation);
        }

        // LateUpdate: ARFoundation's anchor subsystem has already applied
        // this frame's tracked-pose update to _anchorTransform by the time
        // this runs, so we're always smoothing toward the current value.
        private void LateUpdate()
        {
            if (_anchorTransform == null) return;

            transform.position = Vector3.Lerp(transform.position, _anchorTransform.position, _smoothFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, _anchorTransform.rotation, _smoothFactor);
        }
    }
}
