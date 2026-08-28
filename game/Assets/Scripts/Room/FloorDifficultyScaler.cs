using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public static class FloorDifficultyScaler
    {
        public static float GetHealthMultiplier(int floorNumber)
        {
            return floorNumber switch
            {
                1 => 1.0f,
                2 => 1.5f,
                3 => 2.0f,
                _ => floorNumber > 3 ? 1.0f + (floorNumber - 1) * 0.5f : 1.0f,
            };
        }

        public static float GetDamageMultiplier(int floorNumber)
        {
            return floorNumber switch
            {
                1 => 1.0f,
                2 => 1.3f,
                3 => 1.6f,
                _ => floorNumber > 3 ? 1.0f + (floorNumber - 1) * 0.3f : 1.0f,
            };
        }

        public static void ApplyScaling(GameObject enemy, int floorNumber)
        {
            if (enemy == null || floorNumber < 1)
            {
                return;
            }

            ApplyHealthScaling(enemy, GetHealthMultiplier(floorNumber));
            ApplyDamageScaling(enemy, floorNumber);
        }

        public static float GetScaledDamage(float baseDamage, int floorNumber)
        {
            float clampedBaseDamage = Mathf.Max(0.01f, baseDamage);
            return clampedBaseDamage * GetDamageMultiplier(floorNumber);
        }

        private static void ApplyHealthScaling(GameObject enemy, float multiplier)
        {
            Health health = enemy.GetComponent<Health>();
            if (health == null)
            {
                return;
            }

            float scaledMaxHealth = health.MaxHealth * multiplier;
            health.SetMaxHealth(scaledMaxHealth, true);
        }

        private static void ApplyDamageScaling(GameObject enemy, int floorNumber)
        {
            ContactDamage contactDamage = enemy.GetComponent<ContactDamage>();
            if (contactDamage != null)
            {
                float scaledDamage = GetScaledDamage(contactDamage.Damage, floorNumber);
                contactDamage.Configure(scaledDamage, contactDamage.Cooldown);
            }

            RangedEnemyController ranged = enemy.GetComponent<RangedEnemyController>();
            if (ranged != null)
            {
                float scaledDamage = GetScaledDamage(ranged.ProjectileDamage, floorNumber);
                ranged.SetProjectileDamage(scaledDamage);
            }

            ChargingEnemyController charging = enemy.GetComponent<ChargingEnemyController>();
            if (charging != null)
            {
                float scaledDamage = GetScaledDamage(charging.ChargeDamage, floorNumber);
                charging.SetChargeDamage(scaledDamage);
            }

            BossController boss = enemy.GetComponent<BossController>();
            if (boss != null)
            {
                float scaledDamage = GetScaledDamage(boss.ProjectileDamage, floorNumber);
                boss.SetProjectileDamage(scaledDamage);
            }
        }
    }
}
