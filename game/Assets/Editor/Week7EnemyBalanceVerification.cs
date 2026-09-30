using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7EnemyBalanceVerification
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Verify Phase E-4 Enemy Balance")]
        public static void Verify()
        {
            EnemyProfile chaser = ReadChaser();
            EnemyProfile ranged = ReadRanged();
            EnemyProfile charging = ReadCharging();

            Assert(chaser.Health == 60 && chaser.Damage == EnemyDamageTier.Heavy &&
                   Mathf.Approximately(chaser.Speed, 2.25f),
                "Enemy-2 chaser must use HP 60, heavy contact damage, and move speed 2.25.");
            Assert(Require<MeleeEnemyAttack>(LoadPrefab(ChaserPrefabPath), ChaserPrefabPath).DamageTier ==
                   EnemyDamageTier.Critical,
                "Enemy-2 chaser must use critical melee damage.");
            Assert(ranged.Health == 30 && ranged.Damage == EnemyDamageTier.Heavy &&
                   Mathf.Approximately(ranged.Speed, 1.5f),
                "Ranged must use HP 30, heavy projectile damage, and move speed 1.5.");
            Assert(charging.Health == 70 && charging.Damage == EnemyDamageTier.Heavy &&
                   Mathf.Approximately(charging.Speed, Week19Tune1Setup.ChargingDashSpeed),
                $"Charging must use HP 70, heavy charge damage, and dash speed {Week19Tune1Setup.ChargingDashSpeed}.");

            HashSet<float> speedValues = new() { chaser.Speed, ranged.Speed, charging.Speed };
            Assert(speedValues.Count == 3,
                "All three normal monster roles must retain distinct movement speeds.");
            Assert(ranged.Health < chaser.Health && chaser.Health < charging.Health &&
                   ranged.Speed < chaser.Speed && chaser.Speed < charging.Speed &&
                   ranged.Damage <= chaser.Damage && chaser.Damage <= charging.Damage,
                "Enemy profiles must preserve fragile ranged, strong chaser contact, and durable charging roles.");

            Debug.Log(
                "Phase E-4 enemy balance verification passed: all three enemy prefabs preserve their " +
                "intended HP, damage, and movement-speed roles.");
        }

        private static EnemyProfile ReadChaser()
        {
            GameObject prefab = LoadPrefab(ChaserPrefabPath);
            Health health = Require<Health>(prefab, ChaserPrefabPath);
            EnemyChase chase = Require<EnemyChase>(prefab, ChaserPrefabPath);
            ContactDamage contact = Require<ContactDamage>(prefab, ChaserPrefabPath);
            return new EnemyProfile(health.MaxHealth, contact.DamageTier, chase.MoveSpeed);
        }

        private static EnemyProfile ReadRanged()
        {
            GameObject prefab = LoadPrefab(RangedPrefabPath);
            Health health = Require<Health>(prefab, RangedPrefabPath);
            RangedEnemyController ranged = Require<RangedEnemyController>(prefab, RangedPrefabPath);
            return new EnemyProfile(health.MaxHealth, ranged.ProjectileDamageTier, ranged.MoveSpeed);
        }

        private static EnemyProfile ReadCharging()
        {
            GameObject prefab = LoadPrefab(ChargingPrefabPath);
            Health health = Require<Health>(prefab, ChargingPrefabPath);
            ChargingEnemyController charging = Require<ChargingEnemyController>(prefab, ChargingPrefabPath);
            return new EnemyProfile(health.MaxHealth, charging.ChargeDamageTier, charging.DashSpeed);
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, $"Missing enemy prefab at {path}. Run Phase E-2 and E-3 Setup first.");
            return prefab;
        }

        private static T Require<T>(GameObject prefab, string path) where T : Component
        {
            T component = prefab.GetComponent<T>();
            Assert(component != null, $"{path} is missing required component {typeof(T).Name}.");
            return component;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly struct EnemyProfile
        {
            public EnemyProfile(float health, EnemyDamageTier damage, float speed)
            {
                Health = health;
                Damage = damage;
                Speed = speed;
            }

            public float Health { get; }
            public EnemyDamageTier Damage { get; }
            public float Speed { get; }
        }
    }
}
