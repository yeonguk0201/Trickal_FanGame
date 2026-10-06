using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week13FrontendUiAssets
    {
        public const string PlaceholderFillSpritePath = "UI/Skin/UISprite.psd";
        public const string HeartSpritePath = "Assets/Art/UI/HudHeart.png";

        private const int HeartTextureSize = 64;
        private const int HeartSupersample = 4;

        public static Sprite LoadPlaceholderFillSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(PlaceholderFillSpritePath);
            if (sprite == null)
                throw new InvalidOperationException("Unity built-in placeholder fill Sprite is missing: " +
                    PlaceholderFillSpritePath);
            return sprite;
        }

        // Placeholder white heart for HP-2; tinted per state and replaceable by final art at the same path.
        public static Sprite LoadOrCreateHeartSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HeartSpritePath) == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/UI")) AssetDatabase.CreateFolder("Assets/Art", "UI");
                File.WriteAllBytes(HeartSpritePath, CreateHeartTexturePng());
                AssetDatabase.ImportAsset(HeartSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(HeartSpritePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Heart sprite importer is missing: " + HeartSpritePath);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
            if (sprite == null)
                throw new InvalidOperationException("Heart sprite could not be loaded: " + HeartSpritePath);
            return sprite;
        }

        private static byte[] CreateHeartTexturePng()
        {
            Texture2D texture = new(HeartTextureSize, HeartTextureSize, TextureFormat.RGBA32, false);
            try
            {
                float step = 1f / HeartSupersample;
                for (int y = 0; y < HeartTextureSize; y++)
                for (int x = 0; x < HeartTextureSize; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < HeartSupersample; sy++)
                    for (int sx = 0; sx < HeartSupersample; sx++)
                    {
                        float u = (x + (sx + 0.5f) * step) / HeartTextureSize * 2.7f - 1.35f;
                        float v = (y + (sy + 0.5f) * step) / HeartTextureSize * 2.7f - 1.25f;
                        float shape = Mathf.Pow(u * u + v * v - 1f, 3f) - u * u * v * v * v;
                        if (shape <= 0f) inside++;
                    }

                    float alpha = inside / (float)(HeartSupersample * HeartSupersample);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }

                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
