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

        private Health health;
        private PlayerMovement movement;
        private float nextAttackTime;
        private int damageBonus;

        public int CurrentDamage => baseDamage + damageBonus;

        public void AddDamageBonus(int amount)
        {
            damageBonus = Mathf.Max(0, damageBonus + amount);
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
            Projectile projectile = Instantiate(
                projectilePrefab,
                (Vector2)transform.position + direction * spawnOffset,
                Quaternion.identity);

            Vector2 velocity = direction * baseProjectileSpeed
                + movement.CurrentVelocity * inheritedVelocityFactor;
            projectile.Launch(velocity, health, CurrentDamage);
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
