using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy1Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-1 Damage and Readability")]
        public static void Verify()
        {
            ValidatePrefabContract();
            ValidateSharedHitInvulnerability();
            ValidateAttackLifecycleAndCancellation();
            Week7ChaserMonsterVerification.VerifyEnemyBehaviorOnly();
            Week7RangedEnemyVerification.Verify();
            Week7ChargingEnemyVerification.Verify();
            Debug.Log(
                "Week 15 Enemy-1 verification passed: contact, melee, projectile, and charge damage share " +
                "one post-hit window; high-grade invulnerability remains independent; telegraph, active, " +
                "and recovery use distinct silhouettes and cancel safely on knockback, death, and disable.");
        }

        public static void SetupAndVerifyBatch()
        {
            string path = Week15Enemy0Setup.ChargingPrefabPath;
            string guid = AssetDatabase.AssetPathToGUID(path);
            Week15Enemy1Setup.Setup();
            Week15Enemy1Setup.Setup();
            Assert(AssetDatabase.AssetPathToGUID(path) == guid,
                "Enemy-1 Setup changed the charging prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Week15Enemy0Setup.ChargingPrefabPath);
            Assert(prefab != null && prefab.GetComponents<EnemyAttackPresentation>().Length == 1,
                "The charging prefab must contain exactly one shared attack presentation.");
        }

        private static void ValidateSharedHitInvulnerability()
        {
            GameObject root = new("Enemy-1 Damage Verification");
            GameObject player = CreatePlayer(root.transform, out Health health,
                out DamageInvulnerability invulnerability);
            GameObject contact = new("Contact Source");
            GameObject melee = new("Melee Source");
            GameObject projectile = new("Projectile Source");
            GameObject charge = new("Charge Source");
            contact.transform.SetParent(root.transform);
            melee.transform.SetParent(root.transform);
            projectile.transform.SetParent(root.transform);
            charge.transform.SetParent(root.transform);

            try
            {
                invulnerability.Configure(0.35f);
                float initialHealth = health.CurrentHealth;
                health.TakeDamage(new DamageContext(contact, DamageSourceType.EnemyContact, 1f));
                health.TakeDamage(new DamageContext(melee, DamageSourceType.EnemyContact, 2f));
                health.TakeDamage(new DamageContext(projectile, DamageSourceType.EnemyProjectile, 3f));
                health.TakeDamage(new DamageContext(charge, DamageSourceType.EnemyContact, 4f));
                Assert(Mathf.Approximately(health.CurrentHealth, initialHealth - 1f),
                    "Contact, melee, projectile, and charge attempts in one hit window must apply only once.");
                Assert(invulnerability.IsHitInvulnerableAt(Time.time) &&
                       !invulnerability.IsHitInvulnerableAt(invulnerability.HitInvulnerableUntil),
                    "The post-hit window must be active before, but not at, its exact end boundary.");

                invulnerability.ResetHitWindow();
                health.SetInvulnerable(true);
                health.TakeDamage(new DamageContext(projectile, DamageSourceType.EnemyProjectile, 3f));
                Assert(Mathf.Approximately(health.CurrentHealth, initialHealth - 1f) &&
                       !invulnerability.IsHitInvulnerableAt(Time.time),
                    "High-grade invulnerability must block damage without starting or consuming a hit window.");

                health.SetInvulnerable(false);
                health.TakeDamage(new DamageContext(projectile, DamageSourceType.EnemyProjectile, 1f));
                health.SetInvulnerable(true);
                health.SetInvulnerable(false);
                Assert(Mathf.Approximately(health.CurrentHealth, initialHealth - 2f) && health.IsInvulnerable,
                    "Ending high-grade invulnerability must not clear an already active post-hit window.");

                invulnerability.ResetHitWindow();
                Assert(!health.IsInvulnerable,
                    "The player must become vulnerable after both independent invulnerability sources end.");
                health.ResetHealth();
                Assert(!health.IsInvulnerable &&
                       !invulnerability.IsHitInvulnerableAt(Time.time),
                    "Resetting player health must clear explicit and post-hit invulnerability.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateAttackLifecycleAndCancellation()
        {
            GameObject root = new("Enemy-1 Lifecycle Verification");
            GameObject player = CreatePlayer(root.transform, out _, out _);
            GameObject enemy = CreateChargingEnemy(root.transform, out Health enemyHealth,
                out KnockbackReceiver knockback, out ChargingEnemyController charging,
                out EnemyAttackPresentation presentation);

            try
            {
                charging.Configure(7f, 0.5f, 8f, 0.5f, 0.5f, 1f, 2f);
                player.transform.position = Vector2.right * 3f;
                charging.SetTarget(player.transform);
                Vector3 idleScale = enemy.transform.localScale;

                charging.TickBehavior(0f);
                Vector3 telegraphScale = enemy.transform.localScale;
                Assert(charging.State == ChargingEnemyState.Windup &&
                       presentation.Phase == EnemyAttackPhase.Telegraph && telegraphScale != idleScale,
                    "Windup must be stationary and visibly change the enemy silhouette.");

                charging.TickBehavior(0.5f);
                Vector3 activeScale = enemy.transform.localScale;
                Assert(charging.State == ChargingEnemyState.Dashing &&
                       presentation.Phase == EnemyAttackPhase.Active &&
                       activeScale != telegraphScale && activeScale != idleScale,
                    "The dangerous active phase must have a distinct silhouette from windup and idle.");

                charging.TickBehavior(1f);
                Vector3 recoveryScale = enemy.transform.localScale;
                Assert(charging.State == ChargingEnemyState.Recovering &&
                       presentation.Phase == EnemyAttackPhase.Recovery &&
                       new[] { idleScale, telegraphScale, activeScale }.All(scale => scale != recoveryScale),
                    "Recovery must expose a fourth, distinct silhouette and no active movement.");

                charging.TickBehavior(1.5f);
                charging.TickBehavior(2f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "The cancellation checks require a later active windup.");
                Assert(knockback.Apply(Vector2.left, 5f, 0.1f, 0.1f),
                    "The lifecycle enemy must accept knockback.");
                charging.TickBehavior(2.01f);
                Assert(charging.State == ChargingEnemyState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle,
                    "Knockback must cancel the attack presentation and lifecycle.");

                knockback.Stop();
                charging.TickBehavior(4f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "The death cancellation check requires another windup.");
                enemyHealth.TakeDamage(enemyHealth.MaxHealth);
                charging.TickBehavior(4.01f);
                Assert(charging.State == ChargingEnemyState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle,
                    "Death must cancel an in-progress attack and restore the idle silhouette.");

                InvokeLifecycle(charging, "OnDisable");
                InvokeLifecycle(presentation, "OnDisable");
                Assert(charging.State == ChargingEnemyState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle &&
                       enemy.transform.localScale == idleScale,
                    "Room disable must clear the lifecycle and restore the original scale.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(
            Transform parent,
            out Health health,
            out DamageInvulnerability invulnerability)
        {
            GameObject player = new("Enemy-1 Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            invulnerability = player.AddComponent<DamageInvulnerability>();
            player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            return player;
        }

        private static GameObject CreateChargingEnemy(
            Transform parent,
            out Health health,
            out KnockbackReceiver knockback,
            out ChargingEnemyController charging,
            out EnemyAttackPresentation presentation)
        {
            GameObject enemy = new("Enemy-1 Charging Enemy");
            enemy.transform.SetParent(parent);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>();
            health = enemy.AddComponent<Health>();
            knockback = enemy.AddComponent<KnockbackReceiver>();
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
