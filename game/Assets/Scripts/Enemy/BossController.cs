using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Health), typeof(KnockbackReceiver))]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 4f;
        [SerializeField, Min(0.01f)] private float projectileDamage = 2f;

        private Health health;
        private KnockbackReceiver knockback;
        private Transform player;
        private float nextAttackTime;
        public event Action Died;

        public float ProjectileDamage => projectileDamage;

        public bool IsActionSuppressed =>
            health != null && (health.IsDead || (knockback != null && knockback.IsActive));

        public void SetProjectileDamage(float configuredProjectileDamage)
        {
            projectileDamage = Mathf.Max(0.01f, configuredProjectileDamage);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (IsActionSuppressed || Time.time < nextAttackTime) return;
            if (player == null) player = FindFirstObjectByType<PlayerMovement>()?.transform;
            if (player == null) return;

            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            BossProjectile.Create(transform.position, direction * projectileSpeed, gameObject, projectileDamage,
                GetComponent<SpriteRenderer>()?.sprite);
            nextAttackTime = Time.time + attackInterval;
        }

        private void OnDied() => Died?.Invoke();
    }
}
