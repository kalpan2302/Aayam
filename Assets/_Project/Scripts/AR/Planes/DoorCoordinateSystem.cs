using UnityEngine;

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
