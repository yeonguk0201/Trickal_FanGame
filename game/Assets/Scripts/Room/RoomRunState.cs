using System;

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
        public event Action Changed;

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

        public bool TryMarkClearRewardGranted()
        {
            if (HasGrantedClearReward) return false;
            HasVisited = true;
            HasGrantedClearReward = true;
            Changed?.Invoke();
            return true;
        }
    }
}
