using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class BuseureogiCreamProjectileVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Buseureogi Cream Projectile With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week8ProjectileSizingVerification.Verify();
            Week15Boss1Verification.Verify();
            Debug.Log("Buseureogi cream projectile regressions passed: frames, projectile sizing and Boss-1 rules.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Buseureogi Cream Projectile")]
        public static void Verify()
        {
            var frames = new Sprite[BuseureogiCreamProjectileSetup.FramePaths.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = BuseureogiCreamProjectileSetup.FramePaths[i];
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(frames[i] != null, $"Missing cream projectile frame sprite: {path}");
                Vector2 pivot = frames[i].pivot / frames[i].rect.size;
                Require((pivot - BuseureogiCreamProjectileSetup.HeadPivot).sqrMagnitude < 0.0001f,
                    $"{path} pivot must sit on the cream head {BuseureogiCreamProjectileSetup.HeadPivot}, found {pivot}.");
                Require(Mathf.Approximately(frames[i].pixelsPerUnit, BuseureogiCreamProjectileSetup.PixelsPerUnit),
                    $"{path} must import at {BuseureogiCreamProjectileSetup.PixelsPerUnit} PPU.");
            }

            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss0Setup.BossPrefabPath);
            BuseureogiBossPatternRuntime runtime = bossPrefab != null
                ? bossPrefab.GetComponent<BuseureogiBossPatternRuntime>()
                : null;
            Require(runtime != null, "The floor-1 boss prefab must keep its Buseureogi runtime.");
            Require(runtime.ProjectileFrames.Count == frames.Length,
                $"The boss must loop exactly {frames.Length} cream frames, found {runtime.ProjectileFrames.Count}.");
            for (int i = 0; i < frames.Length; i++)
                Require(runtime.ProjectileFrames[i] == frames[i], $"Cream frame {i + 1} is not assigned in order.");

            BossProjectile drawn = null, fallback = null;
            try
            {
                drawn = BossProjectile.Create(new Vector2(-7100f, 7100f), Vector2.up * 5f, null,
                    EnemyDamageTier.Medium, null, frames);
                Require(drawn.GetComponent<SpriteRenderer>() == null && drawn.Artwork != null &&
                        drawn.Artwork.transform.parent == drawn.transform,
                    "Drawn cream must render on a child so rotation and scale leave the hitbox alone.");
                Require(drawn.Artwork.color == Color.white, "The cream drawing must not be tinted.");
                Require(Mathf.Abs(Mathf.DeltaAngle(drawn.Artwork.transform.eulerAngles.z, 90f)) < 0.01f,
                    "The cream head (drawn facing right) must point along the flight direction.");
                Require(Mathf.Approximately(drawn.Artwork.transform.lossyScale.x, BossProjectile.AuthoredArtworkWorldScale),
                    "The cream drawing must keep its authored world scale.");
                CircleCollider2D collider = drawn.GetComponent<CircleCollider2D>();
                Require(Mathf.Approximately(drawn.transform.localScale.x, ProjectileSizing.BossScale) &&
                        Mathf.Approximately(collider.radius, ProjectileSizing.BaseColliderRadius),
                    "Drawn artwork must not change the boss projectile hitbox.");
                // The head fills most of the canvas height; keep it close to the 0.45 player hit distance.
                float canvasHeight = frames[0].bounds.size.y * BossProjectile.AuthoredArtworkWorldScale;
                Require(canvasHeight > 0.45f && canvasHeight < 0.8f,
                    $"The cream drawing must stay readable but near the hit size, found {canvasHeight:0.00} world units tall.");

                float step = 1f / BossProjectile.AuthoredFramesPerSecond;
                int[] expected = { 0, 1, 2, 0 };
                for (int i = 0; i < expected.Length; i++)
                {
                    int shown = drawn.RenderAt(step * i + step * 0.5f);
                    Require(shown == expected[i] && drawn.Artwork.sprite == frames[expected[i]],
                        $"The cream must loop Frame1 → Frame2 → Frame3 → Frame1; step {i} showed {shown}.");
                }

                fallback = BossProjectile.Create(new Vector2(7100f, 7100f), Vector2.right, null,
                    EnemyDamageTier.Medium, frames[0]);
                Require(fallback.Artwork == null && fallback.RenderAt(1f) == -1 &&
                        fallback.GetComponent<SpriteRenderer>()?.sprite == frames[0],
                    "Bosses without cream frames must keep the single tinted sprite.");
            }
            finally
            {
                if (drawn != null) Object.DestroyImmediate(drawn.gameObject);
                if (fallback != null) Object.DestroyImmediate(fallback.gameObject);
            }

            Debug.Log("Buseureogi cream projectile verification passed: 3 looping frames, head pivot, rotation and hitbox.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
