using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Passive-0 §4.6: basic attack shots do not shove enemies through physics. They push only by this formula:
    // base knockback × (shot speed ÷ 8, limited to 0.5~2.0) × (1 + knockback bonus) ÷ enemy knockback weight.
    public static class BasicAttackKnockback
    {
        public const float BaseSpeed = 2f;
        public const float Duration = 0.1f;
        public const float ReferenceProjectileSpeed = 8f;
        public const float MinimumSpeedRatio = 0.5f;
        public const float MaximumSpeedRatio = 2f;

        public static float ResolveSpeed(float projectileSpeed, float knockbackWeight, float knockbackBonus = 0f)
        {
            float speedRatio = Mathf.Clamp(projectileSpeed / ReferenceProjectileSpeed, MinimumSpeedRatio,
                MaximumSpeedRatio);
            return BaseSpeed * speedRatio * Mathf.Max(0f, 1f + knockbackBonus) / Mathf.Max(0.01f, knockbackWeight);
        }

        public static bool TryApply(Health target, Vector2 projectileVelocity)
        {
            if (target == null || target.IsDead)
            {
                return false;
            }

            KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
            return receiver != null && receiver.ApplyPush(projectileVelocity,
                ResolveSpeed(projectileVelocity.magnitude, receiver.KnockbackWeight), Duration);
        }
    }
}
