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
    }
}
