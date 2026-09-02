using System;
using System.Collections.Generic;

namespace TrickalFanGame.Item
{
    public static class ArtifactRewardSelector
    {
        public const int CommonWeight = 60;
        public const int UncommonWeight = 25;
        public const int RareWeight = 12;
        public const int EpicWeight = 3;

        public static bool TryChoose(
            IReadOnlyList<ItemDefinition> itemPool,
            PlayerInventory inventory,
            int runSeed,
            string rewardId,
            out ItemDefinition definition)
        {
            definition = null;
            if (itemPool == null || itemPool.Count == 0)
            {
                return false;
            }

            List<ItemDefinition> candidates = new(itemPool.Count);
            for (int index = 0; index < itemPool.Count; index++)
            {
                ItemDefinition candidate = itemPool[index];
                if (!IsEligible(candidate, inventory))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort((left, right) => string.CompareOrdinal(left.ItemId, right.ItemId));
            int totalWeight = 0;
            uint stateHash = Mix(unchecked((uint)runSeed), rewardId ?? string.Empty);
            foreach (ItemDefinition candidate in candidates)
            {
                int currentStacks = inventory != null ? inventory.GetStackCount(candidate.ItemId) : 0;
                stateHash = Mix(stateHash, candidate.ItemId);
                stateHash = Avalanche(stateHash ^ unchecked((uint)currentStacks));
                totalWeight += GetWeight(candidate.Rarity);
            }

            int roll = (int)(stateHash % unchecked((uint)totalWeight));
            foreach (ItemDefinition candidate in candidates)
            {
                roll -= GetWeight(candidate.Rarity);
                if (roll < 0)
                {
                    definition = candidate;
                    return true;
                }
            }

            throw new InvalidOperationException("A positive artifact reward weight must select a candidate.");
        }

        public static int GetWeight(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => CommonWeight,
                ItemRarity.Uncommon => UncommonWeight,
                ItemRarity.Rare => RareWeight,
                ItemRarity.Epic => EpicWeight,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
            };
        }

        public static bool IsEligible(ItemDefinition definition, PlayerInventory inventory)
        {
            if (definition == null || !definition.IsValid || !definition.IsActive)
            {
                return false;
            }

            int currentStacks = inventory != null ? inventory.GetStackCount(definition.ItemId) : 0;
            return definition.MaxStacks <= 0 || currentStacks < definition.MaxStacks;
        }

        public static bool AreAllActiveArtifactsAtMaximum(
            IReadOnlyList<ItemDefinition> itemPool,
            PlayerInventory inventory)
        {
            if (itemPool == null || inventory == null)
            {
                return false;
            }

            bool foundActive = false;
            for (int index = 0; index < itemPool.Count; index++)
            {
                ItemDefinition definition = itemPool[index];
                if (definition == null || !definition.IsValid || !definition.IsActive)
                {
                    continue;
                }

                foundActive = true;
                if (definition.MaxStacks <= 0 ||
                    inventory.GetStackCount(definition.ItemId) < definition.MaxStacks)
                {
                    return false;
                }
            }

            return foundActive;
        }

        private static uint Mix(uint hash, string value)
        {
            const uint fnvPrime = 16777619u;
            hash = hash == 0u ? 2166136261u : hash;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= fnvPrime;
            }

            return Avalanche(hash);
        }

        private static uint Avalanche(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }
    }
}
