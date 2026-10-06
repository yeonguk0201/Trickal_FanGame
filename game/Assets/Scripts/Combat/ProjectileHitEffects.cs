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

    // What a basic attack shot does to an enemy it hits besides the direct damage. Taken at launch, so an item
    // gained afterwards does not change shots already flying (Passive-0 §3).
    public readonly struct ProjectileHitEffects
    {
        public ProjectileHitEffects(bool appliesKnockback, PoisonSettings poison = default)
        {
            AppliesKnockback = appliesKnockback;
            Poison = poison;
        }

        public bool AppliesKnockback { get; }
        public PoisonSettings Poison { get; }
    }
}
