using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Health))]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 4f;
        [SerializeField, Min(1)] private int projectileDamage = 2;

        private Health health;
        private Transform player;
        private float nextAttackTime;
        public event Action Died;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (health.IsDead || Time.time < nextAttackTime) return;
            if (player == null) player = FindFirstObjectByType<PlayerMovement>()?.transform;
            if (player == null) return;

            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            BossProjectile.Create(transform.position, direction * projectileSpeed, projectileDamage,
                GetComponent<SpriteRenderer>()?.sprite);
            nextAttackTime = Time.time + attackInterval;
        }

        private void OnDied() => Died?.Invoke();
    }
}
