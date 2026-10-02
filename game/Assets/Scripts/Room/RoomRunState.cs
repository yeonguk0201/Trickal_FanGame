using System;
using System.Collections.Generic;

namespace TrickalFanGame.Room
{
    public sealed class RoomRunState
    {
        public RoomRunState(string roomId) => RoomId = roomId;
        public string RoomId { get; }
        public bool HasVisited { get; private set; }
        public bool IsCleared { get; private set; }
        public bool HasClaimedArtifact { get; private set; }
        public int CompletedWaveCount { get; private set; }
        public bool HasGrantedClearReward { get; private set; }
        public bool IsKeyLockOpen { get; private set; }
        public IReadOnlyCollection<string> DestroyedObstacleIds => destroyedObstacleIds;
        private readonly HashSet<string> destroyedObstacleIds = new(StringComparer.Ordinal);
        // Secret rooms only: neighbor room IDs whose hidden passage is open on both sides.
        public IReadOnlyCollection<string> OpenedSecretPassages => openedSecretPassages;
        private readonly HashSet<string> openedSecretPassages = new(StringComparer.Ordinal);
        public bool IsSecretDiscovered => HasVisited || openedSecretPassages.Count > 0;
        // Chest-0: chests placed in this room during the Run, by room-local chest ID.
        public IReadOnlyCollection<ChestRunState> Chests => chests.Values;
        private readonly Dictionary<string, ChestRunState> chests = new(StringComparer.Ordinal);
        public event Action Changed;

        public ChestRunState GetChest(string chestId) =>
            !string.IsNullOrWhiteSpace(chestId) && chests.TryGetValue(chestId, out ChestRunState chest) ? chest : null;

        // The first build of a chest records it; later builds of the room get the stored record and its state. A chest
        // ID keeps its kind for the whole Run.
        public ChestRunState RegisterChest(string chestId, ChestKind kind)
        {
            if (!StableRoomId.TryValidate(chestId, "Chest", out string error))
                throw new ArgumentException(error, nameof(chestId));
            if (!Enum.IsDefined(typeof(ChestKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind),
                    $"Chest '{chestId}' has an undefined kind {(int)kind}.");
            if (chests.TryGetValue(chestId, out ChestRunState existing))
            {
                if (existing.Kind != kind)
                    throw new InvalidOperationException(
                        $"Room '{RoomId}' chest '{chestId}' is {existing.Kind} and cannot become {kind}.");
                return existing;
            }

            ChestRunState chest = new(chestId, kind);
            chests.Add(chestId, chest);
            Changed?.Invoke();
            return chest;
        }

        public bool TryRecordChestPosition(string chestId, UnityEngine.Vector2 localPosition)
        {
            ChestRunState chest = GetChest(chestId);
            if (chest == null || chest.HasPosition) return false;
            chest.HasPosition = true;
            chest.LocalPosition = localPosition;
            return true;
        }

        public bool TryOpenChest(string chestId)
        {
            ChestRunState chest = GetChest(chestId);
            if (chest == null || !chest.IsClosed) return false;
            chest.IsOpened = true;
            Changed?.Invoke();
            return true;
        }

        // Leaving the floor: every unopened chest of the room is gone for the rest of the Run.
        public int DiscardClosedChests()
        {
            int discarded = 0;
            foreach (ChestRunState chest in chests.Values)
            {
                if (!chest.IsClosed) continue;
                chest.IsDiscarded = true;
                discarded++;
            }

            if (discarded > 0) Changed?.Invoke();
            return discarded;
        }

        public bool IsSecretPassageOpen(string neighborRoomId) =>
            !string.IsNullOrWhiteSpace(neighborRoomId) && openedSecretPassages.Contains(neighborRoomId);

        public bool TryOpenSecretPassage(string neighborRoomId)
        {
            if (string.IsNullOrWhiteSpace(neighborRoomId) || !openedSecretPassages.Add(neighborRoomId)) return false;
            Changed?.Invoke();
            return true;
        }

        // Entering the secret room opens every hidden passage it has, so the player can walk out either way.
        public bool TryOpenSecretPassages(IEnumerable<string> neighborRoomIds)
        {
            bool opened = false;
            foreach (string neighborRoomId in neighborRoomIds)
                opened |= !string.IsNullOrWhiteSpace(neighborRoomId) && openedSecretPassages.Add(neighborRoomId);
            if (opened) Changed?.Invoke();
            return opened;
        }

        public void MarkVisited()
        {
            if (HasVisited) return;
            HasVisited = true;
            Changed?.Invoke();
        }

        public void MarkCleared()
        {
            if (HasVisited && IsCleared) return;
            HasVisited = true;
            IsCleared = true;
            Changed?.Invoke();
        }

        public void MarkPreCleared()
        {
            if (IsCleared) return;
            IsCleared = true;
            Changed?.Invoke();
        }

        public void MarkArtifactClaimed()
        {
            if (HasVisited && HasClaimedArtifact) return;
            HasVisited = true;
            HasClaimedArtifact = true;
            Changed?.Invoke();
        }

        public bool TryMarkWaveCompleted(int waveNumber)
        {
            if (waveNumber <= CompletedWaveCount) return false;
            if (waveNumber != CompletedWaveCount + 1)
                throw new InvalidOperationException(
                    $"Room '{RoomId}' cannot complete wave {waveNumber} after wave {CompletedWaveCount}.");

            HasVisited = true;
            CompletedWaveCount = waveNumber;
            Changed?.Invoke();
            return true;
        }

        // 그건 내 잔상: an escaped Encounter restarts from its first wave on the next entry. A cleared room never resets.
        public bool ResetEncounterProgress()
        {
            if (IsCleared || CompletedWaveCount == 0) return false;
            CompletedWaveCount = 0;
            Changed?.Invoke();
            return true;
        }

        public bool IsObstacleDestroyed(string obstacleId) =>
            !string.IsNullOrWhiteSpace(obstacleId) && destroyedObstacleIds.Contains(obstacleId);

        public bool TryMarkObstacleDestroyed(string obstacleId)
        {
            if (string.IsNullOrWhiteSpace(obstacleId) || !destroyedObstacleIds.Add(obstacleId)) return false;
            HasVisited = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryMarkClearRewardGranted()
        {
            if (HasGrantedClearReward) return false;
            HasVisited = true;
            HasGrantedClearReward = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryOpenKeyLock()
        {
            if (IsKeyLockOpen) return false;
            IsKeyLockOpen = true;
            Changed?.Invoke();
            return true;
        }
    }
}
