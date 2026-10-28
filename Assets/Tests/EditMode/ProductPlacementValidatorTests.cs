using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ProductPlacementValidatorTests
    {
        [Test]
        public void Validate_InsideZone_Ok()
        {
            var result = ProductPlacementValidator.Validate(
                new Vector3(0.72f, 1.0f, 0f), DoorComponentZones.ZoneType.Handle, 0.9f, 2.1f, true);
            Assert.IsTrue(result.Ok, result.Reason);
        }

        [Test]
        public void Validate_OutsideZoneButInsideDoor_Rejected()
        {
            var result = ProductPlacementValidator.Validate(
                new Vector3(0.1f, 1.9f, 0f), DoorComponentZones.ZoneType.Handle, 0.9f, 2.1f, true);
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("zone", result.Reason);
        }

        [Test]
        public void Validate_OutsideDoorBounds_Rejected()
        {
            var result = ProductPlacementValidator.Validate(
                new Vector3(-0.1f, 1.0f, 0f), DoorComponentZones.ZoneType.Handle, 0.9f, 2.1f, true);
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("bounds", result.Reason);
        }

        [Test]
        public void Validate_InvalidDoorDimensions_RejectedCleanly()
        {
            var result = ProductPlacementValidator.Validate(
                new Vector3(0.72f, 1.0f, 0f), DoorComponentZones.ZoneType.Handle, 0f, 2.1f, true);
            Assert.IsFalse(result.Ok);
        }
    }
}
