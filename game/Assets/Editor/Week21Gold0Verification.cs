using System;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Gold-0: the Run currency is renamed from elif to gold. Only identifiers and display text change; the
    // serialized value 0, the pickup Prefab and its GUID, the 99 cap, and shop purchases keep their behavior.
    public static class Week21Gold0Verification
    {
        // GUIDs recorded before the rename (2026-10-02); the rename must not recreate these assets.
        private const string GoldPickupGuid = "615b40b3d6223ad4a9363c3e586e6072";
        private const string LegacyCurrencyName = "엘리프";
        private const string GoldName = "골드";

        [MenuItem("Trickal Fan Game/Week 21/Verify Gold-0 Run Currency")]
        public static void Verify()
        {
            ValidateEnumContract();
            ValidatePickupPrefab();
            ValidateShopText();
            Week17Resource1Verification.Verify();
            Week20Special4Verification.Verify();
            Debug.Log("Gold-0 verification passed: RunResourceType keeps Gold = 0 / Key = 1 / Bomb = 2 with no Elif " +
                      "name, the gold pickup Prefab keeps its path, GUID and serialized value, shop prices, prompts " +
                      "and placeholders read gold, and the Resource-1 (99 cap) and Special-4 (shop purchase) " +
                      "regressions pass.");
        }

        private static void ValidateEnumContract()
        {
            Assert((int)RunResourceType.Gold == 0 && (int)RunResourceType.Key == 1 && (int)RunResourceType.Bomb == 2,
                "RunResourceType values are serialized by value and must stay Gold 0, Key 1, Bomb 2.");
            Assert(!Enum.GetNames(typeof(RunResourceType)).Contains("Elif"),
                "The Run currency must be named Gold; a permanent Elif may only be appended after Bomb.");
            Assert(!Enum.GetNames(typeof(ShopPurchaseResult)).Contains("NotEnoughElif") &&
                   Enum.IsDefined(typeof(ShopPurchaseResult), "NotEnoughGold"),
                "ShopPurchaseResult must report NotEnoughGold.");
        }

        private static void ValidatePickupPrefab()
        {
            string path = Week17Resource1Setup.ElifPrefabPath;
            Assert(AssetDatabase.AssetPathToGUID(path) == GoldPickupGuid,
                $"The gold pickup Prefab at {path} must keep GUID {GoldPickupGuid}.");
            RunResourcePickup pickup = Week17Resource1Setup.LoadPrefab(RunResourceType.Gold);
            Assert(pickup != null && AssetDatabase.GetAssetPath(pickup) == path &&
                   pickup.ResourceType == RunResourceType.Gold && pickup.Amount == 1,
                "The existing pickup Prefab must still grant exactly one gold.");
        }

        private static void ValidateShopText()
        {
            Assert(ShopView.NotEnoughGoldMessage.Contains(GoldName) &&
                   !ShopView.NotEnoughGoldMessage.Contains(LegacyCurrencyName),
                "The shop must say gold is short, not elif.");

            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            Assert(catalog != null && catalog.Consumables.Count > 0, "Gold-0 requires the Special-4 shop catalog.");
            ShopConsumable consumable = catalog.Consumables[0];
            string price = ShopView.FormatPriceLabel(ShopOfferStatus.NotEnoughGold, consumable.Price);
            Assert(price == $"{consumable.Price} {GoldName}" && !price.Contains(LegacyCurrencyName),
                $"A shop cell must show its price in gold, got '{price}'.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
