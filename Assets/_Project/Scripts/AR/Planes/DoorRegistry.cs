using System.Collections.Generic;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 4: "Support for multiple doors in the future by
    /// maintaining a list of active DoorCoordinateSystem instances." Static
    /// by design (mirrors ModelCache's own static-registry pattern elsewhere
    /// in this codebase) - a single AR session has one of these regardless
    /// of scene/screen navigation.
    ///
    /// Call Clear() between EditMode test runs and between Play Mode
    /// sessions that shouldn't share state - a static dictionary otherwise
    /// leaks entries across both, which is exactly the kind of silent
    /// cross-test contamination this project's testing discipline exists to
    /// avoid.
    /// </summary>
    public static class DoorRegistry
    {
        private static readonly Dictionary<string, DoorCoordinateSystem> _doors = new();

        public static int Count => _doors.Count;
        public static IReadOnlyCollection<string> ActiveDoorIds => _doors.Keys;

        public static void Register(string doorId, DoorCoordinateSystem door)
        {
            if (string.IsNullOrEmpty(doorId) || door == null) return;
            _doors[doorId] = door;
        }

        public static void Unregister(string doorId)
        {
            if (string.IsNullOrEmpty(doorId)) return;
            _doors.Remove(doorId);
        }

        public static DoorCoordinateSystem Get(string doorId)
        {
            _doors.TryGetValue(doorId, out var door);
            return door;
        }

        public static bool Contains(string doorId) => _doors.ContainsKey(doorId);

        public static void Clear() => _doors.Clear();
    }
}
