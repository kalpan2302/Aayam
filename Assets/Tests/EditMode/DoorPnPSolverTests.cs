using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    /// <summary>
    /// Synthetic ground-truth tests for DoorPnPSolver (Phase 4 Step 1,
    /// Option B). Every sign/convention in the solver is pinned down here
    /// BEFORE any on-device use - same discipline DoorCoordinateSystem's
    /// rotation construction used, and the opposite of how
    /// HandleWorldAnchorRefiner was "verified" in Phase 3 (reasoned through
    /// once, never actually confirmed correct on-device).
    ///
    /// Approach: build a known camera-local pose for a door plane, project
    /// its 4 corners to pixel space using the SAME convention the solver
    /// documents (u right+, v down+, camera-local Y up/Z forward), feed the
    /// pixels back into the solver, and assert it recovers the known pose.
    /// </summary>
    [TestFixture]
    public class DoorPnPSolverTests
    {
        private static readonly DoorPnPSolver.Intrinsics Intrinsics = new DoorPnPSolver.Intrinsics
        {
            Fx = 1000f, Fy = 1000f, Cx = 320f, Cy = 480f,
        };

        /// <summary>
        /// Projects a camera-local point (z must be > 0) to pixel space
        /// using the solver's documented convention - the test's own
        /// "encoder", independent of the solver's internals.
        /// </summary>
        private static Vector2 Project(Vector3 pCameraLocal, DoorPnPSolver.Intrinsics k)
        {
            float x = pCameraLocal.x / pCameraLocal.z;
            float y = pCameraLocal.y / pCameraLocal.z;
            return new Vector2(k.Cx + k.Fx * x, k.Cy - k.Fy * y);
        }

        /// <summary>
        /// Builds the 4 door corners (TL,TR,BR,BL) in camera-local space from
        /// a door pose (rotation + origin translation, both camera-local)
        /// and width/height, object-space convention X=width,Y=height,
        /// origin=bottom-left (matches DoorCoordinateSystem).
        /// </summary>
        private static (Vector3[] cam, Vector2[] obj) BuildCorners(Quaternion rot, Vector3 t, float w, float h)
        {
            var objPts2D = new[] { new Vector2(0, h), new Vector2(w, h), new Vector2(w, 0), new Vector2(0, 0) };
            var cam = new Vector3[4];
            for (int i = 0; i < 4; i++)
                cam[i] = rot * new Vector3(objPts2D[i].x, objPts2D[i].y, 0f) + t;
            return (cam, objPts2D);
        }

        [Test]
        public void SolvePlanarPose_FrontoParallel_RecoversIdentityAndDepth()
        {
            Quaternion rotKnown = Quaternion.identity;
            Vector3 tKnown = new Vector3(0f, 0f, 2f);
            var (cam, obj) = BuildCorners(rotKnown, tKnown, 0.9f, 2.1f);
            var px = new Vector2[4];
            for (int i = 0; i < 4; i++) px[i] = Project(cam[i], Intrinsics);

            var result = DoorPnPSolver.SolvePlanarPose(px, obj, Intrinsics);

            Assert.IsTrue(result.Ok, result.FailReason);
            AssertVectorClose(tKnown, result.PositionCameraLocal, 1e-3f, "position");
            Assert.Less(Quaternion.Angle(rotKnown, result.RotationCameraLocal), 0.1f,
                $"rotation off by {Quaternion.Angle(rotKnown, result.RotationCameraLocal):F3} deg");
        }

        [Test]
        public void SolvePlanarPose_TiltedAndOffset_RecoversKnownPose()
        {
            Quaternion rotKnown = Quaternion.Euler(8f, 22f, -4f);
            Vector3 tKnown = new Vector3(0.15f, -0.08f, 1.8f);
            var (cam, obj) = BuildCorners(rotKnown, tKnown, 1.0f, 2.1f);
            var px = new Vector2[4];
            for (int i = 0; i < 4; i++) px[i] = Project(cam[i], Intrinsics);

            var result = DoorPnPSolver.SolvePlanarPose(px, obj, Intrinsics);

            Assert.IsTrue(result.Ok, result.FailReason);
            AssertVectorClose(tKnown, result.PositionCameraLocal, 1e-3f, "position");
            Assert.Less(Quaternion.Angle(rotKnown, result.RotationCameraLocal), 0.1f,
                $"rotation off by {Quaternion.Angle(rotKnown, result.RotationCameraLocal):F3} deg");
        }

        [Test]
        public void SolvePlanarPose_NonSquareFocalLengths_StillRecoversPose()
        {
            var k = new DoorPnPSolver.Intrinsics { Fx = 1150f, Fy = 980f, Cx = 360f, Cy = 640f };
            Quaternion rotKnown = Quaternion.Euler(-5f, 10f, 3f);
            Vector3 tKnown = new Vector3(-0.1f, 0.05f, 2.4f);
            var (cam, obj) = BuildCorners(rotKnown, tKnown, 0.9f, 2.1f);
            var px = new Vector2[4];
            for (int i = 0; i < 4; i++) px[i] = Project(cam[i], k);

            var result = DoorPnPSolver.SolvePlanarPose(px, obj, k);

            Assert.IsTrue(result.Ok, result.FailReason);
            AssertVectorClose(tKnown, result.PositionCameraLocal, 1e-3f, "position");
            Assert.Less(Quaternion.Angle(rotKnown, result.RotationCameraLocal), 0.1f,
                $"rotation off by {Quaternion.Angle(rotKnown, result.RotationCameraLocal):F3} deg");
        }

        [Test]
        public void SolvePlanarPose_DegeneratePoints_FailsCleanly()
        {
            // All 4 "corners" collinear - no valid homography.
            var px = new[] { new Vector2(100, 100), new Vector2(200, 100), new Vector2(300, 100), new Vector2(400, 100) };
            var obj = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(3, 0) };

            var result = DoorPnPSolver.SolvePlanarPose(px, obj, Intrinsics);

            Assert.IsFalse(result.Ok);
        }

        [Test]
        public void RotateIntrinsics90Clockwise_KnownValues_MatchHandDerived()
        {
            var raw = new DoorPnPSolver.Intrinsics { Fx = 1200f, Fy = 1100f, Cx = 320f, Cy = 240f };
            var rotated = DoorPnPSolver.RotateIntrinsics90Clockwise(raw, rawWidth: 640, rawHeight: 480);

            Assert.AreEqual(1100f, rotated.Fx, 1e-4f);
            Assert.AreEqual(1200f, rotated.Fy, 1e-4f);
            Assert.AreEqual(240f, rotated.Cx, 1e-4f); // rawHeight - raw.Cy = 480 - 240
            Assert.AreEqual(320f, rotated.Cy, 1e-4f); // raw.Cx
        }

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, float tol, string label)
        {
            Assert.AreEqual(expected.x, actual.x, tol, $"{label}.x expected {expected.x} got {actual.x}");
            Assert.AreEqual(expected.y, actual.y, tol, $"{label}.y expected {expected.y} got {actual.y}");
            Assert.AreEqual(expected.z, actual.z, tol, $"{label}.z expected {expected.z} got {actual.z}");
        }
    }
}
