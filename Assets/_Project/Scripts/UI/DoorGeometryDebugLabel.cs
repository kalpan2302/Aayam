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

            // Raw corner coordinates as fractions of mask width/height,
            // added 2026-10-21: the cyan marker has shown a small, very
            // consistent off-screen offset (~2x door width) across 8+
            // on-device samples, and two shaped hypotheses (object-point
            // mirror, camera-local X sign flip) have both been tested and
            // ruled out or are pending. Rather than guess a third shape,
            // this lets the corners be checked directly against the photo -
            // e.g. does "TL" actually land near the door's visual top-left,
            // or does it land somewhere else, which would point straight at
            // DoorGeometryExtractor/OrderCorners instead of the PnP math.
            float mw = result.MaskWidth, mh = result.MaskHeight;
            string corners = $"TL=({result.TopLeftPx.x / mw:F2},{result.TopLeftPx.y / mh:F2}) " +
                              $"TR=({result.TopRightPx.x / mw:F2},{result.TopRightPx.y / mh:F2})\n" +
                              $"BR=({result.BottomRightPx.x / mw:F2},{result.BottomRightPx.y / mh:F2}) " +
                              $"BL=({result.BottomLeftPx.x / mw:F2},{result.BottomLeftPx.y / mh:F2})";

            _label.text = $"Geo: {result.WidthPx:F0}x{result.HeightPx:F0}px handle={side} conf={result.Confidence:F2}\n{corners}";

            // 2026-10-24: same gap as DoorPnPWorldAnchor's viewport diagnostic
            // had - this only ever reached the on-screen label, never the
            // console, so a console dump could show the off-screen viewport
            // numbers but never the raw corners needed to check them against
            // DoorGeometryExtractor/OrderCorners (the next lead once the
            // aspect-mismatch fix was confirmed insufficient).
            Debug.Log($"[DoorGeometryDebugLabel] {corners.Replace("\n", " ")}");
        }
    }
}
