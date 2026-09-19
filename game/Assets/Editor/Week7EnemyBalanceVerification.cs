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

            Assert(chaser.Health == 6 && chaser.Damage == 2 && Mathf.Approximately(chaser.Speed, 2.25f),
                "Enemy-2 chaser must use HP 6, contact damage 2, and move speed 2.25.");
            Assert(Mathf.Approximately(
                    Require<MeleeEnemyAttack>(LoadPrefab(ChaserPrefabPath), ChaserPrefabPath).AttackDamage,
                    3f),
                "Enemy-2 chaser must use melee damage 3.");
            Assert(ranged.Health == 3 && ranged.Damage == 2 && Mathf.Approximately(ranged.Speed, 1.5f),
                "Ranged must use HP 3, projectile damage 2, and move speed 1.5.");
            Assert(charging.Health == 7 && charging.Damage == 3 && Mathf.Approximately(charging.Speed, 8f),
                "Charging must use HP 7, charge damage 3, and dash speed 8.");

            HashSet<float> speedValues = new() { chaser.Speed, ranged.Speed, charging.Speed };
            Assert(speedValues.Count == 3,
                "All three normal monster roles must retain distinct movement speeds.");
            Assert(ranged.Health < chaser.Health && chaser.Health < charging.Health &&
                   ranged.Speed < chaser.Speed && chaser.Speed < charging.Speed &&
                   ranged.Damage <= chaser.Damage && chaser.Damage < charging.Damage,
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
            return new EnemyProfile(health.MaxHealth, contact.Damage, chase.MoveSpeed);
        }

        private static EnemyProfile ReadRanged()
        {
            GameObject prefab = LoadPrefab(RangedPrefabPath);
            Health health = Require<Health>(prefab, RangedPrefabPath);
            RangedEnemyController ranged = Require<RangedEnemyController>(prefab, RangedPrefabPath);
            return new EnemyProfile(health.MaxHealth, ranged.ProjectileDamage, ranged.MoveSpeed);
        }

        private static EnemyProfile ReadCharging()
        {
            GameObject prefab = LoadPrefab(ChargingPrefabPath);
            Health health = Require<Health>(prefab, ChargingPrefabPath);
            ChargingEnemyController charging = Require<ChargingEnemyController>(prefab, ChargingPrefabPath);
            return new EnemyProfile(health.MaxHealth, charging.ChargeDamage, charging.DashSpeed);
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
            public EnemyProfile(float health, float damage, float speed)
            {
                Health = health;
                Damage = damage;
                Speed = speed;
            }

            public float Health { get; }
            public float Damage { get; }
            public float Speed { get; }
        }
    }
}
