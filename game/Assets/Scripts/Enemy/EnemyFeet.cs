using System;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Hitbox-2: a normal enemy's terrain collider, a small circle at the feet on its own child and layer, the same
    // split as the player's Hitbox-0. The EnemyFeet layer collides with Environment and Pit only, and the body circle
    // on the root excludes those two layers, so walls, obstacles, chests and pits stop the feet while the body stays
    // the hurtbox and contact collider. Bosses keep one circle for both. Both colliders share the root Rigidbody2D,
    // so collision messages still reach the enemy root.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class EnemyFeet : MonoBehaviour
    {
        public const string LayerName = "EnemyFeet";
        public const string ObjectName = "Feet";
        // Trial value: a one-cell lane leaves 0.15 on each side. Every normal enemy uses the same world size.
        public const float Radius = 0.35f;
        public static readonly string[] CollisionLayers = { "Environment", "Pit" };

        private CircleCollider2D feetCollider;

        public static int Layer => LayerMask.NameToLayer(LayerName);
        public static int TerrainMask => LayerMask.GetMask(CollisionLayers);

        public CircleCollider2D Collider
        {
            get
            {
                if (feetCollider == null) feetCollider = GetComponent<CircleCollider2D>();
                return feetCollider;
            }
        }

        public Vector2 WorldCenter => transform.TransformPoint(Collider.offset);
        public float WorldRadius => WorldRadiusOf(Collider);

        public static float WorldRadiusOf(CircleCollider2D circle)
        {
            Vector3 scale = circle.transform.lossyScale;
            return circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        // The solid circle on the enemy root: the hurtbox and, without feet, the terrain collider too.
        public static bool TryGetBody(GameObject enemy, out CircleCollider2D body)
        {
            body = null;
            if (enemy == null) return false;
            foreach (CircleCollider2D circle in enemy.GetComponents<CircleCollider2D>())
            {
                if (circle.isTrigger) continue;
                body = circle;
                return true;
            }

            return false;
        }

        // The circle an enemy stands on, in world space. The sprite and its ground shadow stand on the bottom of the
        // body circle. The feet share that bottom, and an enemy without feet uses a feet-sized circle (or the whole
        // body, when smaller) resting on the same bottom, so every enemy meets floor traps at its drawn soles.
        public static void GroundCircle(GameObject enemy, out Vector2 center, out float radius)
        {
            EnemyFeet feet = enemy.GetComponentInChildren<EnemyFeet>();
            if (feet != null && feet.isActiveAndEnabled && feet.Collider != null && feet.Collider.enabled)
            {
                center = feet.WorldCenter;
                radius = feet.WorldRadius;
                return;
            }

            if (!TryGetBody(enemy, out CircleCollider2D body))
            {
                center = enemy.transform.position;
                radius = 0f;
                return;
            }

            Vector2 bodyCenter = body.transform.TransformPoint(body.offset);
            float bodyRadius = WorldRadiusOf(body);
            radius = Mathf.Min(bodyRadius, Radius);
            center = bodyCenter + Vector2.down * (bodyRadius - radius);
        }

        // Normal walking enemies only: bosses keep one circle, stationary boss props have no behavior, a body no
        // wider than the feet gains nothing from them, and a flier (쥬비, 대왕쥬비) meets terrain with its body, which
        // EnemyFlight lets pass over pits and low obstacles (feet would stop at pits).
        public static bool Needs(GameObject enemy)
        {
            return enemy != null && enemy.GetComponent<EnemyBehaviorContext>() != null &&
                   enemy.GetComponent<BossController>() == null && enemy.GetComponent<EnemyFlight>() == null &&
                   TryGetBody(enemy, out CircleCollider2D body) &&
                   WorldRadiusOf(body) > Radius + 0.001f;
        }

        // Finds or creates the feet child and restores its layer, place and size, and keeps terrain off the body.
        // Returns true when anything changed.
        public static bool Ensure(GameObject enemy, out EnemyFeet feet)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (Layer < 0)
                throw new InvalidOperationException($"The '{LayerName}' layer is missing. Run the Hitbox-2 setup.");
            if (!TryGetBody(enemy, out CircleCollider2D body))
                throw new InvalidOperationException($"{enemy.name} has no solid body circle for Hitbox-2 feet.");

            bool changed = false;
            feet = enemy.GetComponentInChildren<EnemyFeet>(true);
            if (feet == null)
            {
                GameObject feetObject = new(ObjectName, typeof(CircleCollider2D), typeof(EnemyFeet));
                feetObject.transform.SetParent(enemy.transform, false);
                feet = feetObject.GetComponent<EnemyFeet>();
                changed = true;
            }

            // The feet keep their world size under a scaled root, and their bottom meets the bottom of the body.
            Vector3 rootScale = enemy.transform.lossyScale;
            float scale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.y)));
            Vector3 localScale = Vector3.one / scale;
            Vector2 localPosition = body.offset + new Vector2(0f, Radius / scale - body.radius);

            Transform feetTransform = feet.transform;
            CircleCollider2D circle = feet.Collider;
            if (feet.gameObject.layer != Layer ||
                ((Vector2)feetTransform.localPosition - localPosition).sqrMagnitude > 0.000001f ||
                (feetTransform.localScale - localScale).sqrMagnitude > 0.000001f ||
                !Mathf.Approximately(circle.radius, Radius) || circle.offset != Vector2.zero || circle.isTrigger ||
                !circle.enabled || !feet.gameObject.activeSelf)
            {
                feet.gameObject.layer = Layer;
                feet.gameObject.SetActive(true);
                feetTransform.localPosition = localPosition;
                feetTransform.localScale = localScale;
                circle.radius = Radius;
                circle.offset = Vector2.zero;
                circle.isTrigger = false;
                circle.enabled = true;
                changed = true;
            }

            int terrain = TerrainMask;
            if ((body.excludeLayers.value & terrain) != terrain)
            {
                body.excludeLayers = body.excludeLayers.value | terrain;
                changed = true;
            }

            return changed;
        }
    }
}
