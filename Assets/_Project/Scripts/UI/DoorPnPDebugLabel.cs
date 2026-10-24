using UnityEngine;
using UnityEngine.UI;
using Aayam.AR.Planes;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 4 Step 1, Option B: on-screen readout of DoorPnPWorldAnchor's
    /// recovered world-space door origin - same Canvas-rooted placement
    /// reasoning as VisualDetectionDebugLabel/DoorGeometryDebugLabel
    /// (ScreenManager.Start() deactivates every screen but the initial one).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class DoorPnPDebugLabel : MonoBehaviour
    {
        [SerializeField] private DoorPnPWorldAnchor _anchor;

        private Text _label;

        private void Awake()
        {
            _label = GetComponent<Text>();
        }

        private void OnEnable()
        {
            if (_anchor != null)
                _anchor.OnWorldPoseUpdated += HandlePoseUpdated;
        }

        private void OnDisable()
        {
            if (_anchor != null)
                _anchor.OnWorldPoseUpdated -= HandlePoseUpdated;
        }

        private void HandlePoseUpdated(Vector3 worldPos, Quaternion worldRot, float distFromCamera, bool inFrontOfCamera, string intrinsicsDiag, string viewportDiag)
        {
            if (_label == null) return;
            _label.text = $"PnP: pos=({worldPos.x:F2},{worldPos.y:F2},{worldPos.z:F2}) rot={worldRot.eulerAngles.y:F0}deg\n" +
                           $"dist={distFromCamera:F2}m {(inFrontOfCamera ? "FRONT" : "BEHIND CAMERA")}\n" +
                           intrinsicsDiag + "\n" +
                           viewportDiag;
        }
    }
}
