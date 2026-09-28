using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.Serialization;

namespace TrickalFanGame.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController), typeof(Rigidbody2D))]
    public sealed class BuseureogiBossPatternRuntime : MonoBehaviour, IBossPatternRuntime
    {
        public const int DefaultMaximumMinions = 4;
        public const int DefaultObstaclesPerThrow = 3;
        public const int MinionsPerSummon = 4;
        public const int ObstaclesPerThrow = 3;
        private const int RandomPlacementAttemptsPerObstacle = 32;

        [Header("Spawned content")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private GameObject[] obstaclePrefabs = Array.Empty<GameObject>();
        [SerializeField] private Vector2[] minionOffsets =
        {
            new(-1.8f, 0f), new(1.8f, 0f), new(0f, -1.8f), new(0f, 1.8f),
            new(-1.3f, -1.3f), new(-1.3f, 1.3f), new(1.3f, -1.3f), new(1.3f, 1.3f),
        };
        [SerializeField] private Vector2[] obstacleOffsets =
        {
            new(-3.7f, -2.15f), new(3.7f, -2.15f), new(-3.7f, 2.15f), new(3.7f, 2.15f),
        };

        [Header("Limits and safe placement")]
        [SerializeField, Min(1)] private int maximumMinions = DefaultMaximumMinions;
        [FormerlySerializedAs("maximumObstacles")]
        [SerializeField, Min(1)] private int obstaclesPerThrowLimit = DefaultObstaclesPerThrow;
        [FormerlySerializedAs("playerExclusionRadius")]
        [SerializeField, Min(0f)] private float obstacleImpactRadius = 1.1f;
        [SerializeField, Min(0f)] private float obstacleSpacing = 2.25f;
        [SerializeField, Min(0f)] private float openCrossHalfWidth = 1.45f;
        [SerializeField, Min(0f)] private float arenaPadding = 0.75f;
        [SerializeField, Min(0f)] private float minionSpacing = 0.65f;
        [SerializeField] private EnemyDamageTier obstacleImpactDamageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0f)] private float obstacleKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float obstacleKnockbackDuration = 0.2f;

        [Header("Approach volley")]
        [SerializeField, Min(0f)] private float approachSpeed = 1.4f;
        [SerializeField, Min(1f)] private float phaseTwoSpeedMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float shotInterval = 0.42f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 5f;
        [SerializeField] private Vector2 movementHalfExtents = new(6.2f, 3.2f);

        private readonly List<GameObject> minions = new();
        private readonly List<GameObject> obstacles = new();
        private BossController boss;
        private Rigidbody2D body;
        private Transform target;
        private Vector2 arenaOrigin;
        private Rect arenaBounds;
        private uint randomState;
        private float lastTickTime;
        private float nextShotTime;
        private readonly List<GameObject> pendingObstaclePrefabs = new();
        private readonly List<GameObject> pendingObstacleTelegraphs = new();
        private readonly List<Vector2> pendingObstaclePositions = new();

        public GameObject MinionPrefab => minionPrefab;
        public IReadOnlyList<Vector2> MinionOffsets => minionOffsets;
        public IReadOnlyList<GameObject> ObstaclePrefabs => obstaclePrefabs;
        public IReadOnlyList<Vector2> ObstacleOffsets => obstacleOffsets;
        public int MaximumMinions => Mathf.Max(1, maximumMinions);
        public int ObstaclesPerThrowLimit => Mathf.Max(1, obstaclesPerThrowLimit);
        public int LiveMinionCount => PruneAndCount(minions);
        public int LiveObstacleCount => PruneAndCount(obstacles);
        public IReadOnlyList<GameObject> LiveMinions
        {
            get
            {
                PruneAndCount(minions);
                return minions;
            }
        }
        public IReadOnlyList<GameObject> LiveObstacles
        {
            get
            {
                PruneAndCount(obstacles);
                return obstacles;
            }
        }
        public Vector2 ArenaOrigin => arenaOrigin;
        public Rect ArenaBounds => arenaBounds;
        public float ApproachSpeed => approachSpeed;
        public float PhaseTwoSpeedMultiplier => Mathf.Max(1f, phaseTwoSpeedMultiplier);
        public float CurrentApproachSpeed => approachSpeed *
                                             (boss != null && boss.CurrentPhase >= 2
                                                 ? PhaseTwoSpeedMultiplier
                                                 : 1f);
        public float CurrentShotInterval => shotInterval *
                                            (boss != null ? boss.CurrentTempoMultiplier : 1f);
        public float ObstacleImpactRadius => obstacleImpactRadius;
        public EnemyDamageTier ObstacleImpactDamageTier => obstacleImpactDamageTier;
        public float ObstacleKnockbackSpeed => obstacleKnockbackSpeed;
        public bool HasPendingObstacleTelegraph => pendingObstaclePositions.Count > 0 &&
                                                   pendingObstacleTelegraphs.Any(item => item != null);
        public int PendingObstacleCount => pendingObstaclePositions.Count;
        public Vector2 PendingObstaclePosition => pendingObstaclePositions.Count > 0
            ? pendingObstaclePositions[0]
            : Vector2.zero;
        public IReadOnlyList<Vector2> PendingObstaclePositions => pendingObstaclePositions;

        private void Awake()
        {
            boss = GetComponent<BossController>();
            body = GetComponent<Rigidbody2D>();
        }

        public void Configure(GameObject configuredMinion, GameObject[] configuredObstacles,
            Vector2[] configuredMinionOffsets, Vector2[] configuredObstacleOffsets)
        {
            minionPrefab = configuredMinion;
            obstaclePrefabs = configuredObstacles ?? Array.Empty<GameObject>();
            minionOffsets = configuredMinionOffsets ?? Array.Empty<Vector2>();
            obstacleOffsets = configuredObstacleOffsets ?? Array.Empty<Vector2>();
        }

        public void ConfigureMovement(float configuredApproachSpeed, float configuredPhaseTwoSpeedMultiplier = 1f)
        {
            approachSpeed = Mathf.Max(0f, configuredApproachSpeed);
            phaseTwoSpeedMultiplier = Mathf.Max(1f, configuredPhaseTwoSpeedMultiplier);
        }

        public void ConfigureLimits(int configuredMaximumMinions, int configuredObstaclesPerThrow)
        {
            maximumMinions = Mathf.Max(1, configuredMaximumMinions);
            obstaclesPerThrowLimit = Mathf.Max(1, configuredObstaclesPerThrow);
        }

        public void ConfigureObstacleImpact(float radius, EnemyDamageTier damageTier, float knockbackSpeed,
            float knockbackDuration)
        {
            obstacleImpactRadius = Mathf.Max(0f, radius);
            obstacleImpactDamageTier = damageTier;
            obstacleKnockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            obstacleKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        }

        public void BeginCombat(Transform configuredTarget, int seed, float now)
        {
            if (boss == null) boss = GetComponent<BossController>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            target = configuredTarget;
            ResolveArenaBounds();
            randomState = SeedToState(seed);
            lastTickTime = now;
            nextShotTime = now;
            minions.Clear();
            obstacles.Clear();
            ClearPendingObstacleTelegraph();
        }

        public bool CanSelect(BossPatternExecution execution) => true;

        public void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt)
        {
            if (state == BossActionState.Telegraph && execution != BossPatternExecution.BuseureogiApproachVolley)
            {
                StopMovement();
                if (execution == BossPatternExecution.BuseureogiThrowObstacle)
                    PrepareObstacleTelegraph(ObstaclesPerThrow);
            }
            else if (state != BossActionState.Active)
            {
                StopMovement();
            }
        }

        public bool TryExecute(BossPatternExecution execution)
        {
            switch (execution)
            {
                case BossPatternExecution.BuseureogiApproachVolley:
                    FireAtTarget();
                    nextShotTime = lastTickTime + CurrentShotInterval;
                    return true;
                case BossPatternExecution.BuseureogiSummonMinions:
                    SpawnMinions(MinionsPerSummon);
                    return true;
                case BossPatternExecution.BuseureogiThrowObstacle:
                    CommitPendingObstacle();
                    return true;
                default:
                    return false;
            }
        }

        public void TickPattern(BossActionState state, BossPatternExecution execution, float now)
        {
            float deltaTime = Mathf.Clamp(now - lastTickTime, 0f, 0.1f);
            lastTickTime = now;
            bool shouldChase = execution == BossPatternExecution.BuseureogiApproachVolley &&
                               (state == BossActionState.Telegraph || state == BossActionState.Active);
            if (!shouldChase || target == null)
            {
                StopMovement();
                return;
            }

            Vector2 current = transform.position;
            Vector2 direction = ((Vector2)target.position - current).normalized;
            Rect movementArea = Shrink(arenaBounds, arenaPadding);
            if ((current.x <= movementArea.xMin && direction.x < 0f) ||
                (current.x >= movementArea.xMax && direction.x > 0f)) direction.x = 0f;
            if ((current.y <= movementArea.yMin && direction.y < 0f) ||
                (current.y >= movementArea.yMax && direction.y > 0f)) direction.y = 0f;
            float movementSpeed = CurrentApproachSpeed;
            if (body != null) body.linearVelocity = direction.normalized * movementSpeed;
            else transform.position = current + direction.normalized * (movementSpeed * deltaTime);

            if (now >= nextShotTime)
            {
                FireAtTarget();
                nextShotTime = now + CurrentShotInterval;
            }
        }

        public int SpawnMinions(int requestedCount)
        {
            PruneAndCount(minions);
            int available = MaximumMinions - minions.Count;
            int count = Mathf.Min(Mathf.Max(0, requestedCount), available);
            if (minionPrefab == null || minionOffsets.Length == 0 || count == 0) return 0;

            int spawned = 0;
            int start = NextRandom(minionOffsets.Length);
            for (int offset = 0; offset < minionOffsets.Length && spawned < count; offset++)
            {
                Vector2 position = (Vector2)transform.position +
                                   minionOffsets[(start + offset) % minionOffsets.Length];
                if (!ContainsWithPadding(arenaBounds, position, arenaPadding)) continue;
                if (minions.Any(item => item != null &&
                    ((Vector2)item.transform.position - position).sqrMagnitude <
                    minionSpacing * minionSpacing)) continue;
                GameObject instance = Instantiate(minionPrefab, position, Quaternion.identity);
                instance.name = "Buseureogi Crumb Minion";
                minions.Add(instance);
                boss?.RegisterOwnedObject(instance);
                EnemyBehaviorContext context = instance.GetComponent<EnemyBehaviorContext>();
                context?.BeginCombat(target);
                spawned++;
            }
            return spawned;
        }

        public bool TrySpawnObstacle()
        {
            if (!PrepareObstacleTelegraph(ObstaclesPerThrow)) return false;
            return CommitPendingObstacle();
        }

        public bool PrepareObstacleTelegraph(int requestedCount = ObstaclesPerThrow)
        {
            ClearPendingObstacleTelegraph();
            PruneAndCount(obstacles);
            int count = Mathf.Min(Mathf.Max(0, requestedCount), ObstaclesPerThrowLimit);
            if (count == 0 || obstaclePrefabs.Length == 0)
                return false;

            Rect placementArea = Shrink(arenaBounds, arenaPadding);
            int maximumAttempts = count * RandomPlacementAttemptsPerObstacle;
            for (int attempt = 0; attempt < maximumAttempts && pendingObstaclePositions.Count < count; attempt++)
            {
                Vector2 position = new(
                    Mathf.Lerp(placementArea.xMin, placementArea.xMax, NextRandom01()),
                    Mathf.Lerp(placementArea.yMin, placementArea.yMax, NextRandom01()));
                Vector2 local = position - arenaOrigin;
                if (!IsCandidateSafe(local, position)) continue;
                AddPendingObstacle(position);
            }

            // Retain authored safe positions only as a rare packing fallback for unusually crowded rooms.
            int fallbackStart = obstacleOffsets.Length > 0 ? NextRandom(obstacleOffsets.Length) : 0;
            for (int offset = 0;
                 offset < obstacleOffsets.Length && pendingObstaclePositions.Count < count;
                 offset++)
            {
                Vector2 local = obstacleOffsets[(fallbackStart + offset) % obstacleOffsets.Length];
                Vector2 position = arenaOrigin + local;
                if (!IsCandidateSafe(local, position)) continue;
                AddPendingObstacle(position);
            }

            // A throw always owes three new obstacles. If the arena is already crowded, preserve the room bounds,
            // open door cross, and spacing inside this wave while allowing a new obstacle to overlap an older one.
            for (int attempt = 0;
                 attempt < maximumAttempts && pendingObstaclePositions.Count < count;
                 attempt++)
            {
                Vector2 position = new(
                    Mathf.Lerp(placementArea.xMin, placementArea.xMax, NextRandom01()),
                    Mathf.Lerp(placementArea.yMin, placementArea.yMax, NextRandom01()));
                Vector2 local = position - arenaOrigin;
                if (!IsCandidateSafe(local, position, false)) continue;
                AddPendingObstacle(position);
            }
            return pendingObstaclePositions.Count > 0;
        }

        public bool CommitPendingObstacle()
        {
            if (pendingObstaclePositions.Count == 0) return false;
            int spawned = 0;
            for (int index = 0; index < pendingObstaclePositions.Count; index++)
            {
                GameObject prefab = pendingObstaclePrefabs[index];
                if (prefab == null) continue;
                Vector2 position = pendingObstaclePositions[index];
                ResolveObstacleImpact(position);
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                instance.name = prefab.name;
                obstacles.Add(instance);
                boss?.RegisterOwnedObject(instance);
                spawned++;
            }
            ClearPendingObstacleTelegraph();
            return spawned > 0;
        }

        public bool IsCandidateSafe(Vector2 localOffset, Vector2 worldPosition, bool avoidExistingObstacles = true)
        {
            // Keeping every obstacle outside this central cross preserves horizontal and vertical routes to all doors.
            if (Mathf.Abs(localOffset.x) <= openCrossHalfWidth || Mathf.Abs(localOffset.y) <= openCrossHalfWidth)
                return false;
            if (!ContainsWithPadding(arenaBounds, worldPosition, arenaPadding)) return false;
            float spacingSquared = obstacleSpacing * obstacleSpacing;
            return (!avoidExistingObstacles || obstacles.All(item => item == null ||
                       ((Vector2)item.transform.position - worldPosition).sqrMagnitude >= spacingSquared)) &&
                   pendingObstaclePositions.All(position =>
                       (position - worldPosition).sqrMagnitude >= spacingSquared);
        }

        public bool IsInsideArena(Vector2 worldPosition) =>
            ContainsWithPadding(arenaBounds, worldPosition, arenaPadding);

        private void FireAtTarget()
        {
            if (target == null || boss == null) return;
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude <= 0f) direction = Vector2.down;
            BossProjectile.Create(transform.position, direction * projectileSpeed, gameObject,
                boss.ProjectileDamageTier, GetComponentInChildren<SpriteRenderer>()?.sprite);
        }

        private void StopMovement()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void ResolveArenaBounds()
        {
            RoomNode room = GetComponentInParent<RoomNode>();
            if (room != null && room.Profile != null)
            {
                Rect local = room.Profile.EncounterBounds;
                Vector2 minimum = room.transform.TransformPoint(local.min);
                Vector2 maximum = room.transform.TransformPoint(local.max);
                arenaBounds = Rect.MinMaxRect(
                    Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                    Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
                arenaOrigin = arenaBounds.center;
                return;
            }

            arenaOrigin = transform.position;
            arenaBounds = new Rect(arenaOrigin - movementHalfExtents, movementHalfExtents * 2f);
        }

        private static Rect Shrink(Rect bounds, float padding)
        {
            float horizontal = Mathf.Min(Mathf.Max(0f, padding), bounds.width * 0.49f);
            float vertical = Mathf.Min(Mathf.Max(0f, padding), bounds.height * 0.49f);
            return Rect.MinMaxRect(bounds.xMin + horizontal, bounds.yMin + vertical,
                bounds.xMax - horizontal, bounds.yMax - vertical);
        }

        private static bool ContainsWithPadding(Rect bounds, Vector2 point, float padding) =>
            Shrink(bounds, padding).Contains(point);

        private void ClearPendingObstacleTelegraph()
        {
            foreach (GameObject telegraph in pendingObstacleTelegraphs)
            {
                if (telegraph == null) continue;
                if (Application.isPlaying) Destroy(telegraph);
                else DestroyImmediate(telegraph);
            }
            pendingObstacleTelegraphs.Clear();
            pendingObstaclePrefabs.Clear();
            pendingObstaclePositions.Clear();
        }

        private void AddPendingObstacle(Vector2 position)
        {
            GameObject prefab = obstaclePrefabs[NextRandom(obstaclePrefabs.Length)];
            if (prefab == null) return;
            pendingObstaclePrefabs.Add(prefab);
            pendingObstaclePositions.Add(position);
            GameObject telegraph = new("Buseureogi Obstacle Landing Telegraph");
            telegraph.transform.position = position;
            telegraph.transform.localScale = Vector3.one * 1.25f;
            SpriteRenderer telegraphRenderer = telegraph.AddComponent<SpriteRenderer>();
            telegraphRenderer.sprite = prefab.GetComponent<SpriteRenderer>()?.sprite;
            telegraphRenderer.color = new Color(1f, 0.35f, 0.15f, 0.38f);
            telegraphRenderer.sortingOrder = 1;
            pendingObstacleTelegraphs.Add(telegraph);
            boss?.RegisterOwnedObject(telegraph);
        }

        private void ResolveObstacleImpact(Vector2 impactPosition)
        {
            if (target == null || ((Vector2)target.position - impactPosition).sqrMagnitude >
                obstacleImpactRadius * obstacleImpactRadius) return;

            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead) return;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            targetHealth.TakeDamage(HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyContact,
                obstacleImpactDamageTier));

            KnockbackReceiver receiver = targetHealth.GetComponent<KnockbackReceiver>();
            if (receiver == null || targetHealth.IsDead) return;
            Vector2 direction = (Vector2)target.position - impactPosition;
            if (direction.sqrMagnitude <= 0.001f)
                direction = impactPosition - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector2.up;
            receiver.Apply(direction, obstacleKnockbackSpeed, obstacleKnockbackDuration, 0f);
        }

        private int NextRandom(int maximumExclusive)
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return maximumExclusive <= 1 ? 0 : (int)(randomState % (uint)maximumExclusive);
        }

        private float NextRandom01() => NextRandom(1 << 24) / (float)(1 << 24);

        private static int PruneAndCount(List<GameObject> objects)
        {
            objects.RemoveAll(item => item == null);
            return objects.Count;
        }

        private static uint SeedToState(int seed)
        {
            uint state = unchecked((uint)seed) ^ 0xB0571E0Fu;
            return state == 0u ? 0xC2B2AE35u : state;
        }
    }
}
