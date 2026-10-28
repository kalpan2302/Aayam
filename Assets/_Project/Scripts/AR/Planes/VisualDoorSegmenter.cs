using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 3 Step 8: runs the trained door+handle YOLOv8n-seg detector and
    /// extracts door geometry (corners, dimensions, handle side) via
    /// DoorGeometryExtractor. Sibling to VisualDoorDetector (box-only model),
    /// not a replacement - this is the new segmentation path.
    ///
    /// Frame capture (acquire + rotate 90CW) is a direct copy of
    /// VisualDoorDetector's, which already fixed the
    /// XRCpuImage.Transformation.None raw-sensor-orientation bug (Step 7).
    /// Must be attached to the same GameObject as the real ARCameraManager.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class VisualDoorSegmenter : MonoBehaviour
    {
        private const int InputSize = 640;
        private const int NumClasses = 2; // door, handle
        private const int NumAnchors = 8400;
        private const int NumMaskCoeffs = 32;
        private const int ProtoSize = 160; // mask prototype spatial resolution

        public enum DetectionClass { Door = 0, Handle = 1 }

        public struct DoorResult
        {
            public bool Ok;
            public string FailReason;
            public float Confidence;
            public float WidthPx, HeightPx;
            public bool IsLeftHandled;
            public bool HandleFound;

            /// <summary>Handle position as a (u,v) fraction within the door
            /// quad (u: left=0..right=1, v: top=0..bottom=1) - scale- and
            /// shape-independent. See DoorGeometryExtractor.DoorGeometry.</summary>
            public Vector2 HandleNormalized;

            // ─── Phase 4 Step 1, Option B: everything DoorPnPWorldAnchor
            // needs to recover world-space pose, captured/computed at the
            // SAME frame as the corners above - not re-read later, which is
            // the exact timing gap that undermined Phase 3's
            // HandleWorldAnchorRefiner verification.

            /// <summary>Door corners in mask-space pixels (MaskWidth x MaskHeight), same order DoorGeometryExtractor returns: TL,TR,BR,BL.</summary>
            public Vector2 TopLeftPx, TopRightPx, BottomRightPx, BottomLeftPx;
            public int MaskWidth, MaskHeight;

            public bool HasCameraPose;
            public Vector3 CameraPositionAtCapture;
            public Quaternion CameraRotationAtCapture;

            /// <summary>Raw (pre-rotation) camera intrinsics and resolution, captured synchronously in OnCameraFrameReceived.</summary>
            public bool HasIntrinsics;
            public float RawFx, RawFy, RawCx, RawCy;
            public int RawWidth, RawHeight;

            /// <summary>
            /// True if ARCameraManager.TryGetIntrinsics's resolution didn't
            /// match the actual captured CPU image, so RawFx/Fy/Cx/Cy above
            /// were proportionally corrected rather than used as-reported -
            /// see OnCameraFrameReceived. Surfaced here so it's visible
            /// on-device (DoorPnPDebugLabel) without needing console access.
            /// </summary>
            public bool IntrinsicsResolutionMismatch;
            public Vector2Int IntrinsicsReportedResolution;
        }

        [Header("Model")]
        [SerializeField] private ModelAsset _modelAsset;
        [SerializeField] private BackendType _backendType = BackendType.CPU; // Step 7: CPU measured faster+more stable than GPUCompute on this chip

        [Header("Scanning")]
        [SerializeField] private int _inferEveryNFrames = 8;
        [SerializeField] [Range(0f, 1f)] private float _confidenceThreshold = 0.4f;
        [SerializeField] [Range(0f, 1f)] private float _iouThreshold = 0.45f;
        [SerializeField] [Range(0f, 1f)] private float _maskThreshold = 0.5f;

        /// <summary>Fired after each inference pass that finds a door with a valid 4-corner mask.</summary>
        public event Action<DoorResult> OnDoorGeometry;

        private ARCameraManager _cameraManager;
        private Camera _arCamera;
        private Worker _worker;
        private Texture2D _frameTexture;
        private int _frameCounter;
        private bool _isInferring;
        private bool _loggedFovDiagnosticOnce;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
            _arCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_modelAsset == null)
            {
                Debug.LogError("[VisualDoorSegmenter] No ModelAsset assigned - disabling.");
                enabled = false;
                return;
            }

            var model = ModelLoader.Load(_modelAsset);
            _worker = new Worker(model, _backendType);
            _frameCounter = 0;

            if (_cameraManager != null)
                _cameraManager.frameReceived += OnCameraFrameReceived;
        }

        private void OnDisable()
        {
            if (_cameraManager != null)
                _cameraManager.frameReceived -= OnCameraFrameReceived;

            _worker?.Dispose();
            _worker = null;
        }

        private void OnDestroy()
        {
            if (_frameTexture != null)
                Destroy(_frameTexture);
        }

        // ─── Frame Callback (copy of VisualDoorDetector's, rotation fix included) ──

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_isInferring) return;
            if (++_frameCounter < _inferEveryNFrames) return;
            _frameCounter = 0;

            if (!_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            // Captured synchronously, same instant as the frame used for
            // inference below - NOT re-read after the async inference
            // completes, which is the timing gap Phase 3's
            // HandleWorldAnchorRefiner verification got burned by.
            bool hasIntrinsics = _cameraManager.TryGetIntrinsics(out var intrinsics);
            Vector3 cameraPositionAtCapture = transform.position;
            Quaternion cameraRotationAtCapture = transform.rotation;

            int width, height;
            byte[] rgba;
            using (image)
            {
                width = image.width;
                height = image.height;
                rgba = new byte[width * height * 4];

                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, width, height),
                    outputDimensions = new Vector2Int(width, height),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None,
                };

                unsafe
                {
                    fixed (byte* ptr = rgba)
                    {
                        image.Convert(conversionParams, new IntPtr(ptr), rgba.Length);
                    }
                }
            }

            int rawWidth = width, rawHeight = height;
            rgba = RotateRgba90Clockwise(rgba, width, height, out int rotatedWidth, out int rotatedHeight);
            width = rotatedWidth;
            height = rotatedHeight;

            if (_frameTexture == null || _frameTexture.width != width || _frameTexture.height != height)
            {
                if (_frameTexture != null) Destroy(_frameTexture);
                _frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
            _frameTexture.LoadRawTextureData(rgba);
            _frameTexture.Apply();

            float rawFx = intrinsics.focalLength.x;
            float rawFy = intrinsics.focalLength.y;
            float rawCx = intrinsics.principalPoint.x;
            float rawCy = intrinsics.principalPoint.y;

            // ARCameraManager.TryGetIntrinsics's focalLength/principalPoint are
            // calibrated for intrinsics.resolution - AR Foundation's contract
            // does NOT guarantee that matches the CPU image actually acquired
            // above (rawWidth x rawHeight), and this codebase never checked
            // before 2026-10-21. A mismatch here scales the recovered PnP pose
            // by the resolution ratio - the exact symptom found on-device that
            // day (DoorPnPWorldAnchor's marker landed further from the real
            // door the farther the camera stood back, converging toward
            // roughly correct up close), after the rotation-frame math, the
            // assumed door size, and the mask-to-frame scaling were all
            // independently verified correct via synthetic ground-truth tests
            // and ruled out. Scaling Fx/Fy/Cx/Cy proportionally here is the
            // standard correction - NOT yet confirmed on-device as the actual
            // fix (no console access at the time), so treat as the leading
            // hypothesis until re-tested, not a confirmed resolution.
            bool resolutionMismatch = hasIntrinsics && intrinsics.resolution.x > 0 && intrinsics.resolution.y > 0 &&
                                       (intrinsics.resolution.x != rawWidth || intrinsics.resolution.y != rawHeight);
            if (resolutionMismatch)
            {
                float ratioX = (float)rawWidth / intrinsics.resolution.x;
                float ratioY = (float)rawHeight / intrinsics.resolution.y;
                rawFx *= ratioX;
                rawCx *= ratioX;
                rawFy *= ratioY;
                rawCy *= ratioY;
            }

            // AR Foundation never syncs Camera.fieldOfView to the real device
            // intrinsics on its own - it defaults to whatever was set in the
            // Inspector (Unity's new-Camera default, 60 deg) regardless of
            // the actual lens. Found 2026-10-21: the cyan marker's recovered
            // world position checked out (camera-local angle to the door was
            // only ~22-33 deg off axis, well inside a real phone lens), yet
            // Camera.main.WorldToViewportPoint() put it at viewport X
            // ~1.3-1.5 - a camera rendering with a narrower FOV than the real
            // lens pushes real, on-camera points outside Unity's render.
            // rawFx is calibrated against the sensor's native (landscape)
            // width axis; after RotateRgba90Clockwise, that axis becomes the
            // portrait screen's VERTICAL axis, which is what
            // Camera.fieldOfView means in Unity.
            if (hasIntrinsics && _arCamera != null && rawFx > 0f)
            {
                _arCamera.fieldOfView = ComputeVerticalFieldOfViewDegrees(rawWidth, rawFx);

                // 2026-10-22 [FOVDIAG] confirmed cameraAspect was still 0.46
                // (Unity's screen-tracked default, Screen=1206x2622) while the
                // PnP/azimuth math is computed in the raw capture's rotated
                // aspect (rawHeight/rawWidth = 1440/1920 = 0.75) - the two
                // disagreeing means Unity's camera frustum is narrower than
                // the space the recovered world position was solved in,
                // pushing real on-screen points outside the render. Setting
                // Camera.aspect explicitly (it stops auto-tracking Screen's
                // aspect once assigned) matches the two spaces.
                _arCamera.aspect = (float)rawHeight / rawWidth;

                // One-time diagnostic (2026-10-22): the FOV fix's own math
                // checked out synthetically, but the marker was still
                // reported off-screen after it shipped, by a much smaller
                // margin than before. Logging Camera.aspect/Screen size here
                // rather than assuming them, since the raw capture (1920x1440,
                // confirmed via ARKit's own videoFormat log) is a much boxier
                // aspect than any iPhone's actual elongated screen - whichever
                // one Camera.aspect is actually tracking at runtime decides
                // whether this fix's remaining gap is an aspect mismatch or
                // something else.
                if (!_loggedFovDiagnosticOnce)
                {
                    _loggedFovDiagnosticOnce = true;
                    Debug.Log($"[FOVDIAG] appliedFieldOfView={_arCamera.fieldOfView:F2} cameraAspect={_arCamera.aspect:F4} " +
                               $"Screen=({Screen.width}x{Screen.height}) rawWidth={rawWidth} rawHeight={rawHeight} rawFx={rawFx:F2} rawFy={rawFy:F2}");
                }
            }

            var capture = new CaptureContext
            {
                HasCameraPose = true,
                CameraPosition = cameraPositionAtCapture,
                CameraRotation = cameraRotationAtCapture,
                HasIntrinsics = hasIntrinsics,
                RawFx = rawFx,
                RawFy = rawFy,
                RawCx = rawCx,
                RawCy = rawCy,
                RawWidth = rawWidth,
                RawHeight = rawHeight,
                IntrinsicsResolutionMismatch = resolutionMismatch,
                IntrinsicsReportedResolution = hasIntrinsics ? intrinsics.resolution : Vector2Int.zero,
            };

            _ = RunInferenceAsync(_frameTexture, width, height, capture);
        }

        /// <summary>Everything about the source frame that OnDoorGeometry's consumers need but the inference pipeline itself doesn't - captured once, synchronously, and threaded through unchanged.</summary>
        private struct CaptureContext
        {
            public bool HasCameraPose;
            public Vector3 CameraPosition;
            public Quaternion CameraRotation;
            public bool HasIntrinsics;
            public float RawFx, RawFy, RawCx, RawCy;
            public int RawWidth, RawHeight;
            public bool IntrinsicsResolutionMismatch;
            public Vector2Int IntrinsicsReportedResolution;
        }

        /// <summary>
        /// Vertical FOV (Unity's Camera.fieldOfView convention) implied by a
        /// sensor-width axis of <paramref name="sensorWidthPx"/> pixels and a
        /// focal length of <paramref name="focalLengthPx"/> pixels along that
        /// same axis. Pure math, no Unity camera/AR dependency, so it can be
        /// verified synthetically - see OnCameraFrameReceived for why the
        /// sensor's WIDTH axis (not height) maps to Unity's portrait-screen
        /// VERTICAL fieldOfView after the 90-degree frame rotation.
        /// </summary>
        public static float ComputeVerticalFieldOfViewDegrees(float sensorWidthPx, float focalLengthPx)
        {
            return 2f * Mathf.Atan(sensorWidthPx / (2f * focalLengthPx)) * Mathf.Rad2Deg;
        }

        private static byte[] RotateRgba90Clockwise(byte[] src, int width, int height, out int newWidth, out int newHeight)
        {
            newWidth = height;
            newHeight = width;
            var dst = new byte[src.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = (y * width + x) * 4;
                    int dstX = height - 1 - y;
                    int dstY = x;
                    int dstIndex = (dstY * newWidth + dstX) * 4;

                    dst[dstIndex] = src[srcIndex];
                    dst[dstIndex + 1] = src[srcIndex + 1];
                    dst[dstIndex + 2] = src[srcIndex + 2];
                    dst[dstIndex + 3] = src[srcIndex + 3];
                }
            }
            return dst;
        }

        // ─── Inference ────────────────────────────────────────────

        private async System.Threading.Tasks.Task RunInferenceAsync(Texture2D texture, int srcWidth, int srcHeight, CaptureContext capture)
        {
            _isInferring = true;
            Tensor<float> inputTensor = null;
            try
            {
                inputTensor = new Tensor<float>(new TensorShape(1, 3, InputSize, InputSize));
                TextureConverter.ToTensor(texture, inputTensor);

                _worker.Schedule(inputTensor);

                var detOutput = _worker.PeekOutput(0) as Tensor<float>;
                var protoOutput = _worker.PeekOutput(1) as Tensor<float>;
                using var detCpu = await detOutput.ReadbackAndCloneAsync();
                using var protoCpu = await protoOutput.ReadbackAndCloneAsync();

                var result = PostProcess(detCpu, protoCpu, srcWidth, srcHeight, capture);
                if (result.HasValue)
                    OnDoorGeometry?.Invoke(result.Value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualDoorSegmenter] Inference failed: {ex.Message}");
            }
            finally
            {
                inputTensor?.Dispose();
                _isInferring = false;
            }
        }

        // ─── Post-processing: decode detections, pick best door, build its mask, extract geometry ──

        private struct Candidate
        {
            public DetectionClass Class;
            public float Confidence;
            public float Cx, Cy, W, H; // 640-space
            public float[] MaskCoeffs;
        }

        private DoorResult? PostProcess(Tensor<float> det, Tensor<float> proto, int srcWidth, int srcHeight, CaptureContext capture)
        {
            var detData = det.DownloadToArray(); // (38, 8400) flattened, channel-major
            var candidates = new System.Collections.Generic.List<Candidate>();

            for (int a = 0; a < NumAnchors; a++)
            {
                float bestScore = 0f;
                int bestClass = -1;
                for (int c = 0; c < NumClasses; c++)
                {
                    float score = detData[(4 + c) * NumAnchors + a];
                    if (score > bestScore) { bestScore = score; bestClass = c; }
                }
                if (bestScore < _confidenceThreshold) continue;

                var coeffs = new float[NumMaskCoeffs];
                for (int c = 0; c < NumMaskCoeffs; c++)
                    coeffs[c] = detData[(4 + NumClasses + c) * NumAnchors + a];

                candidates.Add(new Candidate
                {
                    Class = (DetectionClass)bestClass,
                    Confidence = bestScore,
                    Cx = detData[0 * NumAnchors + a],
                    Cy = detData[1 * NumAnchors + a],
                    W = detData[2 * NumAnchors + a],
                    H = detData[3 * NumAnchors + a],
                    MaskCoeffs = coeffs,
                });
            }

            var kept = NonMaxSuppression(candidates, _iouThreshold);

            Candidate? bestDoor = null;
            Candidate? bestHandle = null;
            foreach (var c in kept)
            {
                if (c.Class == DetectionClass.Door && (bestDoor == null || c.Confidence > bestDoor.Value.Confidence))
                    bestDoor = c;
                if (c.Class == DetectionClass.Handle && (bestHandle == null || c.Confidence > bestHandle.Value.Confidence))
                    bestHandle = c;
            }

            if (bestDoor == null) return null;

            // The mask has to be resized to the SOURCE FRAME'S aspect ratio,
            // not just upscaled square-to-square - verified against real
            // model output: resolution alone (160 through 1280, square)
            // consistently gave 7-9 vertices instead of 4, because the
            // square 640x640 model input squashes a non-square camera frame
            // (e.g. portrait phone dimensions), and that squash is still
            // baked into a square output mask. Only resizing to match the
            // TRUE width:height ratio (mirroring Ultralytics' own
            // letterbox-correction when mapping back to original image
            // coordinates) gave a clean 4-vertex quad - confirmed down to a
            // tiny 95x160 at the correct ratio, so this is about aspect
            // ratio, not pixel count.
            int targetMaskHeight = ProtoSize * 4;
            int targetMaskWidth = Mathf.Max(1, Mathf.RoundToInt(targetMaskHeight * (float)srcWidth / srcHeight));

            var probs = BuildMaskProbabilities(bestDoor.Value, proto);
            byte[] mask = UpsampleAndThreshold(probs, ProtoSize, targetMaskWidth, targetMaskHeight, _maskThreshold);

            Vector2? handleCenterUpsampled = null;
            if (bestHandle != null)
            {
                float scaleX = (float)targetMaskWidth / InputSize;
                float scaleY = (float)targetMaskHeight / InputSize;
                handleCenterUpsampled = new Vector2(bestHandle.Value.Cx * scaleX, bestHandle.Value.Cy * scaleY);
            }

            var geo = DoorGeometryExtractor.Extract(mask, targetMaskWidth, targetMaskHeight, handleCenterUpsampled);

            return new DoorResult
            {
                Ok = geo.Ok,
                FailReason = geo.FailReason,
                Confidence = bestDoor.Value.Confidence,
                WidthPx = geo.WidthPx,
                HeightPx = geo.HeightPx,
                IsLeftHandled = geo.IsLeftHandled,
                HandleFound = bestHandle != null,
                HandleNormalized = geo.HandleNormalized,
                TopLeftPx = geo.TopLeft,
                TopRightPx = geo.TopRight,
                BottomRightPx = geo.BottomRight,
                BottomLeftPx = geo.BottomLeft,
                MaskWidth = targetMaskWidth,
                MaskHeight = targetMaskHeight,
                HasCameraPose = capture.HasCameraPose,
                CameraPositionAtCapture = capture.CameraPosition,
                CameraRotationAtCapture = capture.CameraRotation,
                HasIntrinsics = capture.HasIntrinsics,
                RawFx = capture.RawFx,
                RawFy = capture.RawFy,
                RawCx = capture.RawCx,
                RawCy = capture.RawCy,
                RawWidth = capture.RawWidth,
                RawHeight = capture.RawHeight,
                IntrinsicsResolutionMismatch = capture.IntrinsicsResolutionMismatch,
                IntrinsicsReportedResolution = capture.IntrinsicsReportedResolution,
            };
        }

        /// <summary>
        /// prob = sigmoid(coeffs . prototypes), cropped to the detection's
        /// own box (scaled into prototype space) - mirrors Ultralytics' own
        /// postprocessing, which crops for the same reason: the raw linear
        /// combination isn't bounded to the box and can show faint
        /// activation outside it. Returns the float probability map, NOT
        /// yet thresholded - thresholding has to happen after upsampling,
        /// not before (see BuildUpsampledBinaryMask).
        /// </summary>
        private float[] BuildMaskProbabilities(Candidate det, Tensor<float> proto)
        {
            var protoData = proto.DownloadToArray(); // (32, 160, 160) flattened, channel-major
            var probs = new float[ProtoSize * ProtoSize];

            float scale = (float)ProtoSize / InputSize;
            float boxX0 = (det.Cx - det.W / 2f) * scale;
            float boxY0 = (det.Cy - det.H / 2f) * scale;
            float boxX1 = (det.Cx + det.W / 2f) * scale;
            float boxY1 = (det.Cy + det.H / 2f) * scale;

            for (int y = 0; y < ProtoSize; y++)
            {
                bool inBoxY = y >= boxY0 && y <= boxY1;
                for (int x = 0; x < ProtoSize; x++)
                {
                    if (!inBoxY || x < boxX0 || x > boxX1) continue;

                    float logit = 0f;
                    int pixelIndex = y * ProtoSize + x;
                    for (int c = 0; c < NumMaskCoeffs; c++)
                        logit += det.MaskCoeffs[c] * protoData[c * ProtoSize * ProtoSize + pixelIndex];

                    probs[pixelIndex] = 1f / (1f + Mathf.Exp(-logit));
                }
            }
            return probs;
        }

        /// <summary>
        /// Bilinear-upsamples the probability map, THEN thresholds -
        /// anti-aliases the edge before it becomes a hard boundary, instead
        /// of nearest-neighbor-scaling an already-binary staircase (which
        /// leaves the same jaggedness relative to the object, verified:
        /// upsampling the binary mask 4x nearest-neighbor still failed to
        /// simplify to 4 points - only smoothing the underlying probability
        /// field before the threshold fixed it). Mirrors what Ultralytics
        /// does internally (bilinear interpolate, then threshold).
        /// </summary>
        private static byte[] UpsampleAndThreshold(float[] probs, int srcSize, int dstWidth, int dstHeight, float threshold)
        {
            var dst = new byte[dstWidth * dstHeight];
            float scaleX = (float)srcSize / dstWidth;
            float scaleY = (float)srcSize / dstHeight;

            for (int y = 0; y < dstHeight; y++)
            {
                float sy = (y + 0.5f) * scaleY - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, srcSize - 1);
                int y1 = Mathf.Clamp(y0 + 1, 0, srcSize - 1);
                float fy = Mathf.Clamp01(sy - y0);

                for (int x = 0; x < dstWidth; x++)
                {
                    float sx = (x + 0.5f) * scaleX - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, srcSize - 1);
                    int x1 = Mathf.Clamp(x0 + 1, 0, srcSize - 1);
                    float fx = Mathf.Clamp01(sx - x0);

                    float p00 = probs[y0 * srcSize + x0];
                    float p10 = probs[y0 * srcSize + x1];
                    float p01 = probs[y1 * srcSize + x0];
                    float p11 = probs[y1 * srcSize + x1];
                    float top = Mathf.Lerp(p00, p10, fx);
                    float bottom = Mathf.Lerp(p01, p11, fx);
                    float p = Mathf.Lerp(top, bottom, fy);

                    dst[y * dstWidth + x] = p >= threshold ? (byte)1 : (byte)0;
                }
            }
            return dst;
        }

        private static System.Collections.Generic.List<Candidate> NonMaxSuppression(
            System.Collections.Generic.List<Candidate> candidates, float iouThreshold)
        {
            candidates.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var kept = new System.Collections.Generic.List<Candidate>();

            foreach (var candidate in candidates)
            {
                bool suppressed = false;
                foreach (var k in kept)
                {
                    if (k.Class != candidate.Class) continue;
                    if (IoU(k, candidate) > iouThreshold) { suppressed = true; break; }
                }
                if (!suppressed) kept.Add(candidate);
            }
            return kept;
        }

        private static float IoU(Candidate a, Candidate b)
        {
            float ax0 = a.Cx - a.W / 2f, ay0 = a.Cy - a.H / 2f, ax1 = a.Cx + a.W / 2f, ay1 = a.Cy + a.H / 2f;
            float bx0 = b.Cx - b.W / 2f, by0 = b.Cy - b.H / 2f, bx1 = b.Cx + b.W / 2f, by1 = b.Cy + b.H / 2f;

            float x1 = Mathf.Max(ax0, bx0), y1 = Mathf.Max(ay0, by0);
            float x2 = Mathf.Min(ax1, bx1), y2 = Mathf.Min(ay1, by1);

            float interArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
            float unionArea = a.W * a.H + b.W * b.H - interArea;
            return unionArea <= 0f ? 0f : interArea / unionArea;
        }
    }
}
