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

        public event Action<int, int> Damaged;
        public event Action Died;

        private void Awake()
        {
            ResetHealth();
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Damaged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth != 0)
            {
                return;
            }

            IsDead = true;
            Died?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }
    }
}
