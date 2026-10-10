using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Aayam.Models;

namespace Aayam.Services
{
    // ─── Wire DTOs ──────────────────────────────────────────────────
    // Mirror the backend's JSON shape exactly (snake_case; decimals come
    // across as strings, per Pydantic's default Decimal JSON encoding).
    // Kept separate from ProductData (Aayam.Models) so the wire format can
    // change without touching the app's internal model.

    [Serializable]
    internal class ProductListItemDto
    {
        [JsonProperty("sku")] public string Sku;
        [JsonProperty("name")] public string Name;
        [JsonProperty("brand_name")] public string BrandName;
        [JsonProperty("category_name")] public string CategoryName;
        [JsonProperty("price")] public string Price;
        [JsonProperty("currency")] public string Currency;
        [JsonProperty("thumbnail_url")] public string ThumbnailUrl;
        [JsonProperty("has_3d_model")] public bool Has3DModel;
    }

    [Serializable]
    internal class ProductListResponseDto
    {
        [JsonProperty("items")] public List<ProductListItemDto> Items;
    }

    [Serializable]
    internal class ProductDimensionsDto
    {
        [JsonProperty("width_mm")] public string WidthMm;
        [JsonProperty("height_mm")] public string HeightMm;
        [JsonProperty("depth_mm")] public string DepthMm;
        [JsonProperty("weight_grams")] public string WeightGrams;
    }

    [Serializable]
    internal class ProductAsset3DDto
    {
        [JsonProperty("model_url")] public string ModelUrl;
        [JsonProperty("thumbnail_url")] public string ThumbnailUrl;
        [JsonProperty("anchor_x_mm")] public string AnchorXMm;
        [JsonProperty("anchor_y_mm")] public string AnchorYMm;
        [JsonProperty("anchor_z_mm")] public string AnchorZMm;
    }

    [Serializable]
    internal class DoorCompatibilityDto
    {
        [JsonProperty("door_materials")] public List<string> DoorMaterials;
        [JsonProperty("door_types")] public List<string> DoorTypes;
        [JsonProperty("opening_directions")] public List<string> OpeningDirections;
        [JsonProperty("min_door_thickness_mm")] public string MinDoorThicknessMm;
        [JsonProperty("max_door_thickness_mm")] public string MaxDoorThicknessMm;
        [JsonProperty("bore_diameter_mm")] public string BoreDiameterMm;
        [JsonProperty("backset_mm")] public string BacksetMm;
    }

    [Serializable]
    internal class ProductDetailDto
    {
        [JsonProperty("sku")] public string Sku;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("brand_name")] public string BrandName;
        [JsonProperty("price")] public string Price;
        [JsonProperty("currency")] public string Currency;
        [JsonProperty("dimensions")] public ProductDimensionsDto Dimensions;
        [JsonProperty("asset_3d")] public ProductAsset3DDto Asset3D;
        [JsonProperty("compatibility")] public DoorCompatibilityDto Compatibility;
    }

    // ─── Result type for list queries ──────────────────────────────
    // Deliberately not ProductUIItem (Aayam.UI) - Services must not depend on
    // UI, or Aayam.UI's own reference to Aayam.Services becomes circular.
    // ProductSelector (UI) maps this into its own view model.

    public class ProductSummary
    {
        public string Sku;
        public string Name;
        public string BrandName;
        public string CategoryName;
        public float Price;
        public string Currency;
        public string ThumbnailUrl;
        public bool Has3DModel;
    }

    /// <summary>
    /// Fetches product data from the Aayam backend (AppConfig.ApiBaseUrl /
    /// CdnBaseUrl). Converts wire DTOs into app-internal models.
    /// Not a MonoBehaviour - construct directly where needed, e.g.:
    ///   new ProductApiService(config.ApiBaseUrl, config.CdnBaseUrl)
    /// </summary>
    public class ProductApiService
    {
        private readonly string _apiBaseUrl;
        private readonly string _cdnBaseUrl;

        public ProductApiService(string apiBaseUrl, string cdnBaseUrl)
        {
            _apiBaseUrl = (apiBaseUrl ?? "").TrimEnd('/');
            _cdnBaseUrl = (cdnBaseUrl ?? "").TrimEnd('/');
        }

        /// <summary>Resolve a possibly-relative asset path (e.g. "/static/models/x.glb")
        /// returned by the backend into a fully-qualified URL. Absolute URLs
        /// (e.g. a future real CDN) pass through unchanged.</summary>
        public string ResolveAssetUrl(string relativeOrAbsoluteUrl)
        {
            if (string.IsNullOrEmpty(relativeOrAbsoluteUrl)) return relativeOrAbsoluteUrl;
            if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return relativeOrAbsoluteUrl;
            return _cdnBaseUrl + relativeOrAbsoluteUrl;
        }

        public async Task<List<ProductSummary>> FetchProductListAsync(int page = 1, int pageSize = 50)
        {
            var result = new List<ProductSummary>();
            string url = $"{_apiBaseUrl}/api/v1/products/?page={page}&page_size={pageSize}";
            string json = await GetStringAsync(url);
            if (json == null) return result;

            ProductListResponseDto parsed;
            try { parsed = JsonConvert.DeserializeObject<ProductListResponseDto>(json); }
            catch (Exception ex)
            {
                Debug.LogError($"[ProductApiService] Failed to parse product list: {ex.Message}");
                return result;
            }
            if (parsed?.Items == null) return result;

            foreach (var item in parsed.Items)
            {
                result.Add(new ProductSummary
                {
                    Sku = item.Sku,
                    Name = item.Name,
                    BrandName = item.BrandName,
                    CategoryName = item.CategoryName,
                    Price = ParseFloat(item.Price),
                    Currency = item.Currency,
                    ThumbnailUrl = ResolveAssetUrl(item.ThumbnailUrl),
                    Has3DModel = item.Has3DModel,
                });
            }
            return result;
        }

        public async Task<ProductData> FetchProductDetailAsync(string sku)
        {
            string url = $"{_apiBaseUrl}/api/v1/products/sku/{UnityWebRequest.EscapeURL(sku)}";
            string json = await GetStringAsync(url);
            if (json == null) return null;

            ProductDetailDto dto;
            try { dto = JsonConvert.DeserializeObject<ProductDetailDto>(json); }
            catch (Exception ex)
            {
                Debug.LogError($"[ProductApiService] Failed to parse product detail for '{sku}': {ex.Message}");
                return null;
            }
            return dto == null ? null : MapToProductData(dto);
        }

        // ─── Mapping ────────────────────────────────────────────────

        private ProductData MapToProductData(ProductDetailDto dto)
        {
            var data = new ProductData
            {
                ProductId = dto.Sku,
                SKU = dto.Sku,
                Name = dto.Name,
                Brand = dto.BrandName,
                Description = dto.Description,
                Price = ParseFloat(dto.Price),
                Currency = dto.Currency,
            };

            if (dto.Dimensions != null)
            {
                data.Dimensions = new ProductDimensions
                {
                    WidthMm = ParseFloat(dto.Dimensions.WidthMm),
                    HeightMm = ParseFloat(dto.Dimensions.HeightMm),
                    DepthMm = ParseFloat(dto.Dimensions.DepthMm),
                    WeightGrams = ParseFloat(dto.Dimensions.WeightGrams),
                };
            }

            if (dto.Compatibility != null)
            {
                data.Compatibility = new DoorCompatibility
                {
                    MinThicknessMm = ParseFloat(dto.Compatibility.MinDoorThicknessMm),
                    MaxThicknessMm = ParseFloat(dto.Compatibility.MaxDoorThicknessMm),
                    SupportedMaterials = dto.Compatibility.DoorMaterials?.ToArray() ?? Array.Empty<string>(),
                    SupportedDoorTypes = dto.Compatibility.DoorTypes?.ToArray() ?? Array.Empty<string>(),
                    OpeningDirections = dto.Compatibility.OpeningDirections?.ToArray() ?? Array.Empty<string>(),
                    MountingType = null,
                    BoreDiameterMm = ParseFloat(dto.Compatibility.BoreDiameterMm),
                    BacksetMm = ParseFloat(dto.Compatibility.BacksetMm),
                };
            }

            if (dto.Asset3D != null)
            {
                data.Asset = new ProductAsset
                {
                    ModelUrl = ResolveAssetUrl(dto.Asset3D.ModelUrl),
                    ThumbnailUrl = ResolveAssetUrl(dto.Asset3D.ThumbnailUrl),
                    Anchor = new AnchorPoint
                    {
                        XMm = ParseFloat(dto.Asset3D.AnchorXMm),
                        YMm = ParseFloat(dto.Asset3D.AnchorYMm),
                        ZMm = ParseFloat(dto.Asset3D.AnchorZMm),
                    },
                };
            }

            return data;
        }

        private static float ParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        private async Task<string> GetStringAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ProductApiService] GET {url} failed: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
