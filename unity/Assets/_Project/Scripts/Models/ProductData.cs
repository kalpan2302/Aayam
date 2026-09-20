namespace Aayam.Models
{
    /// <summary>
    /// Represents a hardware product that can be visualized in AR.
    /// </summary>
    [System.Serializable]
    public class ProductData
    {
        // ─── Identity ─────────────────────────────────────────────
        public string ProductId;
        public string Name;
        public string Brand;
        public string SKU;
        public string Category; // "handle", "lock", "accessory"

        // ─── Pricing ──────────────────────────────────────────────
        public float Price;
        public string Currency = "INR";

        // ─── Physical Dimensions (in millimeters) ─────────────────
        public ProductDimensions Dimensions;

        // ─── Door Compatibility ───────────────────────────────────
        public DoorCompatibility Compatibility;

        // ─── 3D Asset ─────────────────────────────────────────────
        public ProductAsset Asset;

        // ─── Display ──────────────────────────────────────────────
        public string Description;
        public string[] Features;
        public string ThumbnailUrl;
    }

    [System.Serializable]
    public class ProductDimensions
    {
        public float WidthMm;
        public float HeightMm;
        public float DepthMm;
        public float WeightGrams;
    }

    [System.Serializable]
    public class DoorCompatibility
    {
        public float MinThicknessMm;
        public float MaxThicknessMm;
        public string[] SupportedMaterials; // "wood", "metal", "glass"
        public string[] SupportedDoorTypes; // "single", "double"
        public string[] OpeningDirections;  // "left", "right"
        public string MountingType;         // "mortise", "cylindrical", "surface"
        public float BoreDiameterMm;
        public float BacksetMm;
    }

    [System.Serializable]
    public class ProductAsset
    {
        public string ModelUrl;      // S3 URL to .glb file
        public string ThumbnailUrl;  // S3 URL to thumbnail image

        /// <summary>
        /// Anchor point relative to the door surface (in mm).
        /// X = horizontal offset from door edge
        /// Y = vertical offset from floor
        /// Z = depth offset from door surface (usually 0)
        /// </summary>
        public AnchorPoint Anchor;
    }

    [System.Serializable]
    public class AnchorPoint
    {
        public float XMm;
        public float YMm;
        public float ZMm;
    }
}
