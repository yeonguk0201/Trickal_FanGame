using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Health), typeof(PlayerMovement))]
    public sealed class PlayerProjectileAttack : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(0f)] private float spawnOffset = 0.65f;
        [SerializeField, Min(0.01f)] private float baseProjectileSpeed = 8f;
        [SerializeField, Min(0f)] private float inheritedVelocityFactor = 0.25f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.35f;
        [SerializeField, Min(1)] private int baseDamage = 1;
        [SerializeField, Range(0f, 45f)] private float multiShotSpreadAngle = 12f;

        private Health health;
        private PlayerMovement movement;
        private float nextAttackTime;
        private int damageBonus;
        private int additionalProjectileCount;
        private int pierceCount;
        private int healOnKill;

        public int CurrentDamage => baseDamage + damageBonus;
        public int ProjectileCount => 1 + additionalProjectileCount;
        public int PierceCount => pierceCount;
        public int HealOnKill => healOnKill;

        public void AddDamageBonus(int amount)
        {
            damageBonus = Mathf.Max(0, damageBonus + amount);
        }

        public void AddProjectiles(int amount)
        {
            additionalProjectileCount = Mathf.Max(0, additionalProjectileCount + amount);
        }

        public void AddPierce(int amount)
        {
            pierceCount = Mathf.Max(0, pierceCount + amount);
        }

        public void AddHealOnKill(int amount)
        {
            healOnKill = Mathf.Max(0, healOnKill + amount);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (health.IsDead || Time.time < nextAttackTime || !TryReadAttackDirection(out Vector2 direction))
            {
                return;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError("Projectile prefab is not assigned.", this);
                enabled = false;
                return;
            }

            nextAttackTime = Time.time + attackCooldown;
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

            Vector2 velocity = direction * baseProjectileSpeed
                + movement.CurrentVelocity * inheritedVelocityFactor;
            projectile.Launch(velocity, health, CurrentDamage, pierceCount, OnTargetKilled);
        }

        private void OnTargetKilled()
        {
            int healedAmount = health.Heal(healOnKill);
            if (healedAmount > 0)
            {
                Debug.Log($"[PlayerProjectileAttack] Healed {healedAmount} HP after defeating an enemy.", this);
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

        private static bool TryReadAttackDirection(out Vector2 direction)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) { direction = Vector2.left; return true; }
                if (keyboard.rightArrowKey.isPressed) { direction = Vector2.right; return true; }
                if (keyboard.downArrowKey.isPressed) { direction = Vector2.down; return true; }
                if (keyboard.upArrowKey.isPressed) { direction = Vector2.up; return true; }
            }

            direction = Vector2.zero;
            return false;
        }
    }
}
