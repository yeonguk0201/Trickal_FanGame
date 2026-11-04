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
    /// While the mound actually travels under the floor it plays four drawn move frames (2026-11-05), one cycle per
    /// stride; standing still or warning it shows the still mound. The ground shadow keeps the still mound's
    /// outline, so flying clods in the move frames do not make it flicker.
    /// Warning before the burst (2026-11-05): the mound rattles side to side instead of a red ellipse. A child copy
    /// of the drawing shakes while the body's own renderer is hidden, so the body, colliders and shadow stay put.
    /// The burst plays its three steps in the first 0.15 s of the attack and holds the last; sinking is unchanged.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    [RequireComponent(typeof(AmbushEnemyController))]
    public sealed class AmbushEnemyMotion : MonoBehaviour
    {
        public const int FrameCount = 4;
        public const int MoundFrame = 0, SurfacedFrame = 3;
        public const string RumbleName = "Ambush Rumble";
        // The three burst steps play within this share of the attack, then the last one holds.
        public const float BurstSeconds = 0.15f;
        // Side-to-side rattle while warning: world units, growing to the full amplitude at the burst.
        public const float RumbleAmplitude = 0.05f;
        public const float RumbleStepSeconds = 0.035f;

        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] moveFrames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.1f)] private float moveStride = 0.9f;

        private AmbushEnemyController controller;
        private Health health;
        private SpriteRenderer visual;
        private Rigidbody2D body;
        private Vector2 previousPosition;
        private float movePhase;
        private SpriteRenderer rumble;

        public Sprite[] Frames => frames;
        public Sprite[] MoveFrames => moveFrames;
        public float MoveStride => moveStride;
        public bool HasFrames => frames != null && frames.Length == FrameCount;
        public bool HasMoveFrames => moveFrames != null && moveFrames.Length == FrameCount;
        public int CurrentFrame { get; private set; } = -1;
        // The move frame shown, or -1 when a state frame is shown.
        public int CurrentMoveFrame { get; private set; } = -1;
        public bool IsMovingUnderground { get; private set; }
        public SpriteRenderer Rumble => rumble;
        public bool IsRumbling => rumble != null && rumble.enabled;

        public void Configure(Sprite[] drawnFrames) => frames = drawnFrames ?? System.Array.Empty<Sprite>();

        public void Configure(Sprite[] drawnFrames, Sprite[] drawnMoveFrames, float stride)
        {
            Configure(drawnFrames);
            moveFrames = drawnMoveFrames ?? System.Array.Empty<Sprite>();
            moveStride = Mathf.Max(0.1f, stride);
        }

        // The frame each state shows, elapsed seconds into it.
        public static int FrameFor(AmbushEnemyController.AmbushState state, float elapsed, float attackDuration,
            float burrowDuration)
        {
            switch (state)
            {
                case AmbushEnemyController.AmbushState.Attacking:
                    return 1 + Step(elapsed, Mathf.Min(attackDuration, BurstSeconds));
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

        private void OnEnable()
        {
            Cache();
            if (body != null) previousPosition = body.position;
            movePhase = 0f;
            IsMovingUnderground = false;
        }

        private void OnDisable() => StopRumble();

        private void Cache()
        {
            controller = GetComponent<AmbushEnemyController>();
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
            visual = view != null && view.Visual != null ? view.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        // Actual travel under the floor, so a mound held by a wall stops. Room placement jumps are ignored.
        private void FixedUpdate()
        {
            if (body == null) return;
            Vector2 position = body.position;
            float distance = (position - previousPosition).magnitude;
            previousPosition = position;
            bool burrowed = controller != null && controller.State == AmbushEnemyController.AmbushState.Burrowed &&
                            (health == null || !health.IsDead);
            IsMovingUnderground = burrowed && distance > 0.0001f &&
                distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f);
            movePhase = IsMovingUnderground ? Mathf.Repeat(movePhase + distance / moveStride, 1f) : 0f;
        }

        private void LateUpdate()
        {
            if (health != null && health.IsDead)
            {
                StopRumble();
                return;
            }
            RenderPose(Time.time);
        }

        public void RenderPose(float currentTime)
        {
            if (controller == null || visual == null) Cache();
            if (!HasFrames || controller == null || visual == null) return;
            CurrentFrame = FrameFor(controller.State, currentTime - controller.StateStartedAt,
                controller.AttackDuration, controller.BurrowDuration);
            CurrentMoveFrame = -1;
            if (!(IsMovingUnderground && controller.State == AmbushEnemyController.AmbushState.Burrowed &&
                  RenderMovePose(movePhase)) && frames[CurrentFrame] != null)
                visual.sprite = frames[CurrentFrame];
            UpdateRumble(currentTime);
        }

        // The rattle offset (world units, x and y) elapsed seconds into a warning of the given length.
        public static Vector2 RumbleOffset(float elapsed, float duration)
        {
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float amplitude = RumbleAmplitude * (0.6f + 0.4f * progress);
            int step = Mathf.Max(0, Mathf.FloorToInt(elapsed / RumbleStepSeconds));
            float x = (step % 2 == 0 ? 1f : -1f) * amplitude;
            float y = (step / 2 % 2 == 0 ? 1f : -1f) * amplitude * 0.25f;
            return new Vector2(x, y);
        }

        private void UpdateRumble(float currentTime)
        {
            if (controller.State != AmbushEnemyController.AmbushState.Emerging)
            {
                StopRumble();
                return;
            }
            if (rumble == null)
            {
                GameObject child = new(RumbleName);
                child.layer = visual.gameObject.layer;
                child.transform.SetParent(visual.transform, false);
                rumble = child.AddComponent<SpriteRenderer>();
            }
            rumble.enabled = true;
            rumble.sprite = visual.sprite;
            rumble.color = visual.color;
            rumble.flipX = visual.flipX;
            rumble.flipY = visual.flipY;
            rumble.sharedMaterial = visual.sharedMaterial;
            rumble.sortingLayerID = visual.sortingLayerID;
            rumble.sortingOrder = visual.sortingOrder;
            Vector2 offset = RumbleOffset(currentTime - controller.StateStartedAt, controller.EmergeTelegraphDuration);
            Vector3 scale = visual.transform.lossyScale;
            rumble.transform.localPosition = new Vector3(offset.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                offset.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 0f);
            rumble.transform.localRotation = Quaternion.identity;
            rumble.transform.localScale = Vector3.one;
            visual.forceRenderingOff = true; // still enabled, so the shadow and bounds keep following it
        }

        private void StopRumble()
        {
            if (rumble != null) rumble.enabled = false;
            if (visual != null) visual.forceRenderingOff = false;
        }

        // Shows the move frame for cycle (0~1). Also used by the Editor verifier.
        public bool RenderMovePose(float cycle)
        {
            if (visual == null) Cache();
            if (visual == null || !HasMoveFrames) return false;
            int index = Mathf.FloorToInt(Mathf.Repeat(cycle, 1f) * FrameCount) % FrameCount;
            if (moveFrames[index] == null) return false;
            visual.sprite = moveFrames[index];
            CurrentMoveFrame = index;
            return true;
        }

        // Under the floor the ground shadow follows the still mound's outline, whatever move frame is shown.
        public bool TryGetMoundBounds(out Bounds bounds)
        {
            bounds = default;
            if (controller == null || visual == null) Cache();
            if (controller == null || visual == null || !controller.IsBurrowed || !HasFrames ||
                frames[MoundFrame] == null)
                return false;
            Bounds local = frames[MoundFrame].bounds;
            Transform view = visual.transform;
            Vector3 scale = view.lossyScale;
            bounds = new Bounds(view.TransformPoint(local.center),
                new Vector3(local.size.x * Mathf.Abs(scale.x), local.size.y * Mathf.Abs(scale.y), 0f));
            return true;
        }
    }
}
