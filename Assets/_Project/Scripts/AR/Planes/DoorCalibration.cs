using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 5: "Ground Truth Comparison" and the scale-correction
    /// half of the roadmap's calibration routine
    /// (docs/phases/phase-4-door-coordinate-system.md Step 5):
    ///   1. Detect the door via CV/AR.
    ///   2. Ask the user to confirm door edges by tapping.
    ///   3. User inputs a known physical measurement.
    ///   4. System calibrates the internal scale factor to match reality.
    /// Steps 1-2 (detect, tap-to-confirm-edges) are a UI/interaction flow,
    /// not built here - same deferral reasoning as Step 3's drag-and-drop
    /// (see ProductPlacementValidator's class doc). This class is steps 3-4:
    /// given the real-world measurement a user enters, compute the
    /// correction DoorPnPWorldAnchor's assumed size should have been using.
    /// </summary>
    public static class DoorCalibration
    {
        /// <summary>Roadmap's own accuracy deliverable: "Accuracy &lt; 5mm for placement".</summary>
        public const float AccuracyTargetMeters = 0.005f;

        public struct CalibrationResult
        {
            public bool Ok;
            public string FailReason;
            public float ScaleCorrection;
        }

        /// <summary>
        /// assumedSizeMeters: what DoorPnPWorldAnchor's PnP solve assumed
        /// (_assumedDoorWidthMeters/_assumedDoorHeightMeters).
        /// measuredSizeMeters: what the user actually measured with a tape.
        /// Multiply recovered positions/sizes by the result to correct for
        /// the gap between the two - see DoorPnPWorldAnchor's own class doc
        /// on why an assumed-size mismatch scales the whole recovered pose
        /// (monocular PnP from one planar target is scale-ambiguous between
        /// assumed object size and recovered distance).
        /// </summary>
        public static CalibrationResult ComputeScaleCorrection(float assumedSizeMeters, float measuredSizeMeters)
        {
            if (assumedSizeMeters <= 0f || measuredSizeMeters <= 0f)
                return new CalibrationResult { Ok = false, FailReason = "sizes must be positive" };

            return new CalibrationResult
            {
                Ok = true,
                ScaleCorrection = measuredSizeMeters / assumedSizeMeters,
            };
        }

        /// <summary>
        /// Roadmap Step 5: "Validate placed objects against physical
        /// measurements." Euclidean distance between a real-world-measured
        /// point and the system's recovered point, same space for both
        /// (world or door - caller's choice, as long as it's consistent).
        /// </summary>
        public static float PlacementErrorMeters(Vector3 measuredPoint, Vector3 recoveredPoint)
        {
            return Vector3.Distance(measuredPoint, recoveredPoint);
        }

        public static bool MeetsAccuracyTarget(float placementErrorMeters) => placementErrorMeters < AccuracyTargetMeters;
    }
}
