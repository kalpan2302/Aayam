using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("UI to Hide During Capture")]
        [SerializeField] private CanvasGroup[] _hideForCapture;   // Hide HUD during screenshot

        // ─── Public API ───────────────────────────────────────────

        public void CaptureScreenshot() => StartCoroutine(CaptureRoutine());

        // ─── Coroutine ────────────────────────────────────────────

        private IEnumerator CaptureRoutine()
        {
            // 1. Hide UI panels
            SetHiddenPanelsVisible(false);

            // 2. White flash start
            if (_flashPanel != null)
            {
                _flashPanel.gameObject.SetActive(true);
                _flashPanel.color = new Color(1f, 1f, 1f, 0.9f);
            }

            // 3. Play shutter sound
            if (_audioSource != null && _shutterClip != null)
                _audioSource.PlayOneShot(_shutterClip);

            // 4. Wait for end of frame (so AR camera has rendered)
            yield return new WaitForEndOfFrame();

            // 5. Capture
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();

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

            // 8. Save
            SaveToGallery(tex);
            Destroy(tex);
        }

        // ─── Save ─────────────────────────────────────────────────

        private void SaveToGallery(Texture2D tex)
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName  = $"Aayam_AR_{timestamp}.png";
            byte[] pngBytes  = tex.EncodeToPNG();

#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, fileName);
            File.WriteAllBytes(path, pngBytes);
            Debug.Log($"[Screenshot] Saved to {path}");

#elif UNITY_ANDROID || UNITY_IOS
            // NativeGallery: com.yasirkula.nativegallery
            // Uncomment after installing the package:
            // NativeGallery.SaveImageToGallery(tex, "Aayam AR", fileName, (success, path) =>
            // {
            //     Debug.Log($"[Screenshot] Gallery save {(success ? "OK" : "FAILED")}: {path}");
            // });

            // Fallback: save to persistent data path
            string fallbackPath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(fallbackPath, pngBytes);
            Debug.Log($"[Screenshot] Saved to persistentData: {fallbackPath}");
#else
            Debug.Log("[Screenshot] Platform not supported for gallery save.");
#endif
        }

        // ─── Helpers ──────────────────────────────────────────────

        private void SetHiddenPanelsVisible(bool visible)
        {
            foreach (var cg in _hideForCapture)
                if (cg != null) cg.alpha = visible ? 1f : 0f;
        }
    }
}
