using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Boss-Pattern-1: applies the dash slash values to the existing prefab and imports its two textures. Other patterns
    // and prefab values are left alone, so running it again changes nothing.
    public static class BossPattern1Setup
    {
        public const string DashPatternId = "crayon-short-dash-chain";
        public const float SwingRecognition = 1.15f;
        public const float PatternWarning = 0.35f;
        public const float PatternRecovery = 0.8f;
        public static readonly string[] TexturePaths =
        {
            "Assets/Resources/CombatEffects/" + CrayonDashRiftEffect.WarningArtwork + ".png",
            "Assets/Resources/CombatEffects/" + CrayonDashRiftEffect.StrikeArtwork + ".png",
        };

        [MenuItem("Trickal Fan Game/Beta/Setup Boss-Pattern-1 Crayon Dash Rift")]
        public static void Setup()
        {
            foreach (string path in TexturePaths) ConfigureTexture(path);
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss3Setup.BossPrefabPath);
            try
            {
                CrayonHeroBossPatternRuntime runtime = contents.GetComponent<CrayonHeroBossPatternRuntime>();
                BossController boss = contents.GetComponent<BossController>();
                if (runtime == null || boss == null)
                    throw new InvalidOperationException("Boss-Pattern-1: the crayon hero prefab lost its boss components.");
                runtime.ConfigureSwingRecognition(SwingRecognition);
                runtime.ConfigureDash(0.05f, 0.7f, 1.2f, EnemyDamageTier.Light);
                runtime.ConfigureDashRift(PatternWarning, 0.3f, 0.1f, PatternRecovery, 4.2f, 0.9f, 1.2f);
                ConfigureDashPattern(boss);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss3Setup.BossPrefabPath) == null)
                    throw new InvalidOperationException("Boss-Pattern-1: could not save the crayon hero prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureDashPattern(BossController boss)
        {
            var serialized = new SerializedObject(boss);
            SerializedProperty patterns = serialized.FindProperty("patterns");
            for (int index = 0; index < patterns.arraySize; index++)
            {
                SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
                if (pattern.FindPropertyRelative("patternId").stringValue != DashPatternId) continue;
                pattern.FindPropertyRelative("telegraphDuration").floatValue = PatternWarning;
                pattern.FindPropertyRelative("recoveryDuration").floatValue = PatternRecovery;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            throw new InvalidOperationException($"Boss-Pattern-1: pattern {DashPatternId} is missing.");
        }

        private static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException($"Boss-Pattern-1: missing texture {path}.");
            bool matches = importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled &&
                           importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None &&
                           importer.textureCompression == TextureImporterCompression.Uncompressed &&
                           importer.maxTextureSize >= 4096 && importer.wrapMode == TextureWrapMode.Clamp;
            if (matches) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
