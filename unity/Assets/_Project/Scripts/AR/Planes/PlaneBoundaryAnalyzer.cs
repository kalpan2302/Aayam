using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Extracts dimensional and shape information from an ARPlane's boundary polygon.
    /// Used by DoorCandidateDetector and PlaneClassifier for heuristic scoring.
    /// </summary>
    public class PlaneBoundaryAnalyzer : MonoBehaviour
    {
        // ─── Dimensions ───────────────────────────────────────────

        /// <summary>
        /// Returns plane dimensions (width, height) in meters using extents.
        /// For vertical planes: x = width, y = height.
        /// </summary>
        public Vector2 EstimateDimensions(ARPlane plane)
        {
            float width  = plane.extents.x * 2f;
            float height = plane.extents.y * 2f;
            return new Vector2(width, height);
        }

        /// <summary>Returns the area of the plane bounding box in m².</summary>
        public float EstimateArea(ARPlane plane)
        {
            var dims = EstimateDimensions(plane);
            return dims.x * dims.y;
        }

        /// <summary>Returns the aspect ratio (height / width) for a vertical plane.</summary>
        public float GetAspectRatio(ARPlane plane)
        {
            var dims = EstimateDimensions(plane);
            if (dims.x < 0.001f) return 0f;
            return dims.y / dims.x;
        }

        // ─── Shape Analysis ───────────────────────────────────────

        /// <summary>
        /// Checks how rectangular a plane is.
        /// Ratio of polygon area to bounding-box area — close to 1.0 = rectangular.
        /// </summary>
        public bool IsRectangular(ARPlane plane, float tolerance = 0.2f)
        {
            float polyArea = CalculatePolygonArea(plane);
            float boxArea  = EstimateArea(plane);

            if (boxArea < 0.001f) return false;

            float ratio = polyArea / boxArea;
            return Mathf.Abs(1f - ratio) < tolerance;
        }

        /// <summary>Returns the polygon area via the shoelace formula (in plane-local space).</summary>
        public float CalculatePolygonArea(ARPlane plane)
        {
            var boundary = plane.boundary;
            if (boundary.Length < 3) return 0f;

            float area = 0f;
            for (int i = 0; i < boundary.Length; i++)
            {
                int j = (i + 1) % boundary.Length;
                area += boundary[i].x * boundary[j].y;
                area -= boundary[j].x * boundary[i].y;
            }
            return Mathf.Abs(area * 0.5f);
        }

        // ─── Boundary Metrics ─────────────────────────────────────

        /// <summary>Returns the count of boundary polygon vertices.</summary>
        public int GetBoundaryVertexCount(ARPlane plane) => plane.boundary.Length;

        /// <summary>Returns the perimeter of the boundary polygon in meters.</summary>
        public float CalculatePerimeter(ARPlane plane)
        {
            var boundary = plane.boundary;
            if (boundary.Length < 2) return 0f;

            float perimeter = 0f;
            for (int i = 0; i < boundary.Length; i++)
            {
                int j = (i + 1) % boundary.Length;
                perimeter += Vector2.Distance(boundary[i], boundary[j]);
            }
            return perimeter;
        }

        /// <summary>
        /// Returns the "compactness" of the boundary: 4π × area / perimeter².
        /// 1.0 = perfect circle, lower = more elongated/irregular.
        /// </summary>
        public float CalculateCompactness(ARPlane plane)
        {
            float area      = CalculatePolygonArea(plane);
            float perimeter = CalculatePerimeter(plane);
            if (perimeter < 0.001f) return 0f;
            return (4f * Mathf.PI * area) / (perimeter * perimeter);
        }
    }
}
