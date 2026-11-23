using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// 불효자손-순수 (2026-11-23): walks into reach and whips its ribbon at the player from any direction. The aim
    /// locks when the wind-up starts, so stepping out of the warning band dodges it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    [RequireComponent(typeof(EnemyBehaviorContext), typeof(EnemyAttackPresentation))]
    public sealed class EnemyRibbonLashController : MonoBehaviour, IKnockbackPushBlocker
    {
        [SerializeField, Min(0f)] private float moveSpeed = 2.4f;
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0.1f)] private float reach = 1.8f;
        // The ribbon is drawn at head height, above the body's root.
        [SerializeField] private float ribbonHeight = 0.29f;
        [SerializeField, Min(0.05f)] private float hitHalfWidth = 0.4f;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.16f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.5f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Light;

        private static readonly Color WarningColor = new(1f, 0.25f, 0.1f, 0.4f);

        private Rigidbody2D body;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private EnemyAttackArtwork artwork;
        private LineRenderer band;
        private MeleeWarningVisual warning;
        private readonly EnemyObstacleNavigator navigator = new();
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private EnemyAttackPhase phase = EnemyAttackPhase.Idle;
        private float phaseEndsAt;
        private float nextAttackAt;
        private Vector2 aim = Vector2.left;
        private bool hitApplied;

        public float Reach => reach;
        public float RibbonHeight => ribbonHeight;
        public float HitHalfWidth => hitHalfWidth;
        public EnemyDamageTier DamageTier => damageTier;
        public EnemyAttackPhase Phase => phase;
        public Vector2 Aim => aim;
        public bool BlocksKnockbackPush => phase == EnemyAttackPhase.Active;

        public void Configure(float speed, float detection, float configuredReach, float height, float halfWidth,
            float telegraph, float active, float recovery, float configuredCooldown, EnemyDamageTier tier)
        {
            moveSpeed = Mathf.Max(0f, speed);
            detectionRange = Mathf.Max(0f, detection);
            reach = Mathf.Max(0.1f, configuredReach);
            ribbonHeight = height;
            hitHalfWidth = Mathf.Max(0.05f, halfWidth);
            telegraphDuration = Mathf.Max(0.01f, telegraph);
            activeDuration = Mathf.Max(0.01f, active);
            recoveryDuration = Mathf.Max(0f, recovery);
            cooldown = Mathf.Max(0f, configuredCooldown);
            damageTier = tier;
        }

        // True when a point (relative to the body's root) is on the ribbon whipped along direction from head height.
        public static bool IsOnRibbon(Vector2 offset, Vector2 direction, float reach, float height, float halfWidth)
        {
            Vector2 fromHead = offset - Vector2.up * height;
            float along = Vector2.Dot(fromHead, direction);
            return along >= 0f && along <= reach && (fromHead - direction * along).sqrMagnitude <= halfWidth * halfWidth;
        }

        private void Awake() => Cache();

        private void Cache()
        {
            body = GetComponent<Rigidbody2D>();
            behavior = GetComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            presentation = GetComponent<EnemyAttackPresentation>();
            artwork = GetComponent<EnemyAttackArtwork>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
        }

        private void OnDisable() => Cancel();

        private void FixedUpdate()
        {
            float now = Time.time;
            if (!behavior.TryAcquireOrAlert(detectionRange) || behavior.IsActionSuppressed)
            {
                Cancel();
                behavior.StopForSuppression();
                return;
            }

            Vector2 offset = behavior.Target.position - transform.position;
            switch (phase)
            {
                case EnemyAttackPhase.Idle:
                    if (now >= nextAttackAt && CanLash(offset))
                    {
                        aim = AimFrom(offset);
                        body.linearVelocity = Vector2.zero;
                        hitApplied = false;
                        artwork?.HoldFacing(aim);
                        SetPhase(EnemyAttackPhase.Telegraph, now + telegraphDuration);
                        ShowBand(true);
                    }
                    else
                    {
                        Approach(offset, now);
                    }

                    break;
                case EnemyAttackPhase.Telegraph:
                    body.linearVelocity = Vector2.zero;
                    if (now >= phaseEndsAt)
                    {
                        ShowBand(false);
                        EnemyProjectile.PlayAttackSound(gameObject);
                        SetPhase(EnemyAttackPhase.Active, now + activeDuration);
                    }

                    break;
                case EnemyAttackPhase.Active:
                    body.linearVelocity = Vector2.zero;
                    TryHit(offset);
                    if (now >= phaseEndsAt)
                        SetPhase(EnemyAttackPhase.Recovery, now + EnemyVariant.AttackIntervalOf(this, recoveryDuration));
                    break;
                case EnemyAttackPhase.Recovery:
                    body.linearVelocity = Vector2.zero;
                    if (now >= phaseEndsAt)
                    {
                        nextAttackAt = now + EnemyVariant.AttackIntervalOf(this, cooldown);
                        artwork?.HoldFacing(Vector2.zero);
                        SetPhase(EnemyAttackPhase.Idle, 0f);
                    }

                    break;
            }
        }

        private bool CanLash(Vector2 offset)
        {
            Vector2 fromHead = offset - Vector2.up * ribbonHeight;
            if (fromHead.sqrMagnitude > reach * reach) return false;
            Vector2 from = (Vector2)transform.position + Vector2.up * ribbonHeight;
            return EnemyObstacleNavigator.HasLineOfFire(from, from + fromHead);
        }

        private Vector2 AimFrom(Vector2 offset)
        {
            Vector2 fromHead = offset - Vector2.up * ribbonHeight;
            return fromHead.sqrMagnitude > 0.0001f ? fromHead.normalized : aim;
        }

        // Walks to the spot on its current bearing from which the ribbon reaches the player.
        private void Approach(Vector2 offset, float now)
        {
            Vector2 goal = (Vector2)behavior.Target.position - Vector2.up * ribbonHeight -
                           AimFrom(offset) * (reach * 0.7f);
            if (((Vector2)transform.position - goal).sqrMagnitude <= 0.01f)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = navigator.GetMoveDirection(transform.position, goal, terrain, now) *
                                  (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
        }

        private void TryHit(Vector2 offset)
        {
            if (hitApplied || !behavior.HasLivingTarget ||
                !IsOnRibbon(offset, aim, reach, ribbonHeight, hitHalfWidth)) return;
            Health target = behavior.TargetHealth;
            if (target.GetComponent<PlayerMovement>() == null) return;
            hitApplied = true;
            target.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            target.TakeDamage(HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyMelee, damageTier));
        }

        private void SetPhase(EnemyAttackPhase next, float endsAt)
        {
            phase = next;
            phaseEndsAt = endsAt;
            presentation.SetPhase(next);
        }

        private void ShowBand(bool show)
        {
            if (band == null)
            {
                if (!show) return;
                SpriteRenderer sprite = GetComponent<SpriteRenderer>();
                band = gameObject.AddComponent<LineRenderer>();
                band.useWorldSpace = true;
                band.positionCount = 2;
                band.sortingLayerID = sprite.sortingLayerID;
                band.sortingOrder = sprite.sortingOrder - 1;
                band.sharedMaterial = sprite.sharedMaterial;
                band.startColor = band.endColor = WarningColor;
                warning = MeleeWarningVisual.Bind(band, hitHalfWidth, 0.3f, -1);
            }

            band.enabled = show;
            if (!show) return;
            band.startWidth = band.endWidth = hitHalfWidth * 2f;
            Vector2 from = (Vector2)transform.position + Vector2.up * ribbonHeight;
            band.SetPosition(0, from);
            band.SetPosition(1, from + aim * reach);
            warning?.SetTiming(telegraphDuration, telegraphDuration, true);
        }

        private void Cancel()
        {
            if (phase != EnemyAttackPhase.Idle)
                nextAttackAt = Mathf.Max(nextAttackAt, Time.time + cooldown);
            ShowBand(false);
            artwork?.HoldFacing(Vector2.zero);
            phase = EnemyAttackPhase.Idle;
            if (presentation != null) presentation.SetPhase(EnemyAttackPhase.Idle);
        }
    }
}
