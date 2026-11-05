using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aayam.Editor
{
    /// <summary>
    /// Headless runner: opens the committed test scene, runs
    /// AayamSceneBootstrapper.BuildTestSceneWiring() (normally a manual
    /// Editor-menu action), and saves the scene. Written during Phase 6
    /// because several bootstrapper additions that session (WireLightEstimation,
    /// WireDoorShadowReceiver, WireFinishPicker, WirePerformanceProfiler,
    /// the HUD button repositioning, WireScreenshotCapture) were never
    /// actually applied to Assets/ARtest.unity - only the C# script had
    /// changed, not the scene, since nothing had ever run the bootstrap in
    /// a scripted/CI-friendly way before. Kept as reusable tooling: run via
    /// `-executeMethod Aayam.Editor.Phase6SceneApply.Run` any time
    /// AayamSceneBootstrapper.cs changes and the scene needs re-wiring
    /// without opening the Editor GUI.
    /// </summary>
    public static class Phase6SceneApply
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/ARtest.unity", OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Phase6SceneApply] Could not open Assets/ARtest.unity");
                EditorApplication.Exit(1);
                return;
            }

            AayamSceneBootstrapper.BuildTestSceneWiring();

            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Phase6SceneApply] Scene save {(saved ? "OK" : "FAILED")}: {scene.path}");
            EditorApplication.Exit(saved ? 0 : 1);
        }
    }
}
