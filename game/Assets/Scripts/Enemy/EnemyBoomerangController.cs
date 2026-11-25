using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// 산사모-냉정 (2026-11-23): throws a shot along a slim teardrop: out on one side, around the player, back on the
    /// other. The round end of the teardrop is placed on the player, so one who stands still is hit at the far point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBoomerangController : EnemyStandoffShooter
    {
        [SerializeField, Min(0.1f)] private float minimumRange = 1.5f;
        [SerializeField, Min(0.1f)] private float maximumRange = 7.5f;
        // Half width as a share of the throw's length, so the teardrop keeps its angle at any distance.
        [SerializeField, Range(0f, 1f)] private float widthRatio = 0.2f;
        [SerializeField, Min(0.1f)] private float flightDuration = 2.4f;

        private int nextSide = 1;

        public float MaximumRange => maximumRange;
        public float WidthRatio => widthRatio;
        public float FlightDuration => flightDuration;

        public void ConfigureBoomerang(float rangeMin, float rangeMax, float ratio, float duration)
        {
            minimumRange = Mathf.Max(0.1f, rangeMin);
            maximumRange = Mathf.Max(minimumRange, rangeMax);
            widthRatio = Mathf.Clamp01(ratio);
            flightDuration = Mathf.Max(0.1f, duration);
        }

        protected override void Fire(Vector2 origin, Vector2 direction)
        {
            // Aimed from where the shot really starts, which a wall can shift away from the body.
            Vector2 toTarget = (Vector2)Behavior.Target.position - origin;
            Vector2 aim = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction;
            float range = ThrowRange(toTarget.magnitude, minimumRange, maximumRange);
            EnemyProjectile shot = CreateShot(origin, aim, flightDuration);
            shot.gameObject.AddComponent<EnemyBoomerangPath>()
                .Begin(transform, origin, aim, range, range * widthRatio * nextSide, flightDuration);
            nextSide = -nextSide;
        }

        public static float ThrowRange(float distance, float rangeMin, float rangeMax) =>
            Mathf.Clamp(distance, rangeMin, rangeMax);
    }
}
