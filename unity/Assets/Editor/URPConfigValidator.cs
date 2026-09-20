using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aayam.Editor
{
    /// <summary>
    /// Editor utility to validate URP is correctly configured for AR.
    /// Checks that the AR Background Renderer Feature is added to the URP renderer.
    /// Run via menu: Aayam > Setup > Validate AR Renderer
    /// </summary>
    public static class URPConfigValidator
    {
        [MenuItem("Aayam/Setup/Validate AR Renderer", false, 10)]
        public static void ValidateARRenderer()
        {
            // Find the URP asset
            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UniversalRenderPipelineAsset;

            if (urpAsset == null)
            {
                Debug.LogError(
                    "[Aayam] No URP asset found in Graphics Settings. " +
                    "Ensure you created the project with the 3D (URP) template."
                );
                return;
            }

            Debug.Log($"<color=green>[Aayam]</color> URP Asset found: {urpAsset.name}");

            // Check render scale
            if (urpAsset.renderScale < 0.75f)
            {
                Debug.LogWarning(
                    $"[Aayam] Render scale is {urpAsset.renderScale}. " +
                    "Consider 0.75-1.0 for AR quality."
                );
            }

            // Check MSAA
            if (urpAsset.msaaSampleCount < 4)
            {
                Debug.LogWarning(
                    "[Aayam] MSAA is below 4x. 4x recommended for smooth 3D edges in AR."
                );
            }

            // Note: Checking for AR Background Renderer Feature programmatically
            // requires accessing the renderer data, which has limited public API.
            // Provide clear instructions instead.
            Debug.Log(
                "<color=yellow>[Aayam]</color> MANUAL CHECK REQUIRED:\n" +
                "1. Select your URP Renderer Data asset (usually in Assets/Settings/)\n" +
                "2. In the Inspector, scroll to 'Renderer Features'\n" +
                "3. Verify 'AR Background Renderer Feature' is in the list\n" +
                "4. If missing: click 'Add Renderer Feature' → 'AR Background Renderer Feature'\n" +
                "\n⚠️ Without this, the AR camera feed will show a BLACK SCREEN on device."
            );
        }

        [MenuItem("Aayam/Setup/Log Player Settings", false, 11)]
        public static void LogPlayerSettings()
        {
            Debug.Log("═══════════════════════════════════════");
            Debug.Log("       AAYAM PLAYER SETTINGS REPORT");
            Debug.Log("═══════════════════════════════════════");

            // General
            Debug.Log($"Company: {PlayerSettings.companyName}");
            Debug.Log($"Product: {PlayerSettings.productName}");
            Debug.Log($"Version: {PlayerSettings.bundleVersion}");

            // iOS
            Debug.Log("\n── iOS ──");
            Debug.Log($"Bundle ID: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)}");
            Debug.Log($"Min iOS Version: {PlayerSettings.iOS.targetOSVersionString}");
            Debug.Log($"Camera Description: {PlayerSettings.iOS.cameraUsageDescription}");
            Debug.Log($"Architecture: {PlayerSettings.GetArchitectures(BuildTargetGroup.iOS)}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS)}");

            // Android
            Debug.Log("\n── Android ──");
            Debug.Log($"Package: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
            Debug.Log($"Min API: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"Target API: {PlayerSettings.Android.targetSdkVersion}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}");
            Debug.Log($"Target Arch: {PlayerSettings.Android.targetArchitectures}");

            Debug.Log("\n═══════════════════════════════════════");
        }

        [MenuItem("Aayam/Setup/Configure Player Settings (Recommended)", false, 12)]
        public static void ConfigureRecommendedSettings()
        {
            // Company and product
            PlayerSettings.companyName = "Aayam";
            PlayerSettings.productName = "AayamAR";
            PlayerSettings.bundleVersion = "0.1.0";

            // iOS
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.aayam.ar");
            PlayerSettings.iOS.targetOSVersionString = "14.0";
            PlayerSettings.iOS.cameraUsageDescription =
                "Aayam needs camera access to scan your door and visualize products in AR.";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitectures(BuildTargetGroup.iOS, (int)iOSArchitecture.ARM64);
            PlayerSettings.iOS.requiresFullScreen = true;

            // Android
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.aayam.ar");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            Debug.Log(
                "<color=green>[Aayam]</color> Player settings configured for iOS + Android. " +
                "Run 'Log Player Settings' to verify."
            );
        }
    }
}

