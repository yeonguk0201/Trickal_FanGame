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
        SaemaeumApproachThrow,
        SaemaeumJumpSequence,
        SaemaeumTreasureHeal,
        CrayonHeroMapSlash,
        CrayonHeroSummonMinions,
        CrayonHeroApproachSwing,
        CrayonHeroDashChain,
    }

    [Serializable]
    public sealed class BossPatternDefinition
    {
        [SerializeField] private string patternId = "boss-pattern";
        [SerializeField] private BossPatternExecution execution = BossPatternExecution.SignalOnly;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float recoveryDuration = 0.5f;
        [SerializeField, Min(0f)] private float reuseCooldown = 1f;

        public BossPatternDefinition(string patternId, BossPatternExecution execution,
            float telegraphDuration, float activeDuration, float recoveryDuration, float reuseCooldown)
        {
            this.patternId = string.IsNullOrWhiteSpace(patternId) ? "boss-pattern" : patternId.Trim();
            this.execution = execution;
            this.telegraphDuration = Mathf.Max(0.01f, telegraphDuration);
            this.activeDuration = Mathf.Max(0.01f, activeDuration);
            this.recoveryDuration = Mathf.Max(0.01f, recoveryDuration);
            this.reuseCooldown = Mathf.Max(0f, reuseCooldown);
        }

        public string PatternId => string.IsNullOrWhiteSpace(patternId) ? "boss-pattern" : patternId.Trim();
        public BossPatternExecution Execution => execution;
        public float TelegraphDuration => Mathf.Max(0.01f, telegraphDuration);
        public float ActiveDuration => Mathf.Max(0.01f, activeDuration);
        public float RecoveryDuration => Mathf.Max(0.01f, recoveryDuration);
        public float ReuseCooldown => Mathf.Max(0f, reuseCooldown);
    }
}
