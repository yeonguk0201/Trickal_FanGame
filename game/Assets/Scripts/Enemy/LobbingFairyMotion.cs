using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-12: 저혈당 요정-광기's throw (user drawing, 2026-10-30). Presentation only: it runs after ArtworkView
    // restores the fitted idle drawing and never changes physics. The 2x2 atlas holds the throw in order: ready
    // (crowbar held), raised over the head, released (arm forward, crowbar gone) and follow-through. The wind-up
    // shows ready then raised, the throw shows released then follow-through, and otherwise the idle drawing shows.
    // While winding up and throwing it faces its target, since the drawings face left.
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class LobbingFairyMotion : MonoBehaviour
    {
        public const int ReadyFrame = 0, RaisedFrame = 1, ReleaseFrame = 2, FollowThroughFrame = 3;
        // Share of the wind-up spent in the ready pose before the crowbar goes up.
        public const float ReadyShare = 0.35f;
        public const float ReleaseDuration = 0.15f;
        public const float FollowThroughEnd = 0.4f;

        [SerializeField] private Texture2D atlas;
        // The ready pose's visible area in its cell (0~1). It fixes the scale and the ground line of every frame.
        [SerializeField] private Rect neutralContent;
        [SerializeField] private string idleArtworkId = "low-blood-sugar-fairy-madness";
        private SpriteRenderer visual;
        private LobbingEnemyController controller;
        private Health health;
        private Sprite idle;
        private readonly Sprite[] frames = new Sprite[4];

        public Texture2D Atlas => atlas;
        public Rect NeutralContent => neutralContent;
        public int CurrentFrame { get; private set; } = -1;
        public Sprite Frame(int index) => index >= 0 && index < frames.Length && Initialize() ? frames[index] : null;

        public void Configure(Texture2D texture, Rect content, string artworkId)
        {
            atlas = texture; neutralContent = content; idleArtworkId = artworkId;
            idle = null;
            ReleaseFrames();
        }

        private void OnEnable()
        {
            visual = GetComponent<SpriteRenderer>();
            controller = GetComponent<LobbingEnemyController>();
            health = GetComponent<Health>();
        }

        private void LateUpdate()
        {
            if (controller == null) return;
            bool suppressed = health != null && health.IsDead;
            RenderPose(controller.IsWindingUp ? WindUpProgress(Time.time) : -1f, Time.time - controller.LastThrowTime,
                suppressed);
        }

        private float WindUpProgress(float now) => controller.WindUpDuration > 0f
            ? Mathf.InverseLerp(controller.WindUpEndsAt - controller.WindUpDuration, controller.WindUpEndsAt, now)
            : 1f;

        // windUpProgress below 0 means no wind-up. Shared by gameplay and verification.
        public void RenderPose(float windUpProgress, float throwAge, bool suppressed = false)
        {
            if (!Initialize()) return;
            CurrentFrame = suppressed ? -1
                : windUpProgress >= 0f ? (windUpProgress < ReadyShare ? ReadyFrame : RaisedFrame)
                : throwAge >= 0f && throwAge < ReleaseDuration ? ReleaseFrame
                : throwAge >= 0f && throwAge < FollowThroughEnd ? FollowThroughFrame
                : -1;
            visual.sprite = CurrentFrame < 0 ? idle : frames[CurrentFrame];
            Transform target = controller != null ? controller.Target : null;
            if (CurrentFrame >= 0 && target != null && Mathf.Abs(target.position.x - transform.position.x) > 0.001f)
                visual.flipX = target.position.x > transform.position.x;
        }

        private bool Initialize()
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (controller == null) controller = GetComponent<LobbingEnemyController>();
            if (idle == null)
            {
                FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
                idle = FairyKingdomArtwork.Fit(idleArtworkId, view != null ? view.LocalWidth : 1.07f);
            }
            if (visual == null || idle == null || atlas == null || neutralContent.height <= 0f) return false;
            if (frames[0] != null) return true;
            float cellWidth = atlas.width / 2f, cellHeight = atlas.height / 2f;
            // The ready pose stands as tall as the idle drawing; the other frames share its scale and pivot.
            float ppu = neutralContent.height * cellHeight / idle.bounds.size.y;
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(atlas, new Rect(i % 2 * cellWidth, (1 - i / 2) * cellHeight, cellWidth,
                    cellHeight), neutralContent.center, ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = $"lobbing-fairy-throw-{i}";
            }
            return true;
        }

        private void OnDisable()
        {
            if (visual != null && idle != null) visual.sprite = idle;
            CurrentFrame = -1;
        }

        private void OnDestroy() => ReleaseFrames();

        private void ReleaseFrames()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) continue;
                if (Application.isPlaying) Destroy(frames[i]); else DestroyImmediate(frames[i]);
                frames[i] = null;
            }
        }
    }
}
