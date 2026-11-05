using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Defeat-0 (docs/28 §7): the user's four-frame defeat drawings (2026-11-03). A dying enemy leaves a picture
    // with no collider that is knocked away from the last hit and fades out; the enemy is still removed at once.
    // Re-running re-imports the same frames and updates the same Prefabs without adding a second component.
    public static class EnemyDefeatArtworkSetup
    {
        public const string FrameRoot = "Assets/Art/Enemies/FairyKingdom/Death";
        public const string BossFrameRoot = "Assets/Art/Bosses/FairyKingdom/Death";
        public const int FrameCount = 4;
        // Bosses burst or slump where they stood; a short knock still shows which way the last hit went.
        public const float BossKnockScale = 0.3f;
        // 잠복형 falls over in its mound where it stood (2026-11-06 user decision).
        public const float InPlaceKnockScale = 0f;

        public readonly struct Binding
        {
            public readonly string Prefab, Frames, Folder;
            public readonly float KnockScale;
            // The drawing the body shows when it dies, when it is not the Prefab's own (잠복형 is surfaced).
            public readonly string BodyPath;
            public Binding(string prefab, string frames, string folder = FrameRoot, float knockScale = 1f,
                string bodyPath = null)
            { Prefab = prefab; Frames = frames; Folder = folder; KnockScale = knockScale; BodyPath = bodyPath; }
            public string FramePath(int index) => $"{Folder}/{Frames}_Death_{index}.png";
        }

        // Prefab name → frame set. 대왕쥬비 shares the 쥬비 drawings at its own size. Defeat-1 (2026-11-04) adds
        // 부스러기 (TestBoss) and 새마음금고. 불효자손 (TestEnemy, 2026-11-05) is the base of SansamoEnemy and TestBoss,
        // so it comes first and its variants override the inherited defeat instead of adding a second one.
        public static readonly Binding[] Bindings =
        {
            new("TestEnemy", "Bulhyojason"),
            new("JyubiEnemy", "Jyubi"),
            new("JyubiKing", "Jyubi"),
            new("QuickRangedFairy", "SansamoPure"),
            new("SansamoEnemy", "Sansamo"),
            new("HighBloodSugarFairy", "HighBloodSugarFairy"),
            new("ChargingEnemy", "LowBloodSugarFairy"),
            new("LobbingFairy", "LowBloodSugarMadness"),
            new("LivelyLowBloodSugarFairy", "LivelyFairy"),
            new("TestBoss", "Buseureogi", BossFrameRoot, BossKnockScale),
            new("SaemaeumVaultBoss", "Vault", BossFrameRoot, BossKnockScale),
            // 잠복형 (2026-11-06) dies above ground, so its size comes from the full burst drawing.
            new("AmbushFairy", "BulhyojasonCold", FrameRoot, InPlaceKnockScale,
                Defeat1MotionSetup.AmbushPath(AmbushEnemyMotion.SurfacedFrame)),
        };

        [MenuItem("Trickal Fan Game/Artwork/Apply Enemy Defeat Frames")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string path in Bindings.SelectMany(binding => Enumerable.Range(0, FrameCount)
                         .Select(binding.FramePath)).Distinct())
                ImportFrame(path);
            foreach (Binding binding in Bindings) RemoveAddedDefeatUnderBoundBase(binding.Prefab);
            foreach (Binding binding in Bindings)
            {
                Sprite[] sprites = Enumerable.Range(0, FrameCount)
                    .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(binding.FramePath(i))).ToArray();
                if (sprites.Any(sprite => sprite == null))
                    throw new InvalidOperationException("Missing defeat frames: " + binding.Frames);
                string path = "Assets/Prefabs/" + binding.Prefab + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var defeat = root.GetComponent<EnemyDefeatAnimation>();
                    bool added = defeat == null;
                    if (added) defeat = root.AddComponent<EnemyDefeatAnimation>();
                    var serialized = new SerializedObject(defeat);
                    SerializedProperty list = serialized.FindProperty("frames");
                    list.arraySize = sprites.Length;
                    for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                    Sprite bodySprite = binding.BodyPath != null
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(binding.BodyPath) : BodySprite(root);
                    if (bodySprite == null) throw new InvalidOperationException("No body drawing: " + binding.Prefab);
                    serialized.FindProperty("sizeMultiplier").floatValue =
                        VisibleShare(bodySprite) / VisibleShare(sprites[0]);
                    serialized.FindProperty("knockScale").floatValue = binding.KnockScale;
                    // Already set: do not re-save, which would only reserialize unrelated fields.
                    if (serialized.ApplyModifiedPropertiesWithoutUndo() || added)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Defeat setup complete: {Bindings.Length} enemy and boss Prefabs use the supplied four-frame defeat drawings.");
        }

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            EnemyDefeatArtworkVerification.Verify();
        }

        // A variant that added its own defeat before its base was bound would end up with two; it keeps the base's
        // and the loop above overrides its frames, size and knock.
        private static void RemoveAddedDefeatUnderBoundBase(string prefab)
        {
            string path = "Assets/Prefabs/" + prefab + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject source = asset != null ? PrefabUtility.GetCorrespondingObjectFromSource(asset) : null;
            if (source == null || !Bindings.Any(binding => binding.Prefab == source.name)) return;
            EnemyDefeatAnimation own = asset.GetComponent<EnemyDefeatAnimation>();
            if (own == null || !PrefabUtility.IsAddedComponentOverride(own)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<EnemyDefeatAnimation>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Visible body height / visible first-frame height, both as a share of their sprite, so a thrown prop
        // above the head (곡사형's crowbar) no longer shrinks the fallen character.
        public static float SizeMultiplier(GameObject enemy, Sprite firstFrame)
        {
            Sprite body = BodySprite(enemy);
            if (body == null) throw new InvalidOperationException("No body drawing to measure: " + enemy.name);
            return VisibleShare(body) / VisibleShare(firstFrame);
        }

        public static Sprite BodySprite(GameObject enemy)
        {
            var artwork = enemy.GetComponent<FairyKingdomArtworkView>();
            if (artwork != null && artwork.Visual != null && artwork.Visual.sprite != null) return artwork.Visual.sprite;
            SpriteRenderer own = enemy.GetComponent<SpriteRenderer>();
            if (own != null && own.sprite != null) return own.sprite;
            return enemy.GetComponentsInChildren<SpriteRenderer>().Select(r => r.sprite).FirstOrDefault(s => s != null);
        }

        // Share of the sprite's height that has visible pixels, read from the source PNG.
        public static float VisibleShare(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            var source = new Texture2D(2, 2);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Unreadable PNG: " + path);
                float toSource = (float)source.width / sprite.texture.width;
                Rect rect = sprite.rect;
                int x0 = Mathf.FloorToInt(rect.xMin * toSource), x1 = Mathf.CeilToInt(rect.xMax * toSource);
                int y0 = Mathf.FloorToInt(rect.yMin * toSource), y1 = Mathf.CeilToInt(rect.yMax * toSource);
                Color32[] pixels = source.GetPixels32();
                int low = -1, high = -1;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (pixels[y * source.width + x].a > 16)
                        {
                            if (low < 0) low = y;
                            high = y;
                            break;
                        }
                if (low < 0) throw new InvalidOperationException("Empty drawing: " + path);
                return (high - low + 1f) / (y1 - y0);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        // Bottom-centre pivot: the frame stands on the spot where the body stood.
        private static void ImportFrame(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing defeat frame: " + path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                !importer.alphaIsTransparency || settings.spriteAlignment != (int)SpriteAlignment.BottomCenter;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
