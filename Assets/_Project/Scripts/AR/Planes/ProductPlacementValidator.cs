using UnityEngine;
using Aayam.Models;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 3: "Constraint system: Hard constraints (e.g., prevent
    /// placing handles near the top edge)." This is the enforcement side of
    /// Step 2's zone data - a proposed user drag-and-drop position is
    /// rejected if it falls outside both the door's own bounds and the
    /// component's valid zone. The drag-and-drop UI itself (clamping a live
    /// gesture to these bounds, not just validating a final position) is
    /// NOT built here - that's real interaction work (see
    /// AR/Interaction/DragHandler.cs) deferred until Step 1 is confirmed on
    /// a real device, same reasoning as the rest of Steps 3-6.
    /// </summary>
    public static class ProductPlacementValidator
    {
        public struct ValidationResult
        {
            public bool Ok;
            public string Reason;
        }

        public static ValidationResult Validate(
            Vector3 doorSpacePosition,
            DoorComponentZones.ZoneType zoneType,
            float doorWidthMeters,
            float doorHeightMeters,
            bool hingeOnLeft)
        {
            if (doorWidthMeters <= 0f || doorHeightMeters <= 0f)
                return new ValidationResult { Ok = false, Reason = "invalid door dimensions" };

            if (doorSpacePosition.x < 0f || doorSpacePosition.x > doorWidthMeters ||
                doorSpacePosition.y < 0f || doorSpacePosition.y > doorHeightMeters)
                return new ValidationResult { Ok = false, Reason = "outside door bounds" };

            var zone = DoorComponentZones.GetZone(zoneType, doorWidthMeters, doorHeightMeters, hingeOnLeft);
            var point2D = new Vector2(doorSpacePosition.x, doorSpacePosition.y);
            if (!zone.Contains(point2D))
                return new ValidationResult { Ok = false, Reason = $"outside valid {zoneType} zone" };

            return new ValidationResult { Ok = true, Reason = null };
        }
    }
}
