using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [DisallowMultipleComponent]
    public sealed class ItemRewardSelectionSession : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerActionState playerActionState;

        private ItemRewardSelectionState state;
        private RunProgress subscribedProgress;

        public ItemRewardSelectionState State => state;
        public IReadOnlyList<ItemRewardCandidate> Candidates =>
            state != null ? state.Candidates : Array.Empty<ItemRewardCandidate>();
        public bool IsOpen => state != null && !state.IsCompleted &&
                              ReferenceEquals(runProgress?.PendingRewardSelection, state);
        public event Action<ItemRewardSelectionState> Opened;
        public event Action<ItemRewardSelectionState, ItemRewardCandidate> Completed;
        public event Action<ItemRewardSelectionState> Cancelled;

        public void Configure(RunProgress configuredProgress, PlayerInventory configuredInventory,
            Health configuredHealth, PlayerActionState configuredActionState)
        {
            UnsubscribeProgress();
            runProgress = configuredProgress;
            inventory = configuredInventory;
            playerHealth = configuredHealth;
            playerActionState = configuredActionState;
            SubscribeProgress();
            SyncActionBlock();
        }

        private void OnEnable()
        {
            SubscribeProgress();
            SyncActionBlock();
        }

        private void OnDisable() => UnsubscribeProgress();
        private void OnDestroy() => UnsubscribeProgress();

        public bool TryOpen(IReadOnlyList<ItemDefinition> itemPool, string rewardId, out string error)
        {
            if (!TryValidateDependencies(out error)) return false;
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                error = "A reward selection requires a stable reward ID.";
                return false;
            }

            ItemRewardSelectionState pending = runProgress.PendingRewardSelection;
            if (pending != null && !string.Equals(pending.RewardId, rewardId, StringComparison.Ordinal))
            {
                error = $"Reward selection '{pending.RewardId}' must be completed before '{rewardId}'.";
                SyncActionBlock();
                return false;
            }

            state = runProgress.GetRewardSelection(rewardId);
            if (state == null)
            {
                IReadOnlyList<ItemRewardCandidate> candidates = ArtifactRewardSelector.BuildCandidates(
                    itemPool, inventory, runProgress.RunSeed, rewardId);
                if (candidates.Count == 0)
                {
                    error = $"Reward selection '{rewardId}' has no valid candidates.";
                    SyncActionBlock();
                    return false;
                }

                state = runProgress.CreateRewardSelection(rewardId, candidates);
            }

            SyncActionBlock();
            if (state.IsCompleted)
            {
                error = $"Reward selection '{rewardId}' is already completed.";
                return false;
            }

            if (!runProgress.TryActivateRewardSelection(state, out error))
            {
                SyncActionBlock();
                return false;
            }

            Opened?.Invoke(state);
            error = null;
            return true;
        }

        public bool TrySelect(int candidateIndex)
        {
            return IsOpen && candidateIndex >= 0 && candidateIndex < state.Candidates.Count &&
                   TryApplyAndComplete(state.Candidates[candidateIndex]);
        }

        public bool TrySelect(string candidateId)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(candidateId)) return false;
            for (int index = 0; index < state.Candidates.Count; index++)
            {
                ItemRewardCandidate candidate = state.Candidates[index];
                if (string.Equals(candidate.StableId, candidateId, StringComparison.Ordinal))
                {
                    return TryApplyAndComplete(candidate);
                }
            }

            return false;
        }

        public bool TryCancel()
        {
            if (!IsOpen || !runProgress.DeactivateRewardSelection(state)) return false;
            SyncActionBlock();
            Cancelled?.Invoke(state);
            return true;
        }

        private bool TryApplyAndComplete(ItemRewardCandidate candidate)
        {
            if (candidate.IsItem)
            {
                if (!inventory.TryAcquire(candidate.Definition)) return false;
            }
            else if (candidate.IsHealing)
            {
                if (playerHealth.IsDead) return false;
                playerHealth.Heal(playerHealth.GetMaxHealthRatioAmount(candidate.HealMaxHealthRatio));
            }
            else
            {
                return false;
            }

            if (!state.TryComplete(candidate))
            {
                throw new InvalidOperationException(
                    $"Reward selection '{state.RewardId}' changed while its result was being applied.");
            }

            runProgress.NotifyRewardSelectionCompleted(state);
            SyncActionBlock();
            Completed?.Invoke(state, candidate);
            return true;
        }

        private bool TryValidateDependencies(out string error)
        {
            if (runProgress == null || inventory == null || playerHealth == null || playerActionState == null)
            {
                error = "Reward selection requires RunProgress, PlayerInventory, Health, and PlayerActionState.";
                return false;
            }

            if (!runProgress.HasRunSeed)
            {
                error = "Reward selection requires an initialized Run seed.";
                return false;
            }

            error = null;
            return true;
        }

        private void SyncActionBlock()
        {
            if (state != null && !ReferenceEquals(runProgress?.GetRewardSelection(state.RewardId), state))
            {
                state = null;
            }

            playerActionState?.SetRewardSelectionBlocked(runProgress?.IsRewardSelectionPending == true);
        }

        private void SubscribeProgress()
        {
            if (runProgress == null || subscribedProgress == runProgress) return;
            UnsubscribeProgress();
            runProgress.RewardSelectionStateChanged += SyncActionBlock;
            subscribedProgress = runProgress;
        }

        private void UnsubscribeProgress()
        {
            if (subscribedProgress == null) return;
            subscribedProgress.RewardSelectionStateChanged -= SyncActionBlock;
            subscribedProgress = null;
        }
    }
}
