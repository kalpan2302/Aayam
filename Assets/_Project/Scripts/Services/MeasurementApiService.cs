using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Aayam.Models;

namespace Aayam.Services
{
    // ─── Wire DTOs ──────────────────────────────────────────────────
    // Mirror the backend's JSON shape (snake_case). Decimal fields come back
    // as strings (Pydantic's default Decimal JSON encoding, same convention
    // ProductApiService already relies on) but are sent as plain numbers -
    // Pydantic's Decimal fields accept either on the way in.

    [Serializable]
    internal class DeviceRegisterRequestDto
    {
        [JsonProperty("device_uuid")] public string DeviceUuid;
    }

    [Serializable]
    internal class DeviceResponseDto
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("device_uuid")] public string DeviceUuid;
    }

    [Serializable]
    internal class DoorCreateRequestDto
    {
        [JsonProperty("room_label")] public string RoomLabel;
        [JsonProperty("project_label")] public string ProjectLabel;
    }

    [Serializable]
    internal class DoorResponseDto
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("device_id")] public string DeviceId;
    }

    [Serializable]
    internal class DoorMeasurementCreateDto
    {
        [JsonProperty("width_mm")] public float? WidthMm;
        [JsonProperty("height_mm")] public float? HeightMm;
        [JsonProperty("thickness_mm")] public float? ThicknessMm;
        [JsonProperty("width_confidence")] public float? WidthConfidence;
        [JsonProperty("height_confidence")] public float? HeightConfidence;
        [JsonProperty("thickness_confidence")] public float? ThicknessConfidence;
        [JsonProperty("source")] public string Source;
        [JsonProperty("material")] public string Material;
        [JsonProperty("opening_direction")] public string OpeningDirection;
        [JsonProperty("door_type")] public string DoorType;
        [JsonProperty("existing_handle_height_mm")] public float? ExistingHandleHeightMm;
        [JsonProperty("existing_lock_height_mm")] public float? ExistingLockHeightMm;
        [JsonProperty("existing_bore_diameter_mm")] public float? ExistingBoreDiameterMm;
    }

    [Serializable]
    internal class DoorMeasurementResponseDto
    {
        [JsonProperty("width_mm")] public string WidthMm;
        [JsonProperty("height_mm")] public string HeightMm;
        [JsonProperty("thickness_mm")] public string ThicknessMm;
        [JsonProperty("width_confidence")] public string WidthConfidence;
        [JsonProperty("height_confidence")] public string HeightConfidence;
        [JsonProperty("thickness_confidence")] public string ThicknessConfidence;
        [JsonProperty("source")] public string Source;
        [JsonProperty("material")] public string Material;
        [JsonProperty("opening_direction")] public string OpeningDirection;
        [JsonProperty("door_type")] public string DoorType;
        [JsonProperty("existing_handle_height_mm")] public string ExistingHandleHeightMm;
        [JsonProperty("existing_lock_height_mm")] public string ExistingLockHeightMm;
        [JsonProperty("existing_bore_diameter_mm")] public string ExistingBoreDiameterMm;
    }

    /// <summary>
    /// Talks to the Phase 5 measurement endpoints (devices/doors/measurements).
    /// Gated by the backend's verify_api_key dependency, unlike ProductApiService's
    /// routes - every call here sends X-API-Key. Not a MonoBehaviour - construct
    /// directly, e.g. new MeasurementApiService(config.ApiBaseUrl, config.ApiKey).
    /// </summary>
    public class MeasurementApiService
    {
        private readonly string _apiBaseUrl;
        private readonly string _apiKey;

        public MeasurementApiService(string apiBaseUrl, string apiKey)
        {
            _apiBaseUrl = (apiBaseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
        }

        /// <summary>Registers (or touches) this device. Returns the backend's device id, or null on failure.</summary>
        public async Task<string> RegisterDeviceAsync(string deviceUuid)
        {
            var body = new DeviceRegisterRequestDto { DeviceUuid = deviceUuid };
            string json = await PostStringAsync($"{_apiBaseUrl}/api/v1/devices/register", body);
            if (json == null) return null;

            try { return JsonConvert.DeserializeObject<DeviceResponseDto>(json)?.Id; }
            catch (Exception ex)
            {
                Debug.LogError($"[MeasurementApiService] Failed to parse device register response: {ex.Message}");
                return null;
            }
        }

        /// <summary>Creates a door scoped to the given device id. Returns the backend's door id, or null on failure.</summary>
        public async Task<string> CreateDoorAsync(string deviceId, string roomLabel = null, string projectLabel = null)
        {
            var body = new DoorCreateRequestDto { RoomLabel = roomLabel, ProjectLabel = projectLabel };
            string url = $"{_apiBaseUrl}/api/v1/doors/?device_id={UnityWebRequest.EscapeURL(deviceId)}";
            string json = await PostStringAsync(url, body);
            if (json == null) return null;

            try { return JsonConvert.DeserializeObject<DoorResponseDto>(json)?.Id; }
            catch (Exception ex)
            {
                Debug.LogError($"[MeasurementApiService] Failed to parse door create response: {ex.Message}");
                return null;
            }
        }

        /// <summary>Saves a measurement for a door. Returns true on success.</summary>
        public async Task<bool> SaveMeasurementAsync(string doorId, DoorProperties properties)
        {
            var body = new DoorMeasurementCreateDto
            {
                WidthMm = NullIfZero(properties.WidthMm),
                HeightMm = NullIfZero(properties.HeightMm),
                ThicknessMm = NullIfZero(properties.ThicknessMm),
                WidthConfidence = NullIfZero(properties.WidthConfidence),
                HeightConfidence = NullIfZero(properties.HeightConfidence),
                ThicknessConfidence = NullIfZero(properties.ThicknessConfidence),
                Source = ToWireSource(properties.Source),
                Material = properties.Material,
                OpeningDirection = properties.OpeningDirection,
                DoorType = properties.DoorType,
                ExistingHandleHeightMm = properties.ExistingHandleHeightMm,
                ExistingLockHeightMm = properties.ExistingLockHeightMm,
                ExistingBoreDiameterMm = properties.ExistingBoreDiameterMm,
            };

            string url = $"{_apiBaseUrl}/api/v1/doors/{UnityWebRequest.EscapeURL(doorId)}/measurements";
            string json = await PostStringAsync(url, body);
            return json != null;
        }

        /// <summary>Fetches the full measurement history for a door, oldest first (as the backend returns it).</summary>
        public async Task<List<DoorProperties>> FetchMeasurementHistoryAsync(string doorId)
        {
            var result = new List<DoorProperties>();
            string url = $"{_apiBaseUrl}/api/v1/doors/{UnityWebRequest.EscapeURL(doorId)}/measurements";
            string json = await GetStringAsync(url);
            if (json == null) return result;

            List<DoorMeasurementResponseDto> items;
            try { items = JsonConvert.DeserializeObject<List<DoorMeasurementResponseDto>>(json); }
            catch (Exception ex)
            {
                Debug.LogError($"[MeasurementApiService] Failed to parse measurement history: {ex.Message}");
                return result;
            }
            if (items == null) return result;

            foreach (var dto in items)
            {
                result.Add(new DoorProperties
                {
                    WidthMm = ParseFloat(dto.WidthMm),
                    HeightMm = ParseFloat(dto.HeightMm),
                    ThicknessMm = ParseFloat(dto.ThicknessMm),
                    WidthConfidence = ParseFloat(dto.WidthConfidence),
                    HeightConfidence = ParseFloat(dto.HeightConfidence),
                    ThicknessConfidence = ParseFloat(dto.ThicknessConfidence),
                    Material = dto.Material,
                    OpeningDirection = dto.OpeningDirection,
                    DoorType = dto.DoorType,
                    ExistingHandleHeightMm = ParseNullableFloat(dto.ExistingHandleHeightMm),
                    ExistingLockHeightMm = ParseNullableFloat(dto.ExistingLockHeightMm),
                    ExistingBoreDiameterMm = ParseNullableFloat(dto.ExistingBoreDiameterMm),
                    Source = FromWireSource(dto.Source),
                });
            }
            return result;
        }

        // ─── Enum <-> wire-string mapping ─────────────────────────────
        // Unity's MeasurementSource is PascalCase; the backend's CHECK
        // constraint and Pydantic Literal use lowercase-snake.

        private static string ToWireSource(MeasurementSource source) => source switch
        {
            MeasurementSource.LiDAR => "lidar",
            MeasurementSource.ARDepth => "ar_depth",
            MeasurementSource.CVMonocular => "cv_monocular",
            MeasurementSource.UserAssisted => "user_assisted",
            MeasurementSource.ManualInput => "manual_input",
            _ => "manual_input",
        };

        private static MeasurementSource FromWireSource(string wire) => wire switch
        {
            "lidar" => MeasurementSource.LiDAR,
            "ar_depth" => MeasurementSource.ARDepth,
            "cv_monocular" => MeasurementSource.CVMonocular,
            "user_assisted" => MeasurementSource.UserAssisted,
            _ => MeasurementSource.ManualInput,
        };

        // ─── Helpers ────────────────────────────────────────────────

        private static float? NullIfZero(float value) => value == 0f ? (float?)null : value;

        private static float ParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        private static float? ParseNullableFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (float?)null;
        }

        private async Task<string> PostStringAsync(string url, object body)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
            using var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(jsonBytes),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-API-Key", _apiKey);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MeasurementApiService] POST {url} failed: {req.error} ({req.downloadHandler?.text})");
                return null;
            }
            return req.downloadHandler.text;
        }

        private async Task<string> GetStringAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("X-API-Key", _apiKey);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MeasurementApiService] GET {url} failed: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
