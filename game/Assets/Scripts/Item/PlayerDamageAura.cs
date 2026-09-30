using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerCombatEvents))]
    public sealed class PlayerDamageAura : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayers = 1 << 6;

        private readonly HashSet<Health> tickTargets = new();
        private Health ownerHealth;
        private RunSession runSession;
        private float maxHealthDamagePercent;
        private float radius;
        private float intervalSeconds;
        private float nextTickTime;

        public int StackCount { get; private set; }
        public float MaxHealthDamagePercent => maxHealthDamagePercent;
        public float Radius => radius;
        public float IntervalSeconds => intervalSeconds;
        public float NextTickTime => nextTickTime;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            Tick(Time.time);
        }

        public bool AddStack(float damagePercent, float configuredRadius, float configuredIntervalSeconds)
        {
            if (damagePercent <= 0f || configuredRadius <= 0f || configuredIntervalSeconds <= 0f)
            {
                return false;
            }

            EnsureReferences();
            maxHealthDamagePercent += damagePercent;
            radius = Mathf.Max(radius, configuredRadius);
            intervalSeconds = intervalSeconds > 0f
                ? Mathf.Min(intervalSeconds, configuredIntervalSeconds)
                : configuredIntervalSeconds;
            StackCount++;
            if (StackCount == 1)
            {
                nextTickTime = Time.time + intervalSeconds;
            }

            return true;
        }

        public void BindRunSession(RunSession configuredRunSession)
        {
            runSession = configuredRunSession;
        }

        public bool Tick(float currentTime)
        {
            EnsureReferences();
            if (!isActiveAndEnabled || StackCount <= 0 || ownerHealth == null || ownerHealth.IsDead ||
                intervalSeconds <= 0f || currentTime < nextTickTime ||
                (runSession != null && runSession.HasEnded))
            {
                return false;
            }

            nextTickTime = currentTime + intervalSeconds;
            float damage = ownerHealth.MaxHealth * maxHealthDamagePercent;
            if (damage <= 0f)
            {
                return true;
            }

            tickTargets.Clear();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, radius, targetLayers))
            {
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || target == ownerHealth || target.IsDead ||
                    !target.gameObject.activeInHierarchy ||
                    target.GetComponent<PlayerCombatEvents>() != null ||
                    !tickTargets.Add(target))
                {
                    continue;
                }

                target.TakeDamage(new DamageContext(
                    gameObject,
                    DamageSourceType.PlayerDamageAura,
                    damage,
                    deliveryType: DamageDeliveryType.Periodic));
            }

            return true;
        }

        private void EnsureReferences()
        {
            if (ownerHealth == null)
            {
                ownerHealth = GetComponent<Health>();
            }

            if (runSession == null)
            {
                runSession = FindFirstObjectByType<RunSession>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (radius <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(0.8f, 0.2f, 0.5f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
