namespace Aayam.Models
{
    /// <summary>
    /// Represents the result of a compatibility check between a product and a door.
    /// </summary>
    [System.Serializable]
    public class CompatibilityResult
    {
        public OverallStatus Status;
        public float Confidence; // 0.0 to 1.0
        public CompatibilityCheck[] Checks;
        public string[] Warnings;
        public string[] Recommendations;
    }

    public enum OverallStatus
    {
        Compatible,      // 🟢
        NeedsVerification, // 🟡
        Incompatible     // 🔴
    }

    [System.Serializable]
    public class CompatibilityCheck
    {
        public string Rule;
        public CheckStatus Status;
        public string Detail;
    }

    public enum CheckStatus
    {
        Pass,
        Warning,
        Fail,
        Unknown
    }

    /// <summary>
    /// Represents the scanned door's detected properties.
    /// Used as input to the compatibility engine.
    /// </summary>
    [System.Serializable]
    public class DoorProperties
    {
        // ─── Dimensions (in mm) ───────────────────────────────────
        public float WidthMm;
        public float HeightMm;
        public float ThicknessMm;

        // ─── Confidence of measurements (0-1) ────────────────────
        public float WidthConfidence;
        public float HeightConfidence;
        public float ThicknessConfidence;

        // ─── Characteristics ──────────────────────────────────────
        public string Material;          // "wood", "metal", "glass"
        public string OpeningDirection;  // "left", "right"
        public string DoorType;          // "single", "double", "sliding"

        // ─── Existing hardware positions (mm from floor/edge) ─────
        public float? ExistingHandleHeightMm;
        public float? ExistingLockHeightMm;
        public float? ExistingBoreDiameterMm;

        // ─── Source of measurement ────────────────────────────────
        public MeasurementSource Source;
    }

    public enum MeasurementSource
    {
        LiDAR,
        ARDepth,
        CVMonocular,
        UserAssisted,
        ManualInput
    }
}

