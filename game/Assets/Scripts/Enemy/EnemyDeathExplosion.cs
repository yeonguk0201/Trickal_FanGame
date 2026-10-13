using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-7: an enemy that leaves a blast where it dies (부스러기가 소환한 쫄따구). Whatever killed it, the blast
    // warns for a moment and then hurts only the player. The blast is its own object, so the dead enemy is released
    // from its room at once and never holds a clear back.
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

        private Health health;
        private BossController owner;

        public float TelegraphDuration => telegraphDuration;
        public float Radius => radius;
        public int DamageUnits => damageUnits;
        public EnemyDeathBlast LastBlast { get; private set; }
        public event Action<EnemyDeathBlast> BlastSpawned;

        public void Configure(float configuredTelegraphDuration, float configuredRadius, int configuredDamageUnits)
        {
            telegraphDuration = Mathf.Max(0f, configuredTelegraphDuration);
            radius = Mathf.Max(0.01f, configuredRadius);
            damageUnits = Mathf.Max(1, configuredDamageUnits);
        }

        // A boss that owns the dying enemy also owns its blast, so the blast is removed with the boss's other objects.
        public void ConfigureOwner(BossController configuredOwner)
        {
            owner = configuredOwner;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
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
            LastBlast = EnemyDeathBlast.Spawn(transform.position, transform.parent, gameObject, visual,
                telegraphDuration, radius, damageUnits, Time.time);
            // A defeated boss is gone; its released minions still explode on their own.
            if (owner != null) owner.RegisterOwnedObject(LastBlast.gameObject);
            BlastSpawned?.Invoke(LastBlast);
        }
    }
}
