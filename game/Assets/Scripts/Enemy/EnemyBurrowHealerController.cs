using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum EnemyBurrowHealerState { Following, Burrowing, Healing, Emerging }

    /// <summary>
    /// 산사모-우울 (2026-11-23): follows its allies instead of attacking. Near a wounded ally it digs in and links an aura
    /// to the allies around it, healing them, then climbs back out. It can be hit the whole time so a room always ends.
    /// Other healers and bosses are never healed, so two healers cannot keep each other alive.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    [RequireComponent(typeof(EnemyBehaviorContext), typeof(EnemyAttackArtwork))]
    public sealed class EnemyBurrowHealerController : MonoBehaviour
    {
        public const int MaximumLinks = 4;
        private const float ScanInterval = 0.3f;

        [SerializeField, Min(0f)] private float moveSpeed = 2.2f;
        [SerializeField, Min(0f)] private float detectionRange = 14f;
        [SerializeField, Min(0.1f)] private float healRadius = 3.6f;
        [SerializeField, Min(0f)] private float followDistance = 1.8f;
        [SerializeField, Min(0.01f)] private float digDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float healDuration = 3f;
        [SerializeField, Min(0f)] private float healCooldown = 3f;
        [SerializeField, Min(0.05f)] private float healInterval = 0.5f;
        [SerializeField, Range(0f, 1f)] private float healShare = 0.05f;
        [SerializeField] private Sprite[] burrowFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] emergeFrames = System.Array.Empty<Sprite>();

        private readonly EnemyObstacleNavigator navigator = new();
        private readonly List<Health> allies = new();
        private readonly List<Health> linked = new();
        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;
        private EnemyAttackArtwork artwork;
        private SpriteRenderer sprite;
        private EnemyHealAuraVisual aura;
        private EnemyObstacleNavigator.TerrainFootprint terrain;
        private bool wasImmune;
        private float stateStartedAt;
        private float nextScanAt;
        private float nextHealAt;
        private float healReadyAt;

        public EnemyBurrowHealerState State { get; private set; } = EnemyBurrowHealerState.Following;
        public float HealRadius => healRadius;
        public float HealShare => healShare;
        public float HealInterval => healInterval;
        public float HealDuration => healDuration;
        public int LinkedCount => linked.Count;
        public int BurrowFrameCount => burrowFrames.Length;
        public int EmergeFrameCount => emergeFrames.Length;

        public void Configure(float speed, float detection, float radius, float follow, float dig, float heal,
            float cooldown, float interval, float share, Sprite[] burrow, Sprite[] emerge)
        {
            moveSpeed = Mathf.Max(0f, speed);
            detectionRange = Mathf.Max(0f, detection);
            healRadius = Mathf.Max(0.1f, radius);
            followDistance = Mathf.Max(0f, follow);
            digDuration = Mathf.Max(0.01f, dig);
            healDuration = Mathf.Max(0.01f, heal);
            healCooldown = Mathf.Max(0f, cooldown);
            healInterval = Mathf.Max(0.05f, interval);
            healShare = Mathf.Clamp01(share);
            burrowFrames = burrow ?? System.Array.Empty<Sprite>();
            emergeFrames = emerge ?? System.Array.Empty<Sprite>();
        }

        // Whether this healer may restore the other enemy.
        public static bool CanHeal(GameObject other) =>
            other != null && other.GetComponent<EnemyBurrowHealerController>() == null &&
            other.GetComponentInParent<BossController>(true) == null;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            behavior = GetComponent<EnemyBehaviorContext>();
            behavior.Initialize();
            artwork = GetComponent<EnemyAttackArtwork>();
            sprite = GetComponent<SpriteRenderer>();
            terrain = EnemyObstacleNavigator.ResolveFootprint(gameObject);
            wasImmune = knockback.IsKnockbackImmune;
        }

        private void OnDisable() => ReturnToFollowing();

        private void FixedUpdate()
        {
            float now = Time.time;
            if (health.IsDead || !behavior.TryAcquireOrAlert(detectionRange) || behavior.IsSpawnActionDelayed)
            {
                ReturnToFollowing();
                behavior.StopForSuppression();
                return;
            }

            if (now >= nextScanAt)
            {
                nextScanAt = now + ScanInterval;
                ScanAllies();
            }

            switch (State)
            {
                case EnemyBurrowHealerState.Following:
                    if (behavior.IsActionSuppressed)
                    {
                        behavior.StopForSuppression();
                        break;
                    }

                    if (now >= healReadyAt && HasWoundedAllyInRadius())
                    {
                        body.linearVelocity = Vector2.zero;
                        knockback.ConfigureKnockbackImmunity(true);
                        Enter(EnemyBurrowHealerState.Burrowing, now);
                        break;
                    }

                    Follow(now);
                    break;
                case EnemyBurrowHealerState.Burrowing:
                    body.linearVelocity = Vector2.zero;
                    HoldFrame(burrowFrames, (now - stateStartedAt) / digDuration);
                    if (now - stateStartedAt >= digDuration)
                    {
                        nextHealAt = now;
                        Enter(EnemyBurrowHealerState.Healing, now);
                    }

                    break;
                case EnemyBurrowHealerState.Healing:
                    body.linearVelocity = Vector2.zero;
                    HoldFrame(burrowFrames, 1f);
                    if (now >= nextHealAt)
                    {
                        nextHealAt = now + healInterval;
                        HealLinked();
                        if (linked.Count > 0) Aura.Pulse();
                    }

                    if (now - stateStartedAt >= healDuration || !HasWoundedAllyInRadius())
                    {
                        linked.Clear();
                        Enter(EnemyBurrowHealerState.Emerging, now);
                    }

                    break;
                case EnemyBurrowHealerState.Emerging:
                    body.linearVelocity = Vector2.zero;
                    HoldFrame(emergeFrames, (now - stateStartedAt) / digDuration);
                    if (now - stateStartedAt >= digDuration)
                    {
                        healReadyAt = now + healCooldown;
                        ReturnToFollowing();
                    }

                    break;
            }
        }

        private EnemyHealAuraVisual Aura => aura ??= new EnemyHealAuraVisual(transform, sprite);

        // After movement, so the links stay on bodies that moved this frame.
        private void LateUpdate()
        {
            if (State == EnemyBurrowHealerState.Healing || aura != null)
                Aura.Render(transform.position, healRadius, linked, State == EnemyBurrowHealerState.Healing,
                    Time.deltaTime, Time.time);
        }

        private void ScanAllies()
        {
            allies.Clear();
            foreach (EnemyBehaviorContext other in FindObjectsByType<EnemyBehaviorContext>(FindObjectsSortMode.None))
            {
                if (other == behavior || !CanHeal(other.gameObject)) continue;
                Health otherHealth = other.GetComponent<Health>();
                if (otherHealth != null && !otherHealth.IsDead) allies.Add(otherHealth);
            }
        }

        private bool IsLinkable(Health ally) =>
            ally != null && !ally.IsDead &&
            ((Vector2)ally.transform.position - (Vector2)transform.position).sqrMagnitude <= healRadius * healRadius;

        private bool HasWoundedAllyInRadius()
        {
            foreach (Health ally in allies)
                if (IsLinkable(ally) && ally.CurrentHealth < ally.MaxHealth) return true;
            return false;
        }

        // Walks to the most wounded ally, then the nearest one; alone, it trails the player.
        private void Follow(float now)
        {
            Health goal = null;
            float bestRatio = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (Health ally in allies)
            {
                if (ally == null || ally.IsDead) continue;
                float ratio = ally.CurrentHealth / Mathf.Max(1f, ally.MaxHealth);
                float distance = ((Vector2)ally.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (ratio < bestRatio - 0.001f || (Mathf.Abs(ratio - bestRatio) <= 0.001f && distance < bestDistance))
                {
                    goal = ally;
                    bestRatio = ratio;
                    bestDistance = distance;
                }
            }

            Vector2 destination = goal != null ? goal.transform.position : behavior.Target.position;
            if (((Vector2)transform.position - destination).sqrMagnitude <= followDistance * followDistance)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = navigator.GetMoveDirection(transform.position, destination, terrain, now) *
                                  (moveSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this));
        }

        private void HealLinked()
        {
            linked.Clear();
            allies.RemoveAll(ally => ally == null);
            allies.Sort((a, b) => (a.CurrentHealth / Mathf.Max(1f, a.MaxHealth))
                .CompareTo(b.CurrentHealth / Mathf.Max(1f, b.MaxHealth)));
            foreach (Health ally in allies)
            {
                if (linked.Count >= MaximumLinks) break;
                if (!IsLinkable(ally) || ally.CurrentHealth >= ally.MaxHealth) continue;
                linked.Add(ally);
                ally.Heal(ally.MaxHealth * healShare);
            }
        }

        private void HoldFrame(Sprite[] frames, float progress)
        {
            if (frames.Length == 0) return;
            artwork.HoldPose(frames[Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1)]);
        }

        private void Enter(EnemyBurrowHealerState state, float now)
        {
            State = state;
            stateStartedAt = now;
        }

        private void ReturnToFollowing()
        {
            if (State == EnemyBurrowHealerState.Following) return;
            linked.Clear();
            artwork?.HoldPose(null);
            knockback?.ConfigureKnockbackImmunity(wasImmune);
            State = EnemyBurrowHealerState.Following;
        }
    }
}
