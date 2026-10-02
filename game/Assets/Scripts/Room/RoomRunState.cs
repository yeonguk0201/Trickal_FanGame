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
        public event Action Changed;

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
