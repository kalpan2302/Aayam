using System;

namespace Aayam.Models
{
    /// <summary>
    /// Parses "aayam://product/{sku}" deep links - from a scanned QR code
    /// (see backend GET /api/v1/assets/{id}/qr) or a system open-url callback -
    /// into a product SKU.
    ///
    /// Pure/static, no UnityEngine dependency - fully unit-testable, and usable
    /// from both the QR-scan path and any future universal-link handler.
    /// </summary>
    public static class DeepLinkParser
    {
        private const string Scheme = "aayam://";
        private const string ProductPrefix = "product/";

        /// <summary>True and sets productId if url is a well-formed
        /// "aayam://product/{sku}" link (optionally with a query string or
        /// fragment after the sku, which is stripped).</summary>
        public static bool TryParseProductId(string url, out string productId)
        {
            productId = null;
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!url.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return false;

            string path = url.Substring(Scheme.Length);
            if (!path.StartsWith(ProductPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            string id = path.Substring(ProductPrefix.Length).Trim();
            int cut = id.IndexOfAny(new[] { '?', '#', '/' });
            if (cut >= 0) id = id.Substring(0, cut);

            if (string.IsNullOrEmpty(id)) return false;

            productId = id;
            return true;
        }
    }
}
