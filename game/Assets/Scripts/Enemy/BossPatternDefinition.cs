using System;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum BossPatternExecution
    {
        SignalOnly,
        AimedProjectile,
        BuseureogiApproachVolley,
        BuseureogiSummonMinions,
        BuseureogiThrowObstacle,
        // Walks toward the player. Its fan throw was removed (2026-11-18) and came back as a three-shot fan
        // (2026-11-21); the serialized value is unchanged.
        SaemaeumApproachWalk,
        SaemaeumJumpSequence,
        SaemaeumTreasureHeal,
        CrayonHeroMapSlash,
        CrayonHeroSummonMinions,
        CrayonHeroApproachSwing,
        CrayonHeroDashChain,
        SaemaeumSlimeScatter,
        SaemaeumGoldPillars,
        SaemaeumGoldMeteor,
        // 2026-11-21: phase two rings the player with gold pillars; the jump sequence always follows.
        SaemaeumGoldCage,
        // 2026-11-21: stands still and sweeps rows of gold pillars across the floor (line, # or three-way fan).
        SaemaeumPillarWave,
    }

    [Serializable]
    public sealed class BossPatternDefinition
    {
        [SerializeField] private string patternId = "boss-pattern";
        [SerializeField] private BossPatternExecution execution = BossPatternExecution.SignalOnly;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.5f;
        // Fixed warning time remains the same when a later phase speeds up the pattern.
        [SerializeField, Min(0f)] private float additionalTelegraphDuration;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float recoveryDuration = 0.5f;
        [SerializeField, Min(0f)] private float reuseCooldown = 1f;

        public BossPatternDefinition(string patternId, BossPatternExecution execution,
            float telegraphDuration, float activeDuration, float recoveryDuration, float reuseCooldown,
            float additionalTelegraphDuration = 0f)
        {
            this.patternId = string.IsNullOrWhiteSpace(patternId) ? "boss-pattern" : patternId.Trim();
            this.execution = execution;
            this.telegraphDuration = Mathf.Max(0.01f, telegraphDuration);
            this.additionalTelegraphDuration = Mathf.Max(0f, additionalTelegraphDuration);
            this.activeDuration = Mathf.Max(0.01f, activeDuration);
            this.recoveryDuration = Mathf.Max(0.01f, recoveryDuration);
            this.reuseCooldown = Mathf.Max(0f, reuseCooldown);
        }

        public string PatternId => string.IsNullOrWhiteSpace(patternId) ? "boss-pattern" : patternId.Trim();
        public BossPatternExecution Execution => execution;
        public float TelegraphDuration => Mathf.Max(0.01f, telegraphDuration);
        public float GetTelegraphDuration(float tempoMultiplier) =>
            Mathf.Max(0.01f, TelegraphDuration * tempoMultiplier + Mathf.Max(0f, additionalTelegraphDuration));
        public float ActiveDuration => Mathf.Max(0.01f, activeDuration);
        public float RecoveryDuration => Mathf.Max(0.01f, recoveryDuration);
        public float ReuseCooldown => Mathf.Max(0f, reuseCooldown);
    }
}
