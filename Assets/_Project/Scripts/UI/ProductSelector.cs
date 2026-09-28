using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aayam.UI
{
    // â”€â”€â”€ Product Item Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Serializable]
    public class ProductUIItem
    {
        public string   ProductId;
        public string   Name;
        public string   Brand;
        public string   PriceFormatted;    // e.g. "â‚¹1,499"
        public Sprite   Thumbnail;
        public string   CompatibilityLabel; // "âœ“ Fits your door" / "Check required"
        public Color    CompatibilityColor;
        public GameObject ModelPrefab;     // 3D prefab to place in AR
    }

    // â”€â”€â”€ Product Card (per item) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Controls a single product card in the horizontal scroll list.
    /// Attach to the product item prefab.
    /// </summary>
    public class ProductCard : MonoBehaviour
    {
        [SerializeField] private Image           _thumbnail;
        [SerializeField] private Text   _nameText;
        [SerializeField] private Text   _priceText;
        [SerializeField] private Text   _compatText;
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

    // â”€â”€â”€ Product Selector (scroll list) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public event Action<ProductUIItem> OnProductSelected;

        // â”€â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly List<ProductCard> _cards = new();
        private ProductUIItem              _selectedItem;

        // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Start()
        {
            // Load stub data for Phase 1 testing
            LoadStubProducts();
        }

        // â”€â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€â”€ Selection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€â”€ Stub Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void LoadStubProducts()
        {
            var stubs = new List<ProductUIItem>
            {
                new() { ProductId = "HANDLE_001", Name = "Matte Black Handle",  Brand = "Godrej", PriceFormatted = "â‚¹1,499", CompatibilityLabel = "âœ“ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "HANDLE_002", Name = "Brushed Gold Lever",  Brand = "Dorset", PriceFormatted = "â‚¹2,299", CompatibilityLabel = "âœ“ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "LOCK_001",   Name = "Smart Deadbolt",      Brand = "Yale",   PriceFormatted = "â‚¹8,999", CompatibilityLabel = "Check bore size",  CompatibilityColor = new Color(1f, 0.7f, 0f) },
                new() { ProductId = "LOCK_002",   Name = "Mortise Lock Set",    Brand = "Godrej", PriceFormatted = "â‚¹3,799", CompatibilityLabel = "âœ“ Fits your door", CompatibilityColor = new Color(0.2f, 0.8f, 0.2f) },
                new() { ProductId = "ACCS_001",   Name = "Chrome Door Stopper", Brand = "Ebco",   PriceFormatted = "â‚¹299",   CompatibilityLabel = "Universal",        CompatibilityColor = new Color(0.6f, 0.6f, 0.6f) },
            };

            Populate(stubs);
        }
    }
}
