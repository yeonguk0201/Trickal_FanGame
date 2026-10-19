using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Presentation only. Runs after ArtworkView restores the fitted idle drawing.
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class LivelyFairyMotion : MonoBehaviour
    {
        [SerializeField] private Texture2D atlas;
        [SerializeField] private Rect neutralContent;
        private SpriteRenderer visual;
        private FourWayShooterController controller;
        private Rigidbody2D body;
        private Sprite idle;
        private readonly Sprite[] frames = new Sprite[8];
        private Vector2 previous;
        private float cycle, shotAt = float.NegativeInfinity;
        private bool walking;
        public int CurrentFrame { get; private set; } = -1;
        public Texture2D Atlas => atlas;

        public void Configure(Texture2D texture, Rect content)
        { atlas = texture; neutralContent = content; }

        private void OnEnable()
        {
            visual = GetComponent<SpriteRenderer>();
            controller = GetComponent<FourWayShooterController>();
            body = GetComponent<Rigidbody2D>();
            previous = body != null ? body.position : (Vector2)transform.position;
            cycle = 0; walking = false; shotAt = float.NegativeInfinity;
            if (controller != null) controller.VolleyFired += OnVolley;
        }

        private void OnVolley(EnemyProjectile[] _) => shotAt = Time.time;

        private void FixedUpdate()
        {
            if (body == null || controller == null) return;
            float distance = Vector2.Distance(body.position, previous);
            previous = body.position;
            walking = controller.State == FourWayShooterState.Moving && !controller.IsActionSuppressed &&
                distance > 0.0001f && body.linearVelocity.sqrMagnitude > 0.0025f &&
                distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3);
            cycle = walking ? Mathf.Repeat(cycle + distance / 1.05f, 1f) : 0;
        }

        private void LateUpdate()
        {
            if (controller == null) return;
            if (controller.IsActionSuppressed) shotAt = float.NegativeInfinity;
            RenderPose(controller.State, cycle, walking, Time.time - shotAt, controller.IsActionSuppressed);
        }

        public void RenderPose(FourWayShooterState state, float walkCycle, bool moving, float shotAge,
            bool suppressed = false)
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (idle == null)
            {
                var view = GetComponent<FairyKingdomArtworkView>();
                idle = FairyKingdomArtwork.Fit("low-blood-sugar-fairy", view != null ? view.LocalWidth : 1.07f);
            }
            if (visual == null || idle == null || atlas == null || neutralContent.height <= 0) return;
            if (frames[0] == null)
            {
                float cellWidth = atlas.width / 4f, cellHeight = atlas.height / 2f;
                float ppu = neutralContent.height * cellHeight / idle.bounds.size.y;
                for (int i = 0; i < 8; i++)
                    frames[i] = Sprite.Create(atlas, new Rect(i % 4 * cellWidth, (1 - i / 4) * cellHeight, cellWidth, cellHeight),
                        neutralContent.center, ppu, 0, SpriteMeshType.FullRect);
            }
            CurrentFrame = suppressed ? -1 : state switch
            {
                FourWayShooterState.Telegraph => 5,
                FourWayShooterState.Recovering => shotAge >= 0 && shotAge < 0.16f ? 6 : 7,
                FourWayShooterState.Moving when moving => Mathf.FloorToInt(Mathf.Repeat(walkCycle, 1) * 4),
                _ => -1,
            };
            visual.sprite = CurrentFrame < 0 ? idle : frames[CurrentFrame];
        }

        private void OnDisable()
        {
            if (controller != null) controller.VolleyFired -= OnVolley;
            if (visual != null && idle != null) visual.sprite = idle;
            CurrentFrame = -1;
        }

        private void OnDestroy()
        {
            foreach (Sprite frame in frames)
                if (frame != null)
                {
                    if (Application.isPlaying) Destroy(frame); else DestroyImmediate(frame);
                }
        }
    }
}
