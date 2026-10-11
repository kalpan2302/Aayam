using System.Collections.Generic;
using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 3 Step 8: binary mask -> door corners -> dimensions -> handle
    /// position estimate. Pure C#, no OpenCV equivalent in Unity, so contour
    /// tracing (Moore-neighbor) and polygon simplification (Douglas-Peucker)
    /// are hand-rolled here - both are standard, well-defined algorithms,
    /// not novel. Mirrors docs/phases/phase-3-door-detection.md Step 6's
    /// pseudocode and the Python prototype it was validated against
    /// (door width/height + handle-side heuristic, ~6-18% position error on
    /// two real test photos - see docs/phases/phase-3-implementation-plan.md
    /// Step 8).
    /// </summary>
    public static class DoorGeometryExtractor
    {
        // 2% (the roadmap doc's own suggested value, and what worked for
        // the box detector's cleaner shapes) was too conservative for real
        // segmentation masks here - verified against actual model output:
        // a real door's mask has a genuine, legitimate bump near the handle
        // (the handle protrudes past the door's flat silhouette), which 2%
        // simplification left as extra vertices (7-8, not 4) even on a mask
        // OpenCV's own findContours+approxPolyDP agreed was that complex -
        // this isn't a tracer bug, the shape really is that detailed. 4%
        // collapses it to a clean quad without going degenerate (5% was
        // already too aggressive, collapsing to a 3-point triangle).
        private const float SimplificationEpsilonFraction = 0.04f;

        public struct DoorGeometry
        {
            public bool Ok;
            public string FailReason;
            public Vector2 TopLeft, TopRight, BottomRight, BottomLeft;
            public float WidthPx, HeightPx;
            public bool IsLeftHandled;
            public Vector2 EstimatedHandlePos;
        }

        /// <summary>
        /// mask: binary (0 or 1) values, row-major, maskWidth x maskHeight.
        /// handleCenter: null if no handle detected for this door.
        /// </summary>
        public static DoorGeometry Extract(byte[] mask, int maskWidth, int maskHeight, Vector2? handleCenter)
        {
            var contour = TraceLargestContour(mask, maskWidth, maskHeight);
            if (contour == null || contour.Count < 3)
                return new DoorGeometry { Ok = false, FailReason = "no contour found" };

            var simplified = DouglasPeucker(contour, epsilon: Perimeter(contour) * SimplificationEpsilonFraction);
            if (simplified.Count != 4)
                return new DoorGeometry { Ok = false, FailReason = $"{simplified.Count} vertices, not 4" };

            var (tl, tr, br, bl) = OrderCorners(simplified);
            float width = (Vector2.Distance(tl, tr) + Vector2.Distance(bl, br)) * 0.5f;
            float height = (Vector2.Distance(tl, bl) + Vector2.Distance(tr, br)) * 0.5f;

            float doorCenterX = (tl.x + tr.x + bl.x + br.x) / 4f;
            bool isLeftHandled = handleCenter.HasValue && handleCenter.Value.x < doorCenterX;

            float fracX = isLeftHandled ? 0.1f : 0.9f;
            Vector2 estHandlePos = new Vector2(
                tl.x + fracX * (tr.x - tl.x),
                tl.y + 0.5f * (bl.y - tl.y));

            return new DoorGeometry
            {
                Ok = true,
                TopLeft = tl, TopRight = tr, BottomRight = br, BottomLeft = bl,
                WidthPx = width, HeightPx = height,
                IsLeftHandled = isLeftHandled,
                EstimatedHandlePos = estHandlePos,
            };
        }

        // ─── Contour tracing (Moore-neighbor) ────────────────────────

        private static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] DY = { 0, 1, 1, 1, 0, -1, -1, -1 };

        private static List<Vector2> TraceLargestContour(byte[] mask, int w, int h)
        {
            bool[,] visited = new bool[w, h];
            List<Vector2> best = null;
            int bestArea = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x] == 0 || visited[x, y]) continue;
                    // Find a boundary pixel: foreground with a background (or edge) neighbor.
                    if (!IsBoundary(mask, w, h, x, y)) { visited[x, y] = true; continue; }

                    var contour = TraceFrom(mask, w, h, x, y, visited);
                    int area = ApproxArea(contour);
                    if (area > bestArea) { bestArea = area; best = contour; }
                }
            }
            return best;
        }

        private static bool IsBoundary(byte[] mask, int w, int h, int x, int y)
        {
            for (int d = 0; d < 8; d += 2) // 4-neighbors are enough to decide boundary
            {
                int nx = x + DX[d], ny = y + DY[d];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h || mask[ny * w + nx] == 0)
                    return true;
            }
            return false;
        }

        private static List<Vector2> TraceFrom(byte[] mask, int w, int h, int startX, int startY, bool[,] visited)
        {
            var points = new List<Vector2>();
            int x = startX, y = startY;
            int startDir = 0;
            int steps = 0;
            int maxSteps = w * h * 2; // safety bound against pathological loops

            do
            {
                points.Add(new Vector2(x, y));
                visited[x, y] = true;

                bool found = false;
                for (int i = 0; i < 8; i++)
                {
                    int tryDir = (startDir + 6 + i) % 8; // search starting behind-left of travel dir
                    int nx = x + DX[tryDir], ny = y + DY[tryDir];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (mask[ny * w + nx] != 0)
                    {
                        x = nx; y = ny; startDir = tryDir; found = true;
                        break;
                    }
                }
                if (!found) break;
                steps++;
            } while ((x != startX || y != startY) && steps < maxSteps);

            return points;
        }

        private static int ApproxArea(List<Vector2> pts)
        {
            if (pts.Count < 3) return 0;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts)
            {
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }
            return Mathf.RoundToInt((maxX - minX) * (maxY - minY));
        }

        // ─── Douglas-Peucker simplification ──────────────────────────

        private static float Perimeter(List<Vector2> pts)
        {
            float p = 0;
            for (int i = 0; i < pts.Count; i++)
                p += Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
            return p;
        }

        private static List<Vector2> DouglasPeucker(List<Vector2> pts, float epsilon)
        {
            if (pts.Count < 3) return new List<Vector2>(pts);
            var simplified = DouglasPeuckerRecursive(pts, epsilon);
            // Open-curve DP leaves start/end separate; for a closed contour,
            // drop a duplicated closing point if present. Moore-neighbor
            // tracing routinely stops 1 pixel short of exact closure
            // (verified: a clean rectangle's raw trace ended one diagonal
            // step short of its start), so this needs real slack - a
            // strict "<1" threshold missed the exact 1.0-distance case.
            if (simplified.Count > 1 && Vector2.Distance(simplified[0], simplified[simplified.Count - 1]) <= 1.5f)
                simplified.RemoveAt(simplified.Count - 1);
            return simplified;
        }

        private static List<Vector2> DouglasPeuckerRecursive(List<Vector2> pts, float epsilon)
        {
            if (pts.Count < 3) return new List<Vector2>(pts);

            float maxDist = 0;
            int index = 0;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float dist = PerpendicularDistance(pts[i], pts[0], pts[pts.Count - 1]);
                if (dist > maxDist) { maxDist = dist; index = i; }
            }

            if (maxDist > epsilon)
            {
                var left = DouglasPeuckerRecursive(pts.GetRange(0, index + 1), epsilon);
                var right = DouglasPeuckerRecursive(pts.GetRange(index, pts.Count - index), epsilon);
                left.RemoveAt(left.Count - 1);
                left.AddRange(right);
                return left;
            }
            return new List<Vector2> { pts[0], pts[pts.Count - 1] };
        }

        private static float PerpendicularDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            if (a == b) return Vector2.Distance(p, a);
            float num = Mathf.Abs((b.y - a.y) * p.x - (b.x - a.x) * p.y + b.x * a.y - b.y * a.x);
            float den = Vector2.Distance(a, b);
            return num / den;
        }

        // ─── Corner ordering ──────────────────────────────────────────

        private static (Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl) OrderCorners(List<Vector2> pts)
        {
            Vector2 tl = pts[0], tr = pts[0], br = pts[0], bl = pts[0];
            float minSum = float.MaxValue, maxSum = float.MinValue;
            float minDiff = float.MaxValue, maxDiff = float.MinValue;

            foreach (var p in pts)
            {
                float sum = p.x + p.y;
                float diff = p.y - p.x;
                if (sum < minSum) { minSum = sum; tl = p; }
                if (sum > maxSum) { maxSum = sum; br = p; }
                if (diff < minDiff) { minDiff = diff; tr = p; }
                if (diff > maxDiff) { maxDiff = diff; bl = p; }
            }
            return (tl, tr, br, bl);
        }
    }
}
