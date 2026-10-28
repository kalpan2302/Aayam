using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorEdgeCalibratorTests
    {
        [Test]
        public void TryIntersectDoorPlane_RayStraightAtPlane_FindsCorrectPoint()
        {
            // Plane at z=2, facing -Z (normal points back toward the ray origin).
            var planePoint = new Vector3(0f, 0f, 2f);
            var planeNormal = new Vector3(0f, 0f, -1f);
            var ray = new Ray(Vector3.zero, Vector3.forward);

            bool ok = DoorEdgeCalibrator.TryIntersectDoorPlane(ray, planePoint, planeNormal, out Vector3 hit);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Vector3(0f, 0f, 2f), hit);
        }

        [Test]
        public void TryIntersectDoorPlane_OffAxisRay_FindsCorrectPoint()
        {
            var planePoint = new Vector3(0f, 0f, 2f);
            var planeNormal = new Vector3(0f, 0f, -1f);
            // Ray angled off-axis from origin - should still hit the z=2 plane,
            // just at a shifted x/y.
            var ray = new Ray(Vector3.zero, new Vector3(0.5f, 0.5f, 1f).normalized);

            bool ok = DoorEdgeCalibrator.TryIntersectDoorPlane(ray, planePoint, planeNormal, out Vector3 hit);

            Assert.IsTrue(ok);
            Assert.AreEqual(2f, hit.z, 1e-4f);
            Assert.AreEqual(1f, hit.x, 1e-4f);
            Assert.AreEqual(1f, hit.y, 1e-4f);
        }

        [Test]
        public void TryIntersectDoorPlane_RayParallelToPlane_Fails()
        {
            var planePoint = new Vector3(0f, 0f, 2f);
            var planeNormal = new Vector3(0f, 0f, -1f);
            var ray = new Ray(Vector3.zero, Vector3.right); // parallel to the plane

            bool ok = DoorEdgeCalibrator.TryIntersectDoorPlane(ray, planePoint, planeNormal, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void TryIntersectDoorPlane_PlaneBehindRay_Fails()
        {
            var planePoint = new Vector3(0f, 0f, -2f); // behind the ray's origin
            var planeNormal = new Vector3(0f, 0f, -1f);
            var ray = new Ray(Vector3.zero, Vector3.forward);

            bool ok = DoorEdgeCalibrator.TryIntersectDoorPlane(ray, planePoint, planeNormal, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void MeasureDoorSpaceSpan_TwoPointsOnAxis_ReturnsCorrectDistance()
        {
            var hostGo = new GameObject("TestDoorHost");
            var originGo = new GameObject("TestDoorOrigin");
            try
            {
                var door = hostGo.AddComponent<DoorCoordinateSystem>();
                door.SetOriginFromWorldPose(new Vector3(0f, 0f, 5f), Quaternion.identity, originGo);

                // Two world points 0.75m apart along the door's local X (width) axis.
                Vector3 worldA = door.DoorToWorldSpace(new Vector3(0f, 1f, 0f));
                Vector3 worldB = door.DoorToWorldSpace(new Vector3(0.75f, 1f, 0f));

                float span = DoorEdgeCalibrator.MeasureDoorSpaceSpan(door, worldA, worldB);

                Assert.AreEqual(0.75f, span, 1e-4f);
            }
            finally
            {
                // DoorCoordinateSystem self-registers into the static
                // DoorRegistry on Awake() - explicit Unregister here (not
                // relying on OnDestroy firing synchronously under
                // DestroyImmediate) so this test can't leak a "front-door"
                // entry into any other test in the same run.
                DoorRegistry.Unregister("front-door");
                Object.DestroyImmediate(hostGo);
                Object.DestroyImmediate(originGo);
            }
        }
    }
}
