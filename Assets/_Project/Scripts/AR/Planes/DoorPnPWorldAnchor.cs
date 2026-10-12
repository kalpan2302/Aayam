using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 1, Option B: converts VisualDoorSegmenter's detected
    /// door corners (mask-space pixels) into a world-space door origin via
    /// DoorPnPSolver, and feeds it into DoorCoordinateSystem. Replaces
    /// trusting ARPlane.extents directly (Option A) - see
    /// docs/phases/phase-4-implementation-plan.md's "Extents-stability scan
    /// results" section for why: ARKit's own plane boundary converges to a
    /// stable but undersized answer on this door (locked at 0.600m wide for
    /// 107 consecutive samples, vs. the 0.9-1.0m standard widths assumed
    /// below), so the door's real size/pose has to come from the ML
    /// detection + an assumed standard size instead.
    ///
    /// All the camera pose/intrinsics used here are captured SYNCHRONOUSLY
    /// at the same instant as the frame the corners came from (see
    /// VisualDoorSegmenter.CaptureContext) - deliberately not re-read later,
    /// which is the exact timing gap that undermined
    /// HandleWorldAnchorRefiner's on-device verification in Phase 3.
    ///
    /// Positions a visible marker at the computed door origin for
    /// verification - like HandleWorldAnchorRefiner, this is a visual
    /// stepping stone, NOT YET VERIFIED on-device. DoorPnPSolver's math
    /// itself is verified (DoorPnPSolverTests, synthetic ground truth,
    /// passing in-session); the end-to-end pipeline through a real camera
    /// frame is not.
    /// </summary>
    public class DoorPnPWorldAnchor : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private VisualDoorSegmenter _segmenter;
        [SerializeField] private DoorCoordinateSystem _coordinateSystem;

        [Header("Assumed real-world door size (roadmap standard sizes)")]
        [SerializeField] private float _assumedDoorWidthMeters = 0.9f;
        [SerializeField] private float _assumedDoorHeightMeters = 2.1f;

        [Header("Marker")]
        [SerializeField] private Color _markerColor = Color.cyan;

        public Vector3? LastWorldPosition { get; private set; }
        public Quaternion? LastWorldRotation { get; private set; }

        /// <summary>Fired after each successful PnP solve with the resulting world-space door origin pose.</summary>
        public event System.Action<Vector3, Quaternion> OnWorldPoseUpdated;

        private GameObject _originHost;
        private GameObject _markerInstance;

        private void OnEnable()
        {
            if (_segmenter != null)
                _segmenter.OnDoorGeometry += HandleGeometry;
        }

        private void OnDisable()
        {
            if (_segmenter != null)
                _segmenter.OnDoorGeometry -= HandleGeometry;
        }

        private void OnDestroy()
        {
            if (_markerInstance != null) Destroy(_markerInstance);
            if (_originHost != null) Destroy(_originHost);
        }

        private void HandleGeometry(VisualDoorSegmenter.DoorResult result)
        {
            if (!result.Ok || !result.HasCameraPose || !result.HasIntrinsics)
                return;

            // Rotated-frame pixel space == (RawHeight, RawWidth): the exact
            // swap RotateRgba90Clockwise performs (newWidth=height,
            // newHeight=width) - so no separate "rotated frame size" field
            // is needed, it's derivable from the raw dims already captured.
            int rotatedFrameWidth = result.RawHeight;
            int rotatedFrameHeight = result.RawWidth;

            float scaleX = (float)rotatedFrameWidth / result.MaskWidth;
            float scaleY = (float)rotatedFrameHeight / result.MaskHeight;

            var pixelCorners = new[]
            {
                new Vector2(result.TopLeftPx.x * scaleX, result.TopLeftPx.y * scaleY),
                new Vector2(result.TopRightPx.x * scaleX, result.TopRightPx.y * scaleY),
                new Vector2(result.BottomRightPx.x * scaleX, result.BottomRightPx.y * scaleY),
                new Vector2(result.BottomLeftPx.x * scaleX, result.BottomLeftPx.y * scaleY),
            };

            float w = _assumedDoorWidthMeters, h = _assumedDoorHeightMeters;
            var objectPoints = new[]
            {
                new Vector2(0, h), new Vector2(w, h), new Vector2(w, 0), new Vector2(0, 0),
            };

            var rawIntrinsics = new DoorPnPSolver.Intrinsics
            {
                Fx = result.RawFx, Fy = result.RawFy, Cx = result.RawCx, Cy = result.RawCy,
            };
            var rotatedIntrinsics = DoorPnPSolver.RotateIntrinsics90Clockwise(rawIntrinsics, result.RawWidth, result.RawHeight);

            var pnp = DoorPnPSolver.SolvePlanarPose(pixelCorners, objectPoints, rotatedIntrinsics);
            if (!pnp.Ok)
            {
                Debug.Log($"[DoorPnPWorldAnchor] PnP failed: {pnp.FailReason}");
                return;
            }

            Vector3 worldPos = result.CameraRotationAtCapture * pnp.PositionCameraLocal + result.CameraPositionAtCapture;
            Quaternion worldRot = result.CameraRotationAtCapture * pnp.RotationCameraLocal;

            // NOT YET VERIFIED on-device - see class doc. Logged deliberately
            // so whoever does that check next has the evidence trail,
            // matching HandleWorldAnchorRefiner's "left in deliberately" log.
            Debug.Log($"[DoorPnPWorldAnchor] worldPos={worldPos} worldRot={worldRot.eulerAngles} " +
                      $"camPos={result.CameraPositionAtCapture} camRot={result.CameraRotationAtCapture.eulerAngles} " +
                      $"assumedSize={w}x{h}m");

            if (_coordinateSystem != null)
            {
                if (_originHost == null) _originHost = new GameObject("DoorOrigin_PnP");
                _coordinateSystem.SetOriginFromWorldPose(worldPos, worldRot, _originHost);
            }

            LastWorldPosition = worldPos;
            LastWorldRotation = worldRot;
            ShowMarker(worldPos);
            OnWorldPoseUpdated?.Invoke(worldPos, worldRot);
        }

        private void ShowMarker(Vector3 worldPosition)
        {
            if (_markerInstance == null)
            {
                // Explicit URP shader - CreatePrimitive's default material
                // renders magenta ("missing shader") under this project's
                // URP, hit twice already (PlacementReticle, undiagnosed;
                // HandleWorldAnchorRefiner, caught and fixed) - not repeating it.
                _markerInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _markerInstance.transform.localScale = Vector3.one * 0.03f;
                Destroy(_markerInstance.GetComponent<Collider>());
                var renderer = _markerInstance.GetComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                renderer.material = shader != null ? new Material(shader) : renderer.material;
                renderer.material.color = _markerColor;
            }
            _markerInstance.transform.position = worldPosition;
        }
    }
}
