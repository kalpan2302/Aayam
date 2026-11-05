using System.IO;
using UnityEditor;
using UnityEngine;
using Aayam.UI;

namespace Aayam.Editor
{
    /// <summary>
    /// Headless preview tool for the Fitora redesign's procedural icons/sprites
    /// (FitoraTheme, FitoraIcons) - saves each as a PNG so they can be visually
    /// checked without a full device deploy. Temporary design-validation
    /// tooling, not part of the shipped app.
    /// </summary>
    public static class FitoraPreviewExporter
    {
        public static void Run()
        {
            string outDir = System.Environment.GetEnvironmentVariable("FITORA_PREVIEW_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = "/tmp/fitora_preview";
            Directory.CreateDirectory(outDir);

            SaveTex(FitoraTheme.GenerateRoundedRectTexture(128, 128, 36, FitoraTheme.Primary), Path.Combine(outDir, "card_primary.png"));
            SaveTex(FitoraTheme.GenerateRoundedRectTexture(128, 128, 36, FitoraTheme.Surface), Path.Combine(outDir, "card_surface.png"));
            SaveTex(FitoraTheme.GenerateRoundedRectTexture(128, 128, 36, Color.white, blurPx: 16f), Path.Combine(outDir, "shadow.png"));

            foreach (FitoraIcons.Icon icon in System.Enum.GetValues(typeof(FitoraIcons.Icon)))
            {
                var sprite = FitoraIcons.Get(icon, FitoraTheme.TextPrimary);
                SaveTex((Texture2D)sprite.texture, Path.Combine(outDir, $"icon_{icon}.png"));
            }

            // Composite sheet: all icons on a cream background, labeled by filename only (no text rendering needed)
            Debug.Log($"[FitoraPreviewExporter] Saved previews to {outDir}");
            EditorApplication.Exit(0);
        }

        private static void SaveTex(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
    }
}
