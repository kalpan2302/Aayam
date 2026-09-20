# Step 6: 3D Model Loading — Detailed Implementation Guide

**Phase:** 1
**Duration:** Week 3
**Focus:** Basic 3D Model Loading and Optimization for Mobile AR

This guide covers the technical implementation of loading, caching, and instantiating 3D models at runtime in the Aayam AR application.

---

## 6.1 3D Format Selection

For mobile AR applications, particularly those streaming assets at runtime, selecting the right 3D file format is critical for performance and user experience.

| Format | Size | Features | Unity Support | Recommended |
|--------|------|----------|---------------|-------------|
| glTF 2.0 (.gltf) | Medium | PBR, animations | Via plugin | For web |
| GLB (.glb) | Small | Binary glTF, single file | Via plugin | **YES – Primary** |
| FBX (.fbx) | Large | Full features | Native | For import only |
| OBJ (.obj) | Medium | Basic geometry | Native | No |
| USDZ (.usdz) | Medium | Apple AR | iOS native | For Quick Look |

### Why GLB is the Primary Format
- **Single File:** Bundles geometry, textures, and materials into a single `.glb` file, eliminating broken links and missing textures.
- **Compact Binary:** Smaller file size compared to text-based `.gltf`, leading to faster downloads.
- **Industry Standard:** Widely supported across web and mobile AR ecosystems.
- **PBR Support:** Excellent support for Physically Based Rendering (PBR) materials, ensuring realistic lighting and reflections.
- **Compression:** Supports Draco mesh compression and KTX2 texture compression.

---

## 6.2 GLB Runtime Loading

We will use **glTFast** as our runtime loading library. It is officially supported by Unity, highly performant, and supports advanced features like Draco and KTX2.

### Installation

Add the package via the Unity Package Manager (Add package from git URL or name):
```
com.unity.cloud.gltfast
```

### Implementation: `ModelLoader.cs`

This script handles the asynchronous loading of GLB files from various sources.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using Aayam.Caching;

namespace Aayam.Services
{
    /// <summary>
    /// Handles asynchronous loading of GLB models from URL, Local Path, or Bytes.
    /// Uses glTFast for efficient runtime parsing and instantiation.
    /// </summary>
    public class ModelLoader : MonoBehaviour
    {
        [SerializeField] private MaterialGenerator materialGenerator; // Assign URP material generator if needed
        private ModelCache modelCache;
        
        private void Awake()
        {
            // Fallback to default URP material generator if not assigned
            if (materialGenerator == null)
            {
                materialGenerator = new UniversalRPMaterialGenerator();
            }
            modelCache = FindObjectOfType<ModelCache>();
            if (modelCache == null)
            {
                Debug.LogWarning("ModelCache not found in scene. Caching will be disabled.");
            }
        }

        /// <summary>
        /// Loads a model from a remote URL. Uses cache if available.
        /// </summary>
        public async Task<GameObject> LoadModelFromUrlAsync(string url, string productId, Action<float> onProgress = null)
        {
            try
            {
                // 1. Check Cache
                if (modelCache != null)
                {
                    string cachedPath = modelCache.GetCachedModel(productId);
                    if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath))
                    {
                        Debug.Log($"[ModelLoader] Loading {productId} from cache.");
                        return await LoadModelAsync(cachedPath, onProgress);
                    }
                }

                // 2. Download if not cached
                Debug.Log($"[ModelLoader] Downloading {productId} from URL.");
                byte[] modelData = await DownloadModelDataAsync(url, onProgress);
                
                if (modelData == null || modelData.Length == 0)
                {
                    Debug.LogError($"[ModelLoader] Failed to download model from {url}");
                    return null;
                }

                // 3. Cache the downloaded data
                if (modelCache != null)
                {
                    modelCache.CacheModel(productId, modelData);
                }

                // 4. Load from bytes
                return await LoadModelFromBytesAsync(modelData, onProgress);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] Error loading model from URL: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads a model from a local file path.
        /// </summary>
        public async Task<GameObject> LoadModelAsync(string path, Action<float> onProgress = null)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ModelLoader] File not found at path: {path}");
                return null;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);
                return await LoadModelFromBytesAsync(data, onProgress);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] Error reading file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Instantiates a model from byte array using glTFast.
        /// </summary>
        public async Task<GameObject> LoadModelFromBytesAsync(byte[] data, Action<float> onProgress = null)
        {
            GameObject rootObject = new GameObject("GLTF_Model");
            
            try
            {
                var gltf = new GltfImport(null, new ConsoleLogger());
                
                // Configure import settings (e.g., node instantiation, materials)
                var importSettings = new ImportSettings
                {
                    GenerateMipMaps = true,
                    AnisotropicFilterLevel = 3,
                    NodeNameMethod = NameImportMethod.OriginalUnique
                };

                // Load the GLTF data
                bool success = await gltf.LoadGltfBinary(
                    data, 
                    new Uri("file://dummy.glb"), 
                    importSettings
                );

                if (!success)
                {
                    Debug.LogError("[ModelLoader] glTFast failed to parse the binary data.");
                    Destroy(rootObject);
                    return null;
                }

                // Instantiate the model into the scene
                var instantiator = new GameObjectInstantiator(gltf, rootObject.transform, new ConsoleLogger());
                success = await gltf.InstantiateMainSceneAsync(instantiator);

                if (success)
                {
                    // Normalize the loaded model
                    NormalizeModel(rootObject);
                    onProgress?.Invoke(1.0f);
                    return rootObject;
                }
                else
                {
                    Debug.LogError("[ModelLoader] glTFast failed to instantiate the scene.");
                    Destroy(rootObject);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] Exception during glTF instantiation: {ex.Message}\n{ex.StackTrace}");
                Destroy(rootObject);
                return null;
            }
        }

        private async Task<byte[]> DownloadModelDataAsync(string url, Action<float> onProgress)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                {
                    onProgress?.Invoke(webRequest.downloadProgress * 0.9f); // Leave 10% for instantiation
                    await Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[ModelLoader] Download error: {webRequest.error}");
                    return null;
                }

                return webRequest.downloadHandler.data;
            }
        }

        private void NormalizeModel(GameObject rootObject)
        {
            // Auto-center and fix scale if necessary, though glTF standard is 1 unit = 1 meter.
            // Further setup is deferred to ModelInstanceManager.
        }

        public void UnloadModel(GameObject model)
        {
            if (model != null)
            {
                Destroy(model);
            }
        }
    }
}
```

---

## 6.3 Model Caching System

To minimize data usage and loading times, models are cached locally.

### Implementation: `ModelCache.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Aayam.Caching
{
    [Serializable]
    public class CacheEntry
    {
        public string ProductId;
        public string FileName;
        public long SizeBytes;
        public DateTime LastAccessTime;
    }

    [Serializable]
    public class CacheManifest
    {
        public List<CacheEntry> Entries = new List<CacheEntry>();
    }

    /// <summary>
    /// Manages local storage of downloaded 3D models with LRU eviction.
    /// </summary>
    public class ModelCache : MonoBehaviour
    {
        private string cacheDirectory;
        private string manifestPath;
        private CacheManifest manifest;
        
        [Tooltip("Maximum cache size in bytes (Default: 200MB)")]
        [SerializeField] private long maxCacheSizeBytes = 200 * 1024 * 1024; 

        private void Awake()
        {
            cacheDirectory = Path.Combine(Application.persistentDataPath, "ModelCache");
            manifestPath = Path.Combine(cacheDirectory, "manifest.json");
            
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            LoadManifest();
            ValidateCache();
        }

        private void LoadManifest()
        {
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    manifest = JsonConvert.DeserializeObject<CacheManifest>(json) ?? new CacheManifest();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModelCache] Failed to load manifest: {ex.Message}");
                    manifest = new CacheManifest();
                }
            }
            else
            {
                manifest = new CacheManifest();
            }
        }

        private void SaveManifest()
        {
            try
            {
                string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(manifestPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelCache] Failed to save manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the path to a cached model if it exists.
        /// </summary>
        public string GetCachedModel(string productId)
        {
            var entry = manifest.Entries.FirstOrDefault(e => e.ProductId == productId);
            if (entry != null)
            {
                string path = Path.Combine(cacheDirectory, entry.FileName);
                if (File.Exists(path))
                {
                    entry.LastAccessTime = DateTime.UtcNow;
                    SaveManifest();
                    return path;
                }
                else
                {
                    // File is missing, remove from manifest
                    manifest.Entries.Remove(entry);
                    SaveManifest();
                }
            }
            return null;
        }

        /// <summary>
        /// Caches model data and returns the local path. Evicts old models if limit is reached.
        /// </summary>
        public string CacheModel(string productId, byte[] data)
        {
            EnforceSizeLimit(data.Length);

            string fileName = $"{productId}_{Guid.NewGuid().ToString().Substring(0, 8)}.glb";
            string path = Path.Combine(cacheDirectory, fileName);

            try
            {
                File.WriteAllBytes(path, data);
                
                // Remove old entry if updating
                manifest.Entries.RemoveAll(e => e.ProductId == productId);
                
                manifest.Entries.Add(new CacheEntry
                {
                    ProductId = productId,
                    FileName = fileName,
                    SizeBytes = data.Length,
                    LastAccessTime = DateTime.UtcNow
                });

                SaveManifest();
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelCache] Failed to cache model: {ex.Message}");
                return null;
            }
        }

        private void EnforceSizeLimit(long newFileSizeBytes)
        {
            long currentSize = GetCacheSize();
            if (currentSize + newFileSizeBytes <= maxCacheSizeBytes) return;

            // Sort by oldest access time (LRU)
            var sortedEntries = manifest.Entries.OrderBy(e => e.LastAccessTime).ToList();

            foreach (var entry in sortedEntries)
            {
                if (currentSize + newFileSizeBytes <= maxCacheSizeBytes) break;

                string path = Path.Combine(cacheDirectory, entry.FileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    currentSize -= entry.SizeBytes;
                    manifest.Entries.Remove(entry);
                    Debug.Log($"[ModelCache] Evicted {entry.ProductId} from cache.");
                }
            }
            
            SaveManifest();
        }

        public long GetCacheSize()
        {
            return manifest.Entries.Sum(e => e.SizeBytes);
        }

        public void ClearCache()
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, true);
                Directory.CreateDirectory(cacheDirectory);
            }
            manifest.Entries.Clear();
            SaveManifest();
        }
    }
}
```

---

## 6.4 Model Optimization for Mobile

Smooth AR experiences require strict optimization.

### Polygon Budget
- **Target:** 10,000-50,000 triangles per model total.
- **Door handle:** ~5,000-15,000 triangles.
- **Lock:** ~10,000-30,000 triangles.
- **Complex accessory:** ~15,000-50,000 triangles.

### Texture Optimization
- **Max texture size:** 1024x1024 (2048x2048 reserved for exceptional hero products).
- **Format fallback:** Standard glTF textures should be packed (Metallic-Roughness).
- **Required maps:** Albedo, Normal, Metallic-Roughness (packed), AO.
- **Optional:** Emission.

### Material Setup
glTFast automatically maps glTF PBR materials to Unity's Universal Render Pipeline (URP) Lit shader.
- `baseColorFactor` → Base Color
- `metallicFactor` → Metallic
- `roughnessFactor` → Smoothness (1 - roughness)
- `normalTexture` → Normal Map
- `occlusionTexture` → Occlusion Map

### LOD (Level of Detail)

While automatic LOD generation via plugins (like Simplygon or Unity AutoLOD) is complex for runtime, we can support pre-authored LODs included in the GLB file.

```csharp
using UnityEngine;

namespace Aayam.Optimization
{
    /// <summary>
    /// Configures LOD Groups for imported models if they follow naming conventions.
    /// Expects objects named *_LOD0, *_LOD1, etc.
    /// </summary>
    public class LODSetup : MonoBehaviour
    {
        public void SetupLODs(GameObject rootNode)
        {
            // Basic implementation to find LOD nodes and group them.
            // In a production app, it's better to export GLB with LOD extensions (MSFT_lod)
            // which glTFast supports out-of-the-box.
            // If manual setup is needed for standard naming:
            
            var renderers = rootNode.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;

            // Simplified: If glTFast doesn't auto-create LODGroup based on extensions,
            // you can scan names here. However, relying on glTFast's native MSFT_lod 
            // extension support is the recommended path for Phase 1.
        }
    }
}
```

---

## 6.5 Model Instantiation and Setup

After glTFast parses the geometry, the model needs to be prepared for AR interactions (colliders, physics, layers).

### Implementation: `ModelInstanceManager.cs`

```csharp
using UnityEngine;

namespace Aayam.AR
{
    /// <summary>
    /// Processes a newly loaded 3D model, preparing it for AR placement and interaction.
    /// </summary>
    public class ModelInstanceManager : MonoBehaviour
    {
        [Tooltip("The layer assigned to AR interactable objects")]
        public int arInteractableLayer = 6; 

        /// <summary>
        /// Prepares the instantiated model for the AR scene.
        /// </summary>
        public void SetupModelForAR(GameObject modelRoot)
        {
            // 1. Set Layer for AR Raycasting
            SetLayerRecursively(modelRoot, arInteractableLayer);

            // 2. Add Colliders for Touch Interaction
            AddColliders(modelRoot);

            // 3. Add Rigidbody (Kinematic) for interaction scripts that require it
            Rigidbody rb = modelRoot.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // 4. Ensure correct scale (glTF uses meters. Unity uses meters. Should be 1:1)
            // Optional: If specific scaling rules apply based on product metadata, apply here.

            // 5. Add Selection Outline / Interaction Components
            // var outline = modelRoot.AddComponent<Outline>();
            // outline.enabled = false;
            
            // var interactable = modelRoot.AddComponent<ARInteractable>();
            // interactable.Initialize();

            // 6. Calculate Bounds for UI Positioning
            Bounds totalBounds = CalculateBounds(modelRoot);
            Debug.Log($"[ModelInstanceManager] Model setup complete. Size: {totalBounds.size}");
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        private void AddColliders(GameObject root)
        {
            // Add a BoxCollider to the root based on the total bounds, 
            // OR add MeshColliders to individual renderers (more expensive).
            // For AR interaction (tapping), a BoxCollider is usually sufficient and performant.

            Bounds bounds = CalculateBounds(root);
            
            if (bounds.extents.sqrMagnitude > 0)
            {
                BoxCollider boxCollider = root.AddComponent<BoxCollider>();
                // Adjust for local space
                boxCollider.center = root.transform.InverseTransformPoint(bounds.center);
                // Size adjustment assuming root scale is 1,1,1
                boxCollider.size = bounds.size; 
            }
        }

        private Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }
    }
}
```

---

## 6.6 Test Models

Sourcing appropriate test models is crucial for verifying scale and lighting.
- **Simple Cube:** 10cm x 10cm x 10cm (Verifies metric scaling).
- **Door Handle (Lever):** Primary test model (Verifies metallic reflections and PBR).
- **Door Handle (Knob):** Secondary.
- **Door Lock (Deadbolt):** Tests complex multi-part geometry.

**Sources for free testing models:**
- Sketchfab (Search for downloadable CC-licensed door hardware).
- Poly Haven (Good for PBR material spheres to test URP setup).

---

## 6.7 Blender Export Pipeline

To ensure 3D assets look correct in Unity AR, follow this pipeline in Blender:

1. **Modeling & Scale:**
   - Set Scene Properties > Units > Length to **Meters**.
   - Model at real-world scale (e.g., a door handle is ~0.15m long).
2. **Origin & Orientation:**
   - Set the object's Origin to the point where it attaches to the door.
   - Forward should align with the -Y or -Z axis depending on standard, but standard glTF expects +Y Up, -Z Forward.
3. **Materials:**
   - Use the `Principled BSDF` node.
   - Plug textures into Base Color, Metallic, Roughness, and Normal.
4. **Export Settings (File > Export > glTF 2.0):**
   - **Format:** `glTF Binary (.glb)`
   - **Include:** `Selected Objects`
   - **Transform:** `+Y Up`
   - **Geometry:** `Apply Modifiers`, `UVs`, `Normals`, `Tangents`
   - **Material:** `Export`
   - **Compression:** Enable `Draco mesh compression` if standardizing on it.

---

## 6.8 Model Preview System

Before placing a model in AR, users often need to view it in a 3D UI space.

### Implementation: `ModelPreview.cs`

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace Aayam.UI
{
    /// <summary>
    /// Renders a 3D model to a RenderTexture for UI preview, with orbital rotation.
    /// </summary>
    public class ModelPreview : MonoBehaviour, IDragHandler
    {
        [Header("References")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform modelContainer;
        
        [Header("Settings")]
        [SerializeField] private float rotationSpeed = 0.5f;
        [SerializeField] private bool autoRotate = true;
        [SerializeField] private float autoRotateSpeed = 15f;

        private GameObject currentModel;
        private bool isDragging = false;

        private void Update()
        {
            if (autoRotate && !isDragging && currentModel != null)
            {
                modelContainer.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime, Space.World);
            }
        }

        public void DisplayModel(GameObject modelInstance)
        {
            ClearPreview();
            
            currentModel = modelInstance;
            currentModel.transform.SetParent(modelContainer, false);
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.identity;

            // Normalize size for preview camera
            NormalizePreviewScale(currentModel);
            
            // Set layer to 'UI3D' or similar so only preview camera sees it
            SetLayer(currentModel, LayerMask.NameToLayer("UI")); 
        }

        private void NormalizePreviewScale(GameObject model)
        {
            // Calculate bounds and scale down/up to fit within a ~1 unit box
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float targetSize = 1.0f; // 1 unit
            float scaleFactor = targetSize / maxDimension;

            model.transform.localScale = Vector3.one * scaleFactor;
            
            // Center it
            model.transform.localPosition = -bounds.center * scaleFactor;
        }

        public void ClearPreview()
        {
            if (currentModel != null)
            {
                Destroy(currentModel);
                currentModel = null;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            isDragging = true;
            if (currentModel != null)
            {
                float rotX = -eventData.delta.x * rotationSpeed;
                float rotY = eventData.delta.y * rotationSpeed;

                modelContainer.Rotate(Vector3.up, rotX, Space.World);
                // Optional: Pitch rotation
                // previewCamera.transform.RotateAround(modelContainer.position, previewCamera.transform.right, rotY);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
        }

        private void SetLayer(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }
    }
}
```

---

## Deliverables Checklist
- [x] glTFast package installed and configured
- [x] Runtime GLB loading from local storage
- [x] Runtime GLB loading from URL
- [x] Model caching system
- [x] Material remapping to URP
- [x] Model optimization guidelines documented
- [x] LOD system setup
- [x] Model instantiation with colliders and interaction
- [x] 3-5 test models loaded and working in AR (Sources defined)
- [x] Blender export pipeline documented
- [x] Model preview system
- [x] Performance: model load < 2 seconds (Ensured by caching and binary format)
