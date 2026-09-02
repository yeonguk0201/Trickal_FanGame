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
        [SerializeField, Range(0f, 1f)] private float baseCriticalChance = 0.05f;
        [SerializeField, Min(1f)] private float baseCriticalDamageMultiplier = 1.5f;

        private Health health;
        private float maxHealthBonus;
        private float flatAttackDamageBonus;
        private float attackDamagePercentBonus;
        private float attackSpeedPercentBonus;
        private float criticalChanceBonus;
        private float skillDamagePercentBonus;
        private float moveSpeedBonus;
        private float moveSpeedPercentBelowHealthBonus;
        private float moveSpeedHealthThreshold;
        private int additionalProjectileCount;
        private int lowerGradeSkillProjectileBonus;
        private int lowerGradeSkillSPThreshold;
        private int pierceCount;
        private float healOnKill;
        private float healOnKillMaxHealthPercent;
        private float distanceDamageBonus;
        private float distanceDamageMinimum;
        private float distanceDamageMaximum;
        private ProjectileSplitSettings projectileSplitSettings;

        public float MaxHealth => baseMaxHealth + maxHealthBonus;
        public float AttackDamage =>
            (baseAttackDamage + flatAttackDamageBonus) * (1f + attackDamagePercentBonus);
        public float SkillDamageMultiplier => 1f + skillDamagePercentBonus;
        public float MoveSpeed => (baseMoveSpeed + moveSpeedBonus) *
            (1f + (IsBelowMoveSpeedHealthThreshold ? moveSpeedPercentBelowHealthBonus : 0f));
        public float AttackSpeed => baseAttackSpeed * (1f + attackSpeedPercentBonus);
        public float CriticalChance => Mathf.Clamp01(baseCriticalChance + criticalChanceBonus);
        public float CriticalDamageMultiplier => Mathf.Max(1f, baseCriticalDamageMultiplier);
        public int ProjectileCount => 1 + additionalProjectileCount;
        public int PierceCount => pierceCount;
        public float HealOnKill => healOnKill;
        public float HealOnKillMaxHealthPercent => healOnKillMaxHealthPercent;
        public float HealOnKillAmount => healOnKill + MaxHealth * healOnKillMaxHealthPercent;
        public ProjectileSplitSettings ProjectileSplitSettings => projectileSplitSettings;
        public bool IsBelowMoveSpeedHealthThreshold =>
            moveSpeedPercentBelowHealthBonus > 0f && health != null && !health.IsDead &&
            (health.CurrentHealth <= health.MaxHealth * moveSpeedHealthThreshold ||
             Mathf.Approximately(health.CurrentHealth, health.MaxHealth * moveSpeedHealthThreshold));

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

        public void AddAttackSpeedPercent(float amount)
        {
            attackSpeedPercentBonus = Mathf.Max(0f, attackSpeedPercentBonus + amount);
        }

        public void AddCriticalChance(float amount)
        {
            criticalChanceBonus = Mathf.Max(0f, criticalChanceBonus + amount);
        }

        public void AddSkillDamagePercent(float amount)
        {
            skillDamagePercentBonus = Mathf.Max(0f, skillDamagePercentBonus + amount);
        }

        public void AddMoveSpeed(float amount)
        {
            moveSpeedBonus = Mathf.Max(0f, moveSpeedBonus + amount);
        }

        public void AddMoveSpeedPercentBelowHealth(float amount, float healthThreshold)
        {
            if (amount <= 0f || healthThreshold <= 0f || healthThreshold > 1f)
            {
                return;
            }

            moveSpeedPercentBelowHealthBonus += amount;
            moveSpeedHealthThreshold = Mathf.Max(moveSpeedHealthThreshold, healthThreshold);
        }

        public void AddProjectiles(int amount)
        {
            additionalProjectileCount = Mathf.Max(0, additionalProjectileCount + amount);
        }

        public void AddLowerGradeSkillProjectileBonus(int amount, int spThreshold)
        {
            if (amount <= 0 || spThreshold <= 0)
            {
                return;
            }

            lowerGradeSkillProjectileBonus += amount;
            lowerGradeSkillSPThreshold = lowerGradeSkillSPThreshold == 0
                ? spThreshold
                : Mathf.Min(lowerGradeSkillSPThreshold, spThreshold);
        }

        public int GetLowerGradeSkillProjectileBonus(int currentSP)
        {
            return lowerGradeSkillSPThreshold > 0 && currentSP >= lowerGradeSkillSPThreshold
                ? lowerGradeSkillProjectileBonus
                : 0;
        }

        public void AddPierce(int amount)
        {
            pierceCount = Mathf.Max(0, pierceCount + amount);
        }

        public void AddHealOnKill(float amount)
        {
            healOnKill = Mathf.Max(0f, healOnKill + amount);
        }

        public void AddHealOnKillMaxHealthPercent(float amount)
        {
            healOnKillMaxHealthPercent = Mathf.Max(0f, healOnKillMaxHealthPercent + amount);
        }

        public void AddDistanceDamage(float maximumBonus, float minimumDistance, float maximumDistance)
        {
            if (maximumBonus <= 0f || minimumDistance < 0f || maximumDistance <= minimumDistance)
            {
                return;
            }

            distanceDamageBonus += maximumBonus;
            distanceDamageMinimum = minimumDistance;
            distanceDamageMaximum = maximumDistance;
        }

        public void ConfigureProjectileSplit(
            int projectileCount,
            float damageMultiplier,
            float maximumDistance,
            float spreadAngleDegrees,
            float scaleMultiplier)
        {
            ProjectileSplitSettings configured = new(
                projectileCount,
                damageMultiplier,
                maximumDistance,
                spreadAngleDegrees,
                scaleMultiplier);
            if (configured.IsEnabled)
            {
                projectileSplitSettings = configured;
            }
        }

        public DamageContext CreateDirectDamageContext(
            GameObject source,
            DamageSourceType sourceType,
            float multiplier = 1f)
        {
            return new DamageContext(
                source,
                sourceType,
                AttackDamage,
                multiplier,
                DamageDeliveryType.Direct,
                CriticalChance,
                CriticalDamageMultiplier,
                distanceDamageBonus: distanceDamageBonus,
                distanceDamageMinimum: distanceDamageMinimum,
                distanceDamageMaximum: distanceDamageMaximum);
        }
    }
}
