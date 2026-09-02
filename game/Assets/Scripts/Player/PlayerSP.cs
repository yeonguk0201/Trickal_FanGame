using System;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSP : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxSP = 3;
        [SerializeField, Min(0)] private int currentSP;

        public int CurrentSP => currentSP;
        public int MaxSP => maxSP;

        public event Action<int, int> Changed;

        private void Awake()
        {
            currentSP = Mathf.Clamp(currentSP, 0, maxSP);
        }

        public bool TryAdd(int amount = 1)
        {
            if (amount <= 0 || currentSP >= maxSP)
            {
                return false;
            }

            int previous = currentSP;
            currentSP = Mathf.Min(maxSP, currentSP + amount);
            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] SP {currentSP}/{maxSP}", this);
            return currentSP != previous;
        }

        public bool TrySpend(int amount = 1)
        {
            if (amount <= 0 || currentSP < amount)
            {
                return false;
            }

            currentSP -= amount;
            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] SP {currentSP}/{maxSP}", this);
            return true;
        }

        public bool AddMaxSP(int amount = 1)
        {
            if (amount <= 0)
            {
                return false;
            }

            maxSP += amount;
            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] Maximum SP increased to {maxSP}; current SP remains {currentSP}.", this);
            return true;
        }
    }
}
