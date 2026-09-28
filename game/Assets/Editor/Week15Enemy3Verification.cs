using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy3Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-3 Pursuit Charger")]
        public static void Verify()
        {
            ValidatePrefabContract();
            ValidatePursuitChargeCycle();
            ValidateCancellation();
            Week15Enemy1Verification.Verify();
            Week7EnemyBalanceVerification.Verify();
            Debug.Log(
                "Week 15 Enemy-3 verification passed: the charger pursues between attacks, tracks a visible " +
                "target during windup, locks on dash start, recovers after dashing or hitting a wall, and cancels safely on " +
                "knockback, death, and room disable; Enemy-0~1 and legacy enemy regressions also pass.");
        }

        public static void SetupAndVerifyBatch()
        {
            string path = Week15Enemy0Setup.ChargingPrefabPath;
            string guid = AssetDatabase.AssetPathToGUID(path);
            Week15Enemy3Setup.Setup();
            Week15Enemy3Setup.Setup();
            Assert(AssetDatabase.AssetPathToGUID(path) == guid,
                "Enemy-3 Setup changed the charging prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChargingPrefabPath);
            Assert(prefab != null &&
                   prefab.GetComponents<ChargingEnemyController>().Length == 1 &&
                   prefab.GetComponents<EnemyBehaviorContext>().Length == 1 &&
                   prefab.GetComponents<EnemyAttackPresentation>().Length == 1 &&
                   prefab.GetComponents<LineRenderer>().Length == 1,
                "Enemy-3 prefab must have exactly one controller, shared behavior, presentation, and path renderer.");

            ChargingEnemyController charging = prefab.GetComponent<ChargingEnemyController>();
            Assert(charging.PursuitSpeed > 0f && charging.PursuitDuration > 0f,
                "Enemy-3 prefab must spend a positive duration actively pursuing between charges.");
            float dashDistance = charging.DashSpeed * charging.DashDuration;
            Assert(dashDistance >= 6f && dashDistance < 12f,
                "Enemy-3 dash must cross meaningful space without spanning half of the 24-unit Large room.");
        }

        private static void ValidatePursuitChargeCycle()
        {
            GameObject root = new("Enemy-3 Cycle Verification");
            GameObject player = CreatePlayer(root.transform);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body,
                out ChargingEnemyController charging, out EnemyAttackPresentation presentation);

            try
            {
                charging.ConfigurePursuitCharge(10f, 3f, 1f, 0.5f, 8f, 0.75f, 0.7f, EnemyDamageTier.Heavy);
                player.transform.position = Vector2.right * 5f;
                charging.SetTarget(player.transform);

                charging.TickBehavior(0f);
                Assert(charging.State == ChargingEnemyState.Pursuing && body.linearVelocity.x > 0f,
                    "The charger must begin by pursuing its target instead of waiting in place.");
                charging.TickBehavior(0.99f);
                Assert(charging.State == ChargingEnemyState.Pursuing,
                    "Pursuit must last for the configured interval before windup.");
                charging.TickBehavior(1f);
                Vector2 locked = charging.LockedDirection;
                LineRenderer path = enemy.GetComponent<LineRenderer>();
                Assert(charging.State == ChargingEnemyState.Windup && body.linearVelocity == Vector2.zero &&
                       locked.x > 0.99f && presentation.Phase == EnemyAttackPhase.Telegraph && path.enabled &&
                       Vector2.Dot(((Vector2)path.GetPosition(1) - (Vector2)path.GetPosition(0)).normalized, locked) > 0.99f,
                    "Windup must stop movement and show the locked dash path without relying on color alone.");

                player.transform.position = Vector2.left * 5f;
                charging.TickBehavior(1.49f);
                Vector2 tracked = charging.LockedDirection;
                Assert(charging.State == ChargingEnemyState.Windup && tracked.x < -0.99f &&
                       Vector2.Dot(((Vector2)path.GetPosition(1) - (Vector2)path.GetPosition(0)).normalized,
                           tracked) > 0.99f,
                    "The windup path must keep tracking the player's current position until the dash starts.");
                charging.TickBehavior(1.5f);
                Assert(charging.State == ChargingEnemyState.Dashing && body.linearVelocity.x < 0f && !path.enabled &&
                       charging.LockedDirection == tracked,
                    "Dash start must lock the latest tracked direction and hide the telegraph path once active.");

                GameObject wall = new("Enemy-3 Wall");
                wall.transform.SetParent(root.transform);
                wall.layer = LayerMask.NameToLayer("Environment");
                Collider2D wallCollider = wall.AddComponent<BoxCollider2D>();
                Assert(charging.TryResolveCollision(wallCollider, 1.6f) &&
                       charging.State == ChargingEnemyState.Recovering && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Recovery,
                    "A wall collision must end the dash in a stationary, readable recovery.");

                charging.TickBehavior(2.3f);
                Assert(charging.State == ChargingEnemyState.Pursuing && body.linearVelocity.x < 0f,
                    "After recovery the charger must resume pursuit before another charge.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateCancellation()
        {
            GameObject root = new("Enemy-3 Cancellation Verification");
            GameObject player = CreatePlayer(root.transform);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body,
                out ChargingEnemyController charging, out EnemyAttackPresentation presentation);
            Health health = enemy.GetComponent<Health>();
            KnockbackReceiver knockback = enemy.GetComponent<KnockbackReceiver>();

            try
            {
                charging.ConfigurePursuitCharge(10f, 3f, 0.2f, 0.5f, 8f, 0.75f, 0.7f, EnemyDamageTier.Heavy);
                player.transform.position = Vector2.right * 5f;
                charging.SetTarget(player.transform);
                charging.TickBehavior(0f);
                charging.TickBehavior(0.2f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "Cancellation verification requires an active windup.");

                Assert(knockback.Apply(Vector2.left, 5f, 0.2f, 0.2f),
                    "Enemy-3 must accept knockback.");
                charging.TickBehavior(0.21f);
                Assert(charging.State == ChargingEnemyState.Idle && body.linearVelocity.x < 0f &&
                       presentation.Phase == EnemyAttackPhase.Idle && !enemy.GetComponent<LineRenderer>().enabled,
                    "Knockback must cancel windup without overwriting knockback velocity or leaving a path.");

                knockback.Stop();
                charging.TickBehavior(1f);
                health.TakeDamage(health.MaxHealth);
                charging.TickBehavior(1.01f);
                Assert(charging.State == ChargingEnemyState.Idle && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Idle,
                    "Death must safely cancel pursuit or attack movement.");

                health.ResetHealth();
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                charging.TickBehavior(2f);
                InvokeLifecycle(charging, "OnDisable");
                Assert(charging.State == ChargingEnemyState.Idle && body.linearVelocity == Vector2.zero &&
                       !enemy.GetComponent<LineRenderer>().enabled,
                    "Room disable must clear movement, locked direction, and the telegraph path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(Transform parent)
        {
            GameObject player = new("Enemy-3 Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            Health health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<DamageInvulnerability>();
            player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            return player;
        }

        private static GameObject CreateEnemy(
            Transform parent,
            out Rigidbody2D body,
            out ChargingEnemyController charging,
            out EnemyAttackPresentation presentation)
        {
            GameObject enemy = new("Enemy-3 Charger");
            enemy.transform.SetParent(parent);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>();
            Health health = enemy.AddComponent<Health>();
            KnockbackReceiver knockback = enemy.AddComponent<KnockbackReceiver>();
            enemy.AddComponent<EnemyBehaviorContext>();
            presentation = enemy.AddComponent<EnemyAttackPresentation>();
            charging = enemy.AddComponent<ChargingEnemyController>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(presentation, "Awake");
            InvokeLifecycle(charging, "Awake");
            return enemy;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
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
