using UnityEngine;

namespace TrickalFanGame.Player
{
    public static class SkillProgressionRules
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 10;

        public static int ClampLevel(int level) => Mathf.Clamp(level, MinimumLevel, MaximumLevel);

        public static float DamageMultiplier(int level)
        {
            return 1f + (ClampLevel(level) - 1) * 0.1f;
        }

        public static int LowerGradeProjectileBonus(int level)
        {
            return ClampLevel(level) >= 5 ? 1 : 0;
        }

        public static float HighGradeCooldownMultiplier(int level)
        {
            return ClampLevel(level) >= 5 ? 0.85f : 1f;
        }
    }
}
