using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField, Min(0.1f)] private float baseMaxHealth = 10f;
        [SerializeField, Min(0.01f)] private float baseAttackDamage = 1f;
        [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float baseAttackSpeed = 1f;

        private Health health;
        private float maxHealthBonus;
        private float flatAttackDamageBonus;
        private float attackDamagePercentBonus;
        private float skillDamagePercentBonus;
        private float moveSpeedBonus;
        private int additionalProjectileCount;
        private int pierceCount;
        private float healOnKill;

        public float MaxHealth => baseMaxHealth + maxHealthBonus;
        public float AttackDamage =>
            (baseAttackDamage + flatAttackDamageBonus) * (1f + attackDamagePercentBonus);
        public float SkillDamageMultiplier => 1f + skillDamagePercentBonus;
        public float MoveSpeed => baseMoveSpeed + moveSpeedBonus;
        public float AttackSpeed => baseAttackSpeed;
        public int ProjectileCount => 1 + additionalProjectileCount;
        public int PierceCount => pierceCount;
        public float HealOnKill => healOnKill;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.SetMaxHealth(MaxHealth, true);
        }

        public void AddMaxHealth(float amount, bool healAddedAmount)
        {
            if (amount <= 0)
            {
                return;
            }

            maxHealthBonus += amount;
            health.SetMaxHealth(MaxHealth, healAddedAmount);
        }

        public void AddAttackDamage(float amount)
        {
            flatAttackDamageBonus = Mathf.Max(0f, flatAttackDamageBonus + amount);
        }

        public void AddAttackDamagePercent(float amount)
        {
            attackDamagePercentBonus = Mathf.Max(0f, attackDamagePercentBonus + amount);
        }

        public void AddSkillDamagePercent(float amount)
        {
            skillDamagePercentBonus = Mathf.Max(0f, skillDamagePercentBonus + amount);
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

        public void AddHealOnKill(float amount)
        {
            healOnKill = Mathf.Max(0f, healOnKill + amount);
        }
    }
}
