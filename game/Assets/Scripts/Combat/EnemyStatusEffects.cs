using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Passive-0 §4.7·§4.8: status effects on an enemy. Added to the enemy the first time one is applied.
    // Poison stacks up; each application adds a stack and renews the duration, and every stack ends together.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private const float TickTimeTolerance = 0.0001f;

        // Placeholder look until status effects have artwork: a green copy of the sprite drawn over the enemy.
        private static readonly Color PoisonTint = new(0.3f, 1f, 0.25f, 0.5f);

        private Health health;
        private GameObject poisonSource;
        private float poisonInterval;
        private SpriteRenderer tintedRenderer;
        private SpriteRenderer tintRenderer;

        public int PoisonStacks { get; private set; }
        public bool IsPoisoned => PoisonStacks > 0;
        // Damage each stack deals per tick, fixed from the attack damage when poison was last applied.
        public float PoisonTickDamagePerStack { get; private set; }
        public float PoisonEndTime { get; private set; }
        public float NextPoisonTickTime { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            Tick(Time.time);
        }

        private void LateUpdate()
        {
            SyncTint();
        }

        private void OnDisable()
        {
            Clear();
        }

        public static bool RollsPoison(PoisonSettings settings, float roll)
        {
            return settings.IsEnabled && roll < settings.Chance;
        }

        public static bool TryApplyPoison(
            Health target,
            GameObject source,
            float attackDamage,
            PoisonSettings settings,
            float roll)
        {
            if (target == null || target.IsDead || attackDamage <= 0f || !RollsPoison(settings, roll))
            {
                return false;
            }

            EnemyStatusEffects effects = target.GetComponent<EnemyStatusEffects>();
            if (effects == null)
            {
                effects = target.gameObject.AddComponent<EnemyStatusEffects>();
            }

            effects.ApplyPoison(source, attackDamage, settings, Time.time);
            return true;
        }

        public void ApplyPoison(GameObject source, float attackDamage, PoisonSettings settings, float currentTime)
        {
            if (health == null)
            {
                Awake();
            }

            if (health.IsDead || !settings.IsEnabled)
            {
                return;
            }

            // The first tick lands one interval after poison starts. Renewing keeps the running tick schedule.
            if (!IsPoisoned)
            {
                NextPoisonTickTime = currentTime + settings.IntervalSeconds;
            }

            poisonSource = source;
            poisonInterval = settings.IntervalSeconds;
            PoisonStacks = Mathf.Min(settings.MaximumStacks, PoisonStacks + 1);
            PoisonTickDamagePerStack = attackDamage * settings.TickDamageRatio;
            PoisonEndTime = currentTime + settings.DurationSeconds;
            ShowTint();
        }

        public void Tick(float currentTime)
        {
            if (!IsPoisoned)
            {
                return;
            }

            while (IsPoisoned && !health.IsDead && currentTime >= NextPoisonTickTime &&
                   NextPoisonTickTime <= PoisonEndTime + TickTimeTolerance)
            {
                NextPoisonTickTime += poisonInterval;
                // Periodic damage: no critical hit, distance damage, knockback, basic attack hit event or new
                // status effect. The player source keeps the kill credited to the player.
                health.TakeDamage(new DamageContext(
                    poisonSource,
                    DamageSourceType.PlayerStatusEffect,
                    PoisonTickDamagePerStack * PoisonStacks,
                    deliveryType: DamageDeliveryType.Periodic));
            }

            if (health.IsDead || currentTime >= PoisonEndTime)
            {
                Clear();
            }
        }

        public void Clear()
        {
            PoisonStacks = 0;
            PoisonTickDamagePerStack = 0f;
            poisonSource = null;
            if (tintRenderer != null)
            {
                tintRenderer.enabled = false;
            }
        }

        private void ShowTint()
        {
            if (tintRenderer == null)
            {
                tintedRenderer = GetComponentInChildren<SpriteRenderer>();
                if (tintedRenderer == null)
                {
                    return;
                }

                GameObject tint = new("Poison Tint");
                tint.transform.SetParent(tintedRenderer.transform, false);
                tintRenderer = tint.AddComponent<SpriteRenderer>();
                tintRenderer.color = PoisonTint;
            }

            tintRenderer.enabled = true;
            SyncTint();
        }

        private void SyncTint()
        {
            if (tintRenderer == null || !tintRenderer.enabled || tintedRenderer == null)
            {
                return;
            }

            tintRenderer.sprite = tintedRenderer.sprite;
            tintRenderer.flipX = tintedRenderer.flipX;
            tintRenderer.flipY = tintedRenderer.flipY;
            tintRenderer.sortingLayerID = tintedRenderer.sortingLayerID;
            tintRenderer.sortingOrder = tintedRenderer.sortingOrder + 1;
        }
    }
}
