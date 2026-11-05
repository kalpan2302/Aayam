using NUnit.Framework;
using Aayam.Utilities;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class PerformanceThresholdsTests
    {
        [Test]
        public void FpsMeetsTarget_ExactlyAtTarget_ReturnsTrue()
        {
            Assert.IsTrue(PerformanceThresholds.FpsMeetsTarget(60f));
        }

        [Test]
        public void FpsMeetsTarget_JustBelowTarget_ReturnsFalse()
        {
            Assert.IsFalse(PerformanceThresholds.FpsMeetsTarget(59.9f));
        }

        [Test]
        public void FpsMeetsTarget_AboveTarget_ReturnsTrue()
        {
            Assert.IsTrue(PerformanceThresholds.FpsMeetsTarget(75f));
        }

        [Test]
        public void MemoryWithinBudget_JustUnderBudget_ReturnsTrue()
        {
            Assert.IsTrue(PerformanceThresholds.MemoryWithinBudget(199));
        }

        [Test]
        public void MemoryWithinBudget_ExactlyAtBudget_ReturnsFalse()
        {
            // "< 200MB", not "<=" - exactly at budget fails, matching the roadmap's strict wording.
            Assert.IsFalse(PerformanceThresholds.MemoryWithinBudget(200));
        }

        [Test]
        public void MemoryWithinBudget_OverBudget_ReturnsFalse()
        {
            Assert.IsFalse(PerformanceThresholds.MemoryWithinBudget(250));
        }
    }
}
