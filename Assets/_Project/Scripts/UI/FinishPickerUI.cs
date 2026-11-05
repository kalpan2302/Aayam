using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aayam.AR.Core;
using Aayam.AR.Interaction;
using Aayam.AR.Placement;
using Aayam.Models;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 6 Step 6: full swatch picker - one tappable, color-matched
    /// button per finish on the most recently placed product, driven by
    /// MaterialSwitcher.SwitchTo(index) directly rather than cycling one
    /// step at a time. Replaces the earlier single "Next Finish" cycle
    /// button, which existed only to prove MaterialSwitcher worked end to
    /// end and was always documented as deferring the real picker UI.
    ///
    /// Rebuilds its swatch row every time a new product is placed (each
    /// product can have a different finish list) via AREventBus.OnObjectPlaced
    /// - that event fires with (position, rotation), not the placed object
    /// itself, so the handler re-reads ObjectPlacementController.GetPlacedObjects()
    /// rather than using the event args directly.
    /// </summary>
    public class FinishPickerUI : MonoBehaviour
    {
        [SerializeField] private ObjectPlacementController _placementController;
        [SerializeField] private RectTransform _swatchContainer;
        [SerializeField] private Text _finishLabel;

        [Header("Swatch appearance")]
        [SerializeField] private Vector2 _swatchSize = new Vector2(64, 64);
        [SerializeField] private float _ringPadding = 6f;

        private readonly List<Button> _swatchButtons = new();
        private MaterialSwitcher _currentSwitcher;

        private void OnEnable()
        {
            AREventBus.OnObjectPlaced += HandleObjectPlaced;
            RefreshForLastPlaced();
        }

        private void OnDisable()
        {
            AREventBus.OnObjectPlaced -= HandleObjectPlaced;
        }

        private void HandleObjectPlaced(Vector3 position, Quaternion rotation) => RefreshForLastPlaced();

        private void RefreshForLastPlaced()
        {
            var placed = _placementController?.GetPlacedObjects();
            GameObject instance = (placed != null && placed.Count > 0) ? placed[placed.Count - 1].Instance : null;
            _currentSwitcher = instance != null ? instance.GetComponent<MaterialSwitcher>() : null;
            BuildSwatches(_currentSwitcher);
        }

        // ─── Swatch row ───────────────────────────────────────────

        private void BuildSwatches(MaterialSwitcher switcher)
        {
            foreach (var btn in _swatchButtons)
                if (btn != null) Destroy(btn.gameObject);
            _swatchButtons.Clear();

            if (_swatchContainer == null) return;

            if (switcher == null || !switcher.HasVariants)
            {
                SetLabel(switcher == null ? "No product placed yet." : "This product has no alternate finishes.");
                return;
            }

            for (int i = 0; i < switcher.VariantCount; i++)
            {
                var variant = switcher.GetVariant(i);
                _swatchButtons.Add(CreateSwatchButton(i, variant, switcher));
            }

            SetLabel(switcher.CurrentVariant?.FinishName ?? "");
            HighlightSelected(switcher.CurrentIndex);
        }

        private Button CreateSwatchButton(int index, MaterialVariantData variant, MaterialSwitcher switcher)
        {
            var swatchGO = new GameObject($"Swatch_{variant.FinishName}", typeof(RectTransform));
            swatchGO.transform.SetParent(_swatchContainer, worldPositionStays: false);
            var rect = swatchGO.GetComponent<RectTransform>();
            rect.sizeDelta = _swatchSize;

            // Selection ring - a white Image sitting behind and slightly
            // larger than the swatch, toggled on/off rather than recreated,
            // so the currently-selected finish is visually obvious at a glance.
            var ringGO = new GameObject("SelectedRing", typeof(RectTransform));
            ringGO.transform.SetParent(swatchGO.transform, worldPositionStays: false);
            var ringRect = ringGO.GetComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = new Vector2(-_ringPadding, -_ringPadding);
            ringRect.offsetMax = new Vector2(_ringPadding, _ringPadding);
            ringGO.AddComponent<Image>().color = Color.white;
            ringGO.SetActive(false);

            var swatchImageGO = new GameObject("Swatch", typeof(RectTransform));
            swatchImageGO.transform.SetParent(swatchGO.transform, worldPositionStays: false);
            var swatchImageRect = swatchImageGO.GetComponent<RectTransform>();
            swatchImageRect.anchorMin = Vector2.zero;
            swatchImageRect.anchorMax = Vector2.one;
            swatchImageRect.offsetMin = Vector2.zero;
            swatchImageRect.offsetMax = Vector2.zero;
            var image = swatchImageGO.AddComponent<Image>();
            image.color = MaterialVariantSelector.TryParseBaseColor(variant.BaseColorHex, out Color c) ? c : Color.white;

            var button = swatchGO.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => HandleSwatchClicked(index, switcher));

            return button;
        }

        private void HandleSwatchClicked(int index, MaterialSwitcher switcher)
        {
            if (switcher == null) return;
            switcher.SwitchTo(index);
            SetLabel(switcher.CurrentVariant?.FinishName ?? "");
            HighlightSelected(index);
        }

        private void HighlightSelected(int selectedIndex)
        {
            for (int i = 0; i < _swatchButtons.Count; i++)
            {
                if (_swatchButtons[i] == null) continue;
                var ring = _swatchButtons[i].transform.Find("SelectedRing");
                if (ring != null) ring.gameObject.SetActive(i == selectedIndex);
            }
        }

        private void SetLabel(string text)
        {
            if (_finishLabel != null) _finishLabel.text = text;
            Debug.Log($"[FinishPickerUI] {text}");
        }
    }
}
