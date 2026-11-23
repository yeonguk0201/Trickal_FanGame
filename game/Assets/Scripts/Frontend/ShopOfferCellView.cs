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
        // Elif-3: an elif offer's price reads in the elif HUD color instead of gold.
        private static readonly Color ElifPriceColor = new(0.72f, 0.88f, 1f, 1f);
        // Shop UI artwork: dark text on the cream cells. A price the player cannot pay turns red and the cell does
        // not open the purchase popup; an empty cell keeps a faded frame.
        public static readonly Color ArtworkPriceColor = new(0.29f, 0.2f, 0.2f, 1f);
        public static readonly Color ArtworkBlockedColor = new(0.82f, 0.34f, 0.29f, 1f);
        public static readonly Color ArtworkFreeColor = new(0.3f, 0.55f, 0.2f, 1f);
        public static readonly Color ArtworkSoldColor = new(0.55f, 0.48f, 0.44f, 1f);
        public const float ArtworkEmptyFrameAlpha = 0.45f;

        [SerializeField] private int cellIndex;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Behaviour focusFrame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Image frame;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private Sprite goldSprite;
        [SerializeField] private Sprite elifSprite;
        [SerializeField] private ShopHoverTarget hover;

        private bool buttonBound;
        private bool hoverBound;

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
        public Image Frame => frame;
        public Image CurrencyIcon => currencyIcon;
        public ShopHoverTarget Hover => hover;
        public bool UsesArtwork => currencyIcon != null;
        // Shop UI artwork: only an offer the player can take now opens the purchase popup.
        public bool CanOpenPurchase => Status is ShopOfferStatus.Available or ShopOfferStatus.Free;
        public event Action<ShopOfferCellView> Clicked;
        public event Action<ShopOfferCellView> Hovered;

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

        // Shop UI artwork: the cell frame, the gold or elif picture beside the price, and the hover target.
        public void ConfigureArtwork(Image configuredFrame, Image configuredCurrencyIcon, Sprite configuredGold,
            Sprite configuredElif, ShopHoverTarget configuredHover)
        {
            UnbindHover();
            frame = configuredFrame;
            currencyIcon = configuredCurrencyIcon;
            goldSprite = configuredGold;
            elifSprite = configuredElif;
            hover = configuredHover;
            BindHover();
        }

        private void Awake() => BindButton();

        private void OnEnable()
        {
            BindButton();
            BindHover();
        }

        private void OnDestroy()
        {
            UnbindButton();
            UnbindHover();
        }

        public void Bind(int boundIndex, ShopOffer offer, ShopOfferStatus status, int price, bool focused)
        {
            cellIndex = boundIndex;
            Offer = status == ShopOfferStatus.Empty ? null : offer;
            Status = Offer == null ? ShopOfferStatus.Empty : status;
            IsFocused = focused;
            if (focusFrame != null) focusFrame.enabled = focused;
            if (content != null) content.alpha = Status == ShopOfferStatus.Empty ? 0f :
                Status == ShopOfferStatus.SoldOut ? 0.35f : 1f;
            if (UsesArtwork)
            {
                if (button != null) button.interactable = CanOpenPurchase;
                if (frame != null)
                {
                    Color color = frame.color;
                    color.a = Offer == null ? ArtworkEmptyFrameAlpha : 1f;
                    frame.color = color;
                }
                if (currencyIcon != null) currencyIcon.enabled = false;
            }
            if (Offer == null) return;

            ApplyIcon(Offer);
            if (nameText != null) nameText.text = Offer.DisplayName;
            if (priceText == null) return;
            if (UsesArtwork)
            {
                BindArtworkPrice(price);
                return;
            }
            priceText.text = ShopView.FormatPriceLabel(Status, price, Offer.IsElifPriced);
            priceText.color = Status switch
            {
                ShopOfferStatus.Free => FreeColor,
                ShopOfferStatus.SoldOut => SoldColor,
                ShopOfferStatus.NotEnoughGold or ShopOfferStatus.ItemUnavailable or ShopOfferStatus.NotEnoughElif or
                    ShopOfferStatus.ElifLocked => BlockedColor,
                _ => Offer.IsElifPriced ? ElifPriceColor : PriceColor,
            };
        }

        private void BindArtworkPrice(int price)
        {
            priceText.text = ShopView.FormatCompactPriceLabel(Status, price);
            priceText.color = Status switch
            {
                ShopOfferStatus.Free => ArtworkFreeColor,
                ShopOfferStatus.SoldOut => ArtworkSoldColor,
                ShopOfferStatus.NotEnoughGold or ShopOfferStatus.ItemUnavailable or ShopOfferStatus.NotEnoughElif or
                    ShopOfferStatus.ElifLocked => ArtworkBlockedColor,
                _ => ArtworkPriceColor,
            };
            if (currencyIcon == null) return;
            currencyIcon.enabled = ShopView.ShowsCurrencyIcon(Status, price);
            currencyIcon.sprite = Offer.IsElifPriced ? elifSprite : goldSprite;
            currencyIcon.preserveAspect = true;
        }

        // Consumables use pickup artwork; items use the same illustration as the HUD and floor drop.
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
                    : offer.Kind != ShopOfferKind.Consumable ? ArtifactHudSlotView.GetRarityColor(offer.Item.Rarity)
                    : Color.white;
            }

            if (offer.Kind != ShopOfferKind.Consumable) UserArtwork.Apply(icon, offer.Item, iconText);
            else if (iconText != null) iconText.gameObject.SetActive(false);

            if (iconText != null)
                iconText.text = offer.Kind != ShopOfferKind.Consumable
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

        private void BindHover()
        {
            if (hoverBound || hover == null) return;
            hover.Entered += NotifyHovered;
            hoverBound = true;
        }

        private void UnbindHover()
        {
            if (!hoverBound || hover == null) return;
            hover.Entered -= NotifyHovered;
            hoverBound = false;
        }

        private void NotifyClicked() => Clicked?.Invoke(this);
        private void NotifyHovered(ShopHoverTarget _) => Hovered?.Invoke(this);
    }
}
