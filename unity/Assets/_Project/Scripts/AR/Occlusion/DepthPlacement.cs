using UnityEngine;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Uses the depth texture to place objects at screen points without AR plane detection.
    /// Useful for placing on uneven or curved surfaces where AR planes don't form.
    ///
    /// Requires DepthTextureHandler on the same camera GameObject.
    /// </summary>
    [RequireComponent(typeof(DepthTextureHandler))]
    [RequireComponent(typeof(Camera))]
    public class DepthPlacement : MonoBehaviour
    {
        [Header("Validation")]
        [SerializeField] private float _minDepthMeters = 0.15f;
        [SerializeField] private float _maxDepthMeters = 5.0f;

        [Header("Normal Estimation")]
        [Tooltip("Pixel offset for finite-difference normal estimation (larger = smoother).")]
        [SerializeField] private int _normalSampleOffset = 3;

        // ─── Components ───────────────────────────────────────────
        private DepthTextureHandler _depthHandler;
        private Camera             _arCamera;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _depthHandler = GetComponent<DepthTextureHandler>();
            _arCamera     = GetComponent<Camera>();
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Attempts to compute a placement Pose using depth data at a screen point.
        /// Returns false if depth is invalid or out of range.
        /// </summary>
        public bool TryGetPlacementPose(Vector2 screenPosition, out Pose hitPose)
        {
            hitPose = Pose.identity;

            float depth = _depthHandler.GetSmoothedDepthAtScreenPoint(screenPosition);

            if (depth < _minDepthMeters || depth > _maxDepthMeters)
                return false;

            // World-space position along the camera ray at the measured depth
            Ray ray = _arCamera.ScreenPointToRay(screenPosition);
            Vector3 worldPos = ray.origin + ray.direction.normalized * depth;

            // Estimate surface normal via finite differences in depth
            Vector3 normal = EstimateSurfaceNormal(screenPosition, depth);

            // Build rotation: forward = normal, up = gravity-aligned
            Vector3 projectedUp = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            if (projectedUp.sqrMagnitude < 0.001f)
                projectedUp = Vector3.ProjectOnPlane(_arCamera.transform.up, normal).normalized;

            Quaternion rotation = Quaternion.LookRotation(normal, projectedUp);

            hitPose = new Pose(worldPos, rotation);
            return true;
        }

        /// <summary>
        /// Returns the depth in meters at the given screen point,
        /// or 0 if unavailable.
        /// </summary>
        public float GetDepthAt(Vector2 screenPosition)
        {
            return _depthHandler.GetSmoothedDepthAtScreenPoint(screenPosition);
        }

        // ─── Normal Estimation ────────────────────────────────────

        /// <summary>
        /// Estimates a surface normal using finite differences:
        ///   sample depth at center, right, and up → compute the cross product.
        /// </summary>
        private Vector3 EstimateSurfaceNormal(Vector2 screenCenter, float centerDepth)
        {
            float dRight = _depthHandler.GetDepthAtScreenPoint(
                screenCenter + new Vector2(_normalSampleOffset, 0));
            float dUp = _depthHandler.GetDepthAtScreenPoint(
                screenCenter + new Vector2(0, _normalSampleOffset));

            // If neighbouring samples are invalid, fall back to camera forward
            if (dRight < _minDepthMeters || dUp < _minDepthMeters)
                return -_arCamera.transform.forward;

            // Convert to world-space points
            Vector3 pCenter = _arCamera.ScreenPointToRay(screenCenter).GetPoint(centerDepth);
            Vector3 pRight  = _arCamera.ScreenPointToRay(
                screenCenter + new Vector2(_normalSampleOffset, 0)).GetPoint(dRight);
            Vector3 pUp     = _arCamera.ScreenPointToRay(
                screenCenter + new Vector2(0, _normalSampleOffset)).GetPoint(dUp);

            // Cross product gives normal
            Vector3 normal = Vector3.Cross(pRight - pCenter, pUp - pCenter).normalized;

            // Ensure normal points toward camera
            if (Vector3.Dot(normal, _arCamera.transform.forward) > 0)
                normal = -normal;

            return normal;
        }
    }
}
