using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class RangedEnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float detectionRange = 8f;
        [SerializeField, Min(0f)] private float minimumAttackDistance = 3f;
        [SerializeField, Min(0f)] private float maximumAttackDistance = 6f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float attackInterval = 1.5f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
        [SerializeField, Min(1)] private int projectileDamage = 1;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 4f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private Health targetHealth;
        private KnockbackReceiver knockback;
        private float nextAttackTime;

        public bool IsActionSuppressed => health == null || health.IsDead ||
                                          (knockback != null && knockback.IsActive);
        public float NextAttackTime => nextAttackTime;
        public float MoveSpeed => moveSpeed;
        public float MinimumAttackDistance => minimumAttackDistance;
        public float MaximumAttackDistance => maximumAttackDistance;
        public int ProjectileDamage => projectileDamage;

        public event Action<EnemyProjectile> ProjectileFired;

        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            FindTargetIfNeeded();
        }

        private void FixedUpdate()
        {
            TickBehavior(Time.time);
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void Configure(
            float configuredMoveSpeed,
            float configuredDetectionRange,
            float configuredMinimumAttackDistance,
            float configuredMaximumAttackDistance,
            float configuredAttackInterval,
            float configuredProjectileSpeed,
            int configuredProjectileDamage,
            float configuredProjectileLifetime)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            minimumAttackDistance = Mathf.Clamp(configuredMinimumAttackDistance, 0f, detectionRange);
            maximumAttackDistance = Mathf.Clamp(
                configuredMaximumAttackDistance,
                minimumAttackDistance,
                detectionRange);
            attackInterval = Mathf.Max(0.01f, configuredAttackInterval);
            projectileSpeed = Mathf.Max(0.01f, configuredProjectileSpeed);
            projectileDamage = Mathf.Max(1, configuredProjectileDamage);
            projectileLifetime = Mathf.Max(0.01f, configuredProjectileLifetime);
        }

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            targetHealth = target != null ? target.GetComponent<Health>() : null;
        }

        public void TickBehavior(float currentTime)
        {
            if (body == null || health == null || knockback == null)
            {
                CacheComponents();
            }

            if (knockback.IsActive)
            {
                if (!knockback.IsKnockedBack)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (health.IsDead || !FindTargetIfNeeded() || targetHealth.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            if (distance > detectionRange || distance <= 0.001f)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 direction = offset / distance;
            if (distance > maximumAttackDistance)
            {
                body.linearVelocity = direction * moveSpeed;
                return;
            }

            if (distance < minimumAttackDistance)
            {
                body.linearVelocity = -direction * moveSpeed;
                return;
            }

            body.linearVelocity = Vector2.zero;
            TryFire(direction, currentTime);
        }

        public EnemyProjectile TryFire(Vector2 direction, float currentTime)
        {
            if (IsActionSuppressed || targetHealth == null || targetHealth.IsDead ||
                currentTime < nextAttackTime || direction.sqrMagnitude <= 0.001f)
            {
                return null;
            }

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            Sprite projectileSprite = renderer != null ? renderer.sprite : null;
            EnemyProjectile projectile = EnemyProjectile.Create(
                transform.position,
                direction,
                gameObject,
                projectileDamage,
                projectileSpeed,
                projectileLifetime,
                projectileSprite);
            nextAttackTime = currentTime + attackInterval;
            ProjectileFired?.Invoke(projectile);
            return projectile;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
        }

        private bool FindTargetIfNeeded()
        {
            if (target != null && targetHealth != null)
            {
                return true;
            }

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                return false;
            }

            SetTarget(player.transform);
            return targetHealth != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minimumAttackDistance);
            Gizmos.DrawWireSphere(transform.position, maximumAttackDistance);
        }
    }
}
