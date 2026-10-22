using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Shop;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RunProgress : MonoBehaviour
    {
        private readonly Dictionary<string, RoomRunState> roomStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemRewardSelectionState> rewardSelections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ShopStockState> shopStocks = new(StringComparer.Ordinal);
        private readonly RunResourceWallet resources = new();
        private readonly HashSet<string> clearedRoomIds = new(StringComparer.Ordinal);
        // Spell-6 (네티의 지도): floors whose whole map the minimap shows.
        private readonly HashSet<int> revealedMapFloors = new();
        private string activeRewardSelectionId;

        public int RunSeed { get; private set; }
        public bool HasRunSeed { get; private set; }
        public int CurrentFloor { get; private set; }
        public int CurrentRoom { get; private set; }
        public int KillCount { get; private set; }
        // Obstacle-7: obstacles (trees) broken while burning in this Run, for a later character unlock.
        public int BurnedObstacleCount { get; private set; }
        public bool IsProgressionStopped { get; private set; }
        public bool HasClearedFinalBoss { get; private set; }
        public GeneratedFloorGraph GeneratedGraph { get; private set; }
        // Spell-6 (엘리스의 타로카드): cards drawn so far in this Run; the next draw uses this index.
        public int TarotDrawCount { get; private set; }
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
        // Power-1: a room's fight was won for the first time in this Run (the 권능 gauge charges on it).
        public event Action<string> RoomCleared;
        public event Action RewardSelectionStateChanged;

        public event Action<int, int> RoomChanged;
        // Spell-6: a floor's whole map was revealed (네티의 지도) or a room changed what it holds (골디의 초대장).
        public event Action MapChanged;
        public event Action<RunResourceType, int> ResourceChanged
        {
            add
            {
                resources.Changed += value;
                elifChanged += value;
            }
            remove
            {
                resources.Changed -= value;
                elifChanged -= value;
            }
        }

        private Action<RunResourceType, int> elifChanged;

        // Elif-2 (contract 27 §4.3): the permanent elif is not a Run resource. The Run starts from the server balance
        // (minus what pending earlier Runs spent), adds what it picks up and subtracts what it spends; only the
        // gained and spent amounts are saved with the Run result. Development grants are usable but never saved.
        public int ElifStartingBalance { get; private set; }
        public int ElifGained { get; private set; }
        public int ElifSpent { get; private set; }
        public int DevelopmentElif { get; private set; }
        public int AvailableElif => Mathf.Max(0, ElifStartingBalance + ElifGained + DevelopmentElif - ElifSpent);

        public int GetResourceCount(RunResourceType type) =>
            type == RunResourceType.Elif ? AvailableElif : resources.GetCount(type);

        // Resources are Run-only: nothing is granted after the Run ends, and ResetProgress returns them to zero.
        // Elif has no Run cap.
        public bool CanAcceptResource(RunResourceType type) =>
            !IsProgressionStopped && (type == RunResourceType.Elif || resources.CanAccept(type));

        public int TryAddResource(RunResourceType type, int amount)
        {
            if (IsProgressionStopped) return 0;
            if (type != RunResourceType.Elif) return resources.Add(type, amount);
            if (amount <= 0) return 0;
            ElifGained += amount;
            NotifyElifChanged();
            return amount;
        }

        public bool TrySpendResource(RunResourceType type, int amount = 1)
        {
            if (IsProgressionStopped) return false;
            if (type != RunResourceType.Elif) return resources.TrySpend(type, amount);
            if (amount <= 0 || AvailableElif < amount) return false;
            ElifSpent += amount;
            NotifyElifChanged();
            return true;
        }

        // Set when the Run starts, before anything is picked up.
        public void SetElifStartingBalance(int balance)
        {
            ElifStartingBalance = Mathf.Clamp(balance, 0, PermanentElif.BalanceMax);
            NotifyElifChanged();
        }

        public void AddDevelopmentElif(int amount)
        {
            if (IsProgressionStopped || amount <= 0) return;
            DevelopmentElif += amount;
            NotifyElifChanged();
        }

        private void NotifyElifChanged() => elifChanged?.Invoke(RunResourceType.Elif, AvailableElif);

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

        public ShopStockState GetShopStock(string shopId)
        {
            return !string.IsNullOrWhiteSpace(shopId) && shopStocks.TryGetValue(shopId, out ShopStockState stock)
                ? stock : null;
        }

        // A shop rolls its offers once per Run; later floor builds reuse the stored stock and its sold slots.
        public ShopStockState GetOrCreateShopStock(string shopId, Func<ShopStockState> create)
        {
            if (string.IsNullOrWhiteSpace(shopId))
                throw new ArgumentException("A shop stock requires a stable shop ID.", nameof(shopId));
            if (shopStocks.TryGetValue(shopId, out ShopStockState existing)) return existing;
            ShopStockState created = create?.Invoke();
            if (created == null || created.ShopId != shopId)
                throw new InvalidOperationException($"Shop stock factory must create stock '{shopId}'.");
            shopStocks.Add(shopId, created);
            return created;
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

        public bool IsFloorMapRevealed(int floorNumber) => revealedMapFloors.Contains(floorNumber);

        // Returns false when the floor was already revealed; the map stays revealed for the rest of the Run.
        public bool TryRevealFloorMap(int floorNumber)
        {
            if (floorNumber < 1 || !revealedMapFloors.Add(floorNumber)) return false;
            MapChanged?.Invoke();
            return true;
        }

        public void NotifyMapChanged() => MapChanged?.Invoke();

        // Returns the index of this draw and counts it.
        public int RecordTarotDraw() => TarotDrawCount++;

        public void RecordRoomCleared(string roomId)
        {
            if (IsProgressionStopped || string.IsNullOrWhiteSpace(roomId) || !clearedRoomIds.Add(roomId)) return;
            RoomCleared?.Invoke(roomId);
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

            int previousFloor = CurrentFloor;
            CurrentFloor = Mathf.Max(1, floorNumber);
            CurrentRoom = Mathf.Max(1, roomNumber);
            if (previousFloor > 0 && previousFloor != CurrentFloor) DiscardClosedChests(previousFloor);
            string roomId = FloorGenerator.BuildRoomId(CurrentFloor, CurrentRoom);
            RoomRunState state = GetRoomState(roomId);
            state?.MarkVisited();
            OpenSecretPassagesOnEntry(roomId, state);
            RoomChanged?.Invoke(CurrentFloor, CurrentRoom);
            Debug.Log($"Run progress: Floor {CurrentFloor}, Room {CurrentRoom}.", this);
        }

        // Chest-0: unopened chests vanish with the floor, like floor pickups, and are never granted again.
        private void DiscardClosedChests(int floorNumber)
        {
            GeneratedFloor floor = GeneratedGraph?.FindFloor(floorNumber);
            if (floor == null) return;
            foreach (GeneratedRoomNode node in floor.Nodes) GetRoomState(node.RoomId)?.DiscardClosedChests();
        }

        private void OpenSecretPassagesOnEntry(string roomId, RoomRunState state)
        {
            GeneratedFloor floor = GeneratedGraph?.FindFloor(CurrentFloor);
            if (floor == null || state == null) return;
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (node.RoomId != roomId) continue;
                // Walking in through a bombed wall keeps the other walls sealed. Only an entry that left no way
                // out (a pit) opens them all.
                if (node.Role == GeneratedRoomRole.Secret && state.OpenedSecretPassages.Count == 0)
                    state.TryOpenSecretPassages(node.ConnectedRoomIds);
                return;
            }
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

        public void RecordBurnedObstacle()
        {
            if (!IsProgressionStopped)
            {
                BurnedObstacleCount++;
            }
        }

        public void ResetProgress()
        {
            CurrentFloor = 0;
            CurrentRoom = 0;
            KillCount = 0;
            BurnedObstacleCount = 0;
            IsProgressionStopped = false;
            HasClearedFinalBoss = false;
            GeneratedGraph = null;
            activeRewardSelectionId = null;
            roomStates.Clear();
            clearedRoomIds.Clear();
            revealedMapFloors.Clear();
            TarotDrawCount = 0;
            rewardSelections.Clear();
            shopStocks.Clear();
            resources.Clear();
            ElifStartingBalance = 0;
            ElifGained = 0;
            ElifSpent = 0;
            DevelopmentElif = 0;
            NotifyElifChanged();
            RewardSelectionStateChanged?.Invoke();
        }
    }
}
