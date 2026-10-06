using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Room;

namespace TrickalFanGame.Shop
{
    public enum ShopOfferKind
    {
        Item,
        Consumable,
    }

    public sealed class ShopOffer
    {
        private ShopOffer(int slotIndex, ShopOfferKind kind, ItemDefinition item, ShopConsumable consumable, int price)
        {
            SlotIndex = slotIndex;
            Kind = kind;
            Item = item;
            Consumable = consumable;
            Price = price;
        }

        public int SlotIndex { get; }
        public ShopOfferKind Kind { get; }
        public ItemDefinition Item { get; }
        public ShopConsumable Consumable { get; }
        public int Price { get; }
        public string OfferId => Kind == ShopOfferKind.Item ? $"item:{Item.ItemId}" : $"consumable:{Consumable.ConsumableId}";
        public string DisplayName => Kind == ShopOfferKind.Item ? Item.DisplayName : Consumable.DisplayName;

        public static ShopOffer ForItem(int slotIndex, ItemDefinition item, int price) =>
            new(slotIndex, ShopOfferKind.Item, item, null, price);

        public static ShopOffer ForConsumable(int slotIndex, ShopConsumable consumable) =>
            new(slotIndex, ShopOfferKind.Consumable, null, consumable, consumable.Price);
    }

    // One shop's rolled offers and which slots are sold. RunProgress keeps it for the whole Run, so revisits and floor
    // rebuilds show the same offers and never sell a slot twice.
    public sealed class ShopStockState
    {
        private readonly ShopOffer[] offers;
        private readonly bool[] purchased;

        public ShopStockState(string shopId, ShopOffer[] configuredOffers)
        {
            ShopId = shopId;
            offers = configuredOffers ?? Array.Empty<ShopOffer>();
            purchased = new bool[offers.Length];
        }

        public string ShopId { get; }
        public IReadOnlyList<ShopOffer> Offers => offers;
        public event Action<int> Purchased;

        public bool IsPurchased(int slotIndex) => slotIndex >= 0 && slotIndex < purchased.Length && purchased[slotIndex];

        public bool TryMarkPurchased(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= purchased.Length || purchased[slotIndex]) return false;
            purchased[slotIndex] = true;
            Purchased?.Invoke(slotIndex);
            return true;
        }
    }

    public static class ShopStockBuilder
    {
        public const uint StockSalt = 0x53544F4Bu;

        public static string BuildShopId(string roomId) => $"{roomId}:shop";

        // Items use the treasure-room rarity weights (distinct, skipping maxed stacks at roll time); consumables are
        // distinct picks from the catalog. If too few Items are eligible, extra consumables fill the empty slots.
        public static ShopStockState Build(string roomId, int roomContentSeed, ShopCatalog catalog,
            IReadOnlyList<ItemDefinition> itemPool, PlayerInventory inventory)
        {
            string shopId = BuildShopId(roomId);
            int stockSeed = FloorGenerator.DeriveSeed(roomContentSeed, 0, StockSalt);
            List<ShopOffer> offers = new(ShopCatalog.OfferCount);
            foreach (ItemRewardCandidate candidate in ArtifactRewardSelector.BuildCandidates(
                         itemPool, inventory, stockSeed, shopId))
            {
                if (offers.Count >= ShopCatalog.ItemOfferCount) break;
                if (candidate.IsItem)
                    offers.Add(ShopOffer.ForItem(offers.Count, candidate.Definition,
                        catalog.GetItemPrice(candidate.Definition.Rarity)));
            }

            List<ShopConsumable> consumables = new(catalog.Consumables);
            consumables.Sort((left, right) => string.CompareOrdinal(left.ConsumableId, right.ConsumableId));
            uint state = unchecked((uint)FloorGenerator.DeriveSeed(stockSeed, 1, StockSalt));
            while (offers.Count < ShopCatalog.OfferCount && consumables.Count > 0)
            {
                state = state == 0u ? 0x6D2B79F5u : state;
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                int index = (int)(state % (uint)consumables.Count);
                offers.Add(ShopOffer.ForConsumable(offers.Count, consumables[index]));
                consumables.RemoveAt(index);
            }

            return new ShopStockState(shopId, offers.ToArray());
        }
    }
}
