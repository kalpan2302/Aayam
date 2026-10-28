using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 3 wiring (2026-10-25): the live, queryable home for a
    /// door's width/height/opening-direction - nothing exposed this outside
    /// DoorPnPWorldAnchor's private [SerializeField] assumed-size fields
    /// before this, and hinge side wasn't tracked live anywhere at all.
    /// Deliberately a separate component from DoorCoordinateSystem (whose
    /// job is purely origin-space transform math, with zero existing notion
    /// of size) rather than growing that class's responsibility.
    ///
    /// Lives on the same GameObject as DoorCoordinateSystem/DoorPnPWorldAnchor
    /// (the camera rig) - see AayamSceneBootstrapper.WireDoorPnPWorldAnchor.
    /// </summary>
    public class DoorGeometryState : MonoBehaviour
    {
        private float _widthMeters;
        private float _heightMeters;
        private DoorOpeningDirection _openingDirection;

        public bool HasGeometry { get; private set; }
        public float DoorWidthMeters => _widthMeters;
        public float DoorHeightMeters => _heightMeters;
        public DoorOpeningDirection OpeningDirection => _openingDirection;

        /// <summary>
        /// Called by DoorPnPWorldAnchor once per solve with whatever it
        /// assumed for this frame's pose recovery - idempotent, cheap to
        /// call every frame.
        /// </summary>
        public void SetGeometry(float widthMeters, float heightMeters)
        {
            _widthMeters = widthMeters;
            _heightMeters = heightMeters;
            HasGeometry = true;
        }

        /// <summary>
        /// Stage C's hook (DoorCalibration.ComputeScaleCorrection's result) -
        /// corrects the stored size in place once the user has measured the
        /// real door and confirmed a scale factor.
        /// </summary>
        public void ApplyScaleCorrection(float factor)
        {
            _widthMeters *= factor;
            _heightMeters *= factor;
        }

        /// <summary>
        /// Stage D's hook (DoorOpeningDirection.WithUserOverride) - the tap-
        /// to-confirm-hinge-side UI calls this once the user has tapped.
        /// </summary>
        public void SetOpeningDirectionOverride(bool hingeOnLeft)
        {
            _openingDirection = _openingDirection.WithUserOverride(hingeOnLeft);
        }
    }
}
