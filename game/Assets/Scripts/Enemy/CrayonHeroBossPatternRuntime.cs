using System.Collections.Generic;
using TrickalFanGame.Audio;
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
        [SerializeField] private EnemyDamageTier slashDamageTier = EnemyDamageTier.Critical;
        [SerializeField, Min(0f)] private float goldenSlashSpreadDegrees = 15f;

        [Header("Summons")]
        [SerializeField] private GameObject[] minionPrefabs = new GameObject[0];
        [SerializeField, Min(1)] private int summonsPerPattern = 4;
        [SerializeField, Min(0.1f)] private float summonRadius = 2.5f;
        [SerializeField, Range(0.01f, 1f)] private float summonReactivationHealthFraction = 0.15f;

        [Header("Approach swing")]
        [SerializeField, Min(0f)] private float approachSpeed = 3.2f;
        [SerializeField, Min(1f)] private float phaseTwoSpeedMultiplier = 1.35f;
        [SerializeField, Min(0.1f)] private float swingRange = 2.28f;
        [SerializeField, Min(0.1f)] private float swingWidth = 3f;
        [SerializeField, Min(0f)] private float swingStartGap = 0.15f;
        [SerializeField, Min(0.05f)] private float swingWindup = 0.3f;
        [SerializeField, Min(0.05f)] private float swingInterval = 0.7f;
        [SerializeField, Min(0.01f)] private float swingDirectionLockDuration = 0.12f;
        [SerializeField] private EnemyDamageTier swingDamageTier = EnemyDamageTier.Medium;
        [SerializeField, Min(1f)] private float swingRecognitionMultiplier = 1.15f;
        // While a sword swing winds up, strikes and follows through, the hero keeps walking at this share of its
        // speed (2026-11-20 user: 40% slower). Walking up to the player before a swing stays at full speed.
        [SerializeField, Range(0f, 1f)] private float swingMoveSpeedMultiplier = 0.6f;
        public float SwingMoveSpeedMultiplier => swingMoveSpeedMultiplier;
        public void ConfigureSwingMoveSpeed(float multiplier) => swingMoveSpeedMultiplier = Mathf.Clamp01(multiplier);
        // Matches the follow-through pose (attack pose 3) after each strike.
        private const float SwingFollowThroughDuration = 0.2f;
        public bool IsSwingAttacking => swingPreparing || lastTickTime - attackFollowThroughStartedAt < SwingFollowThroughDuration;
        private float SwingMoveMultiplier => IsSwingAttacking ? swingMoveSpeedMultiplier : 1f;

        [Header("Dash chain")]
        // One step: every leap takes this long however far it goes (2026-11-23 user).
        [SerializeField, Min(0.01f)] private float dashDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float dashRestDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float dashWidth = 1.2f;
        [SerializeField] private EnemyDamageTier dashDamageTier = EnemyDamageTier.Light;
        // Boss-Pattern-1 (28번 §10.4): warning at the player's spot, leap to just before it, strike after a beat.
        [SerializeField, Min(0.05f)] private float dashWarningDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float goldenDashWarningDuration = 0.3f;
        [SerializeField, Min(0f)] private float dashStrikeDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float dashChainRecovery = 0.8f;
        // Smaller than the drawn slash (7.4 x about 1.5) on purpose.
        [SerializeField, Min(0.1f)] private float riftHitLength = 4.2f;
        [SerializeField, Min(0.1f)] private float riftHitWidth = 0.9f;
        // The boss swings from in front of the cut, so its drawn body (about 1.1 to each side) stays this far off the
        // rift's line. It also keeps a player who stayed on the warning out of the leap's path hit.
        [SerializeField, Min(0f)] private float riftBodyClearance = 1.2f;

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
        [SerializeField] private Sprite awakenedSprite;
        // The phase transition (BossController.PhaseTransitionDuration) plays the four transformation frames for these
        // lengths, then a stagger in the new form, all invulnerable. Values from the 크레용사용 motion tuner
        // (2026-11-20 user): 0.30 / 0.70 / 1.00 / 0.25 + 0.65 stagger, tremble 0.02.
        [SerializeField] private float[] awakeningFrameDurations = { 0.3f, 0.7f, 1f, 0.25f };
        [SerializeField, Min(0f)] private float awakeningStagger = 0.65f;
        [SerializeField, Min(0f)] private float awakeningTremble = 0.02f;
        private Sprite normalSprite;
        public float AwakeningStagger => awakeningStagger;
        public float AwakeningTremble => awakeningTremble;
        public float AwakeningFrameDuration(int index) =>
            awakeningFrameDurations != null && index >= 0 && index < awakeningFrameDurations.Length
                ? Mathf.Max(0.01f, awakeningFrameDurations[index]) : 0.25f;
        public float AwakeningDuration
        {
            get
            {
                float total = awakeningStagger;
                for (int i = 0; i < 4; i++) total += AwakeningFrameDuration(i);
                return total;
            }
        }
        public void ConfigureAwakening(float[] frameDurations, float stagger, float tremble)
        {
            awakeningFrameDurations = frameDurations != null && frameDurations.Length == 4
                ? (float[])frameDurations.Clone() : new[] { 0.3f, 0.7f, 1f, 0.25f };
            awakeningStagger = Mathf.Max(0f, stagger);
            awakeningTremble = Mathf.Max(0f, tremble);
        }
        public Sprite AwakenedSprite => awakenedSprite;
        public void ConfigureAwakenedArtwork(Sprite sprite) => awakenedSprite = sprite;

        private const float LeapTerrainSkin = 0.02f;
        private const int LeapSlideSteps = 4;
        // sin 30: a dash crosses its rift at 30 degrees or more.
        private const float MinRiftCrossingSine = 0.5f;
        // The swing and the map slash hit real boxes, so their corners are only softened.
        private const float WarningCornerRadius = 0.18f;
        // The hitbox lines sort two above the boss sprite; the drawn areas go one below it.
        private const int WarningOrderOffset = -3;

        private static readonly List<RaycastHit2D> FeetHits = new List<RaycastHit2D>();

        private enum DashStage { None, Warning, Leap, StrikeDelay, Rest }

        private readonly List<(Collider2D own, Collider2D player)> dashIgnoredPairs =
            new List<(Collider2D own, Collider2D player)>();
        private readonly List<GameObject> livingMinions = new List<GameObject>();
        private readonly List<LineRenderer> supplementalTelegraphLines = new List<LineRenderer>();
        private BossController boss;
        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer visual;
        private Transform target;
        private Rect arenaBounds;
        // Where the feet may stand (the room's movement bounds). The slash light still spans arenaBounds.
        private Rect standingBounds;
        private uint randomState;
        private float lastTickTime;
        // MovePosition from Update only lands at the next physics step, and only the last call before it counts. The
        // queued destination is the base for the next move in the same step, so speed does not depend on frame rate.
        private Vector2 queuedMovePosition;
        private float queuedMoveFixedTime = float.NaN;
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
        private bool dashHitApplied;
        private float dashStateEndsAt;
        private int dashCountTarget;
        private int completedDashes;
        private DashStage dashStage;
        private bool riftPlaced;
        private Vector2 riftCenter;
        private float riftAngle;
        private Vector2 leapStart, leapEnd;
        private float leapStartedAt, leapDuration;
        private bool leapAdvances;
        private CrayonDashRiftEffect riftEffect;
        private GameObject telegraphObject;
        private LineRenderer telegraphLine;
        private MeleeWarningVisual swingWarning;
        private GameObject recognitionDebugObject;
        private LineRenderer recognitionDebugLine;
        private int attackPoseIndex = -1;
        private int summonPoseIndex = -1;
        private float summonStartedAt, summonTelegraphEndsAt;
        public int SummonPoseIndex => summonPoseIndex;
        private float attackFollowThroughStartedAt = float.NegativeInfinity;
        private float chargeStartedAt;
        private float chargeImpactAt;
        private bool chargingSlash;

        // Sound pass 2026-11-21: the slam is one swing with two layers landing after it, at the offsets set by ear in
        // tools/sound-mixer; the charge hums on a voice cut by the slam; the transformation plays its first
        // seconds and fades.
        public const float SlamMagicDelay = 0.27f;
        public const float SlamClashDelay = 0.39f;
        public const float TransformSoundSeconds = 3f;
        public const float TransformFadeSeconds = 1.5f;
        private int chargeVoice;
        public bool IsChargeSoundActive => chargeVoice != 0;

        private void StopChargeSound(float fadeSeconds = 0.08f)
        {
            CombatSfx.StopVoice(chargeVoice, fadeSeconds);
            chargeVoice = 0;
        }

        // Pose groups: swing 0..3, dash 4..7, slam 8..11, stronger charge stages 12..13.
        public int AttackPoseIndex => attackPoseIndex;
        public float ChargeImpactAt => chargeImpactAt;
        public int ChargeBeat => chargingSlash ? Mathf.Min(2,
            Mathf.FloorToInt(Mathf.Clamp01((lastTickTime - chargeStartedAt) /
                Mathf.Max(0.001f, chargeImpactAt - chargeStartedAt)) * 3f)) : -1;

        private void UpdateChargeArtwork()
        {
            if (!chargingSlash) return;
            // Keep each accumulated aura until the next charge, including the last frame before impact.
            attackPoseIndex = ChargeBeat == 0 ? 9 : ChargeBeat == 1 ? 12 : 13;
        }

        private void ResetAttackArtwork()
        {
            attackPoseIndex = -1;
            summonPoseIndex = -1;
            attackFollowThroughStartedAt = float.NegativeInfinity;
            chargingSlash = false;
        }

        public float SlashLength => slashLength;
        public float SlashWidth => slashWidth;
        public float SlashLockedDelay => slashLockedDelay;
        public EnemyDamageTier SlashDamageTier => slashDamageTier;
        public EnemyDamageTier SwingDamageTier => swingDamageTier;
        public EnemyDamageTier DashDamageTier => dashDamageTier;
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
        public float SwingRecognitionMultiplier => swingRecognitionMultiplier;
        public float SwingWindup => swingWindup;
        public float SwingInterval => swingInterval;
        public float SwingDirectionLockDuration => swingDirectionLockDuration;
        public int SwingCountTarget => swingCountTarget;
        public int CompletedSwings => completedSwings;
        public bool IsSwingPreparing => swingPreparing;
        public float DashDuration => dashDuration;
        public float DashRestDuration => dashRestDuration;
        public float DashStrikeDelay => dashStrikeDelay;
        public float DashChainRecovery => dashChainRecovery;
        public float CurrentDashWarningDuration =>
            boss != null && boss.CurrentPhase >= 2 ? goldenDashWarningDuration : dashWarningDuration;
        public float RiftHitLength => riftHitLength;
        public float RiftHitWidth => riftHitWidth;
        public bool HasPlacedRift => riftPlaced;
        public Vector2 RiftCenter => riftCenter;
        public float RiftAngle => riftAngle;
        public Vector2 LeapEnd => leapEnd;
        public int DashCountTarget => dashCountTarget;
        public int CompletedDashes => completedDashes;
        public bool IsDashInMotion => dashInMotion;
        public bool DashHitApplied => dashHitApplied;
        public bool IsPassingThroughPlayer => dashIgnoredPairs.Count > 0;
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
            ResetAttackArtwork();
            StopMovement();
            ClearTelegraph();
            ClearRecognitionDebug();
            StopChargeSound(0f);
            ResetDashState();
            RestorePlayerCollision(true);
            if (health != null) health.SetInvulnerable(false);
        }

        public void ConfigureSlash(float length, float width, float lockedDelay, EnemyDamageTier damageTier)
        {
            slashLength = Mathf.Max(1f, length);
            slashWidth = Mathf.Max(0.1f, width);
            slashLockedDelay = Mathf.Max(0.05f, lockedDelay);
            slashDamageTier = damageTier;
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
            float startGap, float windup, float interval, float directionLockDuration, EnemyDamageTier damageTier)
        {
            approachSpeed = Mathf.Max(0f, speed);
            phaseTwoSpeedMultiplier = Mathf.Max(1f, phaseTwoMultiplier);
            swingRange = Mathf.Max(0.1f, range);
            swingWidth = Mathf.Max(0.1f, width);
            swingStartGap = Mathf.Max(0f, startGap);
            swingWindup = Mathf.Max(0.05f, windup);
            swingInterval = Mathf.Max(swingWindup, interval);
            swingDirectionLockDuration = Mathf.Clamp(directionLockDuration, 0.01f, swingWindup);
            swingDamageTier = damageTier;
        }

        public void ConfigureDash(float duration, float restDuration, float width, EnemyDamageTier damageTier)
        {
            dashDuration = Mathf.Max(0.01f, duration);
            dashRestDuration = Mathf.Max(0.01f, restDuration);
            dashWidth = Mathf.Max(0.1f, width);
            dashDamageTier = damageTier;
        }

        public void ConfigureDashRift(float warning, float goldenWarning, float strikeDelay, float chainRecovery,
            float hitLength, float hitWidth, float bodyClearance)
        {
            dashWarningDuration = Mathf.Max(0.05f, warning);
            goldenDashWarningDuration = Mathf.Max(0.05f, goldenWarning);
            dashStrikeDelay = Mathf.Max(0f, strikeDelay);
            dashChainRecovery = Mathf.Max(0.01f, chainRecovery);
            riftHitLength = Mathf.Max(0.1f, hitLength);
            riftHitWidth = Mathf.Max(0.1f, hitWidth);
            riftBodyClearance = Mathf.Max(0f, bodyClearance);
        }

        // How far beyond its real reach the boss starts a swing (1 = only when the player is already inside).
        public void ConfigureSwingRecognition(float multiplier) =>
            swingRecognitionMultiplier = Mathf.Max(1f, multiplier);

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
            ResetAttackArtwork();
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
            queuedMoveFixedTime = float.NaN;
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
                StopChargeSound();
                CombatSfx.PlayVoiceFor(CombatSfxCue.BossCrayonTransform, TransformSoundSeconds, TransformFadeSeconds);
                ResetAttackArtwork();
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
                ResetAttackArtwork();
                ClearTelegraph();
                if (execution == BossPatternExecution.CrayonHeroMapSlash)
                {
                    chargingSlash = true;
                    chargeStartedAt = lastTickTime;
                    chargeImpactAt = stateEndsAt + slashLockedDelay;
                    attackPoseIndex = 8;
                    StopChargeSound(0f);
                    chargeVoice = CombatSfx.PlayVoice(CombatSfxCue.BossCrayonCharge);
                    StopMovement();
                    UpdateAimDirection();
                    ShowMapSlashTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroSummonMinions)
                {
                    StopMovement();
                    UpdateAimDirection();
                    summonStartedAt = lastTickTime;
                    summonTelegraphEndsAt = stateEndsAt;
                    summonPoseIndex = 0;
                    ShowSummonTelegraph();
                }
                else if (execution == BossPatternExecution.CrayonHeroDashChain)
                {
                    StopMovement();
                    boss?.OverrideEnteringTelegraphDuration(CurrentDashWarningDuration);
                    completedDashes = 0;
                    PlaceDashWarning();
                }
                return;
            }

            if (state == BossActionState.Recovery)
            {
                StopChargeSound();
                chargingSlash = false;
                summonPoseIndex = execution == BossPatternExecution.CrayonHeroSummonMinions ? 3 : -1;
                attackPoseIndex = execution == BossPatternExecution.CrayonHeroMapSlash ? 11 : -1;
                attackFollowThroughStartedAt = lastTickTime;
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
                    if (execution == BossPatternExecution.CrayonHeroDashChain)
                        boss?.OverrideEnteringRecoveryDuration(dashChainRecovery);
                }
                ResetSwingState();
                ResetDashState();
                slashPending = false;
                return;
            }

            if (state == BossActionState.Defeated)
            {
                StopChargeSound();
                ResetAttackArtwork();
                StopMovement();
                ClearTelegraph();
                ResetDashState();
                RestorePlayerCollision(true);
            }
            else if (state == BossActionState.Idle)
            {
                // A stun (Jjangsem-2) or a cancelled fight drops the pattern under way; it restarts from its telegraph.
                StopChargeSound();
                ResetAttackArtwork();
                StopMovement();
                ClearTelegraph();
                ResetSwingState();
                ResetDashState();
                chargingSlash = false;
                slashPending = false;
                slashResolved = false;
            }
        }

        public bool TryExecute(BossPatternExecution execution)
        {
            switch (execution)
            {
                case BossPatternExecution.CrayonHeroMapSlash:
                    UpdateAimDirection();
                    slashPending = true;
                    slashResolved = false;
                    slashResolvesAt = lastTickTime + slashLockedDelay;
                    chargeImpactAt = slashResolvesAt;
                    ShowMapSlashTelegraph();
                    return true;
                case BossPatternExecution.CrayonHeroSummonMinions:
                    if (!summonAvailable) return false;
                    ClearTelegraph();
                    summonPoseIndex = 2;
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
                    if (!riftPlaced) PlaceDashWarning();
                    BeginLeap(lastTickTime);
                    // Leap length depends on the distance, so the chain itself ends the Active state.
                    boss?.SynchronizeActiveEndTime(float.PositiveInfinity);
                    return true;
                default:
                    return false;
            }
        }

        public void TickPattern(BossActionState state, BossPatternExecution execution, float now)
        {
            float deltaTime = Mathf.Clamp(now - lastTickTime, 0f, 0.1f);
            lastTickTime = now;
            RestorePlayerCollision(false);
            if (state == BossActionState.Telegraph)
            {
                UpdateChargeArtwork();
                if (execution == BossPatternExecution.CrayonHeroSummonMinions)
                    summonPoseIndex = (now - summonStartedAt) / Mathf.Max(0.001f, summonTelegraphEndsAt - summonStartedAt) < 0.35f ? 0 : 1;
                if (execution == BossPatternExecution.CrayonHeroMapSlash)
                {
                    UpdateAimDirection();
                    ShowMapSlashTelegraph();
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
                        TickDashChain(now);
                        break;
                }
                return;
            }

            if (state == BossActionState.Recovery)
            {
                if (now - attackFollowThroughStartedAt >= 0.2f)
                {
                    attackPoseIndex = -1;
                    summonPoseIndex = -1;
                }
                if (execution == BossPatternExecution.CrayonHeroApproachSwing)
                    MoveTowardTarget(deltaTime, SwingMoveMultiplier);
                return;
            }

            if (state == BossActionState.Idle || state == BossActionState.Cooldown)
            {
                ResetAttackArtwork();
                MoveTowardTarget(deltaTime, 1f);
            }
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
            // 28번 §10.4: two or three dashes, always three when golden. The old 2:1 ratio of two to three is kept.
            if (golden) return 3;
            return Mathf.Clamp(roll, 0, 99) < 67 ? 2 : 3;
        }

        // The side of a slash's line that is drawn first follows the dash direction and alternates each dash.
        public static float ResolveRiftAngle(Vector2 dashDirection, int dashIndex)
        {
            const float UpperLeftToLowerRight = -36f, UpperRightToLowerLeft = 216f;
            bool startsUpperLeft = dashDirection.x >= 0f;
            if (dashIndex % 2 != 0) startsUpperLeft = !startsUpperLeft;
            float angle = startsUpperLeft ? UpperLeftToLowerRight : UpperRightToLowerLeft;
            // A cut lying along the dash would put the boss on its own line, so that dash uses the other diagonal.
            if (RiftCrossingSine(dashDirection, angle) < MinRiftCrossingSine)
                angle = startsUpperLeft ? UpperRightToLowerLeft : UpperLeftToLowerRight;
            return angle;
        }

        // How far before the rift's center, along the dash, the boss's body clears the rift's line.
        public static float ResolveRiftStopDistance(Vector2 dashDirection, float riftAngle, float bodyClearance) =>
            bodyClearance / Mathf.Max(MinRiftCrossingSine, RiftCrossingSine(dashDirection, riftAngle));

        private static float RiftCrossingSine(Vector2 direction, float riftAngle)
        {
            float radians = riftAngle * Mathf.Deg2Rad;
            Vector2 unit = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            return Mathf.Abs(unit.x * Mathf.Sin(radians) - unit.y * Mathf.Cos(radians));
        }

        public static bool IsInsideRift(Vector2 point, Vector2 center, float angle, float length, float width)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 offset = point - center;
            return Mathf.Abs(Vector2.Dot(offset, along)) <= length * 0.5f &&
                   Mathf.Abs(Vector2.Dot(offset, new Vector2(-along.y, along.x))) <= width * 0.5f;
        }

        private void TickMapSlash(float now)
        {
            UpdateChargeArtwork();
            if (slashResolved)
                attackPoseIndex = now - attackFollowThroughStartedAt < 0.08f ? 10 : 11;
            if (!slashPending || slashResolved || now < slashResolvesAt) return;
            int hitCount = 0;
            foreach (Vector2 direction in GetSlashDirections())
                if (IsTargetInsideDirectionalBox(0f, slashLength, ActiveSlashWidth, direction)) hitCount++;
            if (hitCount > 0) ApplyTargetDamage(slashDamageTier, DamageSourceType.EnemyMelee, hitCount);
            for (int index = 0; index < GetSlashDirections().Count; index++)
            {
                LineRenderer warning = index == 0 ? telegraphLine : supplementalTelegraphLines[index - 1];
                float lightHeight = Mathf.Clamp(ResolveEffectBodyWidth() * 1.6f, 5f, 9f);
                SlamLightEffect.PlayPath(warning, lightHeight, transform, visual, arenaBounds);
            }
            // Golden phase slams three directions at once; one sound covers them. The charge stops as the swing
            // lands, and the magic burst and the clash follow it.
            StopChargeSound(0.05f);
            CombatSfx.Play(CombatSfxCue.BossSwordSlam);
            CombatSfx.PlayAfter(CombatSfxCue.BossSwordSlamMagic, SlamMagicDelay);
            CombatSfx.PlayAfter(CombatSfxCue.BossSwordSlamClash, SlamClashDelay);
            slashResolved = true;
            slashPending = false;
            chargingSlash = false;
            attackFollowThroughStartedAt = now;
            attackPoseIndex = 10;
            SetAllTelegraphColors(new Color(1f, 0.08f, 0.02f, 0.95f),
                new Color(1f, 0.45f, 0.02f, 0.95f));
        }

        private void TickApproachSwing(float now, float deltaTime)
        {
            if (!swingPreparing)
                attackPoseIndex = now - attackFollowThroughStartedAt < 0.08f ? 2 :
                    now - attackFollowThroughStartedAt < 0.2f ? 3 : -1;
            if (target == null || completedSwings >= swingCountTarget) return;
            MoveTowardTarget(deltaTime, SwingMoveMultiplier);
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            if (!swingPreparing)
            {
                if (now < nextSwingStartsAt || offset.magnitude > SwingTriggerRange) return;
                UpdateAimDirection();
                swingPreparing = true;
                swingDirectionLocked = false;
                swingResolvesAt = now + swingWindup;
                attackPoseIndex = 0;
                ShowSwingTelegraph();
                if (swingWarning != null) swingWarning.SetTiming(swingWindup, swingWindup, false);
                return;
            }

            if (!swingDirectionLocked)
            {
                if (now < swingResolvesAt - swingDirectionLockDuration) UpdateAimDirection();
                else swingDirectionLocked = true;
            }
            UpdateSwingTelegraphPositions();
            if (swingWarning != null)
                swingWarning.SetTiming(swingResolvesAt - now, swingWindup, swingDirectionLocked);
            if (now < swingResolvesAt)
            {
                attackPoseIndex = now < swingResolvesAt - swingWindup * 0.5f ? 0 : 1;
                return;
            }
            ResolveDirectionalHit(SwingStartOffset, swingRange, swingWidth, swingDamageTier,
                DamageSourceType.EnemyMelee, lockedDirection);
            CombatSpriteEffect.Play("sword-slash", (Vector2)transform.position +
                lockedDirection * (SwingStartOffset + swingRange * 0.5f),
                swingRange * 2f, Mathf.Max(swingWidth * 2f, ResolveEffectBodyWidth() * 1.2f), 0.35f, transform, visual,
                angle: Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg);
            CombatSfx.Play(CombatSfxCue.BossSwordSwing);
            completedSwings++;
            attackPoseIndex = 2;
            attackFollowThroughStartedAt = now;
            swingPreparing = false;
            swingDirectionLocked = false;
            nextSwingStartsAt = now + Mathf.Max(0f, swingInterval - swingWindup);
            ClearTelegraph();
        }

        private void TickDashChain(float now)
        {
            if (target == null || dashStage == DashStage.None || completedDashes >= dashCountTarget)
            {
                boss?.SynchronizeActiveEndTime(now);
                return;
            }
            switch (dashStage)
            {
                case DashStage.Leap:
                    TickLeap(now);
                    break;
                case DashStage.StrikeDelay:
                    if (now >= dashStateEndsAt) ResolveRiftStrike(now);
                    break;
                case DashStage.Rest:
                    if (now < dashStateEndsAt) break;
                    PlaceDashWarning();
                    dashStage = DashStage.Warning;
                    dashStateEndsAt = now + CurrentDashWarningDuration;
                    break;
                case DashStage.Warning:
                    if (now >= dashStateEndsAt) BeginLeap(now);
                    break;
            }
        }

        // Fixed where the player stands now; it does not follow them afterwards.
        private void PlaceDashWarning()
        {
            ClearRiftWarning();
            attackPoseIndex = 4;
            if (target == null) return;
            UpdateAimDirection();
            riftCenter = target.position;
            riftAngle = ResolveRiftAngle(lockedDirection, completedDashes);
            riftPlaced = true;
            riftEffect = CrayonDashRiftEffect.Show(riftCenter, riftAngle, visual, WarningOrderOffset);
            if (riftEffect != null) boss?.RegisterOwnedObject(riftEffect.gameObject);
        }

        private void BeginLeap(float now)
        {
            dashStage = DashStage.Leap;
            dashInMotion = true;
            dashHitApplied = false;
            attackPoseIndex = completedDashes % 2 == 0 ? 5 : 6;
            leapStart = CurrentMovePosition;
            Vector2 toRift = riftCenter - leapStart;
            if (toRift.sqrMagnitude > 0.0001f) lockedDirection = toRift.normalized;
            // Negative when the player is too close: the boss hops back so the cut is still in front of it.
            float distance = toRift.magnitude - ResolveRiftStopDistance(lockedDirection, riftAngle, riftBodyClearance);
            leapAdvances = distance > 0f;
            leapEnd = ClampToArena(ResolveLeapEnd(leapStart, lockedDirection * distance));
            leapStartedAt = now;
            leapDuration = dashDuration;
            dashStateEndsAt = now + leapDuration;
            SetPlayerCollisionIgnored();
            BossMovementAnimator animator = GetComponent<BossMovementAnimator>();
            DashAfterimageEffect.Play(animator != null && animator.Artwork != null ? animator.Artwork : visual,
                leapEnd - leapStart);
            CombatSfx.Play(CombatSfxCue.BossSwordSwing);
        }

        private void TickLeap(float now)
        {
            Vector2 from = CurrentMovePosition;
            Vector2 next = Vector2.Lerp(leapStart, leapEnd, (now - leapStartedAt) / Mathf.Max(0.001f, leapDuration));
            if (!dashHitApplied && leapAdvances && IsTargetNearSegment(from, next, dashWidth * 0.5f))
            {
                ApplyTargetDamage(dashDamageTier, DamageSourceType.EnemyContact);
                dashHitApplied = true;
            }
            QueueMove(next);
            if (now < dashStateEndsAt) return;
            dashInMotion = false;
            attackPoseIndex = completedDashes % 2 == 0 ? 6 : 7;
            dashStage = DashStage.StrikeDelay;
            dashStateEndsAt += dashStrikeDelay;
            if (now >= dashStateEndsAt) ResolveRiftStrike(now);
        }

        private void ResolveRiftStrike(float now)
        {
            if (!dashHitApplied && IsDamageableTarget() &&
                IsInsideRift(target.position, riftCenter, riftAngle, riftHitLength, riftHitWidth))
            {
                ApplyTargetDamage(dashDamageTier, DamageSourceType.EnemyMelee);
                dashHitApplied = true;
            }
            if (riftEffect != null) riftEffect.Strike();
            riftEffect = null;
            riftPlaced = false;
            CombatSfx.Play(CombatSfxCue.BossSwordSlamClash);
            completedDashes++;
            if (completedDashes >= dashCountTarget)
            {
                dashStage = DashStage.None;
                boss?.SynchronizeActiveEndTime(now);
                return;
            }
            attackPoseIndex = -1;
            dashStage = DashStage.Rest;
            dashStateEndsAt = now + dashRestDuration;
        }

        private bool IsDamageableTarget()
        {
            if (target == null) return false;
            Health targetHealth = target.GetComponentInParent<Health>();
            return targetHealth != null && !targetHealth.IsDead && target.GetComponentInParent<PlayerMovement>() != null;
        }

        private bool IsTargetNearSegment(Vector2 start, Vector2 end, float radius)
        {
            if (!IsDamageableTarget()) return false;
            Vector2 point = target.position;
            Vector2 segment = end - start;
            float t = segment.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
            return (point - (start + segment * t)).sqrMagnitude <= radius * radius;
        }

        // The leap crosses the player but not walls, obstacles or pits, so it cannot land the feet inside them.
        private Vector2 ResolveLeapEnd(Vector2 start, Vector2 move)
        {
            EnemyFeet feet = GetComponentInChildren<EnemyFeet>();
            if (feet == null || move.sqrMagnitude <= 0.0001f) return start + move;
            Collider2D feetCollider = feet.Capsule != null && feet.Capsule.enabled ? feet.Capsule : feet.Collider;
            if (feetCollider == null) return start + move;
            Bounds shape = feetCollider.bounds;
            Vector2 feetOffset = (Vector2)shape.center - (Vector2)transform.position;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(EnemyFeet.TerrainMask);

            // A wall takes away only the part of the move going into it; the rest continues along the wall.
            Vector2 position = start, remaining = move;
            for (int bounce = 0; bounce < LeapSlideSteps && remaining.sqrMagnitude > 0.0001f; bounce++)
            {
                float length = remaining.magnitude, travel = length;
                Vector2 direction = remaining / length, blockNormal = Vector2.zero;
                FeetHits.Clear();
                Physics2D.CapsuleCast(position + feetOffset, shape.size, CapsuleDirection2D.Horizontal, 0f, direction,
                    filter, FeetHits, length + LeapTerrainSkin);
                foreach (RaycastHit2D hit in FeetHits)
                {
                    if (hit.collider == null || Physics2D.GetIgnoreCollision(feetCollider, hit.collider)) continue;
                    Vector2 normal = hit.normal;
                    // A cast that starts in contact has no useful normal, so ask which way the wall really is.
                    if (bounce == 0 && hit.distance <= LeapTerrainSkin)
                    {
                        ColliderDistance2D contact = feetCollider.Distance(hit.collider);
                        if (contact.isValid) normal = -contact.normal;
                    }
                    if (Vector2.Dot(normal, direction) >= -0.001f) continue;
                    float free = Mathf.Max(0f, hit.distance - LeapTerrainSkin);
                    if (free >= travel) continue;
                    travel = free;
                    blockNormal = normal;
                }
                position += direction * travel;
                remaining -= direction * travel;
                if (blockNormal == Vector2.zero) break;
                remaining -= blockNormal * Vector2.Dot(remaining, blockNormal);
            }
            return position;
        }

        // A Kinematic body shoves whatever it meets, so the boss and the player stop colliding for the leap. That also
        // keeps ContactDamage from adding a second hit to the dash.
        private void SetPlayerCollisionIgnored()
        {
            PlayerMovement player = target != null ? target.GetComponentInParent<PlayerMovement>() : null;
            if (player == null) return;
            foreach (Collider2D own in GetComponentsInChildren<Collider2D>())
            {
                if (own.isTrigger) continue;
                foreach (Collider2D other in player.GetComponentsInChildren<Collider2D>())
                {
                    if (other.isTrigger || Physics2D.GetIgnoreCollision(own, other)) continue;
                    Physics2D.IgnoreCollision(own, other, true);
                    dashIgnoredPairs.Add((own, other));
                }
            }
        }

        // Collision returns after the strike, once the two no longer overlap, so the player is not popped aside.
        private void RestorePlayerCollision(bool force)
        {
            if (dashIgnoredPairs.Count == 0) return;
            if (!force)
            {
                if (dashStage == DashStage.Leap || dashStage == DashStage.StrikeDelay) return;
                foreach ((Collider2D own, Collider2D player) in dashIgnoredPairs)
                    if (own != null && player != null && own.enabled && player.enabled &&
                        own.gameObject.activeInHierarchy && player.gameObject.activeInHierarchy &&
                        own.Distance(player).isOverlapped)
                        return;
            }
            foreach ((Collider2D own, Collider2D player) in dashIgnoredPairs)
                if (own != null && player != null) Physics2D.IgnoreCollision(own, player, false);
            dashIgnoredPairs.Clear();
        }

        private void ClearRiftWarning()
        {
            if (riftEffect != null)
            {
                if (Application.isPlaying) Destroy(riftEffect.gameObject);
                else DestroyImmediate(riftEffect.gameObject);
            }
            riftEffect = null;
            riftPlaced = false;
        }

        private void MoveTowardTarget(float deltaTime, float speedMultiplier)
        {
            if (target == null || body == null || deltaTime <= 0f) return;
            Vector2 origin = CurrentMovePosition;
            Vector2 offset = (Vector2)target.position - origin;
            if (offset.sqrMagnitude <= 0.001f) return;
            float distance = offset.magnitude;
            float step = Mathf.Min(
                CurrentApproachSpeed * speedMultiplier * EnemyStatusEffects.MoveSpeedMultiplierOf(this) * deltaTime,
                distance - BossTargetSpacing.ResolveStopDistance(gameObject, target));
            if (step <= 0f) return;
            QueueMove(ClampToArena(origin + offset / distance * step));
        }

        private Vector2 CurrentMovePosition =>
            queuedMoveFixedTime == Time.fixedTime ? queuedMovePosition : (Vector2)transform.position;

        private void QueueMove(Vector2 destination)
        {
            body.MovePosition(destination);
            queuedMovePosition = destination;
            queuedMoveFixedTime = Time.fixedTime;
        }

        private bool ResolveDirectionalHit(float startOffset, float length, float width, EnemyDamageTier damageTier,
            DamageSourceType sourceType, Vector2 direction)
        {
            if (!IsTargetInsideDirectionalBox(startOffset, length, width, direction)) return false;
            ApplyTargetDamage(damageTier, sourceType);
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

        private void ApplyTargetDamage(EnemyDamageTier damageTier, DamageSourceType sourceType, int hitCount = 1)
        {
            Health targetHealth = target != null ? target.GetComponentInParent<Health>() : null;
            if (targetHealth == null || targetHealth.IsDead) return;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("BOSS");
            targetHealth.TakeDamage(HealthUnits.CreateEnemyDamageContext(gameObject, sourceType, damageTier, hitCount));
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
                Vector2 position = (Vector2)transform.position +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * summonRadius;
                GameObject minion = Instantiate(prefab, position, Quaternion.identity, transform.parent);
                // Clamp by the minion's own feet: the boss's feet sit far below its root, so the boss's root area
                // reaches above the top wall and would put a small minion's feet inside it.
                Rect minionArea = EnemyFeet.StandingRootArea(minion, standingBounds);
                minion.transform.position = new Vector3(Mathf.Clamp(position.x, minionArea.xMin, minionArea.xMax),
                    Mathf.Clamp(position.y, minionArea.yMin, minionArea.yMax), minion.transform.position.z);
                minion.name = prefab.name + " (Crayon Summon)";
                minion.GetComponent<TestEnemy>()?.ConfigureReward(false);
                minion.GetComponent<EnemyBehaviorContext>()?.BeginCombat(target);
                livingMinions.Add(minion);
                boss?.RegisterOwnedObject(minion);
            }
            // Sound pass 2026-11-21: the drawn sword is this boss's own summon sound, in place of the generic appear pop.
            if (count > 0) CombatSfx.Play(CombatSfxCue.BossCrayonSummon);
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
                ChargeWarningVisual.Bind(line, ActiveSlashWidth, false);
                // Each slash starts under the boss, so that end is thinned. The fill arrives with the impact and
                // the flash marks the moment the direction is fixed.
                MeleeWarningVisual warning = MeleeWarningVisual.Bind(line, WarningCornerRadius,
                    ResolveBodyRadius() * 1.5f, WarningOrderOffset);
                if (warning != null)
                    warning.SetTiming(chargeImpactAt - lastTickTime, chargeImpactAt - chargeStartedAt, slashPending);
                line.SetPosition(0, transform.position);
                line.SetPosition(1, (Vector2)transform.position + directions[index] * slashLength);
            }
        }

        private void ShowSwingTelegraph()
        {
            LineRenderer line = EnsurePrimaryLine("Crayon Hero Sword Swing Hitbox");
            ChargeWarningVisual.Bind(line, swingWidth, false);
            ConfigureLine(line, 0, swingWidth, new Color(1f, 0.05f, 0.05f, 0.82f),
                new Color(1f, 0.2f, 0.05f, 0.9f));
            // The line keeps the hitbox data; the drawn area sits on the floor under the boss and the player.
            swingWarning = MeleeWarningVisual.Bind(line, WarningCornerRadius, 0f, WarningOrderOffset);
            UpdateSwingTelegraphPositions();
        }

        private void UpdateSwingTelegraphPositions()
        {
            if (telegraphLine == null) return;
            Vector2 start = (Vector2)transform.position + lockedDirection * SwingStartOffset;
            telegraphLine.SetPosition(0, start);
            telegraphLine.SetPosition(1, start + lockedDirection * swingRange);
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
            if (visual == null) return;
            if (normalSprite == null) normalSprite = visual.sprite;
            visual.sprite = boss != null && boss.CurrentPhase >= 2 && awakenedSprite != null ? awakenedSprite : normalSprite;
            visual.color = Color.white;
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
            dashHitApplied = false;
            dashStateEndsAt = 0f;
            dashCountTarget = 0;
            completedDashes = 0;
            dashStage = DashStage.None;
            ClearRiftWarning();
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
                arenaBounds = ToWorld(room.transform, new Rect(-room.Profile.InteriorSize * 0.5f,
                    room.Profile.InteriorSize));
                Rect movement = room.Profile.MovementBounds;
                standingBounds = movement.width > 0f && movement.height > 0f
                    ? ToWorld(room.transform, movement)
                    : Shrink(arenaBounds, arenaPadding);
                return;
            }
            arenaBounds = new Rect((Vector2)transform.position - fallbackHalfExtents, fallbackHalfExtents * 2f);
            standingBounds = Shrink(arenaBounds, arenaPadding);
        }

        private static Rect ToWorld(Transform room, Rect local)
        {
            Vector2 minimum = room.TransformPoint(local.min);
            Vector2 maximum = room.TransformPoint(local.max);
            return Rect.MinMaxRect(Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
        }

        private static Rect Shrink(Rect area, float padding)
        {
            float horizontal = Mathf.Min(padding, area.width * 0.49f);
            float vertical = Mathf.Min(padding, area.height * 0.49f);
            return Rect.MinMaxRect(area.xMin + horizontal, area.yMin + vertical, area.xMax - horizontal,
                area.yMax - vertical);
        }

        // Boss-Pattern-0 (2026-11-19): the feet stay on the standing floor, so the hero nears walls like other enemies
        // instead of keeping its whole body circle (plus padding) inside the room.
        private Vector2 ClampToArena(Vector2 position)
        {
            Rect area = EnemyFeet.StandingRootArea(gameObject, standingBounds);
            return new Vector2(Mathf.Clamp(position.x, area.xMin, area.xMax),
                Mathf.Clamp(position.y, area.yMin, area.yMax));
        }

        private void StopMovement()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public static float ResolveSlamVisualAngle(Vector2 direction, out bool flipX)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            flipX = true;
            // Leftward attacks mirror the ground artwork rather than turning it upside down.
            if (angle > 90f) { angle -= 180f; flipX = false; }
            else if (angle < -90f) { angle += 180f; flipX = false; }
            return angle;
        }

        public static bool TryGetSlamVisualLayout(Rect arena, Vector2 origin, Vector2 direction,
            float bodyWidth, float radius, out Vector2 center, out float size)
        {
            center = origin;
            size = 0f;
            if (direction.sqrMagnitude < 0.001f || !arena.Contains(origin)) return false;
            direction.Normalize();
            // Artwork bounds include the registered transparent canvas: start at the sword side,
            // rather than leaving half that canvas plus another gap in front of the character.
            float startOffset = Mathf.Max(bodyWidth * 0.2f, radius * 0.8f);
            Vector2 start = origin + direction * startOffset;
            if (!arena.Contains(start)) return false;
            float distance = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > 0.001f)
                distance = Mathf.Min(distance, ((direction.x > 0 ? arena.xMax : arena.xMin) - start.x) / direction.x);
            if (Mathf.Abs(direction.y) > 0.001f)
                distance = Mathf.Min(distance, ((direction.y > 0 ? arena.yMax : arena.yMin) - start.y) / direction.y);
            // Scale uniformly to the room edge rather than stretching or imposing an artwork size cap.
            size = Mathf.Max(0f, distance - 0.15f);
            if (size < 0.6f) return false;
            center = start + direction * size * 0.5f;
            return true;
        }

        private float ResolveEffectBodyWidth() => Mathf.Max(ResolveBodyRadius() * 2f,
            visual != null ? visual.bounds.size.x : 0f);

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
