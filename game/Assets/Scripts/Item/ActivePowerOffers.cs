using System;
using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // Power-2 (plan 26 §4.4): where a 교주의 권능 is offered. The start-floor treasure room always gives one of its 3택1
    // cards to a 권능; a treasure-style secret room does so with SecretRoomPercent, and a shop sells one 권능 in an Item
    // slot with ShopPercent at the Rare price. Every roll comes from the room's content seed, so a seed rebuilds the
    // same offers, and the rolled stock or 3택1 is kept by RunProgress for revisits.
    public static class ActivePowerOffers
    {
        public const int SecretRoomPercent = 30;
        public const int ShopPercent = 25;
        public const uint SecretRoomSalt = 0x50575352u;
        public const uint ShopSalt = 0x50575348u;

        // Where the 3택1 drops the 권능 the player was holding when they take a new one, from the reward spot.
        public static readonly Vector3 RewardDropOffset = new(0f, -1.2f, 0f);

        public static bool IsGuaranteedInTreasureRoom(int floorNumber) =>
            floorNumber == FloorGenerator.StartTreasureFloor;

        public static bool RollSecretRoom(int roomContentSeed) => Roll(roomContentSeed, SecretRoomSalt, SecretRoomPercent);

        public static bool RollShop(int roomContentSeed) => Roll(roomContentSeed, ShopSalt, ShopPercent);

        // A Goldi shop's secret room has no 3택1. A shop's own roll lives with its stock (ShopStockBuilder).
        public static bool IsOfferedInRewardRoom(GeneratedRoomNode node)
        {
            if (node == null) return false;
            return node.Role switch
            {
                GeneratedRoomRole.Treasure => IsGuaranteedInTreasureRoom(node.FloorNumber),
                GeneratedRoomRole.Secret => !node.IsGoldiShop && RollSecretRoom(node.ContentSeed),
                _ => false,
            };
        }

        // Valid 권능 in a stable ID order, without duplicates.
        public static List<ItemDefinition> ValidPowers(IReadOnlyList<ItemDefinition> pool)
        {
            List<ItemDefinition> result = new();
            if (pool == null) return result;
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ItemDefinition power in pool)
                if (power != null && power.IsValid && power.IsActive && power.IsActivePower && ids.Add(power.ItemId))
                    result.Add(power);
            result.Sort((left, right) => string.CompareOrdinal(left.ItemId, right.ItemId));
            return result;
        }

        // Picks one 권능 with the hash, skipping the one the player holds while another is left.
        public static ItemDefinition Choose(IReadOnlyList<ItemDefinition> pool, ItemDefinition held, uint hash)
        {
            List<ItemDefinition> powers = ValidPowers(pool);
            if (held != null && powers.Count > 1) powers.RemoveAll(power => power.ItemId == held.ItemId);
            return powers.Count == 0 ? null : powers[(int)(hash % (uint)powers.Count)];
        }

        public static ItemDefinition HeldPower(PlayerInventory inventory) =>
            inventory != null && inventory.TryGetComponent(out PlayerPowerSlot slot) ? slot.HeldDefinition : null;

        private static bool Roll(int roomContentSeed, uint salt, int percent) =>
            unchecked((uint)FloorGenerator.DeriveSeed(roomContentSeed, 0, salt)) % 100u < (uint)percent;
    }
}
