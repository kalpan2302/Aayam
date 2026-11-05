using UnityEngine;

namespace Aayam.UI
{
    /// <summary>
    /// Procedurally-drawn icon set for the Fitora redesign (2026-11-04) -
    /// this project has no icon asset pipeline or network-download tooling
    /// available mid-session, so icons are composited from basic SDF shapes
    /// (circle/box/line unions) at generation time, same spirit as
    /// FitoraTheme's rounded-rect sprites. Deliberately simple/solid-fill
    /// silhouettes (not fine linework) - legible at small tab-bar size,
    /// achievable without a real vector/raster icon source.
    /// </summary>
    public static class FitoraIcons
    {
        public enum Icon { Home, Explore, Scan, Quotes, Profile, Search, Back, Star, StarOutline, Heart, HeartFilled, Bell, Checkmark, Close, ArCube, Camera, Tools }

        private const int Size = 64;

        public static Sprite Get(Icon icon, Color color)
        {
            var canvas = new float[Size, Size]; // coverage 0..1, union of all strokes
            switch (icon)
            {
                case Icon.Home: DrawHome(canvas); break;
                case Icon.Explore: DrawExplore(canvas); break;
                case Icon.Scan: DrawScanBrackets(canvas); break;
                case Icon.Quotes: DrawQuotes(canvas); break;
                case Icon.Profile: DrawProfile(canvas); break;
                case Icon.Search: DrawSearch(canvas); break;
                case Icon.Back: DrawChevron(canvas); break;
                case Icon.Star: DrawStar(canvas, filled: true); break;
                case Icon.StarOutline: DrawStar(canvas, filled: false); break;
                case Icon.Heart: DrawHeart(canvas, filled: false); break;
                case Icon.HeartFilled: DrawHeart(canvas, filled: true); break;
                case Icon.Bell: DrawBell(canvas); break;
                case Icon.Checkmark: DrawCheckmark(canvas); break;
                case Icon.Close: DrawClose(canvas); break;
                case Icon.ArCube: DrawArCube(canvas); break;
                case Icon.Camera: DrawCameraOutline(canvas); break;
                case Icon.Tools: DrawTools(canvas); break;
            }
            return ToSprite(canvas, color);
        }

        // ─── Primitive compositing ────────────────────────────────

        private static void UnionCircle(float[,] c, float cx, float cy, float r, float stroke = 0f)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = stroke > 0f
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - r) / (stroke * 0.5f) + 0.5f)
                    : Mathf.Clamp01(0.5f - (d - r));
                c[x, y] = Mathf.Max(c[x, y], a);
            }
        }

        private static void UnionBox(float[,] c, float cx, float cy, float hw, float hh, float corner = 0f)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - cx) - (hw - corner);
                float py = Mathf.Abs(y + 0.5f - cy) - (hh - corner);
                float d = Mathf.Sqrt(Mathf.Pow(Mathf.Max(px, 0f), 2) + Mathf.Pow(Mathf.Max(py, 0f), 2))
                          + Mathf.Min(Mathf.Max(px, py), 0f) - corner;
                c[x, y] = Mathf.Max(c[x, y], Mathf.Clamp01(0.5f - d));
            }
        }

        private static void UnionLine(float[,] c, float x0, float y0, float x1, float y1, float width)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = dx * dx + dy * dy;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float px = x + 0.5f - x0, py = y + 0.5f - y0;
                float t = len2 > 0f ? Mathf.Clamp01((px * dx + py * dy) / len2) : 0f;
                float cxp = x0 + t * dx, cyp = y0 + t * dy;
                float d = Mathf.Sqrt((x + 0.5f - cxp) * (x + 0.5f - cxp) + (y + 0.5f - cyp) * (y + 0.5f - cyp));
                c[x, y] = Mathf.Max(c[x, y], Mathf.Clamp01(0.5f - (d - width * 0.5f)));
            }
        }

        private static void UnionTriangle(float[,] c, Vector2 a, Vector2 b, Vector2 p)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = SameSide(pt, a, b, p) && SameSide(pt, b, p, a) && SameSide(pt, p, a, b);
                if (inside) c[x, y] = 1f;
            }
        }

        private static bool SameSide(Vector2 p, Vector2 l1, Vector2 l2, Vector2 refPt)
        {
            float cp1 = Cross(l2 - l1, p - l1);
            float cp2 = Cross(l2 - l1, refPt - l1);
            return cp1 * cp2 >= 0f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static void Subtract(float[,] c, float[,] hole)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                c[x, y] = Mathf.Clamp01(c[x, y] - hole[x, y]);
        }

        // ─── Icon definitions (64x64 canvas, center ~32,32) ────────

        private static void DrawHome(float[,] c)
        {
            UnionTriangle(c, new Vector2(14, 34), new Vector2(50, 34), new Vector2(32, 10));
            UnionBox(c, 32, 44, 15, 14, corner: 2f);
        }

        private static void DrawExplore(float[,] c)
        {
            UnionBox(c, 20, 20, 8, 8, corner: 3f);
            UnionBox(c, 44, 20, 8, 8, corner: 3f);
            UnionBox(c, 20, 44, 8, 8, corner: 3f);
            UnionBox(c, 44, 44, 8, 8, corner: 3f);
        }

        private static void DrawScanBrackets(float[,] c)
        {
            float s = 14, inset = 12, len = 14, w = 4;
            // four L-shaped corner brackets (viewfinder look)
            UnionLine(c, inset, inset, inset + len, inset, w);
            UnionLine(c, inset, inset, inset, inset + len, w);
            UnionLine(c, Size - inset, inset, Size - inset - len, inset, w);
            UnionLine(c, Size - inset, inset, Size - inset, inset + len, w);
            UnionLine(c, inset, Size - inset, inset + len, Size - inset, w);
            UnionLine(c, inset, Size - inset, inset, Size - inset - len, w);
            UnionLine(c, Size - inset, Size - inset, Size - inset - len, Size - inset, w);
            UnionLine(c, Size - inset, Size - inset, Size - inset, Size - inset - len, w);
            _ = s;
        }

        private static void DrawQuotes(float[,] c)
        {
            UnionBox(c, 32, 32, 16, 20, corner: 4f);
            var hole = new float[Size, Size];
            UnionBox(hole, 32, 32, 13.5f, 17.5f, corner: 3f);
            Subtract(c, hole);
            UnionLine(c, 22, 24, 42, 24, 3f);
            UnionLine(c, 22, 32, 42, 32, 3f);
            UnionLine(c, 22, 40, 36, 40, 3f);
        }

        private static void DrawProfile(float[,] c)
        {
            UnionCircle(c, 32, 22, 11);
            UnionBox(c, 32, 50, 18, 13, corner: 10f);
        }

        private static void DrawSearch(float[,] c)
        {
            UnionCircle(c, 27, 27, 13, stroke: 4.5f);
            UnionLine(c, 37, 37, 50, 50, 5f);
        }

        private static void DrawChevron(float[,] c)
        {
            UnionLine(c, 38, 16, 22, 32, 5f);
            UnionLine(c, 22, 32, 38, 48, 5f);
        }

        private static void DrawStar(float[,] c, bool filled)
        {
            Vector2 center = new Vector2(32, 32);
            const int points = 5;
            float outerR = 20, innerR = 8;
            Vector2[] verts = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outerR : innerR;
                verts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            for (int i = 1; i < verts.Length - 1; i++)
                UnionTriangle(c, verts[0], verts[i], verts[i + 1]);

            if (!filled)
            {
                var hole = new float[Size, Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float px = x + 0.5f - center.x, py = y + 0.5f - center.y;
                    if (Mathf.Sqrt(px * px + py * py) < innerR - 3f) hole[x, y] = 1f;
                }
                Subtract(c, hole);
            }
        }

        private static void DrawHeart(float[,] c, bool filled)
        {
            UnionCircle(c, 23, 24, 10);
            UnionCircle(c, 41, 24, 10);
            UnionTriangle(c, new Vector2(13, 28), new Vector2(51, 28), new Vector2(32, 52));

            if (!filled)
            {
                var hole = new float[Size, Size];
                UnionCircle(hole, 23, 24, 6.5f);
                UnionCircle(hole, 41, 24, 6.5f);
                UnionTriangle(hole, new Vector2(18, 30), new Vector2(46, 30), new Vector2(32, 46));
                Subtract(c, hole);
            }
        }

        private static void DrawBell(float[,] c)
        {
            UnionBox(c, 32, 28, 12, 16, corner: 12f);
            var hole = new float[Size, Size];
            UnionBox(hole, 32, 26, 8.5f, 12f, corner: 8f);
            Subtract(c, hole);
            UnionBox(c, 32, 44, 15, 3, corner: 1.5f);
            UnionCircle(c, 32, 50, 4);
        }

        private static void DrawCheckmark(float[,] c)
        {
            UnionLine(c, 16, 32, 27, 43, 5f);
            UnionLine(c, 27, 43, 48, 20, 5f);
        }

        private static void DrawClose(float[,] c)
        {
            UnionLine(c, 18, 18, 46, 46, 5f);
            UnionLine(c, 46, 18, 18, 46, 5f);
        }

        private static void DrawArCube(float[,] c)
        {
            // simple isometric cube outline via 3 parallelogram face edges
            UnionLine(c, 32, 10, 54, 22, 3f);
            UnionLine(c, 32, 10, 10, 22, 3f);
            UnionLine(c, 10, 22, 10, 44, 3f);
            UnionLine(c, 54, 22, 54, 44, 3f);
            UnionLine(c, 10, 44, 32, 56, 3f);
            UnionLine(c, 54, 44, 32, 56, 3f);
            UnionLine(c, 32, 10, 32, 32, 3f);
            UnionLine(c, 10, 22, 32, 32, 3f);
            UnionLine(c, 54, 22, 32, 32, 3f);
            UnionLine(c, 32, 32, 32, 56, 3f);
        }

        private static void DrawTools(float[,] c)
        {
            // simple slider/equalizer glyph - three horizontal tracks, each with a knob
            float[] trackY = { 20, 32, 44 };
            float[] knobX = { 24, 42, 30 };
            foreach (float y in trackY) UnionLine(c, 14, y, 50, y, 3f);
            for (int i = 0; i < 3; i++) UnionCircle(c, knobX[i], trackY[i], 6);
        }

        private static void DrawCameraOutline(float[,] c)
        {
            UnionBox(c, 32, 36, 20, 14, corner: 5f);
            var hole = new float[Size, Size];
            UnionBox(hole, 32, 36, 17, 11, corner: 4f);
            Subtract(c, hole);
            UnionBox(c, 26, 20, 6, 4, corner: 1f);
            UnionCircle(c, 32, 36, 8, stroke: 3.5f);
        }

        // ─── Rasterize ──────────────────────────────────────────────

        private static Sprite ToSprite(float[,] coverage, Color color)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Every DrawXxx function above was authored assuming low Y = "top
            // of the icon" - verified empirically (2026-11-04, via a PIL pixel-
            // count check on the exported PNG) that this pipeline's high Y
            // actually renders at the top instead. Flipping once here, for
            // every icon at once, is simpler and less error-prone than
            // reworking each DrawXxx function's hand-picked coordinates.
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var px = color;
                px.a *= coverage[x, Size - 1 - y];
                pixels[y * Size + x] = px;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
