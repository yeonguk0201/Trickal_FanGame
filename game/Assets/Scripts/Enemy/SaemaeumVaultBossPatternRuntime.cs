using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Debugging;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(BossController), typeof(Rigidbody2D), typeof(Health))]
    public sealed class SaemaeumVaultBossPatternRuntime : MonoBehaviour, IBossPatternRuntime
    {
        // The approach walks toward the player. The fan throw removed 2026-11-18 came back 2026-11-21 (the fight was
        // too easy) as a narrower three-shot fan instead of the old five.
        [Header("Approach walk")]
        [SerializeField, Min(0f)] private float approachSpeed = 1.8f;
        [SerializeField, Min(1f)] private float phaseTwoSpeedMultiplier = 1.35f;
        [SerializeField, Min(0)] private int approachVolleys = 5;
        [SerializeField, Min(0.05f)] private float approachThrowInterval = 0.55f;
        [SerializeField, Min(1)] private int approachProjectilesPerVolley = 3;
        [SerializeField, Range(0f, 180f)] private float approachSpreadDegrees = 30f;
        [SerializeField, Min(0.1f)] private float approachProjectileSpeed = 5.5f;

        [Header("Jump sequence")]
        [SerializeField, Min(1)] private int phaseOneMinimumJumps = 2;
        [SerializeField, Min(1)] private int phaseOneMaximumJumps = 2;
        [SerializeField, Min(1)] private int phaseTwoMinimumJumps = 3;
        [SerializeField, Min(1)] private int phaseTwoMaximumJumps = 3;
        // 1.5 / 1.4 since 2026-11-18 (user Play check: the 0.95 / 0.90 jumps were too fast).
        [SerializeField, Min(0.1f)] private float phaseOneJumpDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float phaseTwoJumpDuration = 1.4f;
        // Grounded pause after each landing except the last. The next destination locks when the pause ends.
        [SerializeField, Min(0f)] private float phaseOneJumpRest = 1.0f;
        [SerializeField, Min(0f)] private float phaseTwoJumpRest = 0.85f;
        // The boss crouches this long before every jump after the first: the end of the rest turns into the wind-up.
        // The first jump crouches through the pattern's own warning (Boss-Pattern-0 sets it to the same 0.8s).
        [SerializeField, Min(0f)] private float preJumpCrouchDuration = 0.8f;
        // 7 since 2026-11-21 (was 5.5): the jumps reach farther.
        [SerializeField, Min(0.1f)] private float jumpDistance = 7f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2f;
        [SerializeField, Min(0.1f)] private float landingRadius = 2.875f;
        [SerializeField] private EnemyDamageTier landingDamageTier = EnemyDamageTier.Medium;
        [SerializeField, Min(0f)] private float landingKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float landingKnockbackDuration = 0.2f;
        // Every landing scatters treasure on a ring just outside the landing radius. Each jump turns the ring by half
        // a gap, so the safe gaps move.
        // 12 / 16 since 2026-11-21 (was 8 / 12).
        [SerializeField, Min(0)] private int phaseOneLandingBurstCount = 12;
        [SerializeField, Min(0)] private int phaseTwoLandingBurstCount = 16;
        [SerializeField, Min(0.1f)] private float phaseOneLandingBurstSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float phaseTwoLandingBurstSpeed = 5.0f;
        [SerializeField] private EnemyDamageTier landingBurstDamageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0f)] private float landingBurstSpawnMargin = 0.2f;
        // 2026-11-21: every landing leaves slime over its whole radius: one large puddle where the boss lands and a
        // ring of small puddles, touching each other, filling the rest out to the landing radius.
        [SerializeField] private bool landingLeavesSlime = true;

        [Header("Gold pillars")]
        // Pillars burst out of the floor at random spots (no spacing, no cap), except that one in pillarTargetEvery,
        // starting with the first, rises under the player. The boss breaks them by walking into them or landing near
        // them; the player breaks them by hits.
        // 7 / 9 since 2026-11-21 (was 4 / 6).
        [SerializeField, Min(0)] private int phaseOnePillars = 7;
        [SerializeField, Min(0)] private int phaseTwoPillars = 9;
        [SerializeField, Min(0f)] private float phaseOnePillarWarning = 0.8f;
        [SerializeField, Min(0f)] private float phaseTwoPillarWarning = 0.7f;
        [SerializeField, Min(0f)] private float pillarStagger = 0.12f;
        [SerializeField, Min(0.05f)] private float pillarHitRadius = 0.6f;
        [SerializeField] private EnemyDamageTier pillarDamageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0f)] private float pillarKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float pillarKnockbackDuration = 0.2f;
        [SerializeField, Min(1)] private int pillarRequiredHits = 3;
        [SerializeField, Min(1)] private int pillarTargetEvery = 3;

        [Header("Gold cage (phase two)")]
        // 2026-11-21: a ring of pillars closes around the player, then the jump sequence always follows. The gaps
        // between pillars are narrower than the player's feet; spots outside the standing area or under the boss are
        // left out, where the wall or the boss closes the ring instead.
        [SerializeField, Min(3)] private int cagePillars = 14;
        [SerializeField, Min(0.5f)] private float cageRadius = 2.4f;
        [SerializeField, Min(0f)] private float cageWarning = 0.8f;
        [SerializeField, Min(0f)] private float cageStagger = 0.02f;
        [SerializeField, Min(0f)] private float cageLifetime = 6f;

        [Header("Pillar wave")]
        // 2026-11-21: the boss stands still and rows of pillars sweep across the floor, each mark appearing as the row
        // reaches it: a line through the player, a # around the player, or a three-way fan toward the player.
        [SerializeField, Min(0.84f)] private float waveSpacing = 1.2f;
        [SerializeField, Min(0.1f)] private float waveSpeed = 12f;
        [SerializeField, Min(0f)] private float waveWarning = 0.55f;
        [SerializeField, Min(0f)] private float waveLifetime = 5f;
        [SerializeField, Min(0.5f)] private float waveGridOffset = 1.8f;
        [SerializeField, Min(1f)] private float waveGridHalfLength = 4.2f;
        [SerializeField, Range(0f, 90f)] private float waveFanDegrees = 30f;

        [Header("Gold meteor (phase two)")]
        // 8-10s since 2026-11-19 (user Play check: 5-7s ended too soon).
        [SerializeField, Min(0.1f)] private float meteorMinimumDuration = 8f;
        [SerializeField, Min(0.1f)] private float meteorMaximumDuration = 10f;
        [SerializeField, Min(0)] private int meteorOpeningCount = 5;
        [SerializeField, Min(0.01f)] private float meteorOpeningInterval = 0.35f;
        [SerializeField, Min(0.01f)] private float meteorInterval = 0.45f;
        // One lump in this many aims at the player, starting with the first.
        [SerializeField, Min(1)] private int meteorTargetEvery = 3;
        // 2026-11-21: the lump right after each aimed one lands where the player will be when it hits, following
        // their current movement. A player standing still gets it on their spot.
        [SerializeField] private bool meteorLeadsPlayer = true;
        [SerializeField, Min(0.01f)] private float meteorWarning = 0.9f;
        [SerializeField, Min(0.05f)] private float meteorRadius = 0.9f;
        [SerializeField] private EnemyDamageTier meteorDamageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0f)] private float meteorKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float meteorKnockbackDuration = 0.2f;
        // Counted from when the last lump lands, so a new rain never starts while one is still falling.
        [SerializeField, Min(0f)] private float meteorCooldownAfterRain = 8f;

        [Header("Slime scatter")]
        // Large puddles land first (the first on the player); small ones are the size of one room obstacle.
        [SerializeField, Min(0)] private int phaseOneLargeSlimes = 1;
        [SerializeField, Min(0)] private int phaseOneSmallSlimes = 4;
        [SerializeField, Min(0)] private int phaseTwoLargeSlimes = 2;
        [SerializeField, Min(0)] private int phaseTwoSmallSlimes = 5;
        [SerializeField, Min(0.1f)] private float slimePuddleRadius = 1.2f;
        [SerializeField, Min(0.1f)] private float smallSlimePuddleRadius = 0.5f;
        // 0.55 since 2026-11-18: the slow got stronger when the jump pattern got easier.
        [SerializeField, Range(0f, 1f)] private float slimeSpeedMultiplier = 0.55f;
        [SerializeField, Min(1)] private int slimeRequiredHits = BossSlimePuddle.DefaultRequiredHits;
        // Puddles stay until the player pops them; past this many, the oldest one dries up. 40 since 2026-11-21
        // (was 14): each landing now leaves 15 puddles, three landings in phase two.
        [SerializeField, Min(1)] private int maximumSlimePuddles = 48;
        [SerializeField, Min(0.05f)] private float slimeFlightDuration = 0.7f;
        [SerializeField, Min(0f)] private float slimeLaunchStagger = 0.08f;
        [SerializeField, Min(0f)] private float slimeArcHeight = 2.4f;
        // The first lump lands on the player; the rest scatter on a ring this wide around them.
        [SerializeField, Min(0f)] private float slimeScatterMinimumDistance = 1.6f;
        [SerializeField, Min(0.1f)] private float slimeScatterMaximumDistance = 3.6f;
        // Puddles from one throw do not overlap, and never land closer than this between centers.
        [SerializeField, Min(0f)] private float slimeMinimumSpacing = 1f;

        [Header("Treasure heal")]
        [SerializeField, Min(0.01f)] private float healPerUse = 138f;
        [SerializeField, Min(1)] private int healPulses = 4;

        [Header("Arena and presentation")]
        [SerializeField, Min(0f)] private float arenaPadding = 0.8f;
        [SerializeField] private Vector2 fallbackHalfExtents = new(6.2f, 5.0f);
        [SerializeField] private Vector2 idleScale = Vector2.one;
        [SerializeField] private Vector2 bounceSquashScale = new(1.12f, 0.88f);
        [SerializeField] private Vector2 jumpCrouchScale = new(1.38f, 0.62f);
        [SerializeField] private Vector2 jumpStretchScale = new(0.72f, 1.42f);
        [SerializeField] private Vector2 landingScale = new(1.28f, 0.72f);

        // Fraction of a jump at which the boss touches the ground and the landing damage resolves.
        private const float LandingContactProgress = 0.82f;

        private BossController boss;
        private Rigidbody2D body;
        private Health health;
        private Transform visualTransform;
        private Vector3 groundedVisualPosition;
        private float visualHeight;
        private Collider2D[] jumpColliders;
        private bool[] originalTriggerStates;
        private bool jumping;
        private int jumpPoseIndex = -1;
        private int jumpCycleFrameIndex = -1;
        public int JumpCycleFrameIndex => jumpCycleFrameIndex;
        private Transform target;
        private Rect arenaBounds;
        private Rect footArena;
        private bool footArenaResolved;
        private uint randomState;
        private float lastTickTime;
        private float activeEndsAt;
        private int jumpCount;
        private int currentJump;
        // Sound-2: the jump whose whistle is sounding, and the voice to cut at
        // its landing.
        private int whistledJump = -1;
        private int jumpWhistleVoice;
        // Sound-3: the chewing while the heal runs.
        private int eatVoice;
        private int resolvedJumpCount;
        private float jumpSequenceStartTime;
        private Vector2 jumpStart;
        private Vector2 jumpDestination;
        private int healUses;
        private int appliedHealPulses;
        private int healPoseIndex = -1;
        private float healReturnEndsAt;
        private GameObject landingTelegraph;
        private readonly List<BossSlimePuddle> slimePuddles = new();
        private readonly List<BossSlimeBlob> slimeBlobs = new();
        private float slimeScatterStartedAt;
        private float actionStartedAt;
        private readonly List<SaemaeumGoldPillar> pillars = new();
        private SaemaeumGoldRain goldRain;
        private float meteorReadyAt;
        private int firedApproachVolleys;
        private float nextApproachThrowAt;

        public int ApproachVolleys => Mathf.Max(0, approachVolleys);
        public int FiredApproachVolleys => firedApproachVolleys;
        public float ApproachThrowInterval => approachThrowInterval;
        public int ApproachProjectilesPerVolley => Mathf.Max(1, approachProjectilesPerVolley);
        public float ApproachSpreadDegrees => Mathf.Clamp(approachSpreadDegrees, 0f, 180f);
        public float ApproachProjectileSpeed => approachProjectileSpeed;
        public bool MeteorLeadsPlayer => meteorLeadsPlayer;
        // Set by the gold cage; only the jump sequence may follow until it starts.
        private bool jumpQueued;
        private SaemaeumPillarWaveShape? forcedWaveShape;

        public bool JumpQueued => jumpQueued;
        public int CagePillars => Mathf.Max(3, cagePillars);
        public float CageRadius => cageRadius;
        public float CageWarning => cageWarning;
        public float CageLifetime => cageLifetime;
        public float WaveSpacing => waveSpacing;
        public float WaveSpeed => waveSpeed;
        public float WaveWarning => waveWarning;
        public float WaveLifetime => waveLifetime;
        public float WaveGridOffset => waveGridOffset;
        public float WaveGridHalfLength => waveGridHalfLength;
        public float WaveFanDegrees => waveFanDegrees;
        public SaemaeumPillarWaveShape LastWaveShape { get; private set; }

        // Verification and development tools: the next wave uses this shape instead of a random one.
        public void ForceNextWaveShape(SaemaeumPillarWaveShape shape) => forcedWaveShape = shape;

        public int HealUses => healUses;
        public float HealPerUse => healPerUse;
        public int PlannedJumpCount => jumpCount;
        public int CurrentJump => currentJump;
        public bool IsJumping => jumping;
        public int JumpPoseIndex => jumpPoseIndex;
        public int HealPoseIndex => healPoseIndex;
        public int PhaseOneMinimumJumps => Mathf.Max(1, phaseOneMinimumJumps);
        public int PhaseOneMaximumJumps => Mathf.Max(PhaseOneMinimumJumps, phaseOneMaximumJumps);
        public int PhaseTwoMinimumJumps => Mathf.Max(1, phaseTwoMinimumJumps);
        public int PhaseTwoMaximumJumps => Mathf.Max(PhaseTwoMinimumJumps, phaseTwoMaximumJumps);
        public float LandingRadius => landingRadius;
        public int LandingBurstCountThisPhase =>
            Mathf.Max(0, IsPhaseTwo ? phaseTwoLandingBurstCount : phaseOneLandingBurstCount);
        public float LandingBurstSpeedThisPhase => IsPhaseTwo ? phaseTwoLandingBurstSpeed : phaseOneLandingBurstSpeed;
        public EnemyDamageTier LandingBurstDamageTier => landingBurstDamageTier;
        public float LandingBurstSpawnRadius => landingRadius + landingBurstSpawnMargin;
        public int PillarCountThisPhase => Mathf.Max(0, IsPhaseTwo ? phaseTwoPillars : phaseOnePillars);
        public float PillarWarningThisPhase => IsPhaseTwo ? phaseTwoPillarWarning : phaseOnePillarWarning;
        public float PillarStagger => pillarStagger;
        public float PillarHitRadius => pillarHitRadius;
        public EnemyDamageTier PillarDamageTier => pillarDamageTier;
        public int PillarRequiredHits => Mathf.Max(1, pillarRequiredHits);
        public int PillarTargetEvery => Mathf.Max(1, pillarTargetEvery);
        public IReadOnlyList<SaemaeumGoldPillar> Pillars => pillars;
        public SaemaeumGoldRain GoldRain => goldRain;
        public float MeteorMinimumDuration => meteorMinimumDuration;
        public float MeteorMaximumDuration => Mathf.Max(meteorMinimumDuration, meteorMaximumDuration);
        public int MeteorOpeningCount => Mathf.Max(0, meteorOpeningCount);
        public float MeteorOpeningInterval => meteorOpeningInterval;
        public float MeteorInterval => meteorInterval;
        public int MeteorTargetEvery => Mathf.Max(1, meteorTargetEvery);
        public float MeteorWarning => meteorWarning;
        public float MeteorRadius => meteorRadius;
        public EnemyDamageTier MeteorDamageTier => meteorDamageTier;
        public float MeteorCooldownAfterRain => meteorCooldownAfterRain;
        public float MeteorReadyAt => meteorReadyAt;
        public float JumpDistance => jumpDistance;
        public float PhaseOneJumpDuration => phaseOneJumpDuration;
        public float PhaseTwoJumpDuration => phaseTwoJumpDuration;
        public float PhaseOneJumpRest => phaseOneJumpRest;
        public float PhaseTwoJumpRest => phaseTwoJumpRest;
        public float PreJumpCrouchDuration => preJumpCrouchDuration;
        public EnemyDamageTier LandingDamageTier => landingDamageTier;
        public float LandingKnockbackSpeed => landingKnockbackSpeed;
        public float ApproachSpeed => approachSpeed;
        public float CurrentApproachSpeed => approachSpeed *
            (boss != null && boss.CurrentPhase >= 2 ? phaseTwoSpeedMultiplier : 1f);
        public int LargeSlimeCountThisPhase => Mathf.Max(0, IsPhaseTwo ? phaseTwoLargeSlimes : phaseOneLargeSlimes);
        public int SmallSlimeCountThisPhase => Mathf.Max(0, IsPhaseTwo ? phaseTwoSmallSlimes : phaseOneSmallSlimes);
        public int SlimeCountThisPhase => Mathf.Max(1, LargeSlimeCountThisPhase + SmallSlimeCountThisPhase);
        public float SlimePuddleRadius => slimePuddleRadius;
        public float SmallSlimePuddleRadius => smallSlimePuddleRadius;
        public float SlimeSpeedMultiplier => slimeSpeedMultiplier;
        public int SlimeRequiredHits => Mathf.Max(1, slimeRequiredHits);
        public int MaximumSlimePuddles => Mathf.Max(1, maximumSlimePuddles);
        public float SlimeFlightDuration => slimeFlightDuration;
        public float SlimeLaunchStagger => slimeLaunchStagger;
        public IReadOnlyList<BossSlimePuddle> SlimePuddles => slimePuddles;
        public IReadOnlyList<BossSlimeBlob> SlimeBlobs => slimeBlobs;
        private bool IsPhaseTwo => boss != null && boss.CurrentPhase >= 2;
        public bool HealIsAvailable => boss != null && boss.CurrentPhase == 1 &&
                                       health != null && !health.IsDead && health.MissingHealth > 0f;
        public Vector3 CurrentVisualScale => visualTransform != null ? visualTransform.localScale : Vector3.one;

        private void Awake()
        {
            boss = GetComponent<BossController>();
            body = GetComponent<Rigidbody2D>();
            // Apply the physical contract to existing scene instances as well as rebuilt prefabs.
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            ContactDamage contact = GetComponent<ContactDamage>();
            if (contact == null) contact = gameObject.AddComponent<ContactDamage>();
            // One heart on floors 1-2: max health rarely grows, so a heavier bump punished too hard.
            contact.Configure(EnemyDamageTier.Light, 0.8f);
            health = GetComponent<Health>();
            visualTransform = GetComponentInChildren<SpriteRenderer>()?.transform;
            if (visualTransform != null) groundedVisualPosition = visualTransform.localPosition;
        }

        public void ConfigureMovement(float speed, float phaseTwoMultiplier)
        {
            approachSpeed = Mathf.Max(0f, speed);
            phaseTwoSpeedMultiplier = Mathf.Max(1f, phaseTwoMultiplier);
        }

        public void ConfigureLandingBurst(int firstCount, int secondCount, float firstSpeed, float secondSpeed,
            EnemyDamageTier damageTier, float spawnMargin)
        {
            phaseOneLandingBurstCount = Mathf.Max(0, firstCount);
            phaseTwoLandingBurstCount = Mathf.Max(0, secondCount);
            phaseOneLandingBurstSpeed = Mathf.Max(0.1f, firstSpeed);
            phaseTwoLandingBurstSpeed = Mathf.Max(0.1f, secondSpeed);
            landingBurstDamageTier = damageTier;
            landingBurstSpawnMargin = Mathf.Max(0f, spawnMargin);
        }

        public void ConfigurePillars(int firstCount, int secondCount, float firstWarning, float secondWarning,
            float stagger, float hitRadius, EnemyDamageTier damageTier, float knockbackSpeed, float knockbackDuration,
            int requiredHits)
        {
            phaseOnePillars = Mathf.Max(0, firstCount);
            phaseTwoPillars = Mathf.Max(0, secondCount);
            phaseOnePillarWarning = Mathf.Max(0f, firstWarning);
            phaseTwoPillarWarning = Mathf.Max(0f, secondWarning);
            pillarStagger = Mathf.Max(0f, stagger);
            pillarHitRadius = Mathf.Max(0.05f, hitRadius);
            pillarDamageTier = damageTier;
            pillarKnockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            pillarKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            pillarRequiredHits = Mathf.Max(1, requiredHits);
        }

        public void ConfigureMeteors(float minimumDuration, float maximumDuration, int openingCount,
            float openingInterval, float interval, int targetEvery, float warning, float radius,
            EnemyDamageTier damageTier, float knockbackSpeed, float knockbackDuration, float cooldownAfterRain)
        {
            meteorMinimumDuration = Mathf.Max(0.1f, minimumDuration);
            meteorMaximumDuration = Mathf.Max(meteorMinimumDuration, maximumDuration);
            meteorOpeningCount = Mathf.Max(0, openingCount);
            meteorOpeningInterval = Mathf.Max(0.01f, openingInterval);
            meteorInterval = Mathf.Max(0.01f, interval);
            meteorTargetEvery = Mathf.Max(1, targetEvery);
            meteorWarning = Mathf.Max(0.01f, warning);
            meteorRadius = Mathf.Max(0.05f, radius);
            meteorDamageTier = damageTier;
            meteorKnockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            meteorKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            meteorCooldownAfterRain = Mathf.Max(0f, cooldownAfterRain);
        }

        public void ConfigureJumps(int firstMin, int firstMax, int secondMin, int secondMax,
            float distance, float radius, EnemyDamageTier damageTier, float knockbackSpeed, float knockbackDuration,
            float firstJumpDuration = 0.76f, float secondJumpDuration = 0.72f,
            float firstJumpRest = 0.20f, float secondJumpRest = 0.15f)
        {
            phaseOneMinimumJumps = Mathf.Max(1, firstMin);
            phaseOneMaximumJumps = Mathf.Max(phaseOneMinimumJumps, firstMax);
            phaseTwoMinimumJumps = Mathf.Max(1, secondMin);
            phaseTwoMaximumJumps = Mathf.Max(phaseTwoMinimumJumps, secondMax);
            phaseOneJumpDuration = Mathf.Max(0.1f, firstJumpDuration);
            phaseTwoJumpDuration = Mathf.Max(0.1f, secondJumpDuration);
            phaseOneJumpRest = Mathf.Max(0f, firstJumpRest);
            phaseTwoJumpRest = Mathf.Max(0f, secondJumpRest);
            jumpDistance = Mathf.Max(0.1f, distance);
            landingRadius = Mathf.Max(0.1f, radius);
            landingDamageTier = damageTier;
            landingKnockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            landingKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        }

        public void ConfigureSlime(int firstLarge, int firstSmall, int secondLarge, int secondSmall,
            float largeRadius, float smallRadius, float speedMultiplier, int requiredHits, int maximumPuddles)
        {
            phaseOneLargeSlimes = Mathf.Max(0, firstLarge);
            phaseOneSmallSlimes = Mathf.Max(0, firstSmall);
            phaseTwoLargeSlimes = Mathf.Max(0, secondLarge);
            phaseTwoSmallSlimes = Mathf.Max(0, secondSmall);
            slimePuddleRadius = Mathf.Max(0.1f, largeRadius);
            smallSlimePuddleRadius = Mathf.Max(0.1f, smallRadius);
            slimeSpeedMultiplier = Mathf.Clamp01(speedMultiplier);
            slimeRequiredHits = Mathf.Max(1, requiredHits);
            maximumSlimePuddles = Mathf.Max(1, maximumPuddles);
        }

        public void ConfigureHealing(float amountPerUse, int pulseCount)
        {
            healPerUse = Mathf.Max(0.01f, amountPerUse);
            healPulses = Mathf.Max(1, pulseCount);
        }

        public void BeginCombat(Transform configuredTarget, int seed, float now)
        {
            ResolveComponents();
            target = configuredTarget;
            randomState = SeedToState(seed);
            lastTickTime = now;
            healUses = 0;
            jumpQueued = false;
            meteorReadyAt = now;
            goldRain = null;
            pillars.Clear();
            appliedHealPulses = 0;
            healPoseIndex = -1;
            // Solid colliders first, so the feet are measured at their real size.
            EndJump();
            ResolveArenaBounds();
            footArena = EnemyFeet.StandingRootArea(gameObject, arenaBounds);
            footArenaResolved = true;
            ResetPresentation();
        }

        public bool CanSelect(BossPatternExecution execution) => jumpQueued
            ? execution == BossPatternExecution.SaemaeumJumpSequence
            : execution switch
        {
            BossPatternExecution.SaemaeumGoldCage => IsPhaseTwo,
            BossPatternExecution.SaemaeumTreasureHeal => HealIsAvailable,
            BossPatternExecution.SaemaeumGoldMeteor => IsPhaseTwo && lastTickTime >= meteorReadyAt &&
                                                      (goldRain == null || goldRain.IsFinished),
            _ => true,
        };

        public void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt)
        {
            activeEndsAt = stateEndsAt;
            // 2페이즈 예고 (2026-11-20): the vault stops, glares and roars, and takes no damage until phase two
            // starts. Blocked hits show "0" through Health.AnyDamageBlocked. BossMovementAnimator draws the poses.
            if (state == BossActionState.PhaseTransition)
            {
                StopEating();
                healPoseIndex = -1;
                EndJump();
                ClearLandingTelegraph();
                StopMovement();
                ResetPresentation();
                if (health != null) health.SetInvulnerable(true);
                return;
            }
            if (health != null) health.SetInvulnerable(false);
            if (state != BossActionState.Active || execution != BossPatternExecution.SaemaeumTreasureHeal) StopEating();
            healPoseIndex = -1;
            if (execution == BossPatternExecution.SaemaeumTreasureHeal && boss != null && boss.CurrentPhase == 1)
            {
                if (state == BossActionState.Telegraph) healPoseIndex = 0;
                else if (state == BossActionState.Active) healPoseIndex = 1;
                else if (state == BossActionState.Recovery)
                {
                    healPoseIndex = 3;
                    healReturnEndsAt = lastTickTime + 0.18f;
                }
            }
            if (state != BossActionState.Active || execution != BossPatternExecution.SaemaeumJumpSequence)
                EndJump();
            if (state == BossActionState.Telegraph && execution == BossPatternExecution.SaemaeumJumpSequence)
            {
                StopMovement();
                ApplyScale(jumpCrouchScale);
                jumpPoseIndex = 1;
                jumpCycleFrameIndex = 1;
                return;
            }
            if (state == BossActionState.Telegraph && execution is BossPatternExecution.SaemaeumSlimeScatter
                    or BossPatternExecution.SaemaeumGoldPillars or BossPatternExecution.SaemaeumGoldMeteor
                    or BossPatternExecution.SaemaeumGoldCage or BossPatternExecution.SaemaeumPillarWave)
            {
                // Gathers itself low before spitting.
                StopMovement();
                jumpCycleFrameIndex = 1;
                return;
            }
            if (state == BossActionState.Telegraph && execution == BossPatternExecution.SaemaeumTreasureHeal)
            {
                StopMovement();
                ApplyScale(bounceSquashScale);
                return;
            }
            if (state != BossActionState.Active)
            {
                StopMovement();
                ClearLandingTelegraph();
                if (state != BossActionState.Telegraph) ResetPresentation();
            }
        }

        public bool TryExecute(BossPatternExecution execution)
        {
            switch (execution)
            {
                case BossPatternExecution.SaemaeumApproachWalk:
                    firedApproachVolleys = 0;
                    nextApproachThrowAt = lastTickTime;
                    FireApproachVolleysDue(lastTickTime);
                    return true;
                case BossPatternExecution.SaemaeumJumpSequence:
                    StartJumpSequence();
                    return true;
                case BossPatternExecution.SaemaeumSlimeScatter:
                    ScatterSlime();
                    return true;
                case BossPatternExecution.SaemaeumGoldPillars:
                    SummonPillars();
                    return true;
                case BossPatternExecution.SaemaeumGoldMeteor:
                    StartGoldRain();
                    return true;
                case BossPatternExecution.SaemaeumGoldCage:
                    SummonCage();
                    return true;
                case BossPatternExecution.SaemaeumPillarWave:
                    SummonPillarWave();
                    return true;
                case BossPatternExecution.SaemaeumTreasureHeal:
                    if (!HealIsAvailable) return true;
                    healUses++;
                    appliedHealPulses = 0;
                    StopEating();
                    eatVoice = CombatSfx.PlayVoice(CombatSfxCue.VaultEat);
                    return true;
                default:
                    return false;
            }
        }

        public void TickPattern(BossActionState state, BossPatternExecution execution, float now)
        {
            float deltaTime = Mathf.Clamp(now - lastTickTime, 0f, 0.1f);
            lastTickTime = now;
            if (state != BossActionState.Active)
            {
                if (state != BossActionState.Telegraph &&
                    (state != BossActionState.Recovery || now >= healReturnEndsAt)) healPoseIndex = -1;
                if (state != BossActionState.Telegraph) ResetPresentation();
                return;
            }

            switch (execution)
            {
                case BossPatternExecution.SaemaeumApproachWalk:
                    TickApproachWalk(now, deltaTime);
                    break;
                case BossPatternExecution.SaemaeumJumpSequence:
                    TickJumpSequence(now);
                    break;
                case BossPatternExecution.SaemaeumSlimeScatter:
                    TickSlimeScatter(now);
                    break;
                case BossPatternExecution.SaemaeumGoldPillars:
                case BossPatternExecution.SaemaeumGoldCage:
                case BossPatternExecution.SaemaeumPillarWave:
                    StopMovement();
                    // Slams the floor as the marks appear, then stands up again.
                    jumpCycleFrameIndex = now - actionStartedAt < 0.2f ? 7 : -1;
                    break;
                case BossPatternExecution.SaemaeumGoldMeteor:
                    StopMovement();
                    // Reaches up while it calls the gold down.
                    jumpCycleFrameIndex = 2;
                    break;
                case BossPatternExecution.SaemaeumTreasureHeal:
                    TickHealing(now);
                    break;
            }
        }

        private void TickApproachWalk(float now, float deltaTime)
        {
            if (target == null) return;
            Vector2 current = transform.position;
            Vector2 offset = (Vector2)target.position - current;
            Vector2 direction = offset.magnitude > BossTargetSpacing.ResolveStopDistance(gameObject, target)
                ? offset.normalized
                : Vector2.zero;
            Rect movementArea = ShrinkArena();
            if ((current.x <= movementArea.xMin && direction.x < 0f) ||
                (current.x >= movementArea.xMax && direction.x > 0f)) direction.x = 0f;
            if ((current.y <= movementArea.yMin && direction.y < 0f) ||
                (current.y >= movementArea.yMax && direction.y > 0f)) direction.y = 0f;
            float approachSpeed = CurrentApproachSpeed * EnemyStatusEffects.MoveSpeedMultiplierOf(this);
            Vector2 next = ClampToArena(current + direction.normalized * (approachSpeed * deltaTime));
            if (body != null) body.linearVelocity = direction.normalized * approachSpeed;
            else transform.position = next;
            float bounce = 0.5f + 0.5f * Mathf.Sin(now * 9f);
            ApplyScale(GetComponent<BossMovementAnimator>() != null
                ? idleScale : Vector2.Lerp(idleScale, bounceSquashScale, bounce));
            FireApproachVolleysDue(now);
        }

        private float CurrentApproachThrowInterval =>
            approachThrowInterval * (boss != null ? boss.CurrentTempoMultiplier : 1f);

        // The first volley leaves as the walk starts, then one every interval until the volleys run out.
        private void FireApproachVolleysDue(float now)
        {
            int safety = 0;
            while (firedApproachVolleys < ApproachVolleys && now >= nextApproachThrowAt && safety++ < 16)
            {
                FireApproachVolley();
                firedApproachVolleys++;
                nextApproachThrowAt += Mathf.Max(0.05f, CurrentApproachThrowInterval);
            }
        }

        // A narrow fan centered on the player.
        private void FireApproachVolley()
        {
            if (target == null || boss == null) return;
            Vector2 direction = (Vector2)target.position - (Vector2)transform.position;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            int count = ApproachProjectilesPerVolley;
            float spread = count > 1 ? ApproachSpreadDegrees : 0f;
            float step = count > 1 ? spread / (count - 1) : 0f;
            Sprite sprite = GetComponentInChildren<SpriteRenderer>()?.sprite;
            Sprite ruby = Resources.Load<Sprite>("CombatEffects/saemaeum-ruby-projectile");
            Sprite[] artwork = ruby != null ? new[] { ruby } : null;
            for (int index = 0; index < count; index++)
            {
                Vector2 shot = Quaternion.Euler(0f, 0f, -spread * 0.5f + step * index) * direction;
                BossProjectile.Create(BossProjectile.FrontLaunchPoint(gameObject, shot),
                    shot * approachProjectileSpeed, gameObject, boss.ProjectileDamageTier, sprite, artwork);
            }
        }

        // Random spots anywhere in the arena with no cap on how many stand, so an unlucky throw can wall the player
        // in. Pillars may touch but never overlap, old ones included (2026-11-19: two aimed pillars used to rise on
        // the same spot). The first aimed pillar rises where the player stands; a later aimed one rises right next
        // to the player. A random spot under the boss's own body is skipped, since the boss would break it.
        private void SummonPillars()
        {
            StopMovement();
            actionStartedAt = lastTickTime;
            pillars.RemoveAll(item => item == null);
            Rect area = ShrinkArenaBy(SaemaeumGoldPillar.VisualWidth * 0.5f);
            float bodyReach = (EnemyFeet.TryGetBody(gameObject, out CircleCollider2D bodyCircle)
                ? EnemyFeet.WorldRadiusOf(bodyCircle) : 0.8f) + SaemaeumGoldPillar.BodyRadius;
            var taken = new List<Vector2>();
            foreach (SaemaeumGoldPillar standing in pillars)
                if (standing != null && (standing.Obstacle == null || !standing.Obstacle.IsBroken))
                    taken.Add(standing.transform.position);
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            int sortingLayer = source != null ? source.sortingLayerID : 0;
            Vector2 player = target != null ? ClampToArea(ResolveTargetGround(), area) : Vector2.zero;
            int aimedSoFar = 0;
            for (int index = 0; index < PillarCountThisPhase; index++)
            {
                bool aimed = target != null && index % PillarTargetEvery == 0;
                Vector2 position = aimed
                    ? PickAimedPillarSpot(player, aimedSoFar++, area, taken)
                    : PickRandomPillarSpot(area, taken, bodyReach);
                taken.Add(position);
                float emergeAt = lastTickTime + PillarWarningThisPhase + index * pillarStagger;
                SaemaeumGoldPillar pillar = SaemaeumGoldPillar.Create(position, lastTickTime, emergeAt,
                    pillarHitRadius, pillarDamageTier, pillarKnockbackSpeed, pillarKnockbackDuration,
                    PillarRequiredHits, gameObject, target, sortingLayer, aimed);
                pillars.Add(pillar);
                boss?.RegisterOwnedObject(pillar.gameObject);
            }
        }

        // Centers this far apart leave two pillar bodies just touching.
        public const float PillarSpacing = SaemaeumGoldPillar.BodyRadius * 2f;

        private static bool OverlapsPillar(Vector2 candidate, List<Vector2> taken)
        {
            float spacing = PillarSpacing - 0.001f;
            foreach (Vector2 other in taken)
                if ((other - candidate).sqrMagnitude < spacing * spacing) return true;
            return false;
        }

        // Under the player first; a later aimed pillar goes on a ring just touching the spot under the player.
        private Vector2 PickAimedPillarSpot(Vector2 player, int aimedIndex, Rect area, List<Vector2> taken)
        {
            if (aimedIndex == 0 && !OverlapsPillar(player, taken)) return player;
            Vector2 fallback = player;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = NextRandom01() * Mathf.PI * 2f;
                float distance = PillarSpacing * (1f + attempt / 24f);
                Vector2 candidate = ClampToArea(player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                    area);
                if (attempt == 0) fallback = candidate;
                if (!OverlapsPillar(candidate, taken)) return candidate;
            }
            return fallback;
        }

        private Vector2 PickRandomPillarSpot(Rect area, List<Vector2> taken, float bodyReach)
        {
            Vector2 candidate = RandomPointIn(area);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                bool underBoss = (candidate - (Vector2)transform.position).sqrMagnitude < bodyReach * bodyReach;
                if (!underBoss && !OverlapsPillar(candidate, taken)) return candidate;
                candidate = RandomPointIn(area);
            }
            // A full arena: the last try still rises, even if it touches the boss or another pillar.
            return candidate;
        }

        private void SummonCage()
        {
            StopMovement();
            actionStartedAt = lastTickTime;
            jumpQueued = true;
            if (target == null) return;
            pillars.RemoveAll(item => item == null);
            List<Vector2> taken = StandingPillarSpots();
            float bodyReach = BossBodyReach();
            int sortingLayer = PillarSortingLayer();
            Vector2 center = ResolveTargetGround();
            int count = CagePillars;
            for (int index = 0; index < count; index++)
            {
                float angle = Mathf.PI * 2f * index / count;
                Vector2 spot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cageRadius;
                if (!arenaBounds.Contains(spot) || OverlapsPillar(spot, taken) ||
                    (spot - (Vector2)transform.position).sqrMagnitude < bodyReach * bodyReach) continue;
                taken.Add(spot);
                SpawnPillar(spot, lastTickTime, lastTickTime + cageWarning + index * cageStagger, sortingLayer,
                    cageLifetime);
            }
        }

        // Rows sweep outward at waveSpeed; each pillar's mark shows when the row reaches it and it bursts one
        // warning later. Spots off the standing area, on another pillar or under the boss are skipped.
        private void SummonPillarWave()
        {
            StopMovement();
            actionStartedAt = lastTickTime;
            SaemaeumPillarWaveShape shape = forcedWaveShape ?? (SaemaeumPillarWaveShape)NextRandom(3);
            forcedWaveShape = null;
            LastWaveShape = shape;
            pillars.RemoveAll(item => item == null);
            Vector2 origin = transform.position;
            Vector2 player = target != null ? ResolveTargetGround() : origin + Vector2.down * 3f;
            Vector2 toward = player - origin;
            toward = toward.sqrMagnitude > 0.001f ? toward.normalized : Vector2.down;
            float bodyReach = BossBodyReach();
            float reach = arenaBounds.width + arenaBounds.height;
            var rows = new List<(Vector2 Start, Vector2 Direction, float Length)>();
            switch (shape)
            {
                case SaemaeumPillarWaveShape.Line:
                    rows.Add((origin + toward * bodyReach, toward, reach));
                    break;
                case SaemaeumPillarWaveShape.Fan:
                    foreach (float turn in new[] { -waveFanDegrees, 0f, waveFanDegrees })
                    {
                        Vector2 direction = Quaternion.Euler(0f, 0f, turn) * toward;
                        rows.Add((origin + direction * bodyReach, direction, reach));
                    }
                    break;
                default:
                    // Two rows each way around the player; each sweeps from one end to the other.
                    float half = waveGridHalfLength;
                    foreach (float side in new[] { -waveGridOffset, waveGridOffset })
                    {
                        rows.Add((player + new Vector2(-half, side), Vector2.right, half * 2f));
                        rows.Add((player + new Vector2(side, half), Vector2.down, half * 2f));
                    }
                    break;
            }

            List<Vector2> taken = StandingPillarSpots();
            int sortingLayer = PillarSortingLayer();
            foreach (var row in rows)
            {
                for (float distance = 0f; distance <= row.Length + 0.001f; distance += waveSpacing)
                {
                    Vector2 spot = row.Start + row.Direction * distance;
                    if (!arenaBounds.Contains(spot) || OverlapsPillar(spot, taken) ||
                        (spot - origin).sqrMagnitude < bodyReach * bodyReach) continue;
                    taken.Add(spot);
                    float shownAt = lastTickTime + distance / waveSpeed;
                    SpawnPillar(spot, shownAt, shownAt + waveWarning, sortingLayer, waveLifetime);
                }
            }
        }

        private List<Vector2> StandingPillarSpots()
        {
            var taken = new List<Vector2>();
            foreach (SaemaeumGoldPillar standing in pillars)
                if (standing != null && (standing.Obstacle == null || !standing.Obstacle.IsBroken))
                    taken.Add(standing.transform.position);
            return taken;
        }

        private float BossBodyReach() => (EnemyFeet.TryGetBody(gameObject, out CircleCollider2D bodyCircle)
            ? EnemyFeet.WorldRadiusOf(bodyCircle) : 0.8f) + SaemaeumGoldPillar.BodyRadius;

        private int PillarSortingLayer()
        {
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            return source != null ? source.sortingLayerID : 0;
        }

        private void SpawnPillar(Vector2 position, float shownAt, float emergeAt, int sortingLayer, float lifetime)
        {
            SaemaeumGoldPillar pillar = SaemaeumGoldPillar.Create(position, shownAt, emergeAt, pillarHitRadius,
                pillarDamageTier, pillarKnockbackSpeed, pillarKnockbackDuration, PillarRequiredHits, gameObject,
                target, sortingLayer, false, lifetime);
            pillars.Add(pillar);
            boss?.RegisterOwnedObject(pillar.gameObject);
        }

        private void StartGoldRain()
        {
            StopMovement();
            actionStartedAt = lastTickTime;
            float duration = Mathf.Lerp(meteorMinimumDuration, MeteorMaximumDuration, NextRandom01());
            var settings = new SaemaeumGoldRain.Settings(duration, MeteorOpeningCount, meteorOpeningInterval,
                meteorInterval, MeteorTargetEvery, meteorWarning, meteorRadius, meteorDamageTier,
                meteorKnockbackSpeed, meteorKnockbackDuration, meteorLeadsPlayer);
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            goldRain = SaemaeumGoldRain.Begin(boss, target, ShrinkArenaBy(meteorRadius * 0.5f), lastTickTime,
                (uint)NextRandom(int.MaxValue) | 1u, settings, source != null ? source.sortingLayerID : 0,
                source != null ? source.sortingOrder : 0);
            // The last shadow appears before the rain's end; its lump lands one warning later.
            meteorReadyAt = lastTickTime + duration + meteorWarning + meteorCooldownAfterRain;
        }

        private Vector2 RandomPointIn(Rect area) => new(Mathf.Lerp(area.xMin, area.xMax, NextRandom01()),
            Mathf.Lerp(area.yMin, area.yMax, NextRandom01()));

        private Rect ShrinkArenaBy(float padding)
        {
            float horizontal = Mathf.Min(Mathf.Max(0f, padding), arenaBounds.width * 0.49f);
            float vertical = Mathf.Min(Mathf.Max(0f, padding), arenaBounds.height * 0.49f);
            return Rect.MinMaxRect(arenaBounds.xMin + horizontal, arenaBounds.yMin + vertical,
                arenaBounds.xMax - horizontal, arenaBounds.yMax - vertical);
        }

        private void ScatterSlime()
        {
            StopMovement();
            slimeScatterStartedAt = lastTickTime;
            slimeBlobs.RemoveAll(item => item == null);
            Vector2 center = target != null ? ResolveTargetGround() : (Vector2)transform.position + Vector2.down * 2f;
            List<float> radii = SlimeRadiiThisPhase();
            List<Vector2> landings = PickSlimeLandings(center, radii);
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            int sortingLayer = source != null ? source.sortingLayerID : 0;
            int sortingOrder = source != null ? source.sortingOrder + 1 : 1;
            Sprite projectile = Resources.Load<Sprite>("CombatEffects/saemaeum-jelly-projectile");
            for (int index = 0; index < landings.Count; index++)
            {
                float radius = radii[index];
                BossSlimeBlob blob = BossSlimeBlob.Launch(transform.position, landings[index],
                    lastTickTime + index * slimeLaunchStagger, slimeFlightDuration, slimeArcHeight, radius,
                    projectile, sortingLayer, sortingOrder,
                    landing => SpawnSlimePuddle(landing, radius, sortingLayer));
                slimeBlobs.Add(blob);
                boss?.RegisterOwnedObject(blob.gameObject);
                if (blob.Mark != null) boss?.RegisterOwnedObject(blob.Mark);
            }
        }

        private void TickSlimeScatter(float now)
        {
            StopMovement();
            // Rises out of the crouch as the lumps leave, then settles back to its walking pose.
            float elapsed = now - slimeScatterStartedAt;
            float spitting = (SlimeCountThisPhase - 1) * slimeLaunchStagger + 0.2f;
            jumpCycleFrameIndex = elapsed < spitting ? 2 : elapsed < spitting + 0.12f ? 7 : -1;
        }

        private Vector2 ResolveTargetGround()
        {
            PlayerMovement player = target.GetComponentInParent<PlayerMovement>();
            return player != null ? player.FeetPosition : (Vector2)target.position;
        }

        // Large puddles first, then small ones, in throw order.
        public List<float> SlimeRadiiThisPhase()
        {
            var radii = new List<float>();
            for (int index = 0; index < LargeSlimeCountThisPhase; index++) radii.Add(slimePuddleRadius);
            for (int index = 0; index < SmallSlimeCountThisPhase; index++) radii.Add(smallSlimePuddleRadius);
            if (radii.Count == 0) radii.Add(slimePuddleRadius);
            return radii;
        }

        // The first lump lands where the player stands; the others spread around them without overlapping.
        private List<Vector2> PickSlimeLandings(Vector2 center, IReadOnlyList<float> radii)
        {
            Rect area = BodyArena();
            int count = radii.Count;
            var landings = new List<Vector2> { ClampToArea(center, area) };
            const int attemptsPerLanding = 24;
            for (int attempt = 0; attempt < count * attemptsPerLanding && landings.Count < count; attempt++)
            {
                float angle = NextRandom01() * Mathf.PI * 2f;
                float distance = Mathf.Lerp(slimeScatterMinimumDistance, slimeScatterMaximumDistance, NextRandom01());
                Vector2 candidate = ClampToArea(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                    area);
                float radius = radii[landings.Count];
                bool crowded = false;
                for (int other = 0; other < landings.Count && !crowded; other++)
                {
                    float spacing = Mathf.Max(slimeMinimumSpacing, radius + radii[other]);
                    crowded = (landings[other] - candidate).sqrMagnitude < spacing * spacing;
                }
                if (crowded) continue;
                landings.Add(candidate);
            }
            // A cramped corner may not fit them all apart; the rest still land, closer together.
            while (landings.Count < count)
            {
                float angle = NextRandom01() * Mathf.PI * 2f;
                landings.Add(ClampToArea(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    slimeScatterMaximumDistance, area));
            }
            return landings;
        }

        private void SpawnSlimePuddle(Vector2 position, float radius, int sortingLayer)
        {
            slimePuddles.RemoveAll(item => item == null || item.IsPopped);
            while (slimePuddles.Count >= MaximumSlimePuddles)
            {
                BossSlimePuddle oldest = slimePuddles[0];
                slimePuddles.RemoveAt(0);
                oldest.Pop();
            }
            BossSlimePuddle puddle = BossSlimePuddle.Create(position, radius, slimeSpeedMultiplier,
                SlimeRequiredHits, target, sortingLayer);
            slimePuddles.Add(puddle);
            boss?.RegisterOwnedObject(puddle.gameObject);
        }

        private static Vector2 ClampToArea(Vector2 position, Rect area) =>
            new(Mathf.Clamp(position.x, area.xMin, area.xMax), Mathf.Clamp(position.y, area.yMin, area.yMax));

        private float NextRandom01() => NextRandom(10000) / 10000f;

        private void StartJumpSequence()
        {
            jumpQueued = false;
            EndJump();
            StopMovement();
            jumpColliders = GetComponentsInChildren<Collider2D>();
            originalTriggerStates = new bool[jumpColliders.Length];
            for (int index = 0; index < jumpColliders.Length; index++)
            {
                originalTriggerStates[index] = jumpColliders[index].isTrigger;
                jumpColliders[index].isTrigger = true;
            }
            jumping = true;
            jumpPoseIndex = 1;
            jumpCycleFrameIndex = 1;
            ApplyScale(jumpCrouchScale);
            SetVisualHeight(0f);
            int minimum = boss != null && boss.CurrentPhase >= 2 ? phaseTwoMinimumJumps : phaseOneMinimumJumps;
            int maximum = boss != null && boss.CurrentPhase >= 2 ? phaseTwoMaximumJumps : phaseOneMaximumJumps;
            jumpCount = minimum + NextRandom(maximum - minimum + 1);
            currentJump = 0;
            resolvedJumpCount = 0;
            whistledJump = -1;
            jumpSequenceStartTime = lastTickTime;
            activeEndsAt = jumpSequenceStartTime + JumpSequenceDuration;
            boss?.SynchronizeActiveEndTime(activeEndsAt);
            jumpStart = transform.position;
            LockNextJumpDestination();
        }

        private void TickJumpSequence(float now)
        {
            if (!jumping || jumpCount <= 0) return;
            float jumpDuration = CurrentJumpDuration;
            float cycle = jumpDuration + CurrentJumpRest;
            float elapsed = Mathf.Max(0f, now - jumpSequenceStartTime);
            bool sequenceFinished = elapsed >= JumpSequenceDuration;
            int jumpIndex = Mathf.Min(jumpCount - 1, Mathf.FloorToInt(elapsed / cycle));
            while (jumpIndex > currentJump)
            {
                if (resolvedJumpCount <= currentJump)
                {
                    ResolveLanding(jumpDestination, resolvedJumpCount);
                    resolvedJumpCount++;
                }
                ClearLandingTelegraph();
                currentJump++;
                jumpStart = jumpDestination;
                LockNextJumpDestination();
            }
            // Progress within the jump itself; it stays at 1 while the boss rests before the next jump.
            float jumpProgress = Mathf.Clamp01((elapsed - jumpIndex * cycle) / jumpDuration);
            // Crouch on the floor, fly, then compress and recover on the floor.
            const float takeoff = 0.16f, contact = LandingContactProgress, compression = 0.90f;
            bool settlesBeforeNextJump = currentJump == jumpCount - 1 || CurrentJumpRest > 0f;
            jumpPoseIndex = jumpProgress < takeoff ? 1 : jumpProgress < contact ? 2 :
                jumpProgress < 0.96f ? 3 : settlesBeforeNextJump ? 0 : 1;
            jumpCycleFrameIndex = jumpProgress < 0.04f ? 0 : jumpProgress < takeoff ? 1 :
                jumpProgress < 0.36f ? 2 : jumpProgress < 0.58f ? 3 : jumpProgress < contact ? 4 :
                jumpProgress < 0.90f ? 5 : jumpProgress < 0.96f ? 6 : 7;
            float flight = Mathf.Clamp01((jumpProgress - takeoff) / (contact - takeoff));
            // The whistle starts as the vault leaves the floor and runs until it lands.
            if (jumpProgress >= takeoff && jumpProgress < contact && whistledJump < currentJump)
            {
                whistledJump = currentJump;
                CombatSfx.StopVoice(jumpWhistleVoice);
                jumpWhistleVoice = CombatSfx.PlayVoice(CombatSfxCue.VaultJumpWhistle);
            }
            Vector2 position = Vector2.Lerp(jumpStart, jumpDestination, JumpTravelFraction(flight));
            if (body != null) body.position = position;
            else transform.position = position;
            SetVisualHeight(jumpHeight * JumpHeightFraction(flight));
            if (jumpProgress < takeoff)
                ApplyScale(Vector2.Lerp(jumpCrouchScale, jumpStretchScale,
                    Mathf.SmoothStep(0f, 1f, jumpProgress / takeoff)));
            else if (jumpProgress < contact)
                ApplyScale(Vector2.Lerp(jumpStretchScale, idleScale,
                    Mathf.SmoothStep(0f, 1f, (flight - 0.6f) / 0.4f)));
            else if (jumpProgress < compression)
                ApplyScale(Vector2.Lerp(idleScale, landingScale,
                    Mathf.SmoothStep(0f, 1f, (jumpProgress - contact) / (compression - contact))));
            else
                ApplyScale(Vector2.Lerp(landingScale, settlesBeforeNextJump ? idleScale : jumpCrouchScale,
                    Mathf.SmoothStep(0f, 1f, (jumpProgress - compression) / (1f - compression))));
            // Winds up for the next jump at the end of the rest.
            float restElapsed = elapsed - jumpIndex * cycle - jumpDuration;
            float crouch = Mathf.Min(preJumpCrouchDuration, CurrentJumpRest);
            if (currentJump < jumpCount - 1 && crouch > 0f && restElapsed >= CurrentJumpRest - crouch)
            {
                jumpPoseIndex = 1;
                jumpCycleFrameIndex = 1;
                ApplyScale(jumpCrouchScale);
            }
            if (jumpProgress >= contact && resolvedJumpCount <= currentJump)
            {
                ResolveLanding(jumpDestination, resolvedJumpCount);
                resolvedJumpCount++;
                ClearLandingTelegraph();
            }
            if (sequenceFinished)
            {
                EndJump();
            }
        }

        // Share of the flight spent rising. The shorter fall makes the descent faster than the takeoff.
        private const float JumpApexFlight = 0.6f;

        // Height as a fraction of the jump height over the flight (0..1): a fast launch that slows into
        // the apex, then a fall that keeps accelerating until the ground hit.
        public static float JumpHeightFraction(float flight)
        {
            flight = Mathf.Clamp01(flight);
            if (flight <= JumpApexFlight)
            {
                float rise = 1f - flight / JumpApexFlight;
                return 1f - rise * rise;
            }
            float fall = (flight - JumpApexFlight) / (1f - JumpApexFlight);
            return 1f - fall * fall;
        }

        // Ground travel over the flight: launches at full speed and only eases slightly before touchdown,
        // so the boss keeps its momentum into the landing instead of floating down onto the spot.
        public static float JumpTravelFraction(float flight)
        {
            flight = Mathf.Clamp01(flight);
            float easeOut = 1f - (1f - flight) * (1f - flight);
            return Mathf.Lerp(flight, easeOut, 0.35f);
        }

        private float CurrentJumpDuration =>
            boss != null && boss.CurrentPhase >= 2 ? phaseTwoJumpDuration : phaseOneJumpDuration;

        private float CurrentJumpRest => boss != null && boss.CurrentPhase >= 2 ? phaseTwoJumpRest : phaseOneJumpRest;

        // The final landing ends the sequence, so there is no rest after it.
        private float JumpSequenceDuration =>
            Mathf.Max(0.01f, jumpCount * CurrentJumpDuration + Mathf.Max(0, jumpCount - 1) * CurrentJumpRest);

        private void LockNextJumpDestination()
        {
            Vector2 targetPosition = target != null ? (Vector2)target.position : jumpStart + Vector2.down * jumpDistance;
            Vector2 offset = targetPosition - jumpStart;
            jumpDestination = ClampToArena(jumpStart + Vector2.ClampMagnitude(offset, jumpDistance));
            CreateLandingTelegraph();
        }

        private void ResolveLanding(Vector2 impactPosition, int landingIndex)
        {
            CombatSfx.StopVoice(jumpWhistleVoice, 0.05f);
            jumpWhistleVoice = 0;
            CombatSfx.Play(CombatSfxCue.VaultJumpLanding);
            GetComponent<BossMovementAnimator>()?.NotifyLanding();
            PlayLandingShockwave(impactPosition);
            BossSummonedObstacle.BreakInCircle(impactPosition, landingRadius);
            FireLandingBurst(impactPosition, landingIndex);
            if (landingLeavesSlime) SpawnLandingSlime(impactPosition + GroundShadowOffset());
            if (target == null || ((Vector2)target.position - impactPosition).sqrMagnitude >
                landingRadius * landingRadius) return;
            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead) return;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            targetHealth.TakeDamage(HealthUnits.CreateEnemyDamageContext(
                gameObject, DamageSourceType.EnemyContact, landingDamageTier));

            KnockbackReceiver receiver = targetHealth.GetComponent<KnockbackReceiver>();
            if (receiver == null || targetHealth.IsDead) return;
            Vector2 direction = (Vector2)target.position - impactPosition;
            if (direction.sqrMagnitude <= 0.001f)
                direction = impactPosition - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector2.up;
            receiver.Apply(direction, landingKnockbackSpeed, landingKnockbackDuration, 0f);
        }

        public bool LandingLeavesSlime => landingLeavesSlime;

        // Small puddles sit on one ring just inside the landing radius, as many as fit touching each other.
        public float LandingSlimeRingRadius =>
            Mathf.Max(slimePuddleRadius + smallSlimePuddleRadius, landingRadius - smallSlimePuddleRadius);
        public int LandingSmallSlimeCount => Mathf.Max(3,
            Mathf.FloorToInt(2f * Mathf.PI * LandingSlimeRingRadius / (smallSlimePuddleRadius * 2f)));

        private void SpawnLandingSlime(Vector2 center)
        {
            int sortingLayer = PillarSortingLayer();
            SpawnSlimePuddle(center, slimePuddleRadius, sortingLayer);
            int count = LandingSmallSlimeCount;
            float ring = LandingSlimeRingRadius;
            for (int index = 0; index < count; index++)
            {
                float angle = Mathf.PI * 2f * index / count;
                Vector2 spot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                if (arenaBounds.Contains(spot)) SpawnSlimePuddle(spot, smallSlimePuddleRadius, sortingLayer);
            }
        }

        public SaemaeumLandingShockwave LastShockwave { get; private set; }

        // Spreads from under the landed body's ground shadow, where the landing mark was drawn.
        private void PlayLandingShockwave(Vector2 impactPosition)
        {
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            LastShockwave = SaemaeumLandingShockwave.Create(impactPosition + GroundShadowOffset(), landingRadius,
                source != null ? source.sortingLayerID : 0);
            boss?.RegisterOwnedObject(LastShockwave.gameObject);
        }

        private void TickHealing(float now)
        {
            StopMovement();
            if (boss != null && boss.CurrentPhase >= 2)
            {
                healPoseIndex = -1;
                return;
            }
            float activeStart = GetHealActiveStartedAt();
            healPoseIndex = 1 + (Mathf.FloorToInt(Mathf.Max(0f, now - activeStart) * 8f) % 2);
            float duration = Mathf.Max(0.01f, activeEndsAt - activeStart);
            int expectedPulses = Mathf.Min(healPulses,
                Mathf.FloorToInt(Mathf.Clamp01((now - activeStart) / duration) * healPulses) + 1);
            while (appliedHealPulses < expectedPulses)
            {
                health?.Heal(healPerUse / healPulses);
                appliedHealPulses++;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 12f);
            ApplyScale(Vector2.Lerp(bounceSquashScale, idleScale, pulse));
        }

        private float GetHealActiveStartedAt() => activeEndsAt - 1.8f *
            (boss != null ? boss.CurrentTempoMultiplier : 1f);

        // Even directions around the landing; odd landings turn the ring by half a gap. A shot that would start
        // outside the room is skipped instead of flying in through the wall.
        public static float LandingBurstAngleOffset(int landingIndex, int count) =>
            count > 0 && landingIndex % 2 == 1 ? 180f / count : 0f;

        private void FireLandingBurst(Vector2 impactPosition, int landingIndex)
        {
            int count = LandingBurstCountThisPhase;
            if (boss == null || count <= 0) return;
            Sprite sprite = GetComponentInChildren<SpriteRenderer>()?.sprite;
            Sprite ruby = Resources.Load<Sprite>("CombatEffects/saemaeum-ruby-projectile");
            Sprite[] artwork = ruby != null ? new[] { ruby } : null;
            float offset = LandingBurstAngleOffset(landingIndex, count);
            for (int index = 0; index < count; index++)
            {
                float angle = (offset + 360f * index / count) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = impactPosition + direction * LandingBurstSpawnRadius;
                if (!arenaBounds.Contains(start)) continue;
                BossProjectile.Create(start, direction * LandingBurstSpeedThisPhase, gameObject,
                    landingBurstDamageTier, sprite, artwork);
            }
        }

        private void ResolveArenaBounds()
        {
            ResolveWallArena();
            // Inside the walls a hedge strip about one unit wide is still out of reach: the room's movement bounds are
            // where actors stand. Jumps, pillars and meteors stay there, so a landing near a wall or corner never
            // aims into the hedge (2026-11-19: the landing shadow sat apart from the boss there).
            RoomNode room = GetComponentInParent<RoomNode>();
            if (room == null || room.Profile == null || room.Profile.MovementBounds.width <= 0f ||
                room.Profile.MovementBounds.height <= 0f) return;
            Rect local = room.Profile.MovementBounds;
            Vector2 a = room.transform.TransformPoint(local.min), b = room.transform.TransformPoint(local.max);
            Rect standing = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            Rect overlap = Rect.MinMaxRect(Mathf.Max(arenaBounds.xMin, standing.xMin),
                Mathf.Max(arenaBounds.yMin, standing.yMin), Mathf.Min(arenaBounds.xMax, standing.xMax),
                Mathf.Min(arenaBounds.yMax, standing.yMax));
            if (overlap.width > 0f && overlap.height > 0f) arenaBounds = overlap;
        }

        private void ResolveWallArena()
        {
            RoomNode room = GetComponentInParent<RoomNode>();
            ItemTestRoomController testRoom = GetComponentInParent<ItemTestRoomController>();
            Transform arenaRoot = room != null ? room.transform : testRoom != null ? testRoom.transform : null;
            if (arenaRoot != null && TryResolveWallInterior(arenaRoot, out arenaBounds)) return;
            if (room != null && room.Profile != null)
            {
                // EncounterBounds is a spawn area, not the physical room boundary.
                Rect local = new Rect(-room.Profile.InteriorSize * 0.5f, room.Profile.InteriorSize);
                Vector2 minimum = room.transform.TransformPoint(local.min);
                Vector2 maximum = room.transform.TransformPoint(local.max);
                arenaBounds = Rect.MinMaxRect(Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                    Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
                return;
            }
            arenaBounds = new Rect((Vector2)transform.position - fallbackHalfExtents, fallbackHalfExtents * 2f);
        }

        private static bool TryResolveWallInterior(Transform root, out Rect interior)
        {
            float left = float.NegativeInfinity, right = float.PositiveInfinity;
            float bottom = float.NegativeInfinity, top = float.PositiveInfinity;
            foreach (BoxCollider2D wall in root.GetComponentsInChildren<BoxCollider2D>())
            {
                if (!wall.enabled || wall.isTrigger || !wall.name.Contains("Wall")) continue;
                Bounds bounds = wall.bounds;
                if (wall.name.StartsWith("Left")) left = Mathf.Max(left, bounds.max.x);
                if (wall.name.StartsWith("Right")) right = Mathf.Min(right, bounds.min.x);
                if (wall.name.StartsWith("Bottom")) bottom = Mathf.Max(bottom, bounds.max.y);
                if (wall.name.StartsWith("Top")) top = Mathf.Min(top, bounds.min.y);
            }
            interior = Rect.MinMaxRect(left, bottom, right, top);
            return !float.IsInfinity(left) && !float.IsInfinity(right) &&
                   !float.IsInfinity(bottom) && !float.IsInfinity(top) && left < right && bottom < top;
        }

        private Vector2 ClampToArena(Vector2 position)
        {
            Rect area = ShrinkArena();
            return new Vector2(Mathf.Clamp(position.x, area.xMin, area.xMax),
                Mathf.Clamp(position.y, area.yMin, area.yMax));
        }

        // Where the root may walk or land: the feet stay on the standing floor (Boss-Pattern-0), so the boss nears
        // walls like other enemies instead of keeping its whole body circle inside.
        // Measured once per fight while the colliders are solid: a jump turns them into triggers, and the body lookup
        // skips triggers, which would otherwise leave the feet at size zero mid-jump.
        private Rect ShrinkArena() => footArenaResolved ? footArena : EnemyFeet.StandingRootArea(gameObject, arenaBounds);

        // The whole body circle inside the arena, for slime: the lumps start and land around the body.
        private Rect BodyArena()
        {
            Collider2D collider = GetComponent<Collider2D>();
            Vector2 extents = collider != null ? (Vector2)collider.bounds.extents : Vector2.one * arenaPadding;
            float horizontal = Mathf.Min(extents.x + 0.02f, arenaBounds.width * 0.49f);
            float vertical = Mathf.Min(extents.y + 0.02f, arenaBounds.height * 0.49f);
            return Rect.MinMaxRect(arenaBounds.xMin + horizontal, arenaBounds.yMin + vertical,
                arenaBounds.xMax - horizontal, arenaBounds.yMax - vertical);
        }

        private void CreateLandingTelegraph()
        {
            ClearLandingTelegraph();
            landingTelegraph = new GameObject("Saemaeum Jump Landing Telegraph");
            // Drawn where the boss's own ground shadow will be after landing, not at its root (the body's center).
            landingTelegraph.transform.position = jumpDestination + GroundShadowOffset();
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            SpriteRenderer renderer = landingTelegraph.AddComponent<SpriteRenderer>();
            Sprite[] shadow = CombatEffectArtwork.Frames("landing-shadow");
            renderer.sprite = shadow.Length > 0 ? shadow[0] : null;
            renderer.color = new Color(1f, 1f, 1f, 0.85f);
            renderer.sortingLayerID = source != null ? source.sortingLayerID : 0;
            renderer.sortingOrder = source != null ? source.sortingOrder - 1 : -1;
            float shadowWidth = source != null ? Mathf.Max(1f, source.bounds.size.x * 1.4f) : 3f;
            CombatEffectArtwork.Size(renderer, shadowWidth, shadowWidth / 3f);
            boss?.RegisterOwnedObject(landingTelegraph);
        }

        // From the root to the boss's ground shadow. The drawn shadow follows the root while the body flies.
        public Vector2 GroundShadowOffset()
        {
            TrickalFanGame.Frontend.GroundShadow shadow = GetComponent<TrickalFanGame.Frontend.GroundShadow>();
            if (shadow != null && shadow.Visual != null && shadow.Visual.enabled)
                return shadow.Visual.transform.position - transform.position;
            // Without a drawn shadow yet (Edit Mode), the same rule: the bottom of the collision footprint.
            if (GetComponent<Collider2D>() is CircleCollider2D circle)
            {
                Vector3 scale = transform.lossyScale;
                float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                return new Vector2(circle.offset.x * scale.x,
                    circle.offset.y * scale.y - radius + Mathf.Min(0.08f, radius * 0.2f));
            }
            return Vector2.zero;
        }

        public Vector2 LandingTelegraphPosition => landingTelegraph != null
            ? (Vector2)landingTelegraph.transform.position
            : jumpDestination + GroundShadowOffset();

        private void ClearLandingTelegraph()
        {
            if (landingTelegraph == null) return;
            if (Application.isPlaying) Destroy(landingTelegraph);
            else DestroyImmediate(landingTelegraph);
            landingTelegraph = null;
        }

        private void StopMovement()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void ResetPresentation()
        {
            ApplyScale(idleScale);
            SetVisualHeight(0f);
        }

        private void SetVisualHeight(float height)
        {
            visualHeight = height;
            if (visualTransform == null || visualTransform == transform) return;
            visualTransform.localPosition = groundedVisualPosition;
            BossMovementAnimator animator = GetComponent<BossMovementAnimator>();
            Sprite sprite = animator != null && animator.BodySprite != null ? animator.BodySprite
                : visualTransform.GetComponent<SpriteRenderer>()?.sprite;
            if (sprite != null)
                visualTransform.localPosition += Vector3.up * sprite.bounds.min.y * (idleScale.y - visualTransform.localScale.y);
            visualTransform.position += Vector3.up * height;
        }

        private void EndJump()
        {
            // A jump cut short (the boss dying, the pattern ending) takes its whistle with it.
            CombatSfx.StopVoice(jumpWhistleVoice);
            jumpWhistleVoice = 0;
            jumpPoseIndex = -1;
            jumpCycleFrameIndex = -1;
            if (jumping)
            {
                for (int index = 0; index < jumpColliders.Length; index++)
                    if (jumpColliders[index] != null)
                        jumpColliders[index].isTrigger = originalTriggerStates[index];
                jumping = false;
                StopMovement();
            }
            SetVisualHeight(0f);
        }

        private void StopEating()
        {
            CombatSfx.StopVoice(eatVoice);
            eatVoice = 0;
        }

        private void OnDisable()
        {
            if (health != null) health.SetInvulnerable(false);
            StopEating();
            healPoseIndex = -1;
            EndJump();
            ClearLandingTelegraph();
            ResetPresentation();
        }

        private void ApplyScale(Vector2 scale)
        {
            if (visualTransform == null) visualTransform = GetComponentInChildren<SpriteRenderer>()?.transform;
            if (visualTransform != null)
            {
                // Temporarily disabled for comparing the artwork at its original proportions.
                // visualTransform.localScale = new Vector3(scale.x, scale.y, 1f);
                visualTransform.localScale = new Vector3(idleScale.x, idleScale.y, 1f);
                SetVisualHeight(visualHeight);
            }
        }

        private void ResolveComponents()
        {
            if (boss == null) boss = GetComponent<BossController>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (health == null) health = GetComponent<Health>();
            if (visualTransform == null) visualTransform = GetComponentInChildren<SpriteRenderer>()?.transform;
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
            uint state = unchecked((uint)seed) ^ 0x5AE4A117u;
            return state == 0u ? 0x85EBCA6Bu : state;
        }
    }
}

namespace TrickalFanGame.Enemy
{
    public enum SaemaeumPillarWaveShape
    {
        Line,
        Grid,
        Fan,
    }
}
