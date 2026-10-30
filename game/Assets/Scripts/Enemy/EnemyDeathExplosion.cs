using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-7: an enemy that leaves a blast where it dies (부스러기가 소환한 쫄따구). Whatever killed it, the blast
    // warns for a moment and then hurts only the player. The blast is its own object, so the dead enemy is released
    // from its room at once and never holds a clear back.
    // Enemy-10 (자폭형) reuses it with three options: the player damage follows a damage tier and the floor, the
    // blast also hurts other enemies and breaks obstacles like a bomb, and a primed enemy (SelfDestructEnemy) that
    // dies mid-telegraph keeps only the time it had left.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyDeathExplosion : MonoBehaviour
    {
        public const float DefaultTelegraphDuration = 0.6f;
        public const float DefaultRadius = 1.2f;
        public const int DefaultDamageUnits = HealthUnits.UnitsPerHeart;

        [SerializeField, Min(0f)] private float telegraphDuration = DefaultTelegraphDuration;
        [SerializeField, Min(0.01f)] private float radius = DefaultRadius;
        [SerializeField, Min(1)] private int damageUnits = DefaultDamageUnits;
        // Enemy-10: when set, the player damage is this tier on the enemy's floor instead of damageUnits.
        [SerializeField] private bool usesDamageTier;
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Medium;
        // Enemy-10: when set, the blast also hurts other enemies and breaks obstacles the way a bomb does.
        [SerializeField] private bool hitsEnemiesAndObstacles;
        [SerializeField, Min(0f)] private float enemyDamage;

        private Health health;
        private BossController owner;
        private float primedAt = float.NaN;
        private bool telegraphFinished;

        public float TelegraphDuration => telegraphDuration;
        public float Radius => radius;
        public int DamageUnits => damageUnits;
        public bool UsesDamageTier => usesDamageTier;
        public EnemyDamageTier DamageTier => damageTier;
        public bool HitsEnemiesAndObstacles => hitsEnemiesAndObstacles;
        public float EnemyDamage => enemyDamage;
        public bool IsPrimed => !float.IsNaN(primedAt);
        public float PrimedAt => primedAt;
        public float PrimedExplodeAt => IsPrimed ? primedAt + telegraphDuration : float.NaN;
        public EnemyDeathBlast LastBlast { get; private set; }
        public event Action<EnemyDeathBlast> BlastSpawned;

        public void Configure(float configuredTelegraphDuration, float configuredRadius, int configuredDamageUnits)
        {
            telegraphDuration = Mathf.Max(0f, configuredTelegraphDuration);
            radius = Mathf.Max(0.01f, configuredRadius);
            damageUnits = Mathf.Max(1, configuredDamageUnits);
        }

        public void ConfigureDamageTier(bool configuredUsesDamageTier, EnemyDamageTier configuredDamageTier)
        {
            usesDamageTier = configuredUsesDamageTier;
            damageTier = configuredDamageTier;
        }

        public void ConfigureEnemyAndObstacleHits(bool configuredHits, float configuredEnemyDamage)
        {
            hitsEnemiesAndObstacles = configuredHits;
            enemyDamage = Mathf.Max(0f, configuredEnemyDamage);
        }

        // A boss that owns the dying enemy also owns its blast, so the blast is removed with the boss's other objects.
        public void ConfigureOwner(BossController configuredOwner)
        {
            owner = configuredOwner;
        }

        // Enemy-10: the telegraph started while the enemy was still alive. Dying before it ends keeps the time left.
        public void Prime(float currentTime)
        {
            if (!IsPrimed) primedAt = currentTime;
        }

        // Enemy-10: the enemy's own telegraph ran out, so the death it causes explodes at once.
        public void FinishTelegraph()
        {
            if (IsPrimed) telegraphFinished = true;
        }

        // The player damage this enemy's blast deals now.
        public int ResolveDamageUnits() => usesDamageTier
            ? HealthUnits.GetEnemyDamageUnits(EnemyVariant.ResolveDamageTier(gameObject, damageTier),
                EnemyFloorLevel.Resolve(gameObject))
            : damageUnits;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            // The enemy's own picture, not a ground shadow drawn under it.
            TrickalFanGame.Frontend.FairyKingdomArtworkView artwork =
                GetComponent<TrickalFanGame.Frontend.FairyKingdomArtworkView>();
            SpriteRenderer visual = artwork != null && artwork.Visual != null
                ? artwork.Visual
                : GetComponentInChildren<SpriteRenderer>();
            float now = Time.time;
            float remaining = telegraphFinished ? 0f
                : IsPrimed ? Mathf.Max(0f, PrimedExplodeAt - now)
                : telegraphDuration;
            LastBlast = EnemyDeathBlast.Spawn(transform.position, transform.parent, gameObject, visual,
                remaining, radius, ResolveDamageUnits(), now);
            // A primed telegraph continues from where it was instead of starting over.
            if (IsPrimed) LastBlast.ContinueTelegraphFrom(primedAt);
            if (hitsEnemiesAndObstacles) LastBlast.ConfigureEnemyAndObstacleHits(enemyDamage);
            // A defeated boss is gone; its released minions still explode on their own.
            if (owner != null) owner.RegisterOwnedObject(LastBlast.gameObject);
            BlastSpawned?.Invoke(LastBlast);
            // A finished telegraph goes off now, not a frame later.
            if (remaining <= 0f) LastBlast.Tick(now);
        }
    }
}
