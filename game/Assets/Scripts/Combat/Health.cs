using System;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float maxHealth = 10f;
        [SerializeField, Min(0f)] private float currentShield;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float CurrentShield => currentShield;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable { get; private set; }

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
                return;
            }

            IsDead = true;
            if (context.Source != null)
            {
                context.Source.GetComponent<PlayerCombatEvents>()?.TryReportEnemyKilled(this, context);
            }
            Died?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            SetShield(0f);
            IsDead = false;
            IsInvulnerable = false;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable && !IsDead;
        }

        public bool SetShield(float value)
        {
            float nextShield = Mathf.Max(0f, value);
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
            float nextMaxHealth = Mathf.Max(0.1f, value);
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
