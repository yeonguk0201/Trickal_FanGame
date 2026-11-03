using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only, for enemies drawn through FairyKingdomArtworkView. While the body actually travels it
    /// shows the drawn walk frames, one cycle per stride; otherwise the view's idle drawing stays. It runs after the
    /// view has restored the idle drawing each frame. The Editor setup sizes the frames to the idle drawing.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyWalkFrames : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.1f)] private float strideLength = 0.8f;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private SpriteRenderer visual;
        private Vector2 previousPosition;
        private float phase;
        private bool moving;

        public Sprite[] Frames => frames;
        public float StrideLength => strideLength;
        public bool IsWalking => moving;

        public void Configure(Sprite[] walkFrames, float stride)
        {
            frames = walkFrames ?? System.Array.Empty<Sprite>();
            strideLength = Mathf.Max(0.1f, stride);
        }

        private void Awake() => Cache();

        private void OnEnable()
        {
            Cache();
            previousPosition = body.position;
            phase = 0f;
            moving = false;
        }

        private void Cache()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
            visual = view != null && view.Visual != null ? view.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            Vector2 position = body.position;
            float distance = (position - previousPosition).magnitude;
            previousPosition = position;
            bool suppressed = (health != null && health.IsDead) || (knockback != null && knockback.IsActive);
            // Actual travel, so a body blocked by a wall stops its feet. Room placement jumps are ignored.
            moving = !suppressed && distance > 0.0001f &&
                     distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f);
            phase = moving ? Mathf.Repeat(phase + distance / strideLength, 1f) : 0f;
        }

        private void LateUpdate()
        {
            if (moving) RenderPose(phase);
        }

        // Shows the walk frame for cycle (0~1). Also used by the Editor verifier.
        public void RenderPose(float cycle)
        {
            if (visual == null) Cache();
            if (visual == null || frames.Length == 0) return;
            Sprite frame = frames[Mathf.FloorToInt(Mathf.Repeat(cycle, 1f) * frames.Length) % frames.Length];
            if (frame != null) visual.sprite = frame;
        }
    }
}
