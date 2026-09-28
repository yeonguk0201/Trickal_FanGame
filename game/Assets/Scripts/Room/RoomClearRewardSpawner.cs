using System;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Rolls the room's clear drop once per Run. The roll comes from the room content seed, and the room state records
    // that it happened, so a revisit or floor reload never rolls or drops again.
    [DisallowMultipleComponent]
    public sealed class RoomClearRewardSpawner : MonoBehaviour
    {
        public const uint DropSeedSalt = 0x5BD1E995u;

        private ResourceDropTable dropTable;
        private Transform rewardPoint;
        private Transform rewardParent;
        private RoomRunState runState;
        private RunProgress runProgress;
        private int dropSeed;

        public bool HasRolled => runState != null && runState.HasGrantedClearReward;
        public GameObject LastSpawnedReward { get; private set; }
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public event Action<GameObject> RewardSpawned;

        public static int DeriveDropSeed(int roomContentSeed)
        {
            return FloorGenerator.DeriveSeed(roomContentSeed, 0, DropSeedSalt);
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
            LastSpawnedReward = null;
        }

        // Returns true only when this call spawned a drop; a roll that drops nothing still uses up the room's roll.
        public bool TrySpawn()
        {
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
    }
}
