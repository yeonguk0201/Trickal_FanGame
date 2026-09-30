using System;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public static class DamageCalculator
    {
        private static Func<float> criticalRollProvider = () => UnityEngine.Random.value;

        public static float Calculate(DamageContext context)
        {
            return Resolve(context).FinalDamage;
        }

        public static float Calculate(DamageContext context, float criticalRoll)
        {
            return Resolve(context, criticalRoll).FinalDamage;
        }

        public static DamageResult Resolve(DamageContext context)
        {
            float criticalRoll = context.CriticalRollOverride ?? criticalRollProvider();
            return Resolve(context, criticalRoll);
        }

        public static DamageResult Resolve(DamageContext context, float criticalRoll)
        {
            float damage = Mathf.Max(
                0f,
                context.BaseDamage * context.Multiplier * context.DistanceDamageMultiplier);
            bool isCritical = context.CanCritical && Mathf.Clamp01(criticalRoll) < context.CriticalChance;
            if (isCritical)
            {
                damage *= context.CriticalDamageMultiplier;
            }

            return new DamageResult(Mathf.Max(0f, damage), isCritical);
        }

        public static void SetCriticalRollProviderForTesting(Func<float> provider)
        {
            criticalRollProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static void ResetCriticalRollProvider()
        {
            criticalRollProvider = () => UnityEngine.Random.value;
        }
    }
}
