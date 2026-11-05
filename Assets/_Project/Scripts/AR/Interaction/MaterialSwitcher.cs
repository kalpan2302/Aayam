using UnityEngine;
using Aayam.Models;

namespace Aayam.AR.Interaction
{
    /// <summary>
    /// Phase 6 Step 6: runtime finish switching for a placed product.
    /// Mutates each renderer's INSTANCE material (never .sharedMaterial -
    /// that would bleed the change onto every other instance sharing the
    /// same source material). Targets glTFast's own Built-in-RP shader
    /// property names (confirmed via its shader source,
    /// Runtime/Shader/Built-In/glTFPbrMetallicRoughness.shader):
    /// "baseColorFactor", "metallicFactor", "roughnessFactor" - no
    /// leading underscore, unlike Unity's Standard shader.
    /// </summary>
    public class MaterialSwitcher : MonoBehaviour
    {
        private static readonly int BaseColorFactorId = Shader.PropertyToID("baseColorFactor");
        private static readonly int MetallicFactorId = Shader.PropertyToID("metallicFactor");
        private static readonly int RoughnessFactorId = Shader.PropertyToID("roughnessFactor");

        private Renderer[] _targetRenderers;
        private MaterialVariantData[] _variants;

        public int CurrentIndex { get; private set; } = -1;
        public bool HasVariants => _variants != null && _variants.Length > 0;
        public int VariantCount => _variants?.Length ?? 0;

        /// <summary>Read-only access to a single variant's data - used by
        /// FinishSwatchPickerUI to render one swatch per finish without
        /// exposing the backing array directly.</summary>
        public MaterialVariantData GetVariant(int index) =>
            (_variants != null && index >= 0 && index < _variants.Length) ? _variants[index] : null;

        public void Initialize(Renderer[] targetRenderers, MaterialVariantData[] variants)
        {
            _targetRenderers = targetRenderers ?? System.Array.Empty<Renderer>();
            _variants = variants ?? System.Array.Empty<MaterialVariantData>();

            if (_variants.Length == 0) return;

            int defaultIndex = MaterialVariantSelector.FindDefaultIndex(_variants);
            SwitchTo(defaultIndex);
        }

        /// <summary>Switches every target renderer to variants[index]. Wraps around (cycle-friendly).</summary>
        public void SwitchTo(int index)
        {
            if (!HasVariants) return;

            index = ((index % _variants.Length) + _variants.Length) % _variants.Length;
            var variant = _variants[index];

            if (!MaterialVariantSelector.TryParseBaseColor(variant.BaseColorHex, out Color color))
            {
                Debug.LogWarning($"[MaterialSwitcher] Couldn't parse BaseColorHex '{variant.BaseColorHex}' " +
                                  $"for finish '{variant.FinishName}' - falling back to white.");
            }

            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;
                var mat = renderer.material; // instance, not shared
                mat.SetColor(BaseColorFactorId, color);
                mat.SetFloat(MetallicFactorId, variant.MetallicFactor);
                mat.SetFloat(RoughnessFactorId, variant.RoughnessFactor);
            }

            CurrentIndex = index;
        }

        /// <summary>Advances to the next finish, wrapping to the first after the last.</summary>
        public void CycleNext()
        {
            if (!HasVariants) return;
            SwitchTo(CurrentIndex + 1);
        }

        public MaterialVariantData CurrentVariant => HasVariants && CurrentIndex >= 0 ? _variants[CurrentIndex] : null;
    }

    /// <summary>Pure selection logic, extracted for EditMode testing (no MonoBehaviour/renderer dependency).</summary>
    public static class MaterialVariantSelector
    {
        /// <summary>Returns the index of the first variant with IsDefault == true, or 0 if none/empty.</summary>
        public static int FindDefaultIndex(MaterialVariantData[] variants)
        {
            if (variants == null || variants.Length == 0) return 0;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].IsDefault) return i;
            }
            return 0;
        }

        /// <summary>Thin wrapper over ColorUtility.TryParseHtmlString with a white fallback on failure - extracted so the fallback behavior is testable without a Renderer/Material.</summary>
        public static bool TryParseBaseColor(string hex, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(hex, out color)) return true;
            color = Color.white;
            return false;
        }
    }
}
