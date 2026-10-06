using System;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class BossMovementAnimationSetup
    {
        public const string Folder = Week15Boss1Setup.BossArtFolder + "/Movement";
        public static readonly string[] PrefabPaths = {
            Week15Boss0Setup.BossPrefabPath, Week15Boss2Setup.BossPrefabPath, Week15Boss3Setup.BossPrefabPath,
        };

        [MenuItem("Trickal Fan Game/Artwork/Setup Boss Movement Animations")]
        public static void Setup()
        {
            Sprite[] frames = new Sprite[4];
            Sprite[][] hopFrames = { new Sprite[4], new Sprite[4] };
            for (int i = 0; i < frames.Length; i++) frames[i] = Import($"{Folder}/CrayonHero_Walk_{i}.png");
            for (int i = 0; i < 4; i++)
            {
                hopFrames[0][i] = Import($"{Folder}/Buseureogi_Hop_{i}.png");
                hopFrames[1][i] = Import($"{Folder}/Vault_Hop_{i}.png");
            }
            Sprite vaultBody = Import(Folder + "/Vault_Body.png");
            Sprite left = Import(Folder + "/Vault_Ground_Left.png");
            Sprite right = Import(Folder + "/Vault_Ground_Right.png");
            Sprite[] healFrames = new Sprite[4];
            for (int i = 0; i < healFrames.Length; i++) healFrames[i] = Import($"{Folder}/Vault_Heal_{i}.png");
            Sprite[] attacks = new Sprite[14];
            string[] attackNames = { "Swing", "Dash", "Slam" };
            for (int group = 0; group < 3; group++)
                for (int pose = 0; pose < 4; pose++)
                    attacks[group * 4 + pose] = Import($"{Folder}/CrayonHero_{attackNames[group]}_{pose}.png");
            attacks[8] = Import($"{Folder}/CrayonHero_SlamCharge_0.png");
            attacks[9] = Import($"{Folder}/CrayonHero_SlamCharge_1.png");
            attacks[12] = Import($"{Folder}/CrayonHero_SlamCharge_2.png");
            attacks[13] = Import($"{Folder}/CrayonHero_SlamCharge_3.png");
            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPaths[i]);
                try
                {
                    SpriteRenderer source = i == 0 ? root.GetComponent<SpriteRenderer>()
                        : root.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
                    if (source == null || source.sprite == null) throw new InvalidOperationException("Missing boss artwork: " + PrefabPaths[i]);
                    BossMovementAnimator animator = root.GetComponent<BossMovementAnimator>();
                    if (animator == null) animator = root.AddComponent<BossMovementAnimator>();
                    SpriteRenderer[] treasures = Array.Empty<SpriteRenderer>();
                    if (i == 1)
                    {
                        treasures = new[] {
                            ConfigureTreasure(root.transform, "Ground Treasure Left", left, source),
                            ConfigureTreasure(root.transform, "Ground Treasure Right", right, source),
                        };
                    }
                    animator.Configure((BossMovementStyle)i, source, i == 1 ? vaultBody : null,
                        i == 2 ? frames : Array.Empty<Sprite>(), treasures,
                        i == 2 ? 1.6f : 1.35f, i == 0 ? 0.48f : 0.42f, i < 2 ? hopFrames[i] : null);
                    if (i == 1) animator.ConfigureHealing(healFrames);
                    if (i == 2)
                    {
                        animator.ConfigureCrayonAttacks(attacks);
                        ConfigureGoldenFrames(animator);
                    }
                    if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPaths[i]) == null)
                        throw new InvalidOperationException("Could not save " + PrefabPaths[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Boss movement configured: Buseureogi hop, separate vault body/ground treasure, Crayon walk with cape and arm poses.");
        }

        private static SpriteRenderer ConfigureTreasure(Transform parent, string name, Sprite sprite, SpriteRenderer source)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.gameObject.layer = source.gameObject.layer;
            child.localPosition = Vector3.zero;
            child.localScale = Vector3.one;
            child.localRotation = Quaternion.identity;
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder - 1;
            renderer.enabled = false;
            return renderer;
        }

        private static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing boss movement asset: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 400f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import " + path);
            return sprite;
        }

        private static void ConfigureGoldenFrames(BossMovementAnimator animator)
        {
            var transition = new Sprite[4];
            for (int i = 0; i < 4; i++) transition[i] = Import($"{Folder}/CrayonHero_Awaken_{i}.png");
            animator.ConfigureAwakening(transition);
            var attacks = new Sprite[14];
            string[] names = { "Swing", "Dash", "Slam" };
            for (int group = 0; group < 3; group++)
                for (int i = 0; i < 4; i++)
                    attacks[group * 4 + i] = Import($"{Folder}/CrayonHero_Golden_{names[group]}_{i}.png");
            attacks[8] = Import($"{Folder}/CrayonHero_Golden_SlamCharge_0.png");
            attacks[9] = Import($"{Folder}/CrayonHero_Golden_SlamCharge_1.png");
            attacks[12] = Import($"{Folder}/CrayonHero_Golden_SlamCharge_2.png");
            attacks[13] = Import($"{Folder}/CrayonHero_Golden_SlamCharge_3.png");
            animator.ConfigureGoldenAttacks(attacks);
            animator.GetComponent<CrayonHeroBossPatternRuntime>().ConfigureAwakenedArtwork(
                Import($"{Folder}/CrayonHero_Awakened.png"));
        }
    }
}
