using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Aayam.Core.Bootstrap;
using Aayam.Models;

namespace Aayam.Services
{
    [Serializable]
    internal class MeasurementHistoryManifest
    {
        public List<MeasurementHistoryEntry> Entries = new List<MeasurementHistoryEntry>();
    }

    /// <summary>
    /// Local-first measurement history (Phase 5 Step 7). SaveMeasurement
    /// writes to disk synchronously first - a measurement is never lost to
    /// a flaky network - then best-effort syncs to the backend in the
    /// background (same "try backend, gracefully fall back" pattern as
    /// ProductSelector.Start()). Mirrors ModelCache's exact JsonUtility-
    /// manifest-under-persistentDataPath pattern. Not a MonoBehaviour -
    /// static, like DeviceIdentity, so callers don't need a scene reference.
    /// </summary>
    public static class MeasurementHistoryStore
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "measurement_history.json");

        private static MeasurementHistoryManifest _manifest;

        // Lazily created on first successful backend sync. This pass only
        // calibrates one physical door per session, so one backend door id
        // is enough - multi-door management is a future-phase concern.
        private static string _backendDoorId;

        /// <summary>Test-only escape hatch - skips the backend sync attempt entirely regardless of AppConfig, so EditMode tests exercise only the local JSON/file logic and never make a real network call.</summary>
        public static bool DisableBackendSyncForTests = false;

        /// <summary>Appends the entry to local history immediately, then (if AppConfig.EnableMeasurementBackendSync) fires off a best-effort backend sync.</summary>
        public static void SaveMeasurement(MeasurementHistoryEntry entry)
        {
            EnsureLoaded();
            _manifest.Entries.Add(entry);
            Save();

            var config = AppConfig.Load();
            if (config.EnableMeasurementBackendSync && !DisableBackendSyncForTests)
            {
                SyncToBackend(config, entry);
            }
        }

        /// <summary>All saved entries, oldest first. Pass doorLocalId to scope to one door.</summary>
        public static List<MeasurementHistoryEntry> ListMeasurements(string doorLocalId = null)
        {
            EnsureLoaded();
            if (doorLocalId == null) return new List<MeasurementHistoryEntry>(_manifest.Entries);
            return _manifest.Entries.Where(e => e.DoorLocalId == doorLocalId).ToList();
        }

        // ─── Backend sync (best-effort, fire-and-forget) ─────────────
        // async void, not a discarded Task - matches ProductSelector's
        // established convention for fire-and-forget calls from sync
        // call sites (UI button handlers aren't awaitable).

        private static async void SyncToBackend(AppConfig config, MeasurementHistoryEntry entry)
        {
            try
            {
                var api = new MeasurementApiService(config.ApiBaseUrl, config.ApiKey);
                string deviceId = await api.RegisterDeviceAsync(DeviceIdentity.GetOrCreateDeviceUuid());
                if (deviceId == null)
                {
                    Debug.LogWarning("[MeasurementHistoryStore] Device registration failed - measurement is still saved locally.");
                    return;
                }

                if (_backendDoorId == null)
                {
                    _backendDoorId = await api.CreateDoorAsync(deviceId);
                    if (_backendDoorId == null)
                    {
                        Debug.LogWarning("[MeasurementHistoryStore] Door creation failed - measurement is still saved locally.");
                        return;
                    }
                }

                var properties = new DoorProperties
                {
                    WidthMm = entry.WidthMm,
                    HeightMm = entry.HeightMm,
                    ThicknessMm = entry.ThicknessMm,
                    WidthConfidence = entry.WidthConfidence,
                    HeightConfidence = entry.HeightConfidence,
                    ThicknessConfidence = entry.ThicknessConfidence,
                    Source = entry.Source,
                };
                bool saved = await api.SaveMeasurementAsync(_backendDoorId, properties);
                Debug.Log(saved
                    ? $"[MeasurementHistoryStore] Synced measurement to backend (door {_backendDoorId})."
                    : "[MeasurementHistoryStore] Backend sync failed - measurement is still saved locally.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MeasurementHistoryStore] Backend sync failed: {ex.Message} — measurement is still saved locally.");
            }
        }

        // ─── Manifest I/O ─────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_manifest != null) return;

            if (!File.Exists(FilePath))
            {
                _manifest = new MeasurementHistoryManifest();
                return;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                _manifest = JsonUtility.FromJson<MeasurementHistoryManifest>(json) ?? new MeasurementHistoryManifest();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MeasurementHistoryStore] Manifest load failed: {ex.Message} — starting fresh.");
                _manifest = new MeasurementHistoryManifest();
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_manifest, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MeasurementHistoryStore] Manifest save failed: {ex.Message}");
            }
        }

        /// <summary>Test-only: forces the next EnsureLoaded to re-read from disk.</summary>
        public static void ResetInMemoryStateForTests()
        {
            _manifest = null;
            _backendDoorId = null;
        }
    }
}
