using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public readonly struct PlayerEnemyKilledEvent
    {
        public PlayerEnemyKilledEvent(Health target, DamageContext killingBlow)
        {
            Target = target;
            KillingBlow = killingBlow;
        }

        public Health Target { get; }
        public DamageContext KillingBlow { get; }
    }

    public readonly struct PlayerBasicAttackHitEvent
    {
        public PlayerBasicAttackHitEvent(Health target, DamageContext damage, float appliedDamage)
        {
            Target = target;
            Damage = damage;
            AppliedDamage = appliedDamage;
        }

        public Health Target { get; }
        public DamageContext Damage { get; }
        public float AppliedDamage { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerCombatEvents : MonoBehaviour
    {
        private readonly HashSet<int> reportedTargetIds = new();

        public event Action<PlayerEnemyKilledEvent> EnemyKilled;
        public event Action<PlayerBasicAttackHitEvent> BasicAttackHit;

        internal bool TryReportBasicAttackHit(Health target, DamageContext damage, float appliedDamage)
        {
            if (target == null || target.gameObject == gameObject || appliedDamage <= 0f ||
                damage.Source != gameObject ||
                damage.SourceType is not (DamageSourceType.PlayerAttack or DamageSourceType.PlayerProjectile))
            {
                return false;
            }

            BasicAttackHit?.Invoke(new PlayerBasicAttackHitEvent(target, damage, appliedDamage));
            return true;
        }

        internal bool TryReportEnemyKilled(Health target, DamageContext killingBlow)
        {
            if (target == null || target.gameObject == gameObject || !target.IsDead ||
                killingBlow.Source != gameObject ||
                !IsPlayerDamage(killingBlow.SourceType) ||
                !reportedTargetIds.Add(target.GetInstanceID()))
            {
                return false;
            }

            EnemyKilled?.Invoke(new PlayerEnemyKilledEvent(target, killingBlow));
            return true;
        }

        private static bool IsPlayerDamage(DamageSourceType sourceType)
        {
            return sourceType == DamageSourceType.PlayerProjectile ||
                   sourceType == DamageSourceType.PlayerAttack ||
                   sourceType == DamageSourceType.PlayerSkillExplosion ||
                   sourceType == DamageSourceType.PlayerUltimateImpact ||
                   sourceType == DamageSourceType.PlayerDamageAura ||
                   sourceType == DamageSourceType.PlayerItemLightning;
        }
    }
}
