using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-11: 대왕쥬비 (user decisions, 2026-10-24 and 2026-10-28). A big flying 쥬비 that keeps about four tiles
    // from the player and buzzes around like a bee instead of hovering in place. Every few seconds it glows for a
    // moment and releases 쥬비 to its left and right. Only its own living 쥬비 count toward the cap, so it stops
    // summoning while that many are alive. The 쥬비 join its room as extra enemies, so the room clears only after
    // they die too, and they stay when 대왕쥬비 dies.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(EnemyBehaviorContext))]
    public sealed class JyubiKingController : MonoBehaviour
    {
        public const float DefaultMoveSpeed = 3f;
        public const float DefaultDetectionRange = 20f;
        public const float DefaultPreferredDistance = 4f;
        // Inside this band around the preferred distance it neither closes in nor backs off.
        public const float DefaultDistanceBand = 1f;
        // How strongly the bee-like wobble and the slow circling bend its path, against the pull to its distance.
        public const float DefaultBuzzStrength = 1f;
        public const float DefaultBuzzFrequency = 1.6f;
        public const float DefaultOrbitStrength = 0.6f;
        public const float DefaultOrbitFrequency = 0.25f;

        public const float DefaultFirstSummonDelay = 1.5f;
        public const float DefaultSummonInterval = 3f;
        public const float DefaultSummonTelegraph = 0.6f;
        public const int DefaultSummonCount = 2;
        public const int DefaultMaxSummons = 8;
        public const float DefaultSummonSideOffset = 0.7f;

        public static readonly Color SummonGlowColor = new(1f, 0.85f, 0.3f);

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = DefaultMoveSpeed;
        [SerializeField, Min(0f)] private float detectionRange = DefaultDetectionRange;
        [SerializeField, Min(0f)] private float preferredDistance = DefaultPreferredDistance;
        [SerializeField, Min(0.01f)] private float distanceBand = DefaultDistanceBand;
        [SerializeField, Min(0f)] private float buzzStrength = DefaultBuzzStrength;
        [SerializeField, Min(0f)] private float buzzFrequency = DefaultBuzzFrequency;
        [SerializeField, Min(0f)] private float orbitStrength = DefaultOrbitStrength;
        [SerializeField, Min(0f)] private float orbitFrequency = DefaultOrbitFrequency;

        [Header("Summon")]
        [SerializeField] private GameObject summonPrefab;
        [SerializeField, Min(0f)] private float firstSummonDelay = DefaultFirstSummonDelay;
        [SerializeField, Min(0.1f)] private float summonInterval = DefaultSummonInterval;
        [SerializeField, Min(0f)] private float summonTelegraph = DefaultSummonTelegraph;
        [SerializeField, Min(1)] private int summonCount = DefaultSummonCount;
        [SerializeField, Min(1)] private int maxSummons = DefaultMaxSummons;
        [SerializeField, Min(0f)] private float summonSideOffset = DefaultSummonSideOffset;

        private readonly List<Health> summons = new();
        private Rigidbody2D body;
        private Health health;
        private EnemyBehaviorContext behavior;
        private SpriteRenderer visual;
        private float noiseSeed;
        private float nextSummonAt = float.NaN;
        private float telegraphStartedAt = float.NaN;

        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float PreferredDistance => preferredDistance;
        public float DistanceBand => distanceBand;
        public GameObject SummonPrefab => summonPrefab;
        public float FirstSummonDelay => firstSummonDelay;
        public float SummonInterval => summonInterval;
        public float SummonTelegraph => summonTelegraph;
        public int SummonCount => summonCount;
        public int MaxSummons => maxSummons;
        public float SummonSideOffset => summonSideOffset;
        public bool IsSummoning => !float.IsNaN(telegraphStartedAt);
        public float NextSummonAt => nextSummonAt;
        public SpriteRenderer Visual => visual;
        public IReadOnlyList<Health> LastSummons { get; private set; } = Array.Empty<Health>();
        public event Action<IReadOnlyList<Health>> Summoned;

        public int AliveSummonCount
        {
            get
            {
                summons.RemoveAll(summon => summon == null || summon.IsDead);
                return summons.Count;
            }
        }

        public void ConfigureMovement(float configuredMoveSpeed, float configuredDetectionRange,
            float configuredPreferredDistance, float configuredDistanceBand)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            detectionRange = Mathf.Max(0f, configuredDetectionRange);
            preferredDistance = Mathf.Max(0f, configuredPreferredDistance);
            distanceBand = Mathf.Max(0.01f, configuredDistanceBand);
        }

        public void ConfigureSummon(GameObject configuredPrefab, float configuredFirstDelay, float configuredInterval,
            float configuredTelegraph, int configuredCount, int configuredMax, float configuredSideOffset)
        {
            summonPrefab = configuredPrefab;
            firstSummonDelay = Mathf.Max(0f, configuredFirstDelay);
            summonInterval = Mathf.Max(0.1f, configuredInterval);
            summonTelegraph = Mathf.Max(0f, configuredTelegraph);
            summonCount = Mathf.Max(1, configuredCount);
            maxSummons = Mathf.Max(1, configuredMax);
            summonSideOffset = Mathf.Max(0f, configuredSideOffset);
        }

        private void Awake()
        {
            CacheComponents();
            noiseSeed = UnityEngine.Random.Range(0f, 1000f);
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            if (health != null) health.Died -= OnDied;
            health = GetComponent<Health>();
            if (health != null) health.Died += OnDied;
            behavior = GetComponent<EnemyBehaviorContext>();
            FairyKingdomArtworkView artwork = GetComponent<FairyKingdomArtworkView>();
            visual = artwork != null && artwork.Visual != null ? artwork.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            TickMovement(Time.time);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void TickMovement(float currentTime)
        {
            if (behavior == null || body == null) CacheComponents();
            if (behavior == null || body == null) return;
            if (!behavior.TryAcquireOrAlert(detectionRange) || behavior.IsMovementSuppressed)
            {
                behavior.StopForSuppression();
                return;
            }

            Vector2 velocity = DesiredVelocity(body.position, behavior.Target.position, currentTime);
            // Shock and webs slow ordinary movement only (Passive-0 §4.8, Trap-0).
            body.linearVelocity = velocity * EnemyStatusEffects.MoveSpeedMultiplierOf(this);
        }

        // Pulls toward the preferred distance from the target, circles it slowly and wobbles like a bee. The three
        // are summed and capped at the move speed, so the body keeps darting even while it holds its distance.
        public Vector2 DesiredVelocity(Vector2 position, Vector2 targetPosition, float currentTime)
        {
            Vector2 away = position - targetPosition;
            float distance = away.magnitude;
            Vector2 outward = distance > 0.0001f ? away / distance : Vector2.up;
            float radial = DistancePull(distance, preferredDistance, distanceBand);
            Vector2 tangent = new(-outward.y, outward.x);
            // A slowly drifting signed value, so the circling slows, stops and turns around now and then.
            float orbit = (Mathf.PerlinNoise(noiseSeed + 17.3f, currentTime * orbitFrequency) * 2f - 1f) *
                          orbitStrength;
            Vector2 buzz = new(Mathf.PerlinNoise(noiseSeed, currentTime * buzzFrequency) * 2f - 1f,
                Mathf.PerlinNoise(currentTime * buzzFrequency, noiseSeed + 53.1f) * 2f - 1f);
            Vector2 steer = outward * radial + tangent * orbit + buzz * buzzStrength;
            return Vector2.ClampMagnitude(steer, 1f) * moveSpeed;
        }

        // +1 backs off at full strength, -1 closes in. Zero inside the band, then grows over one more band.
        public static float DistancePull(float distance, float preferred, float band)
        {
            float gap = preferred - distance;
            float excess = Mathf.Abs(gap) - band;
            if (excess <= 0f) return 0f;
            return Mathf.Sign(gap) * Mathf.Clamp01(excess / Mathf.Max(0.01f, band));
        }

        public void Tick(float currentTime)
        {
            if (health == null || behavior == null) CacheComponents();
            if (health == null || health.IsDead || behavior == null) return;

            if (IsSummoning)
            {
                // A started summon finishes even through a knockback.
                float progress = summonTelegraph > 0f
                    ? Mathf.Clamp01((currentTime - telegraphStartedAt) / summonTelegraph)
                    : 1f;
                if (visual != null) visual.color = GlowColor(progress);
                if (currentTime < telegraphStartedAt + summonTelegraph) return;
                ReleaseSummons();
                EndTelegraph();
                return;
            }

            // The clock starts once it is alerted and its spawn delay is over.
            if (behavior.IsActionSuppressed) return;
            if (float.IsNaN(nextSummonAt))
            {
                nextSummonAt = currentTime + firstSummonDelay;
                return;
            }

            if (currentTime < nextSummonAt) return;
            nextSummonAt = currentTime + summonInterval;
            // At the cap it skips this turn and tries again one interval later.
            if (AliveSummonCount >= maxSummons) return;
            telegraphStartedAt = currentTime;
            if (summonTelegraph <= 0f) Tick(currentTime);
        }

        // White at the start of the telegraph, fully golden at its end, and white again once the 쥬비 are out.
        public static Color GlowColor(float progress)
        {
            // Two quick pulses that end at full glow.
            float pulse = Mathf.Clamp01(progress) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 2f)));
            return Color.Lerp(Color.white, SummonGlowColor, progress >= 1f ? 1f : pulse);
        }

        private void ReleaseSummons()
        {
            int count = Mathf.Min(summonCount, maxSummons - AliveSummonCount);
            List<Health> released = new(Mathf.Max(0, count));
            if (summonPrefab != null && count > 0)
            {
                RoomController room = GetComponentInParent<RoomController>();
                Transform target = behavior != null ? behavior.Target : null;
                for (int index = 0; index < count; index++)
                {
                    Vector3 position = transform.position +
                                       EnemySplitOnDeath.SplitOffset(index, count, summonSideOffset);
                    Health summon = room != null
                        ? room.SpawnExtraEnemy(summonPrefab, position)
                        : SpawnWithoutRoom(position);
                    if (summon == null) continue;
                    Engage(summon, target);
                    summons.Add(summon);
                    released.Add(summon);
                }
            }

            LastSummons = released;
            Summoned?.Invoke(LastSummons);
        }

        private void EndTelegraph()
        {
            telegraphStartedAt = float.NaN;
            if (visual != null) visual.color = Color.white;
        }

        // A summon cut short by death leaves no glow behind. The released 쥬비 stay in the room.
        private void OnDied()
        {
            EndTelegraph();
        }

        // Outside a room (a test or a loose spawn) the 쥬비 keep this enemy's floor.
        private Health SpawnWithoutRoom(Vector3 position)
        {
            GameObject instance = Instantiate(summonPrefab, position, Quaternion.identity, transform.parent);
            Health summon = instance.GetComponent<Health>();
            if (summon == null)
            {
                Debug.LogError($"{name}: Summon Prefab must have a Health component.", instance);
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
                return null;
            }

            FloorDifficultyScaler.ApplyScaling(instance, EnemyFloorLevel.Resolve(gameObject));
            return summon;
        }

        // The 쥬비 chase this enemy's target at once, but wait out the shared spawn delay before acting.
        private static void Engage(Health summon, Transform target)
        {
            EnemyBehaviorContext summonBehavior = summon.GetComponent<EnemyBehaviorContext>();
            if (summonBehavior == null) return;
            summonBehavior.Initialize();
            if (!summonBehavior.IsAlerted) summonBehavior.BeginCombat(target);
            if (!summonBehavior.IsAlerted) summonBehavior.TryAcquireOrAlert(float.PositiveInfinity);
            summonBehavior.BeginSpawnGrace(Time.time);
        }
    }
}
