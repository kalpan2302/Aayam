using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 1 diagnostic: samples the geometric candidate's
    /// ARPlane.extents at a fixed interval during a scan, so extents
    /// stability can be judged from a real time series instead of
    /// one-off snapshots (which is all Phase 3 had - see
    /// docs/phases/phase-4-implementation-plan.md's "Re-reading the Phase 3
    /// failure" section for why that was never enough to separate a sign
    /// bug from a still-converging plane boundary). Temporary/diagnostic,
    /// not part of the permanent architecture - remove once the Option A
    /// vs B decision is settled.
    /// </summary>
    public class DoorPlaneStabilityLogger : MonoBehaviour
    {
        [SerializeField] private DoorCandidateDetector _geometricDetector;
        [SerializeField] private float _sampleIntervalSeconds = 1f;

        private float _timer;
        private TrackableId _lastPlaneId;

        private void Update()
        {
            if (_geometricDetector == null) return;
            var plane = _geometricDetector.BestCandidate;
            if (plane == null) { _timer = 0f; return; }

            _timer += Time.deltaTime;
            if (_timer < _sampleIntervalSeconds) return;
            _timer = 0f;

            bool samePlane = plane.trackableId == _lastPlaneId;
            _lastPlaneId = plane.trackableId;

            Debug.Log($"[DoorPlaneStability] id={plane.trackableId} samePlaneAsLastSample={samePlane} " +
                      $"width={plane.extents.x * 2f:F3}m height={plane.extents.y * 2f:F3}m " +
                      $"boundaryVerts={plane.boundary.Length} score={_geometricDetector.BestCandidateScore:F0}");
        }
    }
}
