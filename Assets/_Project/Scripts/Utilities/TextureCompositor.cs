using UnityEngine;

namespace Aayam.Utilities
{
    /// <summary>
    /// Phase 6 Step 8: composites a watermark (or any overlay) onto a
    /// screenshot. Pure pixel math, no MonoBehaviour dependency - the
    /// watermark fields on ScreenshotCapture were previously declared but
    /// never actually used anywhere.
    /// </summary>
    public static class TextureCompositor
    {
        /// <summary>
        /// Returns a NEW Texture2D with overlay alpha-blended onto a copy
        /// of baseTex, anchored to the bottom-right corner with
        /// bottomRightMargin pixels of padding on each axis. baseTex is
        /// left untouched. A fully-transparent overlay pixel leaves the
        /// base pixel untouched; partial alpha blends.
        /// </summary>
        public static Texture2D CompositeAt(Texture2D baseTex, Texture2D overlay, Vector2Int bottomRightMargin)
        {
            if (baseTex == null) return null;
            if (overlay == null) return baseTex;

            var result = new Texture2D(baseTex.width, baseTex.height, baseTex.format, false);
            result.SetPixels(baseTex.GetPixels());

            int originX = baseTex.width - overlay.width - bottomRightMargin.x;
            int originY = bottomRightMargin.y;

            for (int x = 0; x < overlay.width; x++)
            {
                int destX = originX + x;
                if (destX < 0 || destX >= baseTex.width) continue;

                for (int y = 0; y < overlay.height; y++)
                {
                    int destY = originY + y;
                    if (destY < 0 || destY >= baseTex.height) continue;

                    Color overlayPixel = overlay.GetPixel(x, y);
                    if (overlayPixel.a <= 0f) continue;

                    Color basePixel = result.GetPixel(destX, destY);
                    result.SetPixel(destX, destY, Color.Lerp(basePixel, overlayPixel, overlayPixel.a));
                }
            }

            result.Apply();
            return result;
        }
    }
}
