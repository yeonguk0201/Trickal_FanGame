using System;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerWalkAnimator : MonoBehaviour
    {
        private const string ResourcePath = "Characters/Erpin_Walking";
        private const int FrameCount = 4;

        [SerializeField, Min(1f)] private float framesPerSecond = 8f;

        private readonly Sprite[] downFrames = new Sprite[FrameCount];
        private readonly Sprite[] sideFrames = new Sprite[FrameCount];
        private readonly Sprite[] upFrames = new Sprite[FrameCount];

        private PlayerMovement movement;
        private SpriteRenderer spriteRenderer;
        private Sprite idleSprite;
        private float elapsed;
        private int frameIndex;
        private bool hasFrames;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            idleSprite = spriteRenderer.sprite;
            hasFrames = LoadFrames();
        }

        private void LateUpdate()
        {
            if (!hasFrames || movement == null || spriteRenderer == null)
            {
                return;
            }

            if (!movement.IsMoving)
            {
                elapsed = 0f;
                frameIndex = 0;
                spriteRenderer.sprite = idleSprite;
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
            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
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

        private static Sprite FindSprite(Sprite[] sprites, string spriteName)
        {
            return Array.Find(sprites, sprite => sprite.name == spriteName);
        }
    }
}
