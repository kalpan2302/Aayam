using NUnit.Framework;
using UnityEngine;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class PlacementTests
    {
        [Test]
        public void FlushPose_ForwardFacingWall_ForwardIsNormal()
        {
            Vector3 hitPoint = new Vector3(0, 1, 2);
            Vector3 normal   = Vector3.back;

            Quaternion rot = CalculateFlushRotation(normal);
            Vector3 fwd = rot * Vector3.forward;

            Assert.AreEqual(normal.x, fwd.x, 0.01f);
            Assert.AreEqual(normal.y, fwd.y, 0.01f);
            Assert.AreEqual(normal.z, fwd.z, 0.01f);
        }

        [Test]
        public void FlushPose_SideWall_CorrectOrientation()
        {
            Vector3 normal = Vector3.right;
            Quaternion rot = CalculateFlushRotation(normal);
            Vector3 fwd = rot * Vector3.forward;

            Assert.AreEqual(1f, fwd.x, 0.01f);
            Assert.AreEqual(0f, fwd.y, 0.01f);
            Assert.AreEqual(0f, fwd.z, 0.01f);
        }

        [Test]
        public void FlushPose_UpVectorIsGravityAligned()
        {
            Vector3 normal = new Vector3(0.5f, 0f, 0.866f).normalized;
            Quaternion rot = CalculateFlushRotation(normal);
            Vector3 up = rot * Vector3.up;

            Assert.AreEqual(0f, up.x, 0.1f);
            Assert.AreEqual(1f, up.y, 0.1f, "Up vector must point along gravity");
            Assert.AreEqual(0f, up.z, 0.1f);
        }

        [TestCase(0.95f, "handle",   1.00f, true,  Description = "Near handle height -> snap")]
        [TestCase(1.05f, "handle",   1.00f, true,  Description = "Near handle height -> snap")]
        [TestCase(1.20f, "handle",   1.20f, false, Description = "Far from handle -> no snap")]
        [TestCase(1.45f, "peephole", 1.50f, true,  Description = "Near peephole -> snap")]
        [TestCase(1.35f, "peephole", 1.50f, true,  Description = "Near peephole -> snap")]
        [TestCase(1.10f, "peephole", 1.10f, false, Description = "Far from peephole -> no snap")]
        public void HeightSnap_VariousPositions(float inputY, string category, float expectedY, bool shouldSnap)
        {
            float snappedY = ApplyHeightSnap(inputY, category, 0.08f);

            if (shouldSnap)
                Assert.AreEqual(expectedY, snappedY, 0.001f, $"Should snap {inputY} -> {expectedY} for {category}");
            else
                Assert.AreEqual(inputY, snappedY, 0.001f, $"Should NOT snap {inputY} for {category}");
        }

        [Test]
        public void UndoRedo_PlaceAndUndo_RemovesObject()
        {
            var stack = new SimpleCommandStack(maxSize: 10);
            var placed = new PlaceRecord("obj1");
            stack.Execute(placed);
            Assert.IsTrue(placed.IsPlaced);

            stack.Undo();
            Assert.IsFalse(placed.IsPlaced, "After undo, object should be removed");
        }

        [Test]
        public void UndoRedo_UndoThenRedo_RestoresObject()
        {
            var stack = new SimpleCommandStack(maxSize: 10);
            var placed = new PlaceRecord("obj1");

            stack.Execute(placed);
            stack.Undo();
            stack.Redo();

            Assert.IsTrue(placed.IsPlaced, "After redo, object should be restored");
        }

        [Test]
        public void UndoRedo_CannotUndoPastEmpty()
        {
            var stack = new SimpleCommandStack(maxSize: 10);
            stack.Undo();
            Assert.Pass("Undo on empty stack does not crash");
        }

        [Test]
        public void UndoRedo_MaxSizeEvictsOldest()
        {
            var stack = new SimpleCommandStack(maxSize: 3);

            var r1 = new PlaceRecord("1"); stack.Execute(r1);
            var r2 = new PlaceRecord("2"); stack.Execute(r2);
            var r3 = new PlaceRecord("3"); stack.Execute(r3);
            var r4 = new PlaceRecord("4"); stack.Execute(r4);

            stack.Undo(); Assert.IsFalse(r4.IsPlaced);
            stack.Undo(); Assert.IsFalse(r3.IsPlaced);
            stack.Undo(); Assert.IsFalse(r2.IsPlaced);
            Assert.IsTrue(r1.IsPlaced, "Evicted command should not be undoable");
        }

        private static Quaternion CalculateFlushRotation(Vector3 surfaceNormal)
        {
            Vector3 projectedUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;
            if (projectedUp.sqrMagnitude < 0.001f) projectedUp = Vector3.up;
            return Quaternion.LookRotation(surfaceNormal, projectedUp);
        }

        private static float ApplyHeightSnap(float currentY, string category, float threshold)
        {
            float target = category == "peephole" ? 1.5f : 1.0f;
            return Mathf.Abs(currentY - target) < threshold ? target : currentY;
        }

        private class PlaceRecord
        {
            public string Id;
            public bool IsPlaced;
            public PlaceRecord(string id) { Id = id; IsPlaced = false; }
        }

        private class SimpleCommandStack
        {
            private readonly System.Collections.Generic.List<(PlaceRecord rec, bool prevState)> _history = new();
            private int _pointer = -1;
            private readonly int _maxSize;

            public SimpleCommandStack(int maxSize) { _maxSize = maxSize; }

            public void Execute(PlaceRecord rec)
            {
                if (_pointer < _history.Count - 1)
                    _history.RemoveRange(_pointer + 1, _history.Count - _pointer - 1);

                if (_history.Count >= _maxSize)
                    _history.RemoveAt(0);
                else
                    _pointer++;

                _history.Add((rec, rec.IsPlaced));
                rec.IsPlaced = true;
                _pointer = _history.Count - 1;
            }

            public void Undo()
            {
                if (_pointer < 0) return;
                _history[_pointer].rec.IsPlaced = _history[_pointer].prevState;
                _pointer--;
            }

            public void Redo()
            {
                if (_pointer >= _history.Count - 1) return;
                _pointer++;
                _history[_pointer].rec.IsPlaced = true;
            }
        }
    }
}
