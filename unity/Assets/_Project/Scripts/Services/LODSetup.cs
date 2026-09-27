using UnityEngine;

namespace Aayam.Services
{
    /// <summary>
    /// Configures LOD (Level of Detail) groups on imported GLB models.
    ///
    /// Preferred: Export GLB with MSFT_lod extension — glTFast handles this natively.
    ///
    /// Fallback (this script): Scans the model hierarchy for objects named
    ///   *_LOD0, *_LOD1, *_LOD2  and groups them into a Unity LODGroup.
    ///
    /// LOD transition distances (screen-relative):
    ///   LOD0: 60% → 100% screen height  (full detail)
    ///   LOD1: 25% → 60%                 (medium)
    ///   LOD2:  0% → 25%                 (low / billboard)
    /// </summary>
    public class LODSetup : MonoBehaviour
    {
        [Header("Screen-Relative LOD Distances")]
        [SerializeField] [Range(0f, 1f)] private float _lod0Threshold = 0.60f;
        [SerializeField] [Range(0f, 1f)] private float _lod1Threshold = 0.25f;

        [Header("Culling")]
        [SerializeField] [Range(0f, 0.1f)] private float _cullThreshold = 0.02f;

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Attempts to set up an LODGroup on rootNode.
        /// Returns true if LODs were found and configured.
        /// </summary>
        public bool SetupLODs(GameObject rootNode)
        {
            if (rootNode == null) return false;

            // 1. If glTFast already created an LODGroup — done.
            if (rootNode.GetComponentInChildren<LODGroup>() != null)
            {
                Debug.Log($"[LODSetup] '{rootNode.name}' already has LODGroup (glTFast MSFT_lod).");
                return true;
            }

            // 2. Try manual name-based detection (*_LOD0, *_LOD1, *_LOD2)
            var lod0Renderers = FindRenderersByLODSuffix(rootNode, "_LOD0");
            if (lod0Renderers.Length == 0)
            {
                Debug.Log($"[LODSetup] No LOD meshes found in '{rootNode.name}'. Skipping.");
                return false;
            }

            var lod1Renderers = FindRenderersByLODSuffix(rootNode, "_LOD1");
            var lod2Renderers = FindRenderersByLODSuffix(rootNode, "_LOD2");

            // 3. Create LODGroup
            var lodGroup = rootNode.AddComponent<LODGroup>();

            int lodCount = 1 + (lod1Renderers.Length > 0 ? 1 : 0) + (lod2Renderers.Length > 0 ? 1 : 0);
            var lods = new LOD[lodCount];

            lods[0] = new LOD(_lod0Threshold, lod0Renderers);

            if (lod1Renderers.Length > 0)
                lods[1] = new LOD(_lod1Threshold, lod1Renderers);

            if (lod2Renderers.Length > 0)
                lods[lodCount - 1] = new LOD(_cullThreshold, lod2Renderers);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            Debug.Log($"[LODSetup] '{rootNode.name}': {lodCount} LOD levels configured.");
            return true;
        }

        // ─── Optimization Helpers ─────────────────────────────────

        /// <summary>
        /// Apply recommended mobile AR renderer settings to all renderers in a model:
        ///   - Disable shadow casting (saves fill rate)
        ///   - Enable GPU instancing
        /// </summary>
        public static void OptimizeRenderers(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows     = false;

                foreach (var mat in r.materials)
                {
                    if (mat != null) mat.enableInstancing = true;
                }
            }
        }

        /// <summary>Returns the total triangle count across all meshes in the model.</summary>
        public static int GetTriangleCount(GameObject root)
        {
            int total = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null)
                    total += mf.sharedMesh.triangles.Length / 3;
            }
            return total;
        }

        // ─── Private ──────────────────────────────────────────────

        private static Renderer[] FindRenderersByLODSuffix(GameObject root, string suffix)
        {
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                    result.Add(r);
            }
            return result.ToArray();
        }
    }
}
