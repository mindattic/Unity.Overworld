using UnityEditor;
using UnityEngine;

namespace MindAttic.Overworld.Editor
{
    /// <summary>
    /// Imports the original's pictures (Assets/Unity.Overworld/Art) the way Textures.loadTexture loaded them: one mip
    /// level, linear filtering, clamped, uncompressed, and the magenta colour key (0xFFFF00FF) made transparent black,
    /// exactly what Direct3D's colour key did, so the sprites need no keying shader.
    /// </summary>
    public sealed class OriginalArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/Unity.Overworld/Art/";

        static bool IsOriginalArt(string path) => path.Replace('\\', '/').StartsWith(ArtFolder, System.StringComparison.Ordinal);

        /// <summary>Bump when the import changes, so every original picture is imported again.</summary>
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!IsOriginalArt(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;

            // The originals are 24-bit; keep an alpha channel so the colour key survives import on every platform.
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.format = TextureImporterFormat.RGBA32;
                settings.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsOriginalArt(assetPath)) return;
            var pixels = texture.GetPixels32();
            if (ColourKey.Apply(pixels) == 0) return;
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }

    /// <summary>The original's colour key: opaque magenta becomes transparent black.</summary>
    public static class ColourKey
    {
        public static readonly Color32 Key = new Color32(255, 0, 255, 255);

        /// <summary>Keys out every magenta pixel in place; returns how many there were.</summary>
        public static int Apply(Color32[] pixels)
        {
            var keyed = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.r == 255 && p.g == 0 && p.b == 255 && p.a == 255)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    keyed++;
                }
            }

            return keyed;
        }
    }
}
