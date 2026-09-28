using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.XR.ARFoundation;

namespace Aayam.UI
{
    /// <summary>
    /// In-app debug panel showing FPS, memory, screen resolution, and AR state.
    /// Visible only in Editor and Development Builds.
    /// Toggle with the assigned button or shake gesture.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject          _panelRoot;
        [SerializeField] private Text       _debugText;

        [Header("AR References")]
        [SerializeField] private ARSession           _arSession;
        [SerializeField] private ARPlaneManager      _planeManager;

        [Header("Settings")]
        [SerializeField] private float _updateInterval   = 0.2f;
        [SerializeField] private bool  _startVisible     = false;

        // â”€â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private float       _deltaTime;
        private float       _nextUpdate;
        private readonly StringBuilder _sb = new StringBuilder(512);

        // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Completely hide in release builds
            _panelRoot?.SetActive(false);
            enabled = false;
            return;
#endif
            _panelRoot?.SetActive(_startVisible);
        }

        private void Update()
        {
            // FPS running average
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + _updateInterval;

            if (_panelRoot != null && _panelRoot.activeSelf)
                RefreshText();
        }

        // â”€â”€â”€ Display â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void RefreshText()
        {
            float fps = 1f / Mathf.Max(_deltaTime, 0.0001f);
            string fpsColor = fps >= 55f ? "green" : fps >= 30f ? "yellow" : "red";

            _sb.Clear();
            _sb.AppendLine("<b>â”€ DEBUG PANEL â”€</b>");

            // Performance
            _sb.AppendLine($"FPS: <color={fpsColor}><b>{fps:F0}</b></color>");
            _sb.AppendLine($"Î”t: {Time.unscaledDeltaTime * 1000f:F1}ms");

            // Device
            _sb.AppendLine($"Screen: {Screen.width}Ã—{Screen.height}");
            _sb.AppendLine($"DPI: {Screen.dpi:F0}");
            _sb.AppendLine($"RAM: {SystemInfo.systemMemorySize}MB");
            _sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            _sb.AppendLine($"VRAM: {SystemInfo.graphicsMemorySize}MB");

            // AR Session
            if (_arSession != null)
            {
                _sb.AppendLine();
                _sb.AppendLine("<b>â”€â”€ AR â”€â”€</b>");
                _sb.AppendLine($"State: {ARSession.state}");
            }

            // Planes
            if (_planeManager != null)
            {
                int total    = 0;
                int vertical = 0;
                foreach (var p in _planeManager.trackables)
                {
                    total++;
                    if (Mathf.Abs(Vector3.Dot(p.normal, Vector3.up)) < 0.2f)
                        vertical++;
                }
                _sb.AppendLine($"Planes: {total} total | {vertical} vertical");
            }

            // Memory
            _sb.AppendLine();
            _sb.AppendLine($"<color=grey>Time: {Time.time:F0}s</color>");

            if (_debugText != null)
                _debugText.text = _sb.ToString();
        }

        // â”€â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void TogglePanel()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(!_panelRoot.activeSelf);
        }

        public void Show() => _panelRoot?.SetActive(true);
        public void Hide() => _panelRoot?.SetActive(false);
    }
}
