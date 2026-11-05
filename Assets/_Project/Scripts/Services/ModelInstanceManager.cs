using UnityEngine;
using Aayam.AR.Core;
using Aayam.Models;

namespace Aayam.Services
{
    /// <summary>
    /// Prepares a freshly loaded GLB model for AR interaction.
    /// Applies layer assignment, adds a BoxCollider for touch selection,
    /// attaches a kinematic Rigidbody, and computes world bounds.
    ///
    /// Call SetupModelForAR() immediately after ModelLoader returns the GameObject.
    /// </summary>
    public class ModelInstanceManager : MonoBehaviour
    {
        [Header("Layer Settings")]
        [Tooltip("Unity layer index for AR-selectable objects. Default: 6 (AR Interactable).")]
        [SerializeField] private int _arInteractableLayer = 6;

        [Header("Collider")]
        [Tooltip("Use BoxCollider (fast, recommended) vs per-mesh MeshCollider (accurate, expensive).")]
        [SerializeField] private bool _useBoxCollider = true;

        [Header("Anchor Offsets")]
        [Tooltip("If true, the model root is offset so the anchor point (origin) sits on the door surface.")]
        [SerializeField] private bool _applyAnchorOffset = true;

        [Header("Shadows & LOD (Phase 6)")]
        [Tooltip("Optional - gates real-time shadows/LOD setup by device tier. Shadows off entirely on Low-tier devices for perf.")]
        [SerializeField] private DeviceCapabilityChecker _capabilityChecker;
        [Tooltip("Optional - sets up an LODGroup when the model has *_LOD0/_LOD1/_LOD2 meshes or glTFast's MSFT_lod. No-op otherwise.")]
        [SerializeField] private LODSetup _lodSetup;

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// Full AR setup for an instantiated model. Call once after ModelLoader returns.
        /// Pass the product's catalog dimensions so the model is corrected to its
        /// real-world size (Phase 6 Step 1) before collider/anchor steps run -
        /// both depend on final bounds.
        /// </summary>
        public void SetupModelForAR(GameObject modelRoot, string productCategory = "", ProductDimensions dimensions = null)
        {
            if (modelRoot == null) return;

            // 0. Scale correction (must run first - everything below reads bounds)
            if (ProductScaleCalculator.TryComputeScaleFactor(modelRoot, dimensions, out Vector3 scaleFactor, out string reason))
            {
                modelRoot.transform.localScale = Vector3.Scale(modelRoot.transform.localScale, scaleFactor);
            }
            else if (reason != null)
            {
                Debug.Log($"[ModelInstance] No scale correction applied to '{modelRoot.name}': {reason}");
            }

            // 0b. LOD setup (Phase 6 Step 9) - a no-op today, since the
            // placeholder GLBs have no *_LOD0/_LOD1/_LOD2 meshes or
            // MSFT_lod extension, but wired now so it's live the moment a
            // real LOD-authored asset exists.
            _lodSetup?.SetupLODs(modelRoot);

            // 1. Layer
            SetLayerRecursive(modelRoot, _arInteractableLayer);

            // 2. Kinematic Rigidbody (prevents physics from moving the model)
            if (modelRoot.GetComponent<Rigidbody>() == null)
            {
                var rb         = modelRoot.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity  = false;
            }

            // 3. Collider (for tap selection)
            AddCollider(modelRoot);

            // 4. Anchor offset (center model so origin = door-surface contact point)
            if (_applyAnchorOffset)
                CenterOnBoundsOrigin(modelRoot);

            // 5. Shadows/LOD, gated by device tier (Phase 6 Step 4/9)
            ApplyShadowAndLodPolicy(modelRoot);

            // 6. Debug bounds log
            Bounds b = CalculateBounds(modelRoot);
            Debug.Log($"[ModelInstance] Setup complete '{modelRoot.name}' | Bounds: {b.size:F3} m");
        }

        /// <summary>
        /// Compute and return the world-space bounds of all renderers in the model.
        /// </summary>
        public static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        // ─── Private Helpers ──────────────────────────────────────

        /// <summary>
        /// Phase 6 Step 4 (shadows) + Step 9 (LOD): Low-tier devices get
        /// LODSetup.OptimizeRenderers's perf-first treatment (shadows off,
        /// GPU instancing on); everything else gets real shadows enabled.
        /// Defaults to "shadows on" when no DeviceCapabilityChecker is
        /// wired (matches this method's own tier-unknown fallback).
        /// </summary>
        private void ApplyShadowAndLodPolicy(GameObject modelRoot)
        {
            DeviceQualityTier tier = _capabilityChecker != null ? _capabilityChecker.QualityTier : DeviceQualityTier.Medium;

            if (!ShadowPolicy.ShouldCastShadows(tier))
            {
                Debug.Log($"[ShadowDiag] tier={tier} - shadows disabled, using LODSetup.OptimizeRenderers.");
                LODSetup.OptimizeRenderers(modelRoot);
                return;
            }

            int count = 0;
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows    = true;
                count++;
            }
            Debug.Log($"[ShadowDiag] tier={tier} - enabled shadowCastingMode=On on {count} renderer(s) under '{modelRoot.name}'.");
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void AddCollider(GameObject root)
        {
            // Only add if one doesn't exist
            if (root.GetComponentInChildren<Collider>() != null) return;

            Bounds bounds = CalculateBounds(root);
            if (bounds.extents.sqrMagnitude < 0.0001f) return;

            if (_useBoxCollider)
            {
                var box    = root.AddComponent<BoxCollider>();
                // Convert world-space bounds center to root local space
                box.center = root.transform.InverseTransformPoint(bounds.center);
                box.size   = bounds.size;
            }
            else
            {
                // Per-mesh colliders — more accurate but expensive
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh != null)
                    {
                        var mc  = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh   = mf.sharedMesh;
                        mc.convex       = true;   // Convex required for physics queries
                    }
                }
            }
        }

        private void CenterOnBoundsOrigin(GameObject root)
        {
            Bounds bounds = CalculateBounds(root);
            if (bounds.extents.sqrMagnitude < 0.0001f) return;

            // Shift all children so the model bottom-center aligns with the root transform origin
            Vector3 offset = root.transform.InverseTransformPoint(bounds.center);
            offset.y = root.transform.InverseTransformPoint(bounds.min).y;

            foreach (Transform child in root.transform)
                child.localPosition -= offset;
        }
    }
}
