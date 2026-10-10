using NUnit.Framework;
using UnityEngine;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class MathUtilsTests
    {
        [Test]
        public void SnapToNearest_ExactMultiple_ReturnsUnchanged()
        {
            Assert.AreEqual(90f, SnapToNearest(90f, 90f), 0.01f);
        }

        [Test]
        public void SnapToNearest_SlightlyOff_Snaps()
        {
            Assert.AreEqual(90f, SnapToNearest(85f, 90f), 0.01f);
        }

        [Test]
        public void SnapToNearest_Midpoint_SnapsUp()
        {
            Assert.AreEqual(90f, SnapToNearest(45f, 90f), 0.01f);
        }

        [TestCase(0f, 90f, 0f)]
        [TestCase(44f, 90f, 0f)]
        [TestCase(46f, 90f, 90f)]
        [TestCase(135f, 90f, 180f)]
        [TestCase(359f, 90f, 360f)]
        public void SnapToNearest_VariousAngles_CorrectResult(float input, float snap, float expected)
        {
            Assert.AreEqual(expected, SnapToNearest(input, snap), 0.5f);
        }

        [TestCase(0.9f, 2.1f, true,  Description = "Standard Indian door")]
        [TestCase(0.6f, 1.7f, true,  Description = "Minimum valid door")]
        [TestCase(1.3f, 2.5f, true,  Description = "Maximum valid door")]
        [TestCase(0.5f, 1.7f, false, Description = "Too narrow")]
        [TestCase(1.4f, 2.1f, false, Description = "Too wide")]
        [TestCase(0.9f, 1.6f, false, Description = "Too short")]
        [TestCase(0.9f, 2.6f, false, Description = "Too tall")]
        [TestCase(3.0f, 2.5f, false, Description = "Wall, not door")]
        [TestCase(0.3f, 0.5f, false, Description = "Window/vent")]
        public void IsValidDoorDimension_VariousSizes(float width, float height, bool expected)
        {
            bool result = IsWithinDoorRange(width, height);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void AspectRatio_StandardDoor_IsAboutTwoToOne()
        {
            float aspect = 2.1f / 0.9f;
            Assert.IsTrue(aspect > 1.5f && aspect < 3.5f,
                $"Door aspect ratio {aspect:F2} should be between 1.5 and 3.5");
        }

        [Test]
        public void ProjectOnPlane_VerticalSurface_GravityAlignedUp()
        {
            Vector3 surfaceNormal = Vector3.forward;
            Vector3 projectedUp  = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;
            Assert.AreEqual(Vector3.up, projectedUp);
        }

        [Test]
        public void ProjectOnPlane_HorizontalSurface_ZeroUp()
        {
            Vector3 surfaceNormal = Vector3.up;
            Vector3 projectedUp  = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal);
            Assert.AreEqual(0f, projectedUp.magnitude, 0.001f);
        }

        [Test]
        public void LookRotation_VerticalSurface_ForwardAligned()
        {
            Vector3 normal = Vector3.forward;
            Vector3 up     = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            Quaternion rot = Quaternion.LookRotation(normal, up);

            Vector3 fwd = rot * Vector3.forward;
            Assert.AreEqual(Vector3.forward.x, fwd.x, 0.01f);
            Assert.AreEqual(Vector3.forward.y, fwd.y, 0.01f);
            Assert.AreEqual(Vector3.forward.z, fwd.z, 0.01f);
        }

        private static float SnapToNearest(float angle, float snap)
            => Mathf.Floor(angle / snap + 0.5f) * snap;

        private static bool IsWithinDoorRange(float width, float height)
        {
            const float minW = 0.6f, maxW = 1.3f;
            const float minH = 1.7f, maxH = 2.5f;
            return width >= minW && width <= maxW && height >= minH && height <= maxH;
        }
    }
}
