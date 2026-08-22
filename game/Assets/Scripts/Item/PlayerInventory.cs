using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(Health), typeof(PlayerMovement), typeof(PlayerProjectileAttack))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;

        private readonly List<AcquiredItem> acquiredItems = new();
        private readonly Dictionary<string, int> stackCounts = new();
        private Health health;
        private PlayerMovement movement;
        private PlayerProjectileAttack attack;

        public IReadOnlyList<AcquiredItem> AcquiredItems => acquiredItems;
        public event Action<ItemDefinition, int> ItemAcquired;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            attack = GetComponent<PlayerProjectileAttack>();
            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }
        }

        public bool TryAcquire(ItemDefinition definition)
        {
            if (definition == null || !definition.IsValid)
            {
                Debug.LogError("[PlayerInventory] Cannot acquire an invalid item definition.", this);
                return false;
            }

            stackCounts.TryGetValue(definition.ItemId, out int currentStacks);
            if (definition.MaxStacks > 0 && currentStacks >= definition.MaxStacks)
            {
                Debug.Log($"[PlayerInventory] {definition.DisplayName} is already at its stack limit.", this);
                return false;
            }

            int newStackCount = currentStacks + 1;
            stackCounts[definition.ItemId] = newStackCount;
            ApplyEffect(definition);

            int floor = Mathf.Max(1, runProgress != null ? runProgress.CurrentFloor : 1);
            acquiredItems.Add(new AcquiredItem(
                definition.ItemId,
                floor,
                acquiredItems.Count + 1,
                Time.realtimeSinceStartup));

            ItemAcquired?.Invoke(definition, newStackCount);
            Debug.Log($"[PlayerInventory] Acquired {definition.DisplayName} x{newStackCount}.", this);
            return true;
        }

        public int GetStackCount(string itemId)
        {
            return stackCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        private void ApplyEffect(ItemDefinition definition)
        {
            switch (definition.EffectType)
            {
                case ItemEffectType.AttackDamage:
                    attack.AddDamageBonus(Mathf.RoundToInt(definition.EffectValue));
                    break;
                case ItemEffectType.MaxHealth:
                    health.AddMaxHealth(Mathf.RoundToInt(definition.EffectValue), true);
                    break;
                case ItemEffectType.MoveSpeed:
                    movement.AddMoveSpeedBonus(definition.EffectValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
