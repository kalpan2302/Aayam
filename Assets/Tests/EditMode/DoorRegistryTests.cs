using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DoorRegistryTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            // DoorRegistry is static - without this, entries leak across
            // tests and across Play Mode sessions (see its own class doc).
            DoorRegistry.Clear();
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        /// <summary>
        /// DoorCoordinateSystem now self-registers under its default "front-door"
        /// id in Awake() (2026-10-21 fix - registration has to happen at
        /// runtime, not from the Editor bootstrapper, or it never runs
        /// on-device at all). Clearing right after construction undoes that
        /// side effect so these tests can still control ids explicitly,
        /// rather than leaking a surprise "front-door" entry into every test.
        /// </summary>
        private DoorCoordinateSystem NewDoor()
        {
            var go = new GameObject("TestDoor");
            _spawned.Add(go);
            var door = go.AddComponent<DoorCoordinateSystem>();
            // Unregister only this door's own auto-registration - a blanket
            // Clear() here wiped out any OTHER doors a test had already
            // registered earlier in the same test body (caught 2026-10-21:
            // Register_MultipleDoors_AllTracked called NewDoor() twice, and
            // the second call's Clear() was deleting the first door's
            // "door-a" entry before the test could assert on it).
            DoorRegistry.Unregister("front-door");
            return door;
        }

        [Test]
        public void Register_ThenGet_ReturnsSameInstance()
        {
            var door = NewDoor();
            DoorRegistry.Register("front-door", door);

            Assert.AreSame(door, DoorRegistry.Get("front-door"));
            Assert.AreEqual(1, DoorRegistry.Count);
        }

        [Test]
        public void Register_MultipleDoors_AllTracked()
        {
            DoorRegistry.Register("door-a", NewDoor());
            DoorRegistry.Register("door-b", NewDoor());

            Assert.AreEqual(2, DoorRegistry.Count);
            Assert.IsTrue(DoorRegistry.Contains("door-a"));
            Assert.IsTrue(DoorRegistry.Contains("door-b"));
        }

        [Test]
        public void Unregister_RemovesDoor()
        {
            DoorRegistry.Register("door-a", NewDoor());
            DoorRegistry.Unregister("door-a");

            Assert.IsFalse(DoorRegistry.Contains("door-a"));
            Assert.IsNull(DoorRegistry.Get("door-a"));
        }

        [Test]
        public void Register_NullOrEmptyId_Ignored()
        {
            DoorRegistry.Register("", NewDoor());
            DoorRegistry.Register(null, NewDoor());
            Assert.AreEqual(0, DoorRegistry.Count);
        }

        /// <summary>
        /// NOT independently confirmed passing in-session (2026-10-21) - the
        /// available tool for running C# in the Editor executes synchronously
        /// with no player-loop tick in between, and AddComponent's Awake()
        /// call appeared not to fire within that single call (verified via a
        /// two-call probe: a GameObject created in one call was still present
        /// in a second, separate call, but DoorRegistry was still empty -
        /// ambiguous between genuine Editor-only Awake deferral and the
        /// tool's own lifecycle isolation, couldn't tell which from here).
        /// Standard Unity behavior (relied on by every other MonoBehaviour's
        /// Awake/OnEnable in this codebase, e.g. AnchorController,
        /// DoorPnPWorldAnchor) says this should just work in Play Mode/on a
        /// device. Run via Window > General > Test Runner in the Editor UI
        /// for an authoritative answer if full confidence is needed before
        /// relying on this.
        /// </summary>
        [Test]
        public void DoorCoordinateSystem_SelfRegisters_OnAwake()
        {
            // Deliberately NOT using the NewDoor() helper here - that helper
            // clears the registry right after creation specifically to undo
            // this behavior for the other tests above. This test exists to
            // confirm the behavior those tests work around actually happens.
            var go = new GameObject("TestDoor");
            _spawned.Add(go);
            var door = go.AddComponent<DoorCoordinateSystem>();

            Assert.AreEqual(1, DoorRegistry.Count);
            Assert.AreSame(door, DoorRegistry.Get("front-door"));
        }

        [Test]
        public void DoorCoordinateSystem_Unregisters_OnDestroy()
        {
            var go = new GameObject("TestDoor");
            var door = go.AddComponent<DoorCoordinateSystem>();
            Assert.AreEqual(1, DoorRegistry.Count);

            Object.DestroyImmediate(go);
            Assert.AreEqual(0, DoorRegistry.Count);
        }
    }
}
