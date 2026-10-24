using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 1: World <-> door-space coordinate transforms, per
    /// docs/phases/phase-4-door-coordinate-system.md's own specified API
    /// (WorldToDoorSpace/DoorToWorldSpace/rotation transforms via
    /// Transform.InverseTransformPoint/TransformPoint).
    ///
    /// Deliberately does NOT reuse Phase 3's HandleWorldAnchorRefiner math
    /// as-is: that component's world-space placement was built and tested
    /// twice on-device and was NOT verified correct (see
    /// docs/phases/phase-3-implementation-plan.md Step 8's final status).
    /// This class owns its own origin construction so that question can be
    /// resolved in one place, instrumented, rather than inherited silently.
    ///
    /// Origin convention (roadmap doc Step 2): X = door width (local
    /// bottom-left -> bottom-right), Y = door height (bottom -> top),
    /// Z = normal (out of the surface, matching ARPlane.normal ==
    /// transform.up - see ARPlane.cs source).
    /// </summary>
    public class DoorCoordinateSystem : MonoBehaviour
    {
        [SerializeField] private Transform _doorOrigin;

        // Step 4's "Support for multiple doors... by maintaining a list of
        // active DoorCoordinateSystem instances" (DoorRegistry). Registering
        // here, at runtime Awake/OnDestroy, not from the Editor bootstrapper -
        // a bootstrapper-time Register() call only affects the Editor
        // process that happened to run the menu command, never the actual
        // built app, which would leave DoorRegistry silently empty on-device
        // (caught 2026-10-21 before it shipped, see
        // phase-4-implementation-plan.md).
        [SerializeField] private string _doorId = "front-door";

        public Transform DoorOrigin => _doorOrigin;
        public bool HasOrigin => _doorOrigin != null;

        private void Awake() => DoorRegistry.Register(_doorId, this);
        private void OnDestroy() => DoorRegistry.Unregister(_doorId);

        // ─── World <-> Door space (roadmap doc's own API) ────────────

        public Vector3 WorldToDoorSpace(Vector3 worldPoint)
        {
            return _doorOrigin.InverseTransformPoint(worldPoint);
        }

        public Vector3 DoorToWorldSpace(Vector3 doorPoint)
        {
            return _doorOrigin.TransformPoint(doorPoint);
        }

        public Quaternion WorldToDoorRotation(Quaternion worldRot)
        {
            return Quaternion.Inverse(_doorOrigin.rotation) * worldRot;
        }

        public Quaternion DoorToWorldRotation(Quaternion doorRot)
        {
            return _doorOrigin.rotation * doorRot;
        }

        // ─── Origin construction from an ARPlane (Option A) ──────────

        public struct OriginBuildResult
        {
            public bool Ok;
            public string FailReason;
            public float WidthMeters;
            public float HeightMeters;
        }

        /// <summary>
        /// Builds/updates the door origin from a geometric candidate plane.
        /// Caller is responsible for stability validation (see
        /// DoorOriginStabilityLogger) before trusting the result for
        /// anything visual - this method itself does not gate on stability,
        /// it only reports the raw numbers so the caller can decide.
        /// </summary>
        public OriginBuildResult SetOriginFromPlane(ARPlane plane, GameObject originHost)
        {
            if (plane == null)
                return new OriginBuildResult { Ok = false, FailReason = "plane is null" };

            if (_doorOrigin == null)
            {
                if (originHost == null) originHost = new GameObject("DoorOrigin");
                _doorOrigin = originHost.transform;
            }

            // Bottom-left corner of the plane's local-space bounding box
            // (extents are half-width/half-height around local origin,
            // same convention PlaneBoundaryAnalyzer already uses elsewhere
            // in this codebase - see its "For vertical planes: x = width,
            // y = height" comment).
            Vector2 extents = plane.extents;
            Vector3 bottomLeftLocal = new Vector3(-extents.x, 0f, -extents.y);
            Vector3 bottomLeftWorld = plane.transform.TransformPoint(bottomLeftLocal);

            _doorOrigin.position = bottomLeftWorld;
            // X axis = door width direction, Y axis = door height, Z = normal.
            //
            // Built from an explicit 3x3 basis matrix, NOT sequential
            // transform.right/.up assignment and NOT Quaternion.LookRotation
            // - both were tried and both failed verification: sequential
            // .right/.up assignment silently clobbers the first axis (the
            // second assignment rotates around more than just its own axis;
            // verified empirically, origin.right ended up nowhere near the
            // target after setting .up second), and LookRotation requires
            // hand-deriving a cross-product sign the same way Phase 3's
            // HandleWorldAnchorRefiner got wrong twice. This matrix
            // construction was verified to reproduce all three target axes
            // exactly (~1e-7 floating-point error) before being used here.
            Vector3 xAxis = plane.transform.right;
            Vector3 yAxis = plane.transform.forward;
            Vector3 zAxis = Vector3.Cross(xAxis, yAxis);
            var basis = new Matrix4x4();
            basis.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0f));
            basis.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0f));
            basis.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0f));
            basis.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
            _doorOrigin.rotation = basis.rotation;

            return new OriginBuildResult
            {
                Ok = true,
                WidthMeters = extents.x * 2f,
                HeightMeters = extents.y * 2f,
            };
        }

        /// <summary>
        /// Origin construction (Option B): sets the door origin directly from
        /// an already-computed world-space pose (DoorPnPWorldAnchor's output)
        /// rather than an ARPlane - see the "Extents-stability scan results"
        /// section of docs/phases/phase-4-implementation-plan.md for why
        /// Option A's ARPlane.extents was abandoned as a ground-truth source.
        /// </summary>
        public void SetOriginFromWorldPose(Vector3 worldPosition, Quaternion worldRotation, GameObject originHost)
        {
            if (_doorOrigin == null)
            {
                if (originHost == null) originHost = new GameObject("DoorOrigin");
                _doorOrigin = originHost.transform;
            }
            _doorOrigin.position = worldPosition;
            _doorOrigin.rotation = worldRotation;
        }
    }
}
