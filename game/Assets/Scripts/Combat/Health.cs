using System;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float maxHealth = 10f;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable { get; private set; }

        public event Action<float, float> Damaged;
        public event Action<DamageContext, float, float> DamageApplied;
        public event Action Died;

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
            float amount = DamageCalculator.Calculate(context);
            if (IsDead || IsInvulnerable || amount <= 0)
            {
                return;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            DamageApplied?.Invoke(context, previousHealth - CurrentHealth, CurrentHealth);
            Damaged?.Invoke(CurrentHealth, MaxHealth);

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
            IsDead = false;
            IsInvulnerable = false;
        }

        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable && !IsDead;
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
        }

        public float Heal(float amount)
        {
            if (IsDead || amount <= 0 || CurrentHealth >= maxHealth)
            {
                return 0;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            return CurrentHealth - previousHealth;
        }
    }
}
