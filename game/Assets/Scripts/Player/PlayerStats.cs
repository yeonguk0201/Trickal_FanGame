using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [Tooltip("Half-heart units. 2 units = 1 heart.")]
        [SerializeField, Min(1f)] private float baseMaxHealth = 10f;
        [SerializeField, Min(0.01f)] private float baseAttackDamage = 10f;
        [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float baseAttackSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float baseCriticalChance = 0.05f;
        [SerializeField, Min(1f)] private float baseCriticalDamageMultiplier = 1.5f;

        private Health health;
        private float maxHealthBonus;
        private float flatAttackDamageBonus;
        private float attackDamagePercentBonus;
        private float currentRoomAttackDamagePercentBonus;
        // Single-use spells own this source, so the legacy spell refresh never overwrites it.
        private float singleUseRoomAttackDamagePercentBonus;
        private float singleUseRoomCriticalDamageBonus;
        private float singleUseRoomCriticalChanceBonus;
        private float singleUseRoomAttackSpeedPercentBonus;
        private float singleUseRoomMoveSpeedPercentBonus;
        private float attackSpeedPercentBonus;
        private float currentRoomAttackSpeedPercentBonus;
        private float criticalChanceBonus;
        private float skillDamagePercentBonus;
        private float moveSpeedBonus;
        private float moveSpeedPercentBonus;
        private float moveSpeedPenaltyPercent;
        private float currentRoomMoveSpeedPercentBonus;
        private float moveSpeedPercentBelowHealthBonus;
        private float moveSpeedHealthThreshold;
        private int additionalProjectileCount;
        private int lowerGradeSkillProjectileBonus;
        private int lowerGradeSkillSPThreshold;
        private int pierceCount;
        private float healOnKill;
        private float healOnKillMaxHealthPercent;
        private float periodicKillHealAmount;
        private int periodicKillHealInterval;
        private int periodicKillHealProgress;
        private float distanceDamageBonus;
        private float distanceDamageMinimum;
        private float distanceDamageMaximum;
        private ProjectileSplitSettings projectileSplitSettings;
        private float projectileSpeedPercentBonus;
        private float playerSizePercentBonus;
        private float basicAttackDamagePercentBonus;
        private float projectileLifetimePercentBonus;
        private float projectileSizePercentBonus;
        private PoisonSettings basicAttackPoison;

        public float MaxHealth => baseMaxHealth + maxHealthBonus;
        public float AttackDamage =>
            (baseAttackDamage + flatAttackDamageBonus) * Mathf.Max(0f, 1f + attackDamagePercentBonus);
        public float SkillDamageMultiplier => 1f + skillDamagePercentBonus;
        // Passive-2: PlayerBodySize turns this into the look, the hurtbox and the fixed feet.
        public float PlayerSizeMultiplier => 1f + playerSizePercentBonus;

        public float MoveSpeed => (baseMoveSpeed + moveSpeedBonus) *
            Mathf.Max(0f, 1f + moveSpeedPercentBonus - moveSpeedPenaltyPercent +
                currentRoomMoveSpeedPercentBonus + singleUseRoomMoveSpeedPercentBonus +
                (IsBelowMoveSpeedHealthThreshold ? moveSpeedPercentBelowHealthBonus : 0f));
        public float AttackSpeed => baseAttackSpeed *
            Mathf.Max(0.01f, 1f + attackSpeedPercentBonus + currentRoomAttackSpeedPercentBonus +
                             singleUseRoomAttackSpeedPercentBonus);
        public float BasicAttackRoomDamageMultiplier =>
            1f + basicAttackDamagePercentBonus + currentRoomAttackDamagePercentBonus +
            singleUseRoomAttackDamagePercentBonus;
        public float CriticalChance =>
            Mathf.Clamp01(baseCriticalChance + criticalChanceBonus + singleUseRoomCriticalChanceBonus);
        public float CriticalDamageMultiplier =>
            Mathf.Max(1f, baseCriticalDamageMultiplier + singleUseRoomCriticalDamageBonus);
        public int ProjectileCount => 1 + additionalProjectileCount;
        public int PierceCount => pierceCount;
        public float HealOnKill => healOnKill;
        public float HealOnKillMaxHealthPercent => healOnKillMaxHealthPercent;
        public float HealOnKillAmount => healOnKill +
            (health != null
                ? health.GetMaxHealthRatioAmount(healOnKillMaxHealthPercent)
                : MaxHealth * healOnKillMaxHealthPercent);
        public float PeriodicKillHealAmount => periodicKillHealAmount;
        public int PeriodicKillHealInterval => periodicKillHealInterval;
        public int PeriodicKillHealProgress => periodicKillHealProgress;
        public ProjectileSplitSettings ProjectileSplitSettings => projectileSplitSettings;
        // Range-0: basic attack travel distance = flight time × shot speed.
        public float ProjectileSpeedMultiplier => 1f + projectileSpeedPercentBonus;
        public float ProjectileLifetimeMultiplier => 1f + projectileLifetimePercentBonus;
        // Passive-0 §4.1: scales the basic attack shot's look and collision radius together, never its damage.
        public float ProjectileSizeMultiplier =>
            Mathf.Min(1f + projectileSizePercentBonus, ProjectileSizing.MaximumPlayerBasicSizeMultiplier);
        public PoisonSettings BasicAttackPoison => basicAttackPoison;
        public ProjectileHitEffects BasicAttackHitEffects => new(true, basicAttackPoison);
        public bool IsBelowMoveSpeedHealthThreshold =>
            moveSpeedPercentBelowHealthBonus > 0f && health != null && !health.IsDead &&
            (health.CurrentHealth <= health.MaxHealth * moveSpeedHealthThreshold ||
             Mathf.Approximately(health.CurrentHealth, health.MaxHealth * moveSpeedHealthThreshold));

        private void Awake()
        {
            health = GetComponent<Health>();
            health.EnableHealthUnits();
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

        public void AddMoveSpeedPercent(float amount)
        {
            moveSpeedPercentBonus = Mathf.Max(0f, moveSpeedPercentBonus + amount);
        }

        public void AddMoveSpeedPenaltyPercent(float amount)
        {
            moveSpeedPenaltyPercent = Mathf.Max(0f, moveSpeedPenaltyPercent + amount);
        }

        public void SetCurrentRoomAttackDamagePercent(float amount)
        {
            currentRoomAttackDamagePercentBonus = Mathf.Max(0f, amount);
        }

        public void SetSingleUseRoomAttackDamagePercent(float amount)
        {
            singleUseRoomAttackDamagePercentBonus = Mathf.Max(0f, amount);
        }

        public void SetSingleUseRoomCritical(float damage, float chance)
        {
            singleUseRoomCriticalDamageBonus = Mathf.Max(0f, damage);
            singleUseRoomCriticalChanceBonus = Mathf.Max(0f, chance);
        }

        public void SetSingleUseRoomSpeedPercent(float attackSpeed, float moveSpeed)
        {
            singleUseRoomAttackSpeedPercentBonus = Mathf.Max(0f, attackSpeed);
            singleUseRoomMoveSpeedPercentBonus = Mathf.Max(0f, moveSpeed);
        }

        public void SetCurrentRoomAttackSpeedPercent(float amount)
        {
            currentRoomAttackSpeedPercentBonus = Mathf.Max(0f, amount);
        }

        public void SetCurrentRoomMoveSpeedPercent(float amount)
        {
            currentRoomMoveSpeedPercentBonus = Mathf.Max(0f, amount);
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

        // The first stack sets the kill interval and heal amount; each later stack lowers the interval by one kill.
        public void AddPeriodicKillHeal(float amount, int killInterval)
        {
            if (amount <= 0f || killInterval <= 0)
            {
                return;
            }

            if (periodicKillHealInterval == 0)
            {
                periodicKillHealAmount = amount;
                periodicKillHealInterval = killInterval;
                return;
            }

            periodicKillHealInterval = Mathf.Max(1, periodicKillHealInterval - 1);
        }

        public float RegisterKillAndGetHealAmount()
        {
            float amount = HealOnKillAmount;
            if (periodicKillHealInterval <= 0)
            {
                return amount;
            }

            periodicKillHealProgress++;
            if (periodicKillHealProgress >= periodicKillHealInterval)
            {
                periodicKillHealProgress = 0;
                amount += periodicKillHealAmount;
            }

            return amount;
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

        public void AddProjectileSpeedPercent(float amount)
        {
            projectileSpeedPercentBonus = Mathf.Max(0f, projectileSpeedPercentBonus + amount);
        }

        public void AddProjectileLifetimePercent(float amount)
        {
            projectileLifetimePercentBonus = Mathf.Max(0f, projectileLifetimePercentBonus + amount);
        }

        public void AddProjectileSizePercent(float amount)
        {
            projectileSizePercentBonus = Mathf.Max(0f, projectileSizePercentBonus + amount);
        }

        // Passive-2: a Run-long bonus that joins the room bonuses of the basic attack multiplier.
        public void AddBasicAttackDamagePercent(float amount)
        {
            basicAttackDamagePercentBonus = Mathf.Max(0f, basicAttackDamagePercentBonus + amount);
        }

        public void AddPlayerSizePercent(float amount)
        {
            playerSizePercentBonus = Mathf.Max(0f, playerSizePercentBonus + amount);
        }

        // Sources of the same status effect add their chances (up to 100%); the other values follow the latest one.
        public void AddBasicAttackPoison(
            float chance,
            float tickDamageRatio,
            float durationSeconds,
            float intervalSeconds,
            int maximumStacks)
        {
            PoisonSettings configured = new(
                basicAttackPoison.Chance + Mathf.Max(0f, chance),
                tickDamageRatio,
                durationSeconds,
                intervalSeconds,
                maximumStacks);
            if (configured.IsEnabled)
            {
                basicAttackPoison = configured;
            }
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
            float roomMultiplier = sourceType is DamageSourceType.PlayerAttack or
                DamageSourceType.PlayerProjectile
                ? BasicAttackRoomDamageMultiplier
                : 1f;
            return new DamageContext(
                source,
                sourceType,
                AttackDamage,
                multiplier * roomMultiplier,
                DamageDeliveryType.Direct,
                CriticalChance,
                CriticalDamageMultiplier,
                distanceDamageBonus: distanceDamageBonus,
                distanceDamageMinimum: distanceDamageMinimum,
                distanceDamageMaximum: distanceDamageMaximum);
        }
    }
}
