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
        // The keeper lines break where the clover bubble needs two lines.
        public const string KeeperLine = "마음에 드신 상품이…\n있을깝쇼?";
        public const string GoldiTitle = "골디의 상점";
        public const string GoldiKeeperLine = "부담스럽게 생각마시고\n천천히 둘러보세요";
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
        public const string InputHint = "방향키 이동  ·  Enter 구매  ·  Q 투자  ·  Esc 닫기";
        // Shop UI artwork: Enter or a click opens the purchase popup, and Enter there buys.
        public const string ArtworkInputHint = "방향키 이동  ·  Enter 선택  ·  Q 투자  ·  Esc 닫기";
        public const string ArtworkGoldiInputHint = "방향키 이동  ·  Enter 선택  ·  Esc 닫기";
        public const string PurchaseTitle = "상품 구매";
        public const string CancelLabel = "취소";
        // Elif-3 (contract 27 §4): elif offers and the invest screen of the general shop.
        public const string ElifSuffix = "엘리프";
        public const string ElifLockedLabel = "사용 불가";
        public const string NotEnoughElifMessage = "엘리프 부족";
        public const string ElifLockedMessage = "서버 잔액을 불러오지 못해 이번 Run은 엘리프를 쓸 수 없습니다";
        public const string ElifOfferNote = "엘리프로만 살 수 있는 상품입니다. 할인은 적용되지 않습니다.";
        public const string InvestLabel = "투자";
        public const string InvestBackLabel = "상품 보기";
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
        // Shop UI artwork: titles on the cream plaque. The panel keeps its artwork colors in both shops.
        public static readonly Color ArtworkTitleColor = new(0.35f, 0.23f, 0.23f, 1f);
        public static readonly Color ArtworkGoldiTitleColor = new(0.48f, 0.35f, 0.06f, 1f);

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
        [Header("Shop UI artwork")]
        [SerializeField] private bool usesArtwork;
        [SerializeField] private CanvasGroup confirmPopup;
        [SerializeField] private TMP_Text confirmTitleText;
        [SerializeField] private Image confirmIcon;
        [SerializeField] private GameObject confirmIconGlow;
        [SerializeField] private ShopHoverTarget confirmIconHover;
        [SerializeField] private TMP_Text confirmNoteText;
        [SerializeField] private Image confirmCurrencyIcon;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button dismissButton;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private CanvasGroup tooltipGroup;
        [SerializeField] private TMP_Text tooltipNameText;
        [SerializeField] private TMP_Text inputHintText;
        [SerializeField] private Sprite goldSprite;
        [SerializeField] private Sprite elifSprite;
        [SerializeField] private float tooltipBaseHeight;
        [SerializeField] private float tooltipEffectBaseHeight;

        private bool subscribed;
        private bool confirmByKeyboard;
        [SerializeField] private GameObject buyFocusMark;
        [SerializeField] private GameObject cancelFocusMark;
        [SerializeField] private ShopHoverTarget buyHover;
        [SerializeField] private ShopHoverTarget cancelHover;
        [SerializeField] private GameObject investFocusMark;
        [SerializeField] private GameObject closeFocusMark;
        private int shownFrame = -1;
        private GameObject previousSelectedObject;

        public ShopSession Session => session;
        public CanvasGroup Overlay => overlay;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public EventSystem UiEventSystem => uiEventSystem;
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
        public bool UsesArtwork => usesArtwork;
        public CanvasGroup ConfirmPopup => confirmPopup;
        public TMP_Text ConfirmTitleText => confirmTitleText;
        public Image ConfirmIcon => confirmIcon;
        public TMP_Text ConfirmNoteText => confirmNoteText;
        public Image ConfirmCurrencyIcon => confirmCurrencyIcon;
        public Button CancelButton => cancelButton;
        public Button DismissButton => dismissButton;
        public RectTransform Tooltip => tooltip;
        public CanvasGroup TooltipGroup => tooltipGroup;
        public TMP_Text TooltipNameText => tooltipNameText;
        public TMP_Text InputHintText => inputHintText;
        public bool IsConfirming { get; private set; }
        public bool IsTooltipShown => tooltipGroup != null && tooltipGroup.alpha > 0f;
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

        // Shop UI artwork: the purchase popup replaces the detail column. The detail texts and the buy button move
        // into it: the name and price sit in the popup, the kind and effect in the tooltip that opens while the mouse
        // rests on the item picture (or at once when Enter opened the popup).
        public void ConfigureArtwork(CanvasGroup configuredPopup, TMP_Text configuredPopupTitle,
            Image configuredPopupIcon, GameObject configuredIconGlow, ShopHoverTarget configuredIconHover,
            TMP_Text configuredNote, Image configuredCurrencyIcon, Button configuredCancel, Button configuredDismiss,
            RectTransform configuredTooltip, CanvasGroup configuredTooltipGroup, TMP_Text configuredTooltipName,
            TMP_Text configuredInputHint, Sprite configuredGold, Sprite configuredElif)
        {
            Unsubscribe();
            usesArtwork = true;
            confirmPopup = configuredPopup;
            confirmTitleText = configuredPopupTitle;
            confirmIcon = configuredPopupIcon;
            confirmIconGlow = configuredIconGlow;
            confirmIconHover = configuredIconHover;
            confirmNoteText = configuredNote;
            confirmCurrencyIcon = configuredCurrencyIcon;
            cancelButton = configuredCancel;
            dismissButton = configuredDismiss;
            tooltip = configuredTooltip;
            tooltipGroup = configuredTooltipGroup;
            tooltipNameText = configuredTooltipName;
            inputHintText = configuredInputHint;
            goldSprite = configuredGold;
            elifSprite = configuredElif;
            tooltipBaseHeight = tooltip != null ? tooltip.sizeDelta.y : 0f;
            tooltipEffectBaseHeight = detailEffectText != null ? detailEffectText.rectTransform.sizeDelta.y : 0f;
            Subscribe();
            SetConfirming(false, false);
            ApplyTheme(ShopKind.General);
        }

        public static string FormatCompactPriceLabel(ShopOfferStatus status, int price) => status switch
        {
            ShopOfferStatus.Empty => string.Empty,
            ShopOfferStatus.SoldOut => SoldOutLabel,
            ShopOfferStatus.ItemUnavailable => ItemUnavailableLabel,
            _ => price > 0 ? price.ToString() : FreeLabel,
        };

        public static bool ShowsCurrencyIcon(ShopOfferStatus status, int price) =>
            price > 0 && status is not (ShopOfferStatus.Empty or ShopOfferStatus.SoldOut or
                ShopOfferStatus.ItemUnavailable or ShopOfferStatus.Free);

        // The gold and elif lines: a number beside the picture with the artwork, the full text without it.
        public string FormatGold(int gold) => usesArtwork ? gold.ToString() : $"보유 {gold} {GoldSuffix}";

        public string FormatElif(int available, bool spendingAllowed) =>
            !usesArtwork ? FormatElifBalance(available, spendingAllowed)
            : spendingAllowed ? available.ToString() : $"{available} ({ElifLockedLabel})";

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
        // The rarity in Korean only (the reward cards show the same words).
        public static string KoreanRarityLabel(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "전설",
            _ => "일반",
        };

        // What a shop consumable does, keyed by its stable ID (shop-catalog.asset).
        public static string DescribeConsumable(string consumableId) => consumableId switch
        {
            "heart" => "HP를 1칸 회복합니다.",
            "sp" => "SP를 1칸 회복합니다.",
            "bomb" => "폭탄 1개를 얻습니다. 장애물을 부수고 주변 적에게 피해를 줍니다.",
            "key" => "열쇠 1개를 얻습니다. 잠긴 방이나 황금 상자를 열 수 있습니다.",
            _ => string.Empty,
        };

        public static string GetConsumableEffect(ShopKind kind) =>
            kind == ShopKind.Goldi ? GoldiConsumableEffect : ConsumableEffect;

        private void ApplyTheme(ShopKind kind)
        {
            bool goldi = kind == ShopKind.Goldi;
            ShownKind = kind;
            SetText(titleText, GetTitle(kind));
            if (titleText != null)
                titleText.color = usesArtwork ? goldi ? ArtworkGoldiTitleColor : ArtworkTitleColor
                    : goldi ? GoldiTitleColor : TitleColor;
            SetText(inputHintText, goldi ? ArtworkGoldiInputHint : ArtworkInputHint);
            SetText(keeperNameText, ShopKeeper.GetDisplayName(kind));
            SetText(keeperLineText, GetKeeperLine(kind));
            UserArtwork.Apply(keeperPortrait, UserArtwork.Load(ShopKeeper.GetArtworkKey(kind)));
            if (usesArtwork) return;
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
            // A popup opened with Enter shows the tooltip at once; once the mouse moves, the tooltip follows the
            // mouse resting on the item picture like a popup opened with a click.
            if (confirmByKeyboard && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0f)
                confirmByKeyboard = false;
            if (IsVisible) Refresh();
        }

        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // Escape closes the purchase popup first, then leaves the invest list, then closes the shop.
                if (IsConfirming) CancelConfirm();
                else if (IsInvesting) SetInvesting(false);
                else Close();
                return;
            }

            bool submit = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                          keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame;
            if (IsConfirming)
            {
                // Left and right choose between 취소 and 구매; Enter presses the chosen one.
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    SetConfirmChoice(false);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    SetConfirmChoice(true);
                if (submit) Submit(true);
                return;
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                ToggleInvesting();
                return;
            }

            Vector2Int move = ReadRepeatedDirection(keyboard);
            if (move != Vector2Int.zero) MoveFocus(move.x, move.y);
            if (submit) Submit(true);
        }

        // Holding a direction repeats the move after a short delay, like a text cursor.
        public const float MoveRepeatDelay = 0.35f;
        public const float MoveRepeatInterval = 0.09f;
        private Vector2Int heldDirection;
        private float moveRepeatTimer;

        private Vector2Int ReadRepeatedDirection(Keyboard keyboard)
        {
            Vector2Int pressed =
                keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ? Vector2Int.left
                : keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ? Vector2Int.right
                : keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ? Vector2Int.down
                : keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ? Vector2Int.up
                : Vector2Int.zero;
            if (pressed != Vector2Int.zero)
            {
                // Rows grow downward: up is dy -1 and down is dy +1.
                heldDirection = pressed;
                moveRepeatTimer = MoveRepeatDelay;
                return pressed;
            }

            if (heldDirection == Vector2Int.zero || !IsHeld(keyboard, heldDirection))
            {
                heldDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }

            moveRepeatTimer -= Time.unscaledDeltaTime;
            if (moveRepeatTimer > 0f) return Vector2Int.zero;
            moveRepeatTimer += MoveRepeatInterval;
            return heldDirection;
        }

        private static bool IsHeld(Keyboard keyboard, Vector2Int direction) =>
            direction == Vector2Int.left ? keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed
            : direction == Vector2Int.right ? keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed
            : direction == Vector2Int.down ? keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed
            : keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;

        // Enter or a click on the focused offer. Without the artwork it buys at once; with it, it opens the
        // purchase popup, and inside the popup it buys.
        public void Submit(bool byKeyboard)
        {
            if (!IsVisible) return;
            if (!usesArtwork)
            {
                if (IsInvesting) TryInvestFocused();
                else TryPurchaseFocused();
                return;
            }

            if (!IsConfirming && FocusedAction != ShopFocusAction.None)
            {
                ShopFocusAction action = FocusedAction;
                FocusedAction = ShopFocusAction.None;
                if (action == ShopFocusAction.Invest) ToggleInvesting();
                else Close();
                return;
            }

            if (!IsConfirming) OpenConfirm(byKeyboard);
            else if (ConfirmChoosesBuy) ConfirmFocused();
            else CancelConfirm();
        }

        // Which popup button Enter presses. The popup opens on 구매, so Enter twice still buys.
        public bool ConfirmChoosesBuy { get; private set; } = true;

        public void SetConfirmChoice(bool buy)
        {
            ConfirmChoosesBuy = buy;
            ApplyConfirmChoice();
        }

        private void ApplyConfirmChoice()
        {
            if (buyFocusMark != null) buyFocusMark.SetActive(IsConfirming && ConfirmChoosesBuy);
            if (cancelFocusMark != null) cancelFocusMark.SetActive(IsConfirming && !ConfirmChoosesBuy);
        }

        // The ring around the chosen popup button, and the mouse moving the choice like the arrow keys.
        public void ConfigureConfirmChoice(GameObject configuredBuyMark, GameObject configuredCancelMark,
            ShopHoverTarget configuredBuyHover, ShopHoverTarget configuredCancelHover)
        {
            Unsubscribe();
            buyFocusMark = configuredBuyMark;
            cancelFocusMark = configuredCancelMark;
            buyHover = configuredBuyHover;
            cancelHover = configuredCancelHover;
            Subscribe();
            ApplyConfirmChoice();
        }

        public GameObject BuyFocusMark => buyFocusMark;
        public GameObject InvestFocusMark => investFocusMark;
        public GameObject CloseFocusMark => closeFocusMark;

        // The rings around 투자 and 닫기 when the keyboard focus moves below the grid.
        public void ConfigureActionFocus(GameObject configuredInvestMark, GameObject configuredCloseMark)
        {
            investFocusMark = configuredInvestMark;
            closeFocusMark = configuredCloseMark;
            ApplyActionFocus();
        }

        private void ApplyActionFocus()
        {
            bool shown = IsVisible && !IsInvesting && !IsConfirming;
            if (investFocusMark != null) investFocusMark.SetActive(shown && FocusedAction == ShopFocusAction.Invest);
            if (closeFocusMark != null) closeFocusMark.SetActive(shown && FocusedAction == ShopFocusAction.Close);
        }
        public GameObject CancelFocusMark => cancelFocusMark;

        private void OnBuyHovered(ShopHoverTarget _)
        {
            if (IsConfirming) SetConfirmChoice(true);
        }

        private void OnCancelHovered(ShopHoverTarget _)
        {
            if (IsConfirming) SetConfirmChoice(false);
        }

        // Opens the purchase popup for the focused offer or upgrade when the player can take it now.
        public bool OpenConfirm(bool byKeyboard)
        {
            if (!usesArtwork || !IsVisible || IsConfirming || !CanConfirmFocused()) return false;
            SetConfirming(true, byKeyboard);
            return true;
        }

        public void CancelConfirm() => SetConfirming(false, false);

        // Buys the offer (or the upgrade) shown in the popup and closes the popup.
        public bool ConfirmFocused()
        {
            if (!IsConfirming) return false;
            bool bought = IsInvesting
                ? TryInvestFocused() == ShopUpgradeStatus.Purchased
                : TryPurchaseFocused() == ShopPurchaseResult.Purchased;
            SetConfirming(false, false);
            return bought;
        }

        private bool CanConfirmFocused()
        {
            ShopRoom shop = session != null ? session.Shop : null;
            if (shop == null || shop.Stock == null) return false;
            if (IsInvesting)
                return InvestFocusedIndex < ShopUpgrades.Catalog.Length && shop.GetUpgradeStatus(
                    ShopUpgrades.Catalog[InvestFocusedIndex].UpgradeId) == ShopUpgradeStatus.Available;
            return FocusedOffer != null &&
                   shop.GetStatus(FocusedIndex, session.Inventory) is ShopOfferStatus.Available or ShopOfferStatus.Free;
        }

        private void SetConfirming(bool confirming, bool byKeyboard)
        {
            IsConfirming = confirming && confirmPopup != null;
            confirmByKeyboard = IsConfirming && byKeyboard;
            ConfirmChoosesBuy = true;
            ApplyConfirmChoice();
            if (confirmPopup != null)
            {
                confirmPopup.alpha = IsConfirming ? 1f : 0f;
                confirmPopup.interactable = IsConfirming;
                confirmPopup.blocksRaycasts = IsConfirming;
            }
            if (IsVisible) Refresh();
            else ApplyTooltip();
        }

        private void ApplyTooltip()
        {
            if (tooltipGroup == null) return;
            bool show = IsConfirming && !IsInvesting &&
                        (confirmByKeyboard || (confirmIconHover != null && confirmIconHover.IsHovered));
            tooltipGroup.alpha = show ? 1f : 0f;
            tooltipGroup.blocksRaycasts = false;
            tooltipGroup.interactable = false;
        }

        // Space kept below the effect text, inside the tooltip's bottom frame.
        public const float TooltipBottomPadding = 30f;

        // A long effect runs to several lines: the tooltip grows downward from its top edge, keeping the bottom
        // padding below the last line. The effect text hangs from the tooltip's top edge.
        private void FitTooltip()
        {
            if (tooltip == null || detailEffectText == null || tooltipBaseHeight <= 0f) return;
            RectTransform effect = detailEffectText.rectTransform;
            float preferred = detailEffectText.GetPreferredValues(detailEffectText.text, effect.rect.width, 0f).y;
            float needed = -effect.anchoredPosition.y + preferred + TooltipBottomPadding;
            tooltip.sizeDelta = new Vector2(tooltip.sizeDelta.x, Mathf.Max(tooltipBaseHeight, needed));
            effect.sizeDelta = new Vector2(effect.sizeDelta.x, Mathf.Max(tooltipEffectBaseHeight, preferred));
        }

        public bool CanInvest => session != null && session.Shop != null && session.Shop.SellsUpgrades &&
                                 investPanel != null && investRows.Length > 0;

        public bool ToggleInvesting() => !IsConfirming && SetInvesting(!IsInvesting);

        // Opens or closes the invest list. Only the general shop (시스트) has it.
        public bool SetInvesting(bool investing)
        {
            bool next = investing && IsVisible && CanInvest;
            bool changed = next != IsInvesting;
            if (changed && IsConfirming) SetConfirming(false, false);
            if (changed) FocusedAction = ShopFocusAction.None;
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
            if (!IsVisible || IsConfirming) return;
            int rowCount = Math.Min(investRows.Length, ShopUpgrades.Catalog.Length);
            if (IsInvesting)
            {
                if (dy == 0 || rowCount == 0) return;
                SetInvestFocus(usesArtwork
                    ? (InvestFocusedIndex + dy + rowCount) % rowCount
                    : Mathf.Clamp(InvestFocusedIndex + dy, 0, rowCount - 1));
                return;
            }
            if (usesArtwork)
            {
                MoveArtworkFocus(dx, dy);
                return;
            }
            int columns = ShopCatalog.GridColumns;
            int column = Mathf.Clamp(FocusedIndex % columns + dx, 0, columns - 1);
            int row = Mathf.Clamp(FocusedIndex / columns + dy, 0, ShopCatalog.GridSlotCount / columns - 1);
            SetFocus(row * columns + column);
        }

        // Shop UI artwork: the focus wraps around its row and its column. Below the grid the 투자 button sits under
        // the second column and the 닫기 button under the third, so those columns reach them too.
        private void MoveArtworkFocus(int dx, int dy)
        {
            int columns = ShopCatalog.GridColumns;
            int rows = ShopCatalog.GridSlotCount / columns;
            bool investShown = CanInvest && investButton != null && investButton.gameObject.activeSelf;
            if (FocusedAction != ShopFocusAction.None)
            {
                if (dx != 0)
                {
                    // The button row holds 투자 (when shown) and 닫기.
                    if (investShown)
                        SetFocusedAction(FocusedAction == ShopFocusAction.Invest
                            ? ShopFocusAction.Close
                            : ShopFocusAction.Invest);
                    return;
                }
                if (dy == 0) return;
                int actionColumn = ActionColumn(FocusedAction);
                SetFocus((dy > 0 ? 0 : rows - 1) * columns + actionColumn);
                return;
            }

            int column = FocusedIndex % columns;
            int row = FocusedIndex / columns;
            if (dx != 0)
            {
                SetFocus(row * columns + (column + dx + columns) % columns);
                return;
            }
            if (dy == 0) return;
            ShopFocusAction below = column == ActionColumn(ShopFocusAction.Close) ? ShopFocusAction.Close
                : column == ActionColumn(ShopFocusAction.Invest) && investShown ? ShopFocusAction.Invest
                : ShopFocusAction.None;
            int next = row + dy;
            if (below != ShopFocusAction.None && (next == rows || next < 0))
            {
                SetFocusedAction(below);
                return;
            }
            SetFocus(((next % rows) + rows) % rows * columns + column);
        }

        private static int ActionColumn(ShopFocusAction action) =>
            action == ShopFocusAction.Invest ? 1 : ShopCatalog.GridColumns - 1;

        // The 투자 or 닫기 button chosen with the keyboard; None while a cell has the focus.
        public ShopFocusAction FocusedAction { get; private set; }

        public void SetFocusedAction(ShopFocusAction action)
        {
            if (!IsVisible || IsInvesting || IsConfirming) return;
            FocusedAction = action;
            Refresh();
        }

        public void SetFocus(int cellIndex)
        {
            if (!IsVisible || cellIndex < 0 || cellIndex >= ShopCatalog.GridSlotCount) return;
            if (cellIndex != FocusedIndex) SetFeedback(string.Empty);
            FocusedIndex = cellIndex;
            FocusedAction = ShopFocusAction.None;
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
                    index == FocusedIndex && !IsInvesting && FocusedAction == ShopFocusAction.None);
            }

            if (goldText != null && shop.Progress != null)
                goldText.text = FormatGold(shop.Progress.GetResourceCount(RunResourceType.Gold));
            if (elifText != null && shop.Progress != null)
                elifText.text = FormatElif(shop.Progress.AvailableElif, shop.Progress.IsElifSpendingAllowed);
            if (investButton != null) investButton.gameObject.SetActive(CanInvest);
            if (investButtonLabel != null) investButtonLabel.text = IsInvesting ? InvestBackLabel : InvestLabel;
            if (IsInvesting) RefreshInvest(shop);
            else RefreshDetail(shop, FocusedOffer, shop.GetStatus(FocusedIndex, session.Inventory));
            ApplyTooltip();
            ApplyActionFocus();
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
            if (!usesArtwork) return;
            bool maxLevel = level >= focused.MaxLevel;
            SetText(confirmTitleText, InvestTitle);
            SetText(confirmNoteText, FormatUpgradeNext(focused, level));
            SetText(detailPriceText, maxLevel ? MaxLevelLabel : focused.GetCost(level + 1).ToString());
            SetPicture(confirmCurrencyIcon, maxLevel ? null : elifSprite);
            if (confirmIcon != null) confirmIcon.enabled = false;
            if (confirmIconGlow != null) confirmIconGlow.SetActive(false);
        }

        private static void SetPicture(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
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
            SetText(confirmTitleText, PurchaseTitle);
            if (offer == null)
            {
                SetText(detailNameText, string.Empty);
                SetText(detailKindText, string.Empty);
                SetText(detailEffectText, EmptyCellMessage);
                SetText(detailPriceText, string.Empty);
                SetText(tooltipNameText, string.Empty);
                SetText(confirmNoteText, string.Empty);
                SetPicture(confirmCurrencyIcon, null);
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
                SetText(detailEffectText, DescribeConsumable(offer.Consumable.ConsumableId));
            }

            SetText(detailPriceText, FormatPriceLabel(status, shop.GetPrice(offer), offer.IsElifPriced));
            if (usesArtwork) RefreshPopup(shop, offer, status);
        }

        private void RefreshPopup(ShopRoom shop, ShopOffer offer, ShopOfferStatus status)
        {
            int price = shop.GetPrice(offer);
            SetText(tooltipNameText, offer.DisplayName);
            SetText(detailPriceText, FormatCompactPriceLabel(status, price));
            SetPicture(confirmCurrencyIcon,
                ShowsCurrencyIcon(status, price) ? offer.IsElifPriced ? elifSprite : goldSprite : null);
            if (offer.Kind == ShopOfferKind.Item)
            {
                ItemDefinition item = offer.Item;
                string maximum = item.MaxStacks > 0 ? item.MaxStacks.ToString() : "제한 없음";
                int stacks = session.Inventory != null ? session.Inventory.GetStackCount(item.ItemId) : 0;
                SetText(confirmNoteText, item.IsActivePower
                    ? GamePowerHudView.BuildOfferLine(item)
                    : $"현재 스택 {stacks} / {maximum}");
                SetText(detailEffectText, ArtifactEffectDescription.Build(item));
                // The tooltip's kind line names the rarity in Korean only, like the reward cards.
                SetText(detailKindText, $"{ItemKindText.GetDisplayName(item.Kind)}  ·  {KoreanRarityLabel(item.Rarity)}");
            }
            else
            {
                SetText(confirmNoteText, string.Empty);
            }

            // The popup shows the same picture as the focused cell.
            Image source = FocusedIndex < cells.Length && cells[FocusedIndex] != null ? cells[FocusedIndex].Icon : null;
            if (confirmIcon != null)
            {
                confirmIcon.enabled = source != null && source.sprite != null;
                if (source != null)
                {
                    confirmIcon.sprite = source.sprite;
                    confirmIcon.material = source.material;
                    confirmIcon.color = source.color;
                }
                confirmIcon.preserveAspect = true;
            }
            if (confirmIconGlow != null) confirmIconGlow.SetActive(true);
            FitTooltip();
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
            FocusedAction = ShopFocusAction.None;
            ApplyTheme(session.Shop != null ? session.Shop.Kind : ShopKind.General);
            SetFeedback(string.Empty);
            ApplyVisibility(true);
            SetInvesting(false);
            SetConfirming(false, false);
            Refresh();
        }

        private void OnSessionClosed()
        {
            SetConfirming(false, false);
            SetInvesting(false);
            ApplyVisibility(false);
            ApplyActionFocus();
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
            if (cell == null || IsInvesting || IsConfirming) return;
            SetFocus(cell.CellIndex);
            if (usesArtwork) Submit(false);
        }

        private void OnCellHovered(ShopOfferCellView cell)
        {
            if (cell != null && !IsInvesting && !IsConfirming) SetFocus(cell.CellIndex);
        }

        private void OnInvestRowHovered(ShopUpgradeRowView row)
        {
            if (row != null && !IsConfirming) SetInvestFocus(row.RowIndex);
        }

        private void OnIconHoverChanged(ShopHoverTarget _) => ApplyTooltip();

        private void OnCancelClicked()
        {
            ClearSelection();
            CancelConfirm();
        }

        private void OnBuyClicked()
        {
            ClearSelection();
            if (usesArtwork)
            {
                ConfirmFocused();
                return;
            }
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
            if (row == null || IsConfirming) return;
            SetInvestFocus(row.RowIndex);
            if (usesArtwork) Submit(false);
        }

        private void OnCloseClicked()
        {
            ClearSelection();
            if (IsConfirming) CancelConfirm();
            else Close();
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
            {
                if (cells[index] == null) continue;
                cells[index].Clicked += OnCellClicked;
                cells[index].Hovered += OnCellHovered;
            }
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (investButton != null) investButton.onClick.AddListener(OnInvestClicked);
            foreach (ShopUpgradeRowView row in investRows)
            {
                if (row == null) continue;
                row.Clicked += OnInvestRowClicked;
                row.Hovered += OnInvestRowHovered;
            }
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            if (dismissButton != null) dismissButton.onClick.AddListener(OnCancelClicked);
            if (confirmIconHover != null)
            {
                confirmIconHover.Entered += OnIconHoverChanged;
                confirmIconHover.Exited += OnIconHoverChanged;
            }
            if (buyHover != null) buyHover.Entered += OnBuyHovered;
            if (cancelHover != null) cancelHover.Entered += OnCancelHovered;
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
            {
                if (cells[index] == null) continue;
                cells[index].Clicked -= OnCellClicked;
                cells[index].Hovered -= OnCellHovered;
            }
            if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
            if (investButton != null) investButton.onClick.RemoveListener(OnInvestClicked);
            foreach (ShopUpgradeRowView row in investRows)
            {
                if (row == null) continue;
                row.Clicked -= OnInvestRowClicked;
                row.Hovered -= OnInvestRowHovered;
            }
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (dismissButton != null) dismissButton.onClick.RemoveListener(OnCancelClicked);
            if (confirmIconHover != null)
            {
                confirmIconHover.Entered -= OnIconHoverChanged;
                confirmIconHover.Exited -= OnIconHoverChanged;
            }
            if (buyHover != null) buyHover.Entered -= OnBuyHovered;
            if (cancelHover != null) cancelHover.Entered -= OnCancelHovered;
            subscribed = false;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }

    // Shop UI artwork: the button below the grid that holds the keyboard focus.
    public enum ShopFocusAction
    {
        None,
        Invest,
        Close,
    }
}
