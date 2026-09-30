using System;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [DisallowMultipleComponent]
    public sealed class RoomClearRewardSpawner : MonoBehaviour
    {
        private SPPickup pickupPrefab;
        private Transform rewardPoint;
        private Transform rewardParent;
        private RoomRunState runState;

        public bool HasSpawned => runState != null && runState.HasGrantedClearReward;
        public SPPickup LastSpawnedReward { get; private set; }
        public SPPickup PickupPrefab => pickupPrefab;
        public event Action<SPPickup> RewardSpawned;

        public void Configure(SPPickup configuredPrefab, Transform configuredPoint,
            Transform configuredParent, RoomRunState configuredState)
        {
            pickupPrefab = configuredPrefab;
            rewardPoint = configuredPoint;
            rewardParent = configuredParent;
            runState = configuredState;
            LastSpawnedReward = null;
        }

        public bool TrySpawn()
        {
            if (pickupPrefab == null || rewardPoint == null || runState == null ||
                runState.HasGrantedClearReward)
            {
                return false;
            }

            SPPickup reward = Instantiate(
                pickupPrefab, rewardPoint.position, Quaternion.identity, rewardParent);
            reward.name = $"Clear Reward - {runState.RoomId}";
            if (!runState.TryMarkClearRewardGranted())
            {
                Destroy(reward.gameObject);
                return false;
            }

            LastSpawnedReward = reward;
            RewardSpawned?.Invoke(reward);
            return true;
        }
    }
}
