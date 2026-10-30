using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-0 (docs/28 §8). Numeric values may be stored with generated spawns. Append kinds only.
    public enum EnemyVariantKind
    {
        None = 0,
        // 엘리트(거대): the same enemy, bigger and tougher. Logic is unchanged.
        Elite = 1,
    }

    // Variant-0 (user decision, 2026-10-24): an elite is the same enemy drawn 1.5x with the same feet (one-tile lanes
    // and pathing stay), double health, double knockback weight, one damage tier higher, 1.2x ordinary movement and
    // 1.5x shots. Attack timing is unchanged. Added at spawn after the floor scaling; it never runs twice on one body.
    [DisallowMultipleComponent]
    public sealed class EnemyVariant : MonoBehaviour
    {
        public const float EliteVisualScale = 1.5f;
        public const float EliteHealthMultiplier = 2f;
        public const float EliteKnockbackWeightMultiplier = 2f;
        // The plan's trial value (×1.2). Tune after Play.
        public const float EliteMoveSpeedMultiplier = 1.2f;
        public const float EliteProjectileScale = 1.5f;
        public const int EliteDamageTierSteps = 1;
        public const int EliteThreatMultiplier = 2;

        [SerializeField] private EnemyVariantKind kind;

        public EnemyVariantKind Kind => kind;
        public bool IsElite => kind == EnemyVariantKind.Elite;

        public static EnemyVariantKind KindOf(GameObject enemy) =>
            enemy != null && enemy.TryGetComponent(out EnemyVariant variant) ? variant.kind : EnemyVariantKind.None;

        public static bool IsEliteEnemy(GameObject enemy) => KindOf(enemy) == EnemyVariantKind.Elite;

        // Ordinary movement only, like the status effect slow (dashes, lunges and patterns keep their speed).
        public static float MoveSpeedMultiplierOf(Component enemy) =>
            enemy != null && IsEliteEnemy(enemy.gameObject) ? EliteMoveSpeedMultiplier : 1f;

        public static float ProjectileScaleOf(GameObject source) =>
            IsEliteEnemy(source) ? EliteProjectileScale : 1f;

        public static EnemyDamageTier ResolveDamageTier(GameObject source, EnemyDamageTier tier)
        {
            if (!IsEliteEnemy(source)) return tier;
            int raised = Mathf.Min((int)tier + EliteDamageTierSteps, (int)EnemyDamageTier.Critical);
            return (EnemyDamageTier)raised;
        }

        // Normal walking or flying enemies only. Which roles may become variants is decided where spawns are
        // generated (EnemyVariantRules); this only refuses bodies that can never take one.
        public static bool CanApply(GameObject enemy) =>
            enemy != null && enemy.GetComponent<Health>() != null &&
            enemy.GetComponent<EnemyBehaviorContext>() != null && enemy.GetComponent<BossController>() == null &&
            KindOf(enemy) == EnemyVariantKind.None;

        public static bool Apply(GameObject enemy, EnemyVariantKind variantKind)
        {
            if (variantKind == EnemyVariantKind.None || !CanApply(enemy)) return false;
            EnemyVariant variant = enemy.GetComponent<EnemyVariant>();
            if (variant == null) variant = enemy.AddComponent<EnemyVariant>();
            variant.kind = variantKind;
            if (variantKind == EnemyVariantKind.Elite) ApplyElite(enemy);
            return true;
        }

        private static void ApplyElite(GameObject enemy)
        {
            Health health = enemy.GetComponent<Health>();
            health.SetMaxHealth(health.MaxHealth * EliteHealthMultiplier, true);

            if (enemy.TryGetComponent(out KnockbackReceiver knockback))
                knockback.ConfigureWeight(knockback.KnockbackWeight * EliteKnockbackWeightMultiplier);

            // The whole body grows, so the drawing, its shadow and the hurtbox stay together. The feet are then
            // restored to their world size and moved to the new bottom of the body.
            enemy.transform.localScale *= EliteVisualScale;
            foreach (EnemyAttackPresentation presentation in enemy.GetComponents<EnemyAttackPresentation>())
                presentation.MultiplyIdleScale(EliteVisualScale);
            if (enemy.GetComponentInChildren<EnemyFeet>(true) != null) EnemyFeet.Ensure(enemy, out _);
        }
    }
}
