using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorComponentZonesTests
    {
        [Test]
        public void HandleZone_MatchesRoadmapMillimeters()
        {
            var z = DoorComponentZones.HandleZone;
            Assert.AreEqual(0.680f, z.MinX, 1e-6f);
            Assert.AreEqual(0.770f, z.MaxX, 1e-6f);
            Assert.AreEqual(0.950f, z.MinY, 1e-6f);
            Assert.AreEqual(1.050f, z.MaxY, 1e-6f);
        }

        [Test]
        public void LockZone_MatchesHandleZone_AsRoadmapSpecifies()
        {
            Assert.AreEqual(DoorComponentZones.HandleZone, DoorComponentZones.LockZone);
        }

        [Test]
        public void PeepholeZone_XIsWidthCenter()
        {
            var z = DoorComponentZones.PeepholeZone(0.9f);
            Assert.AreEqual(0.45f, z.MinX, 1e-6f);
            Assert.AreEqual(0.45f, z.MaxX, 1e-6f);
            Assert.AreEqual(1.450f, z.MinY, 1e-6f);
            Assert.AreEqual(1.550f, z.MaxY, 1e-6f);
        }

        [Test]
        public void HingeZone_PicksCorrectEdge_AndSpansFullHeight()
        {
            var left = DoorComponentZones.HingeZone(0.9f, 2.1f, hingeOnLeft: true);
            Assert.AreEqual(0f, left.MinX, 1e-6f);
            Assert.AreEqual(0f, left.MaxX, 1e-6f);
            Assert.AreEqual(0f, left.MinY, 1e-6f);
            Assert.AreEqual(2.1f, left.MaxY, 1e-6f);

            var right = DoorComponentZones.HingeZone(0.9f, 2.1f, hingeOnLeft: false);
            Assert.AreEqual(0.9f, right.MinX, 1e-6f);
            Assert.AreEqual(0.9f, right.MaxX, 1e-6f);
        }

        [Test]
        public void Zone_Contains_InsideAndOutsidePoints()
        {
            var z = DoorComponentZones.HandleZone;
            Assert.IsTrue(z.Contains(new Vector2(0.72f, 1.0f)));   // inside
            Assert.IsTrue(z.Contains(new Vector2(0.680f, 0.950f))); // on the boundary (inclusive)
            Assert.IsFalse(z.Contains(new Vector2(0.5f, 1.0f)));   // left of zone
            Assert.IsFalse(z.Contains(new Vector2(0.72f, 1.2f)));  // above zone
        }

        [Test]
        public void GetZone_DispatchesToCorrectZoneForEachType()
        {
            float w = 0.9f, h = 2.1f;
            Assert.AreEqual(DoorComponentZones.HandleZone, DoorComponentZones.GetZone(DoorComponentZones.ZoneType.Handle, w, h, true));
            Assert.AreEqual(DoorComponentZones.LockZone, DoorComponentZones.GetZone(DoorComponentZones.ZoneType.Lock, w, h, true));
            Assert.AreEqual(DoorComponentZones.PeepholeZone(w), DoorComponentZones.GetZone(DoorComponentZones.ZoneType.Peephole, w, h, true));
            Assert.AreEqual(DoorComponentZones.HingeZone(w, h, true), DoorComponentZones.GetZone(DoorComponentZones.ZoneType.Hinge, w, h, true));
        }

        [Test]
        public void StandardSizes_AndThicknesses_MatchRoadmapCount()
        {
            Assert.AreEqual(2, DoorComponentZones.StandardSizes.Length);
            Assert.AreEqual(5, DoorComponentZones.StandardThicknessesMeters.Length);
        }

        [TestCase("handle", DoorComponentZones.ZoneType.Handle)]
        [TestCase("Handle", DoorComponentZones.ZoneType.Handle)]
        [TestCase("lock", DoorComponentZones.ZoneType.Lock)]
        [TestCase("peephole", DoorComponentZones.ZoneType.Peephole)]
        [TestCase("hinge", DoorComponentZones.ZoneType.Hinge)]
        public void TryMapProductCategory_KnownCategories_MapCorrectly(string category, DoorComponentZones.ZoneType expected)
        {
            Assert.IsTrue(DoorComponentZones.TryMapProductCategory(category, out var zoneType));
            Assert.AreEqual(expected, zoneType);
        }

        [TestCase("accessory")]
        [TestCase("")]
        [TestCase((string)null)]
        [TestCase("shelf")]
        public void TryMapProductCategory_UnknownOrEmpty_ReturnsFalse(string category)
        {
            Assert.IsFalse(DoorComponentZones.TryMapProductCategory(category, out _));
        }

        [Test]
        public void ClampToZone_PointInsideZone_IsUnchanged()
        {
            var z = DoorComponentZones.HandleZone;
            var p = new Vector2(0.72f, 1.0f);
            Assert.AreEqual(p, DoorComponentZones.ClampToZone(p, z));
        }

        [Test]
        public void ClampToZone_PointOutsideEachAxis_ClampsToNearestEdge()
        {
            var z = DoorComponentZones.HandleZone;
            Assert.AreEqual(new Vector2(0.680f, 1.0f), DoorComponentZones.ClampToZone(new Vector2(0.1f, 1.0f), z));
            Assert.AreEqual(new Vector2(0.770f, 1.0f), DoorComponentZones.ClampToZone(new Vector2(2.0f, 1.0f), z));
            Assert.AreEqual(new Vector2(0.72f, 0.950f), DoorComponentZones.ClampToZone(new Vector2(0.72f, 0f), z));
            Assert.AreEqual(new Vector2(0.72f, 1.050f), DoorComponentZones.ClampToZone(new Vector2(0.72f, 2f), z));
        }

        [Test]
        public void ClampToZone_PointOutsideBothAxes_ClampsCorner()
        {
            var z = DoorComponentZones.HandleZone;
            Assert.AreEqual(new Vector2(0.680f, 0.950f), DoorComponentZones.ClampToZone(new Vector2(-1f, -1f), z));
        }

        [Test]
        public void ClampToZone_DegenerateZone_CollapsesToSingleValue()
        {
            // Peephole's X is a single value (MinX==MaxX), not a range.
            var z = DoorComponentZones.PeepholeZone(0.9f);
            var clamped = DoorComponentZones.ClampToZone(new Vector2(0f, 1.5f), z);
            Assert.AreEqual(0.45f, clamped.x, 1e-6f);
        }
    }
}
