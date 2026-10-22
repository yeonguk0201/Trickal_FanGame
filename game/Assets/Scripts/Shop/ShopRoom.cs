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
        // Elif-3: an elif offer without enough elif, or a Run that cannot spend elif (contract 27 §4.3).
        NotEnoughElif,
        ElifLocked,
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
        NotEnoughElif,
        ElifLocked,
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
            if (runProgress != null) runProgress.ShopUpgradesChanged -= ApplyUpgrades;
            runProgress = configuredProgress;
            stock = configuredStock;
            dropParent = configuredDropParent;
            session = configuredSession;
            if (runProgress != null) runProgress.ShopUpgradesChanged += ApplyUpgrades;
            ApplyUpgrades();
            if (keeper != null) keeper.Bind(this, session);
        }

        private void OnDestroy()
        {
            if (runProgress != null) runProgress.ShopUpgradesChanged -= ApplyUpgrades;
            // A floor rebuild destroys the room while its UI could still be open.
            if (session != null && session.Shop == this) session.Close();
        }

        // Elif-3: 상품 칸 추가 opens its last-row slots in this shop at once, even while the screen is open.
        private void ApplyUpgrades()
        {
            if (stock != null && runProgress != null)
                stock.EnsureExtraSlots(runProgress.GetShopUpgradeLevel(ShopUpgrades.ExtraSlotId));
        }

        public int DiscountLevel => runProgress != null ? runProgress.GetShopUpgradeLevel(ShopUpgrades.DiscountId) : 0;

        // What a purchase pays now: nothing when free (멤버십카드), the elif price for an elif offer, otherwise the
        // gold price after the 상점 할인 upgrade.
        public int GetPrice(ShopOffer offer)
        {
            if (offer == null || (stock != null && stock.IsFree)) return 0;
            return offer.IsElifPriced ? offer.ElifPrice : ShopUpgrades.ApplyDiscount(offer.Price, DiscountLevel);
        }

        public static RunResourceType CurrencyOf(ShopOffer offer) =>
            offer != null && offer.IsElifPriced ? RunResourceType.Elif : RunResourceType.Gold;

        public bool CanAfford(ShopOffer offer) =>
            offer != null && runProgress != null &&
            runProgress.GetResourceCount(CurrencyOf(offer)) >= GetPrice(offer);

        public bool IsSold(ShopOffer offer) => offer == null || stock == null || stock.IsPurchased(offer.SlotIndex);

        public ShopOfferStatus GetStatus(int slotIndex, PlayerInventory inventory)
        {
            ShopOffer offer = stock != null ? stock.GetOffer(slotIndex) : null;
            if (offer == null) return ShopOfferStatus.Empty;
            if (stock.IsPurchased(slotIndex)) return ShopOfferStatus.SoldOut;
            if (offer.Kind == ShopOfferKind.Item && !CanReceive(offer.Item, inventory))
                return ShopOfferStatus.ItemUnavailable;
            if (GetPrice(offer) == 0) return ShopOfferStatus.Free;
            if (offer.IsElifPriced && runProgress != null && !runProgress.IsElifSpendingAllowed)
                return ShopOfferStatus.ElifLocked;
            if (CanAfford(offer)) return ShopOfferStatus.Available;
            return offer.IsElifPriced ? ShopOfferStatus.NotEnoughElif : ShopOfferStatus.NotEnoughGold;
        }

        public ShopPurchaseResult TryPurchase(int slotIndex, PlayerInventory inventory)
        {
            LastDroppedPickup = null;
            ShopOffer offer = stock != null ? stock.GetOffer(slotIndex) : null;
            if (offer == null || runProgress == null || runProgress.IsProgressionStopped ||
                runProgress.IsRewardSelectionPending)
                return ShopPurchaseResult.Unavailable;

            if (stock.IsPurchased(slotIndex)) return ShopPurchaseResult.SoldOut;
            if (offer.Kind == ShopOfferKind.Item && !CanReceive(offer.Item, inventory))
                return ShopPurchaseResult.ItemUnavailable;
            // A free offer (멤버십카드) spends nothing.
            int price = GetPrice(offer);
            RunResourceType currency = CurrencyOf(offer);
            if (price > 0 && currency == RunResourceType.Elif && !runProgress.IsElifSpendingAllowed)
                return ShopPurchaseResult.ElifLocked;
            if (price > 0 && !runProgress.TrySpendResource(currency, price))
                return currency == RunResourceType.Elif
                    ? ShopPurchaseResult.NotEnoughElif
                    : ShopPurchaseResult.NotEnoughGold;

            if (offer.Kind == ShopOfferKind.Item && !Grant(offer, inventory))
            {
                if (price > 0 && currency == RunResourceType.Elif) runProgress.RefundElif(price);
                else if (price > 0) runProgress.TryAddResource(RunResourceType.Gold, price);
                return ShopPurchaseResult.ItemUnavailable;
            }

            stock.TryMarkPurchased(slotIndex);
            if (offer.Kind == ShopOfferKind.Consumable) LastDroppedPickup = DropPickup(offer, slotIndex);
            Purchased?.Invoke(offer);
            return ShopPurchaseResult.Purchased;
        }

        // Elif-3: only the general shop (시스트) sells upgrades; both shops apply them.
        public bool SellsUpgrades => stock != null && stock.Kind == ShopKind.General;

        public ShopUpgradeStatus GetUpgradeStatus(string upgradeId)
        {
            if (!SellsUpgrades || runProgress == null || runProgress.IsRewardSelectionPending)
                return ShopUpgradeStatus.Unavailable;
            return runProgress.GetShopUpgradeStatus(upgradeId);
        }

        public ShopUpgradeStatus TryPurchaseUpgrade(string upgradeId)
        {
            ShopUpgradeStatus status = GetUpgradeStatus(upgradeId);
            return status == ShopUpgradeStatus.Available ? runProgress.TryPurchaseShopUpgrade(upgradeId) : status;
        }

        // Power-2: a 교주의 권능 goes to the 권능 slot (never the one already held); the held one drops at the offer's
        // spot in front of the shopkeeper. Every other Item goes to the inventory.
        public static bool CanReceive(ItemDefinition item, PlayerInventory inventory)
        {
            if (inventory == null || item == null) return false;
            if (!item.IsActivePower) return ArtifactRewardSelector.IsEligible(item, inventory);
            PlayerPowerSlot slot = inventory.GetComponent<PlayerPowerSlot>();
            return slot != null && slot.HeldDefinition != item && slot.CanGrant(item);
        }

        private bool Grant(ShopOffer offer, PlayerInventory inventory)
        {
            if (!offer.Item.IsActivePower) return inventory.TryAcquire(offer.Item);
            return inventory.GetComponent<PlayerPowerSlot>().TryGrant(offer.Item, GetDropPosition(offer.SlotIndex),
                dropParent != null ? dropParent : transform);
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
