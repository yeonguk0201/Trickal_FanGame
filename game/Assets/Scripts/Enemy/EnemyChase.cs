using TrickalFanGame.Combat;
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
        [Tooltip("비행하는 적입니다. 길찾기 없이 대상에게 곧장 날아갑니다(EnemyFlight와 함께 사용).")]
        [SerializeField] private bool flies;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private readonly EnemyObstacleNavigator navigator = new();
        private float bodyRadius;

        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float StopDistance => stopDistance;
        public bool Flies => flies;
        public bool IsMovementSuppressed => behavior == null || behavior.IsMovementSuppressed;

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

        public void ConfigureFlight(bool configuredFlies) => flies = configuredFlies;

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            behavior?.SetTarget(configuredTarget);
        }

        public void TickChase()
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
            if (distance <= stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            // A flying body crosses pits and low obstacles, so it needs no path: it heads straight for the target
            // and physics stops it at walls and trees.
            Vector2 direction = flies
                ? offset / distance
                : navigator.GetMoveDirection(transform.position, target.position, bodyRadius, Time.time);
            // Shock (Passive-0 §4.8) slows ordinary movement only.
            body.linearVelocity = direction * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
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
        }

        private bool FindTargetIfNeeded()
        {
            return behavior != null && behavior.TryAcquireOrAlert(detectionRange);
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
