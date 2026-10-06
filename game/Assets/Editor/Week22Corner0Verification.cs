using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Corner-0: a boss stops its approach at touching distance, and for the post-hit invulnerability window the
    // player body passes through enemy bodies while the sprite blinks, so a wall or corner never pins the player.
    public static class Week22Corner0Verification
    {
        private const float PlayerRadius = 0.5f;

        // The high-grade skill regression reads the Player of the Game Scene, so the scene is opened (not saved).
        public static void VerifyWithRegressionsBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            Verify();
            Week7HighGradeSkillVerification.Verify();
            Week15Enemy1Verification.Verify();
            Debug.Log("Corner-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Corner-0 Boss Corner Escape")]
        public static void Verify()
        {
            ValidateStopDistance();
            ValidateHitPassThrough();
            Debug.Log("Corner-0 verification passed: bosses stop at touching distance, and a hit lets the player " +
                      "pass through enemy bodies and blink for the hit window only, without touching the flight " +
                      "pit exclusion, a doorway window or the high-grade dash.");
        }

        private static void ValidateStopDistance()
        {
            GameObject target = new("Corner-0 Player");
            GameObject boss = new("Corner-0 Boss");
            try
            {
                target.AddComponent<CircleCollider2D>().radius = PlayerRadius;
                boss.AddComponent<CircleCollider2D>().radius = 1.4f;
                Physics2D.SyncTransforms();
                float stopDistance = BossTargetSpacing.ResolveStopDistance(boss, target.transform);
                Assert(stopDistance > 1.4f && stopDistance < 1.4f + PlayerRadius,
                    "A boss must stop its approach at touching distance, still overlapping enough for contact damage.");
                Assert(Mathf.Approximately(BossTargetSpacing.ResolveStopDistance(boss, null), 0f),
                    "A boss without a target must not hold a stop distance.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(boss);
            }
        }

        private static void ValidateHitPassThrough()
        {
            GameObject player = new("Corner-0 Hit Player");
            try
            {
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                player.AddComponent<CircleCollider2D>().radius = PlayerRadius;
                SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
                Health health = player.AddComponent<Health>();
                player.AddComponent<PlayerStats>();
                PlayerActionState actionState = player.AddComponent<PlayerActionState>();
                DamageInvulnerability invulnerability = player.AddComponent<DamageInvulnerability>();
                PlayerHitPassThrough passThrough = player.AddComponent<PlayerHitPassThrough>();
                Invoke(health, "Awake");
                Invoke(passThrough, "Awake");
                invulnerability.Configure(0.35f);

                int enemyMask = LayerMask.GetMask("Enemy");
                int pitMask = LayerMask.GetMask("Pit");
                Assert(enemyMask != 0 && pitMask != 0, "Corner-0 requires the Enemy and Pit layers.");
                body.excludeLayers = pitMask;

                invulnerability.BeginWindow(10f, 0.75f);
                passThrough.Tick(10.1f);
                passThrough.RenderBlink(10.14f);
                Assert(!passThrough.IsPassingThrough && (body.excludeLayers.value & enemyMask) == 0 &&
                       Mathf.Approximately(renderer.color.a, 1f),
                    "A doorway invulnerability window must not pass through enemies or blink.");

                invulnerability.ResetHitWindow();
                invulnerability.BeginHitWindow(20f);
                passThrough.Tick(20.1f);
                Assert(passThrough.IsPassingThrough && (body.excludeLayers.value & enemyMask) != 0 &&
                       (body.excludeLayers.value & pitMask) != 0,
                    "A hit must let the body pass through enemies without dropping the flight pit exclusion.");

                // Blink steps alternate every BlinkInterval: 20.19 falls on a dimmed step, 20.12 on a solid one.
                passThrough.RenderBlink(20.19f);
                Assert(Mathf.Approximately(renderer.color.a, PlayerHitPassThrough.BlinkAlpha),
                    "The sprite must dim on a blink step during the hit window.");
                passThrough.RenderBlink(20.12f);
                Assert(Mathf.Approximately(renderer.color.a, 1f), "The sprite must be solid between blink steps.");

                Assert(actionState.TryBeginUltimate(Vector2.right, 2f), "The fixture must start a high-grade dash.");
                passThrough.Tick(20.2f);
                Assert(!passThrough.IsPassingThrough && (body.excludeLayers.value & enemyMask) == 0,
                    "The high-grade dash must keep its enemy collisions during the hit window.");
                actionState.ForceNormal();
                passThrough.Tick(20.2f);
                Assert(passThrough.IsPassingThrough, "Pass-through must resume when the dash ends inside the window.");

                passThrough.RenderBlink(20.19f);
                passThrough.Tick(20.36f);
                passThrough.RenderBlink(20.36f);
                Assert(!passThrough.IsPassingThrough && (body.excludeLayers.value & enemyMask) == 0 &&
                       (body.excludeLayers.value & pitMask) != 0 && Mathf.Approximately(renderer.color.a, 1f),
                    "When the hit window ends, enemy collisions and the solid sprite must return.");

                invulnerability.BeginHitWindow(30f);
                passThrough.Tick(30.1f);
                passThrough.RenderBlink(30.1f);
                Invoke(passThrough, "OnDisable");
                Assert(!passThrough.IsPassingThrough && (body.excludeLayers.value & enemyMask) == 0 &&
                       Mathf.Approximately(renderer.color.a, 1f),
                    "Disabling the player must restore enemy collisions and the solid sprite.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void Invoke(MonoBehaviour component, string methodName)
        {
            component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
