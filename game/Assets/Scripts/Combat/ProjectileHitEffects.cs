using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Passive-0 §4.8: poison that a basic attack hit may put on an enemy (비비의 콧물).
    public readonly struct PoisonSettings
    {
        public PoisonSettings(
            float chance,
            float tickDamageRatio,
            float durationSeconds,
            float intervalSeconds,
            int maximumStacks)
        {
            Chance = Mathf.Clamp01(chance);
            TickDamageRatio = Mathf.Max(0f, tickDamageRatio);
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            IntervalSeconds = Mathf.Max(0f, intervalSeconds);
            MaximumStacks = Mathf.Max(0, maximumStacks);
        }

        public float Chance { get; }
        // Share of the player's attack damage each stack deals per tick.
        public float TickDamageRatio { get; }
        public float DurationSeconds { get; }
        public float IntervalSeconds { get; }
        public int MaximumStacks { get; }
        public bool IsEnabled => Chance > 0f && TickDamageRatio > 0f && DurationSeconds > 0f &&
                                 IntervalSeconds > 0f && MaximumStacks > 0;
    }

    // Passive-0 §4.8: burn that a basic attack hit may put on an enemy (활활 불타활, 불타는 가지). It never stacks;
    // a new application only renews the duration.
    public readonly struct BurnSettings
    {
        public BurnSettings(float chance, float tickDamageRatio, float durationSeconds, float intervalSeconds)
        {
            Chance = Mathf.Clamp01(chance);
            TickDamageRatio = Mathf.Max(0f, tickDamageRatio);
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            IntervalSeconds = Mathf.Max(0f, intervalSeconds);
        }

        public float Chance { get; }
        // Share of the player's attack damage each tick deals.
        public float TickDamageRatio { get; }
        public float DurationSeconds { get; }
        public float IntervalSeconds { get; }
        public bool IsEnabled => Chance > 0f && TickDamageRatio > 0f && DurationSeconds > 0f &&
                                 IntervalSeconds > 0f;
    }

    // Passive-0 §4.8: shock that a basic attack hit may put on an enemy (앗땃따건). It deals no damage and slows
    // the enemy's ordinary movement per stack.
    public readonly struct ShockSettings
    {
        public ShockSettings(float chance, float slowPerStack, float durationSeconds, int maximumStacks)
        {
            Chance = Mathf.Clamp01(chance);
            SlowPerStack = Mathf.Clamp01(slowPerStack);
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            MaximumStacks = Mathf.Max(0, maximumStacks);
        }

        public float Chance { get; }
        public float SlowPerStack { get; }
        public float DurationSeconds { get; }
        public int MaximumStacks { get; }
        public bool IsEnabled => Chance > 0f && SlowPerStack > 0f && DurationSeconds > 0f && MaximumStacks > 0;
    }

    // Passive-0 §4.4: a basic attack shot that hit an enemy turns toward another enemy (칸타의 팽이).
    public readonly struct ProjectileBounceSettings
    {
        public ProjectileBounceSettings(
            int bounceCount,
            float searchRadius,
            float repeatDamageRatio,
            float sameTargetDelaySeconds)
        {
            BounceCount = Mathf.Max(0, bounceCount);
            SearchRadius = Mathf.Max(0f, searchRadius);
            RepeatDamageRatio = Mathf.Clamp01(repeatDamageRatio);
            SameTargetDelaySeconds = Mathf.Max(0f, sameTargetDelaySeconds);
        }

        public int BounceCount { get; }
        // Measured from the centre of the enemy that was just hit.
        public float SearchRadius { get; }
        // Damage kept for every earlier hit of the same shot on the same enemy (0.5 = 100% → 50% → 25%).
        public float RepeatDamageRatio { get; }
        public float SameTargetDelaySeconds { get; }
        public bool IsEnabled => BounceCount > 0 && SearchRadius > 0f;

        public ProjectileBounceSettings WithBounceCount(int bounceCount) =>
            new(bounceCount, SearchRadius, RepeatDamageRatio, SameTargetDelaySeconds);
    }

    // What a basic attack shot does to an enemy it hits besides the direct damage. Taken at launch, so an item
    // gained afterwards does not change shots already flying (Passive-0 §3).
    public readonly struct ProjectileHitEffects
    {
        private readonly float statusTickDamageBonus;

        public ProjectileHitEffects(
            bool appliesKnockback,
            PoisonSettings poison = default,
            BurnSettings burn = default,
            ShockSettings shock = default,
            float statusTickDamageBonus = 0f,
            float knockbackBonus = 0f)
        {
            AppliesKnockback = appliesKnockback;
            Poison = poison;
            Burn = burn;
            Shock = shock;
            this.statusTickDamageBonus = Mathf.Max(0f, statusTickDamageBonus);
            KnockbackBonus = Mathf.Max(0f, knockbackBonus);
        }

        public bool AppliesKnockback { get; }
        public PoisonSettings Poison { get; }
        public BurnSettings Burn { get; }
        public ShockSettings Shock { get; }
        // Multiplies the tick damage of poison and burn this shot applies (앗따검, 탐욕의 반지).
        public float StatusTickDamageMultiplier => 1f + statusTickDamageBonus;
        // Knockback bonus of the Passive-0 §4.6 formula (슈슈슈슉 글러브).
        public float KnockbackBonus { get; }
    }
}
