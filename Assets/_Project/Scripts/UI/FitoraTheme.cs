using UnityEngine;
using UnityEngine.UI;

namespace Aayam.UI
{
    /// <summary>
    /// Fitora visual design system (2026-11-04 rebrand + redesign pass,
    /// replacing Aayam's plain gray AayamSceneBootstrapper-generated UI with
    /// something matching the Fitora MVP reference mockups).
    ///
    /// Centralizes the color palette and a procedural rounded-rect sprite
    /// generator so every screen's cards/buttons share one consistent look
    /// without hand-authored sprite assets - this project has no design
    /// tool export pipeline, so "generate it in code" matches the existing
    /// convention (backend/scripts/generate_placeholder_models.py does the
    /// same thing for 3D assets).
    /// </summary>
    public static class FitoraTheme
    {
        // ─── Palette (sampled from the Fitora MVP reference image) ────
        public static readonly Color Primary       = new Color32(0xB8, 0x74, 0x4A, 0xFF); // warm terracotta (logo, primary buttons)
        public static readonly Color PrimaryDark    = new Color32(0x8C, 0x55, 0x34, 0xFF);
        public static readonly Color PrimaryLight    = new Color32(0xE8, 0xC9, 0xAE, 0xFF); // light tint, selected pills
        public static readonly Color Background     = new Color32(0xFA, 0xF5, 0xEE, 0xFF); // warm cream app background
        public static readonly Color Surface        = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // card white
        public static readonly Color SurfaceAlt      = new Color32(0xF3, 0xEC, 0xE2, 0xFF); // chip/category background
        public static readonly Color TextPrimary     = new Color32(0x2B, 0x22, 0x1C, 0xFF); // near-black warm text
        public static readonly Color TextSecondary   = new Color32(0x8F, 0x82, 0x76, 0xFF); // muted gray-brown
        public static readonly Color TextOnPrimary   = Color.white;
        public static readonly Color Success         = new Color32(0x3E, 0xA0, 0x5C, 0xFF);
        public static readonly Color Border          = new Color32(0xEA, 0xE1, 0xD6, 0xFF);
        public static readonly Color ShadowColor     = new Color(0.25f, 0.18f, 0.10f, 0.12f);

        public const float CardCorner   = 20f;
        public const float ButtonCorner = 26f;
        public const float ChipCorner   = 18f;
        public const float PageMargin   = 36f;

        private static Sprite _cachedPlainRounded;
        private static Sprite _cachedShadow;

        /// <summary>A 9-sliced rounded-rect sprite shared by cards/buttons/chips -
        /// a single flat-white texture, tinted per-use via Image.color so one
        /// generated asset serves every surface color in the palette.</summary>
        public static Sprite RoundedRect(float cornerRadius = CardCorner)
        {
            if (_cachedPlainRounded != null) return _cachedPlainRounded;
            var tex = GenerateRoundedRectTexture(64, 64, cornerRadius, Color.white);
            _cachedPlainRounded = ToSprite(tex, cornerRadius);
            return _cachedPlainRounded;
        }

        /// <summary>A soft drop-shadow sprite (wide anti-alias falloff fakes blur) -
        /// place behind a card, offset down a few px, tinted/alpha'd via Image.color.</summary>
        public static Sprite ShadowSprite(float cornerRadius = CardCorner)
        {
            if (_cachedShadow != null) return _cachedShadow;
            var tex = GenerateRoundedRectTexture(64, 64, cornerRadius, Color.white, blurPx: 10f);
            _cachedShadow = ToSprite(tex, cornerRadius + 10f);
            return _cachedShadow;
        }

        private static Sprite ToSprite(Texture2D tex, float border)
        {
            return Sprite.Create(
                tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Signed-distance-field rounded rect, optionally blurred (wide
        /// soft falloff) for shadow use. White RGB, alpha carries the shape -
        /// tint via Image.color at each call site.</summary>
        public static Texture2D GenerateRoundedRectTexture(int width, int height, float cornerRadius, Color color, float blurPx = 0f)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float hx = width / 2f;
            float hy = height / 2f;
            float r = Mathf.Min(cornerRadius, Mathf.Min(hx, hy));
            float falloff = Mathf.Max(0.75f, blurPx);

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f - hx;
                    float py = y + 0.5f - hy;
                    float qx = Mathf.Abs(px) - (hx - r);
                    float qy = Mathf.Abs(py) - (hy - r);
                    float dist = Mathf.Sqrt(Mathf.Pow(Mathf.Max(qx, 0f), 2) + Mathf.Pow(Mathf.Max(qy, 0f), 2))
                                 + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float alpha = Mathf.Clamp01(0.5f - dist / falloff);
                    var c = color;
                    c.a *= alpha;
                    pixels[y * width + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ─── Apply helpers ──────────────────────────────────────────

        /// <summary>Styles an Image as a flat card/button surface: rounded sprite, sliced, tinted.</summary>
        public static void ApplyCardStyle(Image image, Color fillColor, float cornerRadius = CardCorner)
        {
            image.sprite = RoundedRect(cornerRadius);
            image.type = Image.Type.Sliced;
            image.color = fillColor;
        }

        /// <summary>Styles a Button's background + label text together - rounded
        /// surface, bold label, optional shadow.</summary>
        public static void ApplyButtonStyle(Button button, Color background, Color textColor,
            float cornerRadius = ButtonCorner, bool withShadow = true)
        {
            var image = button.GetComponent<Image>();
            if (image != null) ApplyCardStyle(image, background, cornerRadius);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = textColor;
                text.fontStyle = FontStyle.Bold;
            }
            if (withShadow)
            {
                var rect = button.GetComponent<RectTransform>();
                if (rect != null) AddShadow(rect, cornerRadius);
            }
        }

        /// <summary>Adds a soft shadow Image as the first sibling behind target,
        /// matching target's rect with a small downward offset.</summary>
        public static void AddShadow(RectTransform target, float cornerRadius = CardCorner, float offsetY = -3f, float extraPadding = 6f)
        {
            var shadowGO = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            var shadowRect = shadowGO.GetComponent<RectTransform>();
            shadowGO.transform.SetParent(target.parent, worldPositionStays: false);
            shadowGO.transform.SetSiblingIndex(target.GetSiblingIndex());

            shadowRect.anchorMin = target.anchorMin;
            shadowRect.anchorMax = target.anchorMax;
            shadowRect.pivot = target.pivot;
            shadowRect.sizeDelta = target.sizeDelta + new Vector2(extraPadding * 2, extraPadding * 2);
            shadowRect.anchoredPosition = target.anchoredPosition + new Vector2(0, offsetY);

            var image = shadowGO.GetComponent<Image>();
            image.sprite = ShadowSprite(cornerRadius);
            image.type = Image.Type.Sliced;
            image.color = ShadowColor;
            image.raycastTarget = false;
        }
    }
}
