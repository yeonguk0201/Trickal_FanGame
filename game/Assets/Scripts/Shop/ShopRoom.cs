using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    public enum ShopPurchaseResult
    {
        Purchased,
        SoldOut,
        NotEnoughGold,
        ItemUnavailable,
        Unavailable,
    }

    // Special-4: the shop room's stalls. A purchase checks the offer, spends its gold, marks the slot sold, and only then
    // grants it: an Item goes straight to the inventory, a consumable drops as a floor pickup in front of its stall.
    [DisallowMultipleComponent]
    public sealed class ShopRoom : MonoBehaviour
    {
        public const float DropOffsetY = -1.3f;

        [SerializeField] private ShopStall[] stalls = Array.Empty<ShopStall>();

        private RunProgress runProgress;
        private ShopStockState stock;
        private Transform dropParent;

        public IReadOnlyList<ShopStall> Stalls => stalls;
        public RunProgress Progress => runProgress;
        public ShopStockState Stock => stock;
        public GameObject LastDroppedPickup { get; private set; }
        public event Action<ShopOffer> Purchased;

        public void ConfigureStalls(ShopStall[] configuredStalls)
        {
            stalls = configuredStalls ?? Array.Empty<ShopStall>();
        }

        public void Configure(RunProgress configuredProgress, ShopStockState configuredStock, Transform configuredDropParent)
        {
            runProgress = configuredProgress;
            stock = configuredStock;
            dropParent = configuredDropParent;
            for (int index = 0; index < stalls.Length; index++)
            {
                ShopOffer offer = stock != null && index < stock.Offers.Count ? stock.Offers[index] : null;
                stalls[index].Bind(this, offer);
            }
        }

        public bool CanAfford(ShopOffer offer) =>
            offer != null && runProgress != null && runProgress.GetResourceCount(RunResourceType.Gold) >= offer.Price;

        public bool IsSold(ShopOffer offer) => offer == null || stock == null || stock.IsPurchased(offer.SlotIndex);

        public ShopPurchaseResult TryPurchase(int slotIndex, PlayerInventory inventory)
        {
            LastDroppedPickup = null;
            if (stock == null || slotIndex < 0 || slotIndex >= stock.Offers.Count || runProgress == null ||
                runProgress.IsProgressionStopped || runProgress.IsRewardSelectionPending)
                return ShopPurchaseResult.Unavailable;

            ShopOffer offer = stock.Offers[slotIndex];
            if (stock.IsPurchased(slotIndex)) return ShopPurchaseResult.SoldOut;
            if (offer.Kind == ShopOfferKind.Item &&
                (inventory == null || !ArtifactRewardSelector.IsEligible(offer.Item, inventory)))
                return ShopPurchaseResult.ItemUnavailable;
            if (!runProgress.TrySpendResource(RunResourceType.Gold, offer.Price)) return ShopPurchaseResult.NotEnoughGold;

            if (offer.Kind == ShopOfferKind.Item && !inventory.TryAcquire(offer.Item))
            {
                runProgress.TryAddResource(RunResourceType.Gold, offer.Price);
                return ShopPurchaseResult.ItemUnavailable;
            }

            stock.TryMarkPurchased(slotIndex);
            if (offer.Kind == ShopOfferKind.Consumable) LastDroppedPickup = DropPickup(offer, slotIndex);
            Purchased?.Invoke(offer);
            return ShopPurchaseResult.Purchased;
        }

        private GameObject DropPickup(ShopOffer offer, int slotIndex)
        {
            Vector3 origin = slotIndex < stalls.Length ? stalls[slotIndex].transform.position : transform.position;
            GameObject pickup = Instantiate(offer.Consumable.PickupPrefab, origin + Vector3.up * DropOffsetY,
                Quaternion.identity, dropParent != null ? dropParent : transform);
            pickup.name = $"Shop Drop {offer.Consumable.ConsumableId} - {stock.ShopId}";
            if (pickup.TryGetComponent(out RunResourcePickup resourcePickup)) resourcePickup.BindRunProgress(runProgress);
            return pickup;
        }
    }
}
