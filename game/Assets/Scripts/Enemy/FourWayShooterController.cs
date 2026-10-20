using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum FourWayShooterState
    {
        Idle,
        Moving,
        Telegraph,
        Recovering,
    }

    // Enemy-8 (user decision, 2026-10-09): 저혈당 요정-활발 walks one way for a while, stops, warns, and fires one
    // shot each up, down, left and right. The shots do not aim at the player, so it fires without a line of fire.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class FourWayShooterController : MonoBehaviour
    {
        public static readonly Vector2[] FireDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        // How far a move may turn away from the player. The turn is squared toward zero, so most moves lean toward
        // the player while a few still go sideways or slightly back.
        public const float MaximumTurnDegrees = 120f;
        // Length of each telegraph arm from the body center.
        public const float WarningLength = 1.4f;
        public const float WarningWidth = 0.35f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0.01f)] private float moveDuration = 1.2f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.4f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
        [SerializeField] private EnemyDamageTier projectileDamageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 1.2f;
        // Drawn shot pointing left, sized like the sniper's green onion. Without it the body sprite is tinted instead.
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private SpriteRenderer spriteRenderer;
        private LineRenderer[] warnings;
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private float stateEndsAt;
        private Vector2 moveDirection;

        public FourWayShooterState State { get; private set; }
        public Vector2 MoveDirection => moveDirection;
        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float MoveDuration => moveDuration;
        public float TelegraphDuration => telegraphDuration;
        public float RecoveryDuration => recoveryDuration;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float Range => projectileSpeed * projectileLifetime;
        public EnemyDamageTier ProjectileDamageTier => projectileDamageTier;
        public Sprite ProjectileSprite => projectileSprite;
        public float StateEndsAt => stateEndsAt;
        public bool IsWarningVisible => warnings != null && warnings.Length > 0 && warnings[0] != null &&
                                        warnings[0].enabled;
        public bool IsActionSuppressed => behavior == null || behavior.IsActionSuppressed;

        public event Action<FourWayShooterState> StateChanged;
        public event Action<EnemyProjectile[]> VolleyFired;

        private void Awake()
        {
            CacheComponents();
            SetState(FourWayShooterState.Idle);
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
            bool blocked = ((1 << collision.collider.gameObject.layer) & EnemyObstacleNavigator.ObstacleMask) != 0 ||
                           collision.collider.GetComponentInParent<EnemyBehaviorContext>() != null;
            if (blocked) NotifyBlocked();
        }

        private void OnDisable()
        {
            CancelPattern(true);
        }

        public void Configure(float configuredMoveSpeed, float configuredDetectionRange, float configuredMoveDuration,
            float configuredTelegraphDuration, float configuredRecoveryDuration, float configuredProjectileSpeed,
            EnemyDamageTier configuredProjectileDamageTier, float configuredProjectileLifetime)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            moveDuration = Mathf.Max(0.01f, configuredMoveDuration);
            telegraphDuration = Mathf.Max(0.01f, configuredTelegraphDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            projectileSpeed = Mathf.Max(0.01f, configuredProjectileSpeed);
            projectileDamageTier = configuredProjectileDamageTier;
            projectileLifetime = Mathf.Max(0.01f, configuredProjectileLifetime);
            SetState(FourWayShooterState.Idle);
        }

        public void ConfigureProjectileSprite(Sprite configuredProjectileSprite)
        {
            projectileSprite = configuredProjectileSprite;
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

        // roll is uniform in [0, 1). 0.5 walks straight at the player; the ends turn MaximumTurnDegrees either way.
        public static Vector2 ChooseMoveDirection(Vector2 towardTarget, float roll)
        {
            Vector2 forward = towardTarget.sqrMagnitude > 0.0001f ? towardTarget.normalized : Vector2.right;
            float signed = Mathf.Clamp(roll, 0f, 1f) * 2f - 1f;
            float degrees = Mathf.Sign(signed) * signed * signed * MaximumTurnDegrees;
            return Rotate(forward, degrees);
        }

        public void TickBehavior(float currentTime)
        {
            if (body == null || health == null || knockback == null) CacheComponents();
            if (!behavior.TryAcquireOrAlert(detectionRange))
            {
                CancelPattern(true);
                return;
            }

            target = behavior.Target;
            if (behavior.IsActionSuppressed)
            {
                // A knockback or stun interrupts the warning; the cycle starts over with a new move.
                CancelPattern(!knockback.IsKnockedBack);
                return;
            }

            switch (State)
            {
                case FourWayShooterState.Idle:
                    BeginMove(currentTime, UnityEngine.Random.value);
                    break;
                case FourWayShooterState.Moving:
                    if (currentTime >= stateEndsAt)
                    {
                        BeginTelegraph(currentTime);
                        break;
                    }

                    behavior.SetControllerMovementSuppressed(false);
                    body.linearVelocity = moveDirection * (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
                    break;
                case FourWayShooterState.Telegraph:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt) Fire(currentTime);
                    break;
                case FourWayShooterState.Recovering:
                    behavior.SetControllerMovementSuppressed(true);
                    body.linearVelocity = Vector2.zero;
                    if (currentTime >= stateEndsAt) BeginMove(currentTime, UnityEngine.Random.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void BeginMove(float currentTime, float roll)
        {
            Vector2 toward = target != null ? (Vector2)(target.position - transform.position) : Vector2.right;
            Vector2 wanted = ChooseMoveDirection(toward, roll);
            // Turn to the nearest open direction; boxed in on every side, it waits out this move in place.
            moveDirection = EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, wanted, terrain,
                out Vector2 open)
                ? open
                : Vector2.zero;
            stateEndsAt = currentTime + moveDuration;
            behavior?.SetControllerMovementSuppressed(false);
            SetState(FourWayShooterState.Moving);
        }

        // Hitting a wall, an obstacle or another enemy mid-move turns it toward the nearest open direction.
        public bool NotifyBlocked()
        {
            if (State != FourWayShooterState.Moving || moveDirection == Vector2.zero) return false;
            moveDirection = EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, -moveDirection,
                terrain, out Vector2 open)
                ? open
                : Vector2.zero;
            return true;
        }

        private void BeginTelegraph(float currentTime)
        {
            body.linearVelocity = Vector2.zero;
            behavior.SetControllerMovementSuppressed(true);
            stateEndsAt = currentTime + telegraphDuration;
            SetState(FourWayShooterState.Telegraph);
        }

        public EnemyProjectile[] Fire(float currentTime)
        {
            bool drawn = projectileSprite != null;
            Sprite sprite = drawn ? projectileSprite : spriteRenderer != null ? spriteRenderer.sprite : null;
            EnemyProjectile[] volley = new EnemyProjectile[FireDirections.Length];
            for (int i = 0; i < FireDirections.Length; i++)
            {
                volley[i] = EnemyProjectile.Create(transform.position, FireDirections[i], gameObject,
                    projectileDamageTier, projectileSpeed, projectileLifetime, sprite, drawn);
            }

            stateEndsAt = currentTime + recoveryDuration;
            SetState(FourWayShooterState.Recovering);
            VolleyFired?.Invoke(volley);
            return volley;
        }

        private void CancelPattern(bool stopBody)
        {
            if (stopBody && body != null) body.linearVelocity = Vector2.zero;
            behavior?.SetControllerMovementSuppressed(false);
            moveDirection = Vector2.zero;
            SetState(FourWayShooterState.Idle);
        }

        private void SetState(FourWayShooterState next)
        {
            bool changed = State != next;
            State = next;
            presentation?.SetPhase(State switch
            {
                FourWayShooterState.Telegraph => EnemyAttackPhase.Telegraph,
                FourWayShooterState.Recovering => EnemyAttackPhase.Recovery,
                _ => EnemyAttackPhase.Idle,
            });
            UpdateWarnings();
            if (changed) StateChanged?.Invoke(State);
        }

        private void UpdateWarnings()
        {
            if (warnings == null) return;
            bool show = State == FourWayShooterState.Telegraph;
            for (int i = 0; i < warnings.Length; i++)
            {
                LineRenderer line = warnings[i];
                if (line == null) continue;
                line.enabled = show;
                if (show)
                {
                    line.SetPosition(0, transform.position);
                    line.SetPosition(1, (Vector2)transform.position + FireDirections[i] * WarningLength);
                }

                ChargeWarningVisual.Bind(line, WarningWidth, show);
            }
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
            if (warnings == null) warnings = CreateWarnings();
        }

        // One line per shot direction. The lines are built at runtime so the saved Prefab holds no extra children.
        private LineRenderer[] CreateWarnings()
        {
            LineRenderer[] lines = new LineRenderer[FireDirections.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                GameObject owner = new($"Four-way warning {i}");
                owner.transform.SetParent(transform, false);
                LineRenderer line = owner.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.08f;
                line.endWidth = 0.03f;
                line.startColor = new Color(1f, 0.8f, 0.3f, 0.9f);
                line.endColor = new Color(1f, 0.4f, 0.15f, 0.3f);
                line.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
                if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
                    line.sharedMaterial = spriteRenderer.sharedMaterial;
                line.enabled = false;
                lines[i] = line;
            }

            return lines;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.cyan;
            foreach (Vector2 direction in FireDirections)
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction * Range);
        }
    }
}
