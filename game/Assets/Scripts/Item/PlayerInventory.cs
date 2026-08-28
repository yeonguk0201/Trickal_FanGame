using System;
using System.Collections.Generic;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;

        private readonly List<AcquiredItem> acquiredItems = new();
        private readonly Dictionary<string, int> stackCounts = new();
        private PlayerStats stats;

        public const string MultiShotItemId = "item-06";
        public const string PierceItemId = "item-11";

        public IReadOnlyList<AcquiredItem> AcquiredItems => acquiredItems;
        public bool IsMultiShotPierceSynergyActive { get; private set; }
        public event Action<ItemDefinition, int> ItemAcquired;
        public event Action<string> SynergyActivated;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
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
            EvaluateSynergies();

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
                    stats.AddAttackDamage(definition.EffectValue);
                    break;
                case ItemEffectType.AttackDamagePercent:
                    stats.AddAttackDamagePercent(definition.EffectValue);
                    break;
                case ItemEffectType.SkillDamagePercent:
                    stats.AddSkillDamagePercent(definition.EffectValue);
                    break;
                case ItemEffectType.MaxHealth:
                    stats.AddMaxHealth(definition.EffectValue, true);
                    break;
                case ItemEffectType.MoveSpeed:
                    stats.AddMoveSpeed(definition.EffectValue);
                    break;
                case ItemEffectType.MultiShot:
                    stats.AddProjectiles(Mathf.RoundToInt(definition.EffectValue));
                    break;
                case ItemEffectType.Pierce:
                    stats.AddPierce(Mathf.RoundToInt(definition.EffectValue));
                    break;
                case ItemEffectType.HealOnKill:
                    stats.AddHealOnKill(definition.EffectValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EvaluateSynergies()
        {
            if (IsMultiShotPierceSynergyActive ||
                GetStackCount(MultiShotItemId) == 0 ||
                GetStackCount(PierceItemId) == 0)
            {
                return;
            }

            IsMultiShotPierceSynergyActive = true;
            const string synergyName = "MULTI_SHOT + PIERCE";
            SynergyActivated?.Invoke(synergyName);
            Debug.Log(
                $"[PlayerInventory] Synergy activated: {synergyName}. All multi-shot projectiles now pierce.",
                this);
        }
    }
}
