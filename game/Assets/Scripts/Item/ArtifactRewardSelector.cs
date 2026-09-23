using System;
using System.Collections.Generic;

namespace TrickalFanGame.Item
{
    public static class ArtifactRewardSelector
    {
        public const int MaximumCandidateCount = 3;
        public const float FallbackHealMaxHealthRatio = 0.25f;
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
            IReadOnlyList<ItemRewardCandidate> rewardCandidates = BuildCandidates(
                itemPool, inventory, runSeed, rewardId);
            for (int index = 0; index < rewardCandidates.Count; index++)
            {
                if (rewardCandidates[index].IsItem)
                {
                    definition = rewardCandidates[index].Definition;
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<ItemRewardCandidate> BuildCandidates(
            IReadOnlyList<ItemDefinition> itemPool,
            PlayerInventory inventory,
            int runSeed,
            string rewardId)
        {
            if (itemPool == null || itemPool.Count == 0)
            {
                return Array.Empty<ItemRewardCandidate>();
            }

            Dictionary<string, ItemDefinition> activeById = new(StringComparer.Ordinal);
            for (int index = 0; index < itemPool.Count; index++)
            {
                ItemDefinition candidate = itemPool[index];
                if (candidate == null || !candidate.IsValid || !candidate.IsActive ||
                    activeById.ContainsKey(candidate.ItemId))
                {
                    continue;
                }

                activeById.Add(candidate.ItemId, candidate);
            }

            if (activeById.Count == 0)
            {
                return Array.Empty<ItemRewardCandidate>();
            }

            List<ItemDefinition> candidates = new(activeById.Values);
            candidates.Sort((left, right) => string.CompareOrdinal(left.ItemId, right.ItemId));
            candidates.RemoveAll(candidate => !IsEligible(candidate, inventory));

            uint stateHash = Mix(unchecked((uint)runSeed), rewardId ?? string.Empty);
            foreach (ItemDefinition candidate in candidates)
            {
                int currentStacks = inventory != null ? inventory.GetStackCount(candidate.ItemId) : 0;
                stateHash = Mix(stateHash, candidate.ItemId);
                stateHash = Avalanche(stateHash ^ unchecked((uint)currentStacks));
            }

            List<ItemRewardCandidate> result = new(MaximumCandidateCount);
            while (result.Count < MaximumCandidateCount && candidates.Count > 0)
            {
                ItemDefinition selected = ChooseWeighted(candidates, stateHash);
                int currentStacks = inventory != null ? inventory.GetStackCount(selected.ItemId) : 0;
                result.Add(ItemRewardCandidate.ForItem(selected, currentStacks));
                candidates.Remove(selected);
                stateHash = Mix(stateHash, selected.ItemId);
                stateHash = Avalanche(stateHash ^ unchecked((uint)result.Count));
            }

            if (result.Count < MaximumCandidateCount)
            {
                result.Add(ItemRewardCandidate.ForHealing(FallbackHealMaxHealthRatio));
            }

            return result.ToArray();
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

        private static ItemDefinition ChooseWeighted(IReadOnlyList<ItemDefinition> candidates, uint stateHash)
        {
            int totalWeight = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                totalWeight += GetWeight(candidates[index].Rarity);
            }

            int roll = (int)(stateHash % unchecked((uint)totalWeight));
            for (int index = 0; index < candidates.Count; index++)
            {
                ItemDefinition candidate = candidates[index];
                roll -= GetWeight(candidate.Rarity);
                if (roll < 0)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("A positive item reward weight must select a candidate.");
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
