using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-10: 자폭형 (user decisions, 2026-10-24 and 2026-10-27). It chases the player, and once the player is
    // within the trigger range it stops and warns for its telegraph time (a red circle and a reddening body), then
    // blows itself up. The blast is the crumb minion's death blast (EnemyDeathExplosion), so dying any other way
    // also leaves a blast where it fell: before the telegraph after the full telegraph, during the telegraph after
    // only the time that was left. Knockback still moves a warning enemy, and the blast goes off where it ends up.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(EnemyDeathExplosion), typeof(EnemyBehaviorContext))]
    public sealed class SelfDestructEnemy : MonoBehaviour
    {
        public const float DefaultTriggerRange = 1.2f;
        // Under actors, over the floor, like the death blast's own circle.
        private const int TelegraphSortingOrder = 1;

        [SerializeField, Min(0.1f)] private float triggerRange = DefaultTriggerRange;

        private Health health;
        private EnemyDeathExplosion explosion;
        private EnemyBehaviorContext behavior;
        private SpriteRenderer visual;
        private SpriteRenderer telegraph;

        public float TriggerRange => triggerRange;
        public bool IsPrimed => explosion != null && explosion.IsPrimed;
        public float ExplodeAt => explosion != null ? explosion.PrimedExplodeAt : float.NaN;
        public SpriteRenderer Telegraph => telegraph;
        public SpriteRenderer Visual => visual;

        public void Configure(float configuredTriggerRange)
        {
            triggerRange = Mathf.Max(0.1f, configuredTriggerRange);
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            health = GetComponent<Health>();
            explosion = GetComponent<EnemyDeathExplosion>();
            behavior = GetComponent<EnemyBehaviorContext>();
            FairyKingdomArtworkView artwork = GetComponent<FairyKingdomArtworkView>();
            visual = artwork != null && artwork.Visual != null ? artwork.Visual : GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float currentTime)
        {
            if (health == null) CacheComponents();
            if (health == null || health.IsDead || explosion == null || behavior == null) return;
            if (!IsPrimed)
            {
                // Waits out the spawn delay and knockback, and only a live, alerted target sets it off.
                if (behavior.IsActionSuppressed) return;
                Vector2 offset = behavior.Target.position - transform.position;
                if (offset.sqrMagnitude > triggerRange * triggerRange) return;
                Prime(currentTime);
            }

            float progress = explosion.TelegraphDuration > 0f
                ? Mathf.InverseLerp(explosion.PrimedAt, ExplodeAt, currentTime)
                : 1f;
            if (telegraph != null) telegraph.color = EnemyDeathBlast.TelegraphColor(progress);
            if (visual != null) visual.color = EnemyDeathBlast.BodyColor(progress);
            if (currentTime >= ExplodeAt) Detonate();
        }

        public void Prime(float currentTime)
        {
            if (IsPrimed || health == null || health.IsDead) return;
            explosion.Prime(currentTime);
            behavior.SetControllerMovementSuppressed(true);
            GameObject ring = new("Self Destruct Telegraph");
            ring.transform.SetParent(transform, false);
            // The shared mask is a unit square, so the scale is the diameter (in this enemy's own scale).
            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            ring.transform.localScale = Vector3.one * (explosion.Radius * 2f / scale);
            telegraph = ring.AddComponent<SpriteRenderer>();
            telegraph.sprite = GroundShadow.SharedSprite;
            telegraph.color = EnemyDeathBlast.TelegraphColor(0f);
            telegraph.sortingOrder = TelegraphSortingOrder;
        }

        // The finished telegraph ends the enemy. Its own death leaves the blast, which goes off at once.
        private void Detonate()
        {
            explosion.FinishTelegraph();
            health.TakeDamage(new DamageContext(gameObject, DamageSourceType.Environment,
                health.CurrentHealth + health.CurrentShield + 9999f));
            // The blast draws its own circle. An invulnerable enemy keeps warning and tries again next frame.
            if (health.IsDead && telegraph != null) telegraph.enabled = false;
        }
    }
}
