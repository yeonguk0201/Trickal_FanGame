using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerWalkAnimator : MonoBehaviour
    {
        private const string ResourcePath = "Characters/Erpin_Walking";
        private const string SkillResourcePath = "Characters/Erpin_lowGrade_skill";
        private const int FrameCount = 4;
        private const int SkillFrameCount = 4;
        private const string HighGradeResourcePath = "Characters/Erpin_HighGrade";
        // Defeat-1 (user drawing, 2026-11-04): four falling poses played once on death, the last one held.
        private const string DefeatResourcePath = "Characters/Erpin_Death";
        public const int DefeatFrameCount = 4;
        public const float DefeatFrameSeconds = 0.15f;
        private readonly Sprite[] defeatFrames = new Sprite[DefeatFrameCount];
        private bool hasDefeatFrames;
        private float diedAt = float.NaN;
        // Victory (user drawing, 2026-11-19): a six-frame jump for joy where she stands when a boss dies, played
        // twice; the last pose then stays until she moves, attacks or uses a skill.
        private const string VictoryResourcePath = "Characters/Erpin_Victory";
        public const int VictoryFrameCount = 6;
        public const float VictoryFrameSeconds = 0.11f;
        public const int VictoryLoops = 2;
        private readonly Sprite[] victoryFrames = new Sprite[VictoryFrameCount];
        private bool hasVictoryFrames;
        private float victoryStartsAt = float.NaN;
        private readonly Sprite[] highGradeFrames = new Sprite[8];
        // Flight art (user drawing, 2026-11-20): with 시스트의 가짜 날개 she flies on patched bat wings. Moving shows
        // the four-frame wing beat per direction; standing still shows the standing drawing. Other poses (skill,
        // high-grade dash, victory, defeat) keep their own drawings.
        public const string FlightResourcePath = "Characters/Erpin_Flying";
        public const string FlightIdleName = "Erpin_FlyIdle";
        private readonly Sprite[] flyDownFrames = new Sprite[FrameCount];
        private readonly Sprite[] flySideFrames = new Sprite[FrameCount];
        private readonly Sprite[] flyUpFrames = new Sprite[FrameCount];
        private Sprite flyIdleSprite;
        private bool hasFlightFrames;
        private PlayerFlight flight;
        private PlayerActionState actionState;
        private PlayerUltimate ultimate;
        private Health health;
        private PlayerActionPhase artworkPhase;
        private float highGradeElapsed;
        private bool hasHighGradeFrames;

        [SerializeField, Min(1f)] private float framesPerSecond = 8f;
        [SerializeField, Min(1f)] private float skillFramesPerSecond = 12f;

        private readonly Sprite[] downFrames = new Sprite[FrameCount];
        private readonly Sprite[] sideFrames = new Sprite[FrameCount];
        private readonly Sprite[] upFrames = new Sprite[FrameCount];
        private readonly Sprite[] skillFrames = new Sprite[SkillFrameCount];

        private PlayerMovement movement;
        private PlayerSkill skill;
        private SpriteRenderer spriteRenderer;
        private Sprite idleSprite;
        private float elapsed;
        private int frameIndex;
        private bool hasFrames;
        private bool hasSkillFrames;
        private bool isPlayingSkill;
        private float skillElapsed;

        public bool IsPlayingSkill => isPlayingSkill;
        public bool HasDefeatFrames => hasDefeatFrames;
        public bool HasVictoryFrames => hasVictoryFrames;
        public float VictoryDuration => VictoryFrameCount * VictoryLoops * VictoryFrameSeconds;
        public bool IsVictoryPending => !float.IsNaN(victoryStartsAt);
        public Sprite GetVictoryFrame(int index) => victoryFrames[index];
        public float DefeatDuration => DefeatFrameCount * DefeatFrameSeconds;
        public Sprite GetDefeatFrame(int index) => defeatFrames[index];
        public Sprite IdleSprite => idleSprite;
        public bool HasFlightFrames => hasFlightFrames;
        public Sprite FlightIdleSprite => flyIdleSprite;
        // PlayerFlight is added when the wings are taken, after this component woke up.
        public bool IsShowingFlight
        {
            get
            {
                if (!hasFlightFrames) return false;
                if (flight == null) flight = GetComponent<PlayerFlight>();
                return flight != null && flight.IsFlying;
            }
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            idleSprite = spriteRenderer.sprite;
            hasFrames = LoadFrames();
            hasSkillFrames = LoadSkillFrames();
            hasFlightFrames = LoadFlightFrames();
            actionState = GetComponent<PlayerActionState>();
            ultimate = GetComponent<PlayerUltimate>();
            health = GetComponent<Health>();
            Sprite[] deathSprites = Resources.LoadAll<Sprite>(DefeatResourcePath);
            hasDefeatFrames = true;
            for (int i = 0; i < DefeatFrameCount; i++)
            {
                defeatFrames[i] = FindSprite(deathSprites, $"Erpin_Death_{i}");
                hasDefeatFrames &= defeatFrames[i] != null;
            }
            if (health != null) health.Died += OnDied;
            Sprite[] victorySprites = Resources.LoadAll<Sprite>(VictoryResourcePath);
            hasVictoryFrames = true;
            for (int i = 0; i < VictoryFrameCount; i++)
            {
                victoryFrames[i] = FindSprite(victorySprites, $"Erpin_Victory_{i}");
                hasVictoryFrames &= victoryFrames[i] != null;
            }
            Sprite[] highGradeSprites = Resources.LoadAll<Sprite>(HighGradeResourcePath);
            hasHighGradeFrames = true;
            for (int i = 0; i < highGradeFrames.Length; i++)
            {
                highGradeFrames[i] = FindSprite(highGradeSprites, $"Erpin_HighGrade_{i}");
                hasHighGradeFrames &= highGradeFrames[i] != null;
            }
        }

        private void Start()
        {
            skill = GetComponent<PlayerSkill>();
            if (skill != null)
            {
                skill.SalvoStarted += PlaySkill;
            }
        }

        private void OnDied()
        {
            diedAt = Time.unscaledTime;
            isPlayingSkill = false;
            victoryStartsAt = float.NaN;
        }

        // The run's VICTORY (2026-11-20 user request): she keeps jumping until the scene changes.
        public bool IsVictoryLooping { get; private set; }

        // Jump for joy after delay seconds (game time, so a pause holds it). False when she cannot. Without loop
        // she jumps VictoryLoops times and keeps the last pose; with loop she jumps on without end.
        public bool PlayVictory(float delay, bool loop = false)
        {
            if (!hasVictoryFrames || (health != null && health.IsDead)) return false;
            victoryStartsAt = Time.time + Mathf.Max(0f, delay);
            IsVictoryLooping = loop;
            return true;
        }

        // secondsSinceStart < 0 is the wait before the jump: the normal drawing still shows.
        public bool TickVictory(float secondsSinceStart)
        {
            if (!hasVictoryFrames || float.IsNaN(victoryStartsAt) || (health != null && health.IsDead)) return false;
            if (secondsSinceStart < 0f) return false;
            int index = Mathf.FloorToInt(secondsSinceStart / VictoryFrameSeconds);
            if (!IsVictoryLooping && index >= VictoryFrameCount * VictoryLoops)
            {
                bool busy = isPlayingSkill || (movement != null && (movement.IsMoving || movement.IsAimingAttack));
                if (busy)
                {
                    victoryStartsAt = float.NaN;
                    return false;
                }
                index = VictoryFrameCount - 1;
            }
            spriteRenderer.sprite = victoryFrames[index % VictoryFrameCount];
            return true;
        }

        // Unscaled, like the GAME OVER sequence that waits on it, so a stopped game clock cannot freeze the fall.
        public bool TickDefeat(float secondsSinceDeath)
        {
            if (!hasDefeatFrames || health == null || !health.IsDead) return false;
            int frame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, secondsSinceDeath) / DefeatFrameSeconds), 0,
                DefeatFrameCount - 1);
            spriteRenderer.sprite = defeatFrames[frame];
            return true;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            if (skill != null)
            {
                skill.SalvoStarted -= PlaySkill;
            }
        }

        public void PlaySkill()
        {
            if (!hasSkillFrames)
            {
                return;
            }

            isPlayingSkill = true;
            skillElapsed = 0f;
            victoryStartsAt = float.NaN;
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (TickDefeat(float.IsNaN(diedAt) ? 0f : Time.unscaledTime - diedAt)) return;
            if (TickHighGrade(Time.deltaTime)) return;
            if (!float.IsNaN(victoryStartsAt) && TickVictory(Time.time - victoryStartsAt)) return;

            if (isPlayingSkill && TickSkill(Time.deltaTime))
            {
                return;
            }

            if (!hasFrames || movement == null)
            {
                return;
            }

            bool flying = IsShowingFlight;
            if (!movement.IsMoving)
            {
                elapsed = 0f;
                frameIndex = 0;
                spriteRenderer.sprite = movement.IsAimingAttack ? SelectFrames(movement.FacingDirection)[0]
                    : flying ? flyIdleSprite : idleSprite;
                return;
            }

            elapsed += Time.deltaTime;
            float frameDuration = 1f / framesPerSecond;
            while (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                frameIndex = (frameIndex + 1) % FrameCount;
                // Sound-3: frames 1 and 3 are the feet touching down, two steps per walk cycle. No steps in the air;
                // Sound pass 2026-11-21: flying beats the wings once per cycle (frame 1) instead.
                if (frameIndex % 2 == 1 && !flying)
                    TrickalFanGame.Audio.CombatSfx.Play(TrickalFanGame.Audio.CombatSfxCue.PlayerStep);
                else if (frameIndex == 1 && flying)
                    TrickalFanGame.Audio.CombatSfx.Play(TrickalFanGame.Audio.CombatSfxCue.PlayerWingFlap);
            }

            spriteRenderer.sprite = SelectFrames(movement.FacingDirection)[frameIndex];
        }

        private void OnDisable()
        {
            isPlayingSkill = false;
            highGradeElapsed = 0f;
            artworkPhase = PlayerActionPhase.Normal;
            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        private bool TickSkill(float deltaTime)
        {
            skillElapsed += deltaTime;
            int skillFrame = Mathf.FloorToInt(skillElapsed * skillFramesPerSecond);
            bool salvoActive = skill != null && skill.IsFiring;
            if (skillFrame >= SkillFrameCount && !salvoActive)
            {
                isPlayingSkill = false;
                return false;
            }

            // 발사가 애니메이션보다 길면 마지막 프레임을 유지한다.
            spriteRenderer.sprite = skillFrames[Mathf.Min(skillFrame, SkillFrameCount - 1)];
            return true;
        }

        private bool TickHighGrade(float deltaTime)
        {
            PlayerActionPhase phase = actionState != null ? actionState.Phase : PlayerActionPhase.Normal;
            if (phase != artworkPhase)
            {
                artworkPhase = phase;
                highGradeElapsed = 0f;
            }
            if (!hasHighGradeFrames || (health != null && health.IsDead) ||
                (phase != PlayerActionPhase.UltimateDashing && phase != PlayerActionPhase.UltimateImpactRecovery))
                return false;

            isPlayingSkill = false;
            highGradeElapsed += Mathf.Max(0f, deltaTime);
            if (Mathf.Abs(actionState.DashDirection.x) > 0.001f)
                spriteRenderer.flipX = actionState.DashDirection.x > 0f;
            int index;
            if (phase == PlayerActionPhase.UltimateDashing)
                index = highGradeElapsed < 0.08f ? 0 : 1 + Mathf.FloorToInt((highGradeElapsed - 0.08f) * 10f) % 3;
            else
            {
                float duration = ultimate != null ? ultimate.ImpactRecoveryDuration : 0.4f;
                index = 4 + Mathf.Clamp(Mathf.FloorToInt(highGradeElapsed / Mathf.Max(0.01f, duration) * 4f), 0, 3);
            }
            spriteRenderer.sprite = highGradeFrames[index];
            return true;
        }

        private Sprite[] SelectFrames(Vector2 direction)
        {
            bool flying = IsShowingFlight;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return flying ? flySideFrames : sideFrames;
            }

            if (direction.y > 0f) return flying ? flyUpFrames : upFrames;
            return flying ? flyDownFrames : downFrames;
        }

        // Optional: without the flight drawings she keeps walking frames in the air.
        private bool LoadFlightFrames()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(FlightResourcePath);
            if (sprites.Length == 0) return false;
            bool complete = true;
            for (int i = 0; i < FrameCount; i++)
            {
                flyDownFrames[i] = FindSprite(sprites, $"Erpin_Fly_Down_{i}");
                flySideFrames[i] = FindSprite(sprites, $"Erpin_Fly_Side_{i}");
                flyUpFrames[i] = FindSprite(sprites, $"Erpin_Fly_Up_{i}");
                complete &= flyDownFrames[i] != null && flySideFrames[i] != null && flyUpFrames[i] != null;
            }
            flyIdleSprite = FindSprite(sprites, FlightIdleName);
            complete &= flyIdleSprite != null;
            if (!complete) Debug.LogError($"Erpin flight drawings are incomplete in Resources/{FlightResourcePath}.", this);
            return complete;
        }

        private bool LoadFrames()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcePath);
            for (int i = 0; i < FrameCount; i++)
            {
                downFrames[i] = FindSprite(sprites, $"Erpin_Walk_Down_{i}");
                sideFrames[i] = FindSprite(sprites, $"Erpin_Walk_Side_{i}");
                upFrames[i] = FindSprite(sprites, $"Erpin_Walk_Up_{i}");

                if (downFrames[i] == null || sideFrames[i] == null || upFrames[i] == null)
                {
                    Debug.LogError($"Erpin walking sprite frame {i} is missing from Resources/{ResourcePath}.", this);
                    return false;
                }
            }

            return true;
        }

        private bool LoadSkillFrames()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(SkillResourcePath);
            for (int i = 0; i < SkillFrameCount; i++)
            {
                skillFrames[i] = FindSprite(sprites, $"Erpin_lowGrade_skill_{i}");
                if (skillFrames[i] == null)
                {
                    Debug.LogError($"Erpin skill sprite frame {i} is missing from Resources/{SkillResourcePath}.", this);
                    return false;
                }
            }

            return true;
        }

        private static Sprite FindSprite(Sprite[] sprites, string spriteName)
        {
            return Array.Find(sprites, sprite => sprite.name == spriteName);
        }
    }
}
