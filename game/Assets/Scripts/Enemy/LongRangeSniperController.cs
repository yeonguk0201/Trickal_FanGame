using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum LongRangeSniperState
    {
        Idle,
        Relocating,
        Aiming,
        Firing,
        Recovering,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class LongRangeSniperController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float detectionRange = 14f;
        [SerializeField, Min(0f)] private float preferredDistance = 8f;
        [SerializeField, Min(0f)] private float distanceTolerance = 1f;
        [SerializeField, Min(0.01f)] private float relocationDuration = 1f;
        [SerializeField, Min(0.01f)] private float directionReevaluationInterval = 0.3f;
        [SerializeField, Range(0f, 1f)] private float strafeWeight = 0.65f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float aimDuration = 0.7f;
        [SerializeField, Min(0.01f)] private float firingDuration = 0.08f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 7f;
        [SerializeField] private EnemyDamageTier projectileDamageTier = EnemyDamageTier.Heavy;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 5f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private SpriteRenderer spriteRenderer;
        private LineRenderer aimPath;
        private float stateEndsAt;
        private float nextDirectionReevaluation;
        private Vector2 lockedDirection;
        private int strafeDirection = 1;
        private int relocationSequence;
        private bool strafeThisCycle;
        private readonly EnemyObstacleNavigator navigator = new();
        private float bodyRadius;

        public LongRangeSniperState State { get; private set; }
        public Vector2 LockedDirection => lockedDirection;
        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float PreferredDistance => preferredDistance;
        public float RelocationDuration => relocationDuration;
        public float AimDuration => aimDuration;
        public float RecoveryDuration => recoveryDuration;
        public float ProjectileSpeed => projectileSpeed;
        public EnemyDamageTier ProjectileDamageTier => projectileDamageTier;
        public int StrafeDirection => strafeDirection;
        public bool IsActionSuppressed => behavior == null || behavior.IsActionSuppressed;

        public event Action<LongRangeSniperState> StateChanged;
        public event Action<EnemyProjectile> ProjectileFired;

        private void Awake()
        {
            CacheComponents();
            SetState(LongRangeSniperState.Idle);
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
            if (collision == null || collision.collider == null)
            {
                return;
            }

            bool blocked = collision.collider.gameObject.layer == LayerMask.NameToLayer("Environment") ||
                           collision.collider.GetComponentInParent<EnemyBehaviorContext>() != null;
            if (blocked)
            {
                NotifyBlocked(Time.time);
            }
        }

        private void OnDisable()
        {
            CancelPattern(true);
        }

        public void Configure(
            float configuredMoveSpeed,
            float configuredDetectionRange,
            float configuredPreferredDistance,
            float configuredDistanceTolerance,
            float configuredRelocationDuration,
            float configuredDirectionReevaluationInterval,
            float configuredAimDuration,
            float configuredFiringDuration,
            float configuredRecoveryDuration,
            float configuredProjectileSpeed,
            EnemyDamageTier configuredProjectileDamageTier,
            float configuredProjectileLifetime)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            preferredDistance = Mathf.Clamp(configuredPreferredDistance, 0f, detectionRange);
            distanceTolerance = Mathf.Clamp(configuredDistanceTolerance, 0f, preferredDistance);
            relocationDuration = Mathf.Max(0.01f, configuredRelocationDuration);
            directionReevaluationInterval = Mathf.Max(0.01f, configuredDirectionReevaluationInterval);
            aimDuration = Mathf.Max(0.01f, configuredAimDuration);
            firingDuration = Mathf.Max(0.01f, configuredFiringDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            projectileSpeed = Mathf.Max(0.01f, configuredProjectileSpeed);
            projectileDamageTier = configuredProjectileDamageTier;
            projectileLifetime = Mathf.Max(0.01f, configuredProjectileLifetime);
            relocationSequence = 0;
            strafeDirection = 1;
            SetState(LongRangeSniperState.Idle);
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
            if (body == null || health == null || knockback == null)
            {
                CacheComponents();
            }

            if (!behavior.TryAcquireOrAlert(detectionRange))
            {
                CancelPattern(true);
                return;
            }

            target = behavior.Target;
            if (behavior.IsActionSuppressed)
            {
                CancelPattern(!knockback.IsKnockedBack);
                return;
            }

            switch (State)
            {
                case LongRangeSniperState.Idle:
                    EnterRelocating(currentTime);
                    TickRelocating(currentTime);
                    break;
                case LongRangeSniperState.Relocating:
                    TickRelocating(currentTime);
                    break;
                case LongRangeSniperState.Aiming:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    TrackTargetDuringAim();
                    if (currentTime >= stateEndsAt &&
                        !EnemyObstacleNavigator.HasLineOfFire(transform.position, target.position))
                    {
                        // Obstacle-0: never fire into an obstacle; go back to finding a clear line.
                        EnterRelocating(currentTime);
                    }
                    else if (currentTime >= stateEndsAt)
                    {
                        Fire(currentTime);
                    }
                    break;
                case LongRangeSniperState.Firing:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + recoveryDuration;
                        SetState(LongRangeSniperState.Recovering);
                    }
                    break;
                case LongRangeSniperState.Recovering:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt)
                    {
                        EnterRelocating(currentTime);
                        TickRelocating(currentTime);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool NotifyBlocked(float currentTime)
        {
            if (State != LongRangeSniperState.Relocating)
            {
                return false;
            }

            strafeDirection *= -1;
            strafeThisCycle = true;
            nextDirectionReevaluation = currentTime + directionReevaluationInterval;
            return true;
        }

        private void EnterRelocating(float currentTime)
        {
            lockedDirection = Vector2.zero;
            strafeThisCycle = relocationSequence % 2 == 1;
            relocationSequence++;
            stateEndsAt = currentTime + relocationDuration;
            nextDirectionReevaluation = currentTime + directionReevaluationInterval;
            SetState(LongRangeSniperState.Relocating);
        }

        private void TickRelocating(float currentTime)
        {
            behavior.SetControllerMovementSuppressed(false);
            if (!EnemyObstacleNavigator.HasLineOfFire(transform.position, target.position))
            {
                // Obstacle-0: hold the aim and walk around the obstacle until the line of fire opens.
                body.linearVelocity = navigator.GetMoveDirection(transform.position, target.position, bodyRadius,
                    currentTime) * moveSpeed;
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            Vector2 direction = distance > 0.001f ? offset / distance : Vector2.right;
            Vector2 radial = Vector2.zero;
            if (distance > preferredDistance + distanceTolerance)
            {
                radial = direction;
            }
            else if (distance < preferredDistance - distanceTolerance)
            {
                radial = -direction;
            }

            if (currentTime >= nextDirectionReevaluation)
            {
                nextDirectionReevaluation = currentTime + directionReevaluationInterval;
                if (strafeThisCycle && radial == Vector2.zero)
                {
                    strafeDirection *= -1;
                }
            }

            Vector2 tangent = strafeThisCycle
                ? new Vector2(-direction.y, direction.x) * strafeDirection * strafeWeight
                : Vector2.zero;
            Vector2 movement = radial + tangent;
            body.linearVelocity = movement.sqrMagnitude > 0.001f
                ? movement.normalized * moveSpeed
                : Vector2.zero;

            if (currentTime >= stateEndsAt)
            {
                BeginAim(currentTime, direction);
            }
        }

        private void BeginAim(float currentTime, Vector2 direction)
        {
            body.linearVelocity = Vector2.zero;
            lockedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            stateEndsAt = currentTime + aimDuration;
            SetState(LongRangeSniperState.Aiming);
        }

        private void Fire(float currentTime)
        {
            Sprite drawnProjectile = GetComponent<EnemyAttackArtwork>()?.ProjectileSprite;
            Sprite projectileSprite = drawnProjectile != null ? drawnProjectile : spriteRenderer != null ? spriteRenderer.sprite : null;
            EnemyProjectile projectile = EnemyProjectile.Create(
                transform.position,
                lockedDirection,
                gameObject,
                projectileDamageTier,
                projectileSpeed,
                projectileLifetime,
                projectileSprite, drawnProjectile != null);
            ProjectileFired?.Invoke(projectile);
            stateEndsAt = currentTime + firingDuration;
            SetState(LongRangeSniperState.Firing);
        }

        private void TrackTargetDuringAim()
        {
            if (target == null) return;
            Vector2 offset = target.position - transform.position;
            if (offset.sqrMagnitude <= 0.001f) return;
            lockedDirection = offset.normalized;
            UpdateAimPath();
        }

        private void UpdateAimPath()
        {
            if (aimPath == null || State != LongRangeSniperState.Aiming ||
                lockedDirection.sqrMagnitude <= 0.001f) return;
            aimPath.SetPosition(0, transform.position);
            aimPath.SetPosition(1, (Vector2)transform.position + lockedDirection * preferredDistance);
        }

        private void CancelPattern(bool stopBody)
        {
            if (stopBody && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            lockedDirection = Vector2.zero;
            SetState(LongRangeSniperState.Idle);
        }

        private void SetState(LongRangeSniperState nextState)
        {
            if (State == nextState)
            {
                ApplyPresentation();
                return;
            }

            State = nextState;
            ApplyPresentation();
            StateChanged?.Invoke(State);
        }

        private void ApplyPresentation()
        {
            presentation?.SetPhase(State switch
            {
                LongRangeSniperState.Aiming => EnemyAttackPhase.Telegraph,
                LongRangeSniperState.Firing => EnemyAttackPhase.Active,
                LongRangeSniperState.Recovering => EnemyAttackPhase.Recovery,
                _ => EnemyAttackPhase.Idle,
            });

            if (aimPath == null)
            {
                return;
            }

            bool showPath = State == LongRangeSniperState.Aiming && lockedDirection.sqrMagnitude > 0.001f;
            aimPath.enabled = showPath;
            if (showPath)
            {
                UpdateAimPath();
            }
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            bodyRadius = EnemyObstacleNavigator.ResolveBodyRadius(gameObject);
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

            presentation = GetComponent<EnemyAttackPresentation>();
            if (presentation == null)
            {
                presentation = gameObject.AddComponent<EnemyAttackPresentation>();
            }
            spriteRenderer = GetComponent<SpriteRenderer>();
            aimPath = GetComponent<LineRenderer>();
            if (aimPath == null)
            {
                aimPath = gameObject.AddComponent<LineRenderer>();
            }
            aimPath.useWorldSpace = true;
            aimPath.positionCount = 2;
            aimPath.startWidth = 0.06f;
            aimPath.endWidth = 0.02f;
            aimPath.startColor = new Color(1f, 0.35f, 0.85f, 0.9f);
            aimPath.endColor = new Color(1f, 0.75f, 0.25f, 0.25f);
            aimPath.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
            if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            {
                aimPath.sharedMaterial = spriteRenderer.sharedMaterial;
            }
            aimPath.enabled = false;
        }
    }
}
