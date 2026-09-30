using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Shop
{
    // One shop slot: shows the offer and its price, and buys it with E while the player stands at the stall.
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopStall : MonoBehaviour
    {
        public const string BuyPrompt = "[E] 구매";
        public const string NotEnoughElifPrompt = "엘리프 부족";

        private static readonly Color AffordableColor = new(0.9f, 0.98f, 1f, 1f);
        private static readonly Color UnaffordableColor = new(1f, 0.45f, 0.4f, 1f);

        [SerializeField] private SpriteRenderer display;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private TextMeshPro prompt;

        private ShopRoom owner;
        private ShopStockState boundStock;
        private Sprite itemSprite;
        private Vector3 itemScale = Vector3.one;
        private bool isPlayerInside;
        private PlayerInventory playerInventory;
        private Health playerHealth;

        public ShopOffer Offer { get; private set; }
        public SpriteRenderer Display => display;
        public TextMeshPro Label => label;
        public TextMeshPro Prompt => prompt;
        public bool IsSold => owner == null || owner.IsSold(Offer);
        public bool CanInteract => isPlayerInside && !IsSold && Time.timeScale > 0f &&
                                   (playerHealth == null || !playerHealth.IsDead);

        public void ConfigureVisuals(SpriteRenderer configuredDisplay, TextMeshPro configuredLabel,
            TextMeshPro configuredPrompt)
        {
            display = configuredDisplay;
            label = configuredLabel;
            prompt = configuredPrompt;
        }

        public void Bind(ShopRoom configuredOwner, ShopOffer offer)
        {
            if (boundStock != null) boundStock.Purchased -= OnPurchased;
            owner = configuredOwner;
            Offer = offer;
            boundStock = owner != null ? owner.Stock : null;
            if (boundStock != null) boundStock.Purchased += OnPurchased;
            if (display != null && itemSprite == null)
            {
                itemSprite = display.sprite;
                itemScale = display.transform.localScale;
            }

            ApplyOfferVisuals();
            RefreshVisuals();
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnDestroy()
        {
            if (boundStock != null) boundStock.Purchased -= OnPurchased;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null) return;
            SetPlayerPresence(true, player.GetComponent<PlayerInventory>(), player.GetComponent<Health>());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null) SetPlayerPresence(false, null, null);
        }

        private void Update()
        {
            if (!isPlayerInside) return;
            RefreshVisuals();
            if (CanInteract && Keyboard.current?.eKey.wasPressedThisFrame == true)
                owner.TryPurchase(Offer.SlotIndex, playerInventory);
        }

        public void SetPlayerPresence(bool inside, PlayerInventory inventory, Health health)
        {
            isPlayerInside = inside;
            playerInventory = inside ? inventory : null;
            playerHealth = inside ? health : null;
            RefreshVisuals();
        }

        private void OnPurchased(int slotIndex)
        {
            if (Offer != null && slotIndex == Offer.SlotIndex) RefreshVisuals();
        }

        private void ApplyOfferVisuals()
        {
            if (display != null && Offer != null)
            {
                SpriteRenderer source = Offer.Kind == ShopOfferKind.Consumable
                    ? Offer.Consumable.PickupPrefab.GetComponentInChildren<SpriteRenderer>()
                    : null;
                display.sprite = source != null ? source.sprite : itemSprite;
                display.color = source != null
                    ? source.color
                    : ArtifactHudSlotView.GetRarityColor(Offer.Item.Rarity);
                display.transform.localScale = source != null ? source.transform.lossyScale : itemScale;
            }

            if (label != null && Offer != null) label.text = $"{Offer.DisplayName}\n{Offer.Price} 엘리프";
        }

        private void RefreshVisuals()
        {
            bool sold = IsSold;
            if (display != null) display.gameObject.SetActive(!sold);
            if (label != null) label.gameObject.SetActive(!sold);
            if (prompt == null) return;
            prompt.gameObject.SetActive(CanInteract);
            bool affordable = owner != null && owner.CanAfford(Offer);
            prompt.text = affordable ? BuyPrompt : NotEnoughElifPrompt;
            prompt.color = affordable ? AffordableColor : UnaffordableColor;
        }
    }
}
