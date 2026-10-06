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
        private readonly Sprite[] highGradeFrames = new Sprite[8];
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

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            idleSprite = spriteRenderer.sprite;
            hasFrames = LoadFrames();
            hasSkillFrames = LoadSkillFrames();
            actionState = GetComponent<PlayerActionState>();
            ultimate = GetComponent<PlayerUltimate>();
            health = GetComponent<Health>();
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

        private void OnDestroy()
        {
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
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (TickHighGrade(Time.deltaTime)) return;

            if (isPlayingSkill && TickSkill(Time.deltaTime))
            {
                return;
            }

            if (!hasFrames || movement == null)
            {
                return;
            }

            if (!movement.IsMoving)
            {
                elapsed = 0f;
                frameIndex = 0;
                spriteRenderer.sprite = movement.IsAimingAttack ? SelectFrames(movement.FacingDirection)[0] : idleSprite;
                return;
            }

            elapsed += Time.deltaTime;
            float frameDuration = 1f / framesPerSecond;
            while (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                frameIndex = (frameIndex + 1) % FrameCount;
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
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return sideFrames;
            }

            return direction.y > 0f ? upFrames : downFrames;
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
