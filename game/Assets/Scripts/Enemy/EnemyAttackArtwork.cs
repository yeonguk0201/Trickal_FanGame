using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>Drawn attack poses, driven by the controller's existing combat phases.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(EnemyAttackPresentation))]
    public sealed class EnemyAttackArtwork : MonoBehaviour
    {
        [SerializeField] private Sprite referenceSprite;
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite projectileSprite;
        private SpriteRenderer source;
        private Sprite original;
        private EnemyAttackPresentation presentation;
        private EnemyBehaviorContext behavior;
        private ChargingEnemyController charger;
        private LongRangeSniperController sniper;
        private MeleeEnemyAttack melee;
        private Health health;
        private KnockbackReceiver knockback;

        public int FrameCount => frames.Length;
        public Sprite GetFrame(int index) => frames[index];
        public Sprite ProjectileSprite => MatchesArtwork ? projectileSprite : null;
        private bool MatchesArtwork { get { Cache(); return original == referenceSprite; } }

        public void Configure(Sprite reference, Sprite[] poses, Sprite projectile = null)
        {
            referenceSprite = reference;
            frames = poses ?? System.Array.Empty<Sprite>();
            projectileSprite = projectile;
        }

        private void Awake() => Cache();

        private void Cache()
        {
            if (source != null) return;
            source = GetComponent<SpriteRenderer>();
            original = source.sprite;
            presentation = GetComponent<EnemyAttackPresentation>();
            behavior = GetComponent<EnemyBehaviorContext>();
            charger = GetComponent<ChargingEnemyController>();
            sniper = GetComponent<LongRangeSniperController>();
            melee = GetComponent<MeleeEnemyAttack>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
        }

        // Movement owns the renderer and calls this before rendering a stride or hop.
        public bool TryRender()
        {
            if (!isActiveAndEnabled || !MatchesArtwork || frames.Length != 4 || presentation == null ||
                presentation.Phase == EnemyAttackPhase.Idle || (health != null && health.IsDead) ||
                (knockback != null && knockback.IsActive)) return false;
            Vector2 direction = charger != null ? charger.LockedDirection :
                sniper != null ? sniper.LockedDirection :
                melee != null && melee.LockedDirection != Vector2.zero ? melee.LockedDirection :
                behavior != null && behavior.Target != null ?
                    (Vector2)(behavior.Target.position - transform.position) : Vector2.zero;
            if (Mathf.Abs(direction.x) > 0.001f) source.flipX = direction.x > 0f;
            source.sprite = frames[(int)presentation.Phase];
            return source.sprite != null;
        }
    }
}
