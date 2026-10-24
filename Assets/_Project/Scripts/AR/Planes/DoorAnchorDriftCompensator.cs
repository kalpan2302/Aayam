using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 4: "Tracking Drift Compensation: Continuously refine the
    /// transformation matrix as the AR environment understanding improves."
    /// Direct port of the roadmap doc's own UpdateDoorAnchor pseudocode
    /// (docs/phases/phase-4-door-coordinate-system.md Step 4) - nothing
    /// novel here, the roadmap already specified the blend.
    ///
    /// NOT wired to a live confidence source yet - nothing in this codebase
    /// currently produces a per-frame confidence score for the door pose
    /// (DoorPnPWorldAnchor logs a position/rotation per successful solve,
    /// not a confidence). Caller must supply one; see class doc on
    /// DoorPnPWorldAnchor for where that would need to be added once Step 1
    /// is confirmed and this gets wired in for real.
    /// </summary>
    public class DoorAnchorDriftCompensator : MonoBehaviour
    {
        [SerializeField] private DoorCoordinateSystem _coordinateSystem;
        [SerializeField] private float _confidenceThreshold = 0.5f;
        [SerializeField] private float _lerpSpeed = 2f;

        private void Awake()
        {
            if (_coordinateSystem == null)
                _coordinateSystem = GetComponent<DoorCoordinateSystem>();
            if (_coordinateSystem == null)
                _coordinateSystem = FindFirstObjectByType<DoorCoordinateSystem>();
        }

        /// <summary>
        /// Blends the door origin toward newDetectedPose when confidence
        /// clears the threshold - roadmap's own guard against jitter from
        /// low-confidence detections overwriting a good pose outright.
        /// </summary>
        public void UpdateDoorAnchor(Pose newDetectedPose, float confidence)
        {
            if (_coordinateSystem == null || !_coordinateSystem.HasOrigin) return;
            if (confidence <= _confidenceThreshold) return;

            var origin = _coordinateSystem.DoorOrigin;
            origin.position = Vector3.Lerp(origin.position, newDetectedPose.position, Time.deltaTime * _lerpSpeed);
            origin.rotation = Quaternion.Slerp(origin.rotation, newDetectedPose.rotation, Time.deltaTime * _lerpSpeed);
        }
    }
}
