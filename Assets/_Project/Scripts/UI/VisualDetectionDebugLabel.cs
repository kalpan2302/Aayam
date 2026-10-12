using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Aayam.AR.Planes;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 3 Step 7: on-screen readout of what VisualDoorDetector actually
    /// sees, updated every inference pass. Exists because there was previously
    /// no way to check detection accuracy on-device without a USB/Xcode
    /// console tether - only latency got logged.
    ///
    /// Deliberately screen-space text, not a world- or camera-image-space box
    /// overlay: DoorConfidenceCoordinator's own doc comment explains why that
    /// spatial mapping (camera-image pixel space vs Unity screen space) is a
    /// real risk, not just extra work - it's the same bug class that cost
    /// time in Phase 2's QR scanner debugging. This label sidesteps it
    /// entirely while still answering the question that matters for Step 7:
    /// is the model seeing the door or not.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class VisualDetectionDebugLabel : MonoBehaviour
    {
        [SerializeField] private VisualDoorDetector _visualDetector;

        private Text _label;

        private void Awake()
        {
            _label = GetComponent<Text>();
        }

        private void OnEnable()
        {
            if (_visualDetector != null)
                _visualDetector.OnDetections += HandleDetections;
        }

        private void OnDisable()
        {
            if (_visualDetector != null)
                _visualDetector.OnDetections -= HandleDetections;
        }

        private void HandleDetections(IReadOnlyList<VisualDoorDetector.Detection> detections)
        {
            if (_label == null) return;

            if (detections.Count == 0)
            {
                _label.text = "ML: no detection";
                return;
            }

            var sb = new StringBuilder("ML: ");
            for (int i = 0; i < detections.Count; i++)
            {
                if (i > 0) sb.Append("  |  ");
                sb.Append(detections[i].Class).Append(' ').Append(detections[i].Confidence.ToString("F2"));
            }
            _label.text = sb.ToString();
        }
    }
}
