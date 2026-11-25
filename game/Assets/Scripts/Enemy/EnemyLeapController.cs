using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum EnemyLeapState { Resting, Crouching, Airborne, Landing }

    /// <summary>
    /// 불효자손-활발 (2026-11-23): travels only by quick, slightly erratic leaps toward the player and hurts on landing.
    /// The picture rises above its ground shadow. While airborne it can neither be hit nor hit: attacks pass under it
    /// and it only hurts on landing. The feet stay on the floor collider, so a leap still stops at walls and pits.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    [RequireComponent(typeof(EnemyBehaviorContext), typeof(EnemyAttackArtwork))]
    public sealed class EnemyLeapController : MonoBehaviour, IKnockbackPushBlocker
    {
        public const float MaximumTurnDegrees = 35f;

        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0.1f)] private float leapDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float crouchDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float airDuration = 0.58f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float landingDuration = 0.2f;
        [SerializeField, Min(0f)] private float minimumRest = 0.15f;
        [SerializeField, Min(0f)] private float maximumRest = 0.45f;
        [SerializeField, Min(0.05f)] private float landingRadius = 0.85f;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Light;

        private Rigidbody2D body;
        private EnemyBehaviorContext behavior;
        private EnemyAttackArtwork artwork;
        private EnemyHopMotion hop;
        private Health health;
        private Collider2D hitbox;
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private float stateStartedAt;
        private float stateEndsAt;
        private Vector2 leapVelocity;

        public EnemyLeapState State { get; private set; } = EnemyLeapState.Resting;
        public float LeapDistance => leapDistance;
        public float JumpHeight => jumpHeight;
        public float AirDuration => airDuration;
        public bool IsAirborne { get; private set; }
        public float LandingRadius => landingRadius;
        public EnemyDamageTier DamageTier => damageTier;
        public bool BlocksKnockbackPush => State == EnemyLeapState.Airborne;

        public void Configure(float detection, float distance, float crouch, float air, float height, float landing,
            float restMin, float restMax, float radius, EnemyDamageTier tier)
        {
            detectionRange = Mathf.Max(0f, detection);
            leapDistance = Mathf.Max(0.1f, distance);
            crouchDuration = Mathf.Max(0.01f, crouch);
            airDuration = Mathf.Max(0.01f, air);
            jumpHeight = Mathf.Max(0f, height);
            landingDuration = Mathf.Max(0f, landing);
            minimumRest = Mathf.Max(0f, restMin);
            maximumRest = Mathf.Max(minimumRest, restMax);
            landingRadius = Mathf.Max(0.05f, radius);
            damageTier = tier;
        }

        // roll is uniform in [0, 1). Squared toward zero, so most leaps go at the player and a few veer off.
        public static Vector2 ChooseDirection(Vector2 toward, float roll)
        {
            Vector2 forward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector2.right;
            float signed = Mathf.Clamp01(roll) * 2f - 1f;
            float radians = Mathf.Sign(signed) * signed * signed * MaximumTurnDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            return new Vector2(forward.x * cos - forward.y * sin, forward.x * sin + forward.y * cos);
        }

        // Height of the picture at progress 0~1 of the jump.
        public static float ArcHeight(float progress, float height)
        {
            float p = Mathf.Clamp01(progress);
            return 4f * height * p * (1f - p);
        }

        // Public so verification can check the airborne rule without running physics.
        public void SetAirborne(bool airborne)
        {
            if (health == null) health = GetComponent<Health>();
            if (hitbox == null) hitbox = GetComponent<Collider2D>();
            if (hop == null) hop = GetComponent<EnemyHopMotion>();
            IsAirborne = airborne;
            health.SetInvulnerable(airborne);
            if (hitbox != null) hitbox.enabled = !airborne;
            if (!airborne && hop != null) hop.HoldLift(-1f);
        }

        private void Awake()
        {
            hop = GetComponent<EnemyHopMotion>();
            body = GetComponent<Rigidbody2D>();
            behavior = GetComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            artwork = GetComponent<EnemyAttackArtwork>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
        }

        private void OnDisable() => Interrupt();

        private void Update()
        {
            if (State == EnemyLeapState.Airborne && hop != null)
                hop.HoldLift(ArcHeight((Time.time - stateStartedAt) / airDuration, jumpHeight));
        }

        private void FixedUpdate()
        {
            float now = Time.time;
            if (!behavior.TryAcquireOrAlert(detectionRange) || behavior.IsActionSuppressed)
            {
                Interrupt();
                behavior.StopForSuppression();
                return;
            }

            switch (State)
            {
                case EnemyLeapState.Resting:
                    body.linearVelocity = Vector2.zero;
                    if (now >= stateEndsAt) BeginCrouch(now);
                    break;
                case EnemyLeapState.Crouching:
                    body.linearVelocity = Vector2.zero;
                    if (now >= stateEndsAt) BeginAir(now);
                    break;
                case EnemyLeapState.Airborne:
                    body.linearVelocity = leapVelocity;
                    artwork.HoldPose(artwork.GetFrame(now - stateStartedAt < airDuration * 0.5f ? 1 : 2));
                    if (now >= stateEndsAt) Land(now);
                    break;
                case EnemyLeapState.Landing:
                    body.linearVelocity = Vector2.zero;
                    if (now >= stateEndsAt) Rest(now);
                    break;
            }
        }

        private void BeginCrouch(float now)
        {
            Vector2 toward = behavior.Target.position - transform.position;
            Vector2 direction = ChooseDirection(toward, Random.value);
            float distance = Mathf.Min(leapDistance, toward.magnitude + 0.3f);
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(terrain.Center(transform.position), terrain.Radius,
                         direction, distance, EnemyObstacleNavigator.ObstacleMask))
            {
                if (hit.collider != null && !hit.collider.isTrigger) distance = Mathf.Min(distance, hit.distance);
            }

            leapVelocity = direction * (distance / airDuration);
            artwork.HoldFacing(direction);
            artwork.HoldPose(artwork.GetFrame(0));
            Enter(EnemyLeapState.Crouching, now, crouchDuration);
        }

        private void BeginAir(float now)
        {
            EnemyProjectile.PlayAttackSound(gameObject);
            SetAirborne(true);
            Enter(EnemyLeapState.Airborne, now, airDuration);
        }

        private void Land(float now)
        {
            SetAirborne(false);
            body.linearVelocity = Vector2.zero;
            artwork.HoldPose(artwork.GetFrame(3));
            Enter(EnemyLeapState.Landing, now, landingDuration);
            if (!behavior.HasLivingTarget ||
                ((Vector2)behavior.Target.position - (Vector2)transform.position).sqrMagnitude >
                landingRadius * landingRadius) return;
            Health target = behavior.TargetHealth;
            if (target.GetComponent<PlayerMovement>() == null) return;
            target.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            target.TakeDamage(HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyMelee, damageTier));
        }

        private void Rest(float now)
        {
            artwork.HoldPose(null);
            artwork.HoldFacing(Vector2.zero);
            Enter(EnemyLeapState.Resting, now,
                EnemyVariant.AttackIntervalOf(this, Random.Range(minimumRest, maximumRest)));
        }

        private void Enter(EnemyLeapState state, float now, float duration)
        {
            State = state;
            stateStartedAt = now;
            stateEndsAt = now + duration;
        }

        private void Interrupt()
        {
            if (State == EnemyLeapState.Resting) return;
            if (IsAirborne) SetAirborne(false);
            artwork?.HoldPose(null);
            artwork?.HoldFacing(Vector2.zero);
            Enter(EnemyLeapState.Resting, Time.time, minimumRest);
        }
    }
}
