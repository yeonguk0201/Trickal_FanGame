using System;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Hitbox-2: a normal enemy's terrain collider, a small circle at the feet on its own child and layer, the same
    // split as the player's Hitbox-0. The EnemyFeet layer collides with Environment and Pit only, and the body circle
    // on the root excludes those two layers, so walls, obstacles, chests and pits stop the feet while the body stays
    // the hurtbox and contact collider. Bosses walk on feet too since Boss-Pattern-0 (2026-11-19): with one large
    // circle they stopped far from walls. Both colliders share the root Rigidbody2D, so collision messages still
    // reach the enemy root.
    // Boss-Pattern-0 (2026-11-19): a boss sets its feet per drawing (BossController): a half width and half height,
    // a sideways offset for the drawing as authored (mirrored when the drawing flips) and a drop below the body. Equal
    // width and height keep the single circle. Different ones add a capsule (rounded ends, close to an ellipse) on the
    // same feet object; the circle stays, sized to the smaller half so it always lies inside the capsule, and every
    // caller that reads the circle keeps working.
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
        private BossMovementAnimator facingSource;
        private bool facingResolved;
        private float unflippedLocalX;

        public static int Layer => LayerMask.NameToLayer(LayerName);
        public static int TerrainMask => LayerMask.GetMask(CollisionLayers);

        // World half width and half height of the feet: a boss uses its own, everyone else a circle of Radius.
        public static Vector2 HalfSizeFor(GameObject enemy)
        {
            BossController boss = enemy != null ? enemy.GetComponent<BossController>() : null;
            return boss != null ? new Vector2(boss.FeetHalfWidth, boss.FeetHalfHeight) : new Vector2(Radius, Radius);
        }

        // The circle part of the feet: the smaller half size, so it stays inside a capsule.
        public static float RadiusFor(GameObject enemy)
        {
            Vector2 half = HalfSizeFor(enemy);
            return Mathf.Min(half.x, half.y);
        }

        // Sideways offset for the drawing as authored (unflipped), in world units. Only a boss sets it.
        public static float OffsetXFor(GameObject enemy)
        {
            BossController boss = enemy != null ? enemy.GetComponent<BossController>() : null;
            return boss != null ? boss.FeetOffsetX : 0f;
        }

        // How far below the body bottom the feet sit, in world units (negative raises them). Only a boss sets it:
        // some boss drawings stand lower than their body circle, so their feet move to the drawn soles.
        public static float DropFor(GameObject enemy)
        {
            BossController boss = enemy != null ? enemy.GetComponent<BossController>() : null;
            return boss != null ? boss.FeetDrop : 0f;
        }

        public static bool IsCapsule(Vector2 halfSize) => Mathf.Abs(halfSize.x - halfSize.y) > 0.001f;

        public CircleCollider2D Collider
        {
            get
            {
                if (feetCollider == null) feetCollider = GetComponent<CircleCollider2D>();
                return feetCollider;
            }
        }

        public CapsuleCollider2D Capsule => GetComponent<CapsuleCollider2D>();
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
            GroundShape(enemy, out center, out Vector2 halfSize);
            radius = Mathf.Min(halfSize.x, halfSize.y);
        }

        // The shape an enemy stands on: its center and world half size (equal halves for a circle). Read from the
        // feet transform, not the colliders, so a jump that turns colliders into triggers does not change it.
        public static void GroundShape(GameObject enemy, out Vector2 center, out Vector2 halfSize)
        {
            EnemyFeet feet = enemy.GetComponentInChildren<EnemyFeet>();
            bool boss = enemy.GetComponent<BossController>() != null;
            // A normal enemy whose feet collider is off (잠복형 under the floor) falls back to its body as before.
            if (feet != null && feet.isActiveAndEnabled && feet.Collider != null && (boss || feet.Collider.enabled))
            {
                center = feet.WorldCenter;
                halfSize = boss ? HalfSizeFor(enemy) : Vector2.one * feet.WorldRadius;
                return;
            }

            if (!TryGetBody(enemy, out CircleCollider2D body))
            {
                center = enemy.transform.position;
                halfSize = Vector2.zero;
                return;
            }

            Vector2 bodyCenter = body.transform.TransformPoint(body.offset);
            float bodyRadius = WorldRadiusOf(body);
            Vector2 wanted = HalfSizeFor(enemy);
            halfSize = new Vector2(wanted.x, Mathf.Min(bodyRadius, wanted.y));
            if (!IsCapsule(wanted)) halfSize = Vector2.one * Mathf.Min(bodyRadius, wanted.y);
            center = bodyCenter + new Vector2(OffsetXFor(enemy), -(bodyRadius - halfSize.y + DropFor(enemy)));
        }

        // Walking enemies and bosses (Boss-Pattern-0): stationary boss props have no behavior, a body no wider than
        // the feet gains nothing from them, and a flier (쥬비, 대왕쥬비) meets terrain with its body, which EnemyFlight
        // lets pass over pits and low obstacles (feet would stop at pits).
        public static bool Needs(GameObject enemy)
        {
            return enemy != null && enemy.GetComponent<EnemyBehaviorContext>() != null &&
                   enemy.GetComponent<EnemyFlight>() == null &&
                   TryGetBody(enemy, out CircleCollider2D body) &&
                   WorldRadiusOf(body) > Radius + 0.001f;
        }

        // Where the enemy's root may be so its ground shape stays inside the area. A boss moved by code (not by
        // physics) clamps its root to this, so it nears walls by its feet like a physics-moved enemy: its soles reach
        // the lower edge and its body may overlap the upper wall art.
        public static Rect StandingRootArea(GameObject enemy, Rect area)
        {
            if (enemy == null) return area;
            GroundShape(enemy, out Vector2 center, out Vector2 half);
            Vector2 offset = center - (Vector2)enemy.transform.position;
            float xMin = area.xMin + half.x - offset.x, xMax = area.xMax - half.x - offset.x;
            float yMin = area.yMin + half.y - offset.y, yMax = area.yMax - half.y - offset.y;
            if (xMin > xMax) xMin = xMax = (xMin + xMax) * 0.5f;
            if (yMin > yMax) yMin = yMax = (yMin + yMax) * 0.5f;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // A boss's sideways offset follows its drawing: when the drawing flips to face the other way, the feet move
        // to the mirrored side.
        private void LateUpdate()
        {
            if (!facingResolved)
            {
                facingResolved = true;
                facingSource = OffsetXFor(transform.parent != null ? transform.parent.gameObject : null) != 0f
                    ? GetComponentInParent<BossMovementAnimator>()
                    : null;
                unflippedLocalX = transform.localPosition.x;
            }

            if (facingSource == null || facingSource.Artwork == null) return;
            Vector3 local = transform.localPosition;
            float wanted = facingSource.Artwork.flipX ? -unflippedLocalX : unflippedLocalX;
            if (!Mathf.Approximately(local.x, wanted)) transform.localPosition = new Vector3(wanted, local.y, local.z);
        }

        // Finds or creates the feet child and restores its layer, place, size and shape, and keeps terrain off the
        // body. Returns true when anything changed.
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

            // The feet keep their world size under a scaled root, and their bottom meets the bottom of the body
            // (lowered by the drop). Sizes below are in world units because the feet undo the root scale.
            Vector3 rootScale = enemy.transform.lossyScale;
            float scale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.y)));
            Vector3 localScale = Vector3.one / scale;
            Vector2 half = HalfSizeFor(enemy);
            float feetRadius = Mathf.Min(half.x, half.y);
            Vector2 localPosition = body.offset + new Vector2(OffsetXFor(enemy) / scale,
                (half.y - DropFor(enemy)) / scale - body.radius);

            Transform feetTransform = feet.transform;
            CircleCollider2D circle = feet.Collider;
            if (feet.gameObject.layer != Layer ||
                ((Vector2)feetTransform.localPosition - localPosition).sqrMagnitude > 0.000001f ||
                (feetTransform.localScale - localScale).sqrMagnitude > 0.000001f ||
                !Mathf.Approximately(circle.radius, feetRadius) || circle.offset != Vector2.zero || circle.isTrigger ||
                !circle.enabled || !feet.gameObject.activeSelf)
            {
                feet.gameObject.layer = Layer;
                feet.gameObject.SetActive(true);
                feetTransform.localPosition = localPosition;
                feetTransform.localScale = localScale;
                circle.radius = feetRadius;
                circle.offset = Vector2.zero;
                circle.isTrigger = false;
                circle.enabled = true;
                changed = true;
            }

            CapsuleCollider2D capsule = feet.GetComponent<CapsuleCollider2D>();
            if (IsCapsule(half))
            {
                if (capsule == null)
                {
                    capsule = feet.gameObject.AddComponent<CapsuleCollider2D>();
                    changed = true;
                }

                CapsuleDirection2D direction = half.x >= half.y
                    ? CapsuleDirection2D.Horizontal
                    : CapsuleDirection2D.Vertical;
                Vector2 size = half * 2f;
                if (capsule.direction != direction || (capsule.size - size).sqrMagnitude > 0.000001f ||
                    capsule.offset != Vector2.zero || capsule.isTrigger || !capsule.enabled)
                {
                    capsule.direction = direction;
                    capsule.size = size;
                    capsule.offset = Vector2.zero;
                    capsule.isTrigger = false;
                    capsule.enabled = true;
                    changed = true;
                }
            }
            else if (capsule != null)
            {
                if (Application.isPlaying) Destroy(capsule);
                else DestroyImmediate(capsule, true);
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
