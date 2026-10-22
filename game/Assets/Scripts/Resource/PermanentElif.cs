using UnityEngine;

namespace TrickalFanGame.Resource
{
    // Elif-2 (contract 27 §3): rules for the permanent elif, the rare currency that outlives the Run.
    // A floor gold pickup turns into one elif at ConversionChance, and 아이시아의 지갑 rolls 0-4 elif once.
    // Both rolls hash the Run seed with the drop's identity and never consume the drop tables' random sequence,
    // so a seed still places gold in the same spots and only some of those pickups become elif.
    public static class PermanentElif
    {
        public const float ConversionChance = 0.07f;
        public const string ArtworkKey = "permanent-elif";
        public const string DropId = "permanent-elif";
        // Backend limits (backend/src/contracts/permanent-elif.ts): a Run save above RunGainMax is rejected.
        public const int RunGainMax = 50;
        public const int BalanceMax = 9999;

        // 아이시아의 지갑: weight of 0, 1, 2, 3 and 4 elif (40/30/15/10/5 %, average 1.1).
        public static readonly int[] WalletWeights = { 40, 30, 15, 10, 5 };

        private const uint ConversionSalt = 0x45C1F0A7u;
        private const uint WalletSalt = 0x7A11E7B3u;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel: the next floor gold pickup that is spawned becomes elif, then this clears.
        public static bool DevelopmentForceNextConversion { get; set; }
#endif

        // The roll of one floor gold pickup in [0, 1); below ConversionChance it becomes elif.
        public static float ConversionRoll(int runSeed, string roomId, string sourceId, int pickupIndex)
        {
            uint hash = Hash(runSeed, ConversionSalt, roomId);
            hash = Hash(hash, sourceId);
            hash = Mix(unchecked(hash ^ (uint)pickupIndex));
            return (hash >> 8) * (1f / 16777216f);
        }

        public static bool ShouldConvert(int runSeed, string roomId, string sourceId, int pickupIndex) =>
            ConversionRoll(runSeed, roomId, sourceId, pickupIndex) < ConversionChance;

        // 아이시아의 지갑 holds one stack, so the item ID is the instance: one roll per Run.
        public static int RollWallet(int runSeed, string itemId)
        {
            uint hash = Hash(runSeed, WalletSalt, itemId);
            int total = 0;
            foreach (int weight in WalletWeights) total += weight;
            int pick = (int)(((ulong)Mix(hash) * (ulong)total) >> 32);
            for (int amount = 0; amount < WalletWeights.Length; amount++)
            {
                if (pick < WalletWeights[amount]) return amount;
                pick -= WalletWeights[amount];
            }

            return 0;
        }

        // Called once where a drop roll spawns a floor pickup (chest contents, obstacle drops, clear drops). Only a
        // gold pickup of a seeded Run can turn; copies (멜룬카드) and restored drops are never rolled again.
        public static bool TryConvertSpawnedGold(GameObject spawned, int runSeed, bool hasRunSeed, string roomId,
            string sourceId, int pickupIndex)
        {
            if (spawned == null || !spawned.TryGetComponent(out RunResourcePickup pickup) ||
                pickup.ResourceType != RunResourceType.Gold || pickup.IsCollected)
                return false;
            bool convert = hasRunSeed && ShouldConvert(runSeed, roomId, sourceId, pickupIndex);
            bool forced = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!convert && DevelopmentForceNextConversion)
            {
                DevelopmentForceNextConversion = false;
                convert = forced = true;
            }
#endif
            if (!convert) return false;
            // A forced conversion is a development grant, so it does not count toward the saved gain.
            pickup.ConvertToPermanentElif(forced);
            // "Chest Drop elif - ..." names the gold drop table key; the converted pickup names the new drop ID.
            spawned.name = spawned.name.Replace("Drop elif ", $"Drop {DropId} ");
            return true;
        }

        private static uint Hash(int seed, uint salt, string text) => Hash(unchecked((uint)seed * 0x9E3779B1u ^ salt), text);

        // FNV-1a over the text, continued from the given state.
        private static uint Hash(uint state, string text)
        {
            uint hash = unchecked(state ^ 2166136261u);
            foreach (char character in text ?? string.Empty)
                hash = unchecked((hash ^ character) * 16777619u);
            return Mix(hash);
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value += 0x9E3779B9u;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                return value ^ (value >> 16);
            }
        }
    }
}
