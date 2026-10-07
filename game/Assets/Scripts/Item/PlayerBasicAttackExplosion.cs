using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 폭발 머핀: every N basic attack hits explode at the hit enemy. The explosion is direct artifact damage: it
    // does not count as a basic attack hit and carries no status effect or knockback. Its hit count is separate
    // from 날씨는 맑음 카드.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(PlayerCombatEvents))]
    public sealed class PlayerBasicAttackExplosion : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayers = 1 << 6;

        private readonly HashSet<Health> explosionTargets = new();
        private PlayerStats stats;
        private PlayerCombatEvents combatEvents;
        private int requiredHitCount;
        private float damageMultiplier;
        private float radius;

        public int HitProgress { get; private set; }
        public int TriggerCount { get; private set; }
        public float Radius => radius;
        public bool IsConfigured => requiredHitCount > 0 && damageMultiplier > 0f && radius > 0f;

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            if (combatEvents != null) combatEvents.BasicAttackHit -= HandleBasicAttackHit;
        }

        public void Configure(int configuredRequiredHitCount, float configuredDamageMultiplier, float configuredRadius)
        {
            if (IsConfigured || configuredRequiredHitCount <= 0 || configuredDamageMultiplier <= 0f ||
                configuredRadius <= 0f)
            {
                return;
            }

            requiredHitCount = configuredRequiredHitCount;
            damageMultiplier = configuredDamageMultiplier;
            radius = configuredRadius;
            BindEvents();
        }

        private void HandleBasicAttackHit(PlayerBasicAttackHitEvent hitEvent)
        {
            if (!IsConfigured || hitEvent.Target == null) return;

            HitProgress++;
            if (HitProgress < requiredHitCount) return;

            HitProgress = 0;
            TriggerCount++;
            Explode(hitEvent.Target.transform.position);
        }

        private void Explode(Vector2 center)
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            CombatSpriteEffect.Play("circular-explosion", center, radius * 2.2f, radius * 2.2f, 0.4f, transform,
                GetComponentInChildren<SpriteRenderer>());
            explosionTargets.Clear();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius, targetLayers))
            {
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || target.gameObject == gameObject || target.IsDead ||
                    target.GetComponent<PlayerCombatEvents>() != null || !explosionTargets.Add(target))
                {
                    continue;
                }

                target.TakeDamage(new DamageContext(
                    gameObject,
                    DamageSourceType.PlayerItemExplosion,
                    stats.AttackDamage,
                    damageMultiplier,
                    DamageDeliveryType.Direct));
            }
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
