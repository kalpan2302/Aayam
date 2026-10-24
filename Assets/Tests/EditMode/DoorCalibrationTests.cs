using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorCalibrationTests
    {
        [Test]
        public void ComputeScaleCorrection_MeasuredLargerThanAssumed_GivesFactorAboveOne()
        {
            // Matches this session's real door: assumed default 0.9m, actual 0.7874m.
            var r = DoorCalibration.ComputeScaleCorrection(assumedSizeMeters: 0.9f, measuredSizeMeters: 0.7874f);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0.7874f / 0.9f, r.ScaleCorrection, 1e-5f);
            Assert.Less(r.ScaleCorrection, 1f); // measured is smaller here, so factor < 1
        }

        [Test]
        public void ComputeScaleCorrection_EqualSizes_GivesUnityFactor()
        {
            var r = DoorCalibration.ComputeScaleCorrection(0.9f, 0.9f);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1f, r.ScaleCorrection, 1e-6f);
        }

        [Test]
        public void ComputeScaleCorrection_NonPositiveInput_FailsCleanly()
        {
            Assert.IsFalse(DoorCalibration.ComputeScaleCorrection(0f, 0.9f).Ok);
            Assert.IsFalse(DoorCalibration.ComputeScaleCorrection(0.9f, -1f).Ok);
        }

        [Test]
        public void PlacementErrorMeters_MatchesEuclideanDistance()
        {
            float err = DoorCalibration.PlacementErrorMeters(new Vector3(0, 0, 0), new Vector3(0.003f, 0.004f, 0f));
            Assert.AreEqual(0.005f, err, 1e-6f); // 3-4-5 triangle
        }

        [Test]
        public void MeetsAccuracyTarget_RespectsRoadmapFiveMillimeterBar()
        {
            Assert.IsTrue(DoorCalibration.MeetsAccuracyTarget(0.0049f));
            Assert.IsFalse(DoorCalibration.MeetsAccuracyTarget(0.0051f));
        }
    }
}
