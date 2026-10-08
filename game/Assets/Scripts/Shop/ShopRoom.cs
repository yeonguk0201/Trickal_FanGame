using System;
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

    // What the shop UI shows for one grid cell. Empty is a cell with no offer.
    public enum ShopOfferStatus
    {
        Empty,
        Available,
        Free,
        NotEnoughGold,
        ItemUnavailable,
        SoldOut,
    }

    // Special-4 / Shop-0: the shop room's stock and purchase rules. The player opens the shop UI at the shopkeeper; a
    // purchase checks the offer, spends its gold, marks the slot sold, and only then grants it: an Item goes straight
    // to the inventory, a consumable drops as a floor pickup in front of the shopkeeper.
    [DisallowMultipleComponent]
    public sealed class ShopRoom : MonoBehaviour
    {
        public const float DropOffsetY = -1.6f;
        public const float DropSpacing = 1.2f;

        [SerializeField] private ShopKeeper keeper;

        private RunProgress runProgress;
        private ShopStockState stock;
        private Transform dropParent;
        private ShopSession session;

        public ShopKeeper Keeper => keeper;
        public RunProgress Progress => runProgress;
        public ShopStockState Stock => stock;
        public ShopKind Kind => stock != null ? stock.Kind : ShopKind.General;
        public ShopSession Session => session;
        public GameObject LastDroppedPickup { get; private set; }
        public event Action<ShopOffer> Purchased;

        public void ConfigureKeeper(ShopKeeper configuredKeeper)
        {
            keeper = configuredKeeper;
        }

        public void Configure(RunProgress configuredProgress, ShopStockState configuredStock,
            Transform configuredDropParent, ShopSession configuredSession = null)
        {
            runProgress = configuredProgress;
            stock = configuredStock;
            dropParent = configuredDropParent;
            session = configuredSession;
            if (keeper != null) keeper.Bind(this, session);
        }

        private void OnDestroy()
        {
            // A floor rebuild destroys the room while its UI could still be open.
            if (session != null && session.Shop == this) session.Close();
        }

        public int GetPrice(ShopOffer offer) =>
            offer == null ? 0 : stock != null ? stock.GetPrice(offer.SlotIndex) : offer.Price;

        public bool CanAfford(ShopOffer offer) =>
            offer != null && runProgress != null &&
            runProgress.GetResourceCount(RunResourceType.Gold) >= GetPrice(offer);

        public bool IsSold(ShopOffer offer) => offer == null || stock == null || stock.IsPurchased(offer.SlotIndex);

        public ShopOfferStatus GetStatus(int slotIndex, PlayerInventory inventory)
        {
            if (stock == null || slotIndex < 0 || slotIndex >= stock.Offers.Count) return ShopOfferStatus.Empty;
            ShopOffer offer = stock.Offers[slotIndex];
            if (stock.IsPurchased(slotIndex)) return ShopOfferStatus.SoldOut;
            if (offer.Kind == ShopOfferKind.Item &&
                (inventory == null || !ArtifactRewardSelector.IsEligible(offer.Item, inventory)))
                return ShopOfferStatus.ItemUnavailable;
            if (stock.GetPrice(slotIndex) == 0) return ShopOfferStatus.Free;
            return CanAfford(offer) ? ShopOfferStatus.Available : ShopOfferStatus.NotEnoughGold;
        }

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
            // A free offer (멤버십카드) spends nothing.
            int price = stock.GetPrice(slotIndex);
            if (price > 0 && !runProgress.TrySpendResource(RunResourceType.Gold, price))
                return ShopPurchaseResult.NotEnoughGold;

            if (offer.Kind == ShopOfferKind.Item && !inventory.TryAcquire(offer.Item))
            {
                if (price > 0) runProgress.TryAddResource(RunResourceType.Gold, price);
                return ShopPurchaseResult.ItemUnavailable;
            }

            stock.TryMarkPurchased(slotIndex);
            if (offer.Kind == ShopOfferKind.Consumable) LastDroppedPickup = DropPickup(offer, slotIndex);
            Purchased?.Invoke(offer);
            return ShopPurchaseResult.Purchased;
        }

        // Each grid column drops at its own spot, so several bought consumables do not stack on one point.
        public Vector3 GetDropPosition(int slotIndex)
        {
            Vector3 origin = keeper != null ? keeper.transform.position : transform.position;
            int column = Mathf.Max(0, slotIndex) % ShopCatalog.GridColumns;
            return origin + new Vector3((column - (ShopCatalog.GridColumns - 1) * 0.5f) * DropSpacing, DropOffsetY, 0f);
        }

        private GameObject DropPickup(ShopOffer offer, int slotIndex)
        {
            GameObject pickup = Instantiate(offer.Consumable.PickupPrefab, GetDropPosition(slotIndex),
                Quaternion.identity, dropParent != null ? dropParent : transform);
            pickup.name = $"Shop Drop {offer.Consumable.ConsumableId} - {stock.ShopId}";
            if (pickup.TryGetComponent(out RunResourcePickup resourcePickup)) resourcePickup.BindRunProgress(runProgress);
            return pickup;
        }
    }
}
