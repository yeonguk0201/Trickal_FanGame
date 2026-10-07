using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 슈슈슈슉 글러브: a kill adds a stack and renews the duration. When the duration runs out every stack ends
    // and the cooldown starts; kills during the cooldown do not count.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(PlayerCombatEvents))]
    public sealed class PlayerKillFrenzy : MonoBehaviour
    {
        private PlayerStats stats;
        private PlayerCombatEvents combatEvents;
        private float basicAttackDamagePerStack;
        private float attackSpeedPerStack;
        private float knockbackPerStack;
        private int maximumStacks;
        private float durationSeconds;
        private float cooldownSeconds;

        public int Stacks { get; private set; }
        public float EndTime { get; private set; }
        public float CooldownEndTime { get; private set; } = float.NegativeInfinity;
        public bool IsConfigured => maximumStacks > 0 && durationSeconds > 0f;

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            if (combatEvents != null) combatEvents.EnemyKilled -= HandleEnemyKilled;
            End(float.NegativeInfinity);
        }

        private void Update()
        {
            Tick(Time.time);
        }

        public void Configure(
            float configuredBasicAttackDamagePerStack,
            float configuredAttackSpeedPerStack,
            int configuredMaximumStacks,
            float configuredDurationSeconds,
            float configuredCooldownSeconds)
        {
            if (IsConfigured || configuredMaximumStacks <= 0 || configuredDurationSeconds <= 0f)
            {
                return;
            }

            basicAttackDamagePerStack = Mathf.Max(0f, configuredBasicAttackDamagePerStack);
            attackSpeedPerStack = Mathf.Max(0f, configuredAttackSpeedPerStack);
            maximumStacks = configuredMaximumStacks;
            durationSeconds = configuredDurationSeconds;
            cooldownSeconds = Mathf.Max(0f, configuredCooldownSeconds);
            BindEvents();
        }

        public void AddKnockbackPerStack(float amount)
        {
            knockbackPerStack += Mathf.Max(0f, amount);
            ApplyBonus();
        }

        // Returns true when the kill added a stack or renewed the duration.
        public bool RegisterKill(float currentTime)
        {
            Tick(currentTime);
            if (!IsConfigured || currentTime < CooldownEndTime)
            {
                return false;
            }

            Stacks = Mathf.Min(maximumStacks, Stacks + 1);
            EndTime = currentTime + durationSeconds;
            ApplyBonus();
            return true;
        }

        public void Tick(float currentTime)
        {
            if (Stacks > 0 && currentTime >= EndTime)
            {
                End(EndTime + cooldownSeconds);
            }
        }

        private void End(float cooldownEndTime)
        {
            if (Stacks <= 0)
            {
                return;
            }

            Stacks = 0;
            CooldownEndTime = cooldownEndTime;
            ApplyBonus();
        }

        private void ApplyBonus()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (stats == null) return;
            stats.SetKillFrenzyBonus(basicAttackDamagePerStack * Stacks, attackSpeedPerStack * Stacks,
                knockbackPerStack * Stacks);
        }

        private void HandleEnemyKilled(PlayerEnemyKilledEvent killEvent)
        {
            RegisterKill(Time.time);
        }

        private void BindEvents()
        {
            if (combatEvents == null) combatEvents = GetComponent<PlayerCombatEvents>();
            if (combatEvents == null) return;
            combatEvents.EnemyKilled -= HandleEnemyKilled;
            combatEvents.EnemyKilled += HandleEnemyKilled;
        }
    }
}
