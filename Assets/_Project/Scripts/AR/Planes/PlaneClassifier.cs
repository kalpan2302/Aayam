using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Extended plane classification for Aayam beyond AR Foundation's built-in types.
    /// </summary>
    public enum AayamPlaneType
    {
        Unknown,
        Floor,
        Ceiling,
        Wall,
        Table,
        Door,
        DoorFrame
    }

    /// <summary>
    /// Classifies AR planes using:
    ///   1. Native ARKit/ARCore classification (most accurate)
    ///   2. Geometric heuristics (fallback for Android / unclassified planes)
    ///
    /// Standard Indian door dimensions used for heuristics:
    ///   Width: 700-1200mm, Height: 1800-2400mm
    /// </summary>
    public class PlaneClassifier : MonoBehaviour
    {
        [Header("Door Heuristics (meters)")]
        [SerializeField] private float _minDoorWidth  = 0.6f;
        [SerializeField] private float _maxDoorWidth  = 1.3f;
        [SerializeField] private float _minDoorHeight = 1.7f;
        [SerializeField] private float _maxDoorHeight = 2.5f;

        [Header("Area Thresholds (m²)")]
        [SerializeField] private float _minFloorArea  = 2.0f;  // below = table
        [SerializeField] private float _minWallArea   = 3.0f;  // below = unknown vertical

        // ─── Classification ───────────────────────────────────────

        /// <summary>Classify a plane. Returns AayamPlaneType.</summary>
        public AayamPlaneType ClassifyPlane(ARPlane plane)
        {
            // Step 1: Use native ARKit/ARCore classification where available
            var nativeResult = FromNativeClassification(plane.classification);
            if (nativeResult != AayamPlaneType.Unknown)
                return nativeResult;

            // Step 2: Fall back to geometric heuristics
            return FromGeometry(plane);
        }

        // ─── Native Classification ────────────────────────────────

        private AayamPlaneType FromNativeClassification(PlaneClassification classification)
        {
            return classification switch
            {
                PlaneClassification.Floor         => AayamPlaneType.Floor,
                PlaneClassification.Ceiling       => AayamPlaneType.Ceiling,
                PlaneClassification.Wall          => AayamPlaneType.Wall,
                PlaneClassification.Table         => AayamPlaneType.Table,
                PlaneClassification.Door          => AayamPlaneType.Door,
                PlaneClassification.Seat          => AayamPlaneType.Table,
                PlaneClassification.Window        => AayamPlaneType.Wall,
                _                                 => AayamPlaneType.Unknown,
            };
        }

        // ─── Geometric Heuristics ─────────────────────────────────

        private AayamPlaneType FromGeometry(ARPlane plane)
        {
            // Width and height using extents (half-size in plane-local space)
            float width  = plane.extents.x * 2f;
            float height = plane.extents.y * 2f;
            float area   = width * height;

            // Horizontal planes
            if (plane.alignment == PlaneAlignment.HorizontalUp ||
                plane.alignment == PlaneAlignment.HorizontalDown)
            {
                return area >= _minFloorArea ? AayamPlaneType.Floor : AayamPlaneType.Table;
            }

            // Vertical planes
            if (plane.alignment == PlaneAlignment.Vertical)
            {
                // Door size check
                if (width  >= _minDoorWidth  && width  <= _maxDoorWidth &&
                    height >= _minDoorHeight && height <= _maxDoorHeight)
                {
                    return AayamPlaneType.Door;
                }

                return area >= _minWallArea ? AayamPlaneType.Wall : AayamPlaneType.Unknown;
            }

            return AayamPlaneType.Unknown;
        }

        // ─── Utility ──────────────────────────────────────────────

        /// <summary>Returns a display string with color tags for debug UI.</summary>
        public string GetColoredLabel(AayamPlaneType type)
        {
            return type switch
            {
                AayamPlaneType.Door      => "<color=#FFD700>Door</color>",
                AayamPlaneType.Wall      => "<color=#90EE90>Wall</color>",
                AayamPlaneType.Floor     => "<color=#87CEEB>Floor</color>",
                AayamPlaneType.Ceiling   => "<color=#DDA0DD>Ceiling</color>",
                AayamPlaneType.Table     => "<color=#FFA07A>Table</color>",
                AayamPlaneType.DoorFrame => "<color=#FFC0CB>Frame</color>",
                _                        => "<color=#808080>Unknown</color>",
            };
        }
    }
}
