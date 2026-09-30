namespace TrickalFanGame.Item
{
    public readonly struct AcquiredItem
    {
        public AcquiredItem(string itemId, int floor, int order, float acquiredRealtime)
        {
            ItemId = itemId;
            Floor = floor;
            Order = order;
            AcquiredRealtime = acquiredRealtime;
        }

        public string ItemId { get; }
        public int Floor { get; }
        public int Order { get; }
        public float AcquiredRealtime { get; }
    }
}
