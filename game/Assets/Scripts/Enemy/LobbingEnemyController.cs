using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-12: 곡사형 (user decisions, 2026-10-24 and 2026-10-30). It stays at the back like the ranged enemies, but
    // throws over obstacles instead of needing a clear line of fire. Between its attack distances it stops, winds up
    // briefly (LobbingFairyMotion raises the crowbar), then lobs a projectile (EnemyLobProjectile) at where the
    // player's shadow lay at the end of the wind-up. Too close it backs away; too far it walks around obstacles toward the player.
    // Knockback, stuns or the spawn delay call off a wind-up, and the next one starts over.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class LobbingEnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float detectionRange = 10f;
        [SerializeField, Min(0f)] private float minimumAttackDistance = 3.5f;
        [SerializeField, Min(0f)] private float maximumAttackDistance = 7f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float attackInterval = 2.4f;
        [SerializeField, Min(0f)] private float windUpDuration = 0.4f;
        [SerializeField, Min(0.05f)] private float flightDuration = 1f;
        [SerializeField, Min(0f)] private float arcHeight = 2.2f;
        [SerializeField, Min(0.05f)] private float landingRadius = 0.75f;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Medium;
        [SerializeField] private Transform target;

        [Header("Artwork")]
        // Null throws the plain lump. The drawing spins in flight.
        [SerializeField] private Sprite projectileSprite;
        // How high above the root the projectile leaves the hand.
        [SerializeField, Min(0f)] private float launchHeight;

        private Rigidbody2D body;
        private EnemyBehaviorContext behavior;
        private float nextAttackTime;
        private float windUpEndsAt = float.NaN;
        private readonly EnemyObstacleNavigator navigator = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;

        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float MinimumAttackDistance => minimumAttackDistance;
        public float MaximumAttackDistance => maximumAttackDistance;
        public float AttackInterval => attackInterval;
        public float WindUpDuration => windUpDuration;
        public float FlightDuration => flightDuration;
        public float ArcHeight => arcHeight;
        public float LandingRadius => landingRadius;
        public EnemyDamageTier DamageTier => damageTier;
        public float NextAttackTime => nextAttackTime;
        public bool IsWindingUp => !float.IsNaN(windUpEndsAt);
        public float WindUpEndsAt => windUpEndsAt;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public EnemyLobProjectile LastProjectile { get; private set; }
        public Sprite ProjectileSprite => projectileSprite;
        public float LaunchHeight => launchHeight;
        public float LastThrowTime { get; private set; } = float.NegativeInfinity;
        public Transform Target => behavior != null && behavior.Target != null ? behavior.Target : target;

        public event Action<EnemyLobProjectile> ProjectileThrown;

        public void Configure(float configuredMoveSpeed, float configuredDetectionRange,
            float configuredMinimumAttackDistance, float configuredMaximumAttackDistance,
            float configuredAttackInterval, float configuredWindUpDuration, float configuredFlightDuration,
            float configuredArcHeight, float configuredLandingRadius, EnemyDamageTier configuredDamageTier)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            minimumAttackDistance = Mathf.Clamp(configuredMinimumAttackDistance, 0f, detectionRange);
            maximumAttackDistance = Mathf.Clamp(configuredMaximumAttackDistance, minimumAttackDistance,
                detectionRange);
            attackInterval = Mathf.Max(0.01f, configuredAttackInterval);
            windUpDuration = Mathf.Max(0f, configuredWindUpDuration);
            flightDuration = Mathf.Max(0.05f, configuredFlightDuration);
            arcHeight = Mathf.Max(0f, configuredArcHeight);
            landingRadius = Mathf.Max(0.05f, configuredLandingRadius);
            damageTier = configuredDamageTier;
        }

        public void ConfigureArtwork(Sprite configuredProjectileSprite, float configuredLaunchHeight)
        {
            projectileSprite = configuredProjectileSprite;
            launchHeight = Mathf.Max(0f, configuredLaunchHeight);
        }

        public void SetTarget(Transform configuredTarget)
        {
            target = configuredTarget;
            behavior?.SetTarget(configuredTarget);
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void FixedUpdate()
        {
            TickBehavior(Time.time);
        }

        private void OnDisable()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            CancelWindUp();
        }

        public void TickBehavior(float currentTime)
        {
            if (body == null || behavior == null) CacheComponents();

            if (!behavior.TryAcquireOrAlert(detectionRange) || behavior.IsMovementSuppressed)
            {
                CancelWindUp();
                behavior.StopForSuppression();
                return;
            }

            target = behavior.Target;
            if (IsWindingUp)
            {
                body.linearVelocity = Vector2.zero;
                if (currentTime >= windUpEndsAt) Throw(currentTime);
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            float speed = moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this);
            // No line of fire is needed: the lob passes over obstacles. Only the distance decides.
            if (distance > maximumAttackDistance)
            {
                body.linearVelocity = navigator.GetMoveDirection(transform.position, target.position, terrain,
                    currentTime) * speed;
                return;
            }

            if (distance < minimumAttackDistance && distance > 0.001f &&
                EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, -offset / distance, terrain,
                    out Vector2 retreat))
            {
                body.linearVelocity = retreat * speed;
                return;
            }

            // Backed against a wall or in range, it holds its ground and throws.
            body.linearVelocity = Vector2.zero;
            if (currentTime >= nextAttackTime) BeginWindUp(currentTime);
        }

        public void BeginWindUp(float currentTime)
        {
            if (IsWindingUp) return;
            windUpEndsAt = currentTime + windUpDuration;
            if (windUpDuration <= 0f) Throw(currentTime);
        }

        private void Throw(float currentTime)
        {
            // Aimed at the player's shadow, so the mark sits on the floor under the player.
            Vector2 landing = target != null ? EnemyLobProjectile.GroundPoint(target) : (Vector2)transform.position;
            CancelWindUp();
            nextAttackTime = currentTime + attackInterval;
            LastThrowTime = currentTime;
            LastProjectile = EnemyLobProjectile.Launch(gameObject, transform.position, landing, currentTime,
                flightDuration, arcHeight, landingRadius, damageTier, projectileSprite, launchHeight);
            ProjectileThrown?.Invoke(LastProjectile);
        }

        private void CancelWindUp()
        {
            windUpEndsAt = float.NaN;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
            behavior = GetComponent<EnemyBehaviorContext>();
            if (behavior == null) behavior = gameObject.AddComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            if (target != null) behavior.SetTarget(target);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, minimumAttackDistance);
            Gizmos.DrawWireSphere(transform.position, maximumAttackDistance);
        }
    }
}
