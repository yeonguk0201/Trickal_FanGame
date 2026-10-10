using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class ItemPickup : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        [SerializeField] private ItemDefinition definition;

        public ItemDefinition Definition => definition;

        public void Configure(ItemDefinition configuredDefinition)
        {
            definition = configuredDefinition;
            ApplyArtwork();
        }

        private void Awake() => ApplyArtwork();

        private void ApplyArtwork() => TrickalFanGame.Frontend.UserArtwork.Apply(
            GetComponentInChildren<SpriteRenderer>(), definition);

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
