using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Tune1Setup
    {
        // Two floor-1 hits at the base attack damage of 10.
        public const float QuickRangedMaxHealth = 20f;
        // Light is one heart on floor 1.
        public const EnemyDamageTier QuickRangedProjectileDamageTier = EnemyDamageTier.Light;
        // Raised from 7; the final value is tuned by play feel.
        public const float SniperProjectileSpeed = 11f;
        public const float PreviousSniperProjectileSpeed = 7f;
        // Slightly raised from 8; dash distance stays below half of the 24-unit Large room.
        public const float ChargingDashSpeed = 9.5f;
        public const float PreviousChargingDashSpeed = 8f;

        [MenuItem("Trickal Fan Game/Week 19/Setup Tune-1 Ranged Enemy Balance")]
        public static void Setup()
        {
            ConfigureQuickRanged();
            ConfigureSniper();
            ConfigureCharging();
            AssetDatabase.SaveAssets();
            Debug.Log($"Tune-1 ready: quick ranged HP {QuickRangedMaxHealth} with " +
                      $"{QuickRangedProjectileDamageTier} projectiles, sniper projectile speed " +
                      $"{PreviousSniperProjectileSpeed} -> {SniperProjectileSpeed}, charging dash speed " +
                      $"{PreviousChargingDashSpeed} -> {ChargingDashSpeed}.");
        }

        private static void ConfigureQuickRanged()
        {
            GameObject root = LoadContents(Week19Spawn2Setup.QuickRangedPrefabPath);
            try
            {
                MobileRangedEnemyController controller = root.GetComponent<MobileRangedEnemyController>();
                Health health = root.GetComponent<Health>();
                if (controller == null || health == null)
                {
                    throw new InvalidOperationException("Tune-1 requires the Spawn-2 quick ranged Prefab.");
                }

                SerializedObject serializedController = new(controller);
                serializedController.FindProperty("projectileDamageTier").enumValueIndex =
                    (int)QuickRangedProjectileDamageTier;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                SetMaxHealth(health, QuickRangedMaxHealth);
                Save(root, Week19Spawn2Setup.QuickRangedPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureSniper()
        {
            GameObject root = LoadContents(Week15Enemy4Setup.SniperPrefabPath);
            try
            {
                LongRangeSniperController sniper = root.GetComponent<LongRangeSniperController>();
                if (sniper == null)
                {
                    throw new InvalidOperationException("Tune-1 requires the Enemy-4 sniper Prefab.");
                }

                SerializedObject serializedSniper = new(sniper);
                serializedSniper.FindProperty("projectileSpeed").floatValue = SniperProjectileSpeed;
                serializedSniper.ApplyModifiedPropertiesWithoutUndo();
                Save(root, Week15Enemy4Setup.SniperPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCharging()
        {
            GameObject root = LoadContents(Week15Enemy0Setup.ChargingPrefabPath);
            try
            {
                ChargingEnemyController charging = root.GetComponent<ChargingEnemyController>();
                if (charging == null)
                {
                    throw new InvalidOperationException("Tune-1 requires the charging enemy Prefab.");
                }

                SerializedObject serializedCharging = new(charging);
                serializedCharging.FindProperty("dashSpeed").floatValue = ChargingDashSpeed;
                serializedCharging.ApplyModifiedPropertiesWithoutUndo();
                Save(root, Week15Enemy0Setup.ChargingPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject LoadContents(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException($"Tune-1 is missing Prefab '{path}'.");
            }

            return PrefabUtility.LoadPrefabContents(path);
        }

        private static void SetMaxHealth(Health health, float value)
        {
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = value;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject root, string path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
            {
                throw new InvalidOperationException($"Tune-1 could not save Prefab '{path}'.");
            }
        }
    }
}
