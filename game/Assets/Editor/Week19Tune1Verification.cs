using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Tune1Verification
    {
        // HP-5 verifies that the player's default attack damage is 10.
        private const float PlayerAttackDamage = 10f;
        private const float SniperHealth = 30f;

        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Tune-1 Ranged Enemy Balance")]
        public static void SetupAndVerifyBatch()
        {
            string quickGuid = AssetDatabase.AssetPathToGUID(Week19Spawn2Setup.QuickRangedPrefabPath);
            string sniperGuid = AssetDatabase.AssetPathToGUID(Week15Enemy4Setup.SniperPrefabPath);
            string chargingGuid = AssetDatabase.AssetPathToGUID(Week15Enemy0Setup.ChargingPrefabPath);
            Week19Tune1Setup.Setup();
            Week19Tune1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(chargingGuid) &&
                   chargingGuid == AssetDatabase.AssetPathToGUID(Week15Enemy0Setup.ChargingPrefabPath) &&
                   !string.IsNullOrWhiteSpace(quickGuid) &&
                   quickGuid == AssetDatabase.AssetPathToGUID(Week19Spawn2Setup.QuickRangedPrefabPath) &&
                   !string.IsNullOrWhiteSpace(sniperGuid) &&
                   sniperGuid == AssetDatabase.AssetPathToGUID(Week15Enemy4Setup.SniperPrefabPath),
                "Tune-1 setup changed or lost a ranged enemy Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Tune-1 Ranged Enemy Balance")]
        public static void Verify()
        {
            GameObject quick = AssetDatabase.LoadAssetAtPath<GameObject>(Week19Spawn2Setup.QuickRangedPrefabPath);
            GameObject sniperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy4Setup.SniperPrefabPath);
            MobileRangedEnemyController quickController = quick != null
                ? quick.GetComponent<MobileRangedEnemyController>()
                : null;
            LongRangeSniperController sniper = sniperPrefab != null
                ? sniperPrefab.GetComponent<LongRangeSniperController>()
                : null;
            Health quickHealth = quick != null ? quick.GetComponent<Health>() : null;
            Health sniperHealth = sniperPrefab != null ? sniperPrefab.GetComponent<Health>() : null;
            Assert(quickController != null && sniper != null && quickHealth != null && sniperHealth != null,
                "Run Spawn-2 and Enemy-4 setup before Tune-1 verification.");

            ValidateQuickRanged(quickController, quickHealth);
            ValidateSniper(sniper, sniperHealth, quickController);
            ChargingEnemyController charging = ValidateCharging();
            Debug.Log($"Tune-1 verification passed: quick ranged HP {Week19Tune1Setup.QuickRangedMaxHealth} " +
                      $"dies to exactly two floor-1 hits of {PlayerAttackDamage}, its Light projectiles deal one " +
                      $"heart on floor 1, the sniper keeps HP {SniperHealth} and Heavy damage while its " +
                      $"projectile speed rises from {Week19Tune1Setup.PreviousSniperProjectileSpeed} to " +
                      $"{sniper.ProjectileSpeed}, and the charging dash speed rises from " +
                      $"{Week19Tune1Setup.PreviousChargingDashSpeed} to {charging.DashSpeed} " +
                      $"(dash distance {charging.DashSpeed * charging.DashDuration:0.##}).");
        }

        private static ChargingEnemyController ValidateCharging()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChargingPrefabPath);
            ChargingEnemyController charging = prefab != null
                ? prefab.GetComponent<ChargingEnemyController>()
                : null;
            Health health = prefab != null ? prefab.GetComponent<Health>() : null;
            Assert(charging != null && health != null, "Run Enemy-3 setup before Tune-1 verification.");
            Assert(Mathf.Approximately(charging.DashSpeed, Week19Tune1Setup.ChargingDashSpeed) &&
                   charging.DashSpeed > Week19Tune1Setup.PreviousChargingDashSpeed,
                $"Charging dash speed must be {Week19Tune1Setup.ChargingDashSpeed}, above the previous " +
                $"{Week19Tune1Setup.PreviousChargingDashSpeed}.");
            float dashDistance = charging.DashSpeed * charging.DashDuration;
            Assert(dashDistance >= 6f && dashDistance < 12f,
                "The tuned charging dash must stay within the Enemy-3 distance range of 6 to under 12.");
            Assert(charging.ChargeDamageTier == EnemyDamageTier.Heavy && Mathf.Approximately(health.MaxHealth, 70f),
                "Tune-1 must not change the charging HP or Heavy charge damage.");
            return charging;
        }

        private static void ValidateQuickRanged(MobileRangedEnemyController controller, Health prefabHealth)
        {
            Assert(!prefabHealth.UsesHealthUnits &&
                   Mathf.Approximately(prefabHealth.MaxHealth, Week19Tune1Setup.QuickRangedMaxHealth),
                $"Quick ranged HP must be {Week19Tune1Setup.QuickRangedMaxHealth}, got {prefabHealth.MaxHealth}.");
            Assert(controller.ProjectileDamageTier == Week19Tune1Setup.QuickRangedProjectileDamageTier,
                $"Quick ranged projectiles must use {Week19Tune1Setup.QuickRangedProjectileDamageTier} damage.");

            GameObject enemy = new("Tune-1 Quick Ranged Health");
            try
            {
                Health health = CreateHealth(enemy, prefabHealth.MaxHealth, false);
                FloorDifficultyScaler.ApplyScaling(enemy, 1);
                health.TakeDamage(PlayerAttackDamage);
                Assert(!health.IsDead, "Quick ranged must survive the first floor-1 base attack.");
                health.TakeDamage(PlayerAttackDamage);
                Assert(health.IsDead, "Quick ranged must die to the second floor-1 base attack.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }

            Assert(HealthUnits.GetEnemyDamageUnits(controller.ProjectileDamageTier, 1) == HealthUnits.UnitsPerHeart,
                "Quick ranged projectiles must deal exactly one heart on floor 1.");
            GameObject player = new("Tune-1 Player");
            GameObject source = new("Tune-1 Quick Ranged Projectile Source");
            try
            {
                Health health = CreateHealth(player, 10f, true);
                EnemyFloorLevel.Apply(source, 1);
                float before = health.CurrentHealth;
                health.TakeDamage(HealthUnits.CreateEnemyDamageContext(source, DamageSourceType.EnemyProjectile,
                    controller.ProjectileDamageTier));
                Assert(Mathf.Approximately(before - health.CurrentHealth, HealthUnits.UnitsPerHeart),
                    "A floor-1 quick ranged projectile must remove exactly one heart from the player.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void ValidateSniper(LongRangeSniperController sniper, Health health,
            MobileRangedEnemyController quick)
        {
            Assert(Mathf.Approximately(sniper.ProjectileSpeed, Week19Tune1Setup.SniperProjectileSpeed) &&
                   sniper.ProjectileSpeed > Week19Tune1Setup.PreviousSniperProjectileSpeed &&
                   sniper.ProjectileSpeed > quick.ProjectileSpeed,
                $"Sniper projectile speed must be {Week19Tune1Setup.SniperProjectileSpeed}, above the previous " +
                $"{Week19Tune1Setup.PreviousSniperProjectileSpeed} and the quick ranged {quick.ProjectileSpeed}.");
            Assert(sniper.ProjectileDamageTier == EnemyDamageTier.Heavy &&
                   Mathf.Approximately(health.MaxHealth, SniperHealth),
                "Tune-1 must not change the sniper HP or Heavy projectile damage.");
        }

        private static Health CreateHealth(GameObject owner, float maxHealth, bool useHealthUnits)
        {
            Health health = owner.AddComponent<Health>();
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.FindProperty("useHealthUnits").boolValue = useHealthUnits;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            health.ResetHealth();
            return health;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
