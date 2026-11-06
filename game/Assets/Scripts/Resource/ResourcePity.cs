namespace TrickalFanGame.Resource
{
    // Drop-0: the pity counter for keys and bombs. Each first clear of a combat room counts one room for each resource
    // the player has not gained since; gaining one from any source (pickup, shop) resets its count. When a clear finds
    // the count already at the threshold, that clear guarantees one of the resource and the count starts over.
    // The counter lives in the Run only (no mid-Run save), so it depends on the order the player clears rooms.
    public sealed class ResourcePity
    {
        public const int KeyThreshold = 5;
        public const int BombThreshold = 4;

        public int KeyDroughtClears { get; private set; }
        public int BombDroughtClears { get; private set; }

        public static bool IsTracked(RunResourceType type) => type is RunResourceType.Key or RunResourceType.Bomb;

        public static int ThresholdOf(RunResourceType type) =>
            type == RunResourceType.Key ? KeyThreshold : type == RunResourceType.Bomb ? BombThreshold : 0;

        public int DroughtOf(RunResourceType type) =>
            type == RunResourceType.Key ? KeyDroughtClears : type == RunResourceType.Bomb ? BombDroughtClears : 0;

        // Called once per first combat room clear. Returns which resources this clear must guarantee.
        public void RecordCombatClear(out bool guaranteeKey, out bool guaranteeBomb)
        {
            guaranteeKey = Advance(KeyDroughtClears, KeyThreshold, out int key);
            guaranteeBomb = Advance(BombDroughtClears, BombThreshold, out int bomb);
            KeyDroughtClears = key;
            BombDroughtClears = bomb;
        }

        public void RecordGain(RunResourceType type, int amount)
        {
            if (amount <= 0) return;
            if (type == RunResourceType.Key) KeyDroughtClears = 0;
            else if (type == RunResourceType.Bomb) BombDroughtClears = 0;
        }

        public void Clear()
        {
            KeyDroughtClears = 0;
            BombDroughtClears = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel: puts the count at the threshold so the next combat clear guarantees the resource.
        public void SetDroughtForDevelopment(RunResourceType type, int clears)
        {
            if (type == RunResourceType.Key) KeyDroughtClears = clears < 0 ? 0 : clears;
            else if (type == RunResourceType.Bomb) BombDroughtClears = clears < 0 ? 0 : clears;
        }
#endif

        private static bool Advance(int current, int threshold, out int next)
        {
            if (current >= threshold)
            {
                next = 0;
                return true;
            }

            next = current + 1;
            return false;
        }
    }
}
