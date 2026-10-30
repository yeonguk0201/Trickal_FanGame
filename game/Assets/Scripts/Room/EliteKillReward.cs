using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Variant-0: killing a room's elite always drops one pickup where it fell, picked from its own table with the
    // room content seed. The room state records it, so an escaped fight that brings the elite back, a revisit or a
    // floor reload never drops another. The candidates and weights are a placeholder until Drop-0 tunes drops.
    public sealed class EliteKillReward
    {
        public const uint SeedSalt = 0x3E1F7EC5u;
        public const string SourceId = "elite-kill";

        private readonly ResourceDropTable table;
        private readonly Transform parent;
        private readonly RoomRunState state;
        private readonly RunProgress progress;
        private readonly int seed;

        public EliteKillReward(ResourceDropTable configuredTable, Transform configuredParent,
            RoomRunState configuredState, int configuredSeed, RunProgress configuredProgress = null)
        {
            table = configuredTable;
            parent = configuredParent;
            state = configuredState;
            seed = configuredSeed;
            progress = configuredProgress;
        }

        public ResourceDropTable Table => table;
        public int Seed => seed;
        public GameObject LastSpawnedReward { get; private set; }

        public static int DeriveSeed(int roomContentSeed) =>
            FloorGenerator.DeriveSeed(roomContentSeed, 0, SeedSalt);

        public bool TrySpawn(Vector3 position)
        {
            if (table == null || state == null || !state.TryMarkEliteRewardGranted()) return false;
            if (!table.TryPick(seed, null, out ResourceDropEntry entry) || entry.Prefab == null) return false;

            GameObject reward = Object.Instantiate(entry.Prefab, position, Quaternion.identity, parent);
            reward.name = $"Elite Drop {entry.DropId} - {state.RoomId}";
            if (progress != null && reward.TryGetComponent(out RunResourcePickup pickup))
            {
                pickup.BindRunProgress(progress);
                PermanentElif.TryConvertSpawnedGold(reward, progress.RunSeed, progress.HasRunSeed, state.RoomId,
                    SourceId, 0);
            }

            LastSpawnedReward = reward;
            return true;
        }
    }
}
