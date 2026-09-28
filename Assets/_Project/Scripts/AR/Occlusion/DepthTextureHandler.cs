using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Acquires the depth CPU image from AROcclusionManager each frame,
    /// converts it to a Texture2D (RFloat), and exposes per-pixel depth queries.
    ///
    /// Used by:
    ///   - DepthPlacement (depth-based object placement)
    ///   - DepthVisualizer (debug heatmap overlay)
    ///   - Future: automatic door measurement
    /// </summary>
    [RequireComponent(typeof(AROcclusionManager))]
    public class DepthTextureHandler : MonoBehaviour
    {
        // ─── State ────────────────────────────────────────────────
        private AROcclusionManager _occlusionManager;
        private Texture2D          _depthTexture;

        public Texture2D CurrentDepthTexture => _depthTexture;
        public bool      HasDepthData       => _depthTexture != null;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _occlusionManager = GetComponent<AROcclusionManager>();
        }

        private void OnEnable()
        {
            _occlusionManager.frameReceived += OnOcclusionFrame;
        }

        private void OnDisable()
        {
            _occlusionManager.frameReceived -= OnOcclusionFrame;
        }

        // ─── Frame Callback ───────────────────────────────────────

        private void OnOcclusionFrame(AROcclusionFrameEventArgs args)
        {
            if (!_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage image))
                return;

            using (image)
            {
                UpdateTexture(image);
            }
        }

        private void UpdateTexture(XRCpuImage cpuImage)
        {
            // Recreate texture if resolution changed
            if (_depthTexture == null ||
                _depthTexture.width  != cpuImage.width ||
                _depthTexture.height != cpuImage.height)
            {
                if (_depthTexture != null) Destroy(_depthTexture);
                _depthTexture = new Texture2D(cpuImage.width, cpuImage.height, TextureFormat.RFloat, mipChain: false);
                _depthTexture.filterMode = FilterMode.Bilinear;
                _depthTexture.wrapMode   = TextureWrapMode.Clamp;
            }

            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect        = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
                outputFormat     = TextureFormat.RFloat,
                transformation   = XRCpuImage.Transformation.None
            };

            // Copy directly into the texture's native buffer
            var rawData = _depthTexture.GetRawTextureData<float>();

            unsafe
            {
                cpuImage.Convert(
                    conversionParams,
                    new IntPtr(rawData.GetUnsafePtr()),
                    rawData.Length * sizeof(float)
                );
            }

            _depthTexture.Apply(updateMipmaps: false);
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Returns the depth (meters) at a screen-space pixel coordinate.
        /// Returns 0 if depth data is unavailable.
        /// </summary>
        public float GetDepthAtScreenPoint(Vector2 screenPosition)
        {
            if (_depthTexture == null) return 0f;

            float u = Mathf.Clamp01(screenPosition.x / Screen.width);
            float v = Mathf.Clamp01(screenPosition.y / Screen.height);

            int x = Mathf.Clamp(Mathf.RoundToInt(u * (_depthTexture.width  - 1)), 0, _depthTexture.width  - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (_depthTexture.height - 1)), 0, _depthTexture.height - 1);

            return _depthTexture.GetPixel(x, y).r;
        }

        /// <summary>
        /// Returns the average depth (meters) in a small region around a screen point.
        /// More robust than single-pixel sampling.
        /// </summary>
        public float GetSmoothedDepthAtScreenPoint(Vector2 screenPosition, int kernelRadius = 2)
        {
            if (_depthTexture == null) return 0f;

            float u = screenPosition.x / Screen.width;
            float v = screenPosition.y / Screen.height;

            int cx = Mathf.RoundToInt(u * (_depthTexture.width  - 1));
            int cy = Mathf.RoundToInt(v * (_depthTexture.height - 1));

            float sum   = 0f;
            int   count = 0;

            for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
            for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
            {
                int px = Mathf.Clamp(cx + dx, 0, _depthTexture.width  - 1);
                int py = Mathf.Clamp(cy + dy, 0, _depthTexture.height - 1);
                float d = _depthTexture.GetPixel(px, py).r;
                if (d > 0.05f) // Ignore invalid near-zero
                {
                    sum += d;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }
    }
}
