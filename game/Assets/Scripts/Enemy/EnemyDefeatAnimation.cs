using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only. When the enemy dies it leaves a separate picture with no collider that is knocked away
    /// from the last hit, plays the drawn defeat frames and fades out. The enemy itself is still removed at once,
    /// so room clear, rewards and collisions are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyDefeatAnimation : MonoBehaviour
    {
        // The drawings fall back toward the right; a defeat moving left is mirrored.
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        // Visible body height / visible height of the first frame, measured by the Editor setup, so the fallen
        // character matches the standing one even when a thrown prop makes the frame taller.
        [SerializeField, Min(0.1f)] private float sizeMultiplier = 1f;
        // Share of the full knock distance. A boss bursts or slumps where it stood instead of flying away.
        [SerializeField, Range(0f, 1f)] private float knockScale = 1f;

        private Health health;
        private Vector2? lastHitFrom;

        public Sprite[] Frames => frames;
        public float SizeMultiplier => sizeMultiplier;
        public float KnockScale => knockScale;
        public bool HasFrames => frames != null && frames.Length > 0;

        // A death warning (a blast champion or a 쫄따구 blast) must not leave a second, standing copy of the body
        // while the drawn defeat already shows it falling (2026-11-06 user decision): the warning area alone stays.
        // It is asked during death, after the enemy is already marked for removal (which turns its components off),
        // so only the frames count, not whether the component is still enabled.
        public static bool PlaysOn(GameObject enemy) =>
            enemy != null && enemy.TryGetComponent(out EnemyDefeatAnimation defeat) && defeat.HasFrames;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.DamageResolved += OnDamageResolved;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.DamageResolved -= OnDamageResolved;
            health.Died -= OnDied;
        }

        private void OnDamageResolved(DamageContext context, DamageResult result)
        {
            if (context.Source != null) lastHitFrom = context.Source.transform.position;
        }

        private void OnDied()
        {
            if (Application.isPlaying) SpawnView(KnockDirection());
        }

        // Leaves the defeat picture knocked toward direction. Also used by the Editor verifier.
        public EnemyDefeatView SpawnView(Vector2 direction)
        {
            if (frames == null || frames.Length == 0) return null;
            SpriteRenderer body = FindBody();
            return body != null ? EnemyDefeatView.Spawn(this, body, direction) : null;
        }

        private SpriteRenderer FindBody()
        {
            var artwork = GetComponent<FairyKingdomArtworkView>();
            if (artwork != null && artwork.Visual != null) return artwork.Visual;
            SpriteRenderer own = GetComponent<SpriteRenderer>();
            if (own != null && own.sprite != null) return own;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>())
                if (candidate.sprite != null && candidate.gameObject.name != GroundShadow.ObjectName) return candidate;
            return null;
        }

        private Vector2 KnockDirection()
        {
            Vector2 from;
            if (lastHitFrom.HasValue) from = lastHitFrom.Value;
            else
            {
                PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                if (player == null) return Vector2.right;
                from = player.transform.position;
            }
            Vector2 away = (Vector2)transform.position - from;
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
        }
    }
}
