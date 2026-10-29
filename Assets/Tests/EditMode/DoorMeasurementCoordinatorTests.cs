using NUnit.Framework;
using Aayam.AR.Core;
using Aayam.AR.Measurement;
using Aayam.Models;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorMeasurementCoordinatorTests
    {
        [Test]
        public void ShouldUseDepthSensor_NoEnvironmentDepth_ReturnsFalse()
        {
            Assert.IsFalse(DoorMeasurementCoordinator.ShouldUseDepthSensor(hasEnvironmentDepth: false));
        }

        [Test]
        public void ShouldUseDepthSensor_HasEnvironmentDepth_ReturnsTrue()
        {
            Assert.IsTrue(DoorMeasurementCoordinator.ShouldUseDepthSensor(hasEnvironmentDepth: true));
        }

        [Test]
        public void SourceForTier_Ultra_IsLiDAR()
        {
            Assert.AreEqual(MeasurementSource.LiDAR, DoorMeasurementCoordinator.SourceForTier(DeviceQualityTier.Ultra));
        }

        [Test]
        public void SourceForTier_High_IsARDepth()
        {
            Assert.AreEqual(MeasurementSource.ARDepth, DoorMeasurementCoordinator.SourceForTier(DeviceQualityTier.High));
        }

        [Test]
        public void SourceForTier_MediumOrLow_IsARDepth()
        {
            Assert.AreEqual(MeasurementSource.ARDepth, DoorMeasurementCoordinator.SourceForTier(DeviceQualityTier.Medium));
            Assert.AreEqual(MeasurementSource.ARDepth, DoorMeasurementCoordinator.SourceForTier(DeviceQualityTier.Low));
        }

        [Test]
        public void ConfidenceForSource_LiDAR_WithinRoadmapBand()
        {
            float confidence = DoorMeasurementCoordinator.ConfidenceForSource(MeasurementSource.LiDAR);
            Assert.GreaterOrEqual(confidence, 0.90f);
            Assert.LessOrEqual(confidence, 0.95f);
        }

        [Test]
        public void ConfidenceForSource_ARDepth_WithinRoadmapBand()
        {
            float confidence = DoorMeasurementCoordinator.ConfidenceForSource(MeasurementSource.ARDepth);
            Assert.GreaterOrEqual(confidence, 0.75f);
            Assert.LessOrEqual(confidence, 0.85f);
        }
    }
}
