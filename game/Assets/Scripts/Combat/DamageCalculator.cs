using UnityEngine;

namespace TrickalFanGame.Combat
{
    public static class DamageCalculator
    {
        public static int Calculate(DamageContext context)
        {
            return Mathf.Max(0, Mathf.RoundToInt(context.BaseDamage * context.Multiplier));
        }
    }
}
