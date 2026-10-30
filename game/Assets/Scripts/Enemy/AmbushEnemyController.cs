using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-13: 잠복형 (docs/28 §8.4). It moves under the floor, where it cannot be hit and does not touch anything but
    // terrain, until it is right under the player's feet (its shadow on the player's shadow). There it stops and a red
    // ellipse on the floor warns where it will burst out. It then pops up: every player whose feet are inside the
    // ellipse takes the floor's damage once. It stays up for a while, stuck in the ground (Kinematic, so the player
    // cannot push it, and knockback-immune) and hurts on touch, then sinks and starts over. A player who keeps moving
    // can lead it around; it never bursts out away from the player.
    // The user will draw buried, emerging and attacking art later; until then plain colours tell the states apart.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class AmbushEnemyController : MonoBehaviour
    {
        public enum AmbushState { Burrowed, Emerging, Attacking, Surfaced, Burrowing }

        public const string MarkName = "Ambush Emerge Mark";
        public const string DeathReason = "ENEMY";
        // Under actors, over the floor, like the other floor warnings.
        private const int MarkSortingOrder = 1;
        private const float SearchMargin = 1.5f;

        // Placeholder colours until the user's art (2026-10-30): buried is a dim, see-through earth brown, the
        // burst a strong red.
        public static readonly Color BurrowedColor = new(0.42f, 0.27f, 0.14f, 0.55f);
        public static readonly Color AttackColor = new(1f, 0.3f, 0.25f, 1f);

        [Header("Movement")]
        [SerializeField, Min(0f)] private float burrowedMoveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        // How close its shadow must come to the player's shadow (feet) before it bursts out.
        [SerializeField, Min(0f)] private float emergeRange = 0.3f;
        // It must travel this long under the floor before it may burst out again.
        [SerializeField, Min(0f)] private float minimumBurrowDuration = 1f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float emergeTelegraphDuration = 0.7f;
        [SerializeField, Min(0f)] private float attackDuration = 0.3f;
        [SerializeField, Min(0f)] private float surfacedDuration = 2f;
        [SerializeField, Min(0f)] private float burrowDuration = 0.4f;
        // Half the ellipse's width; its half height is half of that, like the lob mark.
        [SerializeField, Min(0.05f)] private float hitRadius = 1f;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Medium;

        private Rigidbody2D body;
        private Health health;
        private EnemyBehaviorContext behavior;
        private SpriteRenderer visual;
        private Collider2D[] rootColliders = Array.Empty<Collider2D>();
        private readonly EnemyObstacleNavigator navigator = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private float stateStartedAt = float.NegativeInfinity;
        private bool initialized;

        public const float MarkAspect = EnemyLobProjectile.MarkAspect;

        public float BurrowedMoveSpeed => burrowedMoveSpeed;
        public float DetectionRange => detectionRange;
        public float EmergeRange => emergeRange;
        public float MinimumBurrowDuration => minimumBurrowDuration;
        public float EmergeTelegraphDuration => emergeTelegraphDuration;
        public float AttackDuration => attackDuration;
        public float SurfacedDuration => surfacedDuration;
        public float BurrowDuration => burrowDuration;
        public float HitRadius => hitRadius;
        public float HitHalfHeight => hitRadius * MarkAspect;
        public EnemyDamageTier DamageTier => damageTier;
        public AmbushState State { get; private set; } = AmbushState.Burrowed;
        public float StateStartedAt => stateStartedAt;
        public bool IsBurrowed => State is AmbushState.Burrowed or AmbushState.Emerging;
        public SpriteRenderer Mark { get; private set; }
        public SpriteRenderer Visual => visual;
        public Vector2 EmergePoint { get; private set; }
        public int LastHitCount { get; private set; }
        public float LastAttackTime { get; private set; } = float.NegativeInfinity;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        public event Action<AmbushEnemyController> Emerged;

        public void Configure(float configuredBurrowedMoveSpeed, float configuredDetectionRange,
            float configuredEmergeRange, float configuredMinimumBurrowDuration,
            float configuredEmergeTelegraphDuration, float configuredAttackDuration, float configuredSurfacedDuration,
            float configuredBurrowDuration, float configuredHitRadius, EnemyDamageTier configuredDamageTier)
        {
            burrowedMoveSpeed = Mathf.Max(0f, configuredBurrowedMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            emergeRange = Mathf.Max(0f, configuredEmergeRange);
            minimumBurrowDuration = Mathf.Max(0f, configuredMinimumBurrowDuration);
            emergeTelegraphDuration = Mathf.Max(0f, configuredEmergeTelegraphDuration);
            attackDuration = Mathf.Max(0f, configuredAttackDuration);
            surfacedDuration = Mathf.Max(0f, configuredSurfacedDuration);
            burrowDuration = Mathf.Max(0f, configuredBurrowDuration);
            hitRadius = Mathf.Max(0.05f, configuredHitRadius);
            damageTier = configuredDamageTier;
        }

        private void Awake()
        {
            CacheComponents();
            EnterBurrowed(Time.time);
        }

        // Spawned, or shown again with its room, it starts under the floor.
        private void OnEnable()
        {
            CacheComponents();
            EnterBurrowed(Time.time);
        }

        private void OnDisable()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            RemoveMark();
        }

        private void OnDestroy()
        {
            RemoveMark();
        }

        private void FixedUpdate()
        {
            TickBehavior(Time.time);
        }

        public void TickBehavior(float currentTime)
        {
            if (!initialized) CacheComponents();
            if (health == null || health.IsDead)
            {
                RemoveMark();
                return;
            }

            float elapsed = currentTime - stateStartedAt;
            switch (State)
            {
                case AmbushState.Burrowed:
                    TickBurrowed(currentTime, elapsed);
                    break;
                case AmbushState.Emerging:
                    body.linearVelocity = Vector2.zero;
                    if (Mark != null)
                        Mark.color = EnemyLobProjectile.MarkColor(emergeTelegraphDuration > 0f
                            ? elapsed / emergeTelegraphDuration
                            : 1f);
                    if (elapsed >= emergeTelegraphDuration) Burst(currentTime);
                    break;
                case AmbushState.Attacking:
                    HoldStill();
                    if (elapsed >= attackDuration) SetState(AmbushState.Surfaced, currentTime);
                    break;
                case AmbushState.Surfaced:
                    HoldStill();
                    if (elapsed >= surfacedDuration) SetState(AmbushState.Burrowing, currentTime);
                    break;
                case AmbushState.Burrowing:
                    HoldStill();
                    if (visual != null)
                        visual.color = Color.Lerp(Color.white, BurrowedColor,
                            burrowDuration > 0f ? elapsed / burrowDuration : 1f);
                    if (elapsed >= burrowDuration) EnterBurrowed(currentTime);
                    break;
            }
        }

        private void TickBurrowed(float currentTime, float elapsed)
        {
            // It waits under the floor until it notices a living player and the spawn delay has passed.
            if (!behavior.TryAcquireOrAlert(detectionRange) || behavior.IsSpawnActionDelayed)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            // It steers its own shadow onto the player's shadow, so the burst lands under the player's feet.
            Vector2 offset = FeetOffsetToTarget();
            bool underFeet = offset.sqrMagnitude <= emergeRange * emergeRange;
            if (underFeet && elapsed >= minimumBurrowDuration)
            {
                BeginEmerging(currentTime);
                return;
            }

            // Under the feet but still early: it holds there rather than circling.
            if (underFeet)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float speed = burrowedMoveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this);
            Vector2 from = transform.position;
            Vector2 direction = navigator.GetMoveDirection(from, from + offset, terrain, currentTime);
            // The last step stops on the feet instead of passing them.
            float distance = offset.magnitude;
            float dt = Time.fixedDeltaTime;
            body.linearVelocity = direction * (dt > 0f && speed * dt > distance ? distance / dt : speed);
        }

        // From its own shadow to the player's shadow.
        public Vector2 FeetOffsetToTarget()
        {
            Transform target = behavior != null ? behavior.Target : null;
            if (target == null) return Vector2.zero;
            return EnemyLobProjectile.GroundPoint(target) - EnemyLobProjectile.GroundPoint(transform);
        }

        public void BeginEmerging(float currentTime)
        {
            if (State != AmbushState.Burrowed || health == null || health.IsDead) return;
            body.linearVelocity = Vector2.zero;
            EmergePoint = EnemyLobProjectile.GroundPoint(transform);
            SetState(AmbushState.Emerging, currentTime);
            CreateMark();
        }

        // It bursts out: hittable again, and the players standing on the ellipse are hurt once.
        private void Burst(float currentTime)
        {
            RemoveMark();
            SetHidden(false);
            SetAnchored(true);
            SetState(AmbushState.Attacking, currentTime);
            LastAttackTime = currentTime;
            LastHitCount = 0;
            DamageContext context = HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyMelee,
                damageTier);
            HashSet<Health> damaged = new();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(EmergePoint, hitRadius + SearchMargin,
                         LayerMask.GetMask(EnemyLobProjectile.PlayerLayerName)))
            {
                Health player = hit != null ? hit.GetComponentInParent<Health>() : null;
                // One player has several colliders; it is hurt once.
                if (player == null || player.IsDead || player.IsInvulnerable || !damaged.Add(player) ||
                    !Covers(EnemyLobProjectile.GroundPoint(player.transform)))
                    continue;
                player.GetComponent<PlayerDeathReason>()?.SetReason(DeathReason);
                player.TakeDamage(context);
                LastHitCount++;
            }

            Emerged?.Invoke(this);
        }

        public bool Covers(Vector2 groundPoint)
        {
            Vector2 offset = groundPoint - EmergePoint;
            float x = offset.x / hitRadius, y = offset.y / HitHalfHeight;
            return x * x + y * y <= 1f;
        }

        private void EnterBurrowed(float currentTime)
        {
            RemoveMark();
            SetAnchored(false);
            SetHidden(true);
            SetState(AmbushState.Burrowed, currentTime);
        }

        private void SetState(AmbushState state, float currentTime)
        {
            State = state;
            stateStartedAt = currentTime;
            if (visual == null) return;
            visual.color = state switch
            {
                AmbushState.Burrowed or AmbushState.Emerging => BurrowedColor,
                AmbushState.Attacking => AttackColor,
                _ => Color.white,
            };
        }

        // Under the floor it cannot be hurt and its body circle (hurtbox, enemy push) is off; the feet keep it on
        // terrain, so it still goes around walls, obstacles and pits.
        private void SetHidden(bool hidden)
        {
            if (health != null) health.SetInvulnerable(hidden);
            foreach (Collider2D collider in rootColliders)
                if (collider != null) collider.enabled = !hidden;
        }

        private void HoldStill()
        {
            body.linearVelocity = Vector2.zero;
        }

        // Up out of the ground it is stuck in place: a Kinematic body is not pushed by the player or other enemies.
        // Under the floor it is Dynamic again so its feet slide along walls and obstacles.
        private void SetAnchored(bool anchored)
        {
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.bodyType = anchored ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }

        public bool IsAnchored => body != null && body.bodyType == RigidbodyType2D.Kinematic;

        private void CreateMark()
        {
            RemoveMark();
            GameObject mark = new(MarkName);
            mark.transform.SetParent(transform, false);
            mark.transform.position = EmergePoint;
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            // The shared mask is a unit square, so the scale is the size.
            mark.transform.localScale = new Vector3(hitRadius * 2f / sx, HitHalfHeight * 2f / sy, 1f);
            Mark = mark.AddComponent<SpriteRenderer>();
            Mark.sprite = GroundShadow.SharedSprite;
            Mark.color = EnemyLobProjectile.MarkColor(0f);
            Mark.sortingOrder = MarkSortingOrder;
        }

        private void RemoveMark()
        {
            if (Mark == null) return;
            if (Application.isPlaying) Destroy(Mark.gameObject);
            else DestroyImmediate(Mark.gameObject);
            Mark = null;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
            behavior = GetComponent<EnemyBehaviorContext>();
            if (behavior == null) behavior = gameObject.AddComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            FairyKingdomArtworkView artwork = GetComponent<FairyKingdomArtworkView>();
            visual = artwork != null && artwork.Visual != null ? artwork.Visual : GetComponentInChildren<SpriteRenderer>();
            // Only the root's colliders; the feet on their own child stay on.
            rootColliders = GetComponents<Collider2D>();
            initialized = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, emergeRange);
        }
    }
}
