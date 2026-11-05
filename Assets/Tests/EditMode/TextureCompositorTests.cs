using NUnit.Framework;
using UnityEngine;
using Aayam.Utilities;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class TextureCompositorTests
    {
        private static Texture2D MakeSolid(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        [Test]
        public void CompositeAt_OverlayPixelsLandInBottomRightCorner()
        {
            var baseTex = MakeSolid(4, 4, Color.red);
            var overlay = MakeSolid(2, 2, Color.blue);

            var result = TextureCompositor.CompositeAt(baseTex, overlay, Vector2Int.zero);

            // bottom-right 2x2 corner (x=2..3, y=0..1) should be the overlay color.
            Assert.AreEqual(Color.blue, result.GetPixel(2, 0));
            Assert.AreEqual(Color.blue, result.GetPixel(3, 0));
            Assert.AreEqual(Color.blue, result.GetPixel(2, 1));
            Assert.AreEqual(Color.blue, result.GetPixel(3, 1));

            Object.DestroyImmediate(baseTex);
            Object.DestroyImmediate(overlay);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void CompositeAt_PixelsOutsideOverlayAreUnchanged()
        {
            var baseTex = MakeSolid(4, 4, Color.red);
            var overlay = MakeSolid(2, 2, Color.blue);

            var result = TextureCompositor.CompositeAt(baseTex, overlay, Vector2Int.zero);

            // top-left 2x2 (x=0..1, y=2..3) is outside the overlay region - still red.
            Assert.AreEqual(Color.red, result.GetPixel(0, 2));
            Assert.AreEqual(Color.red, result.GetPixel(1, 3));

            Object.DestroyImmediate(baseTex);
            Object.DestroyImmediate(overlay);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void CompositeAt_MarginShiftsOverlayAwayFromEdge()
        {
            var baseTex = MakeSolid(4, 4, Color.red);
            var overlay = MakeSolid(1, 1, Color.blue);

            var result = TextureCompositor.CompositeAt(baseTex, overlay, new Vector2Int(1, 1));

            // With a 1px margin, a 1x1 overlay on a 4x4 base lands at (2,1), not (3,0).
            Assert.AreEqual(Color.blue, result.GetPixel(2, 1));
            Assert.AreEqual(Color.red, result.GetPixel(3, 0));

            Object.DestroyImmediate(baseTex);
            Object.DestroyImmediate(overlay);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void CompositeAt_FullyTransparentOverlay_LeavesBaseUnchanged()
        {
            var baseTex = MakeSolid(4, 4, Color.red);
            var overlay = MakeSolid(2, 2, new Color(0f, 0f, 1f, 0f));

            var result = TextureCompositor.CompositeAt(baseTex, overlay, Vector2Int.zero);

            Assert.AreEqual(Color.red, result.GetPixel(2, 0));

            Object.DestroyImmediate(baseTex);
            Object.DestroyImmediate(overlay);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void CompositeAt_NullOverlay_ReturnsBaseUnchanged()
        {
            var baseTex = MakeSolid(4, 4, Color.red);

            var result = TextureCompositor.CompositeAt(baseTex, null, Vector2Int.zero);

            Assert.AreSame(baseTex, result);

            Object.DestroyImmediate(baseTex);
        }

        [Test]
        public void CompositeAt_NullBase_ReturnsNull()
        {
            var overlay = MakeSolid(2, 2, Color.blue);

            var result = TextureCompositor.CompositeAt(null, overlay, Vector2Int.zero);

            Assert.IsNull(result);

            Object.DestroyImmediate(overlay);
        }
    }
}
