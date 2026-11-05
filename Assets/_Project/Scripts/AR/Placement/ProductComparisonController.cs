using UnityEngine;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Phase 6 Step 7: side-by-side two-instance comparison + solo toggle.
    /// Not a wipe/slider - that needs render-pipeline infrastructure
    /// (second camera/stencil or RenderTexture compositing) this project
    /// has never built; this reuses existing multi-object placement
    /// support instead. Takes the two MOST RECENTLY placed products.
    /// Drives a single cycle via the existing _compareButton:
    ///   Inactive -> SideBySide -> SoloA -> SoloB -> Inactive -> ...
    /// </summary>
    public class ProductComparisonController : MonoBehaviour
    {
        private enum Mode { Inactive, SideBySide, SoloA, SoloB }

        [SerializeField] private ObjectPlacementController _placementController;
        [SerializeField] private Camera _arCamera;
        // Was 0.3f - confirmed live on-device the diagnostic math was
        // correct (clean Y-only rotation, exactly 0.3m apart) but the
        // labels still visually overlapped, because a long product name
        // (e.g. "SS Ball Bearing Hinge 4-inch") renders far wider than
        // 0.3m at this characterSize - the gap needs to out-size the text,
        // not just separate two points.
        [SerializeField] private float _gapMeters = 0.6f;

        private Mode _mode = Mode.Inactive;
        private PlacedProduct _productA;
        private PlacedProduct _productB;
        private Pose _originalPoseA;
        private Pose _originalPoseB;
        private TextMesh _labelA;
        private TextMesh _labelB;

        public bool IsActive => _mode != Mode.Inactive;

        /// <summary>Pure offset math (Phase 6 Stage 6 plan) - the two positions are exactly gapMeters apart, symmetric about center.</summary>
        public static Vector3[] SideBySidePositions(Vector3 center, Vector3 rightAxis, float gapMeters)
        {
            Vector3 half = rightAxis.normalized * (gapMeters * 0.5f);
            return new[] { center - half, center + half };
        }

        /// <summary>Called by the HUD's compare button - advances the cycle above.</summary>
        public void CycleCompare()
        {
            switch (_mode)
            {
                case Mode.Inactive: EnterComparisonMode(); break;
                case Mode.SideBySide: ShowSolo(showA: true); break;
                case Mode.SoloA: ShowSolo(showA: false); break;
                case Mode.SoloB: ExitComparisonMode(); break;
            }
        }

        public bool EnterComparisonMode()
        {
            var placed = _placementController != null ? _placementController.GetPlacedObjects() : null;
            if (placed == null || placed.Count < 2)
            {
                Debug.LogWarning("[ProductComparison] Need at least 2 placed products to compare.");
                return false;
            }

            _productA = placed[placed.Count - 2];
            _productB = placed[placed.Count - 1];
            _originalPoseA = _productA.PlacedPose;
            _originalPoseB = _productB.PlacedPose;

            Vector3 center = (_originalPoseA.position + _originalPoseB.position) * 0.5f;
            Vector3 cameraForward = _arCamera != null ? _arCamera.transform.forward : Vector3.forward;
            Vector3 rightAxis = Vector3.Cross(Vector3.up, cameraForward);
            if (rightAxis.sqrMagnitude < 0.0001f) rightAxis = Vector3.right;

            Vector3[] positions = SideBySidePositions(center, rightAxis, _gapMeters);
            _productA.Instance.transform.position = positions[0];
            _productB.Instance.transform.position = positions[1];

            EnsureLabel(ref _labelA, _productA);
            EnsureLabel(ref _labelB, _productB);
            SetVisible(_productA, true);
            SetVisible(_productB, true);

            _mode = Mode.SideBySide;
            Debug.Log($"[ProductComparison] Side-by-side: '{_productA.DisplayName}' vs '{_productB.DisplayName}'.");
            return true;
        }

        public void ToggleSolo()
        {
            if (_mode == Mode.SoloA) ShowSolo(showA: false);
            else ShowSolo(showA: true);
        }

        public void ExitComparisonMode()
        {
            if (_productA?.Instance != null)
            {
                _productA.Instance.transform.SetPositionAndRotation(_originalPoseA.position, _originalPoseA.rotation);
                SetVisible(_productA, true);
            }
            if (_productB?.Instance != null)
            {
                _productB.Instance.transform.SetPositionAndRotation(_originalPoseB.position, _originalPoseB.rotation);
                SetVisible(_productB, true);
            }
            DestroyLabel(ref _labelA);
            DestroyLabel(ref _labelB);
            _mode = Mode.Inactive;
            Debug.Log("[ProductComparison] Exited comparison mode.");
        }

        private void ShowSolo(bool showA)
        {
            SetVisible(_productA, showA);
            SetVisible(_productB, !showA);
            _mode = showA ? Mode.SoloA : Mode.SoloB;
        }

        private void Update()
        {
            if (!IsActive) return;
            if (_arCamera == null) return;
            BillboardLabel(_labelA, _productA);
            BillboardLabel(_labelB, _productB);
        }

        // ─── Labels (reuses DoorEdgeCalibrationUI's billboarded-TextMesh pattern) ──

        private void EnsureLabel(ref TextMesh label, PlacedProduct product)
        {
            if (label == null)
            {
                var labelObj = new GameObject($"ComparisonLabel_{product.ProductId}");
                label = labelObj.AddComponent<TextMesh>();
                label.fontSize = 48;
                // Was 0.02f - shrunk alongside the wider gap above so a
                // long product name's rendered width stays well inside it.
                label.characterSize = 0.012f;
                label.anchor = TextAnchor.MiddleCenter;
                label.color = Color.white;
            }
            // Truncated - a long name times two, side by side, needs more
            // room than any reasonable gap can give regardless of font
            // size; this keeps every label a predictable, comparable width.
            const int maxNameLength = 18;
            string displayName = string.IsNullOrEmpty(product.DisplayName) ? product.ProductId : product.DisplayName;
            if (displayName.Length > maxNameLength) displayName = displayName.Substring(0, maxNameLength - 1) + "…";
            label.text = $"{displayName}\n{product.PriceFormatted}";
        }

        private void BillboardLabel(TextMesh label, PlacedProduct product)
        {
            if (label == null || product?.Instance == null) return;
            label.transform.position = product.Instance.transform.position + Vector3.up * 0.15f;

            // Y-axis-only billboard: Quaternion.LookRotation(forward) (no
            // explicit up hint) degenerates when forward is close to
            // parallel with world-up - exactly what happens when the
            // camera looks steeply down at a label below it, which is the
            // common case for a door-mounted product. Confirmed live
            // on-device: label text rendered sideways/vertical and two
            // labels' text visually interleaved. Flattening the Y
            // component keeps "up" always exactly world-up, so the text
            // stays upright and readable regardless of camera tilt.
            Vector3 toCamera = label.transform.position - _arCamera.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private void DestroyLabel(ref TextMesh label)
        {
            if (label != null) Destroy(label.gameObject);
            label = null;
        }

        private void SetVisible(PlacedProduct product, bool visible)
        {
            if (product?.Instance != null) product.Instance.SetActive(visible);
        }
    }
}
