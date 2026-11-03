using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Defeat-1 (docs/28 §7, 2026-11-04): the user's step-3 drawings.
    //  - 산사모-광기 (SelfDestructFairy) walks with four drawn frames as tall as its idle drawing.
    //  - 잠복형 (AmbushFairy) shows a moving mound under the floor and breaks out in three drawn steps; the mound
    //    is as wide as its idle drawing.
    //  - 에르핀 falls over with four drawn poses on death, the first as tall as her idle walk frame.
    // Each frame's size and ground line come from its import settings (pixels per unit and pivot), so the drawing
    // keeps its feet where the idle drawing stood. Re-running re-imports the same files and updates the same
    // Prefabs without adding components. The boss defeats use EnemyDefeatArtworkSetup's bindings.
    public static class Defeat1MotionSetup
    {
        public const string WalkFolder = "Assets/Art/Enemies/FairyKingdom/Walking";
        public const string AmbushFolder = "Assets/Art/Enemies/FairyKingdom/Ambush";
        public const string ErpinDeathFolder = "Assets/Resources/Characters/Erpin_Death";
        public const string ErpinWalkPath = "Assets/Resources/Characters/Erpin_Walking.png";
        public const string ErpinIdleName = "Erpin_Walk_Down_0";
        public const string SelfDestructPrefab = "Assets/Prefabs/SelfDestructFairy.prefab";
        public const string AmbushPrefab = "Assets/Prefabs/AmbushFairy.prefab";
        public const int FrameCount = 4;
        public const float SelfDestructStride = 0.9f;

        public static string WalkPath(int i) => $"{WalkFolder}/SansamoMadness_Walk_{i}.png";
        public static string AmbushPath(int i) => $"{AmbushFolder}/BulhyojasonCold_Emerge_{i}.png";
        public static string ErpinDeathPath(int i) => $"{ErpinDeathFolder}/Erpin_Death_{i}.png";

        [MenuItem("Trickal Fan Game/Artwork/Apply Defeat-1 Motions")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");

            // 산사모-광기: frame 0's visible height = the idle drawing's height, feet on its bottom.
            IdleSize(SelfDestructPrefab, out float walkWidth, out float walkHeight);
            RectInt walkRef = VisiblePixels(WalkPath(0));
            float walkPpu = walkRef.height / walkHeight;
            Sprite[] walk = ImportShared(WalkPath, walkRef, walkPpu, -walkHeight / 2f);
            ConfigurePrefab<EnemyWalkFrames>(SelfDestructPrefab, motion => motion.Configure(walk, SelfDestructStride));

            // 잠복형: the full burst (frame 3) is as wide as the idle drawing, the mound's bottom on its feet.
            IdleSize(AmbushPrefab, out float ambushWidth, out float ambushHeight);
            RectInt ambushRef = VisiblePixels(AmbushPath(AmbushEnemyMotion.SurfacedFrame));
            float ambushPpu = ambushRef.width / ambushWidth;
            Sprite[] ambush = ImportShared(AmbushPath, ambushRef, ambushPpu, -ambushHeight / 2f);
            ConfigurePrefab<AmbushEnemyMotion>(AmbushPrefab, motion => motion.Configure(ambush));

            // 에르핀: frame 0 as tall as her idle walk frame, standing on the same ground line.
            Sprite idle = ErpinIdle();
            Rect idleVisible = VisibleRect(idle);
            float idleBottom = (idleVisible.yMin - idle.pivot.y) / idle.pixelsPerUnit;
            RectInt deathRef = VisiblePixels(ErpinDeathPath(0));
            float deathPpu = deathRef.height / (idleVisible.height / idle.pixelsPerUnit);
            ImportShared(ErpinDeathPath, deathRef, deathPpu, idleBottom);

            AssetDatabase.SaveAssets();
            Debug.Log("Defeat-1 setup complete: Sansamo madness walks, the ambusher shows its mound and burst, and " +
                      "Erpin has four drawn defeat poses.");
        }

        public static void ApplyAndVerifyBatch()
        {
            EnemyDefeatArtworkSetup.Apply();
            Apply();
            EnemyDefeatArtworkVerification.Verify();
            Defeat1MotionVerification.Verify();
        }

        // Before a commit: this slice plus the verifiers of the enemies, player and boss whose drawings it changed.
        public static void VerifyWithRegressionsBatch()
        {
            EnemyDefeatArtworkVerification.Verify();
            Defeat1MotionVerification.Verify();
            Week27Enemy13Verification.Verify();
            Week27Enemy10Verification.Verify();
            CrayonHeroAttackAnimationVerification.Verify();
            ErpinWalkAnimationVerification.Verify();
            ErpinHighGradeArtworkSetup.Verify();
            Week25Ux0Verification.Verify();
        }

        // The idle drawing's local size: FairyKingdomArtwork fits its visible rect to the view's width, centred.
        public static void IdleSize(string prefabPath, out float width, out float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var view = prefab != null ? prefab.GetComponent<FairyKingdomArtworkView>() : null;
            if (view == null) throw new InvalidOperationException("No fitted idle drawing on " + prefabPath);
            var entry = FairyKingdomArtwork.Catalog.Entries.FirstOrDefault(e => e.id == view.ArtworkId);
            if (entry == null || entry.sprite == null) throw new InvalidOperationException("No catalog drawing: " + view.ArtworkId);
            Rect rect = entry.visibleRect.width > 0f ? entry.visibleRect : entry.sprite.rect;
            width = view.LocalWidth;
            height = width * rect.height / rect.width;
        }

        public static Sprite ErpinIdle()
        {
            Sprite idle = AssetDatabase.LoadAllAssetsAtPath(ErpinWalkPath).OfType<Sprite>()
                .FirstOrDefault(sprite => sprite.name == ErpinIdleName);
            if (idle == null) throw new InvalidOperationException("Missing " + ErpinIdleName);
            return idle;
        }

        // Every frame of a set shares one canvas, scale and pivot, taken from the reference frame: its visible
        // bottom sits at groundY (local units) and its visible centre on x = 0.
        private static Sprite[] ImportShared(Func<int, string> pathOf, RectInt reference, float ppu, float groundY)
        {
            var sprites = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                string path = pathOf(i);
                var (w, h) = PngSize(path);
                Vector2 pivot = new((reference.x + reference.width / 2f) / w, (reference.y - groundY * ppu) / h);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing frame: " + path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool changed = importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                    !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                    settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                    (settings.spritePivot - pivot).sqrMagnitude > 1e-10f ||
                    Mathf.Abs(importer.spritePixelsPerUnit - ppu) > 0.001f || importer.maxTextureSize < 2048;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 2048;
                    importer.spritePixelsPerUnit = ppu;
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] == null) throw new InvalidOperationException("Could not import " + path);
            }
            return sprites;
        }

        private static void ConfigurePrefab<T>(string path, Action<T> configure) where T : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                T component = root.GetComponent<T>();
                if (component == null) component = root.AddComponent<T>();
                configure(component);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static (int, int) PngSize(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Unreadable PNG: " + path);
                return (texture.width, texture.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // Visible pixels of a whole PNG, bottom-up like Unity's sprite space.
        public static RectInt VisiblePixels(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Unreadable PNG: " + path);
                return Visible(texture.GetPixels32(), texture.width, new RectInt(0, 0, texture.width, texture.height), path);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // Visible part of a sprite, in its own rect's pixels (the sprite's pivot space).
        public static Rect VisibleRect(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Unreadable PNG: " + path);
                float toSource = (float)texture.width / sprite.texture.width;
                Rect rect = sprite.rect;
                var source = new RectInt(Mathf.FloorToInt(rect.xMin * toSource), Mathf.FloorToInt(rect.yMin * toSource),
                    Mathf.CeilToInt(rect.width * toSource), Mathf.CeilToInt(rect.height * toSource));
                RectInt visible = Visible(texture.GetPixels32(), texture.width, source, path);
                return new Rect((visible.x - source.x) / toSource, (visible.y - source.y) / toSource,
                    visible.width / toSource, visible.height / toSource);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static RectInt Visible(Color32[] pixels, int stride, RectInt area, string path)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    if (pixels[y * stride + x].a <= 16) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            if (maxX < 0) throw new InvalidOperationException("Empty drawing: " + path);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
