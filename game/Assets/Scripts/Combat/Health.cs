using System;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int maxHealth = 10;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable { get; private set; }

        public event Action<int, int> Damaged;
        public event Action<DamageContext, int, int> DamageApplied;
        public event Action Died;

        private void Awake()
        {
            ResetHealth();
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(new DamageContext(null, DamageSourceType.Unknown, amount));
        }

        public void TakeDamage(DamageContext context)
        {
            int amount = DamageCalculator.Calculate(context);
            if (IsDead || IsInvulnerable || amount <= 0)
            {
                return;
            }

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            DamageApplied?.Invoke(context, previousHealth - CurrentHealth, CurrentHealth);
            Damaged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth != 0)
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

        internal void SetMaxHealth(int value, bool healAddedAmount)
        {
            int nextMaxHealth = Mathf.Max(1, value);
            int addedAmount = nextMaxHealth - maxHealth;
            if (addedAmount == 0)
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

        public int Heal(int amount)
        {
            if (IsDead || amount <= 0 || CurrentHealth >= maxHealth)
            {
                return 0;
            }

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            return CurrentHealth - previousHealth;
        }
    }
}
