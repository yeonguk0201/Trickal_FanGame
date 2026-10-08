using UnityEngine;

namespace TrickalFanGame.Combat
{
    public readonly struct DamageContext
    {
        public DamageContext(
            GameObject source,
            DamageSourceType sourceType,
            float baseDamage,
            float multiplier = 1f,
            DamageDeliveryType deliveryType = DamageDeliveryType.Direct,
            float criticalChance = 0f,
            float criticalDamageMultiplier = 1f,
            float? criticalRollOverride = null,
            float distanceDamageBonus = 0f,
            float distanceDamageMinimum = 0f,
            float distanceDamageMaximum = 0f,
            float? impactDistance = null,
            bool allowsHalfHeart = false)
        {
            AllowsHalfHeart = allowsHalfHeart;
            Source = source;
            SourceType = sourceType;
            BaseDamage = Mathf.Max(0, baseDamage);
            Multiplier = Mathf.Max(0f, multiplier);
            DeliveryType = deliveryType;
            CriticalChance = Mathf.Clamp01(criticalChance);
            CriticalDamageMultiplier = Mathf.Max(1f, criticalDamageMultiplier);
            CriticalRollOverride = criticalRollOverride.HasValue
                ? Mathf.Clamp01(criticalRollOverride.Value)
                : null;
            DistanceDamageBonus = Mathf.Max(0f, distanceDamageBonus);
            DistanceDamageMinimum = Mathf.Max(0f, distanceDamageMinimum);
            DistanceDamageMaximum = Mathf.Max(DistanceDamageMinimum, distanceDamageMaximum);
            ImpactDistance = impactDistance.HasValue ? Mathf.Max(0f, impactDistance.Value) : null;
        }

        public GameObject Source { get; }
        public DamageSourceType SourceType { get; }
        public float BaseDamage { get; }
        public float Multiplier { get; }
        public DamageDeliveryType DeliveryType { get; }
        public float CriticalChance { get; }
        public float CriticalDamageMultiplier { get; }
        public float? CriticalRollOverride { get; }
        public float DistanceDamageBonus { get; }
        public float DistanceDamageMinimum { get; }
        public float DistanceDamageMaximum { get; }
        public float? ImpactDistance { get; }
        // Enemy-6: a hit that may take half a heart from the player, below the usual one-heart minimum.
        public bool AllowsHalfHeart { get; }
        public bool CanCritical => DeliveryType == DamageDeliveryType.Direct && CriticalChance > 0f;
        public float DistanceDamageMultiplier
        {
            get
            {
                if (DeliveryType != DamageDeliveryType.Direct || DistanceDamageBonus <= 0f ||
                    DistanceDamageMaximum <= DistanceDamageMinimum || !ImpactDistance.HasValue)
                {
                    return 1f;
                }

                float progress = Mathf.InverseLerp(
                    DistanceDamageMinimum,
                    DistanceDamageMaximum,
                    ImpactDistance.Value);
                return 1f + DistanceDamageBonus * progress;
            }
        }

        public DamageContext WithImpactDistance(float distance)
        {
            return new DamageContext(
                Source,
                SourceType,
                BaseDamage,
                Multiplier,
                DeliveryType,
                CriticalChance,
                CriticalDamageMultiplier,
                CriticalRollOverride,
                DistanceDamageBonus,
                DistanceDamageMinimum,
                DistanceDamageMaximum,
                distance,
                AllowsHalfHeart);
        }

        // Artifact-2: bonuses that depend on the target's state (burning, shocked), judged when the hit lands.
        public DamageContext WithTargetBonuses(
            float multiplierScale,
            float criticalChanceBonus,
            float criticalDamageBonus)
        {
            return new DamageContext(
                Source,
                SourceType,
                BaseDamage,
                Multiplier * Mathf.Max(0f, multiplierScale),
                DeliveryType,
                CriticalChance + Mathf.Max(0f, criticalChanceBonus),
                CriticalDamageMultiplier + Mathf.Max(0f, criticalDamageBonus),
                CriticalRollOverride,
                DistanceDamageBonus,
                DistanceDamageMinimum,
                DistanceDamageMaximum,
                ImpactDistance,
                AllowsHalfHeart);
        }

        public DamageContext ScaleMultiplier(float multiplier)
        {
            return new DamageContext(
                Source,
                SourceType,
                BaseDamage,
                Multiplier * Mathf.Max(0f, multiplier),
                DeliveryType,
                CriticalChance,
                CriticalDamageMultiplier,
                CriticalRollOverride,
                DistanceDamageBonus,
                DistanceDamageMinimum,
                DistanceDamageMaximum);
        }
    }
}
