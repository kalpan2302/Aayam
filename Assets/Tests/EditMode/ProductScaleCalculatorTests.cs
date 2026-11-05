using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Aayam.Models;
using Aayam.Services;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ProductScaleCalculatorTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        // Unity's default cube primitive is exactly 1x1x1 at localScale == Vector3.one.
        private GameObject MakeUnitCube()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            return _root;
        }

        [Test]
        public void TryComputeScaleFactor_MatchingDimensions_ReturnsUnitFactor()
        {
            var cube = MakeUnitCube();
            var dims = new ProductDimensions { WidthMm = 1000f, HeightMm = 1000f, DepthMm = 1000f };

            bool ok = ProductScaleCalculator.TryComputeScaleFactor(cube, dims, out Vector3 factor, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(Vector3.one, factor);
        }

        [Test]
        public void TryComputeScaleFactor_DoubleSizeDims_ReturnsFactorTwo()
        {
            var cube = MakeUnitCube();
            var dims = new ProductDimensions { WidthMm = 2000f, HeightMm = 2000f, DepthMm = 2000f };

            bool ok = ProductScaleCalculator.TryComputeScaleFactor(cube, dims, out Vector3 factor, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(Vector3.one * 2f, factor);
        }

        [Test]
        public void TryComputeScaleFactor_MissingDimensions_ReturnsFalse()
        {
            var cube = MakeUnitCube();

            bool ok = ProductScaleCalculator.TryComputeScaleFactor(cube, null, out _, out string reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void TryComputeScaleFactor_AllZeroDimensions_ReturnsFalse()
        {
            var cube = MakeUnitCube();
            var dims = new ProductDimensions { WidthMm = 0f, HeightMm = 0f, DepthMm = 0f };

            bool ok = ProductScaleCalculator.TryComputeScaleFactor(cube, dims, out _, out string reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void TryComputeScaleFactor_NoRenderers_ReturnsFalse()
        {
            _root = new GameObject("Empty");
            var dims = new ProductDimensions { WidthMm = 1000f };

            bool ok = ProductScaleCalculator.TryComputeScaleFactor(_root, dims, out _, out string reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void TryComputeScaleFactor_AxisMismatch_LogsWarningStillReturnsTrue()
        {
            var cube = MakeUnitCube();
            // Width ratio = 1.0, Height ratio = 2.0 - a 100% disagreement, well past the 15% threshold.
            var dims = new ProductDimensions { WidthMm = 1000f, HeightMm = 2000f };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("per-axis scale ratios disagree"));
            bool ok = ProductScaleCalculator.TryComputeScaleFactor(cube, dims, out Vector3 factor, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(Vector3.one * 1.5f, factor);
        }
    }
}
