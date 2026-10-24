using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ProductAutoPlacerTests
    {
        [Test]
        public void AutoPlace_Handle_LandsAtZoneCenter()
        {
            var anchor = ProductAutoPlacer.AutoPlace("HANDLE_001", DoorComponentZones.ZoneType.Handle, 0.9f, 2.1f, true);

            Assert.AreEqual("HANDLE_001", anchor.ProductId);
            Assert.AreEqual(0.725f, anchor.DoorSpacePosition.x, 1e-5f); // (0.680+0.770)/2
            Assert.AreEqual(1.000f, anchor.DoorSpacePosition.y, 1e-5f); // (0.950+1.050)/2
        }

        [Test]
        public void AutoPlace_Peephole_CentersOnDoorWidth()
        {
            var anchor = ProductAutoPlacer.AutoPlace("PEEPHOLE_001", DoorComponentZones.ZoneType.Peephole, 1.0f, 2.1f, true);
            Assert.AreEqual(0.5f, anchor.DoorSpacePosition.x, 1e-5f);
        }

        [Test]
        public void AutoPlace_Hinge_RespectsHingeSide()
        {
            var left = ProductAutoPlacer.AutoPlace("HINGE_001", DoorComponentZones.ZoneType.Hinge, 0.9f, 2.1f, hingeOnLeft: true);
            Assert.AreEqual(0f, left.DoorSpacePosition.x, 1e-5f);

            var right = ProductAutoPlacer.AutoPlace("HINGE_001", DoorComponentZones.ZoneType.Hinge, 0.9f, 2.1f, hingeOnLeft: false);
            Assert.AreEqual(0.9f, right.DoorSpacePosition.x, 1e-5f);
        }
    }
}
