using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Health), typeof(KnockbackReceiver))]
    public sealed class BossController : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private string displayName = "보스";
        [SerializeField, Min(1)] private int phaseCount = 1;

        [SerializeField, Min(0.1f)] private float attackInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 4f;
        [SerializeField, Min(0.01f)] private float projectileDamage = 2f;

        private Health health;
        private KnockbackReceiver knockback;
        private Transform player;
        private float nextAttackTime;
        private int currentPhase = 1;
        public event Action Died;
        public event Action<int, int> PhaseChanged;

        public float ProjectileDamage => projectileDamage;
        public Health Health => health != null ? health : GetComponent<Health>();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "보스" : displayName.Trim();
        public int CurrentPhase => currentPhase;
        public int PhaseCount => Mathf.Max(1, phaseCount);

        public bool IsActionSuppressed =>
            health != null && (health.IsDead || (knockback != null && knockback.IsActive));

        public void SetProjectileDamage(float configuredProjectileDamage)
        {
            projectileDamage = Mathf.Max(0.01f, configuredProjectileDamage);
        }

        public void ConfigureHud(string configuredDisplayName, int configuredPhaseCount)
        {
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName) ? "보스" : configuredDisplayName.Trim();
            phaseCount = Mathf.Max(1, configuredPhaseCount);
            SetPhase(Mathf.Min(currentPhase, phaseCount));
        }

        public bool SetPhase(int phase)
        {
            int nextPhase = Mathf.Clamp(phase, 1, PhaseCount);
            if (currentPhase == nextPhase)
            {
                return false;
            }

            currentPhase = nextPhase;
            PhaseChanged?.Invoke(CurrentPhase, PhaseCount);
            return true;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            currentPhase = Mathf.Clamp(currentPhase, 1, PhaseCount);
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (IsActionSuppressed || Time.time < nextAttackTime) return;
            if (player == null) player = FindFirstObjectByType<PlayerMovement>()?.transform;
            if (player == null) return;

            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            BossProjectile.Create(transform.position, direction * projectileSpeed, gameObject, projectileDamage,
                GetComponent<SpriteRenderer>()?.sprite);
            nextAttackTime = Time.time + attackInterval;
        }

        private void OnDied() => Died?.Invoke();
    }
}
