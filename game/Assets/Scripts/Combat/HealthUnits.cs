using UnityEngine;

namespace TrickalFanGame.Combat
{
    public static class HealthUnits
    {
        public const int UnitsPerHeart = 2;
        public const int MinimumEnemyDamageUnits = UnitsPerHeart;
        // Enemy-6: the only damage below the one-heart minimum, for hits that explicitly allow it (쥬비).
        public const int HalfHeartDamageUnits = 1;
        // The player's current health and shield together stop at 15 hearts (사용자 결정, 2026-10-06).
        public const int MaximumHealthAndShieldUnits = 15 * UnitsPerHeart;
        // Hits of 2 hearts or more grant the longer post-hit invulnerability window (사용자 결정, 2026-10-17).
        public const int HeavyDamageThresholdUnits = 2 * UnitsPerHeart;

        private const float RoundingTolerance = 0.0001f;

        // Curve-0 (사용자 결정, 2026-10-16): damage no longer rises every floor. Floors 1-2 use the base row and
        // floor 3 steps up once, to the former floor 2 values.
        private static readonly int[,] EnemyDamageUnitsByFloor =
        {
            { 2, 2, 2 },
            { 3, 3, 4 },
            { 4, 4, 6 },
            { 6, 6, 7 },
        };

        public static int GetEnemyDamageUnits(EnemyDamageTier tier, int floorNumber)
        {
            int tierIndex = Mathf.Clamp((int)tier, 0, EnemyDamageUnitsByFloor.GetLength(0) - 1);
            int floorIndex = Mathf.Clamp(floorNumber, 1, EnemyDamageUnitsByFloor.GetLength(1)) - 1;
            return EnemyDamageUnitsByFloor[tierIndex, floorIndex];
        }

        public static DamageContext CreateEnemyDamageContext(
            GameObject source,
            DamageSourceType sourceType,
            EnemyDamageTier tier,
            float multiplier = 1f)
        {
            int floorNumber = EnemyFloorLevel.Resolve(source);
            // Variant-0: an elite hits one tier higher.
            tier = TrickalFanGame.Enemy.EnemyVariant.ResolveDamageTier(source, tier);
            return new DamageContext(source, sourceType, GetEnemyDamageUnits(tier, floorNumber), multiplier);
        }

        public static int ToDamageUnits(float damage, bool allowsHalfHeart = false)
        {
            if (damage <= 0f)
            {
                return 0;
            }

            return Mathf.Max(allowsHalfHeart ? HalfHeartDamageUnits : MinimumEnemyDamageUnits,
                Mathf.CeilToInt(damage - RoundingTolerance));
        }

        public static int FloorToUnits(float amount)
        {
            return amount <= 0f ? 0 : Mathf.FloorToInt(amount + RoundingTolerance);
        }

        public static int FromMaxHealthRatio(float maxHealthUnits, float ratio)
        {
            if (maxHealthUnits <= 0f || ratio <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, FloorToUnits(maxHealthUnits * ratio));
        }

        public static float ToHearts(float units)
        {
            return units / UnitsPerHeart;
        }

        public static string FormatHearts(float units)
        {
            return ToHearts(FloorToUnits(units)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
