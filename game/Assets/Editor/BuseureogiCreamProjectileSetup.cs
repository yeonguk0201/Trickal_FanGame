using System;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // 부스러기 approach-volley shot: three looping pink cream frames replace the red-tinted boss sprite.
    public static class BuseureogiCreamProjectileSetup
    {
        public static readonly string[] FramePaths =
        {
            Week15Boss1Setup.BossArtFolder + "/Boss_Buseureogi_CreamProjectile_v1_Frame1.png",
            Week15Boss1Setup.BossArtFolder + "/Boss_Buseureogi_CreamProjectile_v1_Frame2.png",
            Week15Boss1Setup.BossArtFolder + "/Boss_Buseureogi_CreamProjectile_v1_Frame3.png",
        };

        // Center of the round cream head on the 1983x793 canvas. The tail trails to the left of it.
        public static readonly Vector2 HeadPivot = new(0.8f, 0.5f);
        public const float PixelsPerUnit = 1000f;

        [MenuItem("Trickal Fan Game/Effects/Setup Buseureogi Cream Projectile")]
        public static void Setup()
        {
            Sprite[] frames = ConfigureAndLoadFrames();
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss0Setup.BossPrefabPath);
            try
            {
                BuseureogiBossPatternRuntime runtime = contents.GetComponent<BuseureogiBossPatternRuntime>();
                if (runtime == null)
                    throw new InvalidOperationException("Run Week 15 Boss-1 setup first: the boss prefab has no Buseureogi runtime.");
                runtime.ConfigureProjectileArtwork(frames);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss0Setup.BossPrefabPath) == null)
                    throw new InvalidOperationException("Could not save the Buseureogi boss prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Buseureogi cream projectile frames assigned to the boss prefab.");
        }

        public static Sprite[] ConfigureAndLoadFrames()
        {
            var frames = new Sprite[FramePaths.Length];
            for (int i = 0; i < FramePaths.Length; i++)
            {
                ConfigureImport(FramePaths[i]);
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(FramePaths[i]);
                if (frames[i] == null) throw new InvalidOperationException($"Missing cream projectile frame: {FramePaths[i]}");
            }

            return frames;
        }

        private static void ConfigureImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException($"Missing texture importer: {path}");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
                           settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                           settings.spritePivot != HeadPivot;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = HeadPivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
