using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    /// <summary>
    /// Synthetic checks for VisualDoorSegmenter.ComputeVerticalFieldOfViewDegrees
    /// (Phase 4 Step 1, Option B follow-up, 2026-10-21). AR Foundation never
    /// syncs Camera.fieldOfView to the device's real intrinsics on its own -
    /// confirmed by grepping the whole codebase for "fieldOfView" before this
    /// fix, nothing ever set it, so it sat at Unity's new-Camera default
    /// (60 deg), narrower than the real phone lens. That mismatch was
    /// confirmed on-device: the camera-local angle to the door was only
    /// ~22-33 deg off axis (computed from the exact camPos/camRot/worldPos
    /// logged on-device via Quaternion math, not estimated), yet
    /// Camera.main.WorldToViewportPoint() put the same point at viewport X
    /// ~1.3-1.5 - consistent with a render FOV meaningfully narrower than
    /// what the angle math says the real lens actually sees.
    /// </summary>
    [TestFixture]
    public class VisualDoorSegmenterFieldOfViewTests
    {
        [Test]
        public void ComputeVerticalFieldOfViewDegrees_FocalLengthHalfOfWidth_Gives90Degrees()
        {
            // A classic sanity pin: focal length == half the sensor width
            // axis gives exactly a 90 degree FOV along that axis
            // (atan(1) * 2 = 90), independent of any Unity camera/AR state.
            float fov = VisualDoorSegmenter.ComputeVerticalFieldOfViewDegrees(sensorWidthPx: 1000f, focalLengthPx: 500f);
            Assert.AreEqual(90f, fov, 0.001f);
        }

        [Test]
        public void ComputeVerticalFieldOfViewDegrees_LargerFocalLength_GivesNarrowerFov()
        {
            float wideFov = VisualDoorSegmenter.ComputeVerticalFieldOfViewDegrees(1920f, 1400f);
            float narrowFov = VisualDoorSegmenter.ComputeVerticalFieldOfViewDegrees(1920f, 2000f);
            Assert.Less(narrowFov, wideFov);
        }

        [Test]
        public void ComputeVerticalFieldOfViewDegrees_UnityDefaultSixtyDegrees_IsNarrowerThanTypicalPhoneLens()
        {
            // Reproduces the actual discrepancy found on-device: a typical
            // iPhone wide-lens focal length (~26mm-equivalent, roughly
            // Fx/Fy around 0.73x the sensor's long-axis pixel width for a
            // 1920-wide capture) implies a WIDER vertical FOV, once rotated
            // into portrait, than Unity's 60-degree default camera used to
            // render it - the gap that pushed on-screen points outside the
            // render.
            float impliedFov = VisualDoorSegmenter.ComputeVerticalFieldOfViewDegrees(sensorWidthPx: 1920f, focalLengthPx: 1400f);
            Assert.Greater(impliedFov, 60f);
        }
    }
}
