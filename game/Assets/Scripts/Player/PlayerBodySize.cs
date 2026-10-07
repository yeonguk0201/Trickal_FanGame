using UnityEngine;

namespace TrickalFanGame.Player
{
    // Hitbox-1: the player's size multiplier. The look follows it without a limit, the body circle (hurtbox) follows
    // it up to HurtboxSizeLimit, and the feet circle keeps its world size so a large body still fits through doors
    // and gaps. The body grows upward from its feet: they stay where they stand and the root moves up.
    [DisallowMultipleComponent]
    public sealed class PlayerBodySize : MonoBehaviour
    {
        public const float MinimumSize = 0.5f;
        public const float HurtboxSizeLimit = 2f;

        private CircleCollider2D bodyCollider;
        private PlayerFeet feet;
        private Vector3 baseScale = Vector3.one;
        private float baseRadius = PlayerFeet.BodyRadius;
        private bool resolved;

        public float SizeMultiplier { get; private set; } = 1f;
        public float HurtboxMultiplier => Mathf.Min(SizeMultiplier, HurtboxSizeLimit);
        public float HurtboxRadius => baseRadius * HurtboxMultiplier;

        // Returns false when the size does not change.
        public bool SetSizeMultiplier(float multiplier)
        {
            Resolve();
            float size = Mathf.Max(MinimumSize, multiplier);
            if (Mathf.Approximately(size, SizeMultiplier)) return false;

            float previous = SizeMultiplier;
            SizeMultiplier = size;
            transform.localScale = baseScale * size;
            // The collider scales with the root, so the radius is divided back down past the limit.
            if (bodyCollider != null) bodyCollider.radius = baseRadius * HurtboxMultiplier / size;
            if (feet != null)
            {
                feet.ApplyBodyScale(size);
                transform.position += Vector3.up * (PlayerFeet.BodyRadius * (size - previous));
            }

            return true;
        }

        private void Resolve()
        {
            if (feet == null) feet = GetComponentInChildren<PlayerFeet>(true);
            if (resolved) return;
            resolved = true;
            baseScale = transform.localScale;
            bodyCollider = GetComponent<CircleCollider2D>();
            if (bodyCollider != null) baseRadius = bodyCollider.radius;
        }
    }
}
