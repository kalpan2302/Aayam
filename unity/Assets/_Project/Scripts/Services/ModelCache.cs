using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Aayam.Services
{
    // ─── Data Types ───────────────────────────────────────────────

    [Serializable]
    public class ModelCacheEntry
    {
        public string   ProductId;
        public string   FileName;
        public long     SizeBytes;
        public string   CachedAtUtc;      // ISO-8601, avoid DateTime serialization issues
        public string   LastAccessedUtc;
    }

    [Serializable]
    public class ModelCacheManifest
    {
        public List<ModelCacheEntry> Entries = new List<ModelCacheEntry>();
    }

    // ─── Cache Manager ────────────────────────────────────────────

    /// <summary>
    /// Persists downloaded GLB files to disk with LRU eviction.
    /// Max size: 200 MB by default (configurable in Inspector).
    ///
    /// Storage: Application.persistentDataPath/ModelCache/
    /// Manifest: manifest.json (JSON, no external deps — uses JsonUtility)
    /// </summary>
    public class ModelCache : MonoBehaviour
    {
        [Header("Limits")]
        [Tooltip("Maximum cache size (bytes). Default = 200 MB.")]
        [SerializeField] private long _maxCacheBytes = 200L * 1024 * 1024;

        // ─── Paths ────────────────────────────────────────────────
        private string _cacheDir;
        private string _manifestPath;

        // ─── Manifest ─────────────────────────────────────────────
        private ModelCacheManifest _manifest;

        public long CurrentSizeBytes => _manifest.Entries.Sum(e => e.SizeBytes);
        public int  EntryCount       => _manifest.Entries.Count;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _cacheDir     = Path.Combine(Application.persistentDataPath, "ModelCache");
            _manifestPath = Path.Combine(_cacheDir, "manifest.json");

            Directory.CreateDirectory(_cacheDir);
            LoadManifest();
            PruneOrphanedFiles();
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Returns the local file path for a cached model, or null if not cached.
        /// Updates last-access time.
        /// </summary>
        public string GetCachedPath(string productId)
        {
            var entry = FindEntry(productId);
            if (entry == null) return null;

            string path = FilePath(entry.FileName);
            if (!File.Exists(path))
            {
                // Stale manifest entry — remove it
                _manifest.Entries.Remove(entry);
                SaveManifest();
                return null;
            }

            entry.LastAccessedUtc = DateTime.UtcNow.ToString("o");
            SaveManifest();
            return path;
        }

        /// <summary>
        /// Saves model bytes to disk and records it in the manifest.
        /// Runs LRU eviction first if needed.
        /// </summary>
        public string CacheModel(string productId, byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            EvictIfNeeded(data.Length);

            // Remove existing entry for this product (re-cache / update)
            RemoveEntry(productId);

            string fileName = $"{productId}.glb";
            string path     = FilePath(fileName);

            try
            {
                File.WriteAllBytes(path, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelCache] Write failed: {ex.Message}");
                return null;
            }

            _manifest.Entries.Add(new ModelCacheEntry
            {
                ProductId        = productId,
                FileName         = fileName,
                SizeBytes        = data.Length,
                CachedAtUtc      = DateTime.UtcNow.ToString("o"),
                LastAccessedUtc  = DateTime.UtcNow.ToString("o"),
            });

            SaveManifest();
            Debug.Log($"[ModelCache] Cached '{productId}' ({data.Length / 1024} KB). Total: {CurrentSizeBytes / 1024 / 1024} MB");
            return path;
        }

        /// <summary>Returns true if the product is already cached and the file exists.</summary>
        public bool IsCached(string productId)
        {
            var entry = FindEntry(productId);
            return entry != null && File.Exists(FilePath(entry.FileName));
        }

        /// <summary>Delete a single cached model.</summary>
        public void Evict(string productId)
        {
            RemoveEntry(productId);
        }

        /// <summary>Delete all cached models and reset the manifest.</summary>
        public void ClearAll()
        {
            foreach (var entry in _manifest.Entries)
            {
                string path = FilePath(entry.FileName);
                if (File.Exists(path)) File.Delete(path);
            }
            _manifest.Entries.Clear();
            SaveManifest();
            Debug.Log("[ModelCache] Cleared all cached models.");
        }

        // ─── LRU Eviction ─────────────────────────────────────────

        private void EvictIfNeeded(long incomingSizeBytes)
        {
            if (CurrentSizeBytes + incomingSizeBytes <= _maxCacheBytes) return;

            // Sort by oldest last-access time (LRU first)
            var sorted = _manifest.Entries
                .OrderBy(e => e.LastAccessedUtc)
                .ToList();

            foreach (var entry in sorted)
            {
                if (CurrentSizeBytes + incomingSizeBytes <= _maxCacheBytes) break;

                string path = FilePath(entry.FileName);
                if (File.Exists(path)) File.Delete(path);

                _manifest.Entries.Remove(entry);
                Debug.Log($"[ModelCache] LRU evicted: {entry.ProductId}");
            }

            SaveManifest();
        }

        // ─── Manifest I/O ─────────────────────────────────────────

        private void LoadManifest()
        {
            if (!File.Exists(_manifestPath))
            {
                _manifest = new ModelCacheManifest();
                return;
            }

            try
            {
                string json = File.ReadAllText(_manifestPath);
                _manifest = JsonUtility.FromJson<ModelCacheManifest>(json) ?? new ModelCacheManifest();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModelCache] Manifest load failed: {ex.Message} — starting fresh.");
                _manifest = new ModelCacheManifest();
            }
        }

        private void SaveManifest()
        {
            try
            {
                File.WriteAllText(_manifestPath, JsonUtility.ToJson(_manifest, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelCache] Manifest save failed: {ex.Message}");
            }
        }

        // ─── Helpers ──────────────────────────────────────────────

        private void PruneOrphanedFiles()
        {
            // Remove manifest entries whose files no longer exist
            int before = _manifest.Entries.Count;
            _manifest.Entries.RemoveAll(e => !File.Exists(FilePath(e.FileName)));
            if (_manifest.Entries.Count != before) SaveManifest();
        }

        private void RemoveEntry(string productId)
        {
            var entry = FindEntry(productId);
            if (entry == null) return;

            string path = FilePath(entry.FileName);
            if (File.Exists(path)) File.Delete(path);
            _manifest.Entries.Remove(entry);
            SaveManifest();
        }

        private ModelCacheEntry FindEntry(string productId)
            => _manifest.Entries.FirstOrDefault(e => e.ProductId == productId);

        private string FilePath(string fileName)
            => Path.Combine(_cacheDir, fileName);
    }
}
