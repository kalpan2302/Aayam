using UnityEditor;
using UnityEngine;

namespace Aayam.Editor
{
    /// <summary>
    /// Editor utility to validate URP is correctly configured for AR.
    /// Run via menu: Aayam > Setup > Validate AR Renderer
    /// </summary>
    public static class URPConfigValidator
    {
        [MenuItem("Aayam/Setup/Validate AR Renderer", false, 10)]
        public static void ValidateARRenderer()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

            if (pipeline == null)
            {
                Debug.LogError(
                    "[Aayam] No Render Pipeline asset found in Graphics Settings. " +
                    "Ensure you created the project with the 3D (URP) template."
                );
                return;
            }

            Debug.Log($"<color=green>[Aayam]</color> Render Pipeline Asset found: {pipeline.name} ({pipeline.GetType().Name})");

            Debug.Log(
                "<color=yellow>[Aayam]</color> MANUAL CHECK REQUIRED:\n" +
                "1. Select your URP Renderer Data asset (usually in Assets/Settings/)\n" +
                "2. In the Inspector, scroll to 'Renderer Features'\n" +
                "3. Verify 'AR Background Renderer Feature' is in the list\n" +
                "4. If missing: click 'Add Renderer Feature' > 'AR Background Renderer Feature'\n" +
                "\nWithout this, the AR camera feed will show a BLACK SCREEN on device."
            );
        }

        [MenuItem("Aayam/Setup/Log Player Settings", false, 11)]
        public static void LogPlayerSettings()
        {
            Debug.Log("=======================================");
            Debug.Log("       AAYAM PLAYER SETTINGS REPORT");
            Debug.Log("=======================================");

            Debug.Log($"Company: {PlayerSettings.companyName}");
            Debug.Log($"Product: {PlayerSettings.productName}");
            Debug.Log($"Version: {PlayerSettings.bundleVersion}");

            Debug.Log("\n-- iOS --");
            Debug.Log($"Bundle ID: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)}");
            Debug.Log($"Min iOS Version: {PlayerSettings.iOS.targetOSVersionString}");
            Debug.Log($"Camera Description: {PlayerSettings.iOS.cameraUsageDescription}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS)}");

            Debug.Log("\n-- Android --");
            Debug.Log($"Package: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
            Debug.Log($"Min API: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"Target API: {PlayerSettings.Android.targetSdkVersion}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}");
            Debug.Log($"Target Arch: {PlayerSettings.Android.targetArchitectures}");

            Debug.Log("\n=======================================");
        }

        [MenuItem("Aayam/Setup/Configure Player Settings (Recommended)", false, 12)]
        public static void ConfigureRecommendedSettings()
        {
            PlayerSettings.companyName = "Aayam";
            PlayerSettings.productName = "AayamAR";
            PlayerSettings.bundleVersion = "0.1.0";

            // iOS
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.aayam.ar");
            PlayerSettings.iOS.targetOSVersionString = "14.0";
            PlayerSettings.iOS.cameraUsageDescription =
                "Aayam needs camera access to scan your door and visualize products in AR.";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
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
