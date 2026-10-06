using System;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class EnemyAttackAnimationSetup
    {
        public const string Folder = Week15Enemy5Setup.ArtFolder + "/Attacking";
        public static readonly string[] Names = { "Bulhyojason", "Sansamo", "LowBloodSugarFairy", "HighBloodSugarFairy" };
        public const string ProjectilePath = Folder + "/GreenOnion_Projectile.png";
        public const float ProjectilePixelsPerUnit = 200f;
        public const string MinionFolder = BossMovementAnimationSetup.Folder + "/MinionAttacks";

        [MenuItem("Trickal Fan Game/Artwork/Setup Enemy Attack Animations")]
        public static void Setup()
        {
            Sprite projectile = Import(ProjectilePath);
            for (int i = 0; i < Names.Length; i++)
            {
                Sprite[] frames = new Sprite[4];
                for (int j = 0; j < frames.Length; j++) frames[j] = Import($"{Folder}/{Names[i]}_Attack_{j}.png");
                string path = EnemyMovementAnimationSetup.PrefabPaths[i];
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<EnemyMovementAnimator>() == null)
                        throw new InvalidOperationException("Apply enemy movement artwork first: " + path);
                    EnemyAttackArtwork artwork = root.GetComponent<EnemyAttackArtwork>();
                    if (artwork == null) artwork = root.AddComponent<EnemyAttackArtwork>();
                    artwork.Configure(root.GetComponent<SpriteRenderer>().sprite, frames, i == 3 ? projectile : null);
                    SerializedObject presentation = new SerializedObject(root.GetComponent<EnemyAttackPresentation>());
                    foreach (string field in new[] { "telegraphScale", "activeScale", "recoveryScale" })
                        presentation.FindProperty(field).vector2Value = Vector2.one;
                    presentation.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save attack artwork: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // TestEnemy is the ancestor of TestBoss; stop normal attack artwork at this boundary.
            GameObject boss = PrefabUtility.LoadPrefabContents(Week15Enemy2Setup.LegacyBossPrefabPath);
            try
            {
                EnemyAttackArtwork inherited = boss.GetComponent<EnemyAttackArtwork>();
                if (inherited != null)
                {
                    UnityEngine.Object.DestroyImmediate(inherited);
                    if (PrefabUtility.SaveAsPrefabAsset(boss, Week15Enemy2Setup.LegacyBossPrefabPath) == null)
                        throw new InvalidOperationException("Could not exclude boss attack artwork.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(boss); }
            SetupMinions();
            AssetDatabase.SaveAssets();
            Debug.Log("Drawn enemy attack poses and high-blood-sugar fairy green-onion projectile configured.");
        }

        [MenuItem("Trickal Fan Game/Artwork/Setup Crayon Minion Attack Animations")]
        public static void SetupMinions()
        {
            for (int i = 0; i < EnemyMovementAnimationSetup.MinionNames.Length; i++)
            {
                string name = EnemyMovementAnimationSetup.MinionNames[i];
                Sprite[] frames = new Sprite[4];
                for (int j = 0; j < frames.Length; j++) frames[j] = Import($"{MinionFolder}/{name}_Attack_{j}.png");
                Sprite projectile = i < 2 ? Import($"{MinionFolder}/{(i == 0 ? "CrayonArrow" : "CrayonMagic")}_Projectile.png") : null;
                string path = EnemyMovementAnimationSetup.MinionPrefabPaths[i];
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<EnemyMovementAnimator>() == null)
                        throw new InvalidOperationException("Apply minion movement artwork first: " + path);
                    EnemyAttackArtwork artwork = root.GetComponent<EnemyAttackArtwork>();
                    if (artwork == null) artwork = root.AddComponent<EnemyAttackArtwork>();
                    artwork.Configure(root.GetComponent<SpriteRenderer>().sprite, frames, projectile);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save minion attack artwork: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing attack artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Sprite import scale affects artwork only; EnemyProjectile keeps its fixed circular collider.
            importer.spritePixelsPerUnit = path == ProjectilePath ? ProjectilePixelsPerUnit : 400f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import " + path);
            return sprite;
        }
    }
}
