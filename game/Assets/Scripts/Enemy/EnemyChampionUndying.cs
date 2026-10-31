using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-2 (user decision, 2026-10-24, docs/28 §8): the 불사(사이온) champion. The first hit that would kill it
    // (an execution included) leaves it standing with 85% of its maximum health as undying life, which runs out
    // over 3 seconds; hits in that time take it down faster. It keeps moving and attacking 30% faster, drawn grey
    // and see-through. The death happens when the life runs out, so kill effects, the room clear and splitting all
    // wait for it. Its own self-destruct is not deferred. Subscribed from EnemyVariant.Apply.
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyChampionUndying : MonoBehaviour
    {
        public const float LifeShare = 0.85f;
        public const float Duration = 3f;
        // Ordinary movement and the wait between attacks (telegraphs keep their length).
        public const float SpeedMultiplier = 1.3f;
        // Multiplies the body's own colour, so attack tints (자폭형 reddening, 잠복형 states) still show through.
        public static readonly Color BodyTint = new(0.62f, 0.62f, 0.66f, 0.55f);

        private Health health;
        private SpriteRenderer visual;
        private DamageContext killingBlow;
        private float lastTickTime;
        private Color baseColor;
        private Color writtenColor;
        private bool hasWrittenColor;

        public bool HasTriggered { get; private set; }
        public bool IsActive { get; private set; }
        public float StartTime { get; private set; }
        public float EndTime => StartTime + Duration;
        public float StartLife { get; private set; }
        public DamageContext KillingBlow => killingBlow;

        public static bool IsActiveOn(Component enemy) =>
            enemy != null && enemy.TryGetComponent(out EnemyChampionUndying undying) && undying.IsActive;

        public void Initialize()
        {
            if (health == null) health = GetComponent<Health>();
            health.SetLethalDeferral(Defer);
            health.DamageApplied -= OnDamageApplied;
            health.DamageApplied += OnDamageApplied;
            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.SetLethalDeferral(null);
            health.DamageApplied -= OnDamageApplied;
            health.Died -= OnDied;
        }

        private float Defer(DamageContext context)
        {
            // Once only, and never for its own blow (자폭형 blowing itself up).
            if (HasTriggered || context.Source == gameObject) return 0f;
            HasTriggered = true;
            IsActive = true;
            StartTime = lastTickTime = Time.time;
            StartLife = health.MaxHealth * LifeShare;
            killingBlow = context;
            return StartLife;
        }

        // The last player hit takes the kill credit; the downing blow keeps it when the player never hits again.
        private void OnDamageApplied(DamageContext context, float removed, float current)
        {
            if (IsActive && PlayerCombatEvents.IsPlayerDamage(context.SourceType)) killingBlow = context;
        }

        private void OnDied()
        {
            IsActive = false;
        }

        private void Update()
        {
            Tick(Time.time);
        }

        // Public so Editor verification can step the drain without a running Game view.
        public void Tick(float now)
        {
            if (!IsActive || health == null || health.IsDead) return;
            if (now >= EndTime)
            {
                health.DrainHealth(health.CurrentHealth, killingBlow);
                return;
            }

            float elapsed = now - lastTickTime;
            lastTickTime = now;
            if (elapsed > 0f) health.DrainHealth(StartLife / Duration * elapsed, killingBlow);
        }

        private void LateUpdate()
        {
            if (!IsActive) return;
            if (visual == null) visual = EnemyChampionOutline.ResolveBodySprite(gameObject);
            if (visual == null) return;
            // Start again from the owner's colour whenever something else rewrote it this frame.
            Color current = visual.color;
            if (!hasWrittenColor || current != writtenColor) baseColor = current;
            writtenColor = baseColor * BodyTint;
            visual.color = writtenColor;
            hasWrittenColor = true;
        }
    }
}
