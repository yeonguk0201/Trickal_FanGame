using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
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
        private ProjectileBounceSettings projectileBounce;
        private BurnSettings basicAttackBurn;
        private ShockSettings basicAttackShock;
        private int burnSourceCount;
        private float criticalDamageBonus;
        private float statusTickDamagePercentBonus;
        private float executeHealthThreshold;
        private float burningTargetDirectDamagePercent;
        private float shockedTargetSkillDamagePercent;
        private float shockedTargetCriticalDamageBonus;
        private float shockedTargetCriticalChanceBonus;
        // 슈슈슈슉 글러브: bonuses that last only while its kill stacks are up.
        private float killFrenzyBasicAttackDamagePercent;
        private float killFrenzyAttackSpeedPercent;
        private float killFrenzyKnockbackPercent;

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
                             singleUseRoomAttackSpeedPercentBonus + killFrenzyAttackSpeedPercent);
        public float BasicAttackRoomDamageMultiplier =>
            1f + basicAttackDamagePercentBonus + currentRoomAttackDamagePercentBonus +
            singleUseRoomAttackDamagePercentBonus + killFrenzyBasicAttackDamagePercent;
        public float CriticalChance =>
            Mathf.Clamp01(baseCriticalChance + criticalChanceBonus + singleUseRoomCriticalChanceBonus);
        public float CriticalDamageMultiplier =>
            Mathf.Max(1f, baseCriticalDamageMultiplier + criticalDamageBonus + singleUseRoomCriticalDamageBonus);
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
        // Passive-3 (칸타의 팽이): basic attack shots bounce between enemies.
        public ProjectileBounceSettings ProjectileBounce => projectileBounce;
        // Passive-4 (샤샤의 항아리): the basic attack is a water stream instead of shots. PlayerWaterStream fires it.
        public bool HasWaterStream { get; private set; }
        public BurnSettings BasicAttackBurn => basicAttackBurn;
        public ShockSettings BasicAttackShock => basicAttackShock;
        // Added to the tick damage of every status effect the player applies (앗따검, 탐욕의 반지).
        public float StatusTickDamagePercentBonus => statusTickDamagePercentBonus;
        public float ExecuteHealthThreshold => executeHealthThreshold;
        public ProjectileHitEffects BasicAttackHitEffects => new(true, basicAttackPoison, basicAttackBurn,
            basicAttackShock, statusTickDamagePercentBonus, killFrenzyKnockbackPercent);
        // Obstacle-7: whether the player holds a burn artifact (활활 불타활, 불타는 가지). Their burn effect calls
        // AddBurnSource.
        public bool HasBurnSource
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DevelopmentForceBurnSource) return true;
#endif
                return burnSourceCount > 0;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: the player counts as holding a burn artifact.
        public static bool DevelopmentForceBurnSource { get; set; }
#endif
        public bool IsBelowMoveSpeedHealthThreshold =>
            moveSpeedPercentBelowHealthBonus > 0f && health != null && !health.IsDead &&
            (health.CurrentHealth <= health.MaxHealth * moveSpeedHealthThreshold ||
             Mathf.Approximately(health.CurrentHealth, health.MaxHealth * moveSpeedHealthThreshold));

        private void Awake()
        {
            health = GetComponent<Health>();
            health.EnableHealthUnits();
            // Game rule: the player's current health and shield together stop at 15 hearts.
            health.SetHealthAndShieldLimit(HealthUnits.MaximumHealthAndShieldUnits);
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

        public void AddBurnSource() => burnSourceCount++;

        public void EnableWaterStream() => HasWaterStream = true;

        // The first stack sets the bounce; each later stack adds one bounce.
        public void AddProjectileBounce(
            int bounceCount,
            float searchRadius,
            float repeatDamageRatio,
            float sameTargetDelaySeconds)
        {
            ProjectileBounceSettings configured = projectileBounce.IsEnabled
                ? projectileBounce.WithBounceCount(projectileBounce.BounceCount + 1)
                : new ProjectileBounceSettings(bounceCount, searchRadius, repeatDamageRatio,
                    sameTargetDelaySeconds);
            if (configured.IsEnabled)
            {
                projectileBounce = configured;
            }
        }

        public void AddCriticalDamage(float amount)
        {
            criticalDamageBonus = Mathf.Max(0f, criticalDamageBonus + amount);
        }

        public void AddStatusTickDamagePercent(float amount)
        {
            statusTickDamagePercentBonus = Mathf.Max(0f, statusTickDamagePercentBonus + amount);
        }

        public void AddExecuteHealthThreshold(float healthRatio)
        {
            executeHealthThreshold = Mathf.Clamp01(Mathf.Max(executeHealthThreshold, healthRatio));
        }

        public void AddBurningTargetDirectDamagePercent(float amount)
        {
            burningTargetDirectDamagePercent = Mathf.Max(0f, burningTargetDirectDamagePercent + amount);
        }

        public void AddShockedTargetSkillDamagePercent(float amount)
        {
            shockedTargetSkillDamagePercent = Mathf.Max(0f, shockedTargetSkillDamagePercent + amount);
        }

        public void AddShockedTargetCriticalBonus(float damage, float chance)
        {
            shockedTargetCriticalDamageBonus = Mathf.Max(0f, shockedTargetCriticalDamageBonus + damage);
            shockedTargetCriticalChanceBonus = Mathf.Max(0f, shockedTargetCriticalChanceBonus + chance);
        }

        public void SetKillFrenzyBonus(float basicAttackDamage, float attackSpeed, float knockback)
        {
            killFrenzyBasicAttackDamagePercent = Mathf.Max(0f, basicAttackDamage);
            killFrenzyAttackSpeedPercent = Mathf.Max(0f, attackSpeed);
            killFrenzyKnockbackPercent = Mathf.Max(0f, knockback);
        }

        // Sources of the same status effect add their chances (up to 100%); the other values follow the latest one.
        public void AddBasicAttackBurn(
            float chance,
            float tickDamageRatio,
            float durationSeconds,
            float intervalSeconds)
        {
            BurnSettings configured = new(
                basicAttackBurn.Chance + Mathf.Max(0f, chance),
                tickDamageRatio,
                durationSeconds,
                intervalSeconds);
            if (configured.IsEnabled)
            {
                basicAttackBurn = configured;
            }
        }

        public void AddBasicAttackShock(float chance, float slowPerStack, float durationSeconds, int maximumStacks)
        {
            ShockSettings configured = new(
                basicAttackShock.Chance + Mathf.Max(0f, chance),
                slowPerStack,
                durationSeconds,
                maximumStacks);
            if (configured.IsEnabled)
            {
                basicAttackShock = configured;
            }
        }

        // Bonuses that depend on the target's status when the hit lands. Only direct player hits gain them;
        // status ticks, auras and bombs do not.
        public DamageContext ApplyTargetBonuses(Health target, DamageContext context)
        {
            if (target == null || context.DeliveryType != DamageDeliveryType.Direct ||
                (burningTargetDirectDamagePercent <= 0f && shockedTargetSkillDamagePercent <= 0f &&
                 shockedTargetCriticalDamageBonus <= 0f && shockedTargetCriticalChanceBonus <= 0f) ||
                !target.TryGetComponent(out EnemyStatusEffects status))
            {
                return context;
            }

            bool isBasicAttack = context.SourceType is DamageSourceType.PlayerProjectile or
                DamageSourceType.PlayerAttack;
            bool isSkill = context.SourceType is DamageSourceType.PlayerSkillExplosion or
                DamageSourceType.PlayerUltimateImpact;
            if (!isBasicAttack && !isSkill && context.SourceType is not (DamageSourceType.PlayerItemLightning or
                    DamageSourceType.PlayerItemExplosion))
            {
                return context;
            }

            float multiplier = 1f;
            float criticalChance = 0f;
            float criticalDamage = 0f;
            if (status.IsBurning)
            {
                multiplier *= 1f + burningTargetDirectDamagePercent;
            }

            if (status.IsShocked && (isBasicAttack || isSkill))
            {
                if (isSkill)
                {
                    // The same stat adds: the cast already holds the skill damage bonus.
                    multiplier *= (SkillDamageMultiplier + shockedTargetSkillDamagePercent) / SkillDamageMultiplier;
                }

                criticalChance = shockedTargetCriticalChanceBonus;
                criticalDamage = shockedTargetCriticalDamageBonus;
            }

            return Mathf.Approximately(multiplier, 1f) && criticalChance <= 0f && criticalDamage <= 0f
                ? context
                : context.WithTargetBonuses(multiplier, criticalChance, criticalDamage);
        }

        // 림의 낫: right after player damage, an enemy left at or below the threshold dies. Bosses are exempt;
        // what a boss summons is an ordinary enemy.
        public bool Executes(Health target, DamageContext context)
        {
            return executeHealthThreshold > 0f && target != null &&
                   PlayerCombatEvents.IsPlayerDamage(context.SourceType) &&
                   target.CurrentHealth <= target.MaxHealth * executeHealthThreshold + 0.0001f &&
                   target.GetComponent<BossController>() == null;
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

        // Passive-5 (다야의 다이아몬드 커터): every hit splits into four small shots around the enemy.
        public void ConfigureProjectileSplitOnHit(float damageMultiplier, float maximumDistance, float scaleMultiplier)
        {
            ProjectileSplitSettings configured = ProjectileSplitSettings.OnHit(damageMultiplier, maximumDistance,
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
