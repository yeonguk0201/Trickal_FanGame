using System;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Rolls the room's clear reward once per Run. The roll comes from the room content seed, and the room state records
    // that it happened, so a revisit or floor reload never rolls or drops again. Chest-1: with a chest content table
    // the reward is a chest instead of the old single-pickup drop (never both); a rebuilt room restores its chest.
    [DisallowMultipleComponent]
    public sealed class RoomClearRewardSpawner : MonoBehaviour
    {
        public const uint DropSeedSalt = 0x5BD1E995u;
        public const uint ChestSeedSalt = 0x1B873593u;
        public const string ClearChestId = "chest-clear";

        private ResourceDropTable dropTable;
        private Transform rewardPoint;
        private Transform rewardParent;
        private RoomRunState runState;
        private RunProgress runProgress;
        private int dropSeed;
        private ChestContentTable chestTable;
        private RoomChestSite chestSite;
        private int chestSeed;

        public bool HasRolled => runState != null && runState.HasGrantedClearReward;
        public GameObject LastSpawnedReward { get; private set; }
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public ChestContentTable ChestTable => chestTable;
        public int ChestSeed => chestSeed;
        public TreasureChest LastSpawnedChest { get; private set; }
        public event Action<GameObject> RewardSpawned;

        public static int DeriveDropSeed(int roomContentSeed)
        {
            return FloorGenerator.DeriveSeed(roomContentSeed, 0, DropSeedSalt);
        }

        public static int DeriveChestSeed(int roomContentSeed)
        {
            return FloorGenerator.DeriveSeed(roomContentSeed, 0, ChestSeedSalt);
        }

        public void Configure(ResourceDropTable configuredTable, Transform configuredPoint,
            Transform configuredParent, RoomRunState configuredState, int configuredDropSeed,
            RunProgress configuredProgress = null)
        {
            dropTable = configuredTable;
            rewardPoint = configuredPoint;
            rewardParent = configuredParent;
            runState = configuredState;
            dropSeed = configuredDropSeed;
            runProgress = configuredProgress;
            chestTable = null;
            chestSite = null;
            chestSeed = 0;
            LastSpawnedReward = null;
            LastSpawnedChest = null;
        }

        // Chest mode. A chest already rolled for this room in the Run is rebuilt as it was (opened or closed); a
        // chest that left with its floor stays gone.
        public void ConfigureChest(ChestContentTable configuredTable, RoomChestSite configuredSite,
            int configuredChestSeed)
        {
            dropTable = null;
            dropSeed = 0;
            chestTable = configuredTable;
            chestSite = configuredSite;
            chestSeed = configuredChestSeed;
            runState = configuredSite?.State;
            runProgress = configuredSite?.Progress;
            rewardParent = configuredSite?.Parent;
            rewardPoint = configuredSite?.Room.Controller != null ? configuredSite.Room.Controller.transform : null;
            LastSpawnedReward = null;
            LastSpawnedChest = null;

            ChestRunState existing = runState?.GetChest(ClearChestId);
            if (chestTable == null || existing == null || existing.IsDiscarded) return;
            TreasureChest restored = RoomChestSpawner.Spawn(chestTable, chestSite, ClearChestId, existing.Kind,
                chestSeed, out string error);
            if (restored == null) Debug.LogError($"Room {runState.RoomId} could not rebuild its chest. {error}", this);
        }

        // Returns true only when this call spawned a drop; a roll that drops nothing still uses up the room's roll.
        public bool TrySpawn()
        {
            if (chestTable != null) return TrySpawnChest();
            if (dropTable == null || rewardPoint == null || runState == null ||
                !runState.TryMarkClearRewardGranted())
            {
                return false;
            }

            if (!dropTable.TryRoll(dropSeed, out ResourceDropEntry entry) || entry.Prefab == null)
            {
                return false;
            }

            GameObject reward = Instantiate(entry.Prefab, rewardPoint.position, Quaternion.identity, rewardParent);
            reward.name = $"Clear Drop {entry.DropId} - {runState.RoomId}";
            if (runProgress != null && reward.TryGetComponent(out RunResourcePickup resourcePickup))
            {
                resourcePickup.BindRunProgress(runProgress);
            }

            LastSpawnedReward = reward;
            RewardSpawned?.Invoke(reward);
            return true;
        }

        private bool TrySpawnChest()
        {
            if (chestSite == null || runState == null || !runState.TryMarkClearRewardGranted() ||
                !chestTable.TryRollChest(chestSeed, out ChestKind kind))
            {
                return false;
            }

            TreasureChest chest = RoomChestSpawner.Spawn(chestTable, chestSite, ClearChestId, kind, chestSeed,
                out string error);
            if (chest == null)
            {
                Debug.LogError($"Room {runState.RoomId} could not place its {kind} chest. {error}", this);
                return false;
            }

            LastSpawnedChest = chest;
            LastSpawnedReward = chest.gameObject;
            RewardSpawned?.Invoke(chest.gameObject);
            return true;
        }
    }
}
