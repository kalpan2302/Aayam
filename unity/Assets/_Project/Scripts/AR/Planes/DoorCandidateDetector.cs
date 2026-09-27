using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Scores every vertical plane as a potential door using geometric heuristics.
    /// Highlights the best candidate with a distinct material and fires an event.
    ///
    /// Scoring criteria (max 100):
    ///   Width in door range      → 30 pts
    ///   Height in door range     → 30 pts
    ///   Aspect ratio (h/w ≈ 2)  → 20 pts
    ///   Rectangular shape        → 20 pts
    /// </summary>
    public class DoorCandidateDetector : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private VerticalPlaneFilter _verticalFilter;
        [SerializeField] private PlaneBoundaryAnalyzer _boundaryAnalyzer;

        [Header("Scoring Thresholds")]
        [Tooltip("Minimum score (0-100) for a plane to be considered a door candidate.")]
        [SerializeField] [Range(20f, 90f)] private float _minScore = 50f;

        [Header("Door Size Ranges (meters)")]
        [SerializeField] private float _minWidth  = 0.5f;
        [SerializeField] private float _maxWidth  = 1.5f;
        [SerializeField] private float _minHeight = 1.5f;
        [SerializeField] private float _maxHeight = 2.6f;

        [Header("Ideal Size (meters)")]
        [SerializeField] private float _idealWidth  = 0.9f;
        [SerializeField] private float _idealHeight = 2.1f;

        // ─── State ────────────────────────────────────────────────
        public ARPlane BestCandidate { get; private set; }
        public float BestCandidateScore { get; private set; }

        // ─── Update ───────────────────────────────────────────────

        private void Update()
        {
            EvaluateCandidates();
        }

        private void EvaluateCandidates()
        {
            if (_verticalFilter == null || _boundaryAnalyzer == null) return;

            var candidates = new List<(ARPlane plane, float score)>();

            foreach (var plane in _verticalFilter.VerticalPlanes)
            {
                float score = ScorePlaneAsDoor(plane);
                if (score >= _minScore)
                    candidates.Add((plane, score));
            }

            if (candidates.Count == 0)
            {
                ClearBestCandidate();
                return;
            }

            // Sort descending by score
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var (bestPlane, bestScore) = candidates[0];

            if (BestCandidate == bestPlane) return;  // No change

            // Deselect old
            BestCandidate?.GetComponent<PlaneVisualizationController>()
                         ?.SetAsDoorCandidate(false);

            // Select new
            BestCandidate = bestPlane;
            BestCandidateScore = bestScore;
            BestCandidate.GetComponent<PlaneVisualizationController>()
                         ?.SetAsDoorCandidate(true);

            Debug.Log($"[DoorDetector] New best candidate: {BestCandidate.trackableId} | Score: {bestScore:F0}/100");
        }

        // ─── Scoring ──────────────────────────────────────────────

        private float ScorePlaneAsDoor(ARPlane plane)
        {
            float score = 0f;
            Vector2 dims = _boundaryAnalyzer.EstimateDimensions(plane);
            float width  = dims.x;
            float height = dims.y;

            // 1. Width (30 pts)
            if (width >= _minWidth && width <= _maxWidth)
            {
                // Bonus for being closer to ideal width
                float widthDelta = Mathf.Abs(width - _idealWidth) / _idealWidth;
                score += Mathf.Lerp(30f, 10f, widthDelta);
            }

            // 2. Height (30 pts)
            if (height >= _minHeight && height <= _maxHeight)
            {
                float heightDelta = Mathf.Abs(height - _idealHeight) / _idealHeight;
                score += Mathf.Lerp(30f, 10f, heightDelta);
            }

            // 3. Aspect ratio — good doors are roughly 2:1 to 2.5:1 (20 pts)
            float ratio = height / Mathf.Max(width, 0.001f);
            if (ratio >= 1.8f && ratio <= 2.8f)
                score += 20f;
            else if (ratio >= 1.4f && ratio <= 3.2f)
                score += 8f;

            // 4. Rectangular shape (20 pts)
            if (_boundaryAnalyzer.IsRectangular(plane, 0.25f))
                score += 20f;

            return score;
        }

        private void ClearBestCandidate()
        {
            if (BestCandidate == null) return;
            BestCandidate.GetComponent<PlaneVisualizationController>()
                         ?.SetAsDoorCandidate(false);
            BestCandidate = null;
            BestCandidateScore = 0f;
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Manually score a specific plane (for debugging).</summary>
        public float GetScoreForPlane(ARPlane plane) => ScorePlaneAsDoor(plane);

        /// <summary>True if a confident door candidate has been found.</summary>
        public bool HasConfidentCandidate => BestCandidate != null && BestCandidateScore >= 70f;
    }
}
