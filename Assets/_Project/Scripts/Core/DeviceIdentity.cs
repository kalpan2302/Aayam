using System;
using System.IO;
using UnityEngine;

namespace Aayam.Core.Bootstrap
{
    [Serializable]
    internal class DeviceIdentityRecord
    {
        public string DeviceUuid;
    }

    /// <summary>
    /// Lightweight client-generated device identity (not a real user account -
    /// see Phase 5 plan). Generates a UUID once and persists it under
    /// Application.persistentDataPath, same JsonUtility-file pattern as
    /// ModelCache's manifest.
    /// </summary>
    public static class DeviceIdentity
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "device_identity.json");

        private static string _cachedUuid;

        /// <summary>Returns this device's UUID, generating and persisting one on first call.</summary>
        public static string GetOrCreateDeviceUuid()
        {
            if (!string.IsNullOrEmpty(_cachedUuid)) return _cachedUuid;

            if (File.Exists(FilePath))
            {
                try
                {
                    var record = JsonUtility.FromJson<DeviceIdentityRecord>(File.ReadAllText(FilePath));
                    if (record != null && !string.IsNullOrEmpty(record.DeviceUuid))
                    {
                        _cachedUuid = record.DeviceUuid;
                        return _cachedUuid;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeviceIdentity] Read failed: {ex.Message} — generating a new UUID.");
                }
            }

            _cachedUuid = Guid.NewGuid().ToString();
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(new DeviceIdentityRecord { DeviceUuid = _cachedUuid }));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeviceIdentity] Write failed: {ex.Message}");
            }

            return _cachedUuid;
        }
    }
}
