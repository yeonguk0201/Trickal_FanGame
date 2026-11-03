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
        public const int FrameCount = 4;

        // Prefab name → frame set name. 대왕쥬비 shares the 쥬비 drawings at its own size.
        public static readonly (string Prefab, string Frames)[] Bindings =
        {
            ("JyubiEnemy", "Jyubi"),
            ("JyubiKing", "Jyubi"),
            ("QuickRangedFairy", "SansamoPure"),
            ("SansamoEnemy", "Sansamo"),
            ("HighBloodSugarFairy", "HighBloodSugarFairy"),
            ("ChargingEnemy", "LowBloodSugarFairy"),
            ("LobbingFairy", "LowBloodSugarMadness"),
            ("LivelyLowBloodSugarFairy", "LivelyFairy"),
        };

        public static string FramePath(string frames, int index) => $"{FrameRoot}/{frames}_Death_{index}.png";

        [MenuItem("Trickal Fan Game/Artwork/Apply Enemy Defeat Frames")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string frames in Bindings.Select(binding => binding.Frames).Distinct())
                for (int i = 0; i < FrameCount; i++) ImportFrame(FramePath(frames, i));
            foreach (var (prefab, frames) in Bindings)
            {
                Sprite[] sprites = Enumerable.Range(0, FrameCount)
                    .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(frames, i))).ToArray();
                if (sprites.Any(sprite => sprite == null))
                    throw new InvalidOperationException("Missing defeat frames: " + frames);
                string path = "Assets/Prefabs/" + prefab + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var defeat = root.GetComponent<EnemyDefeatAnimation>();
                    if (defeat == null) defeat = root.AddComponent<EnemyDefeatAnimation>();
                    var serialized = new SerializedObject(defeat);
                    SerializedProperty list = serialized.FindProperty("frames");
                    list.arraySize = sprites.Length;
                    for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                    serialized.FindProperty("sizeMultiplier").floatValue = SizeMultiplier(root, sprites[0]);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Defeat-0 setup complete: {Bindings.Length} enemy Prefabs use the supplied four-frame defeat drawings.");
        }

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            EnemyDefeatArtworkVerification.Verify();
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
