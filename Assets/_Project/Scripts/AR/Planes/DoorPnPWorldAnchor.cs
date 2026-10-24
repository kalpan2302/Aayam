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
    /// verification. VERIFIED on-device 2026-10-24 (see
    /// docs/phases/phase-4-implementation-plan.md): the marker now lands on
    /// or near the visible frame on real captured data, after removing the
    /// extra DoorPnPSolver.RotatedFrameCorrection step below - see that
    /// comment for why it was wrong, and why neither the solver math, the
    /// world-transform, the assumed door size, nor camera staleness were
    /// ever the cause (all independently cross-checked and cleared first).
    ///
    /// KNOWN, ACCEPTED RESIDUAL (2026-10-24, Kalpan's decision): the marker
    /// still lands ~0.2-0.3m off from the door's true physical corner, a
    /// smooth/systematic (not jittery) offset that persists even with the
    /// full door framed. Every step of the pose math was independently
    /// re-verified at that point (camera-local geometry matches the
    /// assumed door size to ~1cm) - this isn't a software bug this file can
    /// fix, it's either the ML segmentation model's own corner-detection
    /// precision or a real ARKit pose/lens calibration gap. Accepted as the
    /// current precision ceiling for this detection approach rather than
    /// investigated further - see the plan doc's final entry on this before
    /// reopening it.
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
        // Serialized ASSET reference, not a runtime Shader.Find lookup - the
        // latter failed on-device twice in a row (2026-10-15/21) despite
        // being verified in the Editor both times and despite Always
        // Included Shaders supposedly covering it; screen recordings kept
        // showing the "missing shader" magenta fallback regardless. A
        // Material asset Unity's build pipeline can see as a direct
        // reference (Assets/_Project/Materials/DoorPnPMarker.mat) removes
        // the string-lookup/stripping ambiguity entirely - this is the
        // standard way to guarantee a shader survives a build, not a
        // workaround. Assign the asset in the Inspector (or via
        // AayamSceneBootstrapper, which now does this automatically).
        [SerializeField] private Material _markerMaterialTemplate;
        [SerializeField] private Color _markerColor = Color.cyan;

        public Vector3? LastWorldPosition { get; private set; }
        public Quaternion? LastWorldRotation { get; private set; }

        /// <summary>
        /// Fired after each successful PnP solve with the resulting world-space
        /// door origin pose, plus distance from the capturing camera and whether
        /// that point was in front of it - diagnostic fields added 2026-10-15
        /// because the marker was invisible on-device with no way to tell
        /// whether that meant "behind the camera" vs "far off to the side"
        /// vs "right where the camera stood" without console access.
        /// </summary>
        public event System.Action<Vector3, Quaternion, float, bool, string, string> OnWorldPoseUpdated;

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

            // 2026-10-24: DoorPnPSolver.RotatedFrameCorrection (an extra
            // +90deg rotation applied here since 2026-10-15) is REMOVED -
            // it was the dominant cause of the marker landing off-screen,
            // confirmed on real device data (docs/phases/
            // phase-4-implementation-plan.md has the full evidence chain).
            // RotateRgba90Clockwise + RotateIntrinsics90Clockwise above
            // already correctly align the solved pose with Unity's actual
            // camera-local axes (X right, Y up, Z forward) for this
            // device's real sensor mounting - adding a further +90deg on
            // top of that was an unnecessary second rotation, not a fix for
            // anything. Proven algebraically, not just empirically: solving
            // on rotated pixels/intrinsics and then applying +90deg is
            // mathematically IDENTICAL to solving directly on raw
            // (unrotated) pixels/intrinsics with no correction at all
            // (verified offline against real captured data) - i.e. the
            // rotate-then-correct round trip was a no-op, so the shipped
            // behavior was indistinguishable from never rotating at all,
            // while the real fix needed was to rotate once and stop.
            // DoorPnPSolverTests.RotatedPipeline_RecoversGroundTruthPose's
            // synthetic ground truth has been corrected to match (its
            // "camera-local == raw sensor pixel axes" assumption was the
            // untested, and ultimately wrong, premise this depended on).
            Vector3 positionCameraLocal = pnp.PositionCameraLocal;
            Quaternion rotationCameraLocal = pnp.RotationCameraLocal;

            Vector3 worldPos = result.CameraRotationAtCapture * positionCameraLocal + result.CameraPositionAtCapture;
            Quaternion worldRot = result.CameraRotationAtCapture * rotationCameraLocal;

            Debug.Log($"[DoorPnPWorldAnchor] worldPos={worldPos} worldRot={worldRot.eulerAngles} " +
                      $"camPos={result.CameraPositionAtCapture} camRot={result.CameraRotationAtCapture.eulerAngles} " +
                      $"assumedSize={w}x{h}m");

            if (_coordinateSystem != null)
            {
                if (_originHost == null) _originHost = new GameObject("DoorOrigin_PnP");
                _coordinateSystem.SetOriginFromWorldPose(worldPos, worldRot, _originHost);
            }

            Vector3 toMarker = worldPos - result.CameraPositionAtCapture;
            float distFromCamera = toMarker.magnitude;
            Vector3 camForward = result.CameraRotationAtCapture * Vector3.forward;
            bool inFrontOfCamera = Vector3.Dot(camForward, toMarker) > 0f;

            string intrinsicsDiag = result.IntrinsicsResolutionMismatch
                ? $"intrinsics CORRECTED (reported {result.IntrinsicsReportedResolution.x}x{result.IntrinsicsReportedResolution.y} vs actual {result.RawWidth}x{result.RawHeight})"
                : "intrinsics OK (matched captured resolution)";

            // Viewport readout kept as an ongoing on-screen/console sanity
            // check (not a live hypothesis test anymore - that investigation
            // is closed, see class doc). Note a known, separate, smaller-
            // magnitude residual found during that investigation and NOT
            // fixed here: Camera.main's actual projectionMatrix tracks the
            // real device screen's aspect (~0.46, via ARCameraManager/ARKit
            // overriding it every frame) rather than the ~0.75
            // VisualDoorSegmenter's own fieldOfView/aspect fix sets - reading
            // .aspect back shows the set value, but the matrix actually used
            // for rendering/WorldToViewportPoint does not. Confirmed via
            // Camera.main.projectionMatrix's m00/m11 terms on-device
            // 2026-10-24; not revisited since the rotation fix above already
            // resolved the dominant symptom.
            string viewportDiag = "viewport: no Camera.main";
            if (Camera.main != null)
            {
                Vector3 vp = Camera.main.WorldToViewportPoint(worldPos);
                bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                viewportDiag = $"cyan=({vp.x:F2},{vp.y:F2}) {(onScreen ? "ON" : "OFF")}";
            }

            Debug.Log($"[DoorPnPWorldAnchor] {intrinsicsDiag} | {viewportDiag}");

            LastWorldPosition = worldPos;
            LastWorldRotation = worldRot;
            ShowMarker(worldPos);
            OnWorldPoseUpdated?.Invoke(worldPos, worldRot, distFromCamera, inFrontOfCamera, intrinsicsDiag, viewportDiag);
        }

        private void ShowMarker(Vector3 worldPosition)
        {
            if (_markerInstance == null)
            {
                _markerInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                // 15cm, up from 3cm - a 3cm sphere a couple meters out was
                // indistinguishable from "nothing rendered" on the first
                // on-device verification pass (2026-10-15).
                _markerInstance.transform.localScale = Vector3.one * 0.15f;
                Destroy(_markerInstance.GetComponent<Collider>());
                var renderer = _markerInstance.GetComponent<MeshRenderer>();

                if (_markerMaterialTemplate != null)
                {
                    // Instance copy (not sharedMaterial) so tweaking
                    // _markerColor doesn't touch the project asset.
                    renderer.material = new Material(_markerMaterialTemplate);
                    renderer.material.color = _markerColor;
                }
                else
                {
                    Debug.LogWarning("[DoorPnPWorldAnchor] _markerMaterialTemplate not assigned - " +
                                      "falling back to CreatePrimitive's default material, which has " +
                                      "twice shown as the \"missing shader\" magenta fallback on-device " +
                                      "(2026-10-15, 2026-10-21). Assign Assets/_Project/Materials/" +
                                      "DoorPnPMarker.mat in the Inspector.");
                }
            }
            _markerInstance.transform.position = worldPosition;
        }
    }
}
