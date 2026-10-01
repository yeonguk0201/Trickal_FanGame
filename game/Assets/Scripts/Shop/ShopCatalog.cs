using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    [Serializable]
    public sealed class ShopConsumable
    {
        [Tooltip("상점 소모품의 안정 ID입니다. 기존 ID의 의미를 바꾸거나 재사용하지 않습니다.")]
        [SerializeField] private string consumableId;
        [SerializeField] private string displayName;
        [Tooltip("구매하면 상품 앞 바닥에 떨어뜨리는 픽업입니다.")]
        [SerializeField] private GameObject pickupPrefab;
        [SerializeField, Min(1)] private int price = 1;

        public ShopConsumable(string configuredId, string configuredDisplayName, GameObject configuredPrefab,
            int configuredPrice)
        {
            consumableId = configuredId;
            displayName = configuredDisplayName;
            pickupPrefab = configuredPrefab;
            price = configuredPrice;
        }

        public string ConsumableId => consumableId;
        public string DisplayName => displayName;
        public GameObject PickupPrefab => pickupPrefab;
        public int Price => price;
    }

    // Special-4 price list: fixed gold prices per Item rarity and per consumable, tunable without code changes.
    [CreateAssetMenu(menuName = "Trickal Fan Game/Shop Catalog", fileName = "ShopCatalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        public const int ItemOfferCount = 2;
        public const int ConsumableOfferCount = 2;
        public const int OfferCount = ItemOfferCount + ConsumableOfferCount;

        [SerializeField, Min(1)] private int commonPrice = 10;
        [SerializeField, Min(1)] private int uncommonPrice = 15;
        [SerializeField, Min(1)] private int rarePrice = 20;
        [SerializeField, Min(1)] private int epicPrice = 25;
        [SerializeField] private ShopConsumable[] consumables = Array.Empty<ShopConsumable>();

        public IReadOnlyList<ShopConsumable> Consumables => consumables;

        public int GetItemPrice(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => commonPrice,
            ItemRarity.Uncommon => uncommonPrice,
            ItemRarity.Rare => rarePrice,
            ItemRarity.Epic => epicPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
        };

        public void Configure(int configuredCommon, int configuredUncommon, int configuredRare, int configuredEpic,
            ShopConsumable[] configuredConsumables)
        {
            commonPrice = configuredCommon;
            uncommonPrice = configuredUncommon;
            rarePrice = configuredRare;
            epicPrice = configuredEpic;
            consumables = configuredConsumables ?? Array.Empty<ShopConsumable>();
        }

        public bool TryValidate(out string error)
        {
            if (commonPrice <= 0 || uncommonPrice <= 0 || rarePrice <= 0 || epicPrice <= 0)
            {
                error = $"{name} needs a positive price for every Item rarity.";
                return false;
            }

            if (consumables == null || consumables.Length < ConsumableOfferCount)
            {
                error = $"{name} needs at least {ConsumableOfferCount} consumables.";
                return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ShopConsumable consumable in consumables)
            {
                if (consumable == null || string.IsNullOrWhiteSpace(consumable.ConsumableId) ||
                    !ids.Add(consumable.ConsumableId) || string.IsNullOrWhiteSpace(consumable.DisplayName) ||
                    consumable.PickupPrefab == null || consumable.Price <= 0)
                {
                    error = $"{name} consumables need unique IDs, names, pickup Prefabs, and positive prices.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
