using UnityEngine;
using Aayam.Models;
using Aayam.Utilities;

namespace Aayam.Services
{
    /// <summary>
    /// Phase 6 Step 1: computes the scale correction needed to make a
    /// freshly-instantiated GLB (currently localScale == Vector3.one,
    /// whatever size the model happened to be authored at) match the
    /// product's real-world dimensions. Must run before any collider/
    /// anchor-offset step that depends on final bounds (see
    /// ModelInstanceManager.SetupModelForAR's call order).
    ///
    /// Uniform, not per-axis: a product is rigid hardware, not a
    /// stretchable mesh, so a non-uniform correction would visibly
    /// distort it even if each axis individually "fixed" that axis's
    /// catalog dimension.
    /// </summary>
    public static class ProductScaleCalculator
    {
        /// <summary>Per-axis ratios disagreeing by more than this signal bad catalog data vs. model mismatch - warn, don't fail.</summary>
        public const float AxisMismatchWarningThreshold = 0.15f;

        public static bool TryComputeScaleFactor(GameObject modelRoot, ProductDimensions dims, out Vector3 scaleFactor, out string reason)
        {
            scaleFactor = Vector3.one;
            reason = null;

            if (modelRoot == null)
            {
                reason = "modelRoot is null";
                return false;
            }
            if (dims == null || (dims.WidthMm <= 0f && dims.HeightMm <= 0f && dims.DepthMm <= 0f))
            {
                reason = "no usable ProductDimensions";
                return false;
            }

            Bounds bounds = ModelInstanceManager.CalculateBounds(modelRoot);
            if (bounds.size == Vector3.zero)
            {
                reason = "no renderers found";
                return false;
            }

            var ratios = new System.Collections.Generic.List<float>();
            TryAddRatio(ratios, dims.WidthMm, bounds.size.x);
            TryAddRatio(ratios, dims.HeightMm, bounds.size.y);
            TryAddRatio(ratios, dims.DepthMm, bounds.size.z);

            if (ratios.Count == 0)
            {
                reason = "no axis had both a valid dimension and a non-zero current size";
                return false;
            }

            float min = ratios[0], max = ratios[0], sum = 0f;
            foreach (float r in ratios)
            {
                sum += r;
                if (r < min) min = r;
                if (r > max) max = r;
            }
            float uniformFactor = sum / ratios.Count;

            if (min > 0f && (max - min) / min > AxisMismatchWarningThreshold)
            {
                Debug.LogWarning($"[ProductScaleCalculator] '{modelRoot.name}': per-axis scale ratios disagree by " +
                                  $">{AxisMismatchWarningThreshold:P0} (min={min:F3}, max={max:F3}) - catalog dimensions " +
                                  "and the model's actual proportions may not match. Using the average anyway.");
            }

            scaleFactor = Vector3.one * uniformFactor;
            return true;
        }

        private static void TryAddRatio(System.Collections.Generic.List<float> ratios, float dimensionMm, float currentSizeMeters)
        {
            if (dimensionMm <= 0f || currentSizeMeters <= 0f) return;
            float targetSizeMeters = MathUtils.MmToMeters(dimensionMm);
            ratios.Add(targetSizeMeters / currentSizeMeters);
        }
    }
}
