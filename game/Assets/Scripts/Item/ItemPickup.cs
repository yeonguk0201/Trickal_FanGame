using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class ItemPickup : MonoBehaviour, IDescribedPickup
    {
        private void OnEnable()
        {
            TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);
            DescribedPickupRegistry.Register(this);
        }

        private void OnDisable() => DescribedPickupRegistry.Unregister(this);

        [SerializeField] private ItemDefinition definition;

        public ItemDefinition Definition => definition;
        public Vector3 Position => transform.position;
        public bool IsAvailable => definition != null;

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
