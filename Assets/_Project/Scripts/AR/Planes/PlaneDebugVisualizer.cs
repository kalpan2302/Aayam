using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Debug overlay that displays live AR plane detection statistics.
    /// Shows total plane counts, vertical plane list, and door candidate info.
    /// Visible only in Editor and Development builds.
    /// </summary>
    public class PlaneDebugVisualizer : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text _debugText;

        [Header("System References")]
        [SerializeField] private VerticalPlaneFilter _verticalFilter;
        [SerializeField] private DoorCandidateDetector _doorDetector;
        [SerializeField] private PlaneEventHandler _planeEventHandler;

        [Header("Settings")]
        [SerializeField] private float _updateInterval = 0.2f;  // Update 5x/sec
        [SerializeField] private bool _showPerPlaneDetails = false;

        private float _nextUpdate;
        private readonly StringBuilder _sb = new StringBuilder(512);

        // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Hide in release builds
            if (_debugText != null) _debugText.gameObject.SetActive(false);
            enabled = false;
#endif
        }

        private void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + _updateInterval;
            RefreshText();
        }

        // â”€â”€â”€ Text Refresh â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void RefreshText()
        {
            if (_debugText == null || !_debugText.gameObject.activeInHierarchy) return;

            _sb.Clear();
            _sb.AppendLine("<b>â”€â”€ AR PLANE DEBUG â”€â”€</b>");

            // Overall counts
            int total    = _planeEventHandler != null ? _planeEventHandler.TotalPlaneCount : 0;
            int vertical = _planeEventHandler != null ? _planeEventHandler.VerticalPlaneCount : 0;
            _sb.AppendLine($"Total planes: <b>{total}</b>  Vertical: <b>{vertical}</b>");

            // Door candidate
            _sb.AppendLine();
            if (_doorDetector != null && _doorDetector.BestCandidate != null)
            {
                var plane = _doorDetector.BestCandidate;
                float score = _doorDetector.BestCandidateScore;
                float w = plane.extents.x * 2f;
                float h = plane.extents.y * 2f;
                string confidence = _doorDetector.HasConfidentCandidate
                    ? "<color=#00FF00>HIGH</color>"
                    : "<color=#FFD700>MEDIUM</color>";

                _sb.AppendLine($"<color=#FFD700><b>Door Candidate</b></color>");
                _sb.AppendLine($"Score: {score:F0}/100  Confidence: {confidence}");
                _sb.AppendLine($"Size: {w * 1000:F0}mm Ã— {h * 1000:F0}mm");
                _sb.AppendLine($"ID: {plane.trackableId.ToString()[..8]}...");
            }
            else
            {
                _sb.AppendLine("<color=#FF4444>No door candidate found</color>");
                _sb.AppendLine("â†’ Point camera at a door");
            }

            // Per-plane details
            if (_showPerPlaneDetails && _verticalFilter != null)
            {
                _sb.AppendLine();
                _sb.AppendLine("<b>Vertical Planes:</b>");
                foreach (var plane in _verticalFilter.VerticalPlanes)
                {
                    float w = plane.extents.x * 2f;
                    float h = plane.extents.y * 2f;
                    _sb.AppendLine($"  {plane.trackableId.ToString()[..6]} {w:F2}Ã—{h:F2}m");
                }
            }

            _debugText.text = _sb.ToString();
        }

        // â”€â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Toggle visibility of the debug panel.</summary>
        public void Toggle()
        {
            if (_debugText != null)
                _debugText.gameObject.SetActive(!_debugText.gameObject.activeSelf);
        }
    }
}
