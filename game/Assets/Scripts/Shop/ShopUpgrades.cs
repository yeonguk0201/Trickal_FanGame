using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    public sealed class ShopUpgradeDefinition
    {
        private readonly int[] levelCosts;

        public ShopUpgradeDefinition(string upgradeId, string displayName, int[] configuredLevelCosts)
        {
            UpgradeId = upgradeId;
            DisplayName = displayName;
            levelCosts = configuredLevelCosts ?? Array.Empty<int>();
        }

        public string UpgradeId { get; }
        public string DisplayName { get; }
        // Index 0 is the cost of level 1.
        public IReadOnlyList<int> LevelCosts => levelCosts;
        public int MaxLevel => levelCosts.Length;

        // -1 for a level outside 1..MaxLevel.
        public int GetCost(int level) => level >= 1 && level <= levelCosts.Length ? levelCosts[level - 1] : -1;
    }

    public enum ShopUpgradeStatus
    {
        Available,
        MaxLevel,
        NotEnoughElif,
        // The Run started without a fresh server balance (contract 27 §4.3), so no elif can be spent.
        ElifLocked,
        // An earlier Run that bought this upgrade is still waiting to be saved; buying the next level now could
        // reach the server before it and be refused as a level gap (contract 27 §11).
        PendingSave,
        // The development panel set this upgrade's level for this Run only.
        DevelopmentOverride,
        Unavailable,
        // Only returned by a purchase that went through.
        Purchased,
    }

    // One upgrade level bought in this Run, saved as elif.upgradePurchases (contract 27 §5.2).
    public sealed class ShopUpgradePurchase
    {
        public ShopUpgradePurchase(string upgradeId, int level, int floor, int order)
        {
            UpgradeId = upgradeId;
            Level = level;
            Floor = floor;
            Order = order;
        }

        public string UpgradeId { get; }
        public int Level { get; }
        public int Floor { get; }
        public int Order { get; }
    }

    // Elif-3 (contract 27 §4): the shop's permanent upgrades and elif prices. The IDs, levels and costs must match
    // SHOP_UPGRADE_CATALOG in backend/src/contracts/permanent-elif.ts. IDs are stable keys like Item IDs: never
    // rename, reuse or delete them.
    public static class ShopUpgrades
    {
        public const string ExtraSlotId = "shop-upgrade-extra-slot";
        public const string DiscountId = "shop-upgrade-discount";
        public const int DiscountPercentPerLevel = 10;
        // Each shop rolls once: at this chance one of its Item offers costs elif instead of gold.
        public const int ElifOfferPercent = 25;
        public const int ElifPriceDivisor = 10;

        public static readonly ShopUpgradeDefinition[] Catalog =
        {
            new(ExtraSlotId, "상품 칸 추가", new[] { 2, 3, 5 }),
            new(DiscountId, "상점 할인", new[] { 2, 3, 4 }),
        };

        public static ShopUpgradeDefinition Find(string upgradeId)
        {
            foreach (ShopUpgradeDefinition definition in Catalog)
                if (string.Equals(definition.UpgradeId, upgradeId, StringComparison.Ordinal)) return definition;
            return null;
        }

        public static int GetCost(string upgradeId, int level) => Find(upgradeId)?.GetCost(level) ?? -1;

        // Gold price ÷ 10, rounded up: Common 1, Uncommon 2, Rare 2, Epic 3 with the current price list.
        public static int ElifPriceOf(int goldPrice) =>
            goldPrice <= 0 ? 0 : (goldPrice + ElifPriceDivisor - 1) / ElifPriceDivisor;

        public static int DiscountPercent(int level) =>
            Mathf.Clamp(level, 0, Find(DiscountId).MaxLevel) * DiscountPercentPerLevel;

        // max(1, floor(price × (1 − rate))). A free price stays free.
        public static int ApplyDiscount(int goldPrice, int level)
        {
            int percent = DiscountPercent(level);
            if (goldPrice <= 0 || percent <= 0) return goldPrice;
            return Mathf.Max(1, goldPrice * (100 - percent) / 100);
        }

        // What the next level does, for the invest screen.
        public static string DescribeLevel(string upgradeId, int level)
        {
            ShopUpgradeDefinition definition = Find(upgradeId);
            if (definition == null || level < 1 || level > definition.MaxLevel) return string.Empty;
            return upgradeId switch
            {
                ExtraSlotId => $"아랫줄에 아티팩트 상품 {level}칸",
                DiscountId => $"골드 가격 {level * DiscountPercentPerLevel}% 할인",
                _ => string.Empty,
            };
        }
    }
}
