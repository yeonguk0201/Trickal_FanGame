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
    // Melee-Lunge: the three lunging chasers telegraph from outside touching range, follow the target, fix their
    // direction shortly before the swing and step forward with the hit area in front of the body. Stepping out of the
    // fixed line dodges, standing in it is hit once, and the stationary swing of the other melee enemies is unchanged.
    public static class Week22MeleeLungeVerification
    {
        private const float TelegraphDuration = 0.4f;
        private const float ActiveDuration = 0.12f;

        public static void SetupAndVerifyBatch()
        {
            Week22MeleeLungeSetup.Setup();
            string[] paths = Week22MeleeLungeSetup.LungePrefabPaths
                .Concat(Week22MeleeLungeSetup.StationaryPrefabPaths).ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week22MeleeLungeSetup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Melee-Lunge setup changed a melee enemy prefab GUID.");
            VerifyWithRegressionsBatch();
        }

        // Also re-runs the shared melee lifecycle, chaser roles and enemy balance.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week15Enemy2Verification.Verify();
            Debug.Log("Melee-Lunge regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Melee Lunge")]
        public static void Verify()
        {
            ValidatePrefabs();
            ValidateSidestepDodgesLockedLunge();
            ValidateLungeHitsOnceAndStops();
            ValidateKnockbackCancelsLunge();
            Debug.Log(
                "Melee-Lunge verification passed: Bulhyojason, Sansamo and the crayon axe minion telegraph from " +
                "outside touching range, track then lock their direction, lunge under their own velocity, hit once " +
                "inside the forward area, miss a sidestep and cancel on knockback; the shield and crumb minions keep " +
                "the stationary swing.");
        }

        private static void ValidatePrefabs()
        {
            foreach (string path in Week22MeleeLungeSetup.LungePrefabPaths)
            {
                MeleeEnemyAttack melee = LoadMelee(path);
                Assert(melee.IsLunge &&
                       Mathf.Approximately(melee.LungeTriggerRange, Week22MeleeLungeSetup.TriggerRange) &&
                       Mathf.Approximately(melee.LungeDistance, Week22MeleeLungeSetup.Distance) &&
                       Mathf.Approximately(melee.AimLockLeadTime, Week22MeleeLungeSetup.AimLockLeadTime) &&
                       Mathf.Approximately(melee.LungeHitForwardOffset, Week22MeleeLungeSetup.HitForwardOffset) &&
                       Mathf.Approximately(melee.LungeHitRadius, Week22MeleeLungeSetup.HitRadius),
                    $"{path} must use the authored Melee-Lunge values. Run the Melee Lunge setup.");
                Assert(melee.LungeTriggerRange > melee.AttackRange,
                    $"{path} must start its telegraph from outside the stationary swing range.");
                Assert(melee.LungeReach > melee.LungeTriggerRange,
                    $"{path} must reach past the distance it starts from, or stepping back always dodges.");
                Assert(melee.AimLockLeadTime > 0f && melee.AimLockLeadTime < melee.TelegraphDuration,
                    $"{path} must both follow the target and leave a fixed direction to dodge.");
            }

            foreach (string path in Week22MeleeLungeSetup.StationaryPrefabPaths)
            {
                Assert(!LoadMelee(path).IsLunge, $"{path} must keep the stationary swing.");
            }
        }

        private static MeleeEnemyAttack LoadMelee(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, $"Missing melee enemy prefab at {path}.");
            MeleeEnemyAttack melee = prefab.GetComponent<MeleeEnemyAttack>();
            Assert(melee != null, $"{path} must have a MeleeEnemyAttack.");
            return melee;
        }

        private static void ValidateSidestepDodgesLockedLunge()
        {
            GameObject root = new("Melee-Lunge Dodge Verification");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body, out EnemyChase chase,
                out MeleeEnemyAttack melee, out _);

            try
            {
                chase.Configure(2.25f, 6f, 0.8f);
                melee.Configure(1.15f, TelegraphDuration, ActiveDuration, 0.65f, 0.2f, EnemyDamageTier.Medium);
                chase.SetTarget(player.transform);
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                player.transform.position = Vector2.right * 1.7f;

                melee.TickAttack(0f);
                Assert(melee.State == MeleeEnemyAttackState.Idle,
                    "A stationary swing must not start from outside its attack range.");

                ConfigureLunge(melee);
                melee.TickAttack(0f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Telegraph && body.linearVelocity == Vector2.zero &&
                       melee.LockedDirection == Vector2.zero,
                    "A lunge must start a stationary, not yet locked telegraph from its trigger range.");

                player.transform.position = Vector2.up * 1.7f;
                melee.TickAttack(0.2f);
                Assert(melee.State == MeleeEnemyAttackState.Telegraph && melee.LockedDirection == Vector2.zero,
                    "The telegraph must keep following the target before the lock time.");

                melee.TickAttack(0.3f);
                Assert(Vector2.Distance(melee.LockedDirection, Vector2.up) < 0.001f,
                    "The telegraph must lock onto the target's latest direction shortly before it ends.");

                float healthBefore = playerHealth.CurrentHealth;
                player.transform.position = Vector2.right * 1.7f;
                melee.TickAttack(TelegraphDuration);
                float lungeSpeed = Week22MeleeLungeSetup.Distance / ActiveDuration;
                Assert(melee.State == MeleeEnemyAttackState.Active &&
                       Vector2.Distance(melee.LockedDirection, Vector2.up) < 0.001f &&
                       Vector2.Distance(body.linearVelocity, Vector2.up * lungeSpeed) < 0.01f,
                    "The active phase must lunge along the locked direction, not toward the moved target.");

                chase.TickChase();
                Assert(Vector2.Distance(body.linearVelocity, Vector2.up * lungeSpeed) < 0.01f,
                    "Suppressed chase movement must not stop a lunge in progress.");

                enemy.transform.position = Vector2.up * 0.75f;
                melee.TickAttack(0.46f);
                Assert(melee.State == MeleeEnemyAttackState.Active &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore),
                    "A target that stepped out of the locked line must not be hit.");

                melee.TickAttack(0.52f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Recovery && body.linearVelocity == Vector2.zero &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore),
                    "A missed lunge must end in a stationary recovery window.");

                melee.TickAttack(1.17f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Idle && melee.LockedDirection == Vector2.zero &&
                       body.linearVelocity.sqrMagnitude > 0f,
                    "After recovery the lunging enemy must release the lock and resume chase.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateLungeHitsOnceAndStops()
        {
            GameObject root = new("Melee-Lunge Hit Verification");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body, out EnemyChase chase,
                out MeleeEnemyAttack melee, out _);

            try
            {
                melee.Configure(1.15f, TelegraphDuration, ActiveDuration, 0.65f, 0.2f, EnemyDamageTier.Medium);
                ConfigureLunge(melee);
                chase.SetTarget(player.transform);
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                player.transform.position = Vector2.right * 1.7f;

                float healthBefore = playerHealth.CurrentHealth;
                DamageSourceType appliedType = DamageSourceType.Unknown;
                playerHealth.DamageApplied += (context, applied, remaining) =>
                {
                    if (applied > 0f)
                    {
                        appliedType = context.SourceType;
                    }
                };

                melee.TickAttack(0f);
                melee.TickAttack(0.3f);
                melee.TickAttack(TelegraphDuration);
                Assert(melee.State == MeleeEnemyAttackState.Active && body.linearVelocity.x > 0f &&
                       Mathf.Approximately(playerHealth.CurrentHealth, healthBefore),
                    "A target beyond the forward hit area must not be hit before the lunge closes the distance.");

                enemy.transform.position = Vector2.right * 0.8f;
                melee.TickAttack(0.44f);
                Assert(melee.State == MeleeEnemyAttackState.Active &&
                       playerHealth.CurrentHealth < healthBefore && appliedType == DamageSourceType.EnemyMelee &&
                       body.linearVelocity == Vector2.zero,
                    "The lunge must apply one melee hit when its forward area reaches the target, then stop.");

                float healthAfterHit = playerHealth.CurrentHealth;
                Assert(!melee.TryResolveActiveHit() && Mathf.Approximately(playerHealth.CurrentHealth, healthAfterHit),
                    "One lunge must never damage the same target twice.");

                melee.TickAttack(0.52f);
                Assert(melee.State == MeleeEnemyAttackState.Recovery && body.linearVelocity == Vector2.zero,
                    "A lunge that hit must still end in a stationary recovery window.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateKnockbackCancelsLunge()
        {
            GameObject root = new("Melee-Lunge Cancellation Verification");
            GameObject player = CreatePlayer(root.transform, out _);
            GameObject enemy = CreateEnemy(root.transform, out Rigidbody2D body, out EnemyChase chase,
                out MeleeEnemyAttack melee, out KnockbackReceiver knockback);

            try
            {
                melee.Configure(1.15f, TelegraphDuration, ActiveDuration, 0.65f, 0.2f, EnemyDamageTier.Medium);
                ConfigureLunge(melee);
                chase.SetTarget(player.transform);
                enemy.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                player.transform.position = Vector2.right * 1.7f;

                melee.TickAttack(0f);
                melee.TickAttack(0.3f);
                melee.TickAttack(TelegraphDuration);
                Assert(melee.State == MeleeEnemyAttackState.Active && body.linearVelocity.x > 0f,
                    "Cancellation verification requires a lunge in progress.");

                Assert(knockback.Apply(Vector2.left, 5f, 0.2f, 0.2f), "The lunging enemy must accept knockback.");
                melee.TickAttack(0.42f);
                chase.TickChase();
                Assert(melee.State == MeleeEnemyAttackState.Idle && melee.LockedDirection == Vector2.zero &&
                       body.linearVelocity.x < 0f,
                    "Knockback must cancel the lunge without the lunge or chase overwriting knockback velocity.");

                knockback.Stop();
                InvokeLifecycle(melee, "OnDisable");
                Assert(melee.State == MeleeEnemyAttackState.Idle && body.linearVelocity == Vector2.zero,
                    "Room disable must clear lunge movement.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureLunge(MeleeEnemyAttack melee)
        {
            melee.ConfigureLunge(
                Week22MeleeLungeSetup.TriggerRange,
                Week22MeleeLungeSetup.Distance,
                Week22MeleeLungeSetup.AimLockLeadTime,
                Week22MeleeLungeSetup.HitForwardOffset,
                Week22MeleeLungeSetup.HitRadius);
        }

        private static GameObject CreatePlayer(Transform parent, out Health health)
        {
            GameObject player = new("Melee-Lunge Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
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
            out EnemyChase chase,
            out MeleeEnemyAttack melee,
            out KnockbackReceiver knockback)
        {
            GameObject enemy = new("Melee-Lunge Chaser");
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
            EnemyAttackPresentation presentation = enemy.AddComponent<EnemyAttackPresentation>();
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
