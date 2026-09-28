using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Aayam.UI
{
    // ─── Product Item Data ────────────────────────────────────────

    [Serializable]
    public class ProductUIItem
    {
        public string   ProductId;
        public string   Name;
        public string   Brand;
        public string   PriceFormatted;    // e.g. "₹1,499"
        public Sprite   Thumbnail;
        public string   CompatibilityLabel; // "✓ Fits your door" / "Check required"
        public Color    CompatibilityColor;
        public GameObject ModelPrefab;     // 3D prefab to place in AR
    }

    // ─── Product Card (per item) ──────────────────────────────────

    /// <summary>
    /// Controls a single product card in the horizontal scroll list.
    /// Attach to the product item prefab.
    /// </summary>
    public class ProductCard : MonoBehaviour
    {
        [SerializeField] private Image           _thumbnail;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private TextMeshProUGUI _compatText;
        [SerializeField] private Button          _selectButton;
        [SerializeField] private GameObject      _selectedIndicator;

        public event Action<ProductUIItem> OnSelected;
        private ProductUIItem _data;

        public void Bind(ProductUIItem data)
        {
            _data = data;

            if (_thumbnail   != null && data.Thumbnail != null) _thumbnail.sprite = data.Thumbnail;
            if (_nameText    != null) _nameText.text    = data.Name;
            if (_priceText   != null) _priceText.text   = data.PriceFormatted;
            if (_compatText  != null)
            {
                _compatText.text  = data.CompatibilityLabel;
                _compatText.color = data.CompatibilityColor;
            }

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => OnSelected?.Invoke(_data));
        }

        public void SetSelected(bool selected)
        {
            _selectedIndicator?.SetActive(selected);
        }
    }

    // ─── Product Selector (scroll list) ──────────────────────────

    /// <summary>
    /// Horizontal scrollable product tray at the bottom of the AR HUD.
    /// Populates product cards from a list and fires OnProductSelected
    /// when the user picks one.
    /// </summary>
    public class ProductSelector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform  _contentContainer;
        [SerializeField] private GameObject _productCardPrefab;

        [Header("AR HUD")]
        [SerializeField] private ARHUDController _hud;

        // ─── Events ───────────────────────────────────────────────
        public event Action<ProductUIItem> OnProductSelected;

        // ─── State ────────────────────────────────────────────────
        private readonly List<ProductCard> _cards = new();
        private ProductUIItem              _selectedItem;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            // Load stub data for Phase 1 testing
            LoadStubProducts();
        }

        // ─── Population ───────────────────────────────────────────

        /// <summary>Populate the selector with real product data.</summary>
        public void Populate(List<ProductUIItem> products)
        {
            ClearCards();

            foreach (var p in products)
            {
                var go   = Instantiate(_productCardPrefab, _contentContainer);
                var card = go.GetComponent<ProductCard>();
                if (card == null) continue;

                card.Bind(p);
                card.OnSelected += HandleProductSelected;
                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
            _selectedItem = null;
        }

        // ─── Selection ────────────────────────────────────────────

        private void HandleProductSelected(ProductUIItem item)
        {
            _selectedItem = item;

            // Highlight selected card
            foreach (var card in _cards)
                card.SetSelected(false);

            // Find matching card and highlight
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].gameObject.name.Contains(item.ProductId))
                    _cards[i].SetSelected(true);
            }

            OnProductSelected?.Invoke(item);

            // Start AR placement
            if (item.ModelPrefab != null)
                _hud?.BeginPlacementFor(item.ModelPrefab, item.ProductId);

            Debug.Log($"[ProductSelector] Selected: {item.Name} ({item.ProductId})");
        }

        // ─── Stub Data ────────────────────────────────────────────

        private void LoadStubProducts()
        {
            var stubs = new List<ProductUIItem>
            {
                new() { ProductId = "HANDLE_001", Name = "Matte Black Handle",  Brand = "Godrej", PriceFormatted = "₹1,499", CompatibilityLabel = "✓ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "HANDLE_002", Name = "Brushed Gold Lever",  Brand = "Dorset", PriceFormatted = "₹2,299", CompatibilityLabel = "✓ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "LOCK_001",   Name = "Smart Deadbolt",      Brand = "Yale",   PriceFormatted = "₹8,999", CompatibilityLabel = "Check bore size",  CompatibilityColor = new Color(1f, 0.7f, 0f) },
                new() { ProductId = "LOCK_002",   Name = "Mortise Lock Set",    Brand = "Godrej", PriceFormatted = "₹3,799", CompatibilityLabel = "✓ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "ACCS_001",   Name = "Chrome Door Stopper", Brand = "Ebco",   PriceFormatted = "₹299",   CompatibilityLabel = "Universal",        CompatibilityColor = new Color(0.6f, 0.6f, 0.6f) },
            };

            Populate(stubs);
        }
    }
}
