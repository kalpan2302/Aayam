using NUnit.Framework;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorOpeningDirectionTests
    {
        [Test]
        public void FromHandleSide_LeftHandle_InfersRightHinge()
        {
            var d = DoorOpeningDirection.FromHandleSide(isLeftHandled: true);
            Assert.IsFalse(d.HingeOnLeft);
            Assert.AreEqual(DoorHandedness.RightHand, d.Handedness);
            Assert.IsFalse(d.UserConfirmed);
        }

        [Test]
        public void FromHandleSide_RightHandle_InfersLeftHinge()
        {
            var d = DoorOpeningDirection.FromHandleSide(isLeftHandled: false);
            Assert.IsTrue(d.HingeOnLeft);
            Assert.AreEqual(DoorHandedness.LeftHand, d.Handedness);
        }

        [Test]
        public void WithUserOverride_SetsUserConfirmedAndOverridesHeuristic()
        {
            var inferred = DoorOpeningDirection.FromHandleSide(isLeftHandled: true); // hinge right
            var overridden = inferred.WithUserOverride(hingeOnLeft: true); // user says left

            Assert.IsTrue(overridden.HingeOnLeft);
            Assert.IsTrue(overridden.UserConfirmed);
        }
    }
}
