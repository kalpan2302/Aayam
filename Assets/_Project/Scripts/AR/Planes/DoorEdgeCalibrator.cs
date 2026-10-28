using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 5, steps 1-2 of the roadmap's 4-step calibration routine
    /// ("user taps to confirm door edges" - steps 3-4, the actual scale
    /// correction, already exist in DoorCalibration.cs). Pure/testable core:
    /// intersects a screen tap's ray against the door's ALREADY-SOLVED plane
    /// (DoorCoordinateSystem.DoorOrigin, from Step 1's PnP pose) rather than
    /// a fresh ARRaycastManager plane hit - keeps working even if the
    /// originating ARPlane has since stopped being tracked (and sidesteps
    /// PlacementReticle's separate ARKit-plane-tracking dependency entirely,
    /// see docs/phases/phase-4-implementation-plan.md's 2026-10-25 entry on
    /// why that system can't be reused here for this door).
    /// </summary>
    public static class DoorEdgeCalibrator
    {
        /// <summary>
        /// Standard ray-plane intersection. planeNormal need not be the
        /// door's full rotation - just the plane's outward normal (e.g.
        /// DoorOrigin.forward, per DoorCoordinateSystem's own documented
        /// Z=normal convention). Fails if the ray is parallel to the plane,
        /// or the intersection is behind the ray's origin.
        /// </summary>
        public static bool TryIntersectDoorPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal, out Vector3 worldPoint)
        {
            float denom = Vector3.Dot(ray.direction, planeNormal);
            if (Mathf.Abs(denom) < 1e-6f)
            {
                worldPoint = default;
                return false;
            }

            float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
            if (t < 0f)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = ray.origin + t * ray.direction;
            return true;
        }

        /// <summary>
        /// Distance between two world points as measured in the door's own
        /// space - both points are expected to already lie on (or very near)
        /// the door's plane (e.g. from TryIntersectDoorPlane against the
        /// same plane), so this is effectively the planar span between them.
        /// </summary>
        public static float MeasureDoorSpaceSpan(DoorCoordinateSystem door, Vector3 worldA, Vector3 worldB)
        {
            Vector3 a = door.WorldToDoorSpace(worldA);
            Vector3 b = door.WorldToDoorSpace(worldB);
            return Vector3.Distance(a, b);
        }
    }
}
