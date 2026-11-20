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
        private readonly RangedStanceKeeper stance = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private float nextShotTime;
        private float presentationEndsAt;
        private int shotsRemaining;
        private int orbitDirection = 1;
        private float lastOrbitReverseTime = float.NegativeInfinity;

        public const float OrbitReverseCooldown = 0.6f;
        // How far ahead an orbit step is checked for walls and for keeping the line of fire.
        public const float OrbitProbeDistance = 0.6f;

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
            if (collision == null || collision.collider == null || body == null) return;
            bool blocked = ((1 << collision.collider.gameObject.layer) & EnemyObstacleNavigator.ObstacleMask) != 0 ||
                           collision.collider.GetComponentInParent<EnemyBehaviorContext>() != null;
            if (!blocked || collision.contactCount == 0) return;
            // Only a contact that stops the orbit itself reverses it; sliding along a wall or brushing a neighbour
            // re-enters contact every few frames and must not flip the enemy back and forth.
            Vector2 moving = body.linearVelocity;
            if (moving.sqrMagnitude <= 0.001f ||
                Vector2.Dot(moving.normalized, collision.GetContact(0).normal) > -0.5f) return;
            TryReverseOrbit(Time.time);
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
            lastOrbitReverseTime = float.NegativeInfinity;
            stance.Reset();
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

        // Orbit reversals from contacts or blocked steps wait out a cooldown so the enemy cannot shuffle in place.
        private bool TryReverseOrbit(float currentTime)
        {
            if (currentTime < lastOrbitReverseTime + OrbitReverseCooldown) return false;
            lastOrbitReverseTime = currentTime;
            NotifyBlocked();
            return true;
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

            // Movement judges the same shot the burst will fire (Sansamo's flower, not its feet), so the enemy does
            // not settle where it believes it can shoot but every shot is refused.
            GetShotOrigins(out Vector2 root, out Vector2 origin, out Vector2 mirroredOrigin);
            bool hasSight = HasShotLine(Vector2.zero, root, origin, mirroredOrigin);
            if (stance.ShouldSeekSight(hasSight, currentTime))
            {
                body.linearVelocity = navigator.GetMoveDirection(transform.position, target.position, terrain,
                    currentTime) * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
                return false;
            }

            if (!hasSight)
            {
                // Just lost sight: hold briefly instead of turning around at the shadow's edge.
                body.linearVelocity = Vector2.zero;
                return false;
            }

            Vector2 direction = offset / distance;
            Vector2 radial = Vector2.zero;
            int radialMode = stance.UpdateRadial(distance, preferredDistance, distanceTolerance);
            if (radialMode > 0)
                radial = direction;
            else if (radialMode < 0 &&
                     EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, -direction, terrain,
                         out Vector2 retreat))
                radial = retreat;

            // An orbit step that would walk into a wall or behind cover is not taken. Without this check the enemy
            // circles out of sight, walks back into sight, and repeats every frame at an obstacle's edge.
            Vector2 movement = ComposeMovement(direction, radial);
            if (!IsStepSafe(movement, root, origin, mirroredOrigin))
            {
                movement = TryReverseOrbit(currentTime) ? ComposeMovement(direction, radial) : Vector2.zero;
                if (!IsStepSafe(movement, root, origin, mirroredOrigin))
                    movement = IsStepSafe(radial, root, origin, mirroredOrigin) ? radial : Vector2.zero;
            }

            body.linearVelocity = movement.sqrMagnitude > 0.001f
                ? movement.normalized * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this))
                : Vector2.zero;
            return true;
        }

        private Vector2 ComposeMovement(Vector2 direction, Vector2 radial)
        {
            Vector2 tangent = new Vector2(-direction.y, direction.x) * orbitDirection;
            return radial == Vector2.zero ? tangent : radial + tangent * radialOrbitWeight;
        }

        private bool IsStepSafe(Vector2 movement, Vector2 root, Vector2 origin, Vector2 mirroredOrigin)
        {
            if (movement.sqrMagnitude <= 0.001f) return false;
            Vector2 step = movement.normalized * OrbitProbeDistance;
            Vector2 position = transform.position;
            return EnemyObstacleNavigator.HasClearPath(position, position + step, terrain) &&
                   HasShotLine(step, root, origin, mirroredOrigin);
        }

        // The feet-side launch point, the visible launch point (Sansamo's flower when present), and that point with
        // the sprite facing the other way. The flower sits off center and swaps sides when the enemy turns, so
        // movement requires both sides to see the target; otherwise turning around alone would open or close the
        // line and flip the movement back.
        private void GetShotOrigins(out Vector2 root, out Vector2 origin, out Vector2 mirroredOrigin)
        {
            root = EnemyProjectile.LaunchOrigin(gameObject, transform.position, false);
            SansamoPureMotion flower = GetComponent<SansamoPureMotion>();
            origin = flower != null && flower.isActiveAndEnabled ? flower.LaunchPosition : root;
            float mirrorX = spriteRenderer != null ? spriteRenderer.transform.position.x : transform.position.x;
            mirroredOrigin = origin == root ? root : new Vector2(2f * mirrorX - origin.x, origin.y);
        }

        private bool HasShotLine(Vector2 step, Vector2 root, Vector2 origin, Vector2 mirroredOrigin)
        {
            Vector2 goal = target.position;
            // A visible flower must not shoot through a wall just because the feet have line of sight.
            return EnemyObstacleNavigator.HasLineOfFire(root + step, origin + step) &&
                   EnemyObstacleNavigator.HasLineOfFire(origin + step, goal) &&
                   (mirroredOrigin == origin ||
                    (EnemyObstacleNavigator.HasLineOfFire(root + step, mirroredOrigin + step) &&
                     EnemyObstacleNavigator.HasLineOfFire(mirroredOrigin + step, goal)));
        }

        private bool TryGetShotLine(out Vector2 origin)
        {
            GetShotOrigins(out Vector2 root, out origin, out _);
            return EnemyObstacleNavigator.HasLineOfFire(root, origin) &&
                   EnemyObstacleNavigator.HasLineOfFire(origin, target.position);
        }

        private void TickBurst(float currentTime)
        {
            if (currentTime < nextShotTime) return;
            if (shotsRemaining <= 0)
                shotsRemaining = SelectBurstShotCount(UnityEngine.Random.value, fourShotChance);

            if (!FirePredictive(currentTime))
            {
                nextShotTime = currentTime + Mathf.Min(0.1f, shotInterval);
                return;
            }
            shotsRemaining--;
            nextShotTime = currentTime +
                           EnemyVariant.AttackIntervalOf(this, shotsRemaining > 0 ? shotInterval : burstRestDuration);
            if (shotsRemaining <= 0) body.linearVelocity = Vector2.zero;
        }

        private bool FirePredictive(float currentTime)
        {
            if (!TryGetShotLine(out Vector2 origin)) return false;
            Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
            Vector2 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
            Vector2 direction = CalculatePredictiveDirection(origin, target.position, targetVelocity,
                projectileSpeed, maximumPredictionTime);
            Vector2 predictedPoint = origin + direction * Vector2.Distance(origin, target.position);
            if (!EnemyObstacleNavigator.HasLineOfFire(origin, predictedPoint))
                direction = ((Vector2)target.position - origin).normalized;

            // 산사모 순수 fires its drawn pollen shot from the flower; others keep a tinted copy of their drawing.
            SansamoPureMotion flower = GetComponent<SansamoPureMotion>();
            Sprite drawnProjectile = flower != null && flower.isActiveAndEnabled ? flower.ProjectileSprite : null;
            EnemyProjectile projectile = EnemyProjectile.Create(origin, direction, gameObject,
                projectileDamageTier, projectileSpeed, projectileLifetime,
                drawnProjectile != null ? drawnProjectile : spriteRenderer != null ? spriteRenderer.sprite : null,
                drawnProjectile != null);
            presentation?.SetPhase(EnemyAttackPhase.Active);
            presentationEndsAt = currentTime + Mathf.Min(0.08f, shotInterval);
            ProjectileFired?.Invoke(projectile);
            return true;
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
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
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
