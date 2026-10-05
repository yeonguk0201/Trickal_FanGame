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
        [SerializeField, Min(0.1f)] private float phaseOneJumpDuration = 0.69f;
        [SerializeField, Min(0.1f)] private float phaseTwoJumpDuration = 0.62f;
        [SerializeField, Min(0.1f)] private float jumpDistance = 4.8f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2f;
        [SerializeField, Min(0.1f)] private float landingRadius = 2.875f;
        [SerializeField] private EnemyDamageTier landingDamageTier = EnemyDamageTier.Medium;
        [SerializeField, Min(0f)] private float landingKnockbackSpeed = 7f;
        [SerializeField, Min(0.01f)] private float landingKnockbackDuration = 0.2f;

        [Header("Treasure heal")]
        [SerializeField, Min(0.01f)] private float healPerUse = 150f;
        [SerializeField, Min(1)] private int healPulses = 4;

        [Header("Arena and presentation")]
        [SerializeField, Min(0f)] private float arenaPadding = 0.8f;
        [SerializeField] private Vector2 fallbackHalfExtents = new(6.2f, 5.0f);
        [SerializeField] private Vector2 idleScale = Vector2.one;
        [SerializeField] private Vector2 bounceSquashScale = new(1.12f, 0.88f);
        [SerializeField] private Vector2 jumpCrouchScale = new(1.38f, 0.62f);
        [SerializeField] private Vector2 jumpStretchScale = new(0.72f, 1.42f);
        [SerializeField] private Vector2 landingScale = new(1.28f, 0.72f);

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
        public int PhaseTwoMinimumJumps => Mathf.Max(1, phaseTwoMinimumJumps);
        public float JumpDistance => jumpDistance;
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
            contact.Configure(EnemyDamageTier.Heavy, 0.8f);
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
            float firstJumpDuration = 0.69f, float secondJumpDuration = 0.62f)
        {
            phaseOneMinimumJumps = Mathf.Max(1, firstMin);
            phaseOneMaximumJumps = Mathf.Max(phaseOneMinimumJumps, firstMax);
            phaseTwoMinimumJumps = Mathf.Max(1, secondMin);
            phaseTwoMaximumJumps = Mathf.Max(phaseTwoMinimumJumps, secondMax);
            phaseOneJumpDuration = Mathf.Max(0.1f, firstJumpDuration);
            phaseTwoJumpDuration = Mathf.Max(0.1f, secondJumpDuration);
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
            ResolveArenaBounds();
            ResetPresentation();
        }

        public bool CanSelect(BossPatternExecution execution) =>
            execution != BossPatternExecution.SaemaeumTreasureHeal || HealIsAvailable;

        public void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt)
        {
            activeEndsAt = stateEndsAt;
            if (state != BossActionState.Active || execution != BossPatternExecution.SaemaeumJumpSequence)
                EndJump();
            if (state == BossActionState.Telegraph && execution == BossPatternExecution.SaemaeumJumpSequence)
            {
                StopMovement();
                ApplyScale(jumpCrouchScale);
                jumpPoseIndex = 1;
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
            Vector2 next = ClampToArena(current + direction.normalized * (CurrentApproachSpeed * deltaTime));
            if (body != null) body.linearVelocity = direction.normalized * CurrentApproachSpeed;
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
            ApplyScale(jumpCrouchScale);
            SetVisualHeight(0f);
            int minimum = boss != null && boss.CurrentPhase >= 2 ? phaseTwoMinimumJumps : phaseOneMinimumJumps;
            int maximum = boss != null && boss.CurrentPhase >= 2 ? phaseTwoMaximumJumps : phaseOneMaximumJumps;
            jumpCount = minimum + NextRandom(maximum - minimum + 1);
            currentJump = 0;
            resolvedJumpCount = 0;
            jumpSequenceStartTime = lastTickTime;
            float jumpDuration = boss != null && boss.CurrentPhase >= 2
                ? phaseTwoJumpDuration
                : phaseOneJumpDuration;
            activeEndsAt = jumpSequenceStartTime + jumpCount * jumpDuration;
            boss?.SynchronizeActiveEndTime(activeEndsAt);
            jumpStart = transform.position;
            LockNextJumpDestination();
        }

        private void TickJumpSequence(float now)
        {
            if (!jumping || jumpCount <= 0) return;
            float jumpDuration = boss != null && boss.CurrentPhase >= 2 ? phaseTwoJumpDuration : phaseOneJumpDuration;
            float activeDuration = Mathf.Max(0.01f, jumpCount * jumpDuration);
            float elapsed = Mathf.Max(0f, now - jumpSequenceStartTime);
            float sequenceProgress = Mathf.Clamp01(elapsed / activeDuration);
            float scaled = sequenceProgress * jumpCount;
            int jumpIndex = Mathf.Min(jumpCount - 1, Mathf.FloorToInt(scaled));
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
            float jumpProgress = scaled - jumpIndex;
            // Crouch on the floor, fly, then compress and recover on the floor. Keep the total cycle duration.
            const float takeoff = 0.16f, contact = 0.82f, compression = 0.90f;
            jumpPoseIndex = jumpProgress < takeoff ? 1 : jumpProgress < contact ? 2 :
                jumpProgress < 0.96f ? 3 : currentJump == jumpCount - 1 ? 0 : 1;
            float flight = Mathf.Clamp01((jumpProgress - takeoff) / (contact - takeoff));
            Vector2 position = Vector2.Lerp(jumpStart, jumpDestination, Mathf.SmoothStep(0f, 1f, flight));
            if (body != null) body.position = position;
            else transform.position = position;
            float arc = Mathf.Sin(flight * Mathf.PI);
            SetVisualHeight(jumpHeight * arc * arc);
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
                ApplyScale(Vector2.Lerp(landingScale, currentJump == jumpCount - 1 ? idleScale : jumpCrouchScale,
                    Mathf.SmoothStep(0f, 1f, (jumpProgress - compression) / (1f - compression))));
            if (jumpProgress >= contact && resolvedJumpCount <= currentJump)
            {
                ResolveLanding(jumpDestination);
                resolvedJumpCount++;
                ClearLandingTelegraph();
            }
            if (sequenceProgress >= 1f)
            {
                EndJump();
            }
        }

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
            if (boss != null && boss.CurrentPhase >= 2) return;
            float activeStart = GetHealActiveStartedAt();
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
            landingTelegraph.transform.localScale = Vector3.one * (landingRadius * 1.35f);
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            SpriteRenderer renderer = landingTelegraph.AddComponent<SpriteRenderer>();
            renderer.sprite = source != null ? source.sprite : null;
            renderer.color = new Color(1f, 0.72f, 0.08f, 0.28f);
            renderer.sortingOrder = source != null ? source.sortingOrder - 1 : -1;
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
