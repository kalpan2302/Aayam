using UnityEngine;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Calculates the correct placement pose for objects on a vertical surface (door).
    ///
    /// Orientation math:
    ///   Z (Forward): Surface normal — the product faces OUT from the door.
    ///   Y (Up):      World up projected onto the door plane — keeps product upright.
    ///   X (Right):   Cross(Y, Z) — horizontal along door surface.
    ///
    /// This prevents the handle/lock from being tilted or rotated sideways regardless
    /// of the phone's angle when the user taps to place.
    /// </summary>
    public class DoorSurfacePlacer : MonoBehaviour
    {
        [Header("Offset")]
        [Tooltip("Push object slightly off the surface to prevent Z-fighting (meters)")]
        [SerializeField] private float _surfaceOffset = 0.005f;

        [Header("Anchor Heights (mm from floor — used for auto-snap)")]
        [SerializeField] private float _handleHeightMm   = 1000f;
        [SerializeField] private float _lockHeightMm     = 1000f;
        [SerializeField] private float _peepholeHeightMm = 1500f;
        [Tooltip("Now backed by real per-product anchor_y_mm from the backend " +
                 "(Phase 2 Step 7) - previously off because nothing fed it real data.")]
        [SerializeField] private bool  _useHeightSnap    = true;

        // ─── Core Pose Calculation ────────────────────────────────

        /// <summary>
        /// Given a raw AR raycast hit pose on a vertical plane,
        /// returns a corrected pose with proper surface orientation.
        /// </summary>
        public Pose CalculatePlacementPose(Pose hitPose)
        {
            // The hit pose's "up" direction is the plane normal for AR Foundation
            Vector3 surfaceNormal = hitPose.up;

            // Project world up onto the surface plane (removes component along normal)
            Vector3 projectedUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;

            // Guard: if surface is exactly horizontal (shouldn't happen for doors)
            if (projectedUp.sqrMagnitude < 0.001f)
                projectedUp = hitPose.forward;

            // Build rotation: forward = normal (out of surface), up = gravity-aligned
            Quaternion flushRotation = Quaternion.LookRotation(surfaceNormal, projectedUp);

            // Offset position slightly off the surface
            Vector3 finalPosition = hitPose.position + surfaceNormal * _surfaceOffset;

            return new Pose(finalPosition, flushRotation);
        }

        /// <summary>
        /// Snaps the placement height to a specific mm-from-floor value - the
        /// product's real anchor_y_mm from the backend (Phase 2 Step 7).
        /// Requires a known floor reference point (see FloorPlaneDetector).
        /// heightMm &lt; 0 means "no anchor data" - returns placementPose unchanged.
        /// </summary>
        public Pose SnapToStandardHeight(Pose placementPose, Vector3 floorWorldPos, float heightMm)
        {
            if (!_useHeightSnap || heightMm < 0f) return placementPose;

            float targetHeightM = heightMm * 0.001f;
            Vector3 snappedPos = new Vector3(
                placementPose.position.x,
                floorWorldPos.y + targetHeightM,
                placementPose.position.z
            );

            return new Pose(snappedPos, placementPose.rotation);
        }

        /// <summary>
        /// Back-compat overload using the three generic inspector-configured
        /// heights, for callers without a specific product's real anchor_y_mm.
        /// </summary>
        public Pose SnapToStandardHeight(Pose placementPose, Vector3 floorWorldPos, ProductHeightType heightType)
        {
            float heightMm = heightType switch
            {
                ProductHeightType.Handle   => _handleHeightMm,
                ProductHeightType.Lock     => _lockHeightMm,
                ProductHeightType.Peephole => _peepholeHeightMm,
                _                          => _handleHeightMm
            };
            return SnapToStandardHeight(placementPose, floorWorldPos, heightMm);
        }
    }

    public enum ProductHeightType { Handle, Lock, Peephole, Custom }
}
