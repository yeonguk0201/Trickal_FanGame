using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    // Shop-0: the open shop UI. It only says which shop is open and for whom, and blocks the player's actions
    // meanwhile; prices, stock and sold slots stay in ShopRoom/ShopStockState, so closing and reopening loses nothing.
    [DisallowMultipleComponent]
    public sealed class ShopSession : MonoBehaviour
    {
        [SerializeField] private PlayerActionState playerActionState;
        [SerializeField] private Health playerHealth;

        private int closedFrame = -1;

        public ShopRoom Shop { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public bool IsOpen { get; private set; }
        // Escape closes the shop; the pause menu must not open on that same key press.
        public bool BlocksPause => IsOpen || closedFrame == Time.frameCount;
        public PlayerActionState PlayerActionState => playerActionState;
        public Health PlayerHealth => playerHealth;
        public event Action Opened;
        public event Action Closed;

        public void Configure(PlayerActionState configuredActionState, Health configuredHealth)
        {
            playerActionState = configuredActionState;
            playerHealth = configuredHealth;
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (IsOpen && !CanStayOpen(Shop)) Close();
        }

        public bool TryOpen(ShopRoom shop, PlayerInventory inventory)
        {
            if (IsOpen || !CanStayOpen(shop) || shop.Progress.IsRewardSelectionPending) return false;
            Shop = shop;
            Inventory = inventory;
            IsOpen = true;
            if (playerActionState != null) playerActionState.SetShopBlocked(true);
            Opened?.Invoke();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Shop = null;
            Inventory = null;
            closedFrame = Time.frameCount;
            if (playerActionState != null) playerActionState.SetShopBlocked(false);
            Closed?.Invoke();
        }

        public ShopPurchaseResult TryPurchase(int slotIndex) =>
            IsOpen && Shop != null ? Shop.TryPurchase(slotIndex, Inventory) : ShopPurchaseResult.Unavailable;

        private bool CanStayOpen(ShopRoom shop) =>
            shop != null && shop.Stock != null && shop.Progress != null && !shop.Progress.IsProgressionStopped &&
            (playerHealth == null || !playerHealth.IsDead);
    }
}
