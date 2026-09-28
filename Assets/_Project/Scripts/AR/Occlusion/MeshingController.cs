using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Occlusion
{
    /// <summary>
    /// Controls ARMeshManager for LiDAR-equipped devices.
    /// Generated meshes use the OcclusionPlane material (depth-only)
    /// for high-fidelity environmental occlusion.
    ///
    /// Only enabled on Ultra-tier devices to avoid performance issues.
    /// </summary>
    [RequireComponent(typeof(ARMeshManager))]
    public class MeshingController : MonoBehaviour
    {
        [Header("Material")]
        [SerializeField] private Material _occlusionMaterial;

        [Header("Density")]
        [Tooltip("Mesh density (voxels per meter). Lower = faster, higher = more detailed.")]
        [SerializeField] private float _meshDensity = 2f;

        // ─── State ────────────────────────────────────────────────
        private ARMeshManager _meshManager;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _meshManager = GetComponent<ARMeshManager>();
            // Start disabled — OcclusionQualityManager enables when appropriate
            _meshManager.enabled = false;
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>Enable or disable real-time mesh generation.</summary>
        public void EnableMeshing(bool enable)
        {
            _meshManager.enabled = enable;

            if (enable)
            {
                _meshManager.density = _meshDensity;

                // Apply occlusion material to mesh prefab
                if (_meshManager.meshPrefab != null && _occlusionMaterial != null)
                {
                    var renderer = _meshManager.meshPrefab.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.material = _occlusionMaterial;
                }

                Debug.Log($"[Meshing] Enabled (density={_meshDensity}).");
            }
            else
            {
                _meshManager.DestroyAllMeshes();
                Debug.Log("[Meshing] Disabled — all meshes destroyed.");
            }
        }

        /// <summary>Returns the current mesh count for diagnostics.</summary>
        public int GetMeshCount()
        {
            int count = 0;
            foreach (var _ in _meshManager.meshes)
                count++;
            return count;
        }
    }
}
