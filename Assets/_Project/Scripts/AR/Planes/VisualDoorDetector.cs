using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Runs the trained door+handle YOLOv8n detector (Phase 3) against camera
    /// frames via Unity's Inference Engine (formerly Sentis). Augments
    /// DoorCandidateDetector's geometric heuristic - doesn't replace it, see
    /// docs/phases/phase-3-implementation-plan.md Step 6.
    ///
    /// Must be attached to the same GameObject as the real ARCameraManager,
    /// same constraint as QRScanner - see that class for why.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class VisualDoorDetector : MonoBehaviour
    {
        private const int InputSize  = 640;
        private const int NumClasses = 2; // door, handle - matches DoorHandleDetector.onnx
        private const int NumAnchors = 8400;

        public enum DetectionClass { Door = 0, Handle = 1 }

        public struct Detection
        {
            public DetectionClass Class;
            public float Confidence;

            /// <summary>Box in source camera-image pixel space (not screen space).</summary>
            public Rect BoxPixels;
        }

        [Header("Model")]
        [SerializeField] private ModelAsset _modelAsset;

        [Header("Scanning")]
        [Tooltip("Run inference every N camera frames - matches QRScanner's throttle pattern.")]
        [SerializeField] private int _inferEveryNFrames = 8;
        [SerializeField] [Range(0f, 1f)] private float _confidenceThreshold = 0.4f;
        [SerializeField] [Range(0f, 1f)] private float _iouThreshold = 0.45f;

        /// <summary>Fired after each inference pass with the NMS-filtered detections.</summary>
        public event Action<IReadOnlyList<Detection>> OnDetections;

        private ARCameraManager _cameraManager;
        private Worker _worker;
        private Texture2D _frameTexture;
        private int _frameCounter;
        private bool _isInferring;

        // Phase 3 Step 7: on-device latency measurement (target <50ms/frame,
        // see docs/phases/phase-3-implementation-plan.md). Editor/CPU numbers
        // aren't representative - this only matters run on a real device.
        private int _latencySampleCount;
        private double _latencySumMs;
        private double _latencyMaxMs;
        private const int LatencyLogInterval = 30;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
        }

        private void OnEnable()
        {
            if (_modelAsset == null)
            {
                Debug.LogError("[VisualDoorDetector] No ModelAsset assigned - disabling.");
                enabled = false;
                return;
            }

            var model = ModelLoader.Load(_modelAsset);
            _worker = new Worker(model, BackendType.GPUCompute);
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

        // ─── Frame Callback ───────────────────────────────────────

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_isInferring) return;
            if (++_frameCounter < _inferEveryNFrames) return;
            _frameCounter = 0;

            if (!_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            int width, height;
            byte[] rgba;
            using (image)
            {
                width  = image.width;
                height = image.height;
                rgba   = new byte[width * height * 4];

                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect        = new RectInt(0, 0, width, height),
                    outputDimensions = new Vector2Int(width, height),
                    outputFormat     = TextureFormat.RGBA32,
                    transformation   = XRCpuImage.Transformation.None,
                };

                unsafe
                {
                    fixed (byte* ptr = rgba)
                    {
                        image.Convert(conversionParams, new IntPtr(ptr), rgba.Length);
                    }
                }
            }

            // XRCpuImage.Convert with Transformation.None hands back the raw
            // camera-sensor buffer, not rotated to match the portrait screen -
            // AR Foundation never corrects this for you on CPU-image reads
            // (unlike the display shader, which does). Confirmed via offline
            // test 2026-10-11: the exact photo this pipeline sees as "no
            // detection" detects fine (door 0.58, handle 0.57) through the
            // same ONNX model once rotated back to upright - and a 90°
            // rotation of an otherwise-good photo, either direction, reliably
            // drops the model to zero detections. Without this correction the
            // model was never seeing an upright door at all.
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

            _ = RunInferenceAsync(_frameTexture, width, height);
        }

        /// <summary>Rotates a tightly-packed RGBA32 buffer 90° clockwise.</summary>
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

                    dst[dstIndex]     = src[srcIndex];
                    dst[dstIndex + 1] = src[srcIndex + 1];
                    dst[dstIndex + 2] = src[srcIndex + 2];
                    dst[dstIndex + 3] = src[srcIndex + 3];
                }
            }

            return dst;
        }

        // ─── Inference ────────────────────────────────────────────

        private async Task RunInferenceAsync(Texture2D texture, int srcWidth, int srcHeight)
        {
            _isInferring = true;
            Tensor<float> inputTensor = null;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // TextureConverter resamples to the tensor's declared size
                // automatically, so the source texture doesn't need pre-resizing.
                inputTensor = new Tensor<float>(new TensorShape(1, 3, InputSize, InputSize));
                TextureConverter.ToTensor(texture, inputTensor);

                _worker.Schedule(inputTensor);
                var outputTensor = _worker.PeekOutput() as Tensor<float>;
                using var cpuTensor = await outputTensor.ReadbackAndCloneAsync();

                var detections = PostProcess(cpuTensor, srcWidth, srcHeight);
                OnDetections?.Invoke(detections);

                RecordLatencySample(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualDoorDetector] Inference failed: {ex.Message}");
            }
            finally
            {
                inputTensor?.Dispose();
                _isInferring = false;
            }
        }

        private void RecordLatencySample(double elapsedMs)
        {
            _latencySampleCount++;
            _latencySumMs += elapsedMs;
            if (elapsedMs > _latencyMaxMs) _latencyMaxMs = elapsedMs;

            if (_latencySampleCount >= LatencyLogInterval)
            {
                double avgMs = _latencySumMs / _latencySampleCount;
                Debug.Log($"[VisualDoorDetector] Step7 latency over {_latencySampleCount} frames: " +
                          $"avg={avgMs:F1}ms max={_latencyMaxMs:F1}ms (target <50ms)");
                _latencySampleCount = 0;
                _latencySumMs = 0;
                _latencyMaxMs = 0;
            }
        }

        // ─── Post-processing (NMS + confidence threshold) ────────────

        /// <summary>
        /// Output tensor is (1, 6, 8400): channels = [cx, cy, w, h, scoreDoor,
        /// scoreHandle] in 640-space, channel-major layout - the standard
        /// Ultralytics YOLOv8 ONNX export format (no built-in NMS).
        /// </summary>
        private List<Detection> PostProcess(Tensor<float> output, int srcWidth, int srcHeight)
        {
            var data = output.DownloadToArray();
            var candidates = new List<Detection>();

            float scaleX = srcWidth  / (float)InputSize;
            float scaleY = srcHeight / (float)InputSize;

            for (int a = 0; a < NumAnchors; a++)
            {
                float bestScore = 0f;
                int bestClass = -1;
                for (int c = 0; c < NumClasses; c++)
                {
                    float score = data[(4 + c) * NumAnchors + a];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }

                if (bestScore < _confidenceThreshold) continue;

                float cx = data[0 * NumAnchors + a];
                float cy = data[1 * NumAnchors + a];
                float w  = data[2 * NumAnchors + a];
                float h  = data[3 * NumAnchors + a];

                var box = new Rect(
                    (cx - w / 2f) * scaleX,
                    (cy - h / 2f) * scaleY,
                    w * scaleX,
                    h * scaleY);

                candidates.Add(new Detection
                {
                    Class      = (DetectionClass)bestClass,
                    Confidence = bestScore,
                    BoxPixels  = box,
                });
            }

            return NonMaxSuppression(candidates, _iouThreshold);
        }

        private static List<Detection> NonMaxSuppression(List<Detection> candidates, float iouThreshold)
        {
            candidates.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var kept = new List<Detection>();

            foreach (var candidate in candidates)
            {
                bool suppressed = false;
                foreach (var keptDetection in kept)
                {
                    if (keptDetection.Class != candidate.Class) continue;
                    if (IoU(keptDetection.BoxPixels, candidate.BoxPixels) > iouThreshold)
                    {
                        suppressed = true;
                        break;
                    }
                }
                if (!suppressed) kept.Add(candidate);
            }

            return kept;
        }

        private static float IoU(Rect a, Rect b)
        {
            float x1 = Mathf.Max(a.xMin, b.xMin);
            float y1 = Mathf.Max(a.yMin, b.yMin);
            float x2 = Mathf.Min(a.xMax, b.xMax);
            float y2 = Mathf.Min(a.yMax, b.yMax);

            float interArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
            float unionArea = a.width * a.height + b.width * b.height - interArea;
            return unionArea <= 0f ? 0f : interArea / unionArea;
        }
    }
}
