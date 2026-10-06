using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGCombatFoundationVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase G-2 Combat Foundation")]
        public static void Verify()
        {
            GameObject player = CreatePlayer(out Health playerHealth, out PlayerStats stats, out PlayerSP playerSP);
            GameObject target = CreateHealthObject("Phase G-2 Target", out Health targetHealth);
            DamageCalculator.SetCriticalRollProviderForTesting(() => 0f);

            try
            {
                VerifyStatsAndCriticalDamage(player, stats);
                VerifyDirectAndPeriodicDamage(player, targetHealth);
                VerifyShield(playerHealth, stats, targetHealth);
                VerifyMaximumSP(playerSP);

                Debug.Log(
                    "Phase G-2 combat foundation verification passed: additive attack/attack-speed percentages, " +
                    "5%/150% critical defaults and controlled boundaries, direct/periodic critical separation, " +
                    "shield-first float damage with events, persistent shield across max-HP changes, and maximum-SP UI events are valid.");
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void VerifyStatsAndCriticalDamage(GameObject player, PlayerStats stats)
        {
            Assert(Approximately(stats.AttackDamage, 10f) && Approximately(stats.AttackSpeed, 1f),
                "PlayerStats must start with attack damage 10 (HP-5 enemy scale) and attack speed 1.");
            Assert(Approximately(stats.CriticalChance, 0.05f) &&
                   Approximately(stats.CriticalDamageMultiplier, 1.5f),
                "PlayerStats must own the 5% critical chance and 150% critical damage defaults.");

            stats.AddAttackDamagePercent(0.05f);
            stats.AddAttackDamagePercent(0.15f);
            stats.AddAttackSpeedPercent(0.10f);
            stats.AddAttackSpeedPercent(0.10f);
            stats.AddCriticalChance(0.03f);
            stats.AddCriticalChance(0.03f);

            Assert(Approximately(stats.AttackDamage, 12f),
                "Attack percentages must add before multiplying base attack damage.");
            Assert(Approximately(stats.AttackSpeed, 1.20f),
                "Attack-speed percentages must add before multiplying base attack speed.");
            Assert(Approximately(stats.CriticalChance, 0.11f),
                "Critical chance bonuses must add as percentage points to the 5% base chance.");

            DamageContext context = stats.CreateDirectDamageContext(
                player,
                DamageSourceType.PlayerProjectile);
            DamageResult critical = DamageCalculator.Resolve(context, 0.109f);
            DamageResult normal = DamageCalculator.Resolve(context, 0.11f);
            Assert(critical.IsCritical && Approximately(critical.FinalDamage, 18f),
                "A roll below critical chance must apply the 150% multiplier without rounding.");
            Assert(!normal.IsCritical && Approximately(normal.FinalDamage, 12f),
                "A roll on the critical chance boundary must remain a normal hit.");
        }

        private static void VerifyDirectAndPeriodicDamage(GameObject player, Health target)
        {
            DamageContext direct = new(
                player,
                DamageSourceType.PlayerProjectile,
                4f,
                1f,
                DamageDeliveryType.Direct,
                1f,
                1.5f);
            DamageContext periodic = new(
                player,
                DamageSourceType.PlayerAttack,
                4f,
                1f,
                DamageDeliveryType.Periodic,
                1f,
                1.5f);

            DamageResult directResult = DamageCalculator.Resolve(direct, 0f);
            DamageResult periodicResult = DamageCalculator.Resolve(periodic, 0f);
            Assert(direct.CanCritical && directResult.IsCritical && Approximately(directResult.FinalDamage, 6f),
                "Direct damage must allow controlled critical hits.");
            Assert(!periodic.CanCritical && !periodicResult.IsCritical && Approximately(periodicResult.FinalDamage, 4f),
                "Periodic damage must ignore critical chance even when configured with 100% chance.");

            DamageResult resolved = default;
            int resolvedCount = 0;
            target.DamageResolved += (_, result) =>
            {
                resolved = result;
                resolvedCount++;
            };
            target.TakeDamage(new DamageContext(
                player,
                DamageSourceType.PlayerProjectile,
                1f,
                criticalChance: 1f,
                criticalDamageMultiplier: 1.5f,
                criticalRollOverride: 0f));
            Assert(resolvedCount == 1 && resolved.IsCritical && Approximately(resolved.FinalDamage, 1.5f),
                "Health must use the same controlled critical resolution path as DamageCalculator.");
            target.ResetHealth();
        }

        private static void VerifyShield(Health playerHealth, PlayerStats stats, Health target)
        {
            int shieldChangeCount = 0;
            int healthDamageCount = 0;
            float lastHealthDamage = -1f;
            target.ShieldChanged += _ => shieldChangeCount++;
            target.DamageApplied += (_, damage, _) =>
            {
                healthDamageCount++;
                lastHealthDamage = damage;
            };

            Assert(target.SetShield(5f) && Approximately(target.CurrentShield, 5f),
                "Shield must accept positive float values.");
            target.SetInvulnerable(true);
            target.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 10f));
            Assert(Approximately(target.CurrentShield, 5f) && Approximately(target.CurrentHealth, 10f),
                "Invulnerability must prevent both shield and HP damage.");
            target.SetInvulnerable(false);

            target.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 6f));
            Assert(Approximately(target.CurrentShield, 0f) && Approximately(target.CurrentHealth, 9f),
                "Shield must absorb damage before only the remainder reaches HP.");
            Assert(shieldChangeCount == 2 && healthDamageCount == 1 && Approximately(lastHealthDamage, 1f),
                "Shield and health damage events must report exactly one effective transition each.");

            Assert(target.AddShield(2.5f), "Shield must support additive float gains.");
            target.TakeDamage(new DamageContext(
                null,
                DamageSourceType.Unknown,
                1.25f,
                deliveryType: DamageDeliveryType.Periodic));
            Assert(Approximately(target.CurrentShield, 1.25f) && Approximately(target.CurrentHealth, 9f),
                "A fully absorbed periodic tick must not change HP or round shield values.");
            Assert(shieldChangeCount == 4 && healthDamageCount == 2 && Approximately(lastHealthDamage, 0f),
                "A shield-only hit must publish shield and resolved damage without a false HP loss.");

            playerHealth.SetShield(3f);
            stats.AddMaxHealth(2f, true);
            Assert(Approximately(playerHealth.CurrentShield, 3f) && Approximately(playerHealth.MaxHealth, 12f),
                "Changing max HP must not clear or refill an existing shield.");
        }

        private static void VerifyMaximumSP(PlayerSP playerSP)
        {
            int changedCount = 0;
            int lastCurrent = -1;
            int lastMaximum = -1;
            playerSP.Changed += (current, maximum) =>
            {
                changedCount++;
                lastCurrent = current;
                lastMaximum = maximum;
            };

            Assert(playerSP.CurrentSP == 0 && playerSP.MaxSP == 3,
                "PlayerSP must retain its default 0/3 state.");
            Assert(playerSP.AddMaxSP() && playerSP.CurrentSP == 0 && playerSP.MaxSP == 4,
                "Increasing maximum SP must leave current SP unchanged.");
            Assert(changedCount == 1 && lastCurrent == 0 && lastMaximum == 4,
                "A maximum-SP change must publish current and maximum values for UI consumers.");
            Assert(playerSP.TryAdd(4) && playerSP.CurrentSP == 4,
                "The expanded capacity must immediately accept additional SP.");
            Assert(playerSP.AddMaxSP(2) && playerSP.CurrentSP == 4 && playerSP.MaxSP == 6,
                "Increasing capacity while full must not grant free current SP.");
            Assert(!playerSP.AddMaxSP(0) && changedCount == 3,
                "Invalid maximum-SP changes must not publish UI events.");
            Assert(lastCurrent == 4 && lastMaximum == 6,
                "The latest UI event must expose the expanded 4/6 state.");
        }

        private static GameObject CreatePlayer(out Health health, out PlayerStats stats, out PlayerSP playerSP)
        {
            GameObject player = new("Phase G-2 Player");
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            playerSP = player.AddComponent<PlayerSP>();
            InvokeAwake(health);
            InvokeAwake(stats);
            InvokeAwake(playerSP);
            return player;
        }

        private static GameObject CreateHealthObject(string objectName, out Health health)
        {
            GameObject target = new(objectName);
            health = target.AddComponent<Health>();
            InvokeAwake(health);
            return target;
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
        }

        private static bool Approximately(float left, float right) => Mathf.Approximately(left, right);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
