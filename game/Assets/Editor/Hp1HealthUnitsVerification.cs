using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Hp1HealthUnitsVerification
    {
        private const string BalloonArmorPath = "Assets/Items/item-02.asset";

        [MenuItem("Trickal Fan Game/HP/Verify HP-1 Health Units")]
        public static void Verify()
        {
            ValidateUnitTable();
            ValidatePlayerDamageAndHealing();
            ValidateTieredEnemyDamage();
            ValidateBalloonArmor();
            ValidateEnemyHealthStaysContinuous();
            Debug.Log("HP-1 health unit verification passed: the player starts at 10 half-heart units, " +
                      "enemy damage resolves through tier/floor units with a one-heart minimum, heals and " +
                      "shields floor to whole units, max-health ratios keep a one-unit minimum, item-02 adds " +
                      "one heart with a three-heart shield, and enemy Health keeps continuous values.");
        }

        public static void VerifyRegressionBatch()
        {
            (string Name, Action Run)[] checks =
            {
                ("HP-1", Verify),
                // Week8 full-run and Enemy-2 legacy-boss sections depend on unrelated scene/prefab setup state,
                // so only the sections that exercise HP-1 damage tiers run here.
                ("Week8FloorDifficulty tiers", () => RunSections(typeof(Week8FloorDifficultyVerification),
                    "VerifyMultiplierValues", "VerifyChaserScaling", "VerifyRangedScaling",
                    "VerifyChargingScaling", "VerifyBossScaling")),
                ("Week7EnemyBalance", Week7EnemyBalanceVerification.Verify),
                ("Week7ChargingEnemy", Week7ChargingEnemyVerification.Verify),
                ("Week7RangedEnemy", Week7RangedEnemyVerification.Verify),
                ("Week7RoomGraph", Week7RoomGraphVerification.Verify),
                ("Week7DamageContext", Week7DamageContextVerification.Verify),
                ("Week7PlayerCombatEvents", Week7PlayerCombatEventsVerification.Verify),
                ("Week8ProjectileSizing", Week8ProjectileSizingVerification.Verify),
                ("Week15Enemy1", Week15Enemy1Verification.Verify),
                ("Week15Enemy2 roles/lifecycle", () => RunSections(typeof(Week15Enemy2Verification),
                    "ValidatePrefabRoles", "ValidateMeleeLifecycleAndDamageSharing", "ValidateMeleeCancellation")),
                ("Week15Enemy3", Week15Enemy3Verification.Verify),
                ("Week15Enemy4", Week15Enemy4Verification.Verify),
                ("Week15Boss1", Week15Boss1Verification.Verify),
                ("Week15Boss2", Week15Boss2Verification.Verify),
                ("Week15Boss3", Week15Boss3Verification.Verify),
                ("PhaseGCombatFoundation", PhaseGCombatFoundationVerification.Verify),
                ("PhaseGConditionalEffects", PhaseGConditionalEffectsVerification.Verify),
                ("PhaseGSimpleEffects", PhaseGSimpleEffectsVerification.Verify),
                ("PhaseGDamageAura", PhaseGDamageAuraVerification.Verify),
                ("PhaseGRewardRun", PhaseGRewardRunVerification.Verify),
                ("Week13Hud1", Week13Hud1Verification.Verify),
                ("Week13Hud3B", Week13Hud3BVerification.Verify),
                ("Week16Reward1", Week16Reward1Verification.Verify),
                ("Week16Reward2", Week16Reward2Verification.Verify),
            };

            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    run();
                    Debug.Log($"[HP-1 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[HP-1 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"HP-1 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log($"[HP-1 regression] All {checks.Length} checks passed.");
        }

        private static void RunSections(Type verifier, params string[] methodNames)
        {
            foreach (string methodName in methodNames)
            {
                MethodInfo method = verifier.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
                Assert(method != null, $"{verifier.Name}.{methodName} was not found.");
                method.Invoke(null, null);
            }
        }

        private static void ValidateUnitTable()
        {
            Assert(HealthUnits.UnitsPerHeart == 2 && HealthUnits.MinimumEnemyDamageUnits == 2,
                "One heart must be two units and the minimum enemy damage must be one heart.");
            Assert(HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1) == 2 &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 3) == 3 &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 2) == 4 &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Heavy, 3) == 7 &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Critical, 1) == 6,
                "Tier/floor units do not match the confirmed heart table.");
            Assert(HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 0) ==
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1),
                "Floors below 1 must fall back to the floor 1 row.");
            Assert(HealthUnits.FromMaxHealthRatio(10f, 0.25f) == 2 &&
                   HealthUnits.FromMaxHealthRatio(10f, 0.05f) == 1 &&
                   HealthUnits.FromMaxHealthRatio(10f, 0.01f) == 1 &&
                   HealthUnits.FromMaxHealthRatio(12f, 0.5f) == 6 &&
                   HealthUnits.FromMaxHealthRatio(10f, 0f) == 0,
                "Max-health ratio results must floor to whole units with a one-unit minimum.");
            Assert(HealthUnits.FormatHearts(9f) == "4.5" && HealthUnits.FormatHearts(10f) == "5",
                "Heart formatting must show half hearts only when needed.");
        }

        private static void ValidatePlayerDamageAndHealing()
        {
            GameObject player = CreatePlayer(out Health health, out PlayerStats stats, out _);
            try
            {
                Assert(health.UsesHealthUnits && Mathf.Approximately(health.MaxHealth, 10f) &&
                       Mathf.Approximately(health.CurrentHealth, 10f) &&
                       Mathf.Approximately(health.MaxHealthHearts, 5f),
                    "PlayerStats must enable unit mode and start the player at 10 units (5 hearts).");

                health.TakeDamage(0.3f);
                Assert(Mathf.Approximately(health.CurrentHealth, 8f),
                    "Any positive damage to the player must remove at least one heart.");
                health.TakeDamage(2.2f);
                Assert(Mathf.Approximately(health.CurrentHealth, 5f),
                    "Fractional player damage above the minimum must round up to whole units.");

                Assert(Mathf.Approximately(health.Heal(1.9f), 1f) && Mathf.Approximately(health.CurrentHealth, 6f),
                    "Player healing must floor to whole units.");
                Assert(Mathf.Approximately(health.Heal(0.9f), 0f) && Mathf.Approximately(health.CurrentHealth, 6f),
                    "Healing below one unit must not change player HP.");
                Assert(Mathf.Approximately(health.GetMaxHealthRatioAmount(0.25f), 2f) &&
                       Mathf.Approximately(health.GetMaxHealthRatioAmount(0.05f), 1f),
                    "Player max-health ratios must use the floored one-unit-minimum rule.");

                stats.AddHealOnKillMaxHealthPercent(0.05f);
                Assert(Mathf.Approximately(stats.HealOnKillAmount, 1f),
                    "A 5% heal-on-kill on 10 units must heal one unit.");

                Assert(health.SetShield(2.7f) && Mathf.Approximately(health.CurrentShield, 2f),
                    "Player shields must floor to whole units.");
                health.TakeDamage(0.3f);
                Assert(Mathf.Approximately(health.CurrentShield, 0f) && Mathf.Approximately(health.CurrentHealth, 6f),
                    "A one-heart minimum hit must be fully absorbed by a two-unit shield.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateTieredEnemyDamage()
        {
            GameObject player = CreatePlayer(out Health health, out _, out _);
            GameObject enemy = new("HP-1 Tiered Enemy");
            try
            {
                EnemyFloorLevel.Apply(enemy, 3);
                health.TakeDamage(HealthUnits.CreateEnemyDamageContext(
                    enemy, DamageSourceType.EnemyContact, EnemyDamageTier.Light));
                Assert(Mathf.Approximately(health.CurrentHealth, 7f),
                    "A floor 3 light hit must remove 1.5 hearts (3 units).");

                EnemyFloorLevel.Apply(enemy, 1);
                health.TakeDamage(HealthUnits.CreateEnemyDamageContext(
                    enemy, DamageSourceType.EnemyMelee, EnemyDamageTier.Medium));
                Assert(Mathf.Approximately(health.CurrentHealth, 4f),
                    "A floor 1 medium hit must remove 1.5 hearts (3 units).");

                Assert(EnemyFloorLevel.Resolve(null) == 1,
                    "Sources without a floor level must resolve as floor 1.");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateBalloonArmor()
        {
            ItemDefinition armor = AssetDatabase.LoadAssetAtPath<ItemDefinition>(BalloonArmorPath);
            Assert(armor != null && armor.IsValid, $"Missing valid balloon armor at {BalloonArmorPath}.");
            GameObject player = CreatePlayer(out Health health, out _, out PlayerInventory inventory);
            try
            {
                Assert(inventory.TryAcquire(armor), "Balloon armor must be acquirable.");
                Assert(Mathf.Approximately(health.MaxHealth, 12f) && Mathf.Approximately(health.CurrentHealth, 12f),
                    "Balloon armor must add one heart (2 units) of maximum and current HP.");
                Assert(Mathf.Approximately(health.CurrentShield, 6f),
                    "Balloon armor must grant a 50% shield of 6 units (3 hearts).");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateEnemyHealthStaysContinuous()
        {
            GameObject enemy = new("HP-1 Continuous Enemy");
            try
            {
                Health health = enemy.AddComponent<Health>();
                InvokeLifecycle(health, "Awake");
                health.TakeDamage(0.3f);
                Assert(!health.UsesHealthUnits && Mathf.Approximately(health.CurrentHealth, 9.7f),
                    "Enemy Health must keep continuous damage values.");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        private static GameObject CreatePlayer(out Health health, out PlayerStats stats,
            out PlayerInventory inventory)
        {
            GameObject player = new("HP-1 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            inventory = player.GetComponent<PlayerInventory>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
