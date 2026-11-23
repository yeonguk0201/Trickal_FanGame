using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>불효자손-우울 (2026-11-23): throws three shots in a fan at the player.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyFanShooterController : EnemyStandoffShooter
    {
        public const int ShotCount = 3;

        [SerializeField, Range(1f, 60f)] private float spreadDegrees = 24f;
        [SerializeField, Min(0.01f)] private float shotLifetime = 1.6f;

        public float SpreadDegrees => spreadDegrees;
        public float ShotLifetime => shotLifetime;

        public void ConfigureFan(float spread, float lifetime)
        {
            spreadDegrees = Mathf.Clamp(spread, 1f, 60f);
            shotLifetime = Mathf.Max(0.01f, lifetime);
        }

        public static Vector2 FanDirection(Vector2 forward, int index, float spread)
        {
            float radians = (index - (ShotCount - 1) * 0.5f) * spread * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            return new Vector2(forward.x * cos - forward.y * sin, forward.x * sin + forward.y * cos);
        }

        protected override void Fire(Vector2 origin, Vector2 direction)
        {
            for (int i = 0; i < ShotCount; i++)
                CreateShot(origin, FanDirection(direction, i, spreadDegrees), shotLifetime);
        }
    }
}
