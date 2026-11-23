using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>Steers an EnemyProjectile along the boomerang path; hits, walls and expiry stay with the projectile.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyBoomerangPath : MonoBehaviour
    {
        // sin²·cos peaks at 2/(3√3); this makes the half width the real widest offset.
        private const float WidthNormal = 2.598076f;
        private const float SpinDegreesPerSecond = 540f;

        private Rigidbody2D body;
        private Transform owner;
        private Vector2 origin, forward, side;
        private float range, duration, startedAt;

        // Offset from the thrower at progress 0~1: (along the throw, across it).
        public static Vector2 PathOffset(float progress, float range, float halfWidth)
        {
            float angle = Mathf.Clamp01(progress) * Mathf.PI;
            float sine = Mathf.Sin(angle);
            return new Vector2(range * sine * sine, halfWidth * WidthNormal * sine * sine * Mathf.Cos(angle));
        }

        public void Begin(Transform thrower, Vector2 start, Vector2 direction, float configuredRange, float halfWidth,
            float configuredDuration)
        {
            body = GetComponent<Rigidbody2D>();
            owner = thrower;
            origin = start;
            forward = direction.normalized;
            side = new Vector2(-forward.y, forward.x) * halfWidth;
            range = configuredRange;
            duration = configuredDuration;
            startedAt = Time.time;
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            float progress = (Time.time + Time.fixedDeltaTime - startedAt) / duration;
            // The way out stays fixed so the far point lands where it was aimed; the way back ends on the thrower,
            // wherever it has walked to.
            float back = Mathf.Clamp01(progress * 2f - 1f);
            Vector2 home = owner != null ? Vector2.Lerp(origin, owner.position, back * back) : origin;
            Vector2 offset = PathOffset(progress, range, 1f);
            body.linearVelocity = (home + forward * offset.x + side * offset.y - body.position) / Time.fixedDeltaTime;
            transform.Rotate(0f, 0f, SpinDegreesPerSecond * Time.fixedDeltaTime);
        }
    }
}
