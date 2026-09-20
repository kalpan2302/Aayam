using UnityEditor;
using System.IO;
using UnityEngine;

namespace Aayam.Editor
{
    /// <summary>
    /// Editor utility to create the standard Aayam project folder structure.
    /// Run via menu: Aayam > Setup > Create Project Structure
    /// </summary>
    public static class ProjectStructureCreator
    {
        private static readonly string[] Directories = new[]
        {
            // Scripts - AR
            "Scripts/AR/Core",
            "Scripts/AR/Planes",
            "Scripts/AR/Placement",
            "Scripts/AR/Interaction",

            // Scripts - UI
            "Scripts/UI/Screens",
            "Scripts/UI/Components",
            "Scripts/UI/Managers",

            // Scripts - Core
            "Scripts/Core/Bootstrap",
            "Scripts/Core/Events",
            "Scripts/Core/Config",

            // Scripts - Other
            "Scripts/Models",
            "Scripts/Services",
            "Scripts/Utilities",

            // 3D Assets
            "Models3D/Handles",
            "Models3D/Locks",
            "Models3D/Accessories",

            // Materials
            "Materials/Products",
            "Materials/AR",
            "Materials/UI",

            // Other Assets
            "Textures",
            "Prefabs/AR",
            "Prefabs/Products",
            "Prefabs/UI",
            "Scenes",
            "Resources",
            "StreamingAssets",
            "Animations",
            "Audio",
        };

        [MenuItem("Aayam/Setup/Create Project Structure", false, 1)]
        public static void CreateStructure()
        {
            string root = Path.Combine(Application.dataPath, "_Project");

            int created = 0;
            foreach (string dir in Directories)
            {
                string fullPath = Path.Combine(root, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    created++;
                }
            }

            // Also create the Tests directory at Assets level
            string testsRoot = Path.Combine(Application.dataPath, "Tests");
            string[] testDirs = new[]
            {
                "EditMode/AR",
                "EditMode/Models",
                "EditMode/Utilities",
                "PlayMode/AR",
                "PlayMode/UI",
            };

            foreach (string dir in testDirs)
            {
                string fullPath = Path.Combine(testsRoot, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    created++;
                }
            }

            // Create Editor directory at Assets level
            string editorPath = Path.Combine(Application.dataPath, "Editor");
            if (!Directory.Exists(editorPath))
            {
                Directory.CreateDirectory(editorPath);
                created++;
            }

            // Create Plugins directories
            string pluginsRoot = Path.Combine(Application.dataPath, "Plugins");
            string[] pluginDirs = new[] { "iOS", "Android" };
            foreach (string dir in pluginDirs)
            {
                string fullPath = Path.Combine(pluginsRoot, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    created++;
                }
            }

            AssetDatabase.Refresh();

            if (created > 0)
            {
                Debug.Log($"<color=green>[Aayam]</color> Project structure created. {created} new directories.");
            }
            else
            {
                Debug.Log("<color=green>[Aayam]</color> Project structure already exists. No changes needed.");
            }
        }

        [MenuItem("Aayam/Setup/Verify Project Structure", false, 2)]
        public static void VerifyStructure()
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            int missing = 0;

            foreach (string dir in Directories)
            {
                string fullPath = Path.Combine(root, dir);
                if (!Directory.Exists(fullPath))
                {
                    Debug.LogWarning($"[Aayam] Missing directory: _Project/{dir}");
                    missing++;
                }
            }

            if (missing == 0)
            {
                Debug.Log("<color=green>[Aayam]</color> All project directories verified. ✓");
            }
            else
            {
                Debug.LogError($"[Aayam] {missing} directories missing. Run Aayam > Setup > Create Project Structure.");
            }
        }
    }
}

