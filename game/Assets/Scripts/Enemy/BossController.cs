using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum BossActionState
    {
        Idle,
        Telegraph,
        Active,
        Recovery,
        Cooldown,
        PhaseTransition,
        Defeated,
    }

    [RequireComponent(typeof(Health), typeof(KnockbackReceiver))]
    public sealed class BossController : MonoBehaviour, IKnockbackPushBlocker
    {
        [Header("HUD")]
        [SerializeField] private string displayName = "보스";
        [SerializeField, Min(1)] private int phaseCount = 1;

        [Header("Common pattern runtime")]
        [SerializeField] private BossPatternDefinition[] patterns = Array.Empty<BossPatternDefinition>();
        [SerializeField] private int encounterSeed = 1;
        [SerializeField, Min(0.1f)] private float attackInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 4f;
        [SerializeField] private EnemyDamageTier projectileDamageTier = EnemyDamageTier.Medium;

        [Header("Phase transition")]
        [SerializeField, Min(0f)] private float phaseTransitionDuration;
        [SerializeField, Range(0.1f, 1f)] private float phaseTwoTempoMultiplier = 1f;

        private readonly List<GameObject> ownedObjects = new();
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyAttackPresentation attackPresentation;
        private readonly List<IBossPatternRuntime> patternRuntimes = new();
        private Transform target;
        private float[] nextReusableTimes = Array.Empty<float>();
        private uint randomState;
        private float stateEndsAt;
        private int currentPatternIndex = -1;
        private int previousPatternIndex = -1;
        private int currentPhase = 1;
        private int pendingPhase;
        private bool combatStarted;

        public event Action Died;
        public event Action<int, int> PhaseChanged;
        public event Action<BossActionState, string> PatternStateChanged;

        public EnemyDamageTier ProjectileDamageTier => projectileDamageTier;
        public Health Health => health != null ? health : GetComponent<Health>();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "보스" : displayName.Trim();
        public int CurrentPhase => currentPhase;
        public int PhaseCount => Mathf.Max(1, phaseCount);
        public BossActionState State { get; private set; } = BossActionState.Idle;
        public float StateEndsAt => stateEndsAt;
        public string CurrentPatternId => CurrentPattern?.PatternId;
        public int EncounterSeed => encounterSeed;
        public float PhaseTransitionDuration => Mathf.Max(0f, phaseTransitionDuration);
        public float PhaseTwoTempoMultiplier => Mathf.Clamp(phaseTwoTempoMultiplier, 0.1f, 1f);
        public float CurrentTempoMultiplier => CurrentPhase >= 2 ? PhaseTwoTempoMultiplier : 1f;
        public int OwnedObjectCount
        {
            get
            {
                ownedObjects.RemoveAll(item => item == null);
                return ownedObjects.Count;
            }
        }
        public IReadOnlyList<BossPatternDefinition> Patterns => patterns;

        // A basic attack push does not move a boss while its pattern is running (Passive-0 §4.6).
        public bool BlocksKnockbackPush =>
            State == BossActionState.Active || State == BossActionState.PhaseTransition;

        public bool IsActionSuppressed =>
            health != null && (health.IsDead ||
                               (State != BossActionState.PhaseTransition && knockback != null && knockback.IsActive));

        private BossPatternDefinition CurrentPattern =>
            currentPatternIndex >= 0 && currentPatternIndex < patterns.Length
                ? patterns[currentPatternIndex]
                : null;

        public void SetProjectileDamageTier(EnemyDamageTier configuredProjectileDamageTier) =>
            projectileDamageTier = configuredProjectileDamageTier;

        public void ConfigureHud(string configuredDisplayName, int configuredPhaseCount)
        {
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName) ? "보스" : configuredDisplayName.Trim();
            phaseCount = Mathf.Max(1, configuredPhaseCount);
            SetPhase(Mathf.Min(currentPhase, phaseCount));
        }

        public void ConfigurePatterns(BossPatternDefinition[] configuredPatterns)
        {
            patterns = configuredPatterns ?? Array.Empty<BossPatternDefinition>();
            ResetPatternRuntime();
        }

        public void ConfigurePhaseTwo(float configuredTransitionDuration, float configuredTempoMultiplier)
        {
            phaseTransitionDuration = Mathf.Max(0f, configuredTransitionDuration);
            phaseTwoTempoMultiplier = Mathf.Clamp(configuredTempoMultiplier, 0.1f, 1f);
        }

        public void ConfigureEncounterSeed(int configuredSeed)
        {
            encounterSeed = configuredSeed;
            randomState = SeedToState(configuredSeed);
        }

        public void BeginCombat(Transform configuredTarget, int configuredSeed, float now)
        {
            if (health != null && health.IsDead) return;
            target = configuredTarget;
            ConfigureEncounterSeed(configuredSeed);
            combatStarted = true;
            nextReusableTimes = new float[patterns.Length];
            currentPatternIndex = -1;
            previousPatternIndex = -1;
            pendingPhase = 0;
            foreach (IBossPatternRuntime runtime in patternRuntimes)
                runtime.BeginCombat(configuredTarget, configuredSeed, now);
            StartNextPattern(now);
        }

        public void TickBehavior(float now)
        {
            if (!combatStarted || IsActionSuppressed || State == BossActionState.Defeated) return;

            foreach (IBossPatternRuntime runtime in patternRuntimes)
                runtime.TickPattern(State, CurrentPattern?.Execution ?? BossPatternExecution.SignalOnly, now);

            int safety = 0;
            while (now >= stateEndsAt && safety++ < 16)
            {
                float transitionTime = stateEndsAt;
                switch (State)
                {
                    case BossActionState.Telegraph:
                        SetState(BossActionState.Active, transitionTime + ScaleDuration(CurrentPattern.ActiveDuration));
                        ExecuteCurrentPattern();
                        break;
                    case BossActionState.Active:
                        SetState(BossActionState.Recovery,
                            transitionTime + ScaleDuration(CurrentPattern.RecoveryDuration));
                        break;
                    case BossActionState.Recovery:
                        nextReusableTimes[currentPatternIndex] =
                            transitionTime + ScaleDuration(CurrentPattern.ReuseCooldown);
                        previousPatternIndex = currentPatternIndex;
                        currentPatternIndex = -1;
                        StartNextPattern(transitionTime);
                        break;
                    case BossActionState.Cooldown:
                    case BossActionState.Idle:
                        StartNextPattern(transitionTime);
                        break;
                    case BossActionState.PhaseTransition:
                        CompletePhaseTransition(transitionTime);
                        break;
                    default:
                        return;
                }
            }
        }

        public void RegisterOwnedObject(GameObject ownedObject)
        {
            if (ownedObject != null && !ownedObjects.Contains(ownedObject)) ownedObjects.Add(ownedObject);
        }

        public void SynchronizeActiveEndTime(float endsAt)
        {
            if (State == BossActionState.Active) stateEndsAt = endsAt;
        }

        public void CleanupOwnedObjects()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                GameObject ownedObject = ownedObjects[index];
                if (ownedObject == null) continue;
                if (Application.isPlaying) Destroy(ownedObject);
                else DestroyImmediate(ownedObject);
            }
            ownedObjects.Clear();
        }

        public void CancelCombat()
        {
            combatStarted = false;
            target = null;
            currentPatternIndex = -1;
            pendingPhase = 0;
            SetState(BossActionState.Idle, 0f);
            CleanupOwnedObjects();
        }

        public bool SetPhase(int phase)
        {
            int nextPhase = Mathf.Clamp(phase, 1, PhaseCount);
            if (currentPhase == nextPhase) return false;
            currentPhase = nextPhase;
            PhaseChanged?.Invoke(CurrentPhase, PhaseCount);
            return true;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            attackPresentation = GetComponent<EnemyAttackPresentation>();
            patternRuntimes.Clear();
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                if (component is IBossPatternRuntime runtime) patternRuntimes.Add(runtime);
            currentPhase = Mathf.Clamp(currentPhase, 1, PhaseCount);
            health.Changed += OnHealthChanged;
            health.Died += OnDied;
            ResetPatternRuntime();
        }

        private void OnEnable()
        {
            TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);
            if (health != null && !health.IsDead && State == BossActionState.Defeated)
                SetState(BossActionState.Idle, 0f);
        }

        private void OnDisable()
        {
            if (State != BossActionState.Defeated) CancelCombat();
            else CleanupOwnedObjects();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Changed -= OnHealthChanged;
                health.Died -= OnDied;
            }
            CleanupOwnedObjects();
        }

        private void Update()
        {
            if (!combatStarted)
            {
                if (target == null) target = FindFirstObjectByType<PlayerMovement>()?.transform;
                if (target != null) BeginCombat(target, encounterSeed, Time.time);
            }
            TickBehavior(Time.time);
        }

        private void ResetPatternRuntime()
        {
            nextReusableTimes = new float[patterns.Length];
            randomState = SeedToState(encounterSeed);
            currentPatternIndex = -1;
            previousPatternIndex = -1;
            pendingPhase = 0;
            combatStarted = false;
            State = BossActionState.Idle;
        }

        private void StartNextPattern(float now)
        {
            if (patterns.Length == 0)
            {
                patterns = new[]
                {
                    new BossPatternDefinition("aimed-projectile", BossPatternExecution.AimedProjectile,
                        attackInterval * 0.4f, 0.1f, attackInterval * 0.6f, 0f),
                };
                nextReusableTimes = new float[patterns.Length];
            }

            List<int> candidates = new();
            float earliestReuse = float.PositiveInfinity;
            for (int index = 0; index < patterns.Length; index++)
            {
                if (patterns[index] == null) continue;
                bool selectable = true;
                foreach (IBossPatternRuntime runtime in patternRuntimes)
                    selectable &= runtime.CanSelect(patterns[index].Execution);
                if (!selectable) continue;
                earliestReuse = Mathf.Min(earliestReuse, nextReusableTimes[index]);
                if (now >= nextReusableTimes[index]) candidates.Add(index);
            }

            if (candidates.Count > 1 && candidates.Contains(previousPatternIndex))
                candidates.Remove(previousPatternIndex);

            if (candidates.Count == 0)
            {
                SetState(BossActionState.Cooldown,
                    float.IsPositiveInfinity(earliestReuse) ? now + 0.1f : Mathf.Max(now + 0.01f, earliestReuse));
                return;
            }

            currentPatternIndex = SelectWeightedPattern(candidates);
            SetState(BossActionState.Telegraph, now + CurrentPattern.GetTelegraphDuration(CurrentTempoMultiplier));
        }

        private int SelectWeightedPattern(IReadOnlyList<int> candidates)
        {
            int totalWeight = 0;
            int[] weights = new int[candidates.Count];
            for (int candidate = 0; candidate < candidates.Count; candidate++)
            {
                BossPatternExecution execution = patterns[candidates[candidate]].Execution;
                int weight = 1;
                foreach (IBossPatternRuntime runtime in patternRuntimes)
                {
                    if (runtime is not IBossPatternSelectionPolicy policy) continue;
                    weight = Mathf.Max(0, policy.GetSelectionWeight(execution));
                    break;
                }
                weights[candidate] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0) return candidates[NextRandom(candidates.Count)];
            int roll = NextRandom(totalWeight);
            for (int candidate = 0; candidate < candidates.Count; candidate++)
            {
                roll -= weights[candidate];
                if (roll < 0) return candidates[candidate];
            }
            return candidates[candidates.Count - 1];
        }

        private void ExecuteCurrentPattern()
        {
            if (CurrentPattern == null || target == null) return;
            foreach (IBossPatternRuntime runtime in patternRuntimes)
                if (runtime.TryExecute(CurrentPattern.Execution)) return;
            if (CurrentPattern.Execution != BossPatternExecution.AimedProjectile) return;
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            BossProjectile.Create(transform.position, direction * projectileSpeed, gameObject, projectileDamageTier,
                GetComponentInChildren<SpriteRenderer>()?.sprite);
        }

        private void SetState(BossActionState state, float endsAt)
        {
            State = state;
            stateEndsAt = endsAt;
            attackPresentation?.SetPhase(state switch
            {
                BossActionState.Telegraph => EnemyAttackPhase.Telegraph,
                BossActionState.Active => EnemyAttackPhase.Active,
                BossActionState.Recovery => EnemyAttackPhase.Recovery,
                BossActionState.PhaseTransition => EnemyAttackPhase.Telegraph,
                _ => EnemyAttackPhase.Idle,
            });
            PatternStateChanged?.Invoke(state, CurrentPatternId);
            foreach (IBossPatternRuntime runtime in patternRuntimes)
                runtime.OnPatternStateChanged(state,
                    CurrentPattern?.Execution ?? BossPatternExecution.SignalOnly, endsAt);
        }

        private void OnHealthChanged(float currentHealth, float maximumHealth)
        {
            if (maximumHealth <= 0f) return;
            if (currentHealth > 0f && State == BossActionState.Defeated)
                SetState(BossActionState.Idle, 0f);
            int phase = Mathf.Min(PhaseCount,
                Mathf.FloorToInt((1f - currentHealth / maximumHealth) * PhaseCount) + 1);
            if (phase > currentPhase && phase == 2 && PhaseTransitionDuration > 0f && combatStarted)
            {
                BeginPhaseTransition(phase, Time.time);
                return;
            }
            SetPhase(phase);
        }

        private void OnDied()
        {
            combatStarted = false;
            currentPatternIndex = -1;
            pendingPhase = 0;
            SetState(BossActionState.Defeated, 0f);
            CleanupOwnedObjects();
            Died?.Invoke();
        }

        private int NextRandom(int maximumExclusive)
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (int)(randomState % (uint)maximumExclusive);
        }

        private float ScaleDuration(float duration) => Mathf.Max(0.01f, duration * CurrentTempoMultiplier);

        private void BeginPhaseTransition(int phase, float now)
        {
            if (pendingPhase >= phase || State == BossActionState.PhaseTransition) return;
            pendingPhase = phase;
            currentPatternIndex = -1;
            knockback?.Stop();
            SetState(BossActionState.PhaseTransition, now + PhaseTransitionDuration);
        }

        private void CompletePhaseTransition(float now)
        {
            SetPhase(pendingPhase <= 0 ? 2 : pendingPhase);
            pendingPhase = 0;
            previousPatternIndex = -1;
            Array.Clear(nextReusableTimes, 0, nextReusableTimes.Length);
            StartNextPattern(now);
        }

        private static uint SeedToState(int seed)
        {
            uint state = unchecked((uint)seed) ^ 0x9E3779B9u;
            return state == 0u ? 0xA341316Cu : state;
        }
    }
}
