using System.Collections.Generic;
using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Cross-validates DoorCandidateDetector's geometric heuristic against
    /// VisualDoorDetector's camera-based door detection (Phase 3).
    ///
    /// Augments, never gates: DoorCandidateDetector.HasConfidentCandidate
    /// stays the baseline that HUD placement gating already uses - Phase 1
    /// behavior is unchanged. This adds an additional "visually confirmed"
    /// signal on top, for a future stricter mode or UI confidence indicator,
    /// without risking a regression if the visual detector has a bad frame
    /// or hasn't run yet.
    ///
    /// Deliberately uses temporal co-occurrence (both signals positive
    /// within the same short window) rather than spatial bounding-box
    /// overlap between the visual detection (camera-image pixel space) and
    /// the geometric candidate's on-screen projection (Unity screen space).
    /// Those two spaces don't line up without careful mapping - exactly the
    /// class of bug that cost real time during Phase 2's QR scanner
    /// debugging (pixelRect vs Screen.width/height mismatches). Temporal
    /// co-occurrence is simpler, avoids that whole problem, and still
    /// delivers genuine cross-validation value for the MVP.
    ///
    /// Handle-based anchor horizontal-position refinement (the plan's Step 6
    /// second half) is deferred - it can't avoid needing that same precise
    /// image-to-world mapping, so it deserves its own dedicated pass rather
    /// than being rushed into this one.
    /// </summary>
    public class DoorConfidenceCoordinator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DoorCandidateDetector _geometricDetector;
        [SerializeField] private VisualDoorDetector _visualDetector;

        [Header("Configuration")]
        [Tooltip("How long a visual 'door' detection counts as still current.")]
        [SerializeField] private float _visualConfirmationWindowSeconds = 1f;
        [SerializeField] [Range(0f, 1f)] private float _minVisualDoorConfidence = 0.4f;

        private float _lastVisualDoorSeenTime = -999f;

        /// <summary>True if a visual 'door' detection above threshold has
        /// been seen within the confirmation window.</summary>
        public bool IsVisuallyConfirmed =>
            Time.time - _lastVisualDoorSeenTime <= _visualConfirmationWindowSeconds;

        /// <summary>True only when BOTH the geometric heuristic and the
        /// visual detector agree a door is present right now - a stronger
        /// signal than either alone. Not currently wired to anything that
        /// gates placement; available for future use.</summary>
        public bool IsHighConfidence =>
            _geometricDetector != null && _geometricDetector.HasConfidentCandidate && IsVisuallyConfirmed;

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
            foreach (var d in detections)
            {
                if (d.Class == VisualDoorDetector.DetectionClass.Door && d.Confidence >= _minVisualDoorConfidence)
                {
                    _lastVisualDoorSeenTime = Time.time;
                    break;
                }
            }
        }
    }
}
