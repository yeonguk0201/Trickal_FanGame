using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only (user drawing, 2026-11-04). 잠복형's four frames: a moving mound of earth, then the shovel
    /// breaking out in three steps. Under the floor it shows the mound; the burst plays the three steps during
    /// the attack; up out of the ground it stays on the last step, and sinking plays them back. It runs after
    /// FairyKingdomArtworkView restores the idle drawing, and the Editor setup sizes the frames to that drawing.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    [RequireComponent(typeof(AmbushEnemyController))]
    public sealed class AmbushEnemyMotion : MonoBehaviour
    {
        public const int FrameCount = 4;
        public const int MoundFrame = 0, SurfacedFrame = 3;

        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();

        private AmbushEnemyController controller;
        private Health health;
        private SpriteRenderer visual;

        public Sprite[] Frames => frames;
        public bool HasFrames => frames != null && frames.Length == FrameCount;
        public int CurrentFrame { get; private set; } = -1;

        public void Configure(Sprite[] drawnFrames) => frames = drawnFrames ?? System.Array.Empty<Sprite>();

        // The frame each state shows, elapsed seconds into it.
        public static int FrameFor(AmbushEnemyController.AmbushState state, float elapsed, float attackDuration,
            float burrowDuration)
        {
            switch (state)
            {
                case AmbushEnemyController.AmbushState.Attacking:
                    return 1 + Step(elapsed, attackDuration);
                case AmbushEnemyController.AmbushState.Surfaced:
                    return SurfacedFrame;
                case AmbushEnemyController.AmbushState.Burrowing:
                    return SurfacedFrame - Step(elapsed, burrowDuration);
                default:
                    return MoundFrame;
            }
        }

        // 0, 1, 2 across the duration, then held on 2.
        private static int Step(float elapsed, float duration) =>
            duration <= 0f ? 2 : Mathf.Clamp(Mathf.FloorToInt(elapsed / duration * 3f), 0, 2);

        private void Awake() => Cache();

        private void Cache()
        {
            controller = GetComponent<AmbushEnemyController>();
            health = GetComponent<Health>();
            FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
            visual = view != null && view.Visual != null ? view.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (health != null && health.IsDead) return;
            RenderPose(Time.time);
        }

        public void RenderPose(float currentTime)
        {
            if (controller == null || visual == null) Cache();
            if (!HasFrames || controller == null || visual == null) return;
            CurrentFrame = FrameFor(controller.State, currentTime - controller.StateStartedAt,
                controller.AttackDuration, controller.BurrowDuration);
            if (frames[CurrentFrame] != null) visual.sprite = frames[CurrentFrame];
        }
    }
}
