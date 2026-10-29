using System;

namespace Aayam.Models
{
    /// <summary>
    /// A persisted measurement record (Phase 5 Step 7). Unlike DoorProperties
    /// (transient, compatibility-check-oriented - overwritten on every new
    /// scan), this is append-only history: one entry per confirmed
    /// measurement, grouped by DoorLocalId.
    /// </summary>
    [Serializable]
    public class MeasurementHistoryEntry
    {
        public string DoorLocalId;   // System.Guid.ToString() - JsonUtility can't serialize Guid directly
        public string TimestampUtc;  // ISO-8601, same convention as ModelCacheEntry

        public float WidthMm;
        public float HeightMm;
        public float ThicknessMm;

        public float WidthConfidence;
        public float HeightConfidence;
        public float ThicknessConfidence;

        public MeasurementSource Source;
    }
}
