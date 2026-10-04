using System;
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
