using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Aayam.Services
{
    [Serializable]
    public class AuthUserDto
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("email")] public string Email;
        [JsonProperty("name")] public string Name;
    }

    [Serializable]
    public class AuthTokenDto
    {
        [JsonProperty("access_token")] public string AccessToken;
        [JsonProperty("token_type")] public string TokenType;
        [JsonProperty("user")] public AuthUserDto User;
    }

    /// <summary>Non-exception-throwing result wrapper - every caller here is a
    /// UI form that needs to show the SERVER's actual rejection reason
    /// (e.g. "email already exists", "incorrect password"), not a generic
    /// failure state.</summary>
    public class AuthResult
    {
        public bool Success;
        public AuthTokenDto Token;
        public string ErrorMessage;
    }

    /// <summary>
    /// Fitora user accounts (2026-11-04) - signup/login/me against the real
    /// backend auth endpoints (backend/app/api/v1/auth.py). Mirrors
    /// ProductApiService's construction pattern (new AuthApiService(...),
    /// not a MonoBehaviour).
    /// </summary>
    public class AuthApiService
    {
        private readonly string _apiBaseUrl;

        public AuthApiService(string apiBaseUrl)
        {
            _apiBaseUrl = (apiBaseUrl ?? "").TrimEnd('/');
        }

        public Task<AuthResult> SignUpAsync(string email, string password, string name) =>
            PostAuthAsync("/api/v1/auth/signup", new { email, password, name });

        public Task<AuthResult> LoginAsync(string email, string password) =>
            PostAuthAsync("/api/v1/auth/login", new { email, password });

        public async Task<AuthUserDto> GetMeAsync(string accessToken)
        {
            using var req = UnityWebRequest.Get($"{_apiBaseUrl}/api/v1/auth/me");
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success) return null;
            try { return JsonConvert.DeserializeObject<AuthUserDto>(req.downloadHandler.text); }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthApiService] Failed to parse /auth/me response: {ex.Message}");
                return null;
            }
        }

        private async Task<AuthResult> PostAuthAsync(string path, object body)
        {
            string json = JsonConvert.SerializeObject(body);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest($"{_apiBaseUrl}{path}", "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string message = ExtractErrorDetail(req.downloadHandler?.text) ?? req.error;
                Debug.LogWarning($"[AuthApiService] {path} failed ({req.responseCode}): {message}");
                return new AuthResult { Success = false, ErrorMessage = message };
            }

            try
            {
                var token = JsonConvert.DeserializeObject<AuthTokenDto>(req.downloadHandler.text);
                return new AuthResult { Success = true, Token = token };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthApiService] Failed to parse {path} response: {ex.Message}");
                return new AuthResult { Success = false, ErrorMessage = "Unexpected server response." };
            }
        }

        /// <summary>FastAPI error bodies are {"detail": "..."} for a single
        /// HTTPException, or {"detail": [{"msg": "..."}]} for a Pydantic
        /// validation failure - try the simple case first, fall back to the
        /// first validation message, else null (caller uses req.error).</summary>
        private static string ExtractErrorDetail(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return null;
            try
            {
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseBody);
                var detail = obj?["detail"];
                if (detail == null) return null;
                if (detail.Type == Newtonsoft.Json.Linq.JTokenType.String) return detail.ToString();
                if (detail.Type == Newtonsoft.Json.Linq.JTokenType.Array && detail.HasValues)
                    return detail[0]?["msg"]?.ToString();
                return null;
            }
            catch { return null; }
        }
    }
}
