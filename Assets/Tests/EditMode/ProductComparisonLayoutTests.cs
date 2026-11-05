using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Placement;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ProductComparisonLayoutTests
    {
        [Test]
        public void SideBySidePositions_AreExactlyGapApartAlongRightAxis()
        {
            Vector3 center = new Vector3(1f, 0.5f, 3f);
            Vector3 rightAxis = Vector3.right;
            float gap = 0.3f;

            Vector3[] positions = ProductComparisonController.SideBySidePositions(center, rightAxis, gap);

            Assert.AreEqual(2, positions.Length);
            Assert.AreEqual(gap, Vector3.Distance(positions[0], positions[1]), 0.0001f);
        }

        [Test]
        public void SideBySidePositions_AreSymmetricAboutCenter()
        {
            Vector3 center = new Vector3(1f, 0.5f, 3f);
            Vector3 rightAxis = Vector3.right;
            float gap = 0.3f;

            Vector3[] positions = ProductComparisonController.SideBySidePositions(center, rightAxis, gap);
            Vector3 midpoint = (positions[0] + positions[1]) * 0.5f;

            Assert.Less(Vector3.Distance(midpoint, center), 0.0001f);
        }

        [Test]
        public void SideBySidePositions_NormalizesANonUnitRightAxis()
        {
            Vector3 center = Vector3.zero;
            Vector3 rightAxis = new Vector3(5f, 0f, 0f); // not unit length
            float gap = 0.4f;

            Vector3[] positions = ProductComparisonController.SideBySidePositions(center, rightAxis, gap);

            Assert.AreEqual(gap, Vector3.Distance(positions[0], positions[1]), 0.0001f);
        }
    }
}
