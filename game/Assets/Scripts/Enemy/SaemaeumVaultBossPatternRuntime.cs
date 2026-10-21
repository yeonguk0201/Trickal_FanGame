using TrickalFanGame.Combat;
using TrickalFanGame.Debugging;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(BossController), typeof(Rigidbody2D), typeof(Health))]
    public sealed class SaemaeumVaultBossPatternRuntime : MonoBehaviour, IBossPatternRuntime
    {
        [Header("Approach throw")]
        [SerializeField, Min(0f)] private float approachSpeed = 1.8f;
        [SerializeField, Min(1f)] private float phaseTwoSpeedMultiplier = 1.35f;
        [SerializeField, Min(0.05f)] private float throwInterval = 0.55f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 5.5f;
        [SerializeField, Min(1)] private int projectilesPerVolley = 5;
        [SerializeField, Min(1)] private int volleysPerPattern = 5;
        [SerializeField, Range(0f, 180f)] private float fanSpreadDegrees = 48f;

        [Header("Jump sequence")]
        [SerializeField, Min(1)] private int phaseOneMinimumJumps = 3;
        [SerializeField, Min(1)] private int phaseOneMaximumJumps = 5;
        [SerializeField, Min(1)] private int phaseTwoMinimumJumps = 4;
        [SerializeField, Min(1)] private int phaseTwoMaximumJumps = 6;
        [SerializeField, Min(0.1f)] private float phaseOneJumpDuration = 0.76f;
        [SerializeField, Min(0.1f)] private float phaseTwoJumpDuration = 0.72f;
        // Grounded pause after each landing except the last. The next destination locks when the pause ends.
        [SerializeField, Min(0f)] private float phaseOneJumpRest = 0.20f;
        [SerializeField, Min(0f)] private float phaseTwoJumpRest = 0.15f;
        [SerializeField, Min(0.1f)] private float jumpDistance = 5.5f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2f;
        [SerializeField, Min(0.1f)] private float landingRadius = 2.875f;
        [SerializeField] private EnemyDamageTier landingDamageTier = EnemyDamageTier.Medium;
        [SerializeField, Min(0f)] private float landingKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float landingKnockbackDuration = 0.2f;

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
        private uint randomState;
        private float lastTickTime;
        private float nextThrowTime;
        private float activeEndsAt;
        private int firedVolleys;
        private int jumpCount;
        private int currentJump;
        private int resolvedJumpCount;
        private float jumpSequenceStartTime;
        private Vector2 jumpStart;
        private Vector2 jumpDestination;
        private int healUses;
        private int appliedHealPulses;
        private int healPoseIndex = -1;
        private float healReturnEndsAt;
        private GameObject landingTelegraph;

        public int HealUses => healUses;
        public float HealPerUse => healPerUse;
        public int ProjectilesPerVolley => Mathf.Max(1, projectilesPerVolley);
        public int VolleysPerPattern => Mathf.Max(1, volleysPerPattern);
        public float FanSpreadDegrees => Mathf.Clamp(fanSpreadDegrees, 0f, 180f);
        public int PlannedJumpCount => jumpCount;
        public int CurrentJump => currentJump;
        public bool IsJumping => jumping;
        public int JumpPoseIndex => jumpPoseIndex;
        public int HealPoseIndex => healPoseIndex;
        public int PhaseTwoMinimumJumps => Mathf.Max(1, phaseTwoMinimumJumps);
        public float JumpDistance => jumpDistance;
        public float PhaseOneJumpDuration => phaseOneJumpDuration;
        public float PhaseTwoJumpDuration => phaseTwoJumpDuration;
        public float PhaseOneJumpRest => phaseOneJumpRest;
        public float PhaseTwoJumpRest => phaseTwoJumpRest;
        public EnemyDamageTier LandingDamageTier => landingDamageTier;
        public float LandingKnockbackSpeed => landingKnockbackSpeed;
        public float ApproachSpeed => approachSpeed;
        public float CurrentApproachSpeed => approachSpeed *
            (boss != null && boss.CurrentPhase >= 2 ? phaseTwoSpeedMultiplier : 1f);
        public bool HealIsAvailable => boss != null && boss.CurrentPhase == 1;
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

        public void ConfigureMovement(float speed, float phaseTwoMultiplier, float configuredThrowInterval,
            float configuredProjectileSpeed)
        {
            approachSpeed = Mathf.Max(0f, speed);
            phaseTwoSpeedMultiplier = Mathf.Max(1f, phaseTwoMultiplier);
            throwInterval = Mathf.Max(0.05f, configuredThrowInterval);
            projectileSpeed = Mathf.Max(0.1f, configuredProjectileSpeed);
        }

        public void ConfigureVolley(int projectileCount, int volleyCount, float spreadDegrees)
        {
            projectilesPerVolley = Mathf.Max(1, projectileCount);
            volleysPerPattern = Mathf.Max(1, volleyCount);
            fanSpreadDegrees = Mathf.Clamp(spreadDegrees, 0f, 180f);
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
            nextThrowTime = now;
            firedVolleys = 0;
            healUses = 0;
            appliedHealPulses = 0;
            healPoseIndex = -1;
            ResolveArenaBounds();
            ResetPresentation();
        }

        public bool CanSelect(BossPatternExecution execution) =>
            execution != BossPatternExecution.SaemaeumTreasureHeal || HealIsAvailable;

        public void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt)
        {
            activeEndsAt = stateEndsAt;
            healPoseIndex = -1;
            if (execution == BossPatternExecution.SaemaeumTreasureHeal && HealIsAvailable)
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
                case BossPatternExecution.SaemaeumApproachThrow:
                    firedVolleys = 0;
                    FireTreasureVolley();
                    firedVolleys++;
                    nextThrowTime = lastTickTime + CurrentThrowInterval;
                    return true;
                case BossPatternExecution.SaemaeumJumpSequence:
                    StartJumpSequence();
                    return true;
                case BossPatternExecution.SaemaeumTreasureHeal:
                    if (!HealIsAvailable) return true;
                    healUses++;
                    appliedHealPulses = 0;
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
                case BossPatternExecution.SaemaeumApproachThrow:
                    TickApproachThrow(now, deltaTime);
                    break;
                case BossPatternExecution.SaemaeumJumpSequence:
                    TickJumpSequence(now);
                    break;
                case BossPatternExecution.SaemaeumTreasureHeal:
                    TickHealing(now);
                    break;
            }
        }

        private float CurrentThrowInterval => throwInterval * (boss != null ? boss.CurrentTempoMultiplier : 1f);

        private void TickApproachThrow(float now, float deltaTime)
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
            while (firedVolleys < VolleysPerPattern && now >= nextThrowTime)
            {
                FireTreasureVolley();
                firedVolleys++;
                nextThrowTime += CurrentThrowInterval;
            }
        }

        private void StartJumpSequence()
        {
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
                    ResolveLanding(jumpDestination);
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
            if (jumpProgress >= contact && resolvedJumpCount <= currentJump)
            {
                ResolveLanding(jumpDestination);
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

        private void ResolveLanding(Vector2 impactPosition)
        {
            GetComponent<BossMovementAnimator>()?.NotifyLanding();
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

        private void FireTreasureVolley()
        {
            if (target == null || boss == null) return;
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector2.down;
            float startAngle = -FanSpreadDegrees * 0.5f;
            float step = ProjectilesPerVolley > 1 ? FanSpreadDegrees / (ProjectilesPerVolley - 1) : 0f;
            Sprite sprite = GetComponentInChildren<SpriteRenderer>()?.sprite;
            for (int index = 0; index < ProjectilesPerVolley; index++)
            {
                Vector2 projectileDirection = Quaternion.Euler(0f, 0f, startAngle + step * index) * direction;
                BossProjectile.Create(transform.position, projectileDirection * projectileSpeed, gameObject,
                    boss.ProjectileDamageTier, sprite);
            }
        }

        private void ResolveArenaBounds()
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

        private Rect ShrinkArena()
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
            landingTelegraph.transform.position = jumpDestination;
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

        private void OnDisable()
        {
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
