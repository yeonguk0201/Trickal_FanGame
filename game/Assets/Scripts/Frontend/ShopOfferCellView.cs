using System;
using TMPro;
using TrickalFanGame.Item;
using TrickalFanGame.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // One cell of the 3×3 shop grid: an offer's icon, name and price, or an empty cell.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ShopOfferCellView : MonoBehaviour
    {
        private static readonly Color PriceColor = new(1f, 0.93f, 0.62f, 1f);
        private static readonly Color FreeColor = new(0.55f, 1f, 0.7f, 1f);
        private static readonly Color BlockedColor = new(1f, 0.45f, 0.4f, 1f);
        private static readonly Color SoldColor = new(0.6f, 0.65f, 0.72f, 1f);

        [SerializeField] private int cellIndex;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Behaviour focusFrame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;

        private bool buttonBound;

        public int CellIndex => cellIndex;
        public ShopOffer Offer { get; private set; }
        public ShopOfferStatus Status { get; private set; } = ShopOfferStatus.Empty;
        public bool IsFocused { get; private set; }
        public Button Button => button;
        public CanvasGroup Content => content;
        public Behaviour FocusFrame => focusFrame;
        public Image Icon => icon;
        public TMP_Text IconText => iconText;
        public TMP_Text NameText => nameText;
        public TMP_Text PriceText => priceText;
        public event Action<ShopOfferCellView> Clicked;

        public void ConfigureVisuals(int configuredIndex, Button configuredButton, CanvasGroup configuredContent,
            Behaviour configuredFocusFrame, Image configuredIcon, TMP_Text configuredIconText,
            TMP_Text configuredNameText, TMP_Text configuredPriceText)
        {
            UnbindButton();
            cellIndex = configuredIndex;
            button = configuredButton;
            content = configuredContent;
            focusFrame = configuredFocusFrame;
            icon = configuredIcon;
            iconText = configuredIconText;
            nameText = configuredNameText;
            priceText = configuredPriceText;
            BindButton();
        }

        private void Awake() => BindButton();
        private void OnEnable() => BindButton();
        private void OnDestroy() => UnbindButton();

        public void Bind(int boundIndex, ShopOffer offer, ShopOfferStatus status, int price, bool focused)
        {
            cellIndex = boundIndex;
            Offer = status == ShopOfferStatus.Empty ? null : offer;
            Status = Offer == null ? ShopOfferStatus.Empty : status;
            IsFocused = focused;
            if (focusFrame != null) focusFrame.enabled = focused;
            if (content != null) content.alpha = Status == ShopOfferStatus.Empty ? 0f :
                Status == ShopOfferStatus.SoldOut ? 0.35f : 1f;
            if (Offer == null) return;

            ApplyIcon(Offer);
            if (nameText != null) nameText.text = Offer.DisplayName;
            if (priceText == null) return;
            priceText.text = ShopView.FormatPriceLabel(Status, price);
            priceText.color = Status switch
            {
                ShopOfferStatus.Free => FreeColor,
                ShopOfferStatus.SoldOut => SoldColor,
                ShopOfferStatus.NotEnoughGold or ShopOfferStatus.ItemUnavailable => BlockedColor,
                _ => PriceColor,
            };
        }

        // A consumable shows its floor pickup's sprite; an Item has no artwork yet and shows the HUD placeholder
        // (rarity color and short stable ID).
        private void ApplyIcon(ShopOffer offer)
        {
            SpriteRenderer source = offer.Kind == ShopOfferKind.Consumable && offer.Consumable.PickupPrefab != null
                ? offer.Consumable.PickupPrefab.GetComponentInChildren<SpriteRenderer>()
                : null;
            if (icon != null)
            {
                icon.sprite = source != null ? source.sprite : null;
                icon.preserveAspect = true;
                icon.color = source != null ? source.color
                    : offer.Kind == ShopOfferKind.Item ? ArtifactHudSlotView.GetRarityColor(offer.Item.Rarity)
                    : Color.white;
            }

            if (iconText != null)
                iconText.text = offer.Kind == ShopOfferKind.Item
                    ? ArtifactHudSlotView.GetShortStableId(offer.Item.ItemId)
                    : string.Empty;
        }

        private void BindButton()
        {
            if (buttonBound || button == null) return;
            button.onClick.AddListener(NotifyClicked);
            buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!buttonBound || button == null) return;
            button.onClick.RemoveListener(NotifyClicked);
            buttonBound = false;
        }

        private void NotifyClicked() => Clicked?.Invoke(this);
    }
}
