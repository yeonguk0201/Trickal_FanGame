using System;
using System.Linq;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class EnemyMovementAnimationSetup
    {
        public const string MaterialPath = Week15Enemy5Setup.ArtFolder + "/EnemyMovement.mat";
        public static readonly string[] PrefabPaths = {
            Week15Enemy2Setup.BulhyojasonPrefabPath, Week15Enemy2Setup.SansamoPrefabPath,
            Week15Enemy0Setup.ChargingPrefabPath, Week15Enemy4Setup.SniperPrefabPath,
        };
        public const int WalkFrameCount = 4;
        public const string WalkFolder = Week15Enemy5Setup.ArtFolder + "/Walking";
        public static readonly string[] WalkNames = { "Sansamo", "LowBloodSugarFairy", "HighBloodSugarFairy" };
        public const string MinionWalkFolder = BossMovementAnimationSetup.Folder + "/Minions";
        public static readonly string[] MinionNames = { "CrayonArcher", "CrayonMage", "CrayonAxe", "CrayonShield" };
        public static readonly string[] MinionPrefabPaths = { Week15Boss3Setup.ArcherPrefabPath,
            Week15Boss3Setup.MagePrefabPath, Week15Boss3Setup.AxePrefabPath, Week15Boss3Setup.ShieldPrefabPath };

        [MenuItem("Trickal Fan Game/Artwork/Setup Enemy Movement Animations")]
        public static void Setup()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Week15Enemy5Setup.ArtFolder + "/EnemyMovement.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Enemy movement shader is missing or invalid.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            Sprite[][] walking = WalkNames.Select(name => ImportFrames(name)).ToArray();
            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPaths[i]);
                try
                {
                    if (root.GetComponent<SpriteRenderer>()?.sprite == null || root.GetComponent<Rigidbody2D>() == null)
                        throw new InvalidOperationException(PrefabPaths[i] + " requires artwork and a physics body.");
                    EnemyMovementAnimator animator = root.GetComponent<EnemyMovementAnimator>();
                    if (animator == null) animator = root.AddComponent<EnemyMovementAnimator>();
                    animator.Configure(i == 0 ? EnemyMovementMotion.Hop : EnemyMovementMotion.Walk,
                        material, i == 0 ? Array.Empty<Sprite>() : walking[i - 1],
                        i == 1 ? 1.05f : 1f, i == 0 ? 0.075f : 0f, root.GetComponent<SpriteRenderer>().sprite);
                    if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPaths[i]) == null)
                        throw new InvalidOperationException("Could not save " + PrefabPaths[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // TestBoss inherits TestEnemy. Remove the inherited normal-enemy animator at that boundary
            // so its descendants keep their existing boss-specific presentation.
            GameObject boss = PrefabUtility.LoadPrefabContents(Week15Enemy2Setup.LegacyBossPrefabPath);
            try
            {
                EnemyMovementAnimator inherited = boss.GetComponent<EnemyMovementAnimator>();
                if (inherited != null)
                {
                    UnityEngine.Object.DestroyImmediate(inherited);
                    if (PrefabUtility.SaveAsPrefabAsset(boss, Week15Enemy2Setup.LegacyBossPrefabPath) == null)
                        throw new InvalidOperationException("Could not exclude the legacy boss from normal-enemy animation.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(boss); }
            SetupMinions(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy movement animations configured: Bulhyojason hops; Sansamo and both fairies walk.");
        }

        [MenuItem("Trickal Fan Game/Artwork/Setup Crayon Minion Movement Animations")]
        public static void SetupMinions()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) throw new InvalidOperationException("Apply enemy movement artwork first.");
            SetupMinions(material);
            AssetDatabase.SaveAssets();
        }

        private static void SetupMinions(Material material)
        {
            for (int i = 0; i < MinionPrefabPaths.Length; i++)
            {
                Sprite[] frames = ImportFrames(MinionNames[i], MinionWalkFolder);
                GameObject root = PrefabUtility.LoadPrefabContents(MinionPrefabPaths[i]);
                try
                {
                    SpriteRenderer source = root.GetComponent<SpriteRenderer>();
                    if (source == null || source.sprite == null) throw new InvalidOperationException("Missing minion artwork.");
                    EnemyMovementAnimator animator = root.GetComponent<EnemyMovementAnimator>();
                    if (animator == null) animator = root.AddComponent<EnemyMovementAnimator>();
                    animator.Configure(EnemyMovementMotion.Walk, material, frames, i == 3 ? 0.8f : 1f, 0f, source.sprite);
                    if (PrefabUtility.SaveAsPrefabAsset(root, MinionPrefabPaths[i]) == null)
                        throw new InvalidOperationException("Could not save minion movement: " + MinionPrefabPaths[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static Sprite[] ImportFrames(string character, string folder = WalkFolder)
        {
            Sprite[] frames = new Sprite[WalkFrameCount];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                string path = $"{folder}/{character}_Walk_{frame}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing drawn walk frame: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 400f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = folder == MinionWalkFolder ? 1024 : 512;
                importer.SaveAndReimport();
                frames[frame] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[frame] == null) throw new InvalidOperationException("Could not import " + path);
            }
            return frames;
        }
    }
}
