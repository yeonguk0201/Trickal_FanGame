using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    public sealed class ItemDropSource : MonoBehaviour
    {
        public const float FallbackHealMaxHealthRatio = 0.25f;

        [SerializeField] private ItemPickup pickupPrefab;
        [SerializeField] private ItemDefinition[] itemPool = System.Array.Empty<ItemDefinition>();
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Transform dropParent;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private string rewardId;

        public bool HasDropped { get; private set; }
        public ItemDefinition LastDroppedDefinition { get; private set; }
        public float LastFallbackHealAmount { get; private set; }
        public ItemPickup PickupPrefab => pickupPrefab;
        public IReadOnlyList<ItemDefinition> ItemPool => itemPool;
        public Transform DropParent => dropParent;

        public void Configure(
            ItemPickup configuredPickupPrefab,
            ItemDefinition[] configuredItemPool,
            Transform configuredDropPoint = null,
            Transform configuredDropParent = null)
        {
            pickupPrefab = configuredPickupPrefab;
            itemPool = configuredItemPool ?? System.Array.Empty<ItemDefinition>();
            dropPoint = configuredDropPoint;
            dropParent = configuredDropParent;
        }

        public void ConfigureRewardContext(RunProgress configuredRunProgress, string configuredRewardId)
        {
            runProgress = configuredRunProgress;
            rewardId = configuredRewardId;
        }

        public bool TryDrop()
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            return TryDrop(inventory);
        }

        public bool TryDrop(PlayerInventory inventory)
        {
            if (HasDropped)
            {
                return false;
            }

            if (pickupPrefab == null || itemPool == null || itemPool.Length == 0)
            {
                Debug.LogError($"[ItemDropSource] {name} has no pickup prefab or item pool.", this);
                return false;
            }

            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }

            int runSeed = runProgress != null && runProgress.HasRunSeed ? runProgress.RunSeed : 0;
            string stableRewardId = string.IsNullOrWhiteSpace(rewardId) ? name : rewardId;
            if (!ArtifactRewardSelector.TryChoose(
                    itemPool,
                    inventory,
                    runSeed,
                    stableRewardId,
                    out ItemDefinition definition))
            {
                if (ArtifactRewardSelector.AreAllActiveArtifactsAtMaximum(itemPool, inventory))
                {
                    return TryGrantFallbackHeal(inventory);
                }

                Debug.LogError($"[ItemDropSource] {name} has no eligible active artifact configuration.", this);
                return false;
            }

            Vector3 position = dropPoint != null ? dropPoint.position : transform.position;
            ItemPickup pickup = Instantiate(pickupPrefab, position, Quaternion.identity, dropParent);
            pickup.name = $"Reward - {definition.DisplayName}";
            pickup.Configure(definition);
            HasDropped = true;
            LastDroppedDefinition = definition;
            LastFallbackHealAmount = 0f;

            Debug.Log($"[ItemDropSource] Dropped {definition.DisplayName} from {name}.", this);
            return true;
        }

        private bool TryGrantFallbackHeal(PlayerInventory inventory)
        {
            Health health = inventory != null ? inventory.GetComponent<Health>() : null;
            if (health == null || health.IsDead)
            {
                Debug.Log(
                    $"[ItemDropSource] {name} has no eligible artifact and cannot heal a missing or dead player.",
                    this);
                return false;
            }

            LastFallbackHealAmount = health.Heal(health.MaxHealth * FallbackHealMaxHealthRatio);
            LastDroppedDefinition = null;
            HasDropped = true;
            Debug.Log(
                $"[ItemDropSource] All active artifacts are at maximum stacks; granted " +
                $"{LastFallbackHealAmount:0.##} fallback healing from {name}.",
                this);
            return true;
        }
    }
}
