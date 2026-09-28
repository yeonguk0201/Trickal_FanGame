using System;
using TrickalFanGame.Combat;
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
        [SerializeField] private EnemyDamageTier projectileDamageTier = EnemyDamageTier.Heavy;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 4f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private float nextAttackTime;

        public bool IsActionSuppressed => behavior == null || behavior.IsActionSuppressed;
        public float NextAttackTime => nextAttackTime;
        public float MoveSpeed => moveSpeed;
        public float MinimumAttackDistance => minimumAttackDistance;
        public float MaximumAttackDistance => maximumAttackDistance;
        public EnemyDamageTier ProjectileDamageTier => projectileDamageTier;

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
            EnemyDamageTier configuredProjectileDamageTier,
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
            projectileDamageTier = configuredProjectileDamageTier;
            projectileLifetime = Mathf.Max(0.01f, configuredProjectileLifetime);
        }

        public void SetProjectileDamageTier(EnemyDamageTier configuredProjectileDamageTier)
        {
            projectileDamageTier = configuredProjectileDamageTier;
        }

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            behavior?.SetTarget(configuredTarget);
        }

        public void TickBehavior(float currentTime)
        {
            if (body == null || health == null || knockback == null)
            {
                CacheComponents();
            }

            if (!behavior.TryAcquireOrAlert(detectionRange))
            {
                behavior.StopForSuppression();
                return;
            }

            target = behavior.Target;
            if (behavior.IsMovementSuppressed)
            {
                behavior.StopForSuppression();
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
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
            if (IsActionSuppressed ||
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
                projectileDamageTier,
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
            behavior = GetComponent<EnemyBehaviorContext>();
            if (behavior == null)
            {
                behavior = gameObject.AddComponent<EnemyBehaviorContext>();
            }
            behavior.Initialize();
            if (target != null)
            {
                behavior.SetTarget(target);
            }
        }

        private bool FindTargetIfNeeded()
        {
            return behavior != null && behavior.TryAcquireOrAlert(detectionRange);
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
