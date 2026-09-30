using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss0Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Boss-0 Common Runtime")]
        public static void Verify()
        {
            ValidatePrefabAndBossRooms();
            ValidatePatternLifecycleAndSeed();
            ValidatePhaseAndCleanup();
            Week13Hud5Verification.Verify();
            Debug.Log("Week 15 Boss-0 verification passed: the large boss follows deterministic " +
                      "telegraph-active-recovery-cooldown timing, updates its configured HUD phases, fits every " +
                      "boss room, and removes owned projectiles on death or deactivation.");
        }

        public static void SetupAndVerifyBatch()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week15Boss0Setup.BossPrefabPath);
            Week15Boss0Setup.Setup();
            Week15Boss0Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(prefabGuid) &&
                   prefabGuid == AssetDatabase.AssetPathToGUID(Week15Boss0Setup.BossPrefabPath),
                "Boss-0 setup changed the TestBoss prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabAndBossRooms()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss0Setup.BossPrefabPath);
            BossController boss = prefab != null ? prefab.GetComponent<BossController>() : null;
            SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
            CircleCollider2D collider = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
            Assert(boss != null && renderer != null && renderer.sprite != null && collider != null &&
                   prefab.GetComponent<EnemyAttackPresentation>() != null,
                "Boss-0 prefab is missing its controller, visible sprite, collider, or phase presentation.");
            Assert(boss.PhaseCount >= 1 && boss.Patterns.Count == 3 &&
                   boss.Patterns.Select(pattern => pattern.PatternId).Distinct(StringComparer.Ordinal).Count() == 3,
                "Boss-0 prefab must expose at least one HUD phase and three stable common pattern IDs.");
            Assert(prefab.transform.localScale.x >= 1.5f &&
                   collider.radius * 2f < Mathf.Min(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y),
                "Boss-0 must read larger than the player while keeping its hit collider inside the visual silhouette.");

            float maximumCollisionDiameter = collider.radius * 2f * prefab.transform.localScale.x * 1.3f;
            foreach (string path in new[]
                     {
                         Week14Room6Setup.BossFloor1ProfilePath,
                         Week14Room6Setup.BossFloor2ProfilePath,
                         Week14Room6Setup.BossFloor3ProfilePath,
                     })
            {
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(path);
                Assert(profile != null, $"Missing Boss-0 room profile at {path}.");
                Assert(profile.TryValidate(out string error), error);
                Assert(maximumCollisionDiameter < Mathf.Min(profile.MovementBounds.width,
                           profile.MovementBounds.height) * 0.45f,
                    $"Boss-0 collision silhouette is too large for room profile '{profile.ProfileId}'.");
            }
        }

        private static void ValidatePatternLifecycleAndSeed()
        {
            BossPatternDefinition[] definitions =
            {
                Pattern("one"), Pattern("two"), Pattern("three"),
            };
            List<string> first = SampleSequence(714, definitions);
            List<string> repeat = SampleSequence(714, definitions);
            List<string> other = SampleSequence(715, definitions);
            Assert(first.SequenceEqual(repeat), "The same content seed must reproduce the same boss pattern order.");
            Assert(!first.SequenceEqual(other), "Different content seeds must be able to vary boss pattern order.");
            Assert(first.Zip(first.Skip(1), (left, right) => left != right).All(value => value),
                "Boss-0 must avoid an immediate pattern repeat while another pattern is reusable.");

            GameObject root = CreateRuntimeBoss("Timing Boss", out BossController boss, out _);
            GameObject target = new("Timing Target");
            try
            {
                boss.ConfigurePatterns(new[] { Pattern("only") });
                boss.BeginCombat(target.transform, 99, 0f);
                Assert(boss.State == BossActionState.Telegraph && boss.CurrentPatternId == "only",
                    "A selected pattern must begin in Telegraph.");
                boss.TickBehavior(0.099f);
                Assert(boss.State == BossActionState.Telegraph, "Telegraph ended before its configured duration.");
                boss.TickBehavior(0.1f);
                Assert(boss.State == BossActionState.Active, "Telegraph must transition to Active.");
                boss.TickBehavior(0.2f);
                Assert(boss.State == BossActionState.Recovery, "Active must transition to Recovery.");
                boss.TickBehavior(0.3f);
                Assert(boss.State == BossActionState.Cooldown, "Recovery must enforce pattern reuse cooldown.");
                boss.TickBehavior(0.79f);
                Assert(boss.State == BossActionState.Cooldown, "Pattern was reused before its cooldown elapsed.");
                boss.TickBehavior(0.8f);
                Assert(boss.State == BossActionState.Telegraph, "Pattern did not become reusable after cooldown.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidatePhaseAndCleanup()
        {
            GameObject root = CreateRuntimeBoss("Lifecycle Boss", out BossController boss, out Health health);
            GameObject target = new("Lifecycle Target");
            try
            {
                boss.ConfigureHud("검증 보스", 3);
                boss.ConfigurePatterns(new[] { Pattern("cleanup") });
                health.TakeDamage(4f);
                Assert(boss.CurrentPhase == 2, "Boss HP crossing the first threshold must update HUD phase 2.");
                health.TakeDamage(4f);
                Assert(boss.CurrentPhase == 3, "Boss HP crossing the second threshold must update HUD phase 3.");

                GameObject summon = new("Owned Summon");
                boss.RegisterOwnedObject(summon);
                Assert(boss.OwnedObjectCount == 1, "Boss-0 did not register its owned summon.");
                health.TakeDamage(100f);
                Assert(boss.State == BossActionState.Defeated && boss.OwnedObjectCount == 0 && summon == null,
                    "Boss death must cancel its pattern and remove every owned summon/projectile.");

                GameObject disabledOwned = new("Disabled Owned Summon");
                health.ResetHealth();
                boss.RegisterOwnedObject(disabledOwned);
                Invoke(boss, "OnDisable");
                Assert(boss.State == BossActionState.Idle && boss.OwnedObjectCount == 0 && disabledOwned == null,
                    "Room deactivation must cancel Boss-0 and remove every owned summon/projectile.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static List<string> SampleSequence(int seed, BossPatternDefinition[] patterns)
        {
            GameObject root = CreateRuntimeBoss($"Seed Boss {seed}", out BossController boss, out _);
            GameObject target = new($"Seed Target {seed}");
            try
            {
                boss.ConfigurePatterns(patterns);
                boss.BeginCombat(target.transform, seed, 0f);
                List<string> result = new();
                for (int index = 0; index < 8; index++)
                {
                    result.Add(boss.CurrentPatternId);
                    boss.TickBehavior((index + 1) * 0.31f);
                }
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BossPatternDefinition Pattern(string id) =>
            new(id, BossPatternExecution.SignalOnly, 0.1f, 0.1f, 0.1f, 0.5f);

        private static GameObject CreateRuntimeBoss(string name, out BossController boss, out Health health)
        {
            GameObject root = new(name);
            health = root.AddComponent<Health>();
            root.AddComponent<KnockbackReceiver>();
            boss = root.AddComponent<BossController>();
            Invoke(health, "Awake");
            Invoke(boss, "Awake");
            return root;
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, $"Missing verification hook {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
