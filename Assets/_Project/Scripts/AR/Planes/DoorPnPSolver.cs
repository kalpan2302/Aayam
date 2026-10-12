using UnityEngine;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 1, Option B: recovers a planar object's pose (rotation +
    /// translation, in camera-local space) from 4 known 2D image points and
    /// their corresponding 2D object-plane points (Z=0), given the camera's
    /// pinhole intrinsics. This is what replaces trusting `ARPlane.extents`
    /// directly (Option A) - see docs/phases/phase-4-implementation-plan.md's
    /// "Extents-stability scan results" section: ARKit's own plane boundary
    /// converges to a *stable* answer but not a *correct* one on this door
    /// (locked at 0.600m wide for 107 consecutive 1s samples, well under the
    /// 0.9-1.0m standard door widths the roadmap assumes), so the door's
    /// real-world size/pose has to come from the ML-detected corners instead.
    ///
    /// Four coplanar points reduce general PnP to a homography decomposition
    /// (no iterative solver, no SVD dependency Unity doesn't ship): solve for
    /// a 3x3 homography via a direct 8x8 linear system (exact for 4 points,
    /// Gaussian elimination, no external math library needed), then decompose
    /// it into rotation + translation. Standard technique for planar
    /// marker/AR pose recovery - not novel, but the exact pixel/camera-space
    /// sign conventions have bitten this project twice already
    /// (HandleWorldAnchorRefiner's unresolved placement bug, the
    /// Quaternion.LookRotation cross-product ambiguity), so every sign in
    /// here is pinned down by a synthetic ground-truth unit test
    /// (DoorPnPSolverTests.cs) rather than trusted from derivation alone.
    ///
    /// Pixel convention (must match the caller): u right-increasing from 0,
    /// v DOWN-increasing from 0 (standard image convention - matches
    /// DoorGeometryExtractor's mask row-major storage and its
    /// TopLeft/BottomLeft naming, where "top" = smaller v).
    ///
    /// Camera-local convention: Unity's own (X right, Y up, Z forward - i.e.
    /// `cameraTransform.forward` is local +Z). A point in front of the
    /// camera has local Z > 0.
    /// </summary>
    public static class DoorPnPSolver
    {
        public struct Intrinsics
        {
            public float Fx, Fy, Cx, Cy;
        }

        public struct PnPResult
        {
            public bool Ok;
            public string FailReason;
            public Vector3 PositionCameraLocal;
            public Quaternion RotationCameraLocal;
        }

        /// <summary>
        /// imagePointsPx and objectPointsMeters must both have length 4 and
        /// be in corresponding order (e.g. TL, TR, BR, BL for both).
        /// objectPointsMeters are 2D points on the object's own plane
        /// (Z=0 implicit), e.g. (0,0),(W,0),(W,H),(0,H) for a door with
        /// bottom-left origin, X=width, Y=height.
        /// </summary>
        public static PnPResult SolvePlanarPose(Vector2[] imagePointsPx, Vector2[] objectPointsMeters, Intrinsics intrinsics)
        {
            if (imagePointsPx == null || objectPointsMeters == null || imagePointsPx.Length != 4 || objectPointsMeters.Length != 4)
                return Fail("need exactly 4 corresponding points");

            // Normalize image points into camera-ray space: (xn, yn) such
            // that a camera-local point (x,y,z), z>0, projects to
            // u = cx + fx*(x/z), v = cy - fy*(y/z). See class doc for why
            // the v term is negated (image-down vs camera-local-up).
            var xn = new double[4];
            var yn = new double[4];
            for (int i = 0; i < 4; i++)
            {
                xn[i] = (imagePointsPx[i].x - intrinsics.Cx) / intrinsics.Fx;
                yn[i] = -(imagePointsPx[i].y - intrinsics.Cy) / intrinsics.Fy;
            }

            double[] h = SolveHomographyDLT(objectPointsMeters, xn, yn);
            if (h == null)
                return Fail("homography system singular (degenerate/collinear points)");

            // H columns (row-major h: h11,h12,h13,h21,h22,h23,h31,h32, h33=1 fixed)
            var h1 = new Vector3((float)h[0], (float)h[3], (float)h[6]);
            var h2 = new Vector3((float)h[1], (float)h[4], (float)h[7]);
            var h3 = new Vector3((float)h[2], (float)h[5], 1f);

            float norm1 = h1.magnitude;
            float norm2 = h2.magnitude;
            if (norm1 < 1e-8f || norm2 < 1e-8f)
                return Fail("degenerate homography columns");

            float lambda = 2f / (norm1 + norm2);
            Vector3 r1 = lambda * h1;
            Vector3 r2 = lambda * h2;
            Vector3 t = lambda * h3;

            // Resolve the homography's overall-sign ambiguity by requiring
            // the object to be in front of the camera (local Z > 0) - the
            // standard disambiguation for this decomposition. Flipping the
            // sign flips r1/r2/t but leaves r1 x r2 unchanged (cross of two
            // negated vectors = the original cross), verified in
            // DoorPnPSolverTests' "behind-camera" case.
            if (t.z < 0f)
            {
                r1 = -r1;
                r2 = -r2;
                t = -t;
            }

            // Gram-Schmidt orthogonalization - r1/r2 from a noisy/real
            // homography aren't exactly orthogonal even though the object
            // axes are.
            Vector3 r1n = r1.normalized;
            Vector3 r2n = (r2 - Vector3.Dot(r2, r1n) * r1n).normalized;
            Vector3 r3n = Vector3.Cross(r1n, r2n);

            var basis = new Matrix4x4();
            basis.SetColumn(0, new Vector4(r1n.x, r1n.y, r1n.z, 0f));
            basis.SetColumn(1, new Vector4(r2n.x, r2n.y, r2n.z, 0f));
            basis.SetColumn(2, new Vector4(r3n.x, r3n.y, r3n.z, 0f));
            basis.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

            return new PnPResult
            {
                Ok = true,
                PositionCameraLocal = t,
                RotationCameraLocal = basis.rotation,
            };
        }

        private static PnPResult Fail(string reason) => new PnPResult { Ok = false, FailReason = reason };

        /// <summary>
        /// Solves for H (h33 fixed = 1) mapping object-plane (X,Y) -> normalized
        /// camera-ray (xn,yn) from exactly 4 correspondences, via an 8x8 linear
        /// system (Gaussian elimination, partial pivoting). Exact for 4 points -
        /// no least-squares/SVD needed.
        /// </summary>
        private static double[] SolveHomographyDLT(Vector2[] obj, double[] xn, double[] yn)
        {
            var a = new double[8][];
            var b = new double[8];
            for (int i = 0; i < 4; i++)
            {
                double X = obj[i].x, Y = obj[i].y;
                // Row for xn_i: h11*X + h12*Y + h13 - xn*h31*X - xn*h32*Y = xn
                a[2 * i] = new double[] { X, Y, 1, 0, 0, 0, -xn[i] * X, -xn[i] * Y };
                b[2 * i] = xn[i];
                // Row for yn_i: h21*X + h22*Y + h23 - yn*h31*X - yn*h32*Y = yn
                a[2 * i + 1] = new double[] { 0, 0, 0, X, Y, 1, -yn[i] * X, -yn[i] * Y };
                b[2 * i + 1] = yn[i];
            }
            return SolveLinearSystem(a, b, 8);
        }

        private static double[] SolveLinearSystem(double[][] a, double[] b, int n)
        {
            for (int col = 0; col < n; col++)
            {
                int pivotRow = col;
                double pivotVal = System.Math.Abs(a[col][col]);
                for (int row = col + 1; row < n; row++)
                {
                    double v = System.Math.Abs(a[row][col]);
                    if (v > pivotVal) { pivotVal = v; pivotRow = row; }
                }
                if (pivotVal < 1e-12) return null;

                if (pivotRow != col)
                {
                    (a[col], a[pivotRow]) = (a[pivotRow], a[col]);
                    (b[col], b[pivotRow]) = (b[pivotRow], b[col]);
                }

                for (int row = col + 1; row < n; row++)
                {
                    double factor = a[row][col] / a[col][col];
                    if (factor == 0) continue;
                    for (int k = col; k < n; k++)
                        a[row][k] -= factor * a[col][k];
                    b[row] -= factor * b[col];
                }
            }

            var x = new double[n];
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = b[row];
                for (int k = row + 1; k < n; k++)
                    sum -= a[row][k] * x[k];
                x[row] = sum / a[row][row];
            }
            return x;
        }

        /// <summary>
        /// Rotates a raw camera image's intrinsics to match a frame that has
        /// been rotated 90 degrees clockwise (the exact transform
        /// VisualDoorDetector/VisualDoorSegmenter apply to fix ARKit's raw
        /// sensor-orientation bug - see their RotateRgba90Clockwise). Derived
        /// and pinned down by DoorPnPSolverTests, not trusted from derivation
        /// alone, same as the rest of this file: for a 90CW rotation,
        /// (x,y) -> (rawHeight-1-y, x), so the axis that measured horizontal
        /// extent (fx, cx) in the raw frame measures vertical extent in the
        /// rotated frame and vice versa.
        /// </summary>
        public static Intrinsics RotateIntrinsics90Clockwise(Intrinsics raw, int rawWidth, int rawHeight)
        {
            return new Intrinsics
            {
                Fx = raw.Fy,
                Fy = raw.Fx,
                Cx = rawHeight - raw.Cy,
                Cy = raw.Cx,
            };
        }
    }
}
