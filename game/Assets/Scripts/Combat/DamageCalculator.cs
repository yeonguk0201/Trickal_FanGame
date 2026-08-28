using UnityEngine;

namespace TrickalFanGame.Combat
{
    public static class DamageCalculator
    {
        public static float Calculate(DamageContext context)
        {
            return Mathf.Max(0f, context.BaseDamage * context.Multiplier);
        }
    }
}
