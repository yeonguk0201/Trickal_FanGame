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

    [DisallowMultipleComponent]
    public sealed class PlayerCombatEvents : MonoBehaviour
    {
        private readonly HashSet<int> reportedTargetIds = new();

        public event Action<PlayerEnemyKilledEvent> EnemyKilled;

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
                   sourceType == DamageSourceType.PlayerUltimateImpact;
        }
    }
}
