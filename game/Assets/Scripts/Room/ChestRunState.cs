namespace TrickalFanGame.Room
{
    // Chest-0: one chest of a room for the Run, owned by the room's RoomRunState. A chest opens at most once, and an
    // unopened chest is discarded when the player leaves its floor; neither state ever returns to closed.
    public sealed class ChestRunState
    {
        internal ChestRunState(string chestId, ChestKind kind)
        {
            ChestId = chestId;
            Kind = kind;
        }

        public string ChestId { get; }
        public ChestKind Kind { get; }
        public bool IsOpened { get; internal set; }
        public bool IsDiscarded { get; internal set; }
        // Room-local position where the chest was first placed, so a rebuilt room puts it back on the same spot.
        public bool HasPosition { get; internal set; }
        public UnityEngine.Vector2 LocalPosition { get; internal set; }
        // Jjangsem-1: the seed its contents were rolled from, so a rebuilt room can restore any chest, not only the
        // clear chest whose seed comes from the room.
        public bool HasContentSeed { get; internal set; }
        public int ContentSeed { get; internal set; }
        public bool IsClosed => !IsOpened && !IsDiscarded;
    }
}
