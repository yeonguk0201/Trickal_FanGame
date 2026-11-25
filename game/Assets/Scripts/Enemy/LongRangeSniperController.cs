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
        // Drawn shot size (ProjectileSizing); 고혈당 요정 uses SniperShotScale, the crayon minions keep the default.
        [SerializeField, Min(0.05f)] private float projectileScale = ProjectileSizing.DrawnEnemyScale;
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
        private readonly RangedStanceKeeper stance = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;

        public LongRangeSniperState State { get; private set; }
        public Vector2 LockedDirection => lockedDirection;
        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float PreferredDistance => preferredDistance;
        public float RelocationDuration => relocationDuration;
        public float AimDuration => aimDuration;
        public float RecoveryDuration => recoveryDuration;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileScale => projectileScale;
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

            bool blocked = ((1 << collision.collider.gameObject.layer) & EnemyObstacleNavigator.ObstacleMask) != 0 ||
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
            stance.Reset();
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
                    if (currentTime >= stateEndsAt && !AimReachesTarget())
                    {
                        // Obstacle-0, Balance-1: fire only when the aim line, as wide as the shot, reaches the
                        // player; otherwise go back to finding a clear line within range.
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
                        stateEndsAt = currentTime + EnemyVariant.AttackIntervalOf(this, recoveryDuration);
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
            Vector2 shotOrigin = ShotOrigin;
            bool hasSight = HasShotLine(shotOrigin, target.position);
            if (stance.ShouldSeekSight(hasSight, currentTime))
            {
                // Obstacle-0: hold the aim and walk around the obstacle until the line of fire opens.
                body.linearVelocity = navigator.GetMoveDirection(transform.position, target.position, terrain,
                    currentTime) * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
                return;
            }

            if (!hasSight)
            {
                // Just lost sight: hold briefly instead of turning around at the shadow's edge.
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            Vector2 direction = distance > 0.001f ? offset / distance : Vector2.right;
            int radialMode = stance.UpdateRadial(distance, preferredDistance, distanceTolerance);
            Vector2 radial = radialMode > 0 ? direction : radialMode < 0 ? -direction : Vector2.zero;

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
            // A step into a wall or behind cover is not taken; the enemy drops the strafe, then holds.
            if (!IsStepSafe(movement, shotOrigin))
                movement = IsStepSafe(radial, shotOrigin) ? radial : Vector2.zero;
            body.linearVelocity = movement.sqrMagnitude > 0.001f
                ? movement.normalized * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this))
                : Vector2.zero;

            // The aim starts only from a spot whose shot would reach the player; until then it keeps relocating.
            if (currentTime >= stateEndsAt && AimReachesTarget())
            {
                BeginAim(currentTime, direction);
            }
        }

        private bool IsStepSafe(Vector2 movement, Vector2 shotOrigin)
        {
            if (movement.sqrMagnitude <= 0.001f) return false;
            Vector2 step = movement.normalized * MobileRangedEnemyController.OrbitProbeDistance;
            Vector2 position = transform.position;
            return EnemyObstacleNavigator.HasClearPath(position, position + step, terrain) &&
                   HasShotLine(shotOrigin + step, target.position);
        }

        // Balance-1: true when a shot fired now would reach the player: within ShotReach and with no wall in the
        // way of the shot's full width (the plain line-of-fire probe is thinner than the shot and lets corners
        // through).
        public bool AimReachesTarget()
        {
            if (target == null) return false;
            Vector2 origin = ShotOrigin;
            return Vector2.Distance(origin, target.position) <= ShotReach && HasShotLine(origin, target.position);
        }

        private bool HasShotLine(Vector2 from, Vector2 to)
        {
            Vector2 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= 0.0001f) return true;
            return Physics2D.CircleCast(from, ShotRadius, offset / distance, distance,
                EnemyObstacleNavigator.LineOfFireMask).collider == null;
        }

        private void BeginAim(float currentTime, Vector2 direction)
        {
            body.linearVelocity = Vector2.zero;
            lockedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            stateEndsAt = currentTime + aimDuration;
            SetState(LongRangeSniperState.Aiming);
        }

        // The shot leaves from here, slid off a wall the enemy is pressed against (Hitbox-2).
        private Vector2 ShotOrigin => EnemyProjectile.LaunchOrigin(gameObject, transform.position,
            HasDrawnProjectile, projectileScale);

        public float ShotRadius => EnemyProjectile.LaunchRadiusOf(gameObject, HasDrawnProjectile, projectileScale);

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
                projectileSprite, drawnProjectile != null, projectileScale);
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

        // The shot's real reach: flight time × speed (Range-0).
        public float ShotReach => projectileSpeed * projectileLifetime;

        private bool HasDrawnProjectile => GetComponent<EnemyAttackArtwork>()?.ProjectileSprite != null;

        // The aim line is the shot's own path: as wide as the shot's hitbox, from where the shot leaves to where it
        // stops, at the end of its range or at the first wall it would meet.
        private void UpdateAimPath()
        {
            if (aimPath == null || State != LongRangeSniperState.Aiming ||
                lockedDirection.sqrMagnitude <= 0.001f) return;
            float radius = ShotRadius;
            Vector2 origin = ShotOrigin;
            float reach = ShotReach;
            RaycastHit2D wall = Physics2D.CircleCast(origin, radius, lockedDirection, reach,
                LayerMask.GetMask("Environment"));
            if (wall.collider != null) reach = wall.distance;
            aimPath.startWidth = radius * 2f;
            aimPath.endWidth = radius * 2f;
            aimPath.SetPosition(0, origin);
            aimPath.SetPosition(1, origin + lockedDirection * reach);
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
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
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
            // Width is set per aim from the shot's hitbox; a full-width band is kept translucent.
            aimPath.startColor = new Color(1f, 0.35f, 0.85f, 0.45f);
            aimPath.endColor = new Color(1f, 0.75f, 0.25f, 0.2f);
            aimPath.numCapVertices = 4;
            aimPath.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
            if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            {
                aimPath.sharedMaterial = spriteRenderer.sharedMaterial;
            }
            aimPath.enabled = false;
        }
    }
}
