namespace Aayam.Models
{
    /// <summary>
    /// A selectable finish for a product (Phase 6 Step 6) - swappable at
    /// runtime in AR without leaving the scene. Distinct from
    /// DoorProperties.Material (a detected door's own material, unrelated)
    /// and from ProductData's single catalog finish - this represents one
    /// of possibly several finishes the SAME placed instance can switch to.
    /// </summary>
    [System.Serializable]
    public class MaterialVariantData
    {
        public string FinishName;
        public string BaseColorHex;   // "#RRGGBB" or "#RRGGBBAA"
        public float MetallicFactor;
        public float RoughnessFactor;
        public string ModelUrl;       // null/empty = reuse the parent product's own model
        public bool IsDefault;
    }
}
