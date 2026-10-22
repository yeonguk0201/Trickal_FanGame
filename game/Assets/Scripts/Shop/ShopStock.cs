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

    // Shop-1: who runs the shop. 골디 sells better Items from a secret room; rules and prices are the same.
    public enum ShopKind
    {
        General,
        Goldi,
    }

    public sealed class ShopOffer
    {
        private ShopOffer(int slotIndex, ShopOfferKind kind, ItemDefinition item, ShopConsumable consumable, int price,
            int elifPrice)
        {
            SlotIndex = slotIndex;
            Kind = kind;
            Item = item;
            Consumable = consumable;
            Price = price;
            ElifPrice = elifPrice;
        }

        public int SlotIndex { get; }
        public ShopOfferKind Kind { get; }
        public ItemDefinition Item { get; }
        public ShopConsumable Consumable { get; }
        // The listed gold price. An elif offer keeps it as the base of its elif price.
        public int Price { get; }
        // Elif-3: above zero, the offer is sold for this much permanent elif only (never gold, never discounted).
        public int ElifPrice { get; }
        public bool IsElifPriced => ElifPrice > 0;
        public string OfferId => Kind == ShopOfferKind.Item ? $"item:{Item.ItemId}" : $"consumable:{Consumable.ConsumableId}";
        public string DisplayName => Kind == ShopOfferKind.Item ? Item.DisplayName : Consumable.DisplayName;

        public static ShopOffer ForItem(int slotIndex, ItemDefinition item, int price) =>
            new(slotIndex, ShopOfferKind.Item, item, null, price, 0);

        public static ShopOffer ForConsumable(int slotIndex, ShopConsumable consumable) =>
            new(slotIndex, ShopOfferKind.Consumable, null, consumable, consumable.Price, 0);

        // The same Item at gold price ÷ 10 (rounded up) in elif.
        public ShopOffer WithElifPrice() => Kind == ShopOfferKind.Item
            ? new ShopOffer(SlotIndex, Kind, Item, null, Price, ShopUpgrades.ElifPriceOf(Price))
            : this;
    }

    // One shop's rolled offers and which slots are sold. RunProgress keeps it for the whole Run, so revisits and floor
    // rebuilds show the same offers and never sell a slot twice. Offers are listed in slot order; the base offers take
    // slots 0..5 and Elif-3's extra offers (상품 칸 추가) take the empty last grid row, slots 6..8.
    public sealed class ShopStockState
    {
        private readonly List<ShopOffer> offers;
        private readonly bool[] purchased = new bool[ShopCatalog.GridSlotCount];
        private readonly Func<int, ShopOffer> extraOfferFactory;

        public ShopStockState(string shopId, ShopOffer[] configuredOffers, ShopKind kind = ShopKind.General,
            Func<int, ShopOffer> configuredExtraOfferFactory = null)
        {
            ShopId = shopId;
            Kind = kind;
            offers = new List<ShopOffer>(configuredOffers ?? Array.Empty<ShopOffer>());
            extraOfferFactory = configuredExtraOfferFactory;
        }

        public string ShopId { get; }
        public ShopKind Kind { get; }
        public IReadOnlyList<ShopOffer> Offers => offers;
        public int ExtraSlotCount { get; private set; }
        // Spell-4 (멤버십카드): once set, every offer still on sale costs nothing, including an elif offer and extra
        // offers added afterwards. Kept with the stock for the Run.
        public bool IsFree { get; private set; }
        public event Action<int> Purchased;
        public event Action PricesChanged;
        public event Action OffersChanged;

        public static int FirstExtraSlot => ShopCatalog.OfferCount;
        public static int MaxExtraSlots => ShopCatalog.GridSlotCount - ShopCatalog.OfferCount;

        public ShopOffer GetOffer(int slotIndex)
        {
            foreach (ShopOffer offer in offers)
                if (offer.SlotIndex == slotIndex) return offer;
            return null;
        }

        // The listed price before discounts: 0 when free, otherwise the gold price (an elif offer's elif price is
        // ShopOffer.ElifPrice). ShopRoom.GetPrice is what a purchase pays.
        public int GetPrice(int slotIndex)
        {
            ShopOffer offer = GetOffer(slotIndex);
            return IsFree || offer == null ? 0 : offer.Price;
        }

        public bool TryMakeFree()
        {
            if (IsFree) return false;
            IsFree = true;
            PricesChanged?.Invoke();
            return true;
        }

        // Elif-3: 상품 칸 추가 fills empty last-row slots up to count. Each extra offer depends only on the shop and
        // its slot, so buying the upgrade earlier or later gives the same offer. Existing offers never change.
        public bool EnsureExtraSlots(int count)
        {
            int target = Math.Min(Math.Max(0, count), MaxExtraSlots);
            bool added = false;
            while (ExtraSlotCount < target && extraOfferFactory != null)
            {
                int slot = FirstExtraSlot + ExtraSlotCount;
                ShopOffer offer = extraOfferFactory(slot);
                if (offer == null || offer.SlotIndex != slot || GetOffer(slot) != null) break;
                offers.Add(offer);
                ExtraSlotCount++;
                added = true;
            }

            if (added) OffersChanged?.Invoke();
            return added;
        }

        // Development panel: makes one Item offer still on sale an elif offer, to test elif purchases on demand.
        public bool TryConvertToElifPrice(int slotIndex)
        {
            int index = offers.FindIndex(offer => offer.SlotIndex == slotIndex);
            if (index < 0 || IsPurchased(slotIndex) || offers[index].Kind != ShopOfferKind.Item ||
                offers[index].IsElifPriced)
                return false;
            offers[index] = offers[index].WithElifPrice();
            PricesChanged?.Invoke();
            return true;
        }

        public bool IsPurchased(int slotIndex) => slotIndex >= 0 && slotIndex < purchased.Length && purchased[slotIndex];

        public bool TryMarkPurchased(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= purchased.Length || purchased[slotIndex] || GetOffer(slotIndex) == null)
                return false;
            purchased[slotIndex] = true;
            Purchased?.Invoke(slotIndex);
            return true;
        }
    }

    public static class ShopStockBuilder
    {
        public const uint StockSalt = 0x53544F4Bu;
        // Elif-3: the elif offer roll and the extra-slot offers hash the stock seed apart from the offers' own rolls,
        // so a seed lists the same Items with or without them.
        public const uint ElifOfferSalt = 0x454C4F46u;
        public const uint ExtraSlotSalt = 0x45585452u;
        // Shop-1: 골디 sells Items of this rarity or better.
        public const ItemRarity GoldiMinimumRarity = ItemRarity.Rare;

        public static string BuildShopId(string roomId) => $"{roomId}:shop";

        // Items use the treasure-room rarity weights (distinct, skipping maxed stacks at roll time); consumables are
        // distinct picks from the catalog. If too few Items are eligible, extra consumables fill the empty slots.
        // The 골디 shop rolls among Items of GoldiMinimumRarity or better first and fills what is left from the rest.
        // Power-2: when the room's 권능 roll hits (ActivePowerOffers.ShopPercent), the last Item slot sells one
        // 교주의 권능 from powerPool at the Rare price instead.
        // Elif-3: with rollElifPrice, ShopUpgrades.ElifOfferPercent of shops sell one artifact offer for elif (never
        // the 권능 or a consumable). The stock also knows how to fill the extra slots 상품 칸 추가 opens.
        public static ShopStockState Build(string roomId, int roomContentSeed, ShopCatalog catalog,
            IReadOnlyList<ItemDefinition> itemPool, PlayerInventory inventory, ShopKind kind = ShopKind.General,
            IReadOnlyList<ItemDefinition> powerPool = null, bool rollElifPrice = false)
        {
            string shopId = BuildShopId(roomId);
            int stockSeed = StockSeedOf(roomContentSeed);
            List<ShopOffer> offers = new(ShopCatalog.OfferCount);
            ItemDefinition power = ActivePowerOffers.RollShop(roomContentSeed)
                ? ActivePowerOffers.Choose(powerPool, ActivePowerOffers.HeldPower(inventory),
                    unchecked((uint)FloorGenerator.DeriveSeed(stockSeed, 0, ActivePowerOffers.ShopSalt)))
                : null;
            int itemLimit = ShopCatalog.ItemOfferCount - (power != null ? 1 : 0);
            SplitByRarity(itemPool, out List<ItemDefinition> better, out List<ItemDefinition> rest);
            if (kind == ShopKind.Goldi)
            {
                AddItemOffers(offers, better, inventory, stockSeed, shopId, catalog, itemLimit);
                AddItemOffers(offers, rest, inventory, stockSeed, shopId + ":fill", catalog, itemLimit);
            }
            else
            {
                AddItemOffers(offers, itemPool, inventory, stockSeed, shopId, catalog, itemLimit);
            }

            if (rollElifPrice && RollElifOffer(stockSeed, offers.Count, out int elifIndex))
                offers[elifIndex] = offers[elifIndex].WithElifPrice();

            if (power != null) offers.Add(ShopOffer.ForItem(offers.Count, power, catalog.GetItemPrice(power.Rarity)));

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

            ShopOffer[] baseOffers = offers.ToArray();
            IReadOnlyList<ItemDefinition> extraPool = kind == ShopKind.Goldi && better.Count > 0 ? better : itemPool;
            return new ShopStockState(shopId, baseOffers, kind,
                slot => BuildExtraOffer(slot, baseOffers, extraPool, stockSeed, shopId, catalog));
        }

        public static int StockSeedOf(int roomContentSeed) => FloorGenerator.DeriveSeed(roomContentSeed, 0, StockSalt);

        // Elif-3: whether this shop sells an elif offer and which of its first artifactCount offers it is.
        public static bool RollElifOffer(int stockSeed, int artifactCount, out int index)
        {
            index = -1;
            if (artifactCount <= 0) return false;
            uint roll = unchecked((uint)FloorGenerator.DeriveSeed(stockSeed, 0, ElifOfferSalt));
            if (roll % 100u >= ShopUpgrades.ElifOfferPercent) return false;
            index = (int)(unchecked((uint)FloorGenerator.DeriveSeed(stockSeed, 1, ElifOfferSalt)) %
                          (uint)artifactCount);
            return true;
        }

        // Extra slot k rolls from the pool minus the shop's base Items and the Items of the extra slots before it.
        // It ignores the inventory so the offer never depends on when the upgrade was bought; a maxed Item shows as
        // "보유 한도" like any other offer.
        private static ShopOffer BuildExtraOffer(int slot, IReadOnlyList<ShopOffer> baseOffers,
            IReadOnlyList<ItemDefinition> pool, int stockSeed, string shopId, ShopCatalog catalog)
        {
            int extraIndex = slot - ShopStockState.FirstExtraSlot;
            if (extraIndex < 0 || extraIndex >= ShopStockState.MaxExtraSlots || pool == null || catalog == null)
                return null;
            HashSet<string> taken = new(StringComparer.Ordinal);
            foreach (ShopOffer offer in baseOffers)
                if (offer.Kind == ShopOfferKind.Item) taken.Add(offer.Item.ItemId);

            ItemDefinition chosen = null;
            for (int index = 0; index <= extraIndex; index++)
            {
                List<ItemDefinition> remaining = new();
                foreach (ItemDefinition item in pool)
                    if (item != null && !taken.Contains(item.ItemId)) remaining.Add(item);
                chosen = null;
                foreach (ItemRewardCandidate candidate in ArtifactRewardSelector.BuildCandidates(remaining, null,
                             FloorGenerator.DeriveSeed(stockSeed, index, ExtraSlotSalt), $"{shopId}:extra-{index}"))
                {
                    if (!candidate.IsItem) continue;
                    chosen = candidate.Definition;
                    break;
                }

                if (chosen == null) return null;
                taken.Add(chosen.ItemId);
            }

            return ShopOffer.ForItem(slot, chosen, catalog.GetItemPrice(chosen.Rarity));
        }

        private static void SplitByRarity(IReadOnlyList<ItemDefinition> itemPool, out List<ItemDefinition> better,
            out List<ItemDefinition> rest)
        {
            better = new List<ItemDefinition>();
            rest = new List<ItemDefinition>();
            if (itemPool == null) return;
            foreach (ItemDefinition item in itemPool)
                if (item != null) (item.Rarity >= GoldiMinimumRarity ? better : rest).Add(item);
        }

        private static void AddItemOffers(List<ShopOffer> offers, IReadOnlyList<ItemDefinition> itemPool,
            PlayerInventory inventory, int stockSeed, string rewardId, ShopCatalog catalog, int itemLimit)
        {
            if (offers.Count >= itemLimit) return;
            foreach (ItemRewardCandidate candidate in ArtifactRewardSelector.BuildCandidates(
                         itemPool, inventory, stockSeed, rewardId))
            {
                if (offers.Count >= itemLimit) break;
                if (candidate.IsItem)
                    offers.Add(ShopOffer.ForItem(offers.Count, candidate.Definition,
                        catalog.GetItemPrice(candidate.Definition.Rarity)));
            }
        }
    }
}
