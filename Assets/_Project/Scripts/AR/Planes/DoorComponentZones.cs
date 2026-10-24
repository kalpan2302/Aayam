using System;
using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 2: component placement zones within door space (door-local
    /// meters, X=width Y=height - matches DoorCoordinateSystem's convention).
    /// Data-only port of docs/phases/phase-4-door-coordinate-system.md's Step 2
    /// table. Deliberately not wired into live placement yet - Steps 3-6 are
    /// blocked on Step 1's on-device verification (see
    /// docs/phases/phase-4-implementation-plan.md's "What's actually left"),
    /// and this data doesn't need that frame proven correct to be written down
    /// or unit-tested, only to be *used* for real placement.
    ///
    /// All millimeter figures are the roadmap doc's own values, converted to
    /// meters (Unity's native unit) - kept in comments next to each value so
    /// the mm source stays traceable and re-checkable against the doc.
    /// </summary>
    public static class DoorComponentZones
    {
        public enum ZoneType { Handle, Lock, Peephole, Hinge }

        /// <summary>
        /// A door-space (meters) rectangle a component's anchor point should
        /// fall within. MinX==MaxX (or MinY==MaxY) represents a documented
        /// single value rather than a range - e.g. the roadmap's peephole
        /// zone specifies only "X = Center", not a tolerance band around it.
        /// </summary>
        public struct Zone
        {
            public float MinX, MaxX, MinY, MaxY;

            public bool Contains(Vector2 doorSpacePoint) =>
                doorSpacePoint.x >= MinX && doorSpacePoint.x <= MaxX &&
                doorSpacePoint.y >= MinY && doorSpacePoint.y <= MaxY;
        }

        // Roadmap: Handle zone X = 680-770mm, Y = 950-1050mm.
        public static readonly Zone HandleZone =
            new Zone { MinX = 0.680f, MaxX = 0.770f, MinY = 0.950f, MaxY = 1.050f };

        // Roadmap gives the lock zone the IDENTICAL range to the handle zone
        // (phase-4-door-coordinate-system.md's own table, not a transcription
        // slip reproduced here) - plausible for a residential door where
        // handle and lock share one lockset. Kept as documented rather than
        // silently changed; revisit if Step 3 placement ever needs them
        // distinguishable.
        public static readonly Zone LockZone =
            new Zone { MinX = 0.680f, MaxX = 0.770f, MinY = 0.950f, MaxY = 1.050f };

        /// <summary>
        /// Roadmap: Peephole X = Center (Width/2), Y = 1450-1550mm. X is a
        /// single value, not a range, so door width must be supplied to
        /// resolve it - hence a method, not a static readonly like the zones
        /// above.
        /// </summary>
        public static Zone PeepholeZone(float doorWidthMeters)
        {
            float centerX = doorWidthMeters / 2f;
            return new Zone { MinX = centerX, MaxX = centerX, MinY = 1.450f, MaxY = 1.550f };
        }

        /// <summary>
        /// Roadmap: Hinge X = 0 or X = Width, Y = "Various locations along
        /// edge". The X side is precise (whichever edge carries the hinges),
        /// but the roadmap gives no numeric Y bounds - "various locations" is
        /// explicitly vague, not an omission here. Interpreted as spanning
        /// the full door height since no narrower range exists anywhere in
        /// the roadmap doc; this is the honest reading of an underspecified
        /// requirement, not a precision claim. Narrow this once real
        /// hinge-placement data exists (e.g. from the ML `hinge` class the
        /// Phase 3 roadmap scoped out - see phase-3-implementation-plan.md's
        /// "5 classes -> 2 classes" note).
        /// </summary>
        public static Zone HingeZone(float doorWidthMeters, float doorHeightMeters, bool hingeOnLeft)
        {
            float x = hingeOnLeft ? 0f : doorWidthMeters;
            return new Zone { MinX = x, MaxX = x, MinY = 0f, MaxY = doorHeightMeters };
        }

        public static Zone GetZone(ZoneType type, float doorWidthMeters, float doorHeightMeters, bool hingeOnLeft)
        {
            switch (type)
            {
                case ZoneType.Handle: return HandleZone;
                case ZoneType.Lock: return LockZone;
                case ZoneType.Peephole: return PeepholeZone(doorWidthMeters);
                case ZoneType.Hinge: return HingeZone(doorWidthMeters, doorHeightMeters, hingeOnLeft);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        /// <summary>
        /// Roadmap: Standard Door Dimensions (Indian Context) - width/height
        /// pairs, meters. Same table DoorPnPWorldAnchor's own doc comment
        /// points at for its "assumed door size" default.
        /// </summary>
        public static readonly (float WidthMeters, float HeightMeters)[] StandardSizes =
        {
            (0.900f, 2.100f),
            (1.000f, 2.100f),
        };

        /// <summary>Roadmap: common door thicknesses, mm, converted to meters.</summary>
        public static readonly float[] StandardThicknessesMeters =
        {
            0.035f, 0.038f, 0.040f, 0.045f, 0.050f,
        };
    }
}
