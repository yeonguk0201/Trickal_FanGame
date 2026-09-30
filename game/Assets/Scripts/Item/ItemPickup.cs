using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinition definition;

        public void Configure(ItemDefinition configuredDefinition)
        {
            definition = configuredDefinition;
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null || !inventory.TryAcquire(definition))
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
