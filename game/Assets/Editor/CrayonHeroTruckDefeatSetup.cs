using System;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Defeat-6 (2026-11-19 user drawings): 황금 용사 is run over by a truck that drives in from outside the room and
    // out again, and 에르핀 jumps for joy where she stands when a boss dies. The values below are the defaults the
    // user accepted in the timing editor (output/crayon-hero-truck-defeat/truck-defeat-editor.html); sizes and
    // offsets are shares of the boss's visible height. Frames come from scripts/split-truck-defeat-and-erpin-victory.py.
    // Re-running re-imports the same files and updates the same Prefab without adding a second component.
    public static class CrayonHeroTruckDefeatSetup
    {
        public const string BossPrefabPath = "Assets/Prefabs/CrayonHeroBoss.prefab";
        public const string FrameFolder = "Assets/Art/Bosses/FairyKingdom/Death";
        public const string GoldenBodyPath = "Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Golden_Walk_0.png";
        public const string ErpinVictoryFolder = "Assets/Resources/Characters/Erpin_Victory";
        public const int DriveFrameCount = 2;
        public const int ImpactFrameCount = 8;
        public const float TruckPixelsPerUnit = 400f;

        public static string DrivePath(int i) => $"{FrameFolder}/CrayonHero_TruckDrive_{i}.png";
        public static string ImpactPath(int i) => $"{FrameFolder}/CrayonHero_TruckImpact_{i}.png";
        public static string ErpinVictoryPath(int i) => $"{ErpinVictoryFolder}/Erpin_Victory_{i}.png";

        // x forward from the truck anchor, y up from the boss's feet, width, height — in boss heights. Each crash
        // frame was lined up against the truck so the truck does not jump between frames.
        public static readonly Rect[] DriveRects =
        {
            new(-1.1035f, 0f, 2.2070f, 1.1095f),
            new(-1.2126f, 0f, 2.3161f, 1.1065f),
        };
        // Play order: 1L contact, 1R pushed, 2L thrown, 3L flash, 3R glowing smoke, 2R smoke, 4L thick smoke, 4R clearing.
        public static readonly Rect[] ImpactRects =
        {
            new(-0.9943f, -0.0455f, 3.1983f, 1.2035f),
            new(-1.1156f, -0.1122f, 3.2892f, 1.4036f),
            new(-1.0163f, -0.1406f, 3.1095f, 1.5389f),
            new(-1.0163f, -0.3312f, 3.1355f, 1.6717f),
            new(-0.9802f, -0.0424f, 2.7183f, 1.3238f),
            new(-1.0394f, -0.0367f, 3.0633f, 1.3772f),
            new(-1.0047f, -0.0338f, 2.9969f, 1.3743f),
            new(-1.0163f, -0.0193f, 2.9334f, 1.3714f),
        };
        public static readonly float[] ImpactSeconds = { 0.12f, 0.12f, 0.14f, 0.11f, 0.11f, 0.12f, 0.14f, 0.16f };
        public static readonly Vector2 AnchorFromFeet = new(-1.592f, 0f);
        public const float WaitSeconds = 0.35f, ApproachSeconds = 0.55f, ApproachDistance = 16f;
        public const float ImpactTravel = 0.3618f, ExitSeconds = 0.9f, ExitDistance = 30f, ExitAcceleration = 0.3f;
        public const float WheelFps = 16f, ShakePeak = 0.25f, ShakeSeconds = 0.3f, VictoryAfterImpact = 0.6f;

        [MenuItem("Trickal Fan Game/Artwork/Apply Truck Defeat And Erpin Victory")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");

            // 에르핀: frame 0 as tall as her idle walk frame, standing on the same ground line (like her defeat).
            Sprite idle = Defeat1MotionSetup.ErpinIdle();
            Rect idleVisible = Defeat1MotionSetup.VisibleRect(idle);
            float idleBottom = (idleVisible.yMin - idle.pivot.y) / idle.pixelsPerUnit;
            RectInt victoryRef = Defeat1MotionSetup.VisiblePixels(ErpinVictoryPath(0));
            float victoryPpu = victoryRef.height / (idleVisible.height / idle.pixelsPerUnit);
            Defeat1MotionSetup.ImportShared(ErpinVictoryPath, victoryRef, victoryPpu, idleBottom,
                PlayerVictoryFrameCount);

            Sprite[] drive = Enumerable.Range(0, DriveFrameCount).Select(i => ImportCentred(DrivePath(i))).ToArray();
            Sprite[] impact = Enumerable.Range(0, ImpactFrameCount).Select(i => ImportCentred(ImpactPath(i))).ToArray();
            Sprite golden = AssetDatabase.LoadAssetAtPath<Sprite>(GoldenBodyPath);
            if (golden == null) throw new InvalidOperationException("Missing " + GoldenBodyPath);
            Rect body = Defeat1MotionSetup.VisibleRect(golden);
            float visibleShare = body.height / golden.rect.height;
            float bottomShare = body.yMin / golden.rect.height;

            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var defeat = root.GetComponent<CrayonHeroTruckDefeat>();
                bool added = defeat == null;
                if (added) defeat = root.AddComponent<CrayonHeroTruckDefeat>();
                string before = EditorJsonUtility.ToJson(defeat);
                defeat.Configure(drive, (Rect[])DriveRects.Clone(), impact, (Rect[])ImpactRects.Clone(),
                    (float[])ImpactSeconds.Clone(), AnchorFromFeet, visibleShare, bottomShare);
                defeat.ConfigureTiming(WaitSeconds, ApproachSeconds, ApproachDistance, ImpactTravel, ExitSeconds,
                    ExitDistance, ExitAcceleration, WheelFps, ShakePeak, ShakeSeconds, VictoryAfterImpact);
                // Already set: do not re-save, which would only reserialize unrelated fields.
                if (added || before != EditorJsonUtility.ToJson(defeat))
                {
                    EditorUtility.SetDirty(defeat);
                    if (PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath) == null)
                        throw new InvalidOperationException("Could not save " + BossPrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Truck defeat setup complete: 황금 용사 is run over by the drawn truck (2 wheel and 8 crash " +
                      "frames) and 에르핀 has six drawn victory frames.");
        }

        public const int PlayerVictoryFrameCount = PlayerWalkAnimator.VictoryFrameCount;

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            CrayonHeroTruckDefeatVerification.Verify();
        }

        // Centre pivot: the view places each picture by the centre of its rect.
        private static Sprite ImportCentred(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing truck frame: " + path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.maxTextureSize < 2048 || settings.spriteAlignment != (int)SpriteAlignment.Center ||
                Mathf.Abs(importer.spritePixelsPerUnit - TruckPixelsPerUnit) > 0.001f;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.spritePixelsPerUnit = TruckPixelsPerUnit;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import " + path);
            return sprite;
        }
    }
}
