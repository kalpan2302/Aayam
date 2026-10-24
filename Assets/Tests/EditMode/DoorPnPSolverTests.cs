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
            Assert.AreEqual(239f, rotated.Cx, 1e-4f); // rawHeight - 1 - raw.Cy = 480 - 1 - 240
            Assert.AreEqual(320f, rotated.Cy, 1e-4f); // raw.Cx
        }

        /// <summary>
        /// The test that would have caught the 2026-10-15 on-device bug
        /// (marker on the wrong side of the door) before it ever reached a
        /// device - unlike the test above, this one does NOT re-derive its
        /// expectations from RotateIntrinsics90Clockwise's own formula. It
        /// independently replicates VisualDoorSegmenter's real
        /// RotateRgba90Clockwise pixel mapping, runs the exact same
        /// raw-project -> rotate-pixels -> rotate-intrinsics -> solve
        /// pipeline DoorPnPWorldAnchor uses, and checks the result against
        /// ground truth. The previous "KnownValues_MatchHandDerived" test
        /// above is circular (it only confirms the method matches itself)
        /// and could never have caught a sign/frame bug in the method under
        /// test - this one can, because its oracle (ground-truth pose) is
        /// independent of the code path being tested.
        ///
        /// Corrected 2026-10-24 (docs/phases/phase-4-implementation-plan.md
        /// has the full evidence chain): this test used to assert that an
        /// extra DoorPnPSolver.RotatedFrameCorrection (+90deg) was needed
        /// after the solve to recover tKnown/rotKnown. That passed, but for
        /// the wrong reason - it's an algebraic identity that
        /// rotate-pixels+solve+apply(+90deg) always equals
        /// solve-directly-on-raw-pixels+apply(no correction), REGARDLESS of
        /// whether RotateRgba90Clockwise's direction matches reality. So the
        /// test's ground truth was implicitly built on "camera-local axes ==
        /// raw sensor pixel axes, no rotation needed between them" (Project
        /// applied the raw pinhole formula directly to tKnown/rotKnown's own
        /// camera-local points) - an assumption never checked against a real
        /// device, and found wrong on 2026-10-24 (real on-device data showed
        /// the +90deg correction was the dominant cause of the marker
        /// landing off-screen; removing it, with the rotation step left
        /// otherwise untouched, landed the marker on-screen). Fixed by
        /// projecting through an extra +90deg rotation when building the
        /// synthetic "raw" pixels below, so the ground truth now encodes
        /// "camera-local axes == ROTATED sensor pixel axes" instead - and
        /// asserting the solve recovers tKnown/rotKnown with NO correction
        /// applied, matching DoorPnPWorldAnchor's corrected pipeline.
        /// </summary>
        [Test]
        public void RotatedPipeline_RecoversGroundTruthPose()
        {
            Quaternion rotKnown = Quaternion.Euler(10f, 25f, 3f);
            Vector3 tKnown = new Vector3(0.2f, -0.8f, 2.6f);
            var (cam, obj) = BuildCorners(rotKnown, tKnown, 0.9f, 2.1f);

            int rawW = 1920, rawH = 1440;
            var rawIntrinsics = new DoorPnPSolver.Intrinsics { Fx = 1400f, Fy = 1400f, Cx = rawW / 2f, Cy = rawH / 2f };

            // The real sensor's raw capture axes are rotated 90deg relative
            // to Unity's true camera-local axes (confirmed on-device
            // 2026-10-24) - model that here by rotating the known
            // camera-local corner BEFORE projecting it with the plain raw
            // pinhole formula, rather than projecting tKnown/rotKnown's
            // camera-local points directly as if raw-sensor-axes and
            // camera-local-axes were the same thing.
            var sensorAxisOffset = Quaternion.Euler(0f, 0f, 90f);
            var rotatedPixels = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 rawPx = Project(sensorAxisOffset * cam[i], rawIntrinsics);
                // Independent re-implementation of VisualDoorSegmenter's real
                // RotateRgba90Clockwise pixel mapping - deliberately not
                // calling any production code here, so this test can't pass
                // just because production code agrees with itself.
                rotatedPixels[i] = new Vector2(rawH - 1 - rawPx.y, rawPx.x);
            }

            var rotatedIntrinsics = DoorPnPSolver.RotateIntrinsics90Clockwise(rawIntrinsics, rawW, rawH);
            var result = DoorPnPSolver.SolvePlanarPose(rotatedPixels, obj, rotatedIntrinsics);
            Assert.IsTrue(result.Ok, result.FailReason);

            AssertVectorClose(tKnown, result.PositionCameraLocal, 1e-3f, "position");
            Assert.Less(Quaternion.Angle(rotKnown, result.RotationCameraLocal), 0.1f,
                $"rotation off by {Quaternion.Angle(rotKnown, result.RotationCameraLocal):F3} deg");
        }

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, float tol, string label)
        {
            Assert.AreEqual(expected.x, actual.x, tol, $"{label}.x expected {expected.x} got {actual.x}");
            Assert.AreEqual(expected.y, actual.y, tol, $"{label}.y expected {expected.y} got {actual.y}");
            Assert.AreEqual(expected.z, actual.z, tol, $"{label}.z expected {expected.z} got {actual.z}");
        }
    }
}
