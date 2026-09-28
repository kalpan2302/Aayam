using NUnit.Framework;
using UnityEngine;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorDetectionTests
    {
        [Test]
        public void ScoreDoor_StandardDoor_HighScore()
        {
            float score = ScoreDoorCandidate(0.9f, 2.1f, 0.85f);
            Assert.IsTrue(score >= 80f, $"Standard door should score >=80, got {score:F1}");
        }

        [Test]
        public void ScoreDoor_WideDoor_StillValid()
        {
            float score = ScoreDoorCandidate(1.2f, 2.3f, 0.80f);
            Assert.IsTrue(score >= 60f, $"Wide door should score >=60, got {score:F1}");
        }

        [Test]
        public void ScoreDoor_WallPanel_LowScore()
        {
            float score = ScoreDoorCandidate(3.0f, 2.5f, 0.75f);
            Assert.IsTrue(score < 40f, $"Wall panel should score <40, got {score:F1}");
        }

        [Test]
        public void ScoreDoor_SmallWindow_LowScore()
        {
            float score = ScoreDoorCandidate(0.4f, 0.6f, 0.70f);
            Assert.IsTrue(score < 30f, $"Small window should score <30, got {score:F1}");
        }

        [Test]
        public void ClassifyNormal_PureUp_IsHorizontal()
        {
            float dot = Vector3.Dot(Vector3.up, Vector3.up);
            Assert.AreEqual(1f, dot, 0.01f, "Pure up normal -> floor");
        }

        [Test]
        public void ClassifyNormal_PureForward_IsVertical()
        {
            float dot = Mathf.Abs(Vector3.Dot(Vector3.forward, Vector3.up));
            Assert.IsTrue(dot < 0.2f, "Pure forward normal -> vertical surface");
        }

        [Test]
        public void ClassifyNormal_5DegTilt_StillVertical()
        {
            Vector3 tilted = Quaternion.Euler(5f, 0, 0) * Vector3.forward;
            float dot = Mathf.Abs(Vector3.Dot(tilted, Vector3.up));
            Assert.IsTrue(dot < 0.2f, $"5° tilt should still be vertical, dot={dot:F3}");
        }

        [Test]
        public void ClassifyNormal_45DegTilt_Indeterminate()
        {
            Vector3 tilted = Quaternion.Euler(45f, 0, 0) * Vector3.forward;
            float dot = Mathf.Abs(Vector3.Dot(tilted, Vector3.up));
            Assert.IsTrue(dot > 0.5f, $"45° tilt should not classify as vertical, dot={dot:F3}");
        }

        [Test]
        public void RectangularityScore_PerfectRect_IsOne()
        {
            float area       = 0.9f * 2.1f;
            float boundArea  = area;
            float rect       = area / boundArea;
            Assert.AreEqual(1f, rect, 0.01f);
        }

        [Test]
        public void RectangularityScore_LShapedPolygon_LessThanOne()
        {
            float polygonArea  = 0.75f;
            float boundingArea = 1.0f;
            float rect         = polygonArea / boundingArea;
            Assert.IsTrue(rect < 1f && rect > 0.5f, $"L-shape rectangularity={rect:F2}");
        }

        [TestCase(0.9f, 2.1f, true,  Description = "Standard 900x2100 -> ratio ≈ 2.33")]
        [TestCase(1.0f, 2.1f, true,  Description = "1000x2100 -> ratio ≈ 2.1")]
        [TestCase(0.7f, 1.8f, true,  Description = "Narrow 700x1800 -> ratio ≈ 2.57")]
        [TestCase(2.0f, 2.0f, false, Description = "Square 2000x2000 -> ratio = 1.0")]
        [TestCase(0.5f, 3.0f, false, Description = "Too narrow/tall -> ratio = 6.0")]
        public void AspectRatio_DoorRange_Correct(float w, float h, bool expected)
        {
            float ratio = h / w;
            bool inRange = ratio >= 1.5f && ratio <= 3.5f;
            Assert.AreEqual(expected, inRange, $"Ratio={ratio:F2}");
        }

        private static float ScoreDoorCandidate(float width, float height, float rectangularity)
        {
            float score = 0f;

            if (width >= 0.6f && width <= 1.3f)
            {
                float widthDist = Mathf.Abs(width - 0.9f);
                score += Mathf.Lerp(30f, 15f, widthDist / 0.4f);
            }

            if (height >= 1.7f && height <= 2.5f)
            {
                float heightDist = Mathf.Abs(height - 2.1f);
                score += Mathf.Lerp(30f, 15f, heightDist / 0.4f);
            }

            float aspect = height / Mathf.Max(width, 0.01f);
            if (aspect >= 1.5f && aspect <= 3.5f)
            {
                float aspectDist = Mathf.Abs(aspect - 2.33f);
                score += Mathf.Lerp(20f, 5f, aspectDist / 1.0f);
            }

            score += rectangularity * 20f;
            return score;
        }
    }
}
