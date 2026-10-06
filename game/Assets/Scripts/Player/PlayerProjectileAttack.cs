using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Health), typeof(PlayerMovement), typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerCombatEvents), typeof(PlayerActionState))]
    public sealed class PlayerProjectileAttack : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(0f)] private float spawnOffset = 0.65f;
        [SerializeField, Min(0.01f)] private float baseProjectileSpeed = 8f;
        // Range-0: travel distance = flight time × shot speed, about 5.3 units at the base values (8 ÷ 1.5).
        [SerializeField, Min(0.01f)] private float baseProjectileLifetime = 2f / 3f;
        [SerializeField, Min(0f)] private float inheritedVelocityFactor = 0.25f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.35f;
        [SerializeField, Range(0f, 45f)] private float multiShotSpreadAngle = 12f;

        private Health health;
        private PlayerMovement movement;
        private PlayerStats stats;
        private PlayerCombatEvents combatEvents;
        private PlayerActionState actionState;
        private float nextAttackTime;

        public float CurrentDamage => stats.AttackDamage;
        public float ProjectileSpeed => baseProjectileSpeed * stats.ProjectileSpeedMultiplier;
        public float ProjectileLifetime => baseProjectileLifetime * stats.ProjectileLifetimeMultiplier;
        public float ProjectileSizeMultiplier => stats.ProjectileSizeMultiplier;
        public int ProjectileCount => stats.ProjectileCount;
        public int PierceCount => stats.PierceCount;
        public float HealOnKill => stats.HealOnKill;
        public float CurrentHealOnKillAmount => stats.HealOnKillAmount;
        public bool CanAttack => !health.IsDead && actionState.CanBasicAttack;

        public DamageContext CreateDamageContext()
        {
            return stats.CreateDirectDamageContext(gameObject, DamageSourceType.PlayerProjectile);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            stats = GetComponent<PlayerStats>();
            combatEvents = GetComponent<PlayerCombatEvents>();
            actionState = GetComponent<PlayerActionState>();
        }

        private void OnEnable()
        {
            if (combatEvents == null)
            {
                combatEvents = GetComponent<PlayerCombatEvents>();
            }

            combatEvents.EnemyKilled -= OnEnemyKilled;
            combatEvents.EnemyKilled += OnEnemyKilled;
        }

        private void OnDisable()
        {
            if (combatEvents != null)
            {
                combatEvents.EnemyKilled -= OnEnemyKilled;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!CanAttack || Time.time < nextAttackTime || !PlayerAttack.TryReadAttackDirection(out Vector2 direction))
            {
                return;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError("Projectile prefab is not assigned.", this);
                enabled = false;
                return;
            }

            nextAttackTime = Time.time + attackCooldown / stats.AttackSpeed;
            Fire(direction);
        }

        private void Fire(Vector2 direction)
        {
            int projectileCount = ProjectileCount;
            float centerIndex = (projectileCount - 1) * 0.5f;
            for (int index = 0; index < projectileCount; index++)
            {
                float angle = (index - centerIndex) * multiShotSpreadAngle;
                SpawnProjectile(Rotate(direction, angle));
            }
        }

        private void SpawnProjectile(Vector2 direction)
        {
            Projectile projectile = Instantiate(
                projectilePrefab,
                (Vector2)transform.position + direction * spawnOffset,
                Quaternion.identity);
            projectile.transform.localScale = projectilePrefab.transform.localScale * ProjectileSizeMultiplier;

            Vector2 velocity = direction * ProjectileSpeed
                + movement.CurrentVelocity * inheritedVelocityFactor;
            projectile.Launch(
                velocity,
                health,
                CreateDamageContext(),
                stats.PierceCount,
                stats.ProjectileSplitSettings,
                configuredLifetime: ProjectileLifetime,
                configuredHitEffects: stats.BasicAttackHitEffects);
        }

        private void OnEnemyKilled(PlayerEnemyKilledEvent killEvent)
        {
            float healedAmount = health.Heal(stats.RegisterKillAndGetHealAmount());
            if (healedAmount > 0f)
            {
                Debug.Log(
                    $"[PlayerProjectileAttack] Healed {healedAmount} HP after defeating {killEvent.Target.name}.",
                    this);
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

    }
}
