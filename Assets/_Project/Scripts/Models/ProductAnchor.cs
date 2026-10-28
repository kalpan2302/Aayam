using System;
using UnityEngine;

namespace Aayam.Models
{
    /// <summary>
    /// Phase 4 Step 3: a product's mounting point, door-space (meters,
    /// X=width Y=height Z=depth - DoorCoordinateSystem's convention).
    /// Mirrors docs/phases/phase-4-door-coordinate-system.md Step 3's JSON
    /// example (there expressed in mm; meters here, Unity's native unit).
    /// </summary>
    [Serializable]
    public struct ProductAnchor
    {
        public string ProductId;
        public Vector3 DoorSpacePosition;
        public Vector3 RotationEulerDegrees;
        public MountingSurface MountingSurface;
    }

    public enum MountingSurface { Front, Back, Edge }
}
