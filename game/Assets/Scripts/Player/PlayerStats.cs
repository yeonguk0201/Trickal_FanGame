using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField, Min(1)] private int baseMaxHealth = 10;
        [SerializeField, Min(1)] private int baseAttackDamage = 1;
        [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float baseAttackSpeed = 1f;

        private Health health;
        private int maxHealthBonus;
        private int attackDamageBonus;
        private float moveSpeedBonus;
        private int additionalProjectileCount;
        private int pierceCount;
        private int healOnKill;

        public int MaxHealth => baseMaxHealth + maxHealthBonus;
        public int AttackDamage => baseAttackDamage + attackDamageBonus;
        public float MoveSpeed => baseMoveSpeed + moveSpeedBonus;
        public float AttackSpeed => baseAttackSpeed;
        public int ProjectileCount => 1 + additionalProjectileCount;
        public int PierceCount => pierceCount;
        public int HealOnKill => healOnKill;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.SetMaxHealth(MaxHealth, true);
        }

        public void AddMaxHealth(int amount, bool healAddedAmount)
        {
            if (amount <= 0)
            {
                return;
            }

            maxHealthBonus += amount;
            health.SetMaxHealth(MaxHealth, healAddedAmount);
        }

        public void AddAttackDamage(int amount)
        {
            attackDamageBonus = Mathf.Max(0, attackDamageBonus + amount);
        }

        public void AddMoveSpeed(float amount)
        {
            moveSpeedBonus = Mathf.Max(0f, moveSpeedBonus + amount);
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
    }
}
