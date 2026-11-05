using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only (산사모-광기 자폭 예고, 2026-11-06 user drawing). While the self-destruct warning runs, the
    /// body crouches through four drawn frames spread evenly over the warning instead of standing still; the red
    /// tint SelfDestructEnemy applies stays. If it dies mid-warning, its blast keeps crouching on the same clock
    /// (EnemyDeathExplosion hands these frames to EnemyDeathBlast). It runs after the artwork view and the walk
    /// frames have drawn the frame. The Editor setup sizes the frames to the idle drawing.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(220)]
    [RequireComponent(typeof(SelfDestructEnemy))]
    public sealed class SelfDestructCrouch : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();

        private SelfDestructEnemy bomber;
        private Health health;
        private SpriteRenderer visual;

        public Sprite[] Frames => frames;
        public bool HasFrames => frames != null && frames.Length > 0 && frames[0] != null;

        public void Configure(Sprite[] crouchFrames) => frames = crouchFrames ?? System.Array.Empty<Sprite>();

        // The frame shown at a share (0~1) of the warning: each frame holds an equal part, the last to the end.
        public static int FrameIndex(float progress, int count) =>
            count <= 0 ? -1 : Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * count), 0, count - 1);

        public Sprite FrameAt(float progress) => HasFrames ? frames[FrameIndex(progress, frames.Length)] : null;

        private void Awake() => Cache();

        private void Cache()
        {
            if (bomber == null) bomber = GetComponent<SelfDestructEnemy>();
            if (health == null) health = GetComponent<Health>();
            if (visual != null) return;
            FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
            visual = view != null && view.Visual != null ? view.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            Cache();
            if (bomber == null || !bomber.IsPrimed || (health != null && health.IsDead)) return;
            RenderPose(bomber.TelegraphProgress(Time.time));
        }

        // Shows the crouch frame for a share of the warning. Also used by the Editor verifier.
        public void RenderPose(float progress)
        {
            Cache();
            Sprite frame = FrameAt(progress);
            if (visual != null && frame != null) visual.sprite = frame;
        }
    }
}
