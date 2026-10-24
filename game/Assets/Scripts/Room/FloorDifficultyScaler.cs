using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public static class FloorDifficultyScaler
    {
        // Curve-0 (사용자 결정, 2026-10-16): normal enemies keep their base health on every floor. Difficulty grows
        // from the enemy mix and patterns instead; only bosses keep the depth multiplier.
        public const float NormalEnemyHealthMultiplier = 1.0f;

        public static float GetBossHealthMultiplier(int floorNumber)
        {
            return floorNumber switch
            {
                1 => 1.0f,
                2 => 1.5f,
                3 => 2.0f,
                _ => floorNumber > 3 ? 1.0f + (floorNumber - 1) * 0.5f : 1.0f,
            };
        }

        public static float GetHealthMultiplier(GameObject enemy, int floorNumber)
        {
            return IsBoss(enemy) ? GetBossHealthMultiplier(floorNumber) : NormalEnemyHealthMultiplier;
        }

        public static bool IsBoss(GameObject enemy)
        {
            return enemy != null && enemy.GetComponent<BossController>() != null;
        }

        public static void ApplyScaling(GameObject enemy, int floorNumber)
        {
            if (enemy == null || floorNumber < 1)
            {
                return;
            }

            ApplyHealthScaling(enemy, GetHealthMultiplier(enemy, floorNumber));
            // Player damage is resolved from each attack's EnemyDamageTier and this floor at hit time.
            EnemyFloorLevel.Apply(enemy, floorNumber);
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
    }
}
