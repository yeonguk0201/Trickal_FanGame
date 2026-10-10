using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Passive-0 §4.7·§4.8: shared Health-target status state. Added when a status first applies;
    // its artwork works on players, enemies and bosses without changing damage/stack rules.
    // Poison stacks up; each application adds a stack and renews the duration, and every stack ends together.
    // Burn never stacks and only renews. Shock stacks like poison but slows movement instead of dealing damage.
    // The three are independent and can be on one enemy together.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private const float TickTimeTolerance = 0.0001f;
        // Passive-0 §4.7: a boss takes half of the shock slow.
        private const float BossShockSlowScale = 0.5f;

        private Health health;
        private GameObject poisonSource;
        private float poisonInterval;
        private GameObject burnSource;
        private float burnInterval;
        private float shockSlowPerStack;
        private bool isBoss;

        public int PoisonStacks { get; private set; }
        public bool IsPoisoned => PoisonStacks > 0;
        // Damage each stack deals per tick, fixed from the attack damage when poison was last applied.
        public float PoisonTickDamagePerStack { get; private set; }
        public float PoisonEndTime { get; private set; }
        public float NextPoisonTickTime { get; private set; }

        public bool IsBurning { get; private set; }
        // Damage each burn tick deals, fixed from the attack damage when burn was last applied.
        public float BurnTickDamage { get; private set; }
        public float BurnEndTime { get; private set; }
        public float NextBurnTickTime { get; private set; }

        public int ShockStacks { get; private set; }
        public bool IsShocked => ShockStacks > 0;
        public float ShockEndTime { get; private set; }
        // Ordinary movement (chasing, approaching, retreating) is multiplied by this. Dashes, lunges, boss
        // patterns, shot speed and attack timing ignore it.
        public float MoveSpeedMultiplier => IsShocked
            ? Mathf.Clamp01(1f - shockSlowPerStack * ShockStacks * (isBoss ? BossShockSlowScale : 1f))
            : 1f;

        private void Awake()
        {
            health = GetComponent<Health>();
            isBoss = GetComponent<TrickalFanGame.Enemy.BossController>() != null;
            if (GetComponent<TrickalFanGame.Frontend.FairyKingdomStatusArtwork>() == null)
                gameObject.AddComponent<TrickalFanGame.Frontend.FairyKingdomStatusArtwork>();
        }

        private void Update()
        {
            Tick(Time.time);
        }

        private void OnDisable()
        {
            Clear();
        }

        public static float MoveSpeedMultiplierOf(Component enemy)
        {
            return enemy != null && enemy.TryGetComponent(out EnemyStatusEffects effects)
                ? effects.MoveSpeedMultiplier
                : 1f;
        }

        public static bool RollsPoison(PoisonSettings settings, float roll)
        {
            return settings.IsEnabled && roll < settings.Chance;
        }

        public static bool TryApplyPoison(
            Health target,
            GameObject source,
            float attackDamage,
            PoisonSettings settings,
            float roll,
            float tickDamageMultiplier = 1f)
        {
            if (target == null || target.IsDead || attackDamage <= 0f || !RollsPoison(settings, roll))
            {
                return false;
            }

            GetOrAdd(target).ApplyPoison(source, attackDamage, settings, Time.time, tickDamageMultiplier);
            return true;
        }

        public static bool TryApplyBurn(
            Health target,
            GameObject source,
            float attackDamage,
            BurnSettings settings,
            float roll,
            float tickDamageMultiplier = 1f)
        {
            if (target == null || target.IsDead || attackDamage <= 0f || !settings.IsEnabled ||
                roll >= settings.Chance)
            {
                return false;
            }

            GetOrAdd(target).ApplyBurn(source, attackDamage, settings, Time.time, tickDamageMultiplier);
            return true;
        }

        public static bool TryApplyShock(Health target, ShockSettings settings, float roll)
        {
            if (target == null || target.IsDead || !settings.IsEnabled || roll >= settings.Chance)
            {
                return false;
            }

            GetOrAdd(target).ApplyShock(settings, Time.time);
            return true;
        }

        public void ApplyPoison(
            GameObject source,
            float attackDamage,
            PoisonSettings settings,
            float currentTime,
            float tickDamageMultiplier = 1f)
        {
            EnsureReferences();
            if (health.IsDead || !settings.IsEnabled)
            {
                return;
            }

            // The first tick lands one interval after poison starts. Renewing keeps the running tick schedule.
            if (!IsPoisoned)
            {
                NextPoisonTickTime = currentTime + settings.IntervalSeconds;
            }

            poisonSource = source;
            poisonInterval = settings.IntervalSeconds;
            PoisonStacks = Mathf.Min(settings.MaximumStacks, PoisonStacks + 1);
            PoisonTickDamagePerStack = attackDamage * settings.TickDamageRatio * Mathf.Max(0f, tickDamageMultiplier);
            PoisonEndTime = currentTime + settings.DurationSeconds;
            RefreshArtwork();
        }

        public void ApplyBurn(
            GameObject source,
            float attackDamage,
            BurnSettings settings,
            float currentTime,
            float tickDamageMultiplier = 1f)
        {
            EnsureReferences();
            if (health.IsDead || !settings.IsEnabled)
            {
                return;
            }

            if (!IsBurning)
            {
                NextBurnTickTime = currentTime + settings.IntervalSeconds;
            }

            burnSource = source;
            burnInterval = settings.IntervalSeconds;
            IsBurning = true;
            BurnTickDamage = attackDamage * settings.TickDamageRatio * Mathf.Max(0f, tickDamageMultiplier);
            BurnEndTime = currentTime + settings.DurationSeconds;
            RefreshArtwork();
        }

        public void ApplyShock(ShockSettings settings, float currentTime)
        {
            EnsureReferences();
            if (health.IsDead || !settings.IsEnabled)
            {
                return;
            }

            shockSlowPerStack = settings.SlowPerStack;
            ShockStacks = Mathf.Min(settings.MaximumStacks, ShockStacks + 1);
            ShockEndTime = currentTime + settings.DurationSeconds;
            RefreshArtwork();
        }

        public void Tick(float currentTime)
        {
            if (!IsPoisoned && !IsBurning && !IsShocked)
            {
                return;
            }

            // Periodic damage: no critical hit, distance damage, knockback, basic attack hit event or new
            // status effect. The player source keeps the kill credited to the player.
            while (IsPoisoned && !health.IsDead && currentTime >= NextPoisonTickTime &&
                   NextPoisonTickTime <= PoisonEndTime + TickTimeTolerance)
            {
                NextPoisonTickTime += poisonInterval;
                health.TakeDamage(new DamageContext(
                    poisonSource,
                    DamageSourceType.PlayerStatusEffect,
                    PoisonTickDamagePerStack * PoisonStacks,
                    deliveryType: DamageDeliveryType.Periodic));
            }

            while (IsBurning && !health.IsDead && currentTime >= NextBurnTickTime &&
                   NextBurnTickTime <= BurnEndTime + TickTimeTolerance)
            {
                NextBurnTickTime += burnInterval;
                health.TakeDamage(new DamageContext(
                    burnSource,
                    DamageSourceType.PlayerStatusEffect,
                    BurnTickDamage,
                    deliveryType: DamageDeliveryType.Periodic));
            }

            if (health.IsDead)
            {
                Clear();
                return;
            }

            if (IsPoisoned && currentTime >= PoisonEndTime) ClearPoison();
            if (IsBurning && currentTime >= BurnEndTime) ClearBurn();
            if (IsShocked && currentTime >= ShockEndTime) ShockStacks = 0;
            RefreshArtwork();
        }

        public void Clear()
        {
            ClearPoison();
            ClearBurn();
            ShockStacks = 0;
            RefreshArtwork();
        }

        private void ClearPoison()
        {
            PoisonStacks = 0;
            PoisonTickDamagePerStack = 0f;
            poisonSource = null;
        }

        private void ClearBurn()
        {
            IsBurning = false;
            BurnTickDamage = 0f;
            burnSource = null;
        }

        private static EnemyStatusEffects GetOrAdd(Health target)
        {
            EnemyStatusEffects effects = target.GetComponent<EnemyStatusEffects>();
            return effects != null ? effects : target.gameObject.AddComponent<EnemyStatusEffects>();
        }

        private void EnsureReferences()
        {
            if (health == null)
            {
                Awake();
            }
        }

        private void RefreshArtwork()
        {
            var artwork = GetComponent<TrickalFanGame.Frontend.FairyKingdomStatusArtwork>();
            if (artwork != null) artwork.RefreshAt(Time.time);
        }
    }
}
