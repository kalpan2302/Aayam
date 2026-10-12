using UnityEngine;
using UnityEngine.UI;
using Aayam.AR.Planes;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 3 Step 8: on-screen readout of VisualDoorSegmenter's geometry
    /// extraction (door dimensions, handle side), same purpose and same
    /// Canvas-rooted placement as VisualDetectionDebugLabel for Step 7 -
    /// ScreenManager.Start() deactivates every screen except the initial
    /// one, so anything parented under a specific screen is invisible until
    /// the user navigates there (confirmed the hard way in Step 7).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class DoorGeometryDebugLabel : MonoBehaviour
    {
        [SerializeField] private VisualDoorSegmenter _segmenter;

        private Text _label;

        private void Awake()
        {
            _label = GetComponent<Text>();
        }

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

        private void HandleGeometry(VisualDoorSegmenter.DoorResult result)
        {
            if (_label == null) return;

            if (!result.Ok)
            {
                _label.text = $"Geo: {result.FailReason}";
                return;
            }

            string side = result.HandleFound ? (result.IsLeftHandled ? "left" : "right") : "none";
            _label.text = $"Geo: {result.WidthPx:F0}x{result.HeightPx:F0}px handle={side} conf={result.Confidence:F2}";
        }
    }
}
