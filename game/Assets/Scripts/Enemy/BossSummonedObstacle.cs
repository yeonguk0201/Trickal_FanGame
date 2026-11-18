using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // An obstacle a boss puts into its arena: 부스러기's cream and dough, 새마음금고's gold pillars. It is a
    // DestructibleObstacle, so player attacks break it after a fixed number of hits whatever their damage, and it has
    // no Health: kill counts, on-hit and on-kill items, status effects and homing never treat it as an enemy.
    // The boss breaks it by walking into it, and pattern code breaks the ones under a landing or a falling lump.
    // A broken one is destroyed rather than left inactive like a room obstacle, because no room state remembers it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleObstacle))]
    public sealed class BossSummonedObstacle : MonoBehaviour
    {
        private static readonly List<BossSummonedObstacle> live = new();

        [Tooltip("켜면 보스 탄이 이 장애물에 막힙니다.")]
        [SerializeField] private bool blocksBossProjectiles;

        private DestructibleObstacle obstacle;
        private bool listening;

        public bool BlocksBossProjectiles => blocksBossProjectiles;
        public DestructibleObstacle Obstacle => obstacle != null ? obstacle : obstacle = GetComponent<DestructibleObstacle>();
        public bool IsBroken => Obstacle == null || Obstacle.IsBroken;

        public static IReadOnlyList<BossSummonedObstacle> Live
        {
            get
            {
                live.RemoveAll(item => item == null || item.IsBroken);
                return live;
            }
        }

        // Also registers it: Edit Mode tools create obstacles without Awake or OnEnable running.
        public void Configure(bool configuredBlocksBossProjectiles)
        {
            blocksBossProjectiles = configuredBlocksBossProjectiles;
            Register();
        }

        private void Awake() => Register();

        private void OnEnable() => Register();

        private void Register()
        {
            if (!listening && Obstacle != null)
            {
                Obstacle.Broken += OnBroken;
                listening = true;
            }
            if (!IsBroken && !live.Contains(this)) live.Add(this);
        }

        private void OnDisable() => live.Remove(this);

        private void OnDestroy()
        {
            live.Remove(this);
            if (obstacle != null) obstacle.Broken -= OnBroken;
        }

        private void OnCollisionEnter2D(Collision2D collision) =>
            TryBreakForBoss(collision.collider.GetComponentInParent<BossController>());

        // A kinematic boss that starts overlapping (a pillar rising under it) reports Stay, not Enter.
        private void OnCollisionStay2D(Collision2D collision) =>
            TryBreakForBoss(collision.collider.GetComponentInParent<BossController>());

        public bool TryBreakForBoss(BossController source) => source != null && Break();

        // Breaks it at once, without counting hits or dropping anything.
        public bool Break()
        {
            if (IsBroken || !Obstacle.TryDestroyByBomb()) return false;
            if (!Application.isPlaying && this != null) DestroyImmediate(gameObject);
            return true;
        }

        // Breaks every boss obstacle whose body the circle touches. Returns how many broke.
        public static int BreakInCircle(Vector2 center, float radius)
        {
            int broken = 0;
            foreach (BossSummonedObstacle item in new List<BossSummonedObstacle>(Live))
            {
                if (item == null) continue;
                float reach = Mathf.Max(0f, radius) + item.BodyRadius;
                if (((Vector2)item.transform.position - center).sqrMagnitude > reach * reach) continue;
                if (item.Break()) broken++;
            }
            return broken;
        }

        private float BodyRadius
        {
            get
            {
                Collider2D body = GetComponent<Collider2D>();
                if (body is CircleCollider2D circle)
                    return circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
                return body != null ? Mathf.Max(body.bounds.extents.x, body.bounds.extents.y) : 0.4f;
            }
        }

        private void OnBroken(DestructibleObstacle _)
        {
            // Edit Mode callers are inside DestructibleObstacle.Break here; Break() above destroys it after that.
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
