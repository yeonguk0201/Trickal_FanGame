using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum MeleeEnemyAttackState
    {
        Idle,
        Telegraph,
        Active,
        Recovery,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyChase), typeof(EnemyBehaviorContext), typeof(EnemyAttackPresentation))]
    public sealed class MeleeEnemyAttack : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float attackRange = 1.15f;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.12f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.65f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.2f;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Medium;

        [Header("Lunge")]
        [Tooltip("Distance at which the telegraph may start. 0 keeps the stationary swing that starts at Attack Range.")]
        [SerializeField, Min(0f)] private float lungeTriggerRange;
        [Tooltip("Distance travelled along the locked direction during the active phase. 0 disables the lunge.")]
        [SerializeField, Min(0f)] private float lungeDistance;
        [Tooltip("The telegraph follows the target until this long before it ends, then the direction is fixed.")]
        [SerializeField, Min(0f)] private float aimLockLeadTime = 0.12f;
        [SerializeField, Min(0f)] private float lungeHitForwardOffset = 0.3f;
        [SerializeField, Min(0.01f)] private float lungeHitRadius = 0.75f;

        private static readonly Color TrackingPathColor = new(1f, 0.85f, 0.2f, 0.22f);
        private static readonly Color LockedPathColor = new(1f, 0.25f, 0.1f, 0.4f);

        private EnemyChase chase;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private Rigidbody2D body;
        private LineRenderer lungePath;
        private float bodyRadius;
        private float stateEndsAt;
        private float nextAttackTime;
        private bool hitApplied;
        private Vector2 aimDirection;
        private bool aimLocked;
        private float aimLocksAt;
        private float lungeTravel;
        private bool lunging;

        public MeleeEnemyAttackState State { get; private set; }
        public float AttackRange => attackRange;
        public EnemyDamageTier DamageTier => damageTier;
        public float TelegraphDuration => telegraphDuration;
        public float ActiveDuration => activeDuration;
        public float RecoveryDuration => recoveryDuration;
        public bool IsLunge => lungeTriggerRange > 0f && lungeDistance > 0f;
        public float LungeTriggerRange => lungeTriggerRange;
        public float LungeDistance => lungeDistance;
        public float AimLockLeadTime => aimLockLeadTime;
        public float LungeHitForwardOffset => lungeHitForwardOffset;
        public float LungeHitRadius => lungeHitRadius;
        // How far from its starting point a full lunge can still hit.
        public float LungeReach => lungeDistance + lungeHitForwardOffset + lungeHitRadius;
        // Zero until the telegraph fixes the lunge direction.
        public Vector2 LockedDirection => aimLocked ? aimDirection : Vector2.zero;

        public event Action<MeleeEnemyAttackState> StateChanged;

        private void Awake()
        {
            CacheComponents();
            SetState(MeleeEnemyAttackState.Idle);
        }

        private void FixedUpdate()
        {
            TickAttack(Time.time);
        }

        private void OnDisable()
        {
            CancelAttack(Time.time);
        }

        public void Configure(
            float configuredRange,
            float configuredTelegraphDuration,
            float configuredActiveDuration,
            float configuredRecoveryDuration,
            float configuredCooldown,
            EnemyDamageTier configuredDamageTier)
        {
            attackRange = Mathf.Max(0.01f, configuredRange);
            telegraphDuration = Mathf.Max(0.01f, configuredTelegraphDuration);
            activeDuration = Mathf.Max(0.01f, configuredActiveDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            attackCooldown = Mathf.Max(0f, configuredCooldown);
            damageTier = configuredDamageTier;
        }

        // Melee-Lunge: the telegraph starts from further away, follows the target, fixes its direction shortly before
        // it ends, and the active phase steps forward with the hit area in front of the body. A zero range or
        // distance keeps the stationary swing.
        public void ConfigureLunge(
            float configuredTriggerRange,
            float configuredDistance,
            float configuredAimLockLeadTime,
            float configuredHitForwardOffset,
            float configuredHitRadius)
        {
            lungeTriggerRange = Mathf.Max(0f, configuredTriggerRange);
            lungeDistance = Mathf.Max(0f, configuredDistance);
            aimLockLeadTime = Mathf.Max(0f, configuredAimLockLeadTime);
            lungeHitForwardOffset = Mathf.Max(0f, configuredHitForwardOffset);
            lungeHitRadius = Mathf.Max(0.01f, configuredHitRadius);
        }

        public void TickAttack(float currentTime)
        {
            if (behavior == null || chase == null || presentation == null)
            {
                CacheComponents();
            }

            if (!behavior.TryAcquireOrAlert(chase.DetectionRange) || behavior.IsActionSuppressed)
            {
                CancelAttack(currentTime);
                return;
            }

            switch (State)
            {
                case MeleeEnemyAttackState.Idle:
                    behavior.SetControllerMovementSuppressed(false);
                    if (currentTime >= nextAttackTime && CanStartAttack())
                    {
                        hitApplied = false;
                        aimLocked = false;
                        aimDirection = Vector2.zero;
                        stateEndsAt = currentTime + telegraphDuration;
                        aimLocksAt = stateEndsAt - aimLockLeadTime;
                        behavior.SetControllerMovementSuppressed(true);
                        SetState(MeleeEnemyAttackState.Telegraph);
                        UpdateAim(currentTime);
                    }

                    break;
                case MeleeEnemyAttackState.Telegraph:
                    behavior.SetControllerMovementSuppressed(true);
                    UpdateAim(currentTime);
                    if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + activeDuration;
                        SetState(MeleeEnemyAttackState.Active);
                        BeginLunge();
                        if (TryResolveActiveHit())
                        {
                            EndLunge();
                        }
                    }

                    break;
                case MeleeEnemyAttackState.Active:
                    behavior.SetControllerMovementSuppressed(true);
                    DriveLunge();
                    if (TryResolveActiveHit())
                    {
                        EndLunge();
                    }

                    if (currentTime >= stateEndsAt)
                    {
                        EndLunge();
                        stateEndsAt = currentTime + recoveryDuration;
                        nextAttackTime = stateEndsAt + attackCooldown;
                        SetState(MeleeEnemyAttackState.Recovery);
                    }

                    break;
                case MeleeEnemyAttackState.Recovery:
                    behavior.SetControllerMovementSuppressed(true);
                    if (currentTime >= stateEndsAt)
                    {
                        aimLocked = false;
                        behavior.SetControllerMovementSuppressed(false);
                        SetState(MeleeEnemyAttackState.Idle);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryResolveActiveHit()
        {
            if (State != MeleeEnemyAttackState.Active || hitApplied ||
                !behavior.HasLivingTarget || !IsTargetInHitArea())
            {
                return false;
            }

            Health targetHealth = behavior.TargetHealth;
            if (targetHealth.GetComponent<PlayerMovement>() == null)
            {
                return false;
            }

            hitApplied = true;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            targetHealth.TakeDamage(HealthUnits.CreateEnemyDamageContext(
                gameObject,
                DamageSourceType.EnemyMelee,
                damageTier));
            return true;
        }

        private bool IsTargetInRange()
        {
            if (!behavior.HasLivingTarget)
            {
                return false;
            }

            Vector2 offset = behavior.Target.position - transform.position;
            return offset.sqrMagnitude <= attackRange * attackRange;
        }

        private bool CanStartAttack()
        {
            if (!IsLunge || IsTargetInRange())
            {
                return IsTargetInRange();
            }

            if (!behavior.HasLivingTarget)
            {
                return false;
            }

            Vector2 offset = behavior.Target.position - transform.position;
            return offset.sqrMagnitude <= lungeTriggerRange * lungeTriggerRange &&
                   EnemyObstacleNavigator.HasClearPath(transform.position, behavior.Target.position, bodyRadius);
        }

        private bool IsTargetInHitArea()
        {
            if (!IsLunge || !aimLocked)
            {
                return IsTargetInRange();
            }

            if (!behavior.HasLivingTarget)
            {
                return false;
            }

            Vector2 center = (Vector2)transform.position + aimDirection * lungeHitForwardOffset;
            return ((Vector2)behavior.Target.position - center).sqrMagnitude <= lungeHitRadius * lungeHitRadius;
        }

        private void UpdateAim(float currentTime)
        {
            if (!IsLunge || aimLocked)
            {
                return;
            }

            if (behavior.HasLivingTarget)
            {
                Vector2 offset = behavior.Target.position - transform.position;
                if (offset.sqrMagnitude > 0.001f)
                {
                    aimDirection = offset.normalized;
                }
            }

            if (aimDirection.sqrMagnitude > 0.001f)
            {
                lungeTravel = ResolveLungeTravel(aimDirection);
                aimLocked = currentTime >= aimLocksAt;
            }

            UpdateLungePath();
        }

        // The lunge stops at the first obstacle or pit instead of pushing into it for the rest of the active phase.
        private float ResolveLungeTravel(Vector2 direction)
        {
            float distance = lungeDistance;
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(transform.position, bodyRadius, direction, distance,
                         EnemyObstacleNavigator.ObstacleMask))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                distance = Mathf.Min(distance, hit.distance);
            }

            return distance;
        }

        private void BeginLunge()
        {
            if (!IsLunge || !aimLocked || lungeTravel <= 0.001f)
            {
                return;
            }

            lunging = true;
            behavior.SetControllerVelocityOwned(true);
            DriveLunge();
        }

        private void DriveLunge()
        {
            if (lunging && body != null)
            {
                body.linearVelocity = aimDirection * (lungeTravel / activeDuration);
            }
        }

        private void EndLunge()
        {
            if (!lunging)
            {
                return;
            }

            lunging = false;
            behavior.SetControllerVelocityOwned(false);
            behavior.StopForSuppression();
        }

        private void UpdateLungePath()
        {
            bool show = IsLunge && State == MeleeEnemyAttackState.Telegraph && aimDirection.sqrMagnitude > 0.001f;
            if (lungePath == null)
            {
                if (!show)
                {
                    return;
                }

                CreateLungePath();
            }

            lungePath.enabled = show;
            if (!show)
            {
                return;
            }

            // The band covers the area the lunge can hit, so stepping out of it is a real dodge.
            lungePath.startWidth = lungeHitRadius * 2f;
            lungePath.endWidth = lungeHitRadius * 2f;
            Color color = aimLocked ? LockedPathColor : TrackingPathColor;
            lungePath.startColor = color;
            lungePath.endColor = color;
            lungePath.SetPosition(0, transform.position);
            lungePath.SetPosition(1, (Vector2)transform.position +
                                     aimDirection * (lungeTravel + lungeHitForwardOffset + lungeHitRadius));
        }

        private void CreateLungePath()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            lungePath = GetComponent<LineRenderer>();
            if (lungePath == null)
            {
                lungePath = gameObject.AddComponent<LineRenderer>();
            }

            lungePath.useWorldSpace = true;
            lungePath.positionCount = 2;
            lungePath.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
            if (spriteRenderer != null)
            {
                lungePath.sortingLayerID = spriteRenderer.sortingLayerID;
                if (spriteRenderer.sharedMaterial != null)
                {
                    lungePath.sharedMaterial = spriteRenderer.sharedMaterial;
                }
            }

            lungePath.enabled = false;
        }

        private void CancelAttack(float currentTime)
        {
            lunging = false;
            aimLocked = false;
            if (behavior != null)
            {
                behavior.SetControllerVelocityOwned(false);
                behavior.SetControllerMovementSuppressed(false);
                behavior.StopForSuppression();
            }

            if (State != MeleeEnemyAttackState.Idle)
            {
                nextAttackTime = Mathf.Max(nextAttackTime, currentTime + attackCooldown);
            }

            hitApplied = false;
            SetState(MeleeEnemyAttackState.Idle);
        }

        private void SetState(MeleeEnemyAttackState state)
        {
            bool changed = State != state;
            State = state;
            if (presentation != null)
            {
                presentation.SetPhase(state switch
                {
                    MeleeEnemyAttackState.Telegraph => EnemyAttackPhase.Telegraph,
                    MeleeEnemyAttackState.Active => EnemyAttackPhase.Active,
                    MeleeEnemyAttackState.Recovery => EnemyAttackPhase.Recovery,
                    _ => EnemyAttackPhase.Idle,
                });
            }

            UpdateLungePath();
            if (changed)
            {
                StateChanged?.Invoke(State);
            }
        }

        private void CacheComponents()
        {
            chase = GetComponent<EnemyChase>();
            behavior = GetComponent<EnemyBehaviorContext>();
            presentation = GetComponent<EnemyAttackPresentation>();
            body = GetComponent<Rigidbody2D>();
            bodyRadius = EnemyObstacleNavigator.ResolveBodyRadius(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (IsLunge)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireSphere(transform.position, lungeTriggerRange);
            }
        }
    }
}
