using System;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float maxHealth = 10f;
        [SerializeField, Min(0f)] private float currentShield;
        [SerializeField] private bool useHealthUnits;

        // Flat amount taken off every hit before the shield (빅우드의 열매). Health units when the owner uses them.
        private float incomingDamageReduction;
        private bool explicitInvulnerability;
        private DamageInvulnerability damageInvulnerability;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float CurrentShield => currentShield;
        public bool UsesHealthUnits => useHealthUnits;
        public float IncomingDamageReduction => incomingDamageReduction;
        public float MaxHealthHearts => useHealthUnits ? HealthUnits.ToHearts(maxHealth) : maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => explicitInvulnerability ||
                                      ResolveDamageInvulnerability()?.IsHitInvulnerableAt(Time.time) == true;

        public event Action<float, float> Damaged;
        public event Action<float, float> Changed;
        public event Action<DamageContext, float, float> DamageApplied;
        public event Action<DamageContext, DamageResult> DamageResolved;
        public event Action<float> ShieldChanged;
        public event Action Died;
        public static event Action<Health, DamageContext, DamageResult> AnyDamageResolved;

        private void Awake()
        {
            ResetHealth();
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(new DamageContext(null, DamageSourceType.Unknown, amount));
        }

        public void TakeDamage(DamageContext context)
        {
            DamageResult result = DamageCalculator.Resolve(context);
            if (useHealthUnits)
            {
                result = new DamageResult(HealthUnits.ToDamageUnits(result.FinalDamage, context.AllowsHalfHeart),
                    result.IsCritical);
            }

            if (incomingDamageReduction > 0f)
            {
                result = new DamageResult(Mathf.Max(0f, result.FinalDamage - incomingDamageReduction),
                    result.IsCritical);
            }

            float amount = result.FinalDamage;
            if (IsDead || IsInvulnerable || amount <= 0)
            {
                return;
            }

            float absorbedDamage = Mathf.Min(currentShield, amount);
            if (absorbedDamage > 0f)
            {
                currentShield -= absorbedDamage;
                amount -= absorbedDamage;
                ShieldChanged?.Invoke(currentShield);
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth > 0f)
            {
                ResolveDamageInvulnerability()?.BeginHitWindow(Time.time);
            }
            DamageResolved?.Invoke(context, result);
            AnyDamageResolved?.Invoke(this, context, result);
            DamageApplied?.Invoke(context, previousHealth - CurrentHealth, CurrentHealth);
            if (!Mathf.Approximately(previousHealth, CurrentHealth))
            {
                Damaged?.Invoke(CurrentHealth, MaxHealth);
                Changed?.Invoke(CurrentHealth, MaxHealth);
            }

            if (CurrentHealth > 0f)
            {
                context.Source?.GetComponent<PlayerCombatEvents>()?
                    .TryReportBasicAttackHit(this, context, previousHealth - CurrentHealth);
                return;
            }

            IsDead = true;
            if (context.Source != null)
            {
                PlayerCombatEvents combatEvents = context.Source.GetComponent<PlayerCombatEvents>();
                combatEvents?.TryReportBasicAttackHit(this, context, previousHealth - CurrentHealth);
                combatEvents?.TryReportEnemyKilled(this, context);
            }
            Died?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            SetShield(0f);
            IsDead = false;
            incomingDamageReduction = 0f;
            explicitInvulnerability = false;
            ResolveDamageInvulnerability()?.ResetHitWindow();
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetIncomingDamageReduction(float amount)
        {
            incomingDamageReduction = Mathf.Max(0f, amount);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            explicitInvulnerability = invulnerable && !IsDead;
        }

        private DamageInvulnerability ResolveDamageInvulnerability()
        {
            if (damageInvulnerability == null)
            {
                damageInvulnerability = GetComponent<DamageInvulnerability>();
            }

            return damageInvulnerability;
        }

        public void EnableHealthUnits()
        {
            if (useHealthUnits)
            {
                return;
            }

            useHealthUnits = true;
            maxHealth = Mathf.Max(1f, Mathf.Round(maxHealth));
            CurrentHealth = Mathf.Min(maxHealth, HealthUnits.FloorToUnits(CurrentHealth));
            currentShield = HealthUnits.FloorToUnits(currentShield);
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public float GetMaxHealthRatioAmount(float ratio)
        {
            return useHealthUnits
                ? HealthUnits.FromMaxHealthRatio(maxHealth, ratio)
                : maxHealth * Mathf.Max(0f, ratio);
        }

        public bool SetShield(float value)
        {
            float nextShield = Mathf.Max(0f, useHealthUnits ? HealthUnits.FloorToUnits(value) : value);
            if (Mathf.Approximately(currentShield, nextShield))
            {
                return false;
            }

            currentShield = nextShield;
            ShieldChanged?.Invoke(currentShield);
            return true;
        }

        public bool AddShield(float amount)
        {
            return amount > 0f && SetShield(currentShield + amount);
        }

        internal void SetMaxHealth(float value, bool healAddedAmount)
        {
            float nextMaxHealth = useHealthUnits ? Mathf.Max(1f, Mathf.Round(value)) : Mathf.Max(0.1f, value);
            float addedAmount = nextMaxHealth - maxHealth;
            if (Mathf.Approximately(addedAmount, 0f))
            {
                return;
            }

            maxHealth = nextMaxHealth;
            if (healAddedAmount && addedAmount > 0 && !IsDead)
            {
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + addedAmount);
            }
            else
            {
                CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
            }

            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public float Heal(float amount)
        {
            if (useHealthUnits)
            {
                amount = HealthUnits.FloorToUnits(amount);
            }

            if (IsDead || amount <= 0 || CurrentHealth >= maxHealth)
            {
                return 0;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            float healedAmount = CurrentHealth - previousHealth;
            if (healedAmount > 0f)
            {
                Changed?.Invoke(CurrentHealth, MaxHealth);
            }

            return healedAmount;
        }
    }
}
