using System;
using UnityEngine;

namespace TrickalFanGame.Player
{
    // SP is counted in whole slots. Two extras (Spell-0): a half slot of progress below the maximum, and overcharge
    // above the maximum. Overcharge only comes from TryFillWithOvercharge and disappears as SP is spent back down;
    // ordinary gains never exceed the maximum.
    [DisallowMultipleComponent]
    public sealed class PlayerSP : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxSP = 3;
        [SerializeField, Min(0)] private int currentSP;

        private bool hasHalfSP;

        public int CurrentSP => currentSP;
        public int MaxSP => maxSP;
        public bool HasHalfSP => hasHalfSP;
        public bool IsOvercharged => currentSP > maxSP;

        public event Action<int, int> Changed;
        public event Action Restored;

        private void Awake()
        {
            currentSP = Mathf.Clamp(currentSP, 0, maxSP);
            if (currentSP >= maxSP) hasHalfSP = false;
        }

        public bool TryAdd(int amount = 1)
        {
            if (amount <= 0 || currentSP >= maxSP)
            {
                return false;
            }

            int previous = currentSP;
            currentSP = Mathf.Min(maxSP, currentSP + amount);
            // A full gauge has no room for a half slot.
            if (currentSP >= maxSP) hasHalfSP = false;
            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] SP {currentSP}/{maxSP}", this);
            Restored?.Invoke();
            return currentSP != previous;
        }

        // Adds half a slot; the second half completes a whole slot. Nothing is added at or above the maximum.
        public bool TryAddHalf()
        {
            if (currentSP >= maxSP)
            {
                return false;
            }

            if (hasHalfSP)
            {
                hasHalfSP = false;
                currentSP++;
            }
            else
            {
                hasHalfSP = true;
            }

            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] SP {currentSP}{(hasHalfSP ? ".5" : string.Empty)}/{maxSP}", this);
            Restored?.Invoke();
            return true;
        }

        // Fills SP to the maximum plus the given overcharge. Fails without change when already at that level.
        public bool TryFillWithOvercharge(int overcharge)
        {
            int target = maxSP + Mathf.Max(0, overcharge);
            if (currentSP >= target)
            {
                return false;
            }

            currentSP = target;
            hasHalfSP = false;
            Changed?.Invoke(currentSP, maxSP);
            Debug.Log($"[PlayerSP] SP {currentSP}/{maxSP} (filled with overcharge)", this);
            Restored?.Invoke();
            return true;
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
