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
        // Elif-3 (contract 27 §4): elif offers and the invest screen of the general shop.
        public const string ElifSuffix = "엘리프";
        public const string ElifLockedLabel = "사용 불가";
        public const string NotEnoughElifMessage = "엘리프 부족";
        public const string ElifLockedMessage = "서버 잔액을 불러오지 못해 이번 Run은 엘리프를 쓸 수 없습니다";
        public const string ElifOfferNote = "엘리프로만 살 수 있는 상품입니다. 할인은 적용되지 않습니다.";
        public const string InvestLabel = "투자 [Q]";
        public const string InvestBackLabel = "상품 보기 [Q]";
        public const string InvestBuyLabel = "투자";
        public const string InvestTitle = "상점 투자";
        public const string InvestKind = "상점 투자";
        public const string InvestNotice =
            "투자는 바로 적용되고, 이번 Run 결과가 저장되어야 다음 Run에도 남습니다. 포기하면 사라집니다.";
        public const string InvestPurchasedMessage = "투자 완료";
        public const string InvestMaxLevelMessage = "최대 단계입니다";
        public const string InvestPendingSaveMessage = "저장 대기 중인 이전 Run의 투자가 먼저 저장되어야 합니다";
        public const string InvestDevelopmentMessage = "개발 패널 단계가 적용 중입니다";
        public const string MaxLevelLabel = "최대 단계";

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
        [SerializeField] private TMP_Text elifText;
        [SerializeField] private Button investButton;
        [SerializeField] private TMP_Text investButtonLabel;
        [SerializeField] private CanvasGroup investPanel;
        [SerializeField] private ShopUpgradeRowView[] investRows = Array.Empty<ShopUpgradeRowView>();
        [SerializeField] private TMP_Text investNoticeText;

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
        public TMP_Text ElifText => elifText;
        public Button InvestButton => investButton;
        public TMP_Text InvestButtonLabel => investButtonLabel;
        public CanvasGroup InvestPanel => investPanel;
        public ShopUpgradeRowView[] InvestRows => investRows;
        public TMP_Text InvestNoticeText => investNoticeText;
        public ShopKind ShownKind { get; private set; }
        public bool IsVisible { get; private set; }
        public int FocusedIndex { get; private set; }
        public bool IsInvesting { get; private set; }
        public int InvestFocusedIndex { get; private set; }

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

        // Elif-3: the elif balance line, the invest button and the invest list that covers the grid while open.
        public void ConfigureInvest(TMP_Text configuredElifText, Button configuredInvestButton,
            TMP_Text configuredInvestButtonLabel, CanvasGroup configuredInvestPanel,
            ShopUpgradeRowView[] configuredRows, TMP_Text configuredNotice)
        {
            Unsubscribe();
            elifText = configuredElifText;
            investButton = configuredInvestButton;
            investButtonLabel = configuredInvestButtonLabel;
            investPanel = configuredInvestPanel;
            investRows = configuredRows ?? Array.Empty<ShopUpgradeRowView>();
            investNoticeText = configuredNotice;
            Subscribe();
            SetText(investNoticeText, InvestNotice);
            SetInvesting(false);
        }

        public static string FormatUpgradeLevel(int level, int maxLevel) => $"{level} / {maxLevel}단계";

        public static string FormatUpgradeNext(ShopUpgradeDefinition definition, int level) =>
            definition == null ? string.Empty
            : level >= definition.MaxLevel
                ? $"{ShopUpgrades.DescribeLevel(definition.UpgradeId, definition.MaxLevel)} (최대)"
                : $"다음: {ShopUpgrades.DescribeLevel(definition.UpgradeId, level + 1)}";

        public static string FormatUpgradeCost(ShopUpgradeDefinition definition, int level) =>
            definition == null || level >= definition.MaxLevel
                ? MaxLevelLabel
                : $"{definition.GetCost(level + 1)} {ElifSuffix}";

        public static string FormatElifBalance(int available, bool spendingAllowed) =>
            spendingAllowed ? $"{ElifSuffix} {available}" : $"{ElifSuffix} {available} ({ElifLockedLabel})";

        public static string GetUpgradeMessage(ShopUpgradeStatus status) => status switch
        {
            ShopUpgradeStatus.Purchased => InvestPurchasedMessage,
            ShopUpgradeStatus.MaxLevel => InvestMaxLevelMessage,
            ShopUpgradeStatus.NotEnoughElif => NotEnoughElifMessage,
            ShopUpgradeStatus.ElifLocked => ElifLockedMessage,
            ShopUpgradeStatus.PendingSave => InvestPendingSaveMessage,
            ShopUpgradeStatus.DevelopmentOverride => InvestDevelopmentMessage,
            _ => string.Empty,
        };

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

        public static string FormatPrice(int price, bool elif = false) =>
            price > 0 ? $"{price} {(elif ? ElifSuffix : GoldSuffix)}" : FreeLabel;

        public static string FormatPriceLabel(ShopOfferStatus status, int price, bool elif = false) => status switch
        {
            ShopOfferStatus.Empty => string.Empty,
            ShopOfferStatus.SoldOut => SoldOutLabel,
            ShopOfferStatus.ItemUnavailable => ItemUnavailableLabel,
            _ => FormatPrice(price, elif),
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
                // Escape leaves the invest list first, then closes the shop.
                if (IsInvesting) SetInvesting(false);
                else Close();
                return;
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                ToggleInvesting();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) MoveFocus(-1, 0);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) MoveFocus(1, 0);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveFocus(0, -1);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveFocus(0, 1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                if (IsInvesting) TryInvestFocused();
                else TryPurchaseFocused();
            }
        }

        public bool CanInvest => session != null && session.Shop != null && session.Shop.SellsUpgrades &&
                                 investPanel != null && investRows.Length > 0;

        public bool ToggleInvesting() => SetInvesting(!IsInvesting);

        // Opens or closes the invest list. Only the general shop (시스트) has it.
        public bool SetInvesting(bool investing)
        {
            bool next = investing && IsVisible && CanInvest;
            bool changed = next != IsInvesting;
            IsInvesting = next;
            if (changed)
            {
                InvestFocusedIndex = 0;
                SetFeedback(string.Empty);
            }

            if (investPanel != null)
            {
                investPanel.alpha = next ? 1f : 0f;
                investPanel.interactable = next;
                investPanel.blocksRaycasts = next;
            }

            if (IsVisible) Refresh();
            return next == investing;
        }

        public void SetInvestFocus(int rowIndex)
        {
            if (!IsInvesting || rowIndex < 0 || rowIndex >= Math.Min(investRows.Length, ShopUpgrades.Catalog.Length))
                return;
            if (rowIndex != InvestFocusedIndex) SetFeedback(string.Empty);
            InvestFocusedIndex = rowIndex;
            Refresh();
        }

        public ShopUpgradeStatus TryInvestFocused()
        {
            if (!IsInvesting || session == null || InvestFocusedIndex >= ShopUpgrades.Catalog.Length)
                return ShopUpgradeStatus.Unavailable;
            ShopUpgradeStatus result = session.TryPurchaseUpgrade(ShopUpgrades.Catalog[InvestFocusedIndex].UpgradeId);
            SetFeedback(GetUpgradeMessage(result));
            Refresh();
            return result;
        }

        // Rows grow downward: dy = 1 moves to the row below. The focus stops at the grid edge.
        public void MoveFocus(int dx, int dy)
        {
            if (!IsVisible) return;
            if (IsInvesting)
            {
                if (dy != 0) SetInvestFocus(Mathf.Clamp(InvestFocusedIndex + dy, 0,
                    Math.Min(investRows.Length, ShopUpgrades.Catalog.Length) - 1));
                return;
            }
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
            if (!IsVisible || IsInvesting || session == null) return ShopPurchaseResult.Unavailable;
            ShopPurchaseResult result = session.TryPurchase(FocusedIndex);
            SetFeedback(result switch
            {
                ShopPurchaseResult.Purchased => PurchasedMessage,
                ShopPurchaseResult.NotEnoughGold => NotEnoughGoldMessage,
                ShopPurchaseResult.NotEnoughElif => NotEnoughElifMessage,
                ShopPurchaseResult.ElifLocked => ElifLockedMessage,
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
                ShopOffer offer = stock.GetOffer(index);
                cells[index].Bind(index, offer, shop.GetStatus(index, session.Inventory), shop.GetPrice(offer),
                    index == FocusedIndex && !IsInvesting);
            }

            if (goldText != null && shop.Progress != null)
                goldText.text = $"보유 {shop.Progress.GetResourceCount(RunResourceType.Gold)} {GoldSuffix}";
            if (elifText != null && shop.Progress != null)
                elifText.text = FormatElifBalance(shop.Progress.AvailableElif, shop.Progress.IsElifSpendingAllowed);
            if (investButton != null) investButton.gameObject.SetActive(CanInvest);
            if (investButtonLabel != null) investButtonLabel.text = IsInvesting ? InvestBackLabel : InvestLabel;
            if (IsInvesting)
            {
                RefreshInvest(shop);
                return;
            }

            RefreshDetail(shop, FocusedOffer, shop.GetStatus(FocusedIndex, session.Inventory));
        }

        private void RefreshInvest(ShopRoom shop)
        {
            for (int index = 0; index < investRows.Length; index++)
            {
                if (investRows[index] == null) continue;
                ShopUpgradeDefinition definition =
                    index < ShopUpgrades.Catalog.Length ? ShopUpgrades.Catalog[index] : null;
                investRows[index].gameObject.SetActive(definition != null);
                if (definition == null) continue;
                investRows[index].Bind(definition, shop.Progress.GetShopUpgradeLevel(definition.UpgradeId),
                    shop.GetUpgradeStatus(definition.UpgradeId), index == InvestFocusedIndex);
            }

            ShopUpgradeDefinition focused = ShopUpgrades.Catalog[InvestFocusedIndex];
            int level = shop.Progress.GetShopUpgradeLevel(focused.UpgradeId);
            ShopUpgradeStatus status = shop.GetUpgradeStatus(focused.UpgradeId);
            if (buyButton != null) buyButton.interactable = status == ShopUpgradeStatus.Available;
            if (buyButtonLabel != null) buyButtonLabel.text = InvestBuyLabel;
            SetText(detailNameText, focused.DisplayName);
            SetText(detailKindText, $"{InvestKind}  ·  {FormatUpgradeLevel(level, focused.MaxLevel)}");
            string blocked = status is ShopUpgradeStatus.ElifLocked or ShopUpgradeStatus.PendingSave or
                ShopUpgradeStatus.DevelopmentOverride
                ? $"\n\n{GetUpgradeMessage(status)}"
                : string.Empty;
            SetText(detailEffectText, $"{FormatUpgradeNext(focused, level)}{blocked}\n\n{InvestNotice}");
            SetText(detailPriceText, FormatUpgradeCost(focused, level));
        }

        private ShopOffer FocusedOffer
        {
            get
            {
                ShopStockState stock = session != null && session.Shop != null ? session.Shop.Stock : null;
                return stock?.GetOffer(FocusedIndex);
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
                string note = offer.IsElifPriced ? $"\n\n{ElifOfferNote}" : string.Empty;
                if (status == ShopOfferStatus.ElifLocked) note += $"\n{ElifLockedMessage}";
                SetText(detailEffectText, item.IsActivePower
                    ? $"{ArtifactEffectDescription.Build(item)}\n\n{GamePowerHudView.BuildOfferLine(item)}{note}"
                    : $"{ArtifactEffectDescription.Build(item)}\n\n현재 스택 {stacks} / {maximum}{note}");
            }
            else
            {
                SetText(detailKindText, ConsumableKind);
                SetText(detailEffectText, GetConsumableEffect(shop.Kind));
            }

            SetText(detailPriceText, FormatPriceLabel(status, shop.GetPrice(offer), offer.IsElifPriced));
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
            SetInvesting(false);
            Refresh();
        }

        private void OnSessionClosed()
        {
            SetInvesting(false);
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
            for (int index = 0; index < ShopCatalog.GridSlotCount; index++)
                if (stock.GetOffer(index) != null && !stock.IsPurchased(index)) return index;
            return 0;
        }

        private void OnCellClicked(ShopOfferCellView cell)
        {
            ClearSelection();
            if (cell != null && !IsInvesting) SetFocus(cell.CellIndex);
        }

        private void OnBuyClicked()
        {
            ClearSelection();
            if (IsInvesting) TryInvestFocused();
            else TryPurchaseFocused();
        }

        private void OnInvestClicked()
        {
            ClearSelection();
            ToggleInvesting();
        }

        private void OnInvestRowClicked(ShopUpgradeRowView row)
        {
            ClearSelection();
            if (row != null) SetInvestFocus(row.RowIndex);
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
            if (investButton != null) investButton.onClick.AddListener(OnInvestClicked);
            foreach (ShopUpgradeRowView row in investRows)
                if (row != null) row.Clicked += OnInvestRowClicked;
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
            if (investButton != null) investButton.onClick.RemoveListener(OnInvestClicked);
            foreach (ShopUpgradeRowView row in investRows)
                if (row != null) row.Clicked -= OnInvestRowClicked;
            subscribed = false;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
