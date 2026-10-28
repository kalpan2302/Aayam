using UnityEngine;
using Aayam.Models;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 4 Step 3: "Auto-placement: Instantly position hardware based on
    /// its type in standard zones." Pure logic, no AR/scene dependency -
    /// placement correctness only depends on DoorComponentZones (Step 2,
    /// already tested), not on Step 1's coordinate frame being on-device
    /// verified. Built 2026-10-19 ahead of that verification, at Kalpan's
    /// explicit instruction - see phase-4-implementation-plan.md's
    /// "What's actually left" item 3 for the acknowledged risk.
    /// </summary>
    public static class ProductAutoPlacer
    {
        /// <summary>
        /// Defaults to the target zone's center. For a degenerate zone
        /// (Peephole's X, Hinge's X - MinX==MaxX by construction, see
        /// DoorComponentZones) the "center" is just that fixed value, which
        /// is exactly what's wanted.
        /// </summary>
        public static ProductAnchor AutoPlace(
            string productId,
            DoorComponentZones.ZoneType zoneType,
            float doorWidthMeters,
            float doorHeightMeters,
            bool hingeOnLeft)
        {
            var zone = DoorComponentZones.GetZone(zoneType, doorWidthMeters, doorHeightMeters, hingeOnLeft);
            float x = (zone.MinX + zone.MaxX) * 0.5f;
            float y = (zone.MinY + zone.MaxY) * 0.5f;

            return new ProductAnchor
            {
                ProductId = productId,
                DoorSpacePosition = new Vector3(x, y, 0f),
                RotationEulerDegrees = Vector3.zero,
                MountingSurface = MountingSurface.Front,
            };
        }
    }
}
