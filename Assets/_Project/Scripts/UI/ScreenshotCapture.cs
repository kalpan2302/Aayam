using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Aayam.Utilities;

namespace Aayam.UI
{
    /// <summary>
    /// Captures the full AR screen (camera + virtual objects) and saves to device gallery.
    ///
    /// On device: requires NativeGallery (com.yasirkula.nativegallery) for iOS/Android gallery access.
    /// In editor: saves PNG to Assets/ folder.
    /// Includes white-flash visual effect and optional shutter sound.
    /// </summary>
    public class ScreenshotCapture : MonoBehaviour
    {
        [Header("Visual FX")]
        [SerializeField] private Image      _flashPanel;        // White semi-transparent panel
        [SerializeField] private float      _flashDuration = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _shutterClip;

        [Header("Watermark")]
        [SerializeField] private bool   _addWatermark    = true;
        [SerializeField] private Sprite _watermarkSprite;
        [SerializeField] private Vector2Int _watermarkMargin = new Vector2Int(24, 24);

        [Header("Share")]
        [Tooltip("If true, the native share sheet opens automatically right after a successful capture. " +
                 "Off by default - no existing UI button triggers it, so ShareLastScreenshot() is still " +
                 "callable explicitly once a Share button exists.")]
        [SerializeField] private bool _autoShareAfterCapture = false;

        [Header("UI to Hide During Capture")]
        [SerializeField] private CanvasGroup[] _hideForCapture;   // Hide HUD during screenshot

        private Texture2D _lastCapturedTexture;

        // ─── Public API ───────────────────────────────────────────

        public void CaptureScreenshot() => StartCoroutine(CaptureRoutine());

        /// <summary>Opens the native share sheet (NativeGallery's sibling, com.yasirkula.nativeshare) for the most recently captured screenshot. No-op if nothing has been captured yet.</summary>
        public void ShareLastScreenshot()
        {
            if (_lastCapturedTexture == null)
            {
                Debug.LogWarning("[Screenshot] ShareLastScreenshot called with no captured screenshot yet.");
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            new NativeShare()
                .AddFile(_lastCapturedTexture, "Aayam_AR_Share.png")
                .SetSubject("My Aayam AR Preview")
                .SetText("Check out this hardware preview from Aayam AR!")
                .Share();
#else
            Debug.Log("[Screenshot] Native share sheet not supported on this platform (Editor/Standalone).");
#endif
        }

        // ─── Coroutine ────────────────────────────────────────────

        private IEnumerator CaptureRoutine()
        {
            // 1. Hide UI panels
            SetHiddenPanelsVisible(false);

            // 2. Wait for end of frame (so AR camera has rendered with the
            // UI hidden, and crucially BEFORE the flash below - it used to
            // show the flash first then capture one frame later, baking
            // the still-near-full-opacity white flash into every
            // screenshot (confirmed live on-device: every captured image
            // came out uniformly washed out). The flash is user-facing
            // feedback that a capture just happened, not part of what
            // gets captured, so it must come AFTER ReadPixels now.
            yield return new WaitForEndOfFrame();

            // 3. Capture
            var rawTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            rawTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            rawTex.Apply();

            // 3b. Watermark (previously declared fields, never actually used)
            Texture2D tex = rawTex;
            if (_addWatermark && _watermarkSprite != null)
            {
                tex = TextureCompositor.CompositeAt(rawTex, _watermarkSprite.texture, _watermarkMargin);
                if (tex != rawTex) Destroy(rawTex);
            }

            // 4. White flash + shutter sound (purely user feedback now - after the capture)
            if (_flashPanel != null)
            {
                _flashPanel.gameObject.SetActive(true);
                _flashPanel.color = new Color(1f, 1f, 1f, 0.9f);
            }
            if (_audioSource != null && _shutterClip != null)
                _audioSource.PlayOneShot(_shutterClip);

            // 6. Fade out flash
            if (_flashPanel != null)
            {
                float elapsed = 0f;
                while (elapsed < _flashDuration)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.9f, 0f, elapsed / _flashDuration);
                    _flashPanel.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }
                _flashPanel.gameObject.SetActive(false);
            }

            // 7. Restore UI
            SetHiddenPanelsVisible(true);

            // 8. Save - keeps tex alive as _lastCapturedTexture for ShareLastScreenshot()
            // (previous capture's texture, if any, is disposed first).
            if (_lastCapturedTexture != null && _lastCapturedTexture != tex) Destroy(_lastCapturedTexture);
            _lastCapturedTexture = tex;
            SaveToGallery(tex);

            if (_autoShareAfterCapture) ShareLastScreenshot();
        }

        // ─── Save ─────────────────────────────────────────────────

        private void SaveToGallery(Texture2D tex)
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName  = $"Aayam_AR_{timestamp}.png";

#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[Screenshot] Saved to {path}");

#elif UNITY_ANDROID || UNITY_IOS
            NativeGallery.SaveImageToGallery(tex, "Aayam AR", fileName, (success, path) =>
            {
                Debug.Log($"[Screenshot] Gallery save {(success ? "OK" : "FAILED")}: {path}");
            });
#else
            Debug.Log("[Screenshot] Platform not supported for gallery save.");
#endif
        }

        private void OnDestroy()
        {
            if (_lastCapturedTexture != null) Destroy(_lastCapturedTexture);
        }

        // ─── Helpers ──────────────────────────────────────────────

        private void SetHiddenPanelsVisible(bool visible)
        {
            foreach (var cg in _hideForCapture)
                if (cg != null) cg.alpha = visible ? 1f : 0f;
        }
    }
}
