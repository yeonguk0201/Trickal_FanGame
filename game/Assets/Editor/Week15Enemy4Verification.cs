using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy4Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-4 Long-Range Sniper")]
        public static void Verify()
        {
            ValidatePrefabRoles();
            ValidateSniperCycleAndDistanceRules();
            ValidateBlockingAndCancellation();
            Week15Enemy1Verification.Verify();
            Week7EnemyBalanceVerification.Verify();
            Debug.Log(
                "Week 15 Enemy-4 verification passed: the original ranged role remains intact; the sniper " +
                "relocates, tracks a readable aim until firing, recovers, can shoot at close range, reverses strafe " +
                "when blocked, remains catchable, and cancels safely.");
        }

        public static void SetupAndVerifyBatch()
        {
            string originalGuid = AssetDatabase.AssetPathToGUID(Week15Enemy0Setup.RangedPrefabPath);
            Week15Enemy4Setup.Setup();
            string sniperGuid = AssetDatabase.AssetPathToGUID(Week15Enemy4Setup.SniperPrefabPath);
            Week15Enemy4Setup.Setup();
            Assert(AssetDatabase.AssetPathToGUID(Week15Enemy0Setup.RangedPrefabPath) == originalGuid,
                "Enemy-4 Setup changed the original ranged prefab GUID.");
            Assert(!string.IsNullOrWhiteSpace(sniperGuid) &&
                   AssetDatabase.AssetPathToGUID(Week15Enemy4Setup.SniperPrefabPath) == sniperGuid,
                "Enemy-4 Setup did not preserve the sniper prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabRoles()
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.RangedPrefabPath);
            Assert(original != null && original.GetComponent<RangedEnemyController>() != null &&
                   original.GetComponent<LongRangeSniperController>() == null,
                "Enemy-4 must preserve the existing ranged prefab and behavior.");

            GameObject sniper = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy4Setup.SniperPrefabPath);
            Assert(sniper != null && sniper.GetComponents<LongRangeSniperController>().Length == 1 &&
                   sniper.GetComponent<RangedEnemyController>() == null &&
                   sniper.GetComponents<EnemyBehaviorContext>().Length == 1 &&
                   sniper.GetComponents<EnemyAttackPresentation>().Length == 1 &&
                   sniper.GetComponents<LineRenderer>().Length == 1,
                "Enemy-4 sniper prefab must have one exclusive sniper behavior and shared lifecycle components.");

            LongRangeSniperController controller = sniper.GetComponent<LongRangeSniperController>();
            Assert(controller.DetectionRange >= 12f && controller.PreferredDistance >= 8f,
                "The sniper must use a long-range contract intended for large rooms.");
            Assert(controller.MoveSpeed < 5f,
                "The sniper retreat speed must remain below Erpin's base movement speed.");
        }

        private static void ValidateSniperCycleAndDistanceRules()
        {
            GameObject root = new("Enemy-4 Cycle Verification");
            GameObject player = CreatePlayer(root.transform, out PlayerMovement playerMovement);
            GameObject enemy = CreateSniper(root.transform, out Rigidbody2D body,
                out LongRangeSniperController sniper, out EnemyAttackPresentation presentation);
            List<EnemyProjectile> fired = new();
            sniper.ProjectileFired += fired.Add;

            try
            {
                sniper.Configure(2.5f, 14f, 8f, 1f, 1f, 0.3f, 0.5f, 0.1f, 0.5f, 7f, EnemyDamageTier.Heavy, 5f);
                player.transform.position = Vector2.right * 12f;
                sniper.SetTarget(player.transform);

                sniper.TickBehavior(0f);
                Assert(sniper.State == LongRangeSniperState.Relocating && body.linearVelocity.x > 0f &&
                       fired.Count == 0,
                    "The sniper must approach during relocation without firing while moving.");
                sniper.TickBehavior(1f);
                Vector2 locked = sniper.LockedDirection;
                LineRenderer path = enemy.GetComponent<LineRenderer>();
                Assert(sniper.State == LongRangeSniperState.Aiming && body.linearVelocity == Vector2.zero &&
                       locked.x > 0.99f && presentation.Phase == EnemyAttackPhase.Telegraph && path.enabled,
                    "The sniper must stop and expose a non-color-only locked aim before firing.");

                player.transform.position = Vector2.left;
                sniper.TickBehavior(1.49f);
                Vector2 tracked = sniper.LockedDirection;
                Assert(tracked.x < -0.99f && fired.Count == 0 &&
                       Vector2.Dot(((Vector2)path.GetPosition(1) - (Vector2)path.GetPosition(0)).normalized,
                           tracked) > 0.99f,
                    "The aim line must track the player's current position without firing prematurely.");
                sniper.TickBehavior(1.5f);
                Assert(sniper.State == LongRangeSniperState.Firing && fired.Count == 1 &&
                       fired[0] != null && fired[0].Velocity.x < 0f && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Active && !path.enabled,
                    "Firing must lock and use the latest tracked direction only after the aim duration.");
                sniper.TickBehavior(1.6f);
                Assert(sniper.State == LongRangeSniperState.Recovering && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Recovery,
                    "A shot must expose a stationary recovery window.");

                sniper.TickBehavior(2.1f);
                Assert(sniper.State == LongRangeSniperState.Relocating && body.linearVelocity.x > 0f &&
                       sniper.MoveSpeed < playerMovement.CurrentMoveSpeed,
                    "At close range the sniper must retreat more slowly than the player can pursue.");
                sniper.TickBehavior(3.1f);
                Assert(sniper.State == LongRangeSniperState.Aiming && sniper.LockedDirection.x < 0f,
                    "Close distance alone must not prevent the next aim cycle from starting.");
                sniper.TickBehavior(3.6f);
                Assert(fired.Count == 2 && fired[1] != null && fired[1].Velocity.x < 0f,
                    "The sniper must remain able to fire when the player has closed the distance.");
            }
            finally
            {
                sniper.ProjectileFired -= fired.Add;
                ClearProjectiles();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBlockingAndCancellation()
        {
            GameObject root = new("Enemy-4 Blocking Verification");
            GameObject player = CreatePlayer(root.transform, out _);
            GameObject enemy = CreateSniper(root.transform, out Rigidbody2D body,
                out LongRangeSniperController sniper, out EnemyAttackPresentation presentation);
            Health health = enemy.GetComponent<Health>();
            KnockbackReceiver knockback = enemy.GetComponent<KnockbackReceiver>();

            try
            {
                sniper.Configure(2.5f, 14f, 8f, 1f, 0.4f, 0.2f, 0.5f, 0.1f, 0.5f, 7f, EnemyDamageTier.Heavy, 5f);
                player.transform.position = Vector2.right * 8f;
                sniper.SetTarget(player.transform);
                sniper.TickBehavior(0f);
                sniper.TickBehavior(0.4f);
                sniper.TickBehavior(1f);
                sniper.TickBehavior(1.1f);
                sniper.TickBehavior(1.6f);
                Vector2 beforeBlock = body.linearVelocity;
                int directionBefore = sniper.StrafeDirection;
                Assert(sniper.State == LongRangeSniperState.Relocating && beforeBlock.sqrMagnitude > 0f,
                    "The second relocation must exercise optional lateral movement.");
                sniper.TickBehavior(1.81f);
                Assert(sniper.StrafeDirection == -directionBefore &&
                       Vector2.Dot(beforeBlock, body.linearVelocity) < 0f,
                    "Relocation must reevaluate and reverse an optional lateral choice at the configured interval.");
                Vector2 reevaluatedVelocity = body.linearVelocity;
                int reevaluatedDirection = sniper.StrafeDirection;
                Assert(sniper.NotifyBlocked(1.82f) && sniper.StrafeDirection == -reevaluatedDirection,
                    "A wall or enemy blockage must reverse the lateral choice immediately.");
                sniper.TickBehavior(1.82f);
                Assert(Vector2.Dot(reevaluatedVelocity, body.linearVelocity) < 0f,
                    "Reversing after blockage must move away from the blocked lateral direction.");

                sniper.TickBehavior(2.6f);
                Assert(sniper.State == LongRangeSniperState.Aiming,
                    "Cancellation verification requires an active aim.");
                Assert(knockback.Apply(Vector2.left, 5f, 0.2f, 0.2f),
                    "Enemy-4 must accept knockback.");
                sniper.TickBehavior(2.61f);
                Assert(sniper.State == LongRangeSniperState.Idle && body.linearVelocity.x < 0f &&
                       presentation.Phase == EnemyAttackPhase.Idle && !enemy.GetComponent<LineRenderer>().enabled,
                    "Knockback must cancel aim without overwriting knockback velocity or leaving an aim line.");

                knockback.Stop();
                sniper.TickBehavior(3f);
                health.TakeDamage(health.MaxHealth);
                sniper.TickBehavior(3.01f);
                Assert(sniper.State == LongRangeSniperState.Idle && body.linearVelocity == Vector2.zero,
                    "Death must cancel relocation and attack state.");

                health.ResetHealth();
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                sniper.TickBehavior(4f);
                InvokeLifecycle(sniper, "OnDisable");
                Assert(sniper.State == LongRangeSniperState.Idle && body.linearVelocity == Vector2.zero &&
                       !enemy.GetComponent<LineRenderer>().enabled,
                    "Room disable must clear movement, aim direction, and presentation.");
            }
            finally
            {
                ClearProjectiles();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(Transform parent, out PlayerMovement movement)
        {
            GameObject player = new("Enemy-4 Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            Health health = player.AddComponent<Health>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<DamageInvulnerability>();
            movement = player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(movement, "Awake");
            return player;
        }

        private static GameObject CreateSniper(
            Transform parent,
            out Rigidbody2D body,
            out LongRangeSniperController sniper,
            out EnemyAttackPresentation presentation)
        {
            GameObject enemy = new("Enemy-4 Sniper");
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
            sniper = enemy.AddComponent<LongRangeSniperController>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(presentation, "Awake");
            InvokeLifecycle(sniper, "Awake");
            return enemy;
        }

        private static void ClearProjectiles()
        {
            foreach (EnemyProjectile projectile in
                     UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                projectile.StopAtBoundary();
            }
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
