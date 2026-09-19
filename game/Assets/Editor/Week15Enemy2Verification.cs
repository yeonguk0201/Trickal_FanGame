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
    public static class Week15Enemy2Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-2 Melee Chasers")]
        public static void Verify()
        {
            ValidatePrefabRoles();
            ValidateLegacyBossIsolation();
            ValidateMeleeLifecycleAndDamageSharing();
            ValidateMeleeCancellation();
            Week15Enemy1Verification.Verify();
            Week7EnemyBalanceVerification.Verify();
            Debug.Log(
                "Week 15 Enemy-2 verification passed: Bulhyojason is slower and stronger than Sansamo; " +
                "both stop for readable melee telegraph/active/recovery phases, retain contact damage, share " +
                "player hit invulnerability, resume chase after recovery, and cancel safely.");
        }

        private static void ValidateLegacyBossIsolation()
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy2Setup.LegacyBossPrefabPath);
            Assert(boss != null && boss.GetComponent<BossController>() != null,
                "The legacy boss prefab must remain available for pre-Boss-0 encounters.");
            Assert(boss.GetComponent<MeleeEnemyAttack>() == null &&
                   boss.GetComponent<EnemyAttackPresentation>() == null,
                "The legacy boss variant must not inherit Enemy-2 normal-enemy melee behavior.");
        }

        public static void SetupAndVerifyBatch()
        {
            string baseGuid = AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.BulhyojasonPrefabPath);
            Week15Enemy2Setup.Setup();
            string sansamoGuid = AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.SansamoPrefabPath);
            Week15Enemy2Setup.Setup();
            Assert(AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.BulhyojasonPrefabPath) == baseGuid,
                "Enemy-2 Setup changed the existing chaser prefab GUID.");
            Assert(!string.IsNullOrWhiteSpace(sansamoGuid) &&
                   AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.SansamoPrefabPath) == sansamoGuid,
                "Enemy-2 Setup did not preserve the Sansamo prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabRoles()
        {
            GameObject bulhyojason = ValidatePrefab(Week15Enemy2Setup.BulhyojasonPrefabPath);
            GameObject sansamo = ValidatePrefab(Week15Enemy2Setup.SansamoPrefabPath);
            EnemyChase slowChase = bulhyojason.GetComponent<EnemyChase>();
            EnemyChase fastChase = sansamo.GetComponent<EnemyChase>();
            ContactDamage strongContact = bulhyojason.GetComponent<ContactDamage>();
            ContactDamage weakContact = sansamo.GetComponent<ContactDamage>();
            MeleeEnemyAttack strongMelee = bulhyojason.GetComponent<MeleeEnemyAttack>();
            MeleeEnemyAttack weakMelee = sansamo.GetComponent<MeleeEnemyAttack>();

            Assert(slowChase.MoveSpeed < fastChase.MoveSpeed,
                "Bulhyojason must move slower than Sansamo.");
            Assert(strongContact.Damage > weakContact.Damage &&
                   strongMelee.AttackDamage > weakMelee.AttackDamage,
                "Bulhyojason contact and melee damage must exceed Sansamo damage.");
            Assert(Mathf.Approximately(strongMelee.AttackRange, weakMelee.AttackRange) &&
                   Mathf.Approximately(strongMelee.TelegraphDuration, weakMelee.TelegraphDuration) &&
                   Mathf.Approximately(strongMelee.RecoveryDuration, weakMelee.RecoveryDuration),
                "The two chaser roles must differ through speed/damage, not hidden timing or range changes.");
        }

        private static GameObject ValidatePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, $"Missing Enemy-2 prefab at {path}.");
            Assert(prefab.GetComponents<EnemyChase>().Length == 1 &&
                   prefab.GetComponents<ContactDamage>().Length == 1 &&
                   prefab.GetComponents<MeleeEnemyAttack>().Length == 1 &&
                   prefab.GetComponents<EnemyAttackPresentation>().Length == 1 &&
                   prefab.GetComponents<EnemyBehaviorContext>().Length == 1,
                $"Enemy-2 prefab {path} must have exactly one chase, contact, melee, presentation, and behavior component.");
            return prefab;
        }

        private static void ValidateMeleeLifecycleAndDamageSharing()
        {
            GameObject root = new("Enemy-2 Lifecycle Verification");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth,
                out DamageInvulnerability invulnerability);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body, out EnemyChase chase,
                out ContactDamage contact, out MeleeEnemyAttack melee, out EnemyAttackPresentation presentation,
                out _);

            try
            {
                chase.Configure(2.25f, 6f, 0.8f);
                contact.Configure(2f, 0f);
                melee.Configure(1.15f, 0.4f, 0.12f, 0.65f, 0.2f, 3f);
                chase.SetTarget(player.transform);
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                player.transform.position = Vector2.right;

                chase.TickChase();
                melee.TickAttack(0f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Telegraph && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Telegraph,
                    "Entering melee range must start a stationary, visible telegraph.");

                float healthBefore = playerHealth.CurrentHealth;
                DamageSourceType appliedType = DamageSourceType.Unknown;
                playerHealth.DamageApplied += (context, applied, remaining) =>
                {
                    if (applied > 0f)
                    {
                        appliedType = context.SourceType;
                    }
                };
                melee.TickAttack(0.4f);
                Assert(melee.State == MeleeEnemyAttackState.Active &&
                       presentation.Phase == EnemyAttackPhase.Active &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore - 3f) &&
                       appliedType == DamageSourceType.EnemyMelee,
                    "Only the active phase may apply one dedicated melee hit.");
                Assert(!melee.TryResolveActiveHit(),
                    "One active melee phase must never damage the same target twice.");

                Assert(contact.TryApplyDamage(playerHealth, 0.41f) &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore - 3f),
                    "Contact and melee must share the player's post-hit invulnerability window.");

                melee.TickAttack(0.52f);
                Assert(melee.State == MeleeEnemyAttackState.Recovery && body.linearVelocity == Vector2.zero &&
                       presentation.Phase == EnemyAttackPhase.Recovery,
                    "Melee damage must be followed by a stationary recovery window.");

                invulnerability.ResetHitWindow();
                Assert(contact.TryApplyDamage(playerHealth, 0.6f) &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore - 5f),
                    "Contact damage must remain active during the melee recovery lifecycle.");

                player.transform.position = Vector2.right * 3f;
                melee.TickAttack(1.17f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Idle && body.linearVelocity.x > 0f &&
                       presentation.Phase == EnemyAttackPhase.Idle,
                    "After recovery the enemy must release shared movement suppression and resume chase.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateMeleeCancellation()
        {
            GameObject root = new("Enemy-2 Cancellation Verification");
            GameObject player = CreatePlayer(root.transform, out _, out _);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body, out EnemyChase chase,
                out _, out MeleeEnemyAttack melee, out EnemyAttackPresentation presentation,
                out KnockbackReceiver knockback);

            try
            {
                player.transform.position = Vector2.right;
                chase.SetTarget(player.transform);
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                melee.TickAttack(0f);
                Assert(melee.State == MeleeEnemyAttackState.Telegraph,
                    "Cancellation verification requires an active melee telegraph.");

                Assert(knockback.Apply(Vector2.left, 5f, 0.2f, 0.2f),
                    "The melee chaser must accept knockback.");
                melee.TickAttack(0.1f);
                Assert(melee.State == MeleeEnemyAttackState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle && body.linearVelocity.x < 0f,
                    "Knockback must cancel melee without overwriting knockback velocity.");

                knockback.Stop();
                melee.TickAttack(1f);
                Health enemyHealth = enemy.GetComponent<Health>();
                enemyHealth.TakeDamage(enemyHealth.MaxHealth);
                melee.TickAttack(1.1f);
                Assert(melee.State == MeleeEnemyAttackState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle && body.linearVelocity == Vector2.zero,
                    "Death must cancel an in-progress melee attack.");

                enemyHealth.ResetHealth();
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                melee.TickAttack(2f);
                InvokeLifecycle(melee, "OnDisable");
                Assert(melee.State == MeleeEnemyAttackState.Idle &&
                       presentation.Phase == EnemyAttackPhase.Idle && body.linearVelocity == Vector2.zero,
                    "Room disable must cancel melee and clear residual movement.");
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
            GameObject player = new("Enemy-2 Player");
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

        private static GameObject CreateEnemy(
            Transform parent,
            out Rigidbody2D body,
            out EnemyChase chase,
            out ContactDamage contact,
            out MeleeEnemyAttack melee,
            out EnemyAttackPresentation presentation,
            out KnockbackReceiver knockback)
        {
            GameObject enemy = new("Enemy-2 Chaser");
            enemy.transform.SetParent(parent);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>();
            Health health = enemy.AddComponent<Health>();
            knockback = enemy.AddComponent<KnockbackReceiver>();
            EnemyBehaviorContext behavior = enemy.AddComponent<EnemyBehaviorContext>();
            chase = enemy.AddComponent<EnemyChase>();
            contact = enemy.AddComponent<ContactDamage>();
            presentation = enemy.AddComponent<EnemyAttackPresentation>();
            melee = enemy.AddComponent<MeleeEnemyAttack>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(behavior, "Awake");
            InvokeLifecycle(chase, "Awake");
            InvokeLifecycle(presentation, "Awake");
            InvokeLifecycle(melee, "Awake");
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
