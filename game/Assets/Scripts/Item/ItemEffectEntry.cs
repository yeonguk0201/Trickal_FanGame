using System;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [Serializable]
    public sealed class ItemEffectEntry
    {
        [SerializeField] private ItemEffectType effectType;
        [SerializeField] private float magnitude;
        [SerializeField] private float healthThreshold;
        [SerializeField] private float secondaryMagnitude;
        [SerializeField, Min(0)] private int integerAmount;
        [SerializeField, Min(0f)] private float minimumDistance;
        [SerializeField, Min(0f)] private float maximumDistance;
        [SerializeField, Min(0f)] private float radius;
        [SerializeField, Min(0f)] private float intervalSeconds;
        [SerializeField, Min(0f)] private float spreadAngleDegrees;
        [SerializeField, Min(0f)] private float scaleMultiplier;

        public ItemEffectType EffectType => effectType;
        public float Magnitude => magnitude;
        public float HealthThreshold => healthThreshold;
        public float SecondaryMagnitude => secondaryMagnitude;
        public int IntegerAmount => integerAmount;
        public float MinimumDistance => minimumDistance;
        public float MaximumDistance => maximumDistance;
        public float Radius => radius;
        public float IntervalSeconds => intervalSeconds;
        public float SpreadAngleDegrees => spreadAngleDegrees;
        public float ScaleMultiplier => scaleMultiplier;

        public ItemEffectEntry(
            ItemEffectType configuredEffectType,
            float configuredMagnitude = 0f,
            float configuredHealthThreshold = 0f,
            float configuredSecondaryMagnitude = 0f,
            int configuredIntegerAmount = 0,
            float configuredMinimumDistance = 0f,
            float configuredMaximumDistance = 0f,
            float configuredRadius = 0f,
            float configuredIntervalSeconds = 0f,
            float configuredSpreadAngleDegrees = 0f,
            float configuredScaleMultiplier = 0f)
        {
            effectType = configuredEffectType;
            magnitude = configuredMagnitude;
            healthThreshold = configuredHealthThreshold;
            secondaryMagnitude = configuredSecondaryMagnitude;
            integerAmount = configuredIntegerAmount;
            minimumDistance = configuredMinimumDistance;
            maximumDistance = configuredMaximumDistance;
            radius = configuredRadius;
            intervalSeconds = configuredIntervalSeconds;
            spreadAngleDegrees = configuredSpreadAngleDegrees;
            scaleMultiplier = configuredScaleMultiplier;
        }

        public bool TryValidate(out string error)
        {
            if (RequiresMagnitude(effectType) && magnitude <= 0f)
            {
                error = $"{effectType} requires a positive magnitude.";
                return false;
            }

            switch (effectType)
            {
                case ItemEffectType.MoveSpeedPercentBelowHealth:
                    if (healthThreshold <= 0f || healthThreshold > 1f)
                    {
                        error = "MoveSpeedPercentBelowHealth requires a health threshold in (0, 1].";
                        return false;
                    }

                    break;
                case ItemEffectType.MaxHealthDamageAura:
                    if (radius <= 0f || intervalSeconds <= 0f)
                    {
                        error = "MaxHealthDamageAura requires a positive radius and tick interval.";
                        return false;
                    }

                    break;
                case ItemEffectType.DistanceDamage:
                    if (minimumDistance < 0f || maximumDistance <= minimumDistance)
                    {
                        error = "DistanceDamage requires a non-negative minimum and a greater maximum distance.";
                        return false;
                    }

                    break;
                case ItemEffectType.SplitAfterPierce:
                    if (integerAmount <= 0 || secondaryMagnitude <= 0f || maximumDistance <= 0f ||
                        spreadAngleDegrees <= 0f || scaleMultiplier <= 0f)
                    {
                        error = "SplitAfterPierce requires projectile count, damage multiplier, maximum distance, spread angle, and scale multiplier.";
                        return false;
                    }

                    break;
                case ItemEffectType.SkillProjectileBonusAtSP:
                    if (healthThreshold <= 0f || integerAmount <= 0)
                    {
                        error = "SkillProjectileBonusAtSP requires a positive SP threshold and projectile count.";
                        return false;
                    }

                    break;
                case ItemEffectType.Pierce:
                case ItemEffectType.MaxSP:
                case ItemEffectType.MultiShot:
                    if (integerAmount <= 0)
                    {
                        error = $"{effectType} requires a positive integer amount.";
                        return false;
                    }

                    break;
            }

            error = string.Empty;
            return true;
        }

        private static bool RequiresMagnitude(ItemEffectType type)
        {
            return type != ItemEffectType.Pierce &&
                   type != ItemEffectType.SplitAfterPierce &&
                   type != ItemEffectType.MaxSP &&
                   type != ItemEffectType.SkillProjectileBonusAtSP &&
                   type != ItemEffectType.MultiShot;
        }
    }
}
