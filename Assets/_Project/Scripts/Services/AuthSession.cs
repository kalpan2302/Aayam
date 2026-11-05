using UnityEngine;

namespace Aayam.Services
{
    /// <summary>
    /// Fitora's current-login state (2026-11-04) - static, not a
    /// MonoBehaviour/singleton, matching this project's ServiceLocator-free
    /// convention for plain services. Token persisted to PlayerPrefs so a
    /// relaunch doesn't force a re-login; PlayerPrefs is NOT secure storage
    /// (plaintext on the device) - acceptable for this MVP demo app, flagged
    /// here for when real distribution is considered (would need Keychain/
    /// Android Keystore via a native plugin instead).
    /// </summary>
    public static class AuthSession
    {
        private const string TokenKey = "fitora_auth_token";
        private const string UserIdKey = "fitora_auth_user_id";
        private const string UserEmailKey = "fitora_auth_user_email";
        private const string UserNameKey = "fitora_auth_user_name";

        public static string AccessToken { get; private set; }
        public static string UserId { get; private set; }
        public static string UserEmail { get; private set; }
        public static string UserName { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

        // Static constructor - runs exactly once, on first touch of this
        // class from anywhere, rather than needing every entry point
        // (AuthFormUI, ProfileScreenUI, the tab bar) to remember to call
        // Load() first.
        static AuthSession() => Load();

        private static void Load()
        {
            AccessToken = PlayerPrefs.GetString(TokenKey, "");
            UserId = PlayerPrefs.GetString(UserIdKey, "");
            UserEmail = PlayerPrefs.GetString(UserEmailKey, "");
            UserName = PlayerPrefs.GetString(UserNameKey, "");
            if (string.IsNullOrEmpty(AccessToken)) AccessToken = null;
        }

        public static void SetSession(AuthTokenDto token)
        {
            if (token == null) return;
            AccessToken = token.AccessToken;
            UserId = token.User?.Id;
            UserEmail = token.User?.Email;
            UserName = token.User?.Name;

            PlayerPrefs.SetString(TokenKey, AccessToken ?? "");
            PlayerPrefs.SetString(UserIdKey, UserId ?? "");
            PlayerPrefs.SetString(UserEmailKey, UserEmail ?? "");
            PlayerPrefs.SetString(UserNameKey, UserName ?? "");
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            AccessToken = UserId = UserEmail = UserName = null;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(UserEmailKey);
            PlayerPrefs.DeleteKey(UserNameKey);
            PlayerPrefs.Save();
        }
    }
}
