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
        // Variant-1 챔피언: the same enemy with one death property, shown by a coloured outline.
        // 죽으면 장판 (green): a floor puddle that hurts the player where it died.
        ChampionPuddle = 2,
        // 죽으면 4방향 탄 (purple): shots up, down, left and right; 8 ways for 저혈당 요정-활발.
        ChampionShots = 3,
        // 죽으면 자폭 (yellow): the 쫄따구 death blast (0.6 s telegraph). Never on 자폭형.
        ChampionBlast = 4,
        // Variant-2 불사(사이온) (white): keeps fighting for 3 s on undying life after its health reaches 0.
        ChampionUndying = 5,
    }

    // Variant-0 (user decision, 2026-10-24): an elite is the same enemy drawn 1.5x with the same feet (one-tile lanes
    // and pathing stay), double health, double knockback weight, one damage tier higher, 1.2x ordinary movement and
    // 1.5x shots. Attack timing is unchanged. Added at spawn after the floor scaling; it never runs twice on one body.
    // Variant-1: a champion keeps its body and stats and gains one death property (EnemyChampionDeath) and an
    // outline in that property's colour (EnemyChampionOutline). Elites and champions never stack.
    // Variant-2: the undying champion (EnemyChampionUndying) delays its death instead and has no death effect.
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
        // Variant-1 (docs/28 §8): a champion adds 1 threat.
        public const int ChampionThreatBonus = 1;

        public static readonly EnemyVariantKind[] ChampionKinds =
        {
            EnemyVariantKind.ChampionPuddle, EnemyVariantKind.ChampionShots, EnemyVariantKind.ChampionBlast,
            EnemyVariantKind.ChampionUndying,
        };

        [SerializeField] private EnemyVariantKind kind;

        public EnemyVariantKind Kind => kind;
        public bool IsElite => kind == EnemyVariantKind.Elite;
        public bool IsChampion => IsChampionKind(kind);

        public static bool IsChampionKind(EnemyVariantKind variantKind) =>
            variantKind is EnemyVariantKind.ChampionPuddle or EnemyVariantKind.ChampionShots
                or EnemyVariantKind.ChampionBlast or EnemyVariantKind.ChampionUndying;

        public static EnemyVariantKind KindOf(GameObject enemy) =>
            enemy != null && enemy.TryGetComponent(out EnemyVariant variant) ? variant.kind : EnemyVariantKind.None;

        public static bool IsEliteEnemy(GameObject enemy) => KindOf(enemy) == EnemyVariantKind.Elite;
        public static bool IsChampionEnemy(GameObject enemy) => IsChampionKind(KindOf(enemy));

        // Variant-1 (docs/28 §8): green puddle, purple shots, yellow blast. Variant-2: white undying.
        public static Color ChampionColor(EnemyVariantKind variantKind) => variantKind switch
        {
            EnemyVariantKind.ChampionPuddle => new Color(0.36f, 0.95f, 0.32f, 1f),
            EnemyVariantKind.ChampionShots => new Color(0.72f, 0.36f, 1f, 1f),
            EnemyVariantKind.ChampionBlast => new Color(1f, 0.86f, 0.18f, 1f),
            EnemyVariantKind.ChampionUndying => Color.white,
            _ => Color.clear,
        };

        // Ordinary movement only, like the status effect slow (dashes, lunges and patterns keep their speed).
        // Variant-2: an undying champion moves 30% faster while it runs on undying life.
        public static float MoveSpeedMultiplierOf(Component enemy)
        {
            if (enemy == null) return 1f;
            if (IsEliteEnemy(enemy.gameObject)) return EliteMoveSpeedMultiplier;
            return EnemyChampionUndying.IsActiveOn(enemy) ? EnemyChampionUndying.SpeedMultiplier : 1f;
        }

        // Variant-2: the wait between attacks (cooldowns, recoveries, summon intervals) of an undying champion
        // running on undying life, so it attacks 30% faster. Telegraphs and wind-ups keep their length.
        public static float AttackIntervalOf(Component enemy, float interval) =>
            EnemyChampionUndying.IsActiveOn(enemy) ? interval / EnemyChampionUndying.SpeedMultiplier : interval;

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

        // Variant-1: an enemy that already explodes when it dies (자폭형, 쫄따구) never gets the death blast.
        public static bool CanApply(GameObject enemy, EnemyVariantKind variantKind) =>
            variantKind != EnemyVariantKind.None && CanApply(enemy) &&
            !(variantKind == EnemyVariantKind.ChampionBlast &&
              (enemy.GetComponent<EnemyDeathExplosion>() != null || enemy.GetComponent<SelfDestructEnemy>() != null));

        public static bool Apply(GameObject enemy, EnemyVariantKind variantKind)
        {
            if (!CanApply(enemy, variantKind)) return false;
            EnemyVariant variant = enemy.GetComponent<EnemyVariant>();
            if (variant == null) variant = enemy.AddComponent<EnemyVariant>();
            variant.kind = variantKind;
            if (variantKind == EnemyVariantKind.Elite) ApplyElite(enemy);
            else if (IsChampionKind(variantKind)) ApplyChampion(enemy, variantKind);
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

        private static void ApplyChampion(GameObject enemy, EnemyVariantKind variantKind)
        {
            EnemyChampionOutline outline = enemy.GetComponent<EnemyChampionOutline>();
            if (outline == null) outline = enemy.AddComponent<EnemyChampionOutline>();
            outline.Configure(ChampionColor(variantKind));
            if (variantKind == EnemyVariantKind.ChampionUndying)
            {
                EnemyChampionUndying undying = enemy.GetComponent<EnemyChampionUndying>();
                if (undying == null) undying = enemy.AddComponent<EnemyChampionUndying>();
                undying.Initialize();
                return;
            }

            EnemyChampionDeath death = enemy.GetComponent<EnemyChampionDeath>();
            if (death == null) death = enemy.AddComponent<EnemyChampionDeath>();
            death.Initialize(variantKind);
        }
    }
}
