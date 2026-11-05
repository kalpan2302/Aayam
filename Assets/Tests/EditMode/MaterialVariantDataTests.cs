using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Interaction;
using Aayam.Models;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class MaterialVariantDataTests
    {
        private static MaterialVariantData Make(string finish, bool isDefault = false)
        {
            return new MaterialVariantData { FinishName = finish, BaseColorHex = "#FFFFFF", IsDefault = isDefault };
        }

        [Test]
        public void FindDefaultIndex_EmptyArray_ReturnsZero()
        {
            Assert.AreEqual(0, MaterialVariantSelector.FindDefaultIndex(new MaterialVariantData[0]));
        }

        [Test]
        public void FindDefaultIndex_NullArray_ReturnsZero()
        {
            Assert.AreEqual(0, MaterialVariantSelector.FindDefaultIndex(null));
        }

        [Test]
        public void FindDefaultIndex_NoneMarkedDefault_ReturnsZero()
        {
            var variants = new[] { Make("chrome"), Make("matte_black"), Make("silver") };
            Assert.AreEqual(0, MaterialVariantSelector.FindDefaultIndex(variants));
        }

        [Test]
        public void FindDefaultIndex_OneMarkedDefault_ReturnsItsIndex()
        {
            var variants = new[] { Make("chrome"), Make("matte_black", isDefault: true), Make("silver") };
            Assert.AreEqual(1, MaterialVariantSelector.FindDefaultIndex(variants));
        }

        [Test]
        public void TryParseBaseColor_ValidHex_ReturnsTrueAndColor()
        {
            bool ok = MaterialVariantSelector.TryParseBaseColor("#FF0000", out Color color);

            Assert.IsTrue(ok);
            Assert.AreEqual(1f, color.r, 0.01f);
            Assert.AreEqual(0f, color.g, 0.01f);
        }

        [Test]
        public void TryParseBaseColor_InvalidHex_ReturnsFalseAndWhiteFallback()
        {
            bool ok = MaterialVariantSelector.TryParseBaseColor("not-a-color", out Color color);

            Assert.IsFalse(ok);
            Assert.AreEqual(Color.white, color);
        }
    }
}
