using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only, for a one-drawing enemy that hops as it travels like 부스러기 (부스러기 쫄따구, 2026-11-06).
    /// While the body actually moves, a copy of its picture rises and lands once per stride; the body, its collider
    /// and ground shadow stay on the floor. It runs after the artwork view has chosen the drawing, so colour
    /// variants, hit flashes and facing follow the body.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyHopMotion : MonoBehaviour
    {
        public const string ArtworkName = "Enemy Hop Artwork";
        // Share of a stride spent in the air, as the boss hop (take off at 18%, land at 86%).
        public const float TakeOff = 0.18f;
        public const float Landing = 0.86f;

        [SerializeField, Min(0.1f)] private float strideLength = 0.7f;
        [SerializeField, Min(0f)] private float hopHeight = 0.14f;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private SpriteRenderer source;
        private SpriteRenderer artwork;
        private Vector2 previousPosition;
        private float phase;
        private bool moving;
        private float displayedLift;
        private bool hidSource;

        public float StrideLength => strideLength;
        public float HopHeight => hopHeight;
        public bool IsHopping => moving;
        public SpriteRenderer Artwork => artwork;

        public void Configure(float stride, float height)
        {
            strideLength = Mathf.Max(0.1f, stride);
            hopHeight = Mathf.Max(0f, height);
        }

        // Height above the floor (world units) at a point of the stride.
        public static float LiftAt(float cycle, float height)
        {
            float p = Mathf.Repeat(cycle, 1f);
            if (p < TakeOff || p >= Landing) return 0f;
            // Squared sine: no speed at take-off and landing.
            float sine = Mathf.Sin((p - TakeOff) / (Landing - TakeOff) * Mathf.PI);
            return sine * sine * height;
        }

        private void Awake() => Cache();

        private void OnEnable()
        {
            Cache();
            previousPosition = body.position;
            phase = 0f;
            moving = false;
            displayedLift = 0f;
        }

        private void Cache()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (health == null) health = GetComponent<Health>();
            if (knockback == null) knockback = GetComponent<KnockbackReceiver>();
            if (source != null) return;
            FairyKingdomArtworkView view = GetComponent<FairyKingdomArtworkView>();
            source = view != null && view.Visual != null ? view.Visual : GetComponent<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            Vector2 position = body.position;
            float distance = (position - previousPosition).magnitude;
            previousPosition = position;
            bool suppressed = (health != null && health.IsDead) || (knockback != null && knockback.IsActive);
            // Actual travel, so a body blocked by a wall lands. Room placement jumps are ignored.
            moving = !suppressed && distance > 0.0001f &&
                     distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f);
            // A stopped body finishes on the floor instead of hanging in the air.
            phase = moving ? Mathf.Repeat(phase + distance / strideLength, 1f) : 0f;
        }

        private void LateUpdate()
        {
            if (health != null && health.IsDead)
            {
                Restore();
                return;
            }
            float target = moving ? LiftAt(phase, hopHeight) : 0f;
            // FixedUpdate moves in steps; ease the display between them and back to the floor.
            displayedLift = Mathf.Lerp(displayedLift, target, 1f - Mathf.Exp(-Time.deltaTime / 0.035f));
            if (displayedLift < 0.002f && !moving) displayedLift = 0f;
            RenderLift(displayedLift);
        }

        // Shows the picture lifted by height (world units). Also used by the Editor verifier.
        public void RenderLift(float height)
        {
            Cache();
            if (source == null) return;
            if (height <= 0f)
            {
                Restore();
                return;
            }
            if (artwork == null)
            {
                GameObject child = new(ArtworkName);
                child.layer = source.gameObject.layer;
                child.transform.SetParent(source.transform, false);
                artwork = child.AddComponent<SpriteRenderer>();
            }
            if (!hidSource)
            {
                hidSource = !source.forceRenderingOff;
                if (!hidSource) return; // something else already hides the body; leave it hidden
                source.forceRenderingOff = true;
            }
            artwork.enabled = source.enabled;
            artwork.sprite = source.sprite;
            artwork.color = source.color;
            artwork.flipX = source.flipX;
            artwork.flipY = source.flipY;
            artwork.sharedMaterial = source.sharedMaterial;
            artwork.sortingLayerID = source.sortingLayerID;
            artwork.sortingOrder = source.sortingOrder;
            artwork.maskInteraction = source.maskInteraction;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(source.transform.lossyScale.y));
            artwork.transform.localPosition = new Vector3(0f, height / scale, 0f);
            artwork.transform.localRotation = Quaternion.identity;
            artwork.transform.localScale = Vector3.one;
        }

        private void Restore()
        {
            if (hidSource && source != null) source.forceRenderingOff = false;
            hidSource = false;
            if (artwork != null) artwork.enabled = false;
        }

        private void OnDisable()
        {
            moving = false;
            phase = 0f;
            displayedLift = 0f;
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
            if (artwork == null) return;
            if (Application.isPlaying) Destroy(artwork.gameObject);
            else DestroyImmediate(artwork.gameObject);
        }
    }
}
