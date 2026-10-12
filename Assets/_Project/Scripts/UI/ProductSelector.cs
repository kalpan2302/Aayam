using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Aayam.Core.Bootstrap;
using Aayam.Services;

namespace Aayam.UI
{
    // â”€â”€â”€ Product Item Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // Design-time override: assign a prefab in the Inspector to skip the
        // network fetch entirely (useful for UI testing without a backend).
        public GameObject ModelPrefab;

        // Real path: populated when this item came from the backend. If
        // ModelPrefab is unset, HandleProductSelected downloads this instead.
        public string    ModelUrl;
        public string    Sku;
        public float      AnchorYMm = -1f;   // height from floor, mm; -1 = no anchor data
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

        [Header("Model Loading")]
        [SerializeField] private Aayam.Services.ModelLoader _modelLoader;
        [SerializeField] private Aayam.Services.ModelInstanceManager _modelInstanceManager;

        // â”€â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public event Action<ProductUIItem> OnProductSelected;

        // â”€â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly List<ProductCard> _cards = new();
        private ProductUIItem              _selectedItem;
        private ProductApiService          _apiService;

        // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            if (_modelLoader == null)
                _modelLoader = FindFirstObjectByType<Aayam.Services.ModelLoader>();
            if (_modelInstanceManager == null)
                _modelInstanceManager = FindFirstObjectByType<Aayam.Services.ModelInstanceManager>();

            // ServiceLocator.TryGet() throws if Initialize() was never called,
            // despite the "Try" name - IsRegistered() is the one that's actually
            // safe pre-init (it just checks the dictionary, no EnsureInitialized()
            // call). There's no AppBootstrapper in this test scene (it would try
            // to load a scene called "ARScene" that doesn't exist), so this path
            // always falls through to AppConfig.Load() here - that's expected.
            AppConfig config = ServiceLocator.IsRegistered<AppConfig>()
                ? ServiceLocator.Get<AppConfig>()
                : AppConfig.Load();
            _apiService = new ProductApiService(config.ApiBaseUrl, config.CdnBaseUrl);
        }

        private async void Start()
        {
            // Try the real backend first; fall back to stub data (e.g. the
            // backend isn't running, or this is a pure UI-in-Editor test) so
            // the screen is never left empty.
            List<ProductUIItem> items = null;
            try
            {
                var summaries = await _apiService.FetchProductListAsync();
                if (summaries.Count > 0)
                {
                    items = new List<ProductUIItem>();
                    foreach (var s in summaries)
                    {
                        items.Add(new ProductUIItem
                        {
                            ProductId          = s.Sku,
                            Sku                 = s.Sku,
                            Name                = s.Name,
                            Brand               = s.BrandName,
                            PriceFormatted      = FormatPrice(s.Price, s.Currency),
                            CompatibilityLabel  = s.Has3DModel ? "" : "No 3D model yet",
                            CompatibilityColor  = s.Has3DModel ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.6f, 0.2f),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProductSelector] Backend fetch failed, using stub data: {ex.Message}");
            }

            if (items != null) Populate(items);
            else LoadStubProducts();
        }

        private static string FormatPrice(float price, string currency)
        {
            string symbol = currency == "INR" ? "₹" : (currency ?? "") + " ";
            return $"{symbol}{price:N0}";
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

        private async void HandleProductSelected(ProductUIItem item)
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
            Debug.Log($"[ProductSelector] Selected: {item.Name} ({item.ProductId})");

            // Design-time override takes priority - skip the network entirely.
            if (item.ModelPrefab != null)
            {
                _hud?.BeginPlacementFor(item.ModelPrefab, item.ProductId, item.AnchorYMm);
                return;
            }

            string sku = !string.IsNullOrEmpty(item.Sku) ? item.Sku : item.ProductId;
            await LoadAndPlaceAsync(sku);
        }

        /// <summary>Entry point for selection paths that don't go through a
        /// catalogue card - currently the QR scanner (decoded "aayam://product/{sku}"
        /// link via DeepLinkParser). Does the same fetch -> download -> place
        /// flow as tapping a card.</summary>
        public async void SelectProductBySku(string sku)
        {
            await LoadAndPlaceAsync(sku);
        }

        /// <summary>Fetch full detail (list items don't carry model_url/anchor
        /// data - only the detail endpoint does), download the GLB, then begin
        /// placement with an already-loaded instance.</summary>
        private async Task LoadAndPlaceAsync(string sku)
        {
            Aayam.Models.ProductData detail = null;
            try { detail = await _apiService.FetchProductDetailAsync(sku); }
            catch (Exception ex)
            {
                Debug.LogError($"[ProductSelector] Failed to fetch detail for '{sku}': {ex.Message}");
            }

            if (detail?.Asset?.ModelUrl == null)
            {
                Debug.LogWarning($"[ProductSelector] No model available for '{sku}' - cannot start placement.");
                return;
            }

            if (_modelLoader == null)
            {
                Debug.LogError("[ProductSelector] No ModelLoader assigned/found - cannot load model.");
                return;
            }

            GameObject instance = await _modelLoader.LoadFromUrlAsync(detail.Asset.ModelUrl, sku);
            if (instance == null)
            {
                Debug.LogError($"[ProductSelector] Model load failed for '{sku}'.");
                return;
            }

            _modelInstanceManager?.SetupModelForAR(instance, detail.Category);
            // ExecutePlacement (ObjectPlacementController) instantiates a clone of
            // this at placement time - hide the loaded template until then.
            instance.SetActive(false);

            float anchorYMm = detail.Asset.Anchor?.YMm ?? -1f;
            _hud?.BeginPlacementFor(instance, sku, anchorYMm);
        }

        // â”€â”€â”€ Stub Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
