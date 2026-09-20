using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using System;
using System.Collections;

namespace Aayam.AR.Core
{
    /// <summary>
    /// Handles runtime camera permission requests on iOS and Android.
    /// Must be called before starting the AR session.
    /// </summary>
    public class CameraPermissionHandler : MonoBehaviour
    {
        // ─── Events ───────────────────────────────────────────────
        public static event Action OnPermissionGranted;
        public static event Action OnPermissionDenied;

        // ─── State ────────────────────────────────────────────────
        public bool HasPermission { get; private set; }
        public bool IsRequestInProgress { get; private set; }

        /// <summary>
        /// Request camera permission. Call this before starting AR.
        /// Fires OnPermissionGranted or OnPermissionDenied.
        /// </summary>
        public void RequestCameraPermission()
        {
            if (IsRequestInProgress) return;
            IsRequestInProgress = true;

#if UNITY_EDITOR
            // Always grant in editor
            Debug.Log("[CameraPermission] Editor mode — permission auto-granted.");
            GrantPermission();
#elif UNITY_ANDROID
            RequestAndroidPermission();
#elif UNITY_IOS
            StartCoroutine(RequestIOSPermission());
#else
            GrantPermission();
#endif
        }

        // ─── Android ─────────────────────────────────────────────

#if UNITY_ANDROID
        private void RequestAndroidPermission()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[CameraPermission] Android — already granted.");
                GrantPermission();
                return;
            }

            Debug.Log("[CameraPermission] Android — requesting camera permission...");

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += OnAndroidGranted;
            callbacks.PermissionDenied += OnAndroidDenied;
            callbacks.PermissionDeniedAndDontAskAgain += OnAndroidDeniedPermanently;

            Permission.RequestUserPermission(Permission.Camera, callbacks);
        }

        private void OnAndroidGranted(string permissionName)
        {
            Debug.Log("[CameraPermission] Android — granted.");
            GrantPermission();
        }

        private void OnAndroidDenied(string permissionName)
        {
            Debug.LogWarning("[CameraPermission] Android — denied.");
            DenyPermission();
        }

        private void OnAndroidDeniedPermanently(string permissionName)
        {
            Debug.LogWarning("[CameraPermission] Android — denied permanently. User must go to Settings.");
            DenyPermission();
        }
#endif

        // ─── iOS ──────────────────────────────────────────────────

#if UNITY_IOS
        private IEnumerator RequestIOSPermission()
        {
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[CameraPermission] iOS — already granted.");
                GrantPermission();
                yield break;
            }

            Debug.Log("[CameraPermission] iOS — requesting camera permission...");
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[CameraPermission] iOS — granted.");
                GrantPermission();
            }
            else
            {
                Debug.LogWarning("[CameraPermission] iOS — denied.");
                DenyPermission();
            }
        }
#endif

        // ─── Result Handlers ──────────────────────────────────────

        private void GrantPermission()
        {
            HasPermission = true;
            IsRequestInProgress = false;
            OnPermissionGranted?.Invoke();
        }

        private void DenyPermission()
        {
            HasPermission = false;
            IsRequestInProgress = false;
            OnPermissionDenied?.Invoke();
        }

        // ─── Settings Deep Link ───────────────────────────────────

        /// <summary>
        /// Open the device's app settings page so the user can manually grant camera permission.
        /// Call this when permission has been permanently denied.
        /// </summary>
        public void OpenAppSettings()
        {
#if UNITY_IOS
            Application.OpenURL("app-settings:");
#elif UNITY_ANDROID
            try
            {
                using var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
                string packageName = activity.Call<string>("getPackageName");

                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>(
                    "fromParts", "package", packageName, null
                );
                using var intent = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.APPLICATION_DETAILS_SETTINGS",
                    uri
                );
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.DEFAULT");
                intent.Call<AndroidJavaObject>("setFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                activity.Call("startActivity", intent);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CameraPermission] Failed to open settings: {e.Message}");
            }
#endif
        }
    }
}
