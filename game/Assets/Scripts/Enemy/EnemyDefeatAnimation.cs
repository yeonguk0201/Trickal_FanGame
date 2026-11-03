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

        private Health health;
        private Vector2? lastHitFrom;

        public Sprite[] Frames => frames;
        public float SizeMultiplier => sizeMultiplier;

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
