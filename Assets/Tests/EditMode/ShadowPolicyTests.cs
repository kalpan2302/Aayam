using NUnit.Framework;
using Aayam.AR.Core;
using Aayam.Services;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ShadowPolicyTests
    {
        [Test]
        public void ShouldCastShadows_Low_ReturnsFalse()
        {
            Assert.IsFalse(ShadowPolicy.ShouldCastShadows(DeviceQualityTier.Low));
        }

        [TestCase(DeviceQualityTier.Medium)]
        [TestCase(DeviceQualityTier.High)]
        [TestCase(DeviceQualityTier.Ultra)]
        public void ShouldCastShadows_AboveLow_ReturnsTrue(DeviceQualityTier tier)
        {
            Assert.IsTrue(ShadowPolicy.ShouldCastShadows(tier));
        }
    }
}
