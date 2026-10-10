using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using ZXing.Common;
using Aayam.Models;

namespace Aayam.UI
{
    /// <summary>
    /// Scans camera frames for a QR code encoding an "aayam://product/{sku}"
    /// deep link (see backend GET /api/v1/assets/{id}/qr), then hands the
    /// decoded SKU to ProductSelector.SelectProductBySku - the same
    /// fetch/download/place path a catalogue-card tap uses.
    ///
    /// Must be attached to the SAME GameObject as the real ARCameraManager
    /// (the AR camera, part of XR Origin) to get actual camera frames - which
    /// means that GameObject has to stay always-active for the AR session to
    /// function, so scanning can't be gated via OnEnable/OnDisable the way a
    /// dedicated screen-root object would be. Instead it tracks
    /// ScreenManager.OnScreenChanged and only does decode work while
    /// AppScreen.QRScanner is the current screen.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class QRScanner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProductSelector _productSelector;

        [Header("Scanning")]
        [Tooltip("Attempt a decode every N camera frames - QR decoding is real " +
                 "work; no need to try on every single frame at 30-60fps.")]
        [SerializeField] private int _decodeEveryNFrames = 8;

        // ─── Events ───────────────────────────────────────────────
        /// <summary>Fired with the decoded SKU, right before placement starts.</summary>
        public event Action<string> OnProductScanned;

        // ─── State ────────────────────────────────────────────────
        private ARCameraManager _cameraManager;
        private int  _frameCounter;
        private bool _isDecoding;
        private bool _isScreenActive;

        private readonly BarcodeReaderGeneric _reader = new()
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                TryHarder = false,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            },
        };

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
            if (_productSelector == null)
                _productSelector = FindAnyObjectByType<ProductSelector>();
            // Starts false; ScreenManager.OnScreenChanged (subscribed in OnEnable)
            // corrects this as soon as the user actually navigates somewhere -
            // fine since the initial screen is never QRScanner in practice.
        }

        private void OnEnable()
        {
            _frameCounter = 0;
            if (_cameraManager != null)
                _cameraManager.frameReceived += OnCameraFrameReceived;
            ScreenManager.OnScreenChanged += HandleScreenChanged;
        }

        private void OnDisable()
        {
            if (_cameraManager != null)
                _cameraManager.frameReceived -= OnCameraFrameReceived;
            ScreenManager.OnScreenChanged -= HandleScreenChanged;
        }

        private void HandleScreenChanged(AppScreen from, AppScreen to)
        {
            _isScreenActive = to == AppScreen.QRScanner;
        }

        // ─── Frame Callback ───────────────────────────────────────

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!_isScreenActive) return;
            if (_isDecoding) return;
            if (++_frameCounter < _decodeEveryNFrames) return;
            _frameCounter = 0;

            if (!_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            byte[] rgba;
            int width, height;
            using (image)
            {
                width = image.width;
                height = image.height;
                rgba = new byte[width * height * 4];

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

            _ = DecodeAsync(rgba, width, height);
        }

        // ─── Decode (off main thread) ──────────────────────────────

        private async Task DecodeAsync(byte[] rgba, int width, int height)
        {
            _isDecoding = true;
            try
            {
                Result result = await Task.Run(() =>
                {
                    var source = new RGBLuminanceSource(rgba, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
                    return _reader.Decode(source);
                });

                if (result != null && DeepLinkParser.TryParseProductId(result.Text, out string sku))
                    HandleScanned(sku);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QRScanner] Decode failed: {ex.Message}");
            }
            finally
            {
                _isDecoding = false;
            }
        }

        private void HandleScanned(string sku)
        {
            Debug.Log($"[QRScanner] Decoded product: {sku}");
            OnProductScanned?.Invoke(sku);

            if (_productSelector != null)
                _productSelector.SelectProductBySku(sku);
            else
                Debug.LogError("[QRScanner] No ProductSelector assigned/found - cannot start placement.");
        }
    }
}
