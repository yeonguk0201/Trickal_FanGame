using System;
using System.Collections.Generic;

namespace TrickalFanGame.Item
{
    public sealed class ItemRewardSelectionState
    {
        private readonly ItemRewardCandidate[] candidates;

        public ItemRewardSelectionState(string rewardId, IReadOnlyList<ItemRewardCandidate> configuredCandidates)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("A reward selection requires a stable reward ID.", nameof(rewardId));
            }

            if (configuredCandidates == null || configuredCandidates.Count == 0)
            {
                throw new ArgumentException("A reward selection requires at least one candidate.",
                    nameof(configuredCandidates));
            }

            RewardId = rewardId;
            candidates = new ItemRewardCandidate[configuredCandidates.Count];
            HashSet<string> stableIds = new(StringComparer.Ordinal);
            for (int index = 0; index < configuredCandidates.Count; index++)
            {
                ItemRewardCandidate candidate = configuredCandidates[index];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.StableId) ||
                    !stableIds.Add(candidate.StableId))
                {
                    throw new ArgumentException("Reward candidates must be present and have unique stable IDs.",
                        nameof(configuredCandidates));
                }

                candidates[index] = candidate;
            }
        }

        public string RewardId { get; }
        public IReadOnlyList<ItemRewardCandidate> Candidates => candidates;
        public bool IsCompleted { get; private set; }
        public string SelectedCandidateId { get; private set; }

        internal bool TryComplete(ItemRewardCandidate selectedCandidate)
        {
            if (IsCompleted || selectedCandidate == null ||
                Array.IndexOf(candidates, selectedCandidate) < 0)
            {
                return false;
            }

            SelectedCandidateId = selectedCandidate.StableId;
            IsCompleted = true;
            return true;
        }
    }
}
