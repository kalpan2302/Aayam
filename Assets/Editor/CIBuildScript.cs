using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aayam.Editor
{
    /// <summary>
    /// Temporary CLI build entry point for the Camera.aspect fix verification
    /// pass (2026-10-24) - no scripted build pipeline existed in this project
    /// before. Incremental (AcceptExternalModificationsToPlayer) so it
    /// updates the existing Builds/iOS Xcode project in place rather than
    /// wiping its signing config.
    /// </summary>
    public static class CIBuildScript
    {
        public static void PerformIOSBuild()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            BuildReport report = BuildPipeline.BuildPlayer(
                scenes,
                "Builds/iOS",
                BuildTarget.iOS,
                BuildOptions.AcceptExternalModificationsToPlayer);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[CIBuildScript] Build failed: {report.summary.result}, {report.summary.totalErrors} errors");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CIBuildScript] Build succeeded: {report.summary.outputPath}");
            EditorApplication.Exit(0);
        }
    }
}
