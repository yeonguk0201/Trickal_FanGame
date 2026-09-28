using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RunProgress : MonoBehaviour
    {
        private readonly Dictionary<string, RoomRunState> roomStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemRewardSelectionState> rewardSelections = new(StringComparer.Ordinal);
        private readonly RunResourceWallet resources = new();
        private string activeRewardSelectionId;

        public int RunSeed { get; private set; }
        public bool HasRunSeed { get; private set; }
        public int CurrentFloor { get; private set; }
        public int CurrentRoom { get; private set; }
        public int KillCount { get; private set; }
        public bool IsProgressionStopped { get; private set; }
        public bool HasClearedFinalBoss { get; private set; }
        public GeneratedFloorGraph GeneratedGraph { get; private set; }
        public IReadOnlyDictionary<string, RoomRunState> RoomStates => roomStates;
        public IReadOnlyDictionary<string, ItemRewardSelectionState> RewardSelections => rewardSelections;
        public ItemRewardSelectionState PendingRewardSelection
        {
            get
            {
                ItemRewardSelectionState selection = GetRewardSelection(activeRewardSelectionId);
                return selection != null && !selection.IsCompleted ? selection : null;
            }
        }
        public bool IsRewardSelectionPending => PendingRewardSelection != null;
        public event Action FinalBossCleared;
        public event Action RewardSelectionStateChanged;

        public event Action<int, int> RoomChanged;
        public event Action<RunResourceType, int> ResourceChanged
        {
            add => resources.Changed += value;
            remove => resources.Changed -= value;
        }

        public int GetResourceCount(RunResourceType type) => resources.GetCount(type);

        // Resources are Run-only: nothing is granted after the Run ends, and ResetProgress returns them to zero.
        public bool CanAcceptResource(RunResourceType type) => !IsProgressionStopped && resources.CanAccept(type);

        public int TryAddResource(RunResourceType type, int amount)
        {
            return IsProgressionStopped ? 0 : resources.Add(type, amount);
        }

        public bool TrySetGeneratedGraph(GeneratedFloorGraph graph, out string error)
        {
            if (graph == null)
            {
                error = "RunProgress requires a generated graph.";
                return false;
            }

            if (!graph.TryValidate(out error))
            {
                error = $"RunProgress rejected an invalid generated graph. {error}";
                return false;
            }

            if (GeneratedGraph != null)
            {
                if (ReferenceEquals(GeneratedGraph, graph)) { error = null; return true; }
                error = "RunProgress already owns a generated graph for this Run.";
                return false;
            }

            GeneratedGraph = graph;
            foreach (GeneratedRoomNode node in graph.Nodes)
                roomStates.Add(node.RoomId, new RoomRunState(node.RoomId));
            error = null;
            return true;
        }

        public RoomRunState GetRoomState(string roomId)
        {
            return !string.IsNullOrWhiteSpace(roomId) && roomStates.TryGetValue(roomId, out RoomRunState state)
                ? state : null;
        }

        public ItemRewardSelectionState GetRewardSelection(string rewardId)
        {
            return !string.IsNullOrWhiteSpace(rewardId) &&
                   rewardSelections.TryGetValue(rewardId, out ItemRewardSelectionState selection)
                ? selection
                : null;
        }

        public ItemRewardSelectionState CreateRewardSelection(string rewardId,
            IReadOnlyList<ItemRewardCandidate> candidates)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("A reward selection requires a stable reward ID.", nameof(rewardId));
            }

            if (rewardSelections.TryGetValue(rewardId, out ItemRewardSelectionState existing))
            {
                return existing;
            }

            ItemRewardSelectionState selection = new(rewardId, candidates);
            rewardSelections.Add(rewardId, selection);
            RewardSelectionStateChanged?.Invoke();
            return selection;
        }

        public void NotifyRewardSelectionCompleted(ItemRewardSelectionState selection)
        {
            if (selection != null && selection.IsCompleted &&
                ReferenceEquals(GetRewardSelection(selection.RewardId), selection))
            {
                if (string.Equals(activeRewardSelectionId, selection.RewardId, StringComparison.Ordinal))
                    activeRewardSelectionId = null;
                RewardSelectionStateChanged?.Invoke();
            }
        }

        public bool TryActivateRewardSelection(ItemRewardSelectionState selection, out string error)
        {
            if (selection == null || selection.IsCompleted ||
                !ReferenceEquals(GetRewardSelection(selection.RewardId), selection))
            {
                error = "Only an owned incomplete reward selection can be activated.";
                return false;
            }

            ItemRewardSelectionState active = PendingRewardSelection;
            if (active != null && !ReferenceEquals(active, selection))
            {
                error = $"Reward selection '{active.RewardId}' must be closed before '{selection.RewardId}'.";
                return false;
            }

            activeRewardSelectionId = selection.RewardId;
            RewardSelectionStateChanged?.Invoke();
            error = null;
            return true;
        }

        public bool DeactivateRewardSelection(ItemRewardSelectionState selection)
        {
            if (selection == null || !string.Equals(activeRewardSelectionId, selection.RewardId,
                    StringComparison.Ordinal)) return false;
            activeRewardSelectionId = null;
            RewardSelectionStateChanged?.Invoke();
            return true;
        }

        public void RecordFinalBossCleared()
        {
            if (IsProgressionStopped || HasClearedFinalBoss) return;
            HasClearedFinalBoss = true;
            FinalBossCleared?.Invoke();
        }

        public bool TryInitializeRunSeed(int configuredRunSeed, out string error)
        {
            if (HasRunSeed)
            {
                if (RunSeed == configuredRunSeed)
                {
                    error = null;
                    return true;
                }

                error = $"Run seed is already initialized to {RunSeed} and cannot change during the Run.";
                return false;
            }

            RunSeed = configuredRunSeed;
            HasRunSeed = true;
            error = null;
            return true;
        }

        public void RecordRoomEntry(int floorNumber, int roomNumber)
        {
            if (IsProgressionStopped)
            {
                return;
            }

            CurrentFloor = Mathf.Max(1, floorNumber);
            CurrentRoom = Mathf.Max(1, roomNumber);
            GetRoomState(FloorGenerator.BuildRoomId(CurrentFloor, CurrentRoom))?.MarkVisited();
            RoomChanged?.Invoke(CurrentFloor, CurrentRoom);
            Debug.Log($"Run progress: Floor {CurrentFloor}, Room {CurrentRoom}.", this);
        }

        public void StopProgression()
        {
            IsProgressionStopped = true;
            Debug.Log($"Run progress stopped at Floor {CurrentFloor}, Room {CurrentRoom}.", this);
        }

        public void RecordKill()
        {
            if (!IsProgressionStopped)
            {
                KillCount++;
            }
        }

        public void ResetProgress()
        {
            CurrentFloor = 0;
            CurrentRoom = 0;
            KillCount = 0;
            IsProgressionStopped = false;
            HasClearedFinalBoss = false;
            GeneratedGraph = null;
            activeRewardSelectionId = null;
            roomStates.Clear();
            rewardSelections.Clear();
            resources.Clear();
            RewardSelectionStateChanged?.Invoke();
        }
    }
}
