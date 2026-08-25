using UnityEngine;

namespace TrickalFanGame.Item
{
    public sealed class ItemDropSource : MonoBehaviour
    {
        [SerializeField] private ItemPickup pickupPrefab;
        [SerializeField] private ItemDefinition[] itemPool = System.Array.Empty<ItemDefinition>();
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Transform dropParent;

        public bool HasDropped { get; private set; }

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

        public bool TryDrop()
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

            ItemDefinition definition = ChooseDefinition();
            if (definition == null)
            {
                Debug.LogError($"[ItemDropSource] {name} has no valid item definition.", this);
                return false;
            }

            Vector3 position = dropPoint != null ? dropPoint.position : transform.position;
            ItemPickup pickup = Instantiate(pickupPrefab, position, Quaternion.identity, dropParent);
            pickup.name = $"Reward - {definition.DisplayName}";
            pickup.Configure(definition);
            HasDropped = true;

            Debug.Log($"[ItemDropSource] Dropped {definition.DisplayName} from {name}.", this);
            return true;
        }

        private ItemDefinition ChooseDefinition()
        {
            int startIndex = Random.Range(0, itemPool.Length);
            for (int offset = 0; offset < itemPool.Length; offset++)
            {
                ItemDefinition candidate = itemPool[(startIndex + offset) % itemPool.Length];
                if (candidate != null && candidate.IsValid)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
