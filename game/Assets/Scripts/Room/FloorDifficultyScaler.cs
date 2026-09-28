using TrickalFanGame.Combat;
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

        public static void ApplyScaling(GameObject enemy, int floorNumber)
        {
            if (enemy == null || floorNumber < 1)
            {
                return;
            }

            ApplyHealthScaling(enemy, GetHealthMultiplier(floorNumber));
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
