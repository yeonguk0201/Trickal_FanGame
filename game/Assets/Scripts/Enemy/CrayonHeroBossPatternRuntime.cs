using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(BossController), typeof(Rigidbody2D), typeof(Health))]
    public sealed class CrayonHeroBossPatternRuntime : MonoBehaviour, IBossPatternRuntime,
        IBossPatternSelectionPolicy
    {
        [Header("Map slash")]
        [SerializeField, Min(1f)] private float slashLength = 18f;
        [SerializeField, Min(0.1f)] private float slashWidth = 1.15f;
        [SerializeField, Min(0.1f)] private float goldenSlashWidth = 0.57f;
        [SerializeField, Min(0.05f)] private float slashLockedDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float slashDamage = 3f;
        [SerializeField, Min(0f)] private float goldenSlashSpreadDegrees = 15f;

        [Header("Summons")]
        [SerializeField] private GameObject[] minionPrefabs = new GameObject[0];
        [SerializeField, Min(1)] private int summonsPerPattern = 4;
        [SerializeField, Min(0.1f)] private float summonRadius = 2.5f;
        [SerializeField, Range(0.01f, 1f)] private float summonReactivationHealthFraction = 0.15f;

        [Header("Approach swing")]
        [SerializeField, Min(0f)] private float approachSpeed = 2.5f;
        [SerializeField, Min(1f)] private float phaseTwoSpeedMultiplier = 1.35f;
        [SerializeField, Min(0.1f)] private float swingRange = 2.28f;
        [SerializeField, Min(0.1f)] private float swingWidth = 3.2f;
        [SerializeField, Min(0f)] private float swingStartGap = 0.15f;
        [SerializeField, Min(0.05f)] private float swingWindup = 0.3f;
        [SerializeField, Min(0.05f)] private float swingInterval = 0.7f;
        [SerializeField, Min(0.01f)] private float swingDirectionLockDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float swingDamage = 2f;
        [SerializeField, Min(1f)] private float swingRecognitionMultiplier = 1.625f;

        [Header("Dash chain")]
        [SerializeField, Min(0.1f)] private float dashSpeed = 15f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.17f;
        [SerializeField, Min(0.01f)] private float dashRetargetDuration = 0.1f;
        [SerializeField, Min(0.1f)] private float dashWidth = 1.8f;
        [SerializeField, Min(0.01f)] private float dashDamage = 2f;

        [Header("Pattern selection")]
        [SerializeField, Min(1)] private int pressureActionsBeforeSlash = 3;
        [SerializeField, Min(0f)] private float slashCooldownPhaseOne = 9f;
        [SerializeField, Min(0f)] private float slashCooldownGolden = 7.5f;
        [SerializeField, Min(0)] private int swingSelectionWeight = 45;
        [SerializeField, Min(0)] private int dashSelectionWeight = 30;
        [SerializeField, Min(0)] private int summonSelectionWeight = 10;
        [SerializeField, Min(0)] private int slashSelectionWeight = 15;

        [Header("Arena and presentation")]
        [SerializeField, Min(0f)] private float arenaPadding = 0.7f;
        [SerializeField] private Vector2 fallbackHalfExtents = new Vector2(7.5f, 5.5f);
        [SerializeField] private Color phaseTwoColor = new Color(1f, 0.78f, 0.18f, 1f);

        private readonly List<GameObject> livingMinions = new List<GameObject>();
        private readonly List<LineRenderer> supplementalTelegraphLines = new List<LineRenderer>();
        private BossController boss;
        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer visual;
        private Transform target;
        private Rect arenaBounds;
        private uint randomState;
        private float lastTickTime;
        private Vector2 lockedDirection = Vector2.down;
        private bool slashPending;
        private bool slashResolved;
        private float slashResolvesAt;
        private int completedPressureActions;
        private float nextSlashEligibleAt;
        private bool summonAvailable = true;
        private float healthAtLastSummon;
        private bool swingPreparing;
        private bool swingDirectionLocked;
        private float swingResolvesAt;
        private float nextSwingStartsAt;
        private int swingCountTarget;
        private int completedSwings;
        private bool dashInMotion;
        private bool dashChainHitApplied;
        private float dashStateEndsAt;
        private int dashCountTarget;
        private int completedDashes;
        private GameObject telegraphObject;
        private LineRenderer telegraphLine;
        private GameObject recognitionDebugObject;
        private LineRenderer recognitionDebugLine;

        public float SlashLength => slashLength;
        public float SlashWidth => slashWidth;
        public float SlashLockedDelay => slashLockedDelay;
        public float SlashDamage => slashDamage;
        public float GoldenSlashSpreadDegrees => goldenSlashSpreadDegrees;
        public int ActiveSlashDirectionCount => boss != null && boss.CurrentPhase >= 2 ? 3 : 1;
        public float ActiveSlashWidth => boss != null && boss.CurrentPhase >= 2 ? goldenSlashWidth : slashWidth;
        public int SummonsPerPattern => summonsPerPattern;
        public float SummonReactivationHealthFraction => summonReactivationHealthFraction;
        public bool SummonAvailable => summonAvailable;
        public int CompletedPressureActions => completedPressureActions;
        public float NextSlashEligibleAt => nextSlashEligibleAt;
        public float CurrentApproachSpeed => approachSpeed *
            (boss != null && boss.CurrentPhase >= 2 ? phaseTwoSpeedMultiplier : 1f);
        public float SwingRange => swingRange;
        public float SwingWidth => swingWidth;
        public float SwingStartOffset => ResolveBodyRadius() + swingStartGap;
        public float SwingAttackReach => SwingStartOffset + swingRange;
        public float SwingTriggerRange => SwingAttackReach * swingRecognitionMultiplier;
        public float SwingWindup => swingWindup;
        public float SwingInterval => swingInterval;
        public float SwingDirectionLockDuration => swingDirectionLockDuration;
        public int SwingCountTarget => swingCountTarget;
        public int CompletedSwings => completedSwings;
        public bool IsSwingPreparing => swingPreparing;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashRetargetDuration => dashRetargetDuration;
        public int DashCountTarget => dashCountTarget;
        public int CompletedDashes => completedDashes;
        public bool IsDashInMotion => dashInMotion;
        public bool DashChainHitApplied => dashChainHitApplied;
        public bool AttackHitboxVisible => telegraphLine != null && telegraphLine.enabled;
        public bool RecognitionRadiusVisible => recognitionDebugLine != null && recognitionDebugLine.enabled;
        public int LivingMinionCount
        {
            get
            {
                livingMinions.RemoveAll(item => item == null);
                return livingMinions.Count;
            }
        }
        public IReadOnlyList<GameObject> MinionPrefabs => minionPrefabs;
        public Vector2 LockedDirection => lockedDirection;

        private void Awake()
        {
            ResolveComponents();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            health.Changed -= OnHealthChanged;
            health.Changed += OnHealthChanged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Changed -= OnHealthChanged;
        }

        private void OnDisable()
        {
            StopMovement();
            ClearTelegraph();
            ClearRecognitionDebug();
            if (health != null) health.SetInvulnerable(false);
        }

        public void ConfigureSlash(float length, float width, float lockedDelay, float damage)
        {
            slashLength = Mathf.Max(1f, length);
            slashWidth = Mathf.Max(0.1f, width);
            slashLockedDelay = Mathf.Max(0.05f, lockedDelay);
            slashDamage = Mathf.Max(0.01f, damage);
        }

        public void ConfigureSummons(GameObject[] prefabs, int countPerPattern, float radius,
            float reactivationHealthFraction)
        {
            minionPrefabs = prefabs ?? new GameObject[0];
            summonsPerPattern = Mathf.Max(1, countPerPattern);
            summonRadius = Mathf.Max(0.1f, radius);
            summonReactivationHealthFraction = Mathf.Clamp(reactivationHealthFraction, 0.01f, 1f);
        }

        public void ConfigureSwing(float speed, float phaseTwoMultiplier, float range, float width,
            float startGap, float windup, float interval, float directionLockDuration, float damage)
        {
            approachSpeed = Mathf.Max(0f, speed);
            phaseTwoSpeedMultiplier = Mathf.Max(1f, phaseTwoMultiplier);
            swingRange = Mathf.Max(0.1f, range);
            swingWidth = Mathf.Max(0.1f, width);
            swingStartGap = Mathf.Max(0f, startGap);
            swingWindup = Mathf.Max(0.05f, windup);
            swingInterval = Mathf.Max(swingWindup, interval);
            swingDirectionLockDuration = Mathf.Clamp(directionLockDuration, 0.01f, swingWindup);
            swingDamage = Mathf.Max(0.01f, damage);
        }

        public void ConfigureDash(float speed, float duration, float retargetDuration, float width, float damage)
        {
            dashSpeed = Mathf.Max(0.1f, speed);
            dashDuration = Mathf.Max(0.01f, duration);
            dashRetargetDuration = Mathf.Max(0.01f, retargetDuration);
            dashWidth = Mathf.Max(0.1f, width);
            dashDamage = Mathf.Max(0.01f, damage);
        }

        public void ConfigureSelection(int pressureBeforeSlash, float phaseOneCooldown, float goldenCooldown,
            int swingWeight, int dashWeight, int summonWeight, int slashWeight)
        {
            pressureActionsBeforeSlash = Mathf.Max(1, pressureBeforeSlash);
            slashCooldownPhaseOne = Mathf.Max(0f, phaseOneCooldown);
            slashCooldownGolden = Mathf.Max(0f, goldenCooldown);
            swingSelectionWeight = Mathf.Max(0, swingWeight);
            dashSelectionWeight = Mathf.Max(0, dashWeight);
            summonSelectionWeight = Mathf.Max(0, summonWeight);
            slashSelectionWeight = Mathf.Max(0, slashWeight);
        }

        public void BeginCombat(Transform configuredTarget, int seed, float now)
        {
            ResolveComponents();
            target = configuredTarget;
            randomState = SeedToState(seed);
            lastTickTime = now;
            slashPending = false;
            slashResolved = false;
            completedPressureActions = 0;
            nextSlashEligibleAt = now +
                (boss != null && boss.CurrentPhase >= 2 ? slashCooldownGolden : slashCooldownPhaseOne);
            summonAvailable = true;
            healthAtLastSummon = health != null ? health.CurrentHealth : 0f;
            ResetSwingState();
            ResetDashState();
            ResolveArenaBounds();
            ApplyPhasePresentation();
        }

        public bool CanSelect(BossPatternExecution execution)
        {
            return execution switch
            {
                BossPatternExecution.CrayonHeroMapSlash =>
                    completedPressureActions >= pressureActionsBeforeSlash && lastTickTime >= nextSlashEligibleAt,
                BossPatternExecution.CrayonHeroSummonMinions => summonAvailable,
                _ => true,
            };
        }

        public int GetSelectionWeight(BossPatternExecution execution)
        {
            return execution switch
            {
                BossPatternExecution.CrayonHeroApproachSwing => swingSelectionWeight,
                BossPatternExecution.CrayonHeroDashChain => dashSelectionWeight,
                BossPatternExecution.CrayonHeroSummonMinions => summonSelectionWeight,
                BossPatternExecution.CrayonHeroMapSlash => slashSelectionWeight,
                _ => 1,
            };
        }

        public void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt)
        {
            ResolveComponents();
            if (state == BossActionState.PhaseTransition)
            {
                StopMovement();
                ClearTelegraph();
                ResetSwingState();
                ResetDashState();
                slashPending = false;
                slashResolved = false;
                health.SetInvulnerable(true);
                return;
            }

            health.SetInvulnerable(false);
            ApplyPhasePresentation();
            if (state == BossActionState.Telegraph)
            {
                ClearTelegraph();
                if (execution == BossPatternExecution.CrayonHeroMapSlash)
                {
                    StopMovement();
                    UpdateAimDirection();
                    ShowMapSlashTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroSummonMinions)
                {
                    StopMovement();
                    ShowSummonTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroDashChain)
                {
                    StopMovement();
                    UpdateAimDirection();
                    ShowDashTelegraph();
                }
                return;
            }

            if (state == BossActionState.Recovery)
            {
                ClearTelegraph();
                if (IsPressureAttack(execution)) completedPressureActions++;
                if (execution == BossPatternExecution.CrayonHeroMapSlash)
                {
                    completedPressureActions = 0;
                    nextSlashEligibleAt = lastTickTime +
                        (boss != null && boss.CurrentPhase >= 2 ? slashCooldownGolden : slashCooldownPhaseOne);
                    StopMovement();
                }
                else if (execution == BossPatternExecution.CrayonHeroSummonMinions ||
                         execution == BossPatternExecution.CrayonHeroDashChain)
                {
                    StopMovement();
                }
                ResetSwingState();
                ResetDashState();
                slashPending = false;
                return;
            }

            if (state == BossActionState.Defeated)
            {
                StopMovement();
                ClearTelegraph();
            }
        }

        public bool TryExecute(BossPatternExecution execution)
        {
            switch (execution)
            {
                case BossPatternExecution.CrayonHeroMapSlash:
                    UpdateAimDirection();
                    ShowMapSlashTelegraph();
                    slashPending = true;
                    slashResolved = false;
                    slashResolvesAt = lastTickTime + slashLockedDelay;
                    return true;
                case BossPatternExecution.CrayonHeroSummonMinions:
                    if (!summonAvailable) return false;
                    ClearTelegraph();
                    SummonMinions();
                    summonAvailable = false;
                    healthAtLastSummon = health != null ? health.CurrentHealth : 0f;
                    return true;
                case BossPatternExecution.CrayonHeroApproachSwing:
                    ClearTelegraph();
                    ResetSwingState();
                    swingCountTarget = 2 + NextRandom(2);
                    nextSwingStartsAt = lastTickTime;
                    return true;
                case BossPatternExecution.CrayonHeroDashChain:
                    dashCountTarget = ResolveDashCountForRoll(NextRandom(100), boss != null && boss.CurrentPhase >= 2);
                    completedDashes = 0;
                    dashChainHitApplied = false;
                    BeginDash(lastTickTime);
                    boss?.SynchronizeActiveEndTime(lastTickTime + dashCountTarget * dashDuration +
                                                   (dashCountTarget - 1) * dashRetargetDuration + 0.02f);
                    return true;
                default:
                    return false;
            }
        }

        public void TickPattern(BossActionState state, BossPatternExecution execution, float now)
        {
            float deltaTime = Mathf.Clamp(now - lastTickTime, 0f, 0.1f);
            lastTickTime = now;
            if (state == BossActionState.Telegraph)
            {
                if (execution == BossPatternExecution.CrayonHeroMapSlash)
                {
                    UpdateAimDirection();
                    ShowMapSlashTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroDashChain)
                {
                    UpdateAimDirection();
                    ShowDashTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroApproachSwing)
                {
                    MoveTowardTarget(deltaTime, 1f);
                }
                return;
            }

            if (state == BossActionState.Active)
            {
                switch (execution)
                {
                    case BossPatternExecution.CrayonHeroMapSlash:
                        TickMapSlash(now);
                        break;
                    case BossPatternExecution.CrayonHeroApproachSwing:
                        TickApproachSwing(now, deltaTime);
                        break;
                    case BossPatternExecution.CrayonHeroDashChain:
                        TickDashChain(now, deltaTime);
                        break;
                }
                return;
            }

            if (state == BossActionState.Recovery)
            {
                if (execution == BossPatternExecution.CrayonHeroApproachSwing)
                    MoveTowardTarget(deltaTime, 1f);
                return;
            }

            if (state == BossActionState.Idle || state == BossActionState.Cooldown)
                MoveTowardTarget(deltaTime, 1f);
        }

        private void LateUpdate()
        {
            if (swingPreparing && telegraphLine != null) UpdateSwingTelegraphPositions();
            if (recognitionDebugLine != null && recognitionDebugLine.enabled) UpdateRecognitionDebugCircle();
        }

        public void SetRecognitionRadiusDebugVisible(bool visible)
        {
            if (!visible)
            {
                ClearRecognitionDebug();
                return;
            }
            EnsureRecognitionDebug();
            UpdateRecognitionDebugCircle();
        }

        public static int ResolveDashCountForRoll(int roll, bool golden)
        {
            int normalized = Mathf.Clamp(roll, 0, 99);
            if (golden)
            {
                if (normalized < 25) return 1;
                if (normalized < 55) return 2;
                return 3;
            }
            if (normalized < 25) return 1;
            if (normalized < 75) return 2;
            return 3;
        }

        private void TickMapSlash(float now)
        {
            if (!slashPending || slashResolved || now < slashResolvesAt) return;
            int hitCount = 0;
            foreach (Vector2 direction in GetSlashDirections())
                if (IsTargetInsideDirectionalBox(0f, slashLength, ActiveSlashWidth, direction)) hitCount++;
            if (hitCount > 0) ApplyTargetDamage(slashDamage * hitCount, DamageSourceType.EnemyMelee);
            slashResolved = true;
            slashPending = false;
            SetAllTelegraphColors(new Color(1f, 0.08f, 0.02f, 0.95f),
                new Color(1f, 0.45f, 0.02f, 0.95f));
        }

        private void TickApproachSwing(float now, float deltaTime)
        {
            if (target == null || completedSwings >= swingCountTarget) return;
            MoveTowardTarget(deltaTime, 1f);
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            if (!swingPreparing)
            {
                if (now < nextSwingStartsAt || offset.magnitude > SwingTriggerRange) return;
                UpdateAimDirection();
                swingPreparing = true;
                swingDirectionLocked = false;
                swingResolvesAt = now + swingWindup;
                ShowSwingTelegraph();
                return;
            }

            if (!swingDirectionLocked)
            {
                if (now < swingResolvesAt - swingDirectionLockDuration) UpdateAimDirection();
                else swingDirectionLocked = true;
            }
            UpdateSwingTelegraphPositions();
            if (now < swingResolvesAt) return;
            ResolveDirectionalHit(SwingStartOffset, swingRange, swingWidth, swingDamage,
                DamageSourceType.EnemyMelee, lockedDirection);
            completedSwings++;
            swingPreparing = false;
            swingDirectionLocked = false;
            nextSwingStartsAt = now + Mathf.Max(0f, swingInterval - swingWindup);
            ClearTelegraph();
        }

        private void TickDashChain(float now, float deltaTime)
        {
            if (target == null || completedDashes >= dashCountTarget) return;
            if (dashInMotion)
            {
                float travel = dashSpeed * deltaTime;
                if (!dashChainHitApplied && ResolveDirectionalHit(0f, ResolveBodyRadius() + travel, dashWidth,
                        dashDamage, DamageSourceType.EnemyContact, lockedDirection))
                    dashChainHitApplied = true;
                body.MovePosition(ClampToArena((Vector2)transform.position + lockedDirection * travel));
                if (now < dashStateEndsAt) return;
                completedDashes++;
                dashInMotion = false;
                if (completedDashes >= dashCountTarget)
                {
                    ClearTelegraph();
                    return;
                }
                dashStateEndsAt = now + dashRetargetDuration;
                UpdateAimDirection();
                ShowDashTelegraph();
                return;
            }

            StopMovement();
            UpdateAimDirection();
            ShowDashTelegraph();
            if (now >= dashStateEndsAt) BeginDash(now);
        }

        private void BeginDash(float now)
        {
            UpdateAimDirection();
            dashInMotion = true;
            dashStateEndsAt = now + dashDuration;
            ShowDashTelegraph();
            SetAllTelegraphColors(new Color(1f, 0.12f, 0.05f, 0.9f),
                new Color(1f, 0.45f, 0.05f, 0.95f));
        }

        private void MoveTowardTarget(float deltaTime, float speedMultiplier)
        {
            if (target == null || body == null || deltaTime <= 0f) return;
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            if (offset.sqrMagnitude <= 0.001f) return;
            Vector2 destination = ClampToArena((Vector2)transform.position +
                                               offset.normalized * CurrentApproachSpeed * speedMultiplier * deltaTime);
            body.MovePosition(destination);
        }

        private bool ResolveDirectionalHit(float startOffset, float length, float width, float damage,
            DamageSourceType sourceType, Vector2 direction)
        {
            if (!IsTargetInsideDirectionalBox(startOffset, length, width, direction)) return false;
            ApplyTargetDamage(damage, sourceType);
            return true;
        }

        private bool IsTargetInsideDirectionalBox(float startOffset, float length, float width, Vector2 direction)
        {
            if (target == null) return false;
            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead || target.GetComponentInParent<PlayerMovement>() == null)
                return false;
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            float forward = Vector2.Dot(offset, normalizedDirection);
            float sideways = Mathf.Abs(Vector2.Dot(offset,
                new Vector2(-normalizedDirection.y, normalizedDirection.x)));
            return forward >= startOffset && forward <= startOffset + length && sideways <= width * 0.5f;
        }

        private void ApplyTargetDamage(float damage, DamageSourceType sourceType)
        {
            Health targetHealth = target != null ? target.GetComponentInParent<Health>() : null;
            if (targetHealth == null || targetHealth.IsDead) return;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("BOSS");
            targetHealth.TakeDamage(new DamageContext(gameObject, sourceType, damage));
        }

        private void SummonMinions()
        {
            livingMinions.RemoveAll(item => item == null);
            int count = Mathf.Min(summonsPerPattern, minionPrefabs.Length);
            for (int index = 0; index < count; index++)
            {
                GameObject prefab = minionPrefabs[index];
                if (prefab == null) continue;
                float angle = (360f * index / Mathf.Max(1, count) + NextRandom(30)) * Mathf.Deg2Rad;
                Vector2 position = ClampToArena((Vector2)transform.position +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * summonRadius);
                GameObject minion = Instantiate(prefab, position, Quaternion.identity, transform.parent);
                minion.name = prefab.name + " (Crayon Summon)";
                minion.GetComponent<TestEnemy>()?.ConfigureReward(false);
                minion.GetComponent<EnemyBehaviorContext>()?.BeginCombat(target);
                livingMinions.Add(minion);
                boss?.RegisterOwnedObject(minion);
            }
        }

        private void UpdateAimDirection()
        {
            if (target == null) return;
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            if (offset.sqrMagnitude > 0.001f) lockedDirection = offset.normalized;
        }

        private IReadOnlyList<Vector2> GetSlashDirections()
        {
            if (boss == null || boss.CurrentPhase < 2) return new[] { lockedDirection };
            return new[]
            {
                RotateDirection(lockedDirection, -goldenSlashSpreadDegrees),
                lockedDirection,
                RotateDirection(lockedDirection, goldenSlashSpreadDegrees),
            };
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }

        private void ShowMapSlashTelegraph()
        {
            IReadOnlyList<Vector2> directions = GetSlashDirections();
            for (int index = 0; index < directions.Count; index++)
            {
                LineRenderer line = index == 0
                    ? EnsurePrimaryLine("Crayon Hero Map Slash Telegraph")
                    : EnsureSupplementalLine(index - 1);
                ConfigureLine(line, 6, ActiveSlashWidth, new Color(1f, 0.28f, 0.02f, 0.62f),
                    new Color(1f, 0.78f, 0.05f, 0.78f));
                line.SetPosition(0, transform.position);
                line.SetPosition(1, (Vector2)transform.position + directions[index] * slashLength);
            }
        }

        private void ShowSwingTelegraph()
        {
            LineRenderer line = EnsurePrimaryLine("Crayon Hero Sword Swing Hitbox");
            ConfigureLine(line, 0, swingWidth, new Color(1f, 0.05f, 0.05f, 0.82f),
                new Color(1f, 0.2f, 0.05f, 0.9f));
            UpdateSwingTelegraphPositions();
        }

        private void UpdateSwingTelegraphPositions()
        {
            if (telegraphLine == null) return;
            Vector2 start = (Vector2)transform.position + lockedDirection * SwingStartOffset;
            telegraphLine.SetPosition(0, start);
            telegraphLine.SetPosition(1, start + lockedDirection * swingRange);
        }

        private void ShowDashTelegraph()
        {
            LineRenderer line = EnsurePrimaryLine("Crayon Hero Dash Path");
            ConfigureLine(line, 2, dashWidth, new Color(1f, 0.55f, 0.05f, 0.55f),
                new Color(1f, 0.12f, 0.05f, 0.8f));
            line.SetPosition(0, transform.position);
            line.SetPosition(1, (Vector2)transform.position + lockedDirection * dashSpeed * dashDuration);
        }

        private void ShowSummonTelegraph()
        {
            ClearTelegraph();
            telegraphObject = new GameObject("Crayon Hero Summon Telegraph");
            telegraphObject.transform.position = transform.position;
            telegraphObject.transform.localScale = Vector3.one * summonRadius * 1.4f;
            SpriteRenderer renderer = telegraphObject.AddComponent<SpriteRenderer>();
            renderer.sprite = visual != null ? visual.sprite : null;
            renderer.color = new Color(0.65f, 0.25f, 1f, 0.22f);
            renderer.sortingOrder = visual != null ? visual.sortingOrder - 1 : -1;
            boss?.RegisterOwnedObject(telegraphObject);
        }

        private LineRenderer EnsurePrimaryLine(string objectName)
        {
            if (telegraphObject == null)
            {
                telegraphObject = new GameObject(objectName);
                telegraphLine = CreateLineRenderer(telegraphObject);
                boss?.RegisterOwnedObject(telegraphObject);
            }
            telegraphObject.name = objectName;
            return telegraphLine;
        }

        private LineRenderer EnsureSupplementalLine(int index)
        {
            while (supplementalTelegraphLines.Count <= index)
            {
                GameObject child = new GameObject($"Golden Slash Line {supplementalTelegraphLines.Count + 2}");
                child.transform.SetParent(telegraphObject.transform, false);
                supplementalTelegraphLines.Add(CreateLineRenderer(child));
            }
            return supplementalTelegraphLines[index];
        }

        private LineRenderer CreateLineRenderer(GameObject owner)
        {
            LineRenderer line = owner.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingLayerID = visual != null ? visual.sortingLayerID : 0;
            line.sortingOrder = visual != null ? visual.sortingOrder + 2 : 2;
            return line;
        }

        private static void ConfigureLine(LineRenderer line, int capVertices, float width, Color start, Color end)
        {
            line.numCapVertices = capVertices;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = start;
            line.endColor = end;
        }

        private void SetAllTelegraphColors(Color start, Color end)
        {
            if (telegraphLine != null)
            {
                telegraphLine.startColor = start;
                telegraphLine.endColor = end;
            }
            foreach (LineRenderer line in supplementalTelegraphLines)
            {
                if (line == null) continue;
                line.startColor = start;
                line.endColor = end;
            }
        }

        private void EnsureRecognitionDebug()
        {
            if (recognitionDebugObject != null) return;
            recognitionDebugObject = new GameObject("Crayon Hero Recognition Radius Debug");
            recognitionDebugLine = recognitionDebugObject.AddComponent<LineRenderer>();
            recognitionDebugLine.useWorldSpace = true;
            recognitionDebugLine.loop = true;
            recognitionDebugLine.positionCount = 64;
            recognitionDebugLine.startWidth = 0.06f;
            recognitionDebugLine.endWidth = 0.06f;
            recognitionDebugLine.numCapVertices = 4;
            recognitionDebugLine.material = new Material(Shader.Find("Sprites/Default"));
            recognitionDebugLine.startColor = new Color(0.18f, 0.35f, 1f, 0.9f);
            recognitionDebugLine.endColor = new Color(0.18f, 0.7f, 1f, 0.9f);
            recognitionDebugLine.sortingLayerID = visual != null ? visual.sortingLayerID : 0;
            recognitionDebugLine.sortingOrder = visual != null ? visual.sortingOrder + 3 : 3;
            boss?.RegisterOwnedObject(recognitionDebugObject);
        }

        private void UpdateRecognitionDebugCircle()
        {
            if (recognitionDebugLine == null) return;
            Vector2 center = transform.position;
            float radius = SwingTriggerRange;
            for (int index = 0; index < recognitionDebugLine.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / recognitionDebugLine.positionCount;
                recognitionDebugLine.SetPosition(index,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void ClearRecognitionDebug()
        {
            if (recognitionDebugObject == null) return;
            if (Application.isPlaying) Destroy(recognitionDebugObject);
            else DestroyImmediate(recognitionDebugObject);
            recognitionDebugObject = null;
            recognitionDebugLine = null;
        }

        private void ClearTelegraph()
        {
            if (telegraphObject != null)
            {
                if (Application.isPlaying) Destroy(telegraphObject);
                else DestroyImmediate(telegraphObject);
            }
            telegraphObject = null;
            telegraphLine = null;
            supplementalTelegraphLines.Clear();
        }

        private void ApplyPhasePresentation()
        {
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            if (visual != null) visual.color = boss != null && boss.CurrentPhase >= 2 ? phaseTwoColor : Color.white;
        }

        private void OnHealthChanged(float current, float maximum)
        {
            if (!summonAvailable && maximum > 0f &&
                current <= healthAtLastSummon - maximum * summonReactivationHealthFraction + 0.001f)
                summonAvailable = true;
            if (maximum > 0f && current <= maximum * 0.5f) ApplyPhasePresentation();
        }

        private void ResetSwingState()
        {
            swingPreparing = false;
            swingDirectionLocked = false;
            swingResolvesAt = 0f;
            nextSwingStartsAt = 0f;
            swingCountTarget = 0;
            completedSwings = 0;
        }

        private void ResetDashState()
        {
            dashInMotion = false;
            dashChainHitApplied = false;
            dashStateEndsAt = 0f;
            dashCountTarget = 0;
            completedDashes = 0;
        }

        private static bool IsPressureAttack(BossPatternExecution execution)
        {
            return execution == BossPatternExecution.CrayonHeroApproachSwing ||
                   execution == BossPatternExecution.CrayonHeroDashChain ||
                   execution == BossPatternExecution.CrayonHeroSummonMinions;
        }

        private void ResolveArenaBounds()
        {
            RoomNode room = GetComponentInParent<RoomNode>();
            if (room != null && room.Profile != null)
            {
                Rect local = new Rect(-room.Profile.InteriorSize * 0.5f, room.Profile.InteriorSize);
                Vector2 minimum = room.transform.TransformPoint(local.min);
                Vector2 maximum = room.transform.TransformPoint(local.max);
                arenaBounds = Rect.MinMaxRect(Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                    Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
                return;
            }
            arenaBounds = new Rect((Vector2)transform.position - fallbackHalfExtents, fallbackHalfExtents * 2f);
        }

        private Vector2 ClampToArena(Vector2 position)
        {
            Collider2D collider = GetComponent<Collider2D>();
            Vector2 extents = collider != null ? (Vector2)collider.bounds.extents : Vector2.one * arenaPadding;
            float horizontal = Mathf.Min(extents.x + arenaPadding, arenaBounds.width * 0.49f);
            float vertical = Mathf.Min(extents.y + arenaPadding, arenaBounds.height * 0.49f);
            return new Vector2(Mathf.Clamp(position.x, arenaBounds.xMin + horizontal, arenaBounds.xMax - horizontal),
                Mathf.Clamp(position.y, arenaBounds.yMin + vertical, arenaBounds.yMax - vertical));
        }

        private void StopMovement()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private float ResolveBodyRadius()
        {
            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null) return 0f;
            Vector2 extents = collider.bounds.extents;
            return Mathf.Max(extents.x, extents.y);
        }

        private void ResolveComponents()
        {
            if (boss == null) boss = GetComponent<BossController>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (health == null) health = GetComponent<Health>();
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        }

        private int NextRandom(int maximumExclusive)
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return maximumExclusive <= 1 ? 0 : (int)(randomState % (uint)maximumExclusive);
        }

        private static uint SeedToState(int seed)
        {
            uint state = unchecked((uint)seed) ^ 0xC2A70A31u;
            return state == 0u ? 0x27D4EB2Fu : state;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, SwingTriggerRange);
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, swingRange);
        }
    }
}
