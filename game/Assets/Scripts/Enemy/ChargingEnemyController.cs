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
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class ChargingEnemyController : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Min(0f)] private float detectionRange = 7f;

        [Header("Charge")]
        [SerializeField, Min(0.01f)] private float windupDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float dashSpeed = 9f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.8f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;
        [SerializeField, Min(0f)] private float chargeCooldown = 1.5f;
        [SerializeField, Min(1)] private int chargeDamage = 2;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private Health targetHealth;
        private KnockbackReceiver knockback;
        private SpriteRenderer spriteRenderer;
        private Color idleColor = Color.white;
        private float stateEndsAt;
        private float nextChargeTime;
        private Vector2 lockedDirection;

        public ChargingEnemyState State { get; private set; }
        public Vector2 LockedDirection => lockedDirection;
        public float NextChargeTime => nextChargeTime;
        public float DashSpeed => dashSpeed;
        public int ChargeDamage => chargeDamage;
        public bool IsActionSuppressed => health == null || health.IsDead ||
                                          (knockback != null && knockback.IsActive);

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
            SetState(ChargingEnemyState.Idle);
        }

        public void Configure(
            float configuredDetectionRange,
            float configuredWindupDuration,
            float configuredDashSpeed,
            float configuredDashDuration,
            float configuredRecoveryDuration,
            float configuredChargeCooldown,
            int configuredChargeDamage)
        {
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            windupDuration = Mathf.Max(0.01f, configuredWindupDuration);
            dashSpeed = Mathf.Max(0.01f, configuredDashSpeed);
            dashDuration = Mathf.Max(0.01f, configuredDashDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            chargeCooldown = Mathf.Max(0f, configuredChargeCooldown);
            chargeDamage = Mathf.Max(1, configuredChargeDamage);
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
                InterruptPattern(currentTime, !knockback.IsKnockedBack);
                return;
            }

            if (health.IsDead || !FindTargetIfNeeded() || targetHealth.IsDead)
            {
                InterruptPattern(currentTime, true);
                return;
            }

            switch (State)
            {
                case ChargingEnemyState.Idle:
                    body.linearVelocity = Vector2.zero;
                    TryBeginWindup(currentTime);
                    break;
                case ChargingEnemyState.Windup:
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + dashDuration;
                        body.linearVelocity = lockedDirection * dashSpeed;
                        SetState(ChargingEnemyState.Dashing);
                    }

                    break;
                case ChargingEnemyState.Dashing:
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
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt)
                    {
                        lockedDirection = Vector2.zero;
                        SetState(ChargingEnemyState.Idle);
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
                playerHealth.TakeDamage(new DamageContext(gameObject, DamageSourceType.EnemyContact, chargeDamage));
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

        private void TryBeginWindup(float currentTime)
        {
            if (currentTime < nextChargeTime)
            {
                return;
            }

            Vector2 offset = target.position - transform.position;
            if (offset.sqrMagnitude <= 0.001f || offset.sqrMagnitude > detectionRange * detectionRange)
            {
                return;
            }

            lockedDirection = offset.normalized;
            stateEndsAt = currentTime + windupDuration;
            SetState(ChargingEnemyState.Windup);
        }

        private void EnterRecovery(float currentTime)
        {
            body.linearVelocity = Vector2.zero;
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
            if (spriteRenderer == null)
            {
                return;
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
            spriteRenderer = GetComponent<SpriteRenderer>();
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
        }
    }
}
