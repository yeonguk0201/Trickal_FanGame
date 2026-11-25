using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Shared body of the attribute shooters (불효자손-우울, 산사모-냉정, 2026-11-23): keeps its distance, stops, winds up
    /// and fires once. Subclasses decide what leaves the hand; their drawn frames follow the attack phases.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    [RequireComponent(typeof(EnemyBehaviorContext), typeof(EnemyAttackPresentation))]
    public abstract class EnemyStandoffShooter : MonoBehaviour
    {
        [SerializeField, Min(0f)] protected float moveSpeed = 2.4f;
        [SerializeField, Min(0f)] protected float detectionRange = 12f;
        [SerializeField, Min(0f)] protected float preferredDistance = 5f;
        [SerializeField, Min(0f)] protected float distanceTolerance = 0.9f;
        [SerializeField, Min(0.01f)] protected float telegraphDuration = 0.5f;
        [SerializeField, Min(0.01f)] protected float activeDuration = 0.14f;
        [SerializeField, Min(0f)] protected float recoveryDuration = 0.4f;
        [SerializeField, Min(0f)] protected float restDuration = 1.5f;
        [SerializeField, Min(0.01f)] protected float projectileSpeed = 5.5f;
        [SerializeField] protected EnemyDamageTier damageTier = EnemyDamageTier.Light;
        // The pollen drawing. 불효자손-우울 tints it until its own art arrives; 산사모-냉정 keeps it as drawn (2026-11-24).
        [SerializeField] protected Sprite projectileSprite;
        [SerializeField] protected Color projectileTint = Color.white;

        private readonly EnemyObstacleNavigator navigator = new();
        private readonly RangedStanceKeeper stance = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private EnemyAttackPresentation presentation;
        private EnemyAttackArtwork artwork;
        private float phaseEndsAt;
        private float nextAttackAt;

        protected Rigidbody2D Body { get; private set; }
        protected EnemyBehaviorContext Behavior { get; private set; }
        public EnemyAttackPhase Phase { get; private set; } = EnemyAttackPhase.Idle;
        public float PreferredDistance => preferredDistance;
        public float ProjectileSpeed => projectileSpeed;
        public EnemyDamageTier DamageTier => damageTier;
        public Sprite ProjectileSprite => projectileSprite;
        public Color ProjectileTint => projectileTint;

        public void ConfigureShooter(float speed, float detection, float preferred, float tolerance, float telegraph,
            float active, float recovery, float rest, float shotSpeed, EnemyDamageTier tier, Sprite shot, Color tint)
        {
            moveSpeed = Mathf.Max(0f, speed);
            detectionRange = Mathf.Max(0f, detection);
            preferredDistance = Mathf.Clamp(preferred, 0f, detectionRange);
            distanceTolerance = Mathf.Clamp(tolerance, 0f, preferredDistance);
            telegraphDuration = Mathf.Max(0.01f, telegraph);
            activeDuration = Mathf.Max(0.01f, active);
            recoveryDuration = Mathf.Max(0f, recovery);
            restDuration = Mathf.Max(0f, rest);
            projectileSpeed = Mathf.Max(0.01f, shotSpeed);
            damageTier = tier;
            projectileSprite = shot;
            projectileTint = tint;
        }

        protected abstract void Fire(Vector2 origin, Vector2 direction);

        protected EnemyProjectile CreateShot(Vector2 origin, Vector2 direction, float lifetime)
        {
            EnemyProjectile shot = EnemyProjectile.Create(origin, direction, gameObject, damageTier, projectileSpeed,
                lifetime, projectileSprite, projectileSprite != null, ProjectileSizing.PollenShotScale);
            if (projectileSprite != null) shot.GetComponent<SpriteRenderer>().color = projectileTint;
            return shot;
        }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Behavior = GetComponent<EnemyBehaviorContext>();
            Behavior.Initialize();
            presentation = GetComponent<EnemyAttackPresentation>();
            artwork = GetComponent<EnemyAttackArtwork>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
        }

        private void OnDisable()
        {
            if (Body != null) Body.linearVelocity = Vector2.zero;
            ResetAttack();
        }

        private void FixedUpdate()
        {
            float now = Time.time;
            if (!Behavior.TryAcquireOrAlert(detectionRange) || Behavior.IsActionSuppressed)
            {
                if (Phase != EnemyAttackPhase.Idle) nextAttackAt = now + restDuration * 0.5f;
                ResetAttack();
                Behavior.StopForSuppression();
                return;
            }

            Vector2 offset = Behavior.Target.position - transform.position;
            switch (Phase)
            {
                case EnemyAttackPhase.Idle:
                    bool sight = EnemyObstacleNavigator.HasLineOfFire(transform.position, Behavior.Target.position);
                    Move(offset, sight, now);
                    if (sight && now >= nextAttackAt)
                    {
                        Body.linearVelocity = Vector2.zero;
                        artwork?.HoldFacing(offset);
                        SetPhase(EnemyAttackPhase.Telegraph, now + telegraphDuration);
                    }

                    break;
                case EnemyAttackPhase.Telegraph:
                    Body.linearVelocity = Vector2.zero;
                    if (now >= phaseEndsAt)
                    {
                        Vector2 origin = EnemyProjectile.LaunchOrigin(gameObject, transform.position, true,
                            ProjectileSizing.PollenShotScale);
                        Fire(origin, offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.left);
                        SetPhase(EnemyAttackPhase.Active, now + activeDuration);
                    }

                    break;
                case EnemyAttackPhase.Active:
                    Body.linearVelocity = Vector2.zero;
                    if (now >= phaseEndsAt) SetPhase(EnemyAttackPhase.Recovery, now + recoveryDuration);
                    break;
                case EnemyAttackPhase.Recovery:
                    Body.linearVelocity = Vector2.zero;
                    if (now >= phaseEndsAt)
                    {
                        nextAttackAt = now + EnemyVariant.AttackIntervalOf(this, restDuration);
                        artwork?.HoldFacing(Vector2.zero);
                        SetPhase(EnemyAttackPhase.Idle, 0f);
                    }

                    break;
            }
        }

        private void Move(Vector2 offset, bool sight, float now)
        {
            float speed = moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this);
            if (stance.ShouldSeekSight(sight, now))
            {
                Body.linearVelocity = navigator.GetMoveDirection(transform.position, Behavior.Target.position, terrain,
                    now) * speed;
                return;
            }

            float distance = offset.magnitude;
            int radial = sight ? stance.UpdateRadial(distance, preferredDistance, distanceTolerance) : 0;
            Vector2 direction = distance > 0.001f ? offset / distance : Vector2.zero;
            if (radial > 0)
            {
                Body.linearVelocity = navigator.GetMoveDirection(transform.position, Behavior.Target.position, terrain,
                    now) * speed;
            }
            else if (radial < 0 &&
                     EnemyObstacleNavigator.TryFindRetreatDirection(transform.position, -direction, terrain,
                         out Vector2 retreat))
            {
                Body.linearVelocity = retreat * speed;
            }
            else
            {
                Body.linearVelocity = Vector2.zero;
            }
        }

        private void SetPhase(EnemyAttackPhase next, float endsAt)
        {
            Phase = next;
            phaseEndsAt = endsAt;
            presentation.SetPhase(next);
        }

        private void ResetAttack()
        {
            artwork?.HoldFacing(Vector2.zero);
            Phase = EnemyAttackPhase.Idle;
            if (presentation != null) presentation.SetPhase(EnemyAttackPhase.Idle);
        }
    }
}
