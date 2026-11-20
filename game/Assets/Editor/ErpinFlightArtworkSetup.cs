using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Flight art (user drawings, 2026-11-20): 에르핀 with 시스트의 가짜 날개. Frames come from
    // scripts/split-erpin-flight.py: twelve wing-beat frames (Down/Side/Up x4) on one canvas and the standing drawing.
    // The front frame 0 and the standing drawing are each as tall as her idle walk frame and stand on its ground line,
    // so taking the wings changes the drawing without moving her feet or the flight shadow. All twelve frames share
    // one scale and pivot, so the wing beat's bob is kept. Re-running re-imports the same files only when a setting
    // differs.
    public static class ErpinFlightArtworkSetup
    {
        public const string Folder = "Assets/Resources/Characters/Erpin_Flying";
        public static readonly string[] Directions = { "Down", "Side", "Up" };
        public const int FramesPerDirection = 4;
        public const float HeightTolerance = 0.02f;

        public static string FramePath(int i) =>
            $"{Folder}/Erpin_Fly_{Directions[i / FramesPerDirection]}_{i % FramesPerDirection}.png";
        public static string IdlePath(int _) => $"{Folder}/{PlayerWalkAnimator.FlightIdleName}.png";

        [MenuItem("Trickal Fan Game/Artwork/Apply Erpin Flight Artwork")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");

            IdleGround(out float idleHeight, out float idleBottom);
            RectInt frameRef = Defeat1MotionSetup.VisiblePixels(FramePath(0));
            Defeat1MotionSetup.ImportShared(FramePath, frameRef, frameRef.height / idleHeight, idleBottom,
                Directions.Length * FramesPerDirection);
            RectInt standRef = Defeat1MotionSetup.VisiblePixels(IdlePath(0));
            Defeat1MotionSetup.ImportShared(IdlePath, standRef, standRef.height / idleHeight, idleBottom, 1);
            AssetDatabase.SaveAssets();
            Debug.Log("Erpin flight artwork applied: 12 wing-beat frames and the standing drawing at her idle height.");
        }

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            Verify();
        }

        // Before a commit: this slice plus the walk animation and the flight behaviour it draws.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            ErpinWalkAnimationVerification.Verify();
            CrayonHeroTruckDefeatVerification.Verify();
            Week22Flight0Verification.Verify();
        }

        [MenuItem("Trickal Fan Game/Verification/Verify Erpin Flight Artwork")]
        public static void Verify()
        {
            IdleGround(out float idleHeight, out float idleBottom);
            Sprite[] sprites = Resources.LoadAll<Sprite>(PlayerWalkAnimator.FlightResourcePath);
            Require(sprites.Length == Directions.Length * FramesPerDirection + 1,
                $"Expected 13 Erpin flight drawings, found {sprites.Length}.");
            Sprite first = Find(sprites, "Erpin_Fly_Down_0");
            for (int i = 0; i < Directions.Length * FramesPerDirection; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(FramePath(i));
                Sprite frame = Find(sprites, name);
                Require(frame.rect.size == first.rect.size && frame.pivot == first.pivot &&
                        Mathf.Approximately(frame.pixelsPerUnit, first.pixelsPerUnit),
                    $"{name} must share frame 0's canvas, pivot and scale so the wing beat does not jump.");
            }
            CheckStands(first, idleHeight, idleBottom);
            Sprite stand = Find(sprites, PlayerWalkAnimator.FlightIdleName);
            CheckStands(stand, idleHeight, idleBottom);
            VerifyRuntime();
            Debug.Log("Erpin flight artwork verification passed: 12 wing-beat frames and the standing drawing stand " +
                      "on her idle ground line, and the animator swaps to them only while flying.");
        }

        private static void VerifyRuntime()
        {
            GameObject player = new("ErpinFlightVerification") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
                renderer.sprite = Defeat1MotionSetup.ErpinIdle();
                player.AddComponent<Rigidbody2D>().gravityScale = 0f;
                PlayerMovement movement = player.AddComponent<PlayerMovement>();
                Invoke(movement, "Awake");
                PlayerWalkAnimator animator = player.GetComponent<PlayerWalkAnimator>();
                Require(animator != null, "PlayerMovement must attach PlayerWalkAnimator.");
                Invoke(animator, "Awake");
                Require(animator.HasFlightFrames, "The animator did not load the flight drawings.");

                Invoke(animator, "LateUpdate");
                Require(!animator.IsShowingFlight && renderer.sprite == animator.IdleSprite,
                    "On foot she must keep her walking idle drawing.");

                PlayerFlight flight = player.AddComponent<PlayerFlight>();
                Require(flight.TryStartFlying(), "Flight did not start.");
                Invoke(animator, "LateUpdate");
                Require(animator.IsShowingFlight && renderer.sprite == animator.FlightIdleSprite,
                    "Hovering in place must show the standing flight drawing.");

                MethodInfo select = typeof(PlayerWalkAnimator).GetMethod("SelectFrames",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(select != null, "PlayerWalkAnimator.SelectFrames could not be found.");
                (Vector2 dir, string prefix)[] cases =
                {
                    (Vector2.down, "Erpin_Fly_Down_"), (Vector2.left, "Erpin_Fly_Side_"), (Vector2.up, "Erpin_Fly_Up_"),
                };
                foreach (var (dir, prefix) in cases)
                {
                    var frames = (Sprite[])select.Invoke(animator, new object[] { dir });
                    Require(frames.All(f => f != null && f.name.StartsWith(prefix)),
                        $"Flying toward {dir} must use the {prefix}* frames.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void CheckStands(Sprite sprite, float idleHeight, float idleBottom)
        {
            Rect visible = Defeat1MotionSetup.VisibleRect(sprite);
            float height = visible.height / sprite.pixelsPerUnit;
            float bottom = (visible.yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
            Require(Mathf.Abs(height / idleHeight - 1f) < HeightTolerance,
                $"{sprite.name} must be as tall as the idle walk frame ({height:0.000} vs {idleHeight:0.000}).");
            Require(Mathf.Abs(bottom - idleBottom) < 0.01f,
                $"{sprite.name} must stand on the idle ground line ({bottom:0.000} vs {idleBottom:0.000}).");
        }

        private static void IdleGround(out float height, out float bottom)
        {
            Sprite idle = Defeat1MotionSetup.ErpinIdle();
            Rect visible = Defeat1MotionSetup.VisibleRect(idle);
            height = visible.height / idle.pixelsPerUnit;
            bottom = (visible.yMin - idle.pivot.y) / idle.pixelsPerUnit;
        }

        private static Sprite Find(Sprite[] sprites, string name)
        {
            Sprite sprite = sprites.FirstOrDefault(s => s.name == name);
            Require(sprite != null, $"Missing Erpin flight drawing '{name}'.");
            return sprite;
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, $"{target.GetType().Name}.{method} could not be found.");
            info.Invoke(target, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
