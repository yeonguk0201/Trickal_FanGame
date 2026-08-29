namespace TrickalFanGame.Room
{
    public sealed class RoomRunState
    {
        public RoomRunState(string roomId) => RoomId = roomId;
        public string RoomId { get; }
        public bool HasVisited { get; private set; }
        public bool IsCleared { get; private set; }
        public bool HasClaimedArtifact { get; private set; }
        public void MarkVisited() => HasVisited = true;
        public void MarkCleared() { HasVisited = true; IsCleared = true; }
        public void MarkArtifactClaimed() { HasVisited = true; HasClaimedArtifact = true; }
    }
}
