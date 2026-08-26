using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float detectionRange = 6f;
        [SerializeField, Min(0f)] private float stopDistance = 0.8f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private Health targetHealth;
        private KnockbackReceiver knockback;

        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float StopDistance => stopDistance;
        public bool IsMovementSuppressed => health == null || health.IsDead ||
                                            (knockback != null && knockback.IsActive);

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
            TickChase();
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void Configure(float configuredMoveSpeed, float configuredDetectionRange, float configuredStopDistance)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            stopDistance = Mathf.Clamp(configuredStopDistance, 0f, detectionRange);
        }

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            targetHealth = target != null ? target.GetComponent<Health>() : null;
        }

        public void TickChase()
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
            if (distance > detectionRange || distance <= stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = offset / distance * moveSpeed;
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

            target = player.transform;
            targetHealth = player.GetComponent<Health>();
            return targetHealth != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stopDistance);
        }
    }
}
