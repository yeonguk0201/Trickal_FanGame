using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Drop-0: killing a champion may drop one pickup where it fell. The chance rises with depth (the table's own drop
    // chance is not used); the roll and the pick come from the room content seed and the champion's wave slot. The room
    // state records each slot, so an escaped fight that brings the champion back or a revisit never rolls again.
    public sealed class ChampionKillReward
    {
        public const uint SeedSalt = 0x2B7E1516u;
        public const string SourceId = "champion-kill";

        private readonly ResourceDropTable table;
        private readonly Transform parent;
        private readonly RoomRunState state;
        private readonly RunProgress progress;
        private readonly int floorNumber;
        private readonly int roomSeed;

        public ChampionKillReward(ResourceDropTable configuredTable, Transform configuredParent,
            RoomRunState configuredState, int configuredFloorNumber, int configuredRoomSeed,
            RunProgress configuredProgress = null)
        {
            table = configuredTable;
            parent = configuredParent;
            state = configuredState;
            floorNumber = configuredFloorNumber;
            roomSeed = configuredRoomSeed;
            progress = configuredProgress;
        }

        public ResourceDropTable Table => table;
        public int FloorNumber => floorNumber;
        public GameObject LastSpawnedReward { get; private set; }
        public int RolledCount => state != null ? state.RewardedChampionSlots.Count : 0;

        public static int DropPercent(int floorNumber) => floorNumber switch
        {
            <= 1 => 0,
            2 => 20,
            3 => 25,
            <= 6 => 30,
            _ => 35,
        };

        public static int DeriveRoomSeed(int roomContentSeed) =>
            FloorGenerator.DeriveSeed(roomContentSeed, 0, SeedSalt);

        public static int DeriveSlotSeed(int roomSeed, int slot) => FloorGenerator.DeriveSeed(roomSeed, slot, SeedSalt);

        public static bool Rolls(int floorNumber, int roomSeed, int slot) =>
            unchecked((uint)DeriveSlotSeed(roomSeed, slot)) % 100u < (uint)DropPercent(floorNumber);

        // Returns true only when this call dropped a pickup; a missed roll still uses up the slot.
        public bool TrySpawn(int slot, Vector3 position)
        {
            LastSpawnedReward = null;
            if (table == null || state == null || !state.TryMarkChampionRewardRolled(slot)) return false;
            if (!Rolls(floorNumber, roomSeed, slot)) return false;
            if (!table.TryPick(DeriveSlotSeed(roomSeed, slot) ^ (int)SeedSalt, null, out ResourceDropEntry entry) ||
                entry.Prefab == null)
                return false;

            GameObject reward = Object.Instantiate(entry.Prefab, position, Quaternion.identity, parent);
            reward.name = $"Champion Drop {entry.DropId} - {state.RoomId} {slot}";
            if (progress != null && reward.TryGetComponent(out RunResourcePickup pickup))
            {
                pickup.BindRunProgress(progress);
                PermanentElif.TryConvertSpawnedGold(reward, progress.RunSeed, progress.HasRunSeed, state.RoomId,
                    SourceId, slot);
            }

            LastSpawnedReward = reward;
            return true;
        }
    }
}
