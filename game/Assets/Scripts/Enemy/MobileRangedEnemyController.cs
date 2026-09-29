using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class MobileRangedEnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.25f;
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0f)] private float preferredDistance = 5.6f;
        [SerializeField, Min(0f)] private float distanceTolerance = 0.8f;
        [SerializeField, Range(0f, 1f)] private float radialOrbitWeight = 0.35f;

        [Header("Burst Attack")]
        [SerializeField, Min(0.01f)] private float shotInterval = 0.4f;
        [SerializeField, Min(0f)] private float burstRestDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] private float fourShotChance = 0.4f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 6.5f;
        [SerializeField] private EnemyDamageTier projectileDamageTier = EnemyDamageTier.Medium;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 5f;
        [SerializeField, Min(0f)] private float maximumPredictionTime = 0.25f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private SpriteRenderer spriteRenderer;
        private readonly EnemyObstacleNavigator navigator = new();
        private float bodyRadius;
        private float nextShotTime;
        private float presentationEndsAt;
        private int shotsRemaining;
        private int orbitDirection = 1;

        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float PreferredDistance => preferredDistance;
        public float ShotInterval => shotInterval;
        public float BurstRestDuration => burstRestDuration;
        public float FourShotChance => fourShotChance;
        public float ProjectileSpeed => projectileSpeed;
        public float MaximumPredictionTime => maximumPredictionTime;
        public EnemyDamageTier ProjectileDamageTier => projectileDamageTier;
        public int ShotsRemaining => shotsRemaining;
        public int OrbitDirection => orbitDirection;
        public bool IsActionSuppressed => behavior == null || behavior.IsActionSuppressed;

        public event Action<EnemyProjectile> ProjectileFired;

        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            behavior.TryAcquireOrAlert(detectionRange);
        }

        private void FixedUpdate()
        {
            TickBehavior(Time.time);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null) return;
            bool blocked = collision.collider.gameObject.layer == LayerMask.NameToLayer("Environment") ||
                           collision.collider.GetComponentInParent<EnemyBehaviorContext>() != null;
            if (blocked) NotifyBlocked();
        }

        private void OnDisable()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            shotsRemaining = 0;
            nextShotTime = 0f;
            presentation?.SetPhase(EnemyAttackPhase.Idle);
        }

        public void Configure(float configuredMoveSpeed, float configuredDetectionRange,
            float configuredPreferredDistance, float configuredDistanceTolerance,
            float configuredRadialOrbitWeight, float configuredShotInterval,
            float configuredBurstRestDuration, float configuredFourShotChance,
            float configuredProjectileSpeed, EnemyDamageTier configuredProjectileDamageTier,
            float configuredProjectileLifetime, float configuredMaximumPredictionTime)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            preferredDistance = Mathf.Clamp(configuredPreferredDistance, 0f, detectionRange);
            distanceTolerance = Mathf.Clamp(configuredDistanceTolerance, 0f, preferredDistance);
            radialOrbitWeight = Mathf.Clamp01(configuredRadialOrbitWeight);
            shotInterval = Mathf.Max(0.01f, configuredShotInterval);
            burstRestDuration = Mathf.Max(0f, configuredBurstRestDuration);
            fourShotChance = Mathf.Clamp01(configuredFourShotChance);
            projectileSpeed = Mathf.Max(0.01f, configuredProjectileSpeed);
            projectileDamageTier = configuredProjectileDamageTier;
            projectileLifetime = Mathf.Max(0.01f, configuredProjectileLifetime);
            maximumPredictionTime = Mathf.Max(0f, configuredMaximumPredictionTime);
            shotsRemaining = 0;
            nextShotTime = 0f;
            orbitDirection = 1;
        }

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            behavior?.SetTarget(configuredTarget);
        }

        public void SetProjectileDamageTier(EnemyDamageTier configuredProjectileDamageTier)
        {
            projectileDamageTier = configuredProjectileDamageTier;
        }

        public void TickBehavior(float currentTime)
        {
            if (body == null || health == null || knockback == null) CacheComponents();
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

            if (IsBurstResting(currentTime))
            {
                body.linearVelocity = Vector2.zero;
                presentation?.SetPhase(EnemyAttackPhase.Recovery);
                return;
            }

            if (currentTime >= presentationEndsAt)
                presentation?.SetPhase(EnemyAttackPhase.Idle);

            bool hasLineOfFire = TickMovement(currentTime);
            if (hasLineOfFire) TickBurst(currentTime);
        }

        public void NotifyBlocked()
        {
            orbitDirection *= -1;
        }

        public bool IsBurstResting(float currentTime)
        {
            return shotsRemaining <= 0 && currentTime < nextShotTime;
        }

        public static int SelectBurstShotCount(float roll, float configuredFourShotChance = 0.4f)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(configuredFourShotChance) ? 4 : 3;
        }

        public static Vector2 CalculatePredictiveDirection(Vector2 shooterPosition, Vector2 targetPosition,
            Vector2 targetVelocity, float configuredProjectileSpeed, float configuredMaximumPredictionTime)
        {
            Vector2 offset = targetPosition - shooterPosition;
            if (offset.sqrMagnitude <= 0.001f) return Vector2.right;

            float speed = Mathf.Max(0.01f, configuredProjectileSpeed);
            float a = targetVelocity.sqrMagnitude - speed * speed;
            float b = 2f * Vector2.Dot(offset, targetVelocity);
            float c = offset.sqrMagnitude;
            float interceptTime = 0f;
            if (Mathf.Abs(a) <= 0.0001f)
            {
                if (Mathf.Abs(b) > 0.0001f) interceptTime = -c / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0f)
                {
                    float root = Mathf.Sqrt(discriminant);
                    float first = (-b - root) / (2f * a);
                    float second = (-b + root) / (2f * a);
                    interceptTime = SmallestPositive(first, second);
                }
            }

            interceptTime = Mathf.Clamp(interceptTime, 0f, Mathf.Max(0f, configuredMaximumPredictionTime));
            Vector2 predictedOffset = offset + targetVelocity * interceptTime;
            return predictedOffset.sqrMagnitude > 0.001f ? predictedOffset.normalized : offset.normalized;
        }

        private bool TickMovement(float currentTime)
        {
            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                body.linearVelocity = Vector2.zero;
                return false;
            }

            bool hasLineOfFire = EnemyObstacleNavigator.HasLineOfFire(transform.position, target.position);
            if (!hasLineOfFire)
            {
                body.linearVelocity = navigator.GetMoveDirection(transform.position, target.position, bodyRadius,
                    currentTime) * moveSpeed;
                return false;
            }

            Vector2 direction = offset / distance;
            Vector2 tangent = new(-direction.y, direction.x);
            tangent *= orbitDirection;
            Vector2 radial = Vector2.zero;
            if (distance > preferredDistance + distanceTolerance)
                radial = direction;
            else if (distance < preferredDistance - distanceTolerance &&
                     EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, -direction, bodyRadius,
                         out Vector2 retreat))
                radial = retreat;

            Vector2 movement = radial == Vector2.zero
                ? tangent
                : radial + tangent * radialOrbitWeight;
            body.linearVelocity = movement.sqrMagnitude > 0.001f
                ? movement.normalized * moveSpeed
                : Vector2.zero;
            return true;
        }

        private void TickBurst(float currentTime)
        {
            if (currentTime < nextShotTime) return;
            if (shotsRemaining <= 0)
                shotsRemaining = SelectBurstShotCount(UnityEngine.Random.value, fourShotChance);

            FirePredictive(currentTime);
            shotsRemaining--;
            nextShotTime = currentTime + (shotsRemaining > 0 ? shotInterval : burstRestDuration);
            if (shotsRemaining <= 0) body.linearVelocity = Vector2.zero;
        }

        private void FirePredictive(float currentTime)
        {
            Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
            Vector2 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
            Vector2 direction = CalculatePredictiveDirection(transform.position, target.position, targetVelocity,
                projectileSpeed, maximumPredictionTime);
            Vector2 predictedPoint = (Vector2)transform.position + direction *
                Vector2.Distance(transform.position, target.position);
            if (!EnemyObstacleNavigator.HasLineOfFire(transform.position, predictedPoint))
                direction = ((Vector2)target.position - (Vector2)transform.position).normalized;

            EnemyProjectile projectile = EnemyProjectile.Create(transform.position, direction, gameObject,
                projectileDamageTier, projectileSpeed, projectileLifetime,
                spriteRenderer != null ? spriteRenderer.sprite : null);
            presentation?.SetPhase(EnemyAttackPhase.Active);
            presentationEndsAt = currentTime + Mathf.Min(0.08f, shotInterval);
            ProjectileFired?.Invoke(projectile);
        }

        private static float SmallestPositive(float first, float second)
        {
            bool firstPositive = first > 0f;
            bool secondPositive = second > 0f;
            if (firstPositive && secondPositive) return Mathf.Min(first, second);
            if (firstPositive) return first;
            return secondPositive ? second : 0f;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            bodyRadius = EnemyObstacleNavigator.ResolveBodyRadius(gameObject);
            behavior = GetComponent<EnemyBehaviorContext>();
            if (behavior == null) behavior = gameObject.AddComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            if (target != null) behavior.SetTarget(target);
            presentation = GetComponent<EnemyAttackPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<EnemyAttackPresentation>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
