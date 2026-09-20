# Step 1: Unity Project Setup — Detailed Implementation Guide

This guide provides an exhaustive, step-by-step walkthrough to set up the AayamAR Unity project for cross-platform (iOS & Android) mobile AR using AR Foundation.

**Duration:** Week 1  
**Goal:** Establish a robust, scalable, and version-controlled foundation for the AR application.

---

## 1.1 Prerequisites

Before starting, ensure all required software is installed and configured on your machine.

*   **Unity Hub (Latest Version)**
    *   **Link:** [Download Unity Hub](https://unity.com/download)
    *   **Role:** Manages Unity editor versions and project instances.
*   **Unity Editor: Unity 6000.1 LTS (or 2022.3 LTS fallback)**
    *   **Role:** The core game engine.
    *   **Build Modules Required:**
        *   **iOS Build Support:** Required for exporting Xcode projects.
        *   **Android Build Support:** Must check the sub-options to include the Android SDK, NDK tools, and OpenJDK.
*   **IDE (Choose One)**
    *   **Visual Studio 2022 Community** (Ensure "Game development with Unity" workload is checked).
    *   **VS Code** (Install the "C# Dev Kit" and "Unity" extensions).
*   **Git for Windows**
    *   **Link:** [Download Git](https://git-scm.com/download/win)
    *   **Role:** Version control system.
*   **Blender 4.x**
    *   **Link:** [Download Blender](https://www.blender.org/download/)
    *   **Role:** For inspecting, optimizing, and creating 3D assets (e.g., handles, locks).
*   **macOS Machine (Optional but needed for iOS builds)**
    *   Building for iOS strictly requires Xcode running on macOS. Alternatively, Unity Cloud Build or GameCI (configured later) can be used.

---

## 1.2 Create Unity Project

1.  Open **Unity Hub**.
2.  Navigate to the **Projects** tab and click **New project**.
3.  Ensure the correct Editor version is selected at the top.
4.  Select the **3D (URP)** template.
    > [!TIP]
    > **Why URP?** The Universal Render Pipeline (URP) is specifically optimized for mobile devices and AR platforms. It provides better performance and battery life compared to the High Definition Render Pipeline (HDRP, meant for high-end PC/consoles) and the legacy Built-in Render Pipeline.
5.  Set the **Project name** to: `AayamAR`
6.  Set the **Location** to: `D:\Projects\Personal\Aayam\unity\`
7.  Click **Create project**. Unity will take a few minutes to resolve packages and generate the default URP scene and settings folders.

---

## 1.3 AR Foundation Package Installation

Unity’s AR capabilities are modular. You must install AR Foundation and the platform-specific plugins.

1.  In Unity, open **Window > Package Manager**.
2.  Click the `+` icon in the top-left and select **Add package by name...** (or search in the Unity Registry).
3.  Install the following packages using their exact names:
    *   `com.unity.xr.arfoundation` (Version 6.x) - Core AR API.
    *   `com.unity.xr.arkit` (Version 6.x) - Apple iOS AR backend.
    *   `com.unity.xr.arcore` (Version 6.x) - Google Android AR backend.
    *   `com.unity.xr.management` (Version 4.x) - Manages XR plugin lifecycles.
    *   `com.unity.inputsystem` (Version 1.x) - Modern input handling for touch/gestures.

> [!IMPORTANT]
> **Version Compatibility:** AR Foundation 6.x requires Unity 6000+ or 2023.3+. If using the 2022.3 LTS fallback, you must use AR Foundation 5.x.

Your `Packages/manifest.json` will automatically reflect these additions:
```json
{
  "dependencies": {
    "com.unity.xr.arfoundation": "6.0.0-pre.6",
    "com.unity.xr.arkit": "6.0.0-pre.6",
    "com.unity.xr.arcore": "6.0.0-pre.6",
    "com.unity.inputsystem": "1.7.0"
    ...
  }
}
```

**Troubleshooting:** If installing the new Input System prompts to restart the editor to apply changes, click **Yes**.

---

## 1.4 XR Plugin Management Configuration

You must explicitly enable the AR backend for each target platform.

1.  Go to **Edit > Project Settings > XR Plug-in Management**.
2.  **iOS Tab (Apple icon):**
    *   Check **Apple ARKit** under Plug-in Providers.
    *   Click on **Apple ARKit** in the sidebar (under XR Plug-in Management). Enable Face Tracking or other optional features only if needed (for Aayam, standard AR tracking is sufficient).
3.  **Android Tab (Android icon):**
    *   Check **Google ARCore** under Plug-in Providers.
    *   Click on **Google ARCore** in the sidebar. Set **Requirement** to **Required** (since this is an AR-first app, it shouldn't run on non-AR devices).

---

## 1.5 URP Configuration

AR requires the camera feed to be rendered as the background. URP needs a specific configuration to allow this.

1.  Navigate to `Assets/Settings` (or wherever your URP asset was generated).
2.  Select the **Universal Render Pipeline Asset**.
    *   **Render Scale:** Keep at `1.0` (drop to `0.75` later if thermal throttling occurs on older devices).
    *   **Anti-aliasing (MSAA):** Set to `4x` for smooth 3D edges.
    *   **Shadow Resolution:** Set to `Medium` or `Low` (1024/512) to save performance.
3.  Select the **Universal Renderer Data** (usually next to the URP asset).
    *   Scroll to the bottom, click **Add Renderer Feature**.
    *   Select **AR Background Renderer Feature**.
    > [!IMPORTANT]
    > **Critical Step:** Without the AR Background Renderer Feature, your AR session will just show a black screen instead of the device camera feed.

**Helper Script for URP Setup (Optional):**
```csharp
// Editor/URPConfig.cs
using UnityEditor;
using UnityEngine.Rendering.Universal;

public class URPConfig
{
    [MenuItem("Aayam/Setup/Verify AR Background Renderer")]
    public static void Verify()
    {
        // Implementation to programmatically check URP renderer features
        UnityEngine.Debug.Log("Please ensure AR Background Renderer Feature is added to your URP Data asset.");
    }
}
```

---

## 1.6 Project Structure Creation

A clean structure is vital for scaling. Below is the proposed architecture.

```text
Assets/
├── _Project/                    # All project-specific assets (keeps root clean)
│   ├── Scripts/
│   │   ├── AR/                  # AR session, plane detection, anchoring
│   │   │   ├── Core/            # AR session management
│   │   │   ├── Planes/          # Plane detection & visualization
│   │   │   ├── Placement/       # Object placement & anchoring
│   │   │   └── Interaction/     # Gesture handling
│   │   ├── UI/                  # UI controllers and views
│   │   │   ├── Screens/         # Screen controllers
│   │   │   ├── Components/      # Reusable UI components
│   │   │   └── Managers/        # UI state management
│   │   ├── Models/              # Data models and DTOs
│   │   ├── Services/            # API services, data loading
│   │   ├── Utilities/           # Helper classes, extensions
│   │   └── Core/                # App lifecycle, DI
│   │       ├── Bootstrap/       # App initialization
│   │       ├── Events/          # Event system
│   │       └── Config/          # Configuration
│   ├── Models3D/                # 3D models (.glb, .fbx)
│   │   ├── Handles/
│   │   ├── Locks/
│   │   └── Accessories/
│   ├── Materials/               # Materials and shaders
│   │   ├── Products/
│   │   ├── AR/                  # AR-specific materials
│   │   └── UI/
│   ├── Textures/                # Texture assets
│   ├── Prefabs/                 # Prefab assets
│   │   ├── AR/                  # AR prefabs (planes, reticle)
│   │   ├── Products/            # Product prefabs
│   │   └── UI/                  # UI prefabs
│   ├── Scenes/                  # Unity scenes
│   │   ├── Bootstrap.unity      # Initial loading scene
│   │   ├── ARScene.unity        # Main AR scene
│   │   └── Debug.unity          # Debug/testing scene
│   ├── Resources/               # Runtime-loaded assets
│   ├── StreamingAssets/         # Platform-specific native assets
│   ├── Animations/              # Animation clips and controllers
│   └── Audio/                   # Sound effects and music
├── Plugins/                     # Native plugins (iOS/Android)
└── Settings/                    # Project configuration
```

**Automate Creation via Editor Script:**
Create `Assets/Editor/ProjectStructureCreator.cs`:
```csharp
using UnityEditor;
using System.IO;
using UnityEngine;

public class ProjectStructureCreator
{
    [MenuItem("Aayam/Setup/Create Project Structure")]
    public static void CreateStructure()
    {
        string root = Application.dataPath + "/_Project";
        string[] directories = new string[]
        {
            "Scripts/AR/Core", "Scripts/AR/Planes", "Scripts/AR/Placement", "Scripts/AR/Interaction",
            "Scripts/UI/Screens", "Scripts/UI/Components", "Scripts/UI/Managers",
            "Scripts/Models", "Scripts/Services", "Scripts/Utilities",
            "Scripts/Core/Bootstrap", "Scripts/Core/Events", "Scripts/Core/Config",
            "Models3D/Handles", "Models3D/Locks", "Models3D/Accessories",
            "Materials/Products", "Materials/AR", "Materials/UI",
            "Textures", "Prefabs/AR", "Prefabs/Products", "Prefabs/UI",
            "Scenes", "Resources", "StreamingAssets", "Animations", "Audio"
        };

        foreach (string dir in directories)
        {
            Directory.CreateDirectory(Path.Combine(root, dir));
        }
        AssetDatabase.Refresh();
        Debug.Log("Aayam Project Structure Created Successfully.");
    }
}
```

---

## 1.7 Player Settings Configuration

Navigate to **Edit > Project Settings > Player**.

### iOS Settings
*   **Bundle Identifier:** `com.aayam.ar`
*   **Version:** `0.1.0`
*   **Minimum iOS Version:** `14.0` (ARKit 4 requirement)
*   **Target SDK:** Device SDK
*   **Architecture:** ARM64
*   **Camera Usage Description:** `"Aayam needs camera access for AR visualization"`
*   **Scripting Backend:** IL2CPP (Required for iOS)
*   **API Compatibility Level:** .NET Standard 2.1

### Android Settings
*   **Package Name:** `com.aayam.ar`
*   **Minimum API Level:** Android 7.0 (API level 24)
*   **Target API Level:** Automatic (highest installed)
*   **Scripting Backend:** IL2CPP (Required for ARM64)
*   **Target Architectures:** Uncheck ARMv7, check **ARM64**.
*   **ARCore Supported:** Checked (Found under XR settings or sometimes Player settings depending on unity version).
*   **Internet Access:** Require (For downloading models/auth).
*   **Write Permission:** External (SDCard).

---

## 1.8 Git Setup

1.  Open terminal and navigate to `D:\Projects\Personal\Aayam\`.
2.  Run `git init`.
3.  Create `.gitignore` at the Unity project root (`D:\Projects\Personal\Aayam\unity\.gitignore`):
```text
# Unity Gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/

# MemoryCaptures
MemoryCaptures/

# Asset meta data should only be ignored if the asset itself is ignored!
!/[Aa]ssets/**/*.meta

# VS/Rider/Mac
.vs/
.idea/
.DS_Store
*.csproj
*.unityproj
*.sln
```
4.  Create `.gitattributes` at the project root to enforce Git LFS for large binaries:
```text
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.asset filter=lfs diff=lfs merge=lfs -text
*.dll filter=lfs diff=lfs merge=lfs -text
```
5.  **Initial Commit:**
    ```bash
    git add .
    git commit -m "chore: initial unity project setup with AR Foundation"
    ```
6.  **Branching Strategy:** `main` (production) -> `develop` (integration) -> `feature/ar-placement` (development).

---

## 1.9 CI/CD Pipeline Stub (GitHub Actions)

Create `.github/workflows/unity-build.yml` in your repository root.

```yaml
name: Unity CI/CD Pipeline

on:
  push:
    branches:
      - main
      - develop

jobs:
  build-android:
    name: Build for Android
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v3
        with:
          lfs: true
      
      # Caching for faster builds
      - uses: actions/cache@v3
        with:
          path: unity/Library
          key: Library-Android
      
      - name: Build Unity Android APK
        uses: game-ci/unity-builder@v2
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: unity
          targetPlatform: Android
          
      - name: Upload APK
        uses: actions/upload-artifact@v3
        with:
          name: Android-Build
          path: build/Android/

  build-ios:
    name: Build for iOS
    runs-on: macos-latest
    # Similar steps utilizing game-ci targeting iOS, exports Xcode project.
```
*   **Secrets Needed:** You must generate a Unity personal license file (ULF) or provide Unity credentials in your GitHub Repo Secrets.

---

## 1.10 Assembly Definitions

Using Assembly Definitions (`.asmdef`) drastically reduces compile times by preventing the entire codebase from compiling when only a single script changes.

Create these files in their respective folders under `_Project/Scripts/`:
*   `Aayam.Core.asmdef` (No dependencies)
*   `Aayam.Models.asmdef` (Depends on Core)
*   `Aayam.Services.asmdef` (Depends on Models, Core)
*   `Aayam.AR.asmdef` (Depends on Core, Unity.XR.ARFoundation, Unity.XR.ARSubsystems)
*   `Aayam.UI.asmdef` (Depends on Core, Models, AR)
*   `Aayam.Utilities.asmdef`

**How to create:** Right-click in the folder -> Create -> Assembly Definition. Name it appropriately. Configure references in the inspector.

---

## 1.11 Core Bootstrap System

A clear entry point is vital. We create a simple `Bootstrap.unity` scene and an `AppBootstrapper.cs`.

```csharp
// _Project/Scripts/Core/Bootstrap/AppBootstrapper.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aayam.Core.Bootstrap
{
    public class AppBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            InitializeApp();
        }

        private void InitializeApp()
        {
            Debug.Log("[Bootstrapper] Initializing Core Services...");
            
            // Register services (e.g., Simple Service Locator)
            ServiceLocator.Initialize();
            
            // Load configuration
            AppConfig.Load();

            // Load main AR Scene
            Debug.Log("[Bootstrapper] Transitioning to AR Scene...");
            SceneManager.LoadScene("ARScene");
        }
    }
}
```

```csharp
// _Project/Scripts/Core/Config/AppConfig.cs
namespace Aayam.Core.Config
{
    public static class AppConfig
    {
        public static void Load() 
        {
            // Load remote configs, env variables, etc.
        }
    }
}
```

---

## 1.12 Coding Standards

Create an `.editorconfig` file in the `unity/` root to enforce styles:

```ini
root = true

[*.cs]
# Indentation and spacing
indent_style = space
indent_size = 4
tab_width = 4

# Naming conventions
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.severity = error
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.symbols = interface
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.style = i_prefix

dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_style.i_prefix.required_prefix = I
dotnet_naming_style.i_prefix.capitalization = pascal_case

# Private fields: _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = _camel_case

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style._camel_case.required_prefix = _
dotnet_naming_style._camel_case.capitalization = camel_case
```

---

## Deliverables Checklist

- [ ] Unity Hub + Editor installed with correct modules
- [ ] New URP project created: AayamAR
- [ ] AR Foundation + ARKit + ARCore packages installed
- [ ] XR Plugin Management configured (iOS + Android)
- [ ] URP configured with AR Background Renderer Feature
- [ ] Full project folder structure created
- [ ] Player settings configured (iOS + Android)
- [ ] Git initialized with .gitignore and .gitattributes
- [ ] CI/CD pipeline stub created
- [ ] Assembly definitions created
- [ ] Core bootstrap system implemented
- [ ] Coding standards documented (.editorconfig)
- [ ] Project compiles without errors on both platforms
- [ ] First commit pushed to repository

---

## Common Issues & Troubleshooting

*   **Package Version Conflicts:** If you see compile errors in AR Foundation, ensure your Unity Editor version explicitly supports the AR Foundation version you downloaded (6.x requires Unity 6000+).
*   **Black Screen on Device (No Camera Feed):** You forgot Step 1.5. Add the `AR Background Renderer Feature` to your URP Renderer Data.
*   **Android Build Fails - NDK Not Found:** Open Unity Hub -> Installs -> Add modules -> Ensure NDK is checked. Check `Edit > Preferences > External Tools` to verify the path.
*   **iOS Build Requires macOS:** You cannot build the final `.ipa` on Windows. Windows Unity can only export an Xcode project directory. Move this directory to a Mac to compile, or rely on GameCI in GitHub actions.
*   **AR Subsystem Stopped Working / Crashes:** Ensure `Camera Usage Description` is populated in iOS Player Settings. Missing privacy descriptions will cause instant silent crashes on iOS.
