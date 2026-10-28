namespace Aayam.AR.Planes
{
    public enum DoorHandedness { LeftHand, RightHand }

    /// <summary>
    /// Phase 4 Step 6: "Detect hinge side through structural analysis or
    /// user prompt. Infer opening direction (left-hand or right-hand,
    /// inward or outward). Initially user-assisted ('Tap the side with
    /// hinges')."
    ///
    /// FromHandleSide is a heuristic default, NOT structural detection - no
    /// code anywhere in this project currently classifies hinges visually
    /// (Phase 3's roadmap scoped the ML model down to 2 classes, door and
    /// handle, dropping hinge - see phase-3-implementation-plan.md's
    /// "5 classes -> 2 classes" note). The heuristic assumes hinges sit
    /// opposite the handle, true for a standard single-handle residential
    /// door, and should be treated as a starting guess the user can
    /// override, not a detection result.
    ///
    /// Inward/outward swing is NOT modeled here at all - the roadmap doc
    /// itself doesn't describe how to derive it from a single camera view
    /// either (it's listed "user-assisted" too, same as hinge side), and no
    /// sensor input in this codebase could currently supply it.
    /// </summary>
    public struct DoorOpeningDirection
    {
        public bool HingeOnLeft;
        public bool UserConfirmed;

        public static DoorOpeningDirection FromHandleSide(bool isLeftHandled)
        {
            return new DoorOpeningDirection { HingeOnLeft = !isLeftHandled, UserConfirmed = false };
        }

        /// <summary>Roadmap's "Tap the side with hinges" user-assist path.</summary>
        public DoorOpeningDirection WithUserOverride(bool hingeOnLeft)
        {
            return new DoorOpeningDirection { HingeOnLeft = hingeOnLeft, UserConfirmed = true };
        }

        public DoorHandedness Handedness => HingeOnLeft ? DoorHandedness.LeftHand : DoorHandedness.RightHand;
    }
}
