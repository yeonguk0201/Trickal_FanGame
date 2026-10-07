using System;
using UnityEngine;

namespace TrickalFanGame.Player
{
    // Hitbox-0: the player's terrain collider, a small circle at the feet on its own child and layer. The PlayerFeet
    // layer collides with Environment and Pit only, and the Player layer no longer does, so walls, doors, obstacles,
    // chests and pits stop the feet while the root body circle stays the hurtbox for enemies, enemy projectiles and
    // pickups. Both colliders share the root Rigidbody2D, so collision messages still reach the player root.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PlayerFeet : MonoBehaviour
    {
        public const string LayerName = "PlayerFeet";
        public const string ObjectName = "Feet";
        public const float Radius = 0.3f;
        public const float BodyRadius = 0.5f;

        // The bottom of the feet meets the bottom of the body circle.
        public static readonly Vector2 LocalPosition = new(0f, Radius - BodyRadius);
        public static readonly string[] CollisionLayers = { "Environment", "Pit" };

        private CircleCollider2D feetCollider;

        public static int Layer => LayerMask.NameToLayer(LayerName);

        public CircleCollider2D Collider
        {
            get
            {
                if (feetCollider == null) feetCollider = GetComponent<CircleCollider2D>();
                return feetCollider;
            }
        }

        public Vector2 WorldCenter => transform.TransformPoint(Collider.offset);

        // Hitbox-1: under a root scaled by bodyScale the feet keep their world radius, and their bottom stays at the
        // bottom of the scaled body circle.
        public void ApplyBodyScale(float bodyScale)
        {
            float scale = Mathf.Max(0.01f, bodyScale);
            transform.localScale = Vector3.one / scale;
            transform.localPosition = new Vector3(0f, (Radius - BodyRadius * scale) / scale, 0f);
        }

        // Geometric, so it also answers for pairs the layer matrix or flight ignores.
        public bool Overlaps(Collider2D other) => other != null && Collider.Distance(other).isOverlapped;

        // Finds or creates the feet child and restores its layer, place and size. Returns true when anything changed.
        public static bool Ensure(GameObject player, out PlayerFeet feet)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (Layer < 0)
                throw new InvalidOperationException($"The '{LayerName}' layer is missing. Run the Hitbox-0 setup.");

            bool changed = false;
            feet = player.GetComponentInChildren<PlayerFeet>(true);
            if (feet == null)
            {
                GameObject feetObject = new(ObjectName, typeof(CircleCollider2D), typeof(PlayerFeet));
                feetObject.transform.SetParent(player.transform, false);
                feet = feetObject.GetComponent<PlayerFeet>();
                changed = true;
            }

            Transform feetTransform = feet.transform;
            CircleCollider2D circle = feet.Collider;
            if (feet.gameObject.layer != Layer || (Vector2)feetTransform.localPosition != LocalPosition ||
                feetTransform.localScale != Vector3.one || !Mathf.Approximately(circle.radius, Radius) ||
                circle.offset != Vector2.zero || circle.isTrigger || !circle.enabled)
            {
                feet.gameObject.layer = Layer;
                feetTransform.localPosition = LocalPosition;
                feetTransform.localScale = Vector3.one;
                circle.radius = Radius;
                circle.offset = Vector2.zero;
                circle.isTrigger = false;
                circle.enabled = true;
                changed = true;
            }

            return changed;
        }
    }
}
