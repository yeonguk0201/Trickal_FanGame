using System;
using System.IO;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class SansamoPureMotionSetup
    {
        public const string AtlasPath = "Assets/Art/Enemies/FairyKingdom/Walking/SansamoPure_Walk.png";
        public const string PrefabPath = "Assets/Prefabs/QuickRangedFairy.prefab";

        [MenuItem("Trickal Fan Game/Artwork/Setup Sansamo Pure Motion")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Sansamo pure walk atlas.");
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture.width != texture.height || texture.width % 2 != 0)
                throw new InvalidOperationException("Expected a square 2x2 atlas.");
            Texture2D pixels = new(2, 2);
            Rect content;
            try
            {
                pixels.LoadImage(File.ReadAllBytes(AtlasPath));
                int cell = pixels.width / 2, minX = cell, minY = cell, maxX = 0, maxY = 0;
                Color32[] colors = pixels.GetPixels32();
                for (int y = 0; y < pixels.height; y++)
                for (int x = 0; x < pixels.width; x++)
                {
                    if (colors[y * pixels.width + x].a < 20) continue;
                    minX = Mathf.Min(minX, x % cell); maxX = Mathf.Max(maxX, x % cell);
                    minY = Mathf.Min(minY, y % cell); maxY = Mathf.Max(maxY, y % cell);
                }
                if (minX <= 0 || minY <= 0 || maxX >= cell - 1 || maxY >= cell - 1)
                    throw new InvalidOperationException("Walk atlas clips a cell boundary.");
                content = new Rect(minX / (float)cell, minY / (float)cell,
                    (maxX - minX + 1f) / cell, (maxY - minY + 1f) / cell);
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(EnemyMovementAnimationSetup.MaterialPath);
            if (material == null || ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Missing/invalid enemy movement material.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SansamoPureMotion motion = root.GetComponent<SansamoPureMotion>();
                if (motion == null) motion = root.AddComponent<SansamoPureMotion>();
                motion.Configure(texture, material, content);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save pure Sansamo motion.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void SetupAndVerifyBatch()
        { Setup(); SansamoPureMotionVerification.Verify(); }
    }
}
