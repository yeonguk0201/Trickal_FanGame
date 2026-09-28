using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum ChargingEnemyState
    {
        Idle,
        Windup,
        Dashing,
        Recovering,
        Pursuing,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class ChargingEnemyController : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Min(0f)] private float detectionRange = 7f;

        [Header("Charge")]
        [SerializeField, Min(0f)] private float pursuitSpeed = 2.5f;
        [SerializeField, Min(0f)] private float pursuitDuration = 1.1f;
        [SerializeField, Min(0.01f)] private float windupDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float dashSpeed = 9f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.8f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;
        [SerializeField, Min(0f)] private float chargeCooldown = 1.5f;
        [SerializeField] private EnemyDamageTier chargeDamageTier = EnemyDamageTier.Heavy;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation attackPresentation;
        private SpriteRenderer spriteRenderer;
        private LineRenderer chargePath;
        private Color idleColor = Color.white;
        private float stateEndsAt;
        private float nextChargeTime;
        private Vector2 lockedDirection;
        private bool pursuitTimerStarted;
        private readonly EnemyObstacleNavigator navigator = new();
        private float bodyRadius;

        public ChargingEnemyState State { get; private set; }
        public Vector2 LockedDirection => lockedDirection;
        public float NextChargeTime => nextChargeTime;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float PursuitSpeed => pursuitSpeed;
        public float PursuitDuration => pursuitDuration;
        public EnemyDamageTier ChargeDamageTier => chargeDamageTier;
        public bool IsActionSuppressed => behavior == null || behavior.IsActionSuppressed;

        public event Action<ChargingEnemyState> StateChanged;

        private void Awake()
        {
            CacheComponents();
            if (spriteRenderer != null)
            {
                idleColor = spriteRenderer.color;
            }

            SetState(ChargingEnemyState.Idle);
        }

        private void Start()
        {
            FindTargetIfNeeded();
        }

        private void FixedUpdate()
        {
            TickBehavior(Time.time);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryResolveCollision(collision.collider, Time.time);
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            lockedDirection = Vector2.zero;
            pursuitTimerStarted = false;
            SetState(ChargingEnemyState.Idle);
        }

        public void Configure(
            float configuredDetectionRange,
            float configuredWindupDuration,
            float configuredDashSpeed,
            float configuredDashDuration,
            float configuredRecoveryDuration,
            float configuredChargeCooldown,
            EnemyDamageTier configuredChargeDamageTier)
        {
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            windupDuration = Mathf.Max(0.01f, configuredWindupDuration);
            dashSpeed = Mathf.Max(0.01f, configuredDashSpeed);
            dashDuration = Mathf.Max(0.01f, configuredDashDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            chargeCooldown = Mathf.Max(0f, configuredChargeCooldown);
            chargeDamageTier = configuredChargeDamageTier;
            pursuitDuration = 0f;
            pursuitTimerStarted = false;
            SetState(ChargingEnemyState.Idle);
        }

        public void ConfigurePursuitCharge(
            float configuredDetectionRange,
            float configuredPursuitSpeed,
            float configuredPursuitDuration,
            float configuredWindupDuration,
            float configuredDashSpeed,
            float configuredDashDuration,
            float configuredRecoveryDuration,
            EnemyDamageTier configuredChargeDamageTier)
        {
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            pursuitSpeed = Mathf.Max(0f, configuredPursuitSpeed);
            pursuitDuration = Mathf.Max(0f, configuredPursuitDuration);
            windupDuration = Mathf.Max(0.01f, configuredWindupDuration);
            dashSpeed = Mathf.Max(0.01f, configuredDashSpeed);
            dashDuration = Mathf.Max(0.01f, configuredDashDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            chargeCooldown = 0f;
            chargeDamageTier = configuredChargeDamageTier;
            pursuitTimerStarted = false;
            SetState(ChargingEnemyState.Idle);
        }

        public void SetChargeDamageTier(EnemyDamageTier configuredChargeDamageTier)
        {
            chargeDamageTier = configuredChargeDamageTier;
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
                InterruptPattern(currentTime, true);
                return;
            }

            target = behavior.Target;
            if (behavior.IsActionSuppressed)
            {
                InterruptPattern(currentTime, !knockback.IsKnockedBack);
                return;
            }

            switch (State)
            {
                case ChargingEnemyState.Idle:
                    if (pursuitDuration > 0f)
                    {
                        EnterPursuit(currentTime);
                        TickPursuit(currentTime);
                    }
                    else
                    {
                        behavior.SetControllerMovementSuppressed(false);
                        body.linearVelocity = Vector2.zero;
                        if (!TryBeginWindup(currentTime) && !HasClearChargePath())
                        {
                            MoveAroundObstacles(currentTime);
                        }
                    }
                    break;
                case ChargingEnemyState.Pursuing:
                    TickPursuit(currentTime);
                    break;
                case ChargingEnemyState.Windup:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    TrackTargetDuringWindup();
                    if (currentTime >= stateEndsAt && !HasClearChargePath())
                    {
                        // The telegraphed line became blocked: skip the dash as if it had hit the obstacle.
                        EnterRecovery(currentTime);
                    }
                    else if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + dashDuration;
                        body.linearVelocity = lockedDirection * dashSpeed;
                        SetState(ChargingEnemyState.Dashing);
                    }

                    break;
                case ChargingEnemyState.Dashing:
                    behavior.SetControllerMovementSuppressed(false);
                    if (currentTime >= stateEndsAt)
                    {
                        EnterRecovery(currentTime);
                    }
                    else
                    {
                        body.linearVelocity = lockedDirection * dashSpeed;
                    }

                    break;
                case ChargingEnemyState.Recovering:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt)
                    {
                        lockedDirection = Vector2.zero;
                        if (pursuitDuration > 0f)
                        {
                            EnterPursuit(currentTime);
                            TickPursuit(currentTime);
                        }
                        else
                        {
                            SetState(ChargingEnemyState.Idle);
                        }
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryResolveCollision(Collider2D other, float currentTime)
        {
            if (State != ChargingEnemyState.Dashing || other == null)
            {
                return false;
            }

            Health playerHealth = other.GetComponentInParent<Health>();
            if (playerHealth != null && !playerHealth.IsDead &&
                playerHealth.GetComponent<PlayerMovement>() != null)
            {
                playerHealth.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
                playerHealth.TakeDamage(HealthUnits.CreateEnemyDamageContext(
                    gameObject, DamageSourceType.EnemyContact, chargeDamageTier));
                EnterRecovery(currentTime);
                return true;
            }

            if (other.GetComponentInParent<DoorController>() != null ||
                other.gameObject.layer == LayerMask.NameToLayer("Environment"))
            {
                EnterRecovery(currentTime);
                return true;
            }

            return false;
        }

        private bool TryBeginWindup(float currentTime)
        {
            if (currentTime < nextChargeTime || !HasClearChargePath())
            {
                return false;
            }

            Vector2 offset = target.position - transform.position;
            if (offset.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            lockedDirection = offset.normalized;
            stateEndsAt = currentTime + windupDuration;
            SetState(ChargingEnemyState.Windup);
            return true;
        }

        private bool HasClearChargePath()
        {
            return target != null &&
                   EnemyObstacleNavigator.HasClearPath(transform.position, target.position, bodyRadius);
        }

        private void MoveAroundObstacles(float currentTime)
        {
            body.linearVelocity = target != null
                ? navigator.GetMoveDirection(transform.position, target.position, bodyRadius, currentTime) *
                  pursuitSpeed
                : Vector2.zero;
        }

        private void TrackTargetDuringWindup()
        {
            if (target == null) return;
            Vector2 offset = target.position - transform.position;
            if (offset.sqrMagnitude <= 0.001f) return;
            lockedDirection = offset.normalized;
            UpdateChargePath();
        }

        private void UpdateChargePath()
        {
            if (chargePath == null || State != ChargingEnemyState.Windup ||
                lockedDirection.sqrMagnitude <= 0.001f) return;
            chargePath.SetPosition(0, transform.position);
            chargePath.SetPosition(1, (Vector2)transform.position + lockedDirection * dashSpeed * dashDuration);
        }

        private void EnterPursuit(float currentTime)
        {
            lockedDirection = Vector2.zero;
            pursuitTimerStarted = true;
            stateEndsAt = currentTime + pursuitDuration;
            SetState(ChargingEnemyState.Pursuing);
        }

        private void TickPursuit(float currentTime)
        {
            behavior.SetControllerMovementSuppressed(false);
            if (!pursuitTimerStarted)
            {
                stateEndsAt = currentTime + pursuitDuration;
                pursuitTimerStarted = true;
            }

            MoveAroundObstacles(currentTime);

            // Obstacle-0: the charge only starts once the straight line to the target is clear; until then the
            // pursuit continues around obstacles and re-checks every tick.
            if (currentTime >= stateEndsAt && HasClearChargePath())
            {
                body.linearVelocity = Vector2.zero;
                TryBeginWindup(currentTime);
            }
        }

        private void EnterRecovery(float currentTime)
        {
            body.linearVelocity = Vector2.zero;
            pursuitTimerStarted = false;
            stateEndsAt = currentTime + recoveryDuration;
            nextChargeTime = currentTime + chargeCooldown;
            SetState(ChargingEnemyState.Recovering);
        }

        private void InterruptPattern(float currentTime, bool stopBody)
        {
            if (stopBody && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (State == ChargingEnemyState.Idle)
            {
                return;
            }

            lockedDirection = Vector2.zero;
            pursuitTimerStarted = false;
            nextChargeTime = Mathf.Max(nextChargeTime, currentTime + chargeCooldown);
            SetState(ChargingEnemyState.Idle);
        }

        private void SetState(ChargingEnemyState nextState)
        {
            if (State == nextState)
            {
                ApplyStateVisual();
                return;
            }

            State = nextState;
            ApplyStateVisual();
            StateChanged?.Invoke(State);
        }

        private void ApplyStateVisual()
        {
            if (attackPresentation != null)
            {
                attackPresentation.SetPhase(State switch
                {
                    ChargingEnemyState.Windup => EnemyAttackPhase.Telegraph,
                    ChargingEnemyState.Dashing => EnemyAttackPhase.Active,
                    ChargingEnemyState.Recovering => EnemyAttackPhase.Recovery,
                    _ => EnemyAttackPhase.Idle,
                });
            }

            if (spriteRenderer == null)
            {
                return;
            }

            if (chargePath != null)
            {
                bool showPath = State == ChargingEnemyState.Windup && lockedDirection.sqrMagnitude > 0.001f;
                chargePath.enabled = showPath;
                if (showPath)
                {
                    UpdateChargePath();
                }
            }

            spriteRenderer.color = State switch
            {
                ChargingEnemyState.Windup => new Color(1f, 0.85f, 0.2f),
                ChargingEnemyState.Dashing => new Color(1f, 0.2f, 0.15f),
                ChargingEnemyState.Recovering => Color.Lerp(idleColor, Color.gray, 0.55f),
                _ => idleColor,
            };
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
            spriteRenderer = GetComponent<SpriteRenderer>();
            chargePath = GetComponent<LineRenderer>();
            if (chargePath == null)
            {
                chargePath = gameObject.AddComponent<LineRenderer>();
            }
            chargePath.useWorldSpace = true;
            chargePath.positionCount = 2;
            chargePath.startWidth = 0.1f;
            chargePath.endWidth = 0.04f;
            chargePath.startColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            chargePath.endColor = new Color(1f, 0.25f, 0.1f, 0.35f);
            chargePath.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
            if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            {
                chargePath.sharedMaterial = spriteRenderer.sharedMaterial;
            }
            chargePath.enabled = false;
            attackPresentation = GetComponent<EnemyAttackPresentation>();
            if (attackPresentation == null)
            {
                attackPresentation = gameObject.AddComponent<EnemyAttackPresentation>();
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
        }
    }
}
