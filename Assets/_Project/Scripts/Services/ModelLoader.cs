using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// NOTE: glTFast must be installed via Package Manager: com.unity.cloud.gltfast
// Add it before compiling this file.
#if GLTFAST_PRESENT
using GLTFast;
using GLTFast.Logging;
#endif

namespace Aayam.Services
{
    /// <summary>
    /// Asynchronously loads GLB/glTF 3D models from:
    ///   1. Local cache (fastest)
    ///   2. Local file path (StreamingAssets, persistentData)
    ///   3. Remote URL (with progress reporting)
    ///
    /// Requires: com.unity.cloud.gltfast (install via Package Manager)
    /// Namespace guard: wrap in #if GLTFAST_PRESENT after package is added.
    /// </summary>
    public class ModelLoader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ModelCache _modelCache;

        [Header("Settings")]
        [SerializeField] private bool _generateMipMaps        = true;
        [SerializeField] private int  _anisotropicFilterLevel = 3;
        [SerializeField] private int  _downloadTimeoutSec     = 30;

        // Serialized asset reference, not CreatePrimitive's bare default
        // material - same root issue found project-wide 2026-10-21
        // (DoorPnPWorldAnchor, PlacementReticle, HandleWorldAnchorRefiner
        // all had it too): this project has no render pipeline package
        // installed, and CreatePrimitive's default material has repeatedly
        // shown as the "missing shader" fallback rather than anything
        // readable. Assign Assets/_Project/Materials/PlaceholderCube.mat.
        [SerializeField] private Material _placeholderMaterialTemplate;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_modelCache == null)
                _modelCache = FindFirstObjectByType<ModelCache>();

            if (_modelCache == null)
                Debug.LogWarning("[ModelLoader] ModelCache not found — caching disabled.");
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Load a GLB from a URL. Checks cache first; downloads and caches if not found.
        /// onProgress reports 0→1 (download = 0-0.9, instantiation = 0.9-1.0).
        /// </summary>
        public async Task<GameObject> LoadFromUrlAsync(
            string url,
            string productId,
            Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                // 1. Cache hit
                if (_modelCache != null)
                {
                    string cached = _modelCache.GetCachedPath(productId);
                    if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
                    {
                        Debug.Log($"[ModelLoader] Cache hit: {productId}");
                        return await LoadFromPathAsync(cached, onProgress);
                    }
                }

                // 2. Download
                Debug.Log($"[ModelLoader] Downloading: {url}");
                byte[] data = await DownloadBytesAsync(url, p => onProgress?.Invoke(p * 0.9f));

                if (data == null || data.Length == 0)
                {
                    Debug.LogError($"[ModelLoader] Download returned empty data for {productId}.");
                    return null;
                }

                // 3. Cache to disk
                _modelCache?.CacheModel(productId, data);

                // 4. Instantiate from bytes
                onProgress?.Invoke(0.9f);
                var go = await InstantiateFromBytesAsync(data);
                onProgress?.Invoke(1f);
                return go;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] URL load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Load from a local absolute path (StreamingAssets, persistentData, cache).</summary>
        public async Task<GameObject> LoadFromPathAsync(string path, Action<float> onProgress = null)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ModelLoader] File not found: {path}");
                return null;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);
                onProgress?.Invoke(0.8f);
                var go = await InstantiateFromBytesAsync(data);
                onProgress?.Invoke(1f);
                return go;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] Path load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Load from StreamingAssets (synchronous read, async instantiate).</summary>
        public async Task<GameObject> LoadFromStreamingAssetsAsync(string relPath, Action<float> onProgress = null)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relPath);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: StreamingAssets is inside a JAR — use UnityWebRequest
            byte[] data = await DownloadBytesAsync("file://" + fullPath, p => onProgress?.Invoke(p * 0.8f));
            if (data == null) return null;
            var go = await InstantiateFromBytesAsync(data);
            onProgress?.Invoke(1f);
            return go;
#else
            return await LoadFromPathAsync(fullPath, onProgress);
#endif
        }

        // ─── Core Instantiation ───────────────────────────────────

        private async Task<GameObject> InstantiateFromBytesAsync(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

#if GLTFAST_PRESENT
            var root = new GameObject("GLB_Model");
            try
            {
                // glTFast 6.x's GltfImport ctor is (downloadProvider, deferAgent,
                // materialGenerator, logger) - logger is 4th, not 2nd. Named arg
                // so this doesn't silently break again on a future reorder.
                var gltf = new GltfImport(logger: new ConsoleLogger());

                var settings = new ImportSettings
                {
                    GenerateMipMaps        = _generateMipMaps,
                    AnisotropicFilterLevel = _anisotropicFilterLevel,
                    NodeNameMethod         = NameImportMethod.OriginalUnique
                };

                bool ok = await gltf.LoadGltfBinary(data, new Uri("file://model.glb"), settings);
                if (!ok)
                {
                    Debug.LogError("[ModelLoader] glTFast failed to parse binary.");
                    Destroy(root);
                    return null;
                }

                var instantiator = new GameObjectInstantiator(gltf, root.transform, new ConsoleLogger());
                ok = await gltf.InstantiateMainSceneAsync(instantiator);
                if (!ok)
                {
                    Debug.LogError("[ModelLoader] glTFast failed to instantiate scene.");
                    Destroy(root);
                    return null;
                }

                return root;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLoader] Instantiation exception: {ex.Message}\n{ex.StackTrace}");
                Destroy(root);
                return null;
            }
#else
            Debug.LogWarning("[ModelLoader] glTFast not installed. Returning placeholder cube.");
            return CreatePlaceholderCube();
#endif
        }

        // ─── Network ──────────────────────────────────────────────

        private async Task<byte[]> DownloadBytesAsync(string url, Action<float> onProgress)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = _downloadTimeoutSec;
            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                onProgress?.Invoke(req.downloadProgress);
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ModelLoader] Download error: {req.error}");
                return null;
            }

            return req.downloadHandler.data;
        }

        // ─── Helpers ──────────────────────────────────────────────

        /// <summary>Destroy a loaded model GameObject cleanly.</summary>
        public void Unload(GameObject model)
        {
            if (model != null) Destroy(model);
        }

        private GameObject CreatePlaceholderCube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Placeholder_Model";
            cube.transform.localScale = Vector3.one * 0.1f;   // 10cm cube

            if (_placeholderMaterialTemplate != null)
            {
                var renderer = cube.GetComponent<MeshRenderer>();
                renderer.material = new Material(_placeholderMaterialTemplate);
            }
            else
            {
                Debug.LogWarning("[ModelLoader] _placeholderMaterialTemplate not assigned - " +
                                  "falling back to CreatePrimitive's default material.");
            }

            return cube;
        }
    }
}
