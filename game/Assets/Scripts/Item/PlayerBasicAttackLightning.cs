using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(PlayerCombatEvents))]
    public sealed class PlayerBasicAttackLightning : MonoBehaviour
    {
        private PlayerStats stats;
        private PlayerCombatEvents combatEvents;
        private int requiredHitCount;
        private float damageMultiplier;

        public int HitProgress { get; private set; }
        public int TriggerCount { get; private set; }
        public bool IsConfigured => requiredHitCount > 0 && damageMultiplier > 0f;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            combatEvents = GetComponent<PlayerCombatEvents>();
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            if (combatEvents != null) combatEvents.BasicAttackHit -= HandleBasicAttackHit;
        }

        public void Configure(int configuredRequiredHitCount, float configuredDamageMultiplier)
        {
            if (IsConfigured || configuredRequiredHitCount <= 0 || configuredDamageMultiplier <= 0f)
            {
                return;
            }

            requiredHitCount = configuredRequiredHitCount;
            damageMultiplier = configuredDamageMultiplier;
            if (stats == null) stats = GetComponent<PlayerStats>();
            BindEvents();
        }

        private void HandleBasicAttackHit(PlayerBasicAttackHitEvent hitEvent)
        {
            if (!IsConfigured || hitEvent.Target == null) return;

            HitProgress++;
            if (HitProgress < requiredHitCount) return;

            HitProgress = 0;
            TriggerCount++;
            if (hitEvent.Target.IsDead) return;
            if (stats == null) stats = GetComponent<PlayerStats>();
            TrickalFanGame.Frontend.FairyKingdomSpriteEffect.Play("effect-lightning",
                hitEvent.Target.transform.position + Vector3.up * 0.35f, 0.8f, 0.2f,
                hitEvent.Target.GetComponent<SpriteRenderer>());
            hitEvent.Target.TakeDamage(new DamageContext(
                gameObject,
                DamageSourceType.PlayerItemLightning,
                stats.AttackDamage,
                damageMultiplier,
                DamageDeliveryType.Direct));
        }

        private void BindEvents()
        {
            if (combatEvents == null) combatEvents = GetComponent<PlayerCombatEvents>();
            if (combatEvents == null) return;
            combatEvents.BasicAttackHit -= HandleBasicAttackHit;
            combatEvents.BasicAttackHit += HandleBasicAttackHit;
        }
    }
}
