using System;
using TMPro;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Shop-0: the shop screen. The shopkeeper stands on the left and the 3×3 offer grid on the right; the detail
    // column describes the focused cell. Arrow keys or WASD move the focus, Enter/Space/E buys, Escape closes.
    // It reads ShopRoom/ShopStockState every frame, so price changes (멤버십카드) and gold changes show at once.
    // Shop-1: the same screen shows the 골디 shop with 골디's portrait, texts, and a gold panel.
    [DisallowMultipleComponent]
    public sealed class ShopView : MonoBehaviour
    {
        public const string Title = "시스트의 상점";
        public const string KeeperLine = "마음에 드신 상품이… 있을깝쇼?";
        public const string GoldiTitle = "골디의 상점";
        public const string GoldiKeeperLine = "부담스럽게 생각마시고 천천히 둘러보세요";
        public const string GoldSuffix = "골드";
        public const string FreeLabel = "무료";
        public const string SoldOutLabel = "판매 완료";
        public const string ItemUnavailableLabel = "보유 한도";
        public const string BuyLabel = "구매";
        public const string TakeLabel = "받기";
        public const string CloseLabel = "닫기";
        public const string NotEnoughGoldMessage = "골드 부족";
        public const string PurchasedMessage = "구매 완료";
        public const string SoldOutMessage = "이미 판매된 상품입니다";
        public const string ItemUnavailableMessage = "더 가질 수 없는 아이템입니다";
        public const string EmptyCellMessage = "빈 칸입니다";
        public const string ConsumableKind = "소모품";
        public const string ConsumableEffect = "구매하면 시스트 앞에 떨어집니다. 주워서 사용합니다.";
        public const string GoldiConsumableEffect = "구매하면 골디 앞에 떨어집니다. 주워서 사용합니다.";
        public const string InputHint = "방향키 이동  ·  Enter 구매  ·  Esc 닫기";

        public static readonly Color PanelColor = new(0.055f, 0.075f, 0.12f, 1f);
        public static readonly Color PanelOutlineColor = new(0.95f, 0.78f, 0.38f, 0.9f);
        public static readonly Vector2 PanelOutlineDistance = new(2f, -2f);
        public static readonly Color TitleColor = new(0.92f, 0.96f, 1f, 1f);
        public static readonly Color GoldiPanelColor = new(0.17f, 0.115f, 0.035f, 1f);
        public static readonly Color GoldiPanelOutlineColor = new(1f, 0.87f, 0.3f, 1f);
        public static readonly Vector2 GoldiPanelOutlineDistance = new(5f, -5f);
        public static readonly Color GoldiTitleColor = new(1f, 0.87f, 0.3f, 1f);

        [SerializeField] private ShopSession session;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem uiEventSystem;
        [SerializeField] private Image keeperPortrait;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private ShopOfferCellView[] cells = Array.Empty<ShopOfferCellView>();
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailKindText;
        [SerializeField] private TMP_Text detailEffectText;
        [SerializeField] private TMP_Text detailPriceText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text keeperNameText;
        [SerializeField] private TMP_Text keeperLineText;
        [SerializeField] private Image panelImage;
        [SerializeField] private Outline panelOutline;

        private bool subscribed;
        private int shownFrame = -1;
        private GameObject previousSelectedObject;

        public ShopSession Session => session;
        public CanvasGroup Overlay => overlay;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public Image KeeperPortrait => keeperPortrait;
        public TMP_Text GoldText => goldText;
        public ShopOfferCellView[] Cells => cells;
        public TMP_Text DetailNameText => detailNameText;
        public TMP_Text DetailKindText => detailKindText;
        public TMP_Text DetailEffectText => detailEffectText;
        public TMP_Text DetailPriceText => detailPriceText;
        public TMP_Text FeedbackText => feedbackText;
        public Button BuyButton => buyButton;
        public TMP_Text BuyButtonLabel => buyButtonLabel;
        public Button CloseButton => closeButton;
        public TMP_Text TitleText => titleText;
        public TMP_Text KeeperNameText => keeperNameText;
        public TMP_Text KeeperLineText => keeperLineText;
        public Image PanelImage => panelImage;
        public Outline PanelOutline => panelOutline;
        public ShopKind ShownKind { get; private set; }
        public bool IsVisible { get; private set; }
        public int FocusedIndex { get; private set; }

        public void Configure(ShopSession configuredSession, CanvasGroup configuredOverlay,
            GraphicRaycaster configuredRaycaster, EventSystem configuredEventSystem, Image configuredKeeperPortrait,
            TMP_Text configuredGoldText, ShopOfferCellView[] configuredCells, TMP_Text configuredDetailName,
            TMP_Text configuredDetailKind, TMP_Text configuredDetailEffect, TMP_Text configuredDetailPrice,
            TMP_Text configuredFeedback, Button configuredBuyButton, TMP_Text configuredBuyButtonLabel,
            Button configuredCloseButton)
        {
            Unsubscribe();
            session = configuredSession;
            overlay = configuredOverlay;
            graphicRaycaster = configuredRaycaster;
            uiEventSystem = configuredEventSystem;
            keeperPortrait = configuredKeeperPortrait;
            goldText = configuredGoldText;
            cells = configuredCells ?? Array.Empty<ShopOfferCellView>();
            detailNameText = configuredDetailName;
            detailKindText = configuredDetailKind;
            detailEffectText = configuredDetailEffect;
            detailPriceText = configuredDetailPrice;
            feedbackText = configuredFeedback;
            buyButton = configuredBuyButton;
            buyButtonLabel = configuredBuyButtonLabel;
            closeButton = configuredCloseButton;
            Subscribe();
            UserArtwork.Apply(keeperPortrait, UserArtwork.Load(ShopKeeper.ArtworkKey));
            ApplyVisibility(false);
        }

        // Shop-1: the parts that differ between the general shop and the 골디 shop.
        public void ConfigureTheme(TMP_Text configuredTitle, TMP_Text configuredKeeperName,
            TMP_Text configuredKeeperLine, Image configuredPanelImage, Outline configuredPanelOutline)
        {
            titleText = configuredTitle;
            keeperNameText = configuredKeeperName;
            keeperLineText = configuredKeeperLine;
            panelImage = configuredPanelImage;
            panelOutline = configuredPanelOutline;
            ApplyTheme(ShopKind.General);
        }

        public static string GetTitle(ShopKind kind) => kind == ShopKind.Goldi ? GoldiTitle : Title;
        public static string GetKeeperLine(ShopKind kind) => kind == ShopKind.Goldi ? GoldiKeeperLine : KeeperLine;
        public static string GetConsumableEffect(ShopKind kind) =>
            kind == ShopKind.Goldi ? GoldiConsumableEffect : ConsumableEffect;

        private void ApplyTheme(ShopKind kind)
        {
            bool goldi = kind == ShopKind.Goldi;
            ShownKind = kind;
            SetText(titleText, GetTitle(kind));
            if (titleText != null) titleText.color = goldi ? GoldiTitleColor : TitleColor;
            SetText(keeperNameText, ShopKeeper.GetDisplayName(kind));
            SetText(keeperLineText, GetKeeperLine(kind));
            UserArtwork.Apply(keeperPortrait, UserArtwork.Load(ShopKeeper.GetArtworkKey(kind)));
            if (panelImage != null) panelImage.color = goldi ? GoldiPanelColor : PanelColor;
            if (panelOutline != null)
            {
                panelOutline.effectColor = goldi ? GoldiPanelOutlineColor : PanelOutlineColor;
                panelOutline.effectDistance = goldi ? GoldiPanelOutlineDistance : PanelOutlineDistance;
            }
        }

        public static string FormatPrice(int price) => price > 0 ? $"{price} {GoldSuffix}" : FreeLabel;

        public static string FormatPriceLabel(ShopOfferStatus status, int price) => status switch
        {
            ShopOfferStatus.Empty => string.Empty,
            ShopOfferStatus.SoldOut => SoldOutLabel,
            ShopOfferStatus.ItemUnavailable => ItemUnavailableLabel,
            _ => FormatPrice(price),
        };

        private void Awake()
        {
            Subscribe();
            ApplyVisibility(false);
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (!IsVisible) return;
            // The E press that opened the shop must not also buy the first offer.
            if (Time.frameCount != shownFrame) ReadKeyboard();
            if (IsVisible) Refresh();
        }

        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) MoveFocus(-1, 0);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) MoveFocus(1, 0);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveFocus(0, -1);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveFocus(0, 1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                TryPurchaseFocused();
        }

        // Rows grow downward: dy = 1 moves to the row below. The focus stops at the grid edge.
        public void MoveFocus(int dx, int dy)
        {
            if (!IsVisible) return;
            int columns = ShopCatalog.GridColumns;
            int column = Mathf.Clamp(FocusedIndex % columns + dx, 0, columns - 1);
            int row = Mathf.Clamp(FocusedIndex / columns + dy, 0, ShopCatalog.GridSlotCount / columns - 1);
            SetFocus(row * columns + column);
        }

        public void SetFocus(int cellIndex)
        {
            if (!IsVisible || cellIndex < 0 || cellIndex >= ShopCatalog.GridSlotCount) return;
            if (cellIndex != FocusedIndex) SetFeedback(string.Empty);
            FocusedIndex = cellIndex;
            Refresh();
        }

        public ShopPurchaseResult TryPurchaseFocused()
        {
            if (!IsVisible || session == null) return ShopPurchaseResult.Unavailable;
            ShopPurchaseResult result = session.TryPurchase(FocusedIndex);
            SetFeedback(result switch
            {
                ShopPurchaseResult.Purchased => PurchasedMessage,
                ShopPurchaseResult.NotEnoughGold => NotEnoughGoldMessage,
                ShopPurchaseResult.SoldOut => SoldOutMessage,
                ShopPurchaseResult.ItemUnavailable => ItemUnavailableMessage,
                _ => FocusedOffer == null ? EmptyCellMessage : string.Empty,
            });
            Refresh();
            return result;
        }

        public void Close()
        {
            if (session != null) session.Close();
        }

        public void Refresh()
        {
            ShopRoom shop = session != null ? session.Shop : null;
            if (!IsVisible || shop == null || shop.Stock == null) return;
            ShopStockState stock = shop.Stock;
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index] == null) continue;
                ShopOffer offer = index < stock.Offers.Count ? stock.Offers[index] : null;
                cells[index].Bind(index, offer, shop.GetStatus(index, session.Inventory), shop.GetPrice(offer),
                    index == FocusedIndex);
            }

            if (goldText != null && shop.Progress != null)
                goldText.text = $"보유 {shop.Progress.GetResourceCount(RunResourceType.Gold)} {GoldSuffix}";
            RefreshDetail(shop, FocusedOffer, shop.GetStatus(FocusedIndex, session.Inventory));
        }

        private ShopOffer FocusedOffer
        {
            get
            {
                ShopStockState stock = session != null && session.Shop != null ? session.Shop.Stock : null;
                return stock != null && FocusedIndex < stock.Offers.Count ? stock.Offers[FocusedIndex] : null;
            }
        }

        private void RefreshDetail(ShopRoom shop, ShopOffer offer, ShopOfferStatus status)
        {
            bool canBuy = status is ShopOfferStatus.Available or ShopOfferStatus.Free;
            if (buyButton != null) buyButton.interactable = canBuy;
            if (buyButtonLabel != null) buyButtonLabel.text = status == ShopOfferStatus.Free ? TakeLabel : BuyLabel;
            if (offer == null)
            {
                SetText(detailNameText, string.Empty);
                SetText(detailKindText, string.Empty);
                SetText(detailEffectText, EmptyCellMessage);
                SetText(detailPriceText, string.Empty);
                return;
            }

            SetText(detailNameText, offer.DisplayName);
            if (offer.Kind == ShopOfferKind.Item)
            {
                ItemDefinition item = offer.Item;
                SetText(detailKindText,
                    $"{ItemKindText.GetDisplayName(item.Kind)}  ·  {ItemRewardCardView.RarityLabel(item.Rarity)}");
                string maximum = item.MaxStacks > 0 ? item.MaxStacks.ToString() : "제한 없음";
                int stacks = session.Inventory != null ? session.Inventory.GetStackCount(item.ItemId) : 0;
                SetText(detailEffectText,
                    $"{ArtifactEffectDescription.Build(item)}\n\n현재 스택 {stacks} / {maximum}");
            }
            else
            {
                SetText(detailKindText, ConsumableKind);
                SetText(detailEffectText, GetConsumableEffect(shop.Kind));
            }

            SetText(detailPriceText, FormatPriceLabel(status, shop.GetPrice(offer)));
        }

        private void OnSessionOpened()
        {
            EventSystem activeEventSystem = ActiveEventSystem;
            previousSelectedObject = activeEventSystem != null ? activeEventSystem.currentSelectedGameObject : null;
            // Selection stays empty while the shop is open: the view reads the keyboard itself, so a selected
            // Button must not also receive Submit.
            if (activeEventSystem != null) activeEventSystem.SetSelectedGameObject(null);
            shownFrame = Time.frameCount;
            FocusedIndex = FirstOfferOnSale();
            ApplyTheme(session.Shop != null ? session.Shop.Kind : ShopKind.General);
            SetFeedback(string.Empty);
            ApplyVisibility(true);
            Refresh();
        }

        private void OnSessionClosed()
        {
            ApplyVisibility(false);
            EventSystem activeEventSystem = ActiveEventSystem;
            if (activeEventSystem != null)
                activeEventSystem.SetSelectedGameObject(
                    previousSelectedObject != null && previousSelectedObject.activeInHierarchy
                        ? previousSelectedObject
                        : null);
            previousSelectedObject = null;
        }

        private int FirstOfferOnSale()
        {
            ShopStockState stock = session != null && session.Shop != null ? session.Shop.Stock : null;
            if (stock == null) return 0;
            for (int index = 0; index < stock.Offers.Count && index < ShopCatalog.GridSlotCount; index++)
                if (!stock.IsPurchased(index)) return index;
            return 0;
        }

        private void OnCellClicked(ShopOfferCellView cell)
        {
            ClearSelection();
            if (cell != null) SetFocus(cell.CellIndex);
        }

        private void OnBuyClicked()
        {
            ClearSelection();
            TryPurchaseFocused();
        }

        private void OnCloseClicked()
        {
            ClearSelection();
            Close();
        }

        private void ClearSelection()
        {
            EventSystem activeEventSystem = ActiveEventSystem;
            if (activeEventSystem != null) activeEventSystem.SetSelectedGameObject(null);
        }

        private void SetFeedback(string message) => SetText(feedbackText, message);

        private void ApplyVisibility(bool show)
        {
            IsVisible = show;
            if (overlay != null)
            {
                overlay.alpha = show ? 1f : 0f;
                overlay.interactable = show;
                overlay.blocksRaycasts = show;
            }
            if (graphicRaycaster != null) graphicRaycaster.enabled = show;
        }

        private EventSystem ActiveEventSystem => uiEventSystem != null ? uiEventSystem : EventSystem.current;

        private void Subscribe()
        {
            if (subscribed || session == null) return;
            session.Opened += OnSessionOpened;
            session.Closed += OnSessionClosed;
            for (int index = 0; index < cells.Length; index++)
                if (cells[index] != null) cells[index].Clicked += OnCellClicked;
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (session != null)
            {
                session.Opened -= OnSessionOpened;
                session.Closed -= OnSessionClosed;
            }
            for (int index = 0; index < cells.Length; index++)
                if (cells[index] != null) cells[index].Clicked -= OnCellClicked;
            if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
            subscribed = false;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
