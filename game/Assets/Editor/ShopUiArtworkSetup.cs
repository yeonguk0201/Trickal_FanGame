using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;
using Box = TrickalFanGame.Editor.ShopUiArtworkLayout.Box;

namespace TrickalFanGame.Editor
{
    // Shop UI artwork (output/art/shop-ui-v1, layout editor shop-ui-v2): skins the Shop-0/Elif-3 shop screen with
    // the clover shop artwork and adds the purchase popup. Positions come from ShopUiArtworkLayout, which the layout
    // editor generates. Re-running updates the same objects; Shop-0 and Elif-3 setups re-apply it when the screen
    // already uses the artwork, so their re-runs keep it.
    public static class ShopUiArtworkSetup
    {
        public const string Folder = "Assets/Art/UI/Shop/";
        public const string PopupName = "Purchase Popup";
        public const string TooltipName = "Item Tooltip";
        public const string SelectedFrameName = "Selected Frame";
        public static readonly Color OverlayColor = new(0.04f, 0.08f, 0.03f, 0.62f);
        public static readonly Color PopupDimColor = new(1f, 0.965f, 0.933f, 0.5f);
        public static readonly Color PaperColor = new(0.984f, 0.953f, 0.902f, 1f);
        public static readonly Color PaperEdgeColor = new(0.925f, 0.847f, 0.749f, 1f);
        public static readonly Color TextColor = new(0.29f, 0.2f, 0.2f, 1f);
        public static readonly Color SoftTextColor = new(0.55f, 0.48f, 0.44f, 1f);
        public static readonly Color HintColor = new(0.63f, 0.54f, 0.48f, 1f);
        public static readonly Color DividerColor = new(0.79f, 0.86f, 0.66f, 1f);
        public static readonly Color ButtonInk = new(0.36f, 0.23f, 0.17f, 1f);
        // The same yellow as the selected item cell, for 투자 and 닫기 chosen with the keyboard.
        public static readonly Color SelectedRingColor = new(1f, 0.84f, 0.3f, 1f);

        // The base cell and row; the first cell and row of the layout editor show the selected frame.
        private const string CellBox = "slot2-frame";
        private const string SelectedCellBox = "slot1-frame";
        private const string RowBox = "invest-row2-frame";
        private const string SelectedRowBox = "invest-row1-frame";

        [MenuItem("Trickal Fan Game/UI/Apply Clover Shop UI")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying the shop artwork.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ShopView[] views = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ShopView>(true)).ToArray();
            if (views.Length != 1)
                throw new InvalidOperationException("Run Shop-0 setup first: the Game Scene needs one shop screen.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Clover Shop UI");
            Apply(views[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the shop UI.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Clover Shop UI applied: the shop screen uses the clover artwork and opens a purchase popup.");
        }

        // The selected first cell's frame is larger but shares its center with the plain cell.
        public static Vector2 CellPosition(int index) => Relative(LayoutBox($"slot{index + 1}-frame"), "shop-panel");

        public static Vector2 CellSize => LayoutBox(CellBox).Size;

        // Skins the shop screen in the open scene. The caller saves the scene.
        public static void Apply(ShopView view)
        {
            Transform overlay = view.transform;
            var panel = overlay.Find("Shop Panel") as RectTransform;
            if (panel == null) throw new InvalidOperationException("The shop screen needs its Shop Panel.");
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Apply Clover Shop UI");

            Sprite gold = Import("gold");
            Sprite elif = Import("elif");
            Sprite star = Import("shop-star");

            Image dimmer = overlay.GetComponent<Image>();
            if (dimmer != null) dimmer.color = OverlayColor;

            Place(panel, Relative(LayoutBox("shop-panel"), null), LayoutBox("shop-panel").Size);
            Image panelImage = panel.GetComponent<Image>();
            SkinArt(panelImage, "shop-panel", "shop-panel");
            if (panel.TryGetComponent(out Outline outline)) outline.enabled = false;

            Art(panel, "Keeper Backdrop", "sist-backdrop", "sist-backdrop");
            Image portrait = view.KeeperPortrait;
            Place(portrait.rectTransform, Rel("sist"), LayoutBox("sist").Size);
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            Art(panel, "Keeper Foreground", "sist-foreground", "sist-foreground");
            Art(panel, "Header Artwork", "shop-header", "shop-header");
            Picture(panel, "Title Star Left", star, "shop-title-star-l");
            Picture(panel, "Title Star Right", star, "shop-title-star-r");
            Style(view.TitleText, "shop-title", 44f, ShopView.ArtworkTitleColor);

            RectTransform grid = Node(panel, "Grid Area", Rel("grid-area"), LayoutBox("grid-area").Size);
            Skin(grid.gameObject, MenuArtSetup.RoundedFill, PaperColor);
            GameObject ring = Ring(grid, "Grid Area Edge", MenuArtSetup.RingRounded, PaperEdgeColor, 2f);
            ring.SetActive(true);

            Art(panel, "Gold Pill", "price-pill", "gold-pill");
            Picture(panel, "Gold Icon", gold, "gold-icon");
            Style(view.GoldText, "gold-amount", 26f, TextColor);
            TMP_Text elifText = view.ElifText;
            if (elifText != null)
            {
                Art(panel, "Elif Pill", "price-pill", "elif-pill");
                Picture(panel, "Elif Icon", elif, "elif-icon");
                Style(elifText, "elif-amount", 26f, TextColor);
            }

            Art(panel, "Keeper Bubble", "sist-dialogue", "sist-dialogue");
            Style(view.KeeperLineText, "sist-dialogue-text", 25f, TextColor, wrap: true);
            if (view.KeeperNameText != null) view.KeeperNameText.gameObject.SetActive(false);

            for (int index = 0; index < view.Cells.Length; index++)
                ApplyCell(view.Cells[index], index, gold, elif, star);
            ApplyInvest(view, panel, elif);

            if (view.FeedbackText != null) view.FeedbackText.gameObject.SetActive(false);
            TMP_Text hint = panel.Find("Input Hint")?.GetComponent<TMP_Text>();
            if (hint != null) Style(hint, "shop-hint", 18f, HintColor);
            foreach (string old in new[] { "Detail Name", "Detail Kind", "Detail Divider", "Detail Effect",
                         "Detail Price", "Buy" })
                Deactivate(panel, old);

            if (view.InvestButton != null)
            {
                SkinButton(view.InvestButton, "button-invest", "invest-button");
                Style(view.InvestButtonLabel, "invest-label", 36f, Color.white, parent: "invest-button");
                view.InvestButtonLabel.text = ShopView.InvestLabel;
            }
            SkinButton(view.CloseButton, "button-close", "close-button");
            // The keyboard focus reaches 투자 and 닫기 below the grid. Like the reward screen's buttons, the chosen
            // one (and the one under the mouse) grows a little instead of wearing a ring.
            GrowOnFocus(view.CloseButton);
            if (view.InvestButton != null) GrowOnFocus(view.InvestButton);
            Style(view.CloseButton.transform.Find("Label").GetComponent<TMP_Text>(), "close-label", 36f, Color.white,
                parent: "close-button");

            Order(panel, "Keeper Backdrop", "Keeper Portrait", "Keeper Foreground", "Header Artwork", "Title Star Left",
                "Title Star Right", "Title", "Grid Area", "Gold Pill", "Gold Icon", "Gold", "Elif Pill", "Elif Icon",
                Week27Elif3Setup.ElifTextName, "Keeper Bubble", "Keeper Line");
            foreach (ShopOfferCellView cell in view.Cells) cell.transform.SetAsLastSibling();
            foreach (string front in new[] { Week27Elif3Setup.InvestPanelName, "Input Hint",
                         Week27Elif3Setup.InvestButtonName, "Close" })
                panel.Find(front)?.SetAsLastSibling();

            ApplyPopup(view, star, gold, elif);
            view.ConfigureActionFocus(null, null);
            EnsureGlyphs();

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                if (component != null) EditorUtility.SetDirty(component);
        }

        private static void ApplyCell(ShopOfferCellView cell, int index, Sprite gold, Sprite elif, Sprite star)
        {
            var root = (RectTransform)cell.transform;
            Place(root, CellPosition(index), CellSize);
            Image background = root.GetComponent<Image>();
            SkinArt(background, "item-slot", CellBox);
            background.raycastTarget = true;
            cell.Button.transition = Selectable.Transition.None;
            if (root.TryGetComponent(out Outline focus)) focus.enabled = false;

            RectTransform selectedRect = Node(root, SelectedFrameName, Vector2.zero, LayoutBox(SelectedCellBox).Size);
            Image selected = Ensure<Image>(selectedRect.gameObject);
            SkinArt(selected, "item-slot-selected", SelectedCellBox);
            selected.raycastTarget = false;
            selected.enabled = false;
            selectedRect.SetAsFirstSibling();

            var content = (RectTransform)cell.Content.transform;
            Place(content, Vector2.zero, CellSize);
            content.SetAsLastSibling();
            Style(cell.NameText, "slot2-name", 24f, TextColor, parent: CellBox);
            cell.NameText.textWrappingMode = TextWrappingModes.NoWrap;
            Picture(content, "Star Left", star, "slot2-star-l", CellBox);
            Picture(content, "Star Right", star, "slot2-star-r", CellBox);
            Box iconBox = LayoutBox("slot2-icon");
            float side = Mathf.Max(iconBox.Width, iconBox.Height);
            var iconRect = cell.Icon.rectTransform;
            Place(iconRect, Relative(iconBox, CellBox), new Vector2(side, side));
            cell.Icon.preserveAspect = true;
            Place(cell.IconText.rectTransform, Vector2.zero, new Vector2(side, 40f));
            Art(content, "Price Pill", "price-pill", "slot2-price-pill", CellBox);
            Image currency = Picture(content, "Currency Icon", gold, "slot2-coin", CellBox);
            Style(cell.PriceText, "slot2-price", 24f, TextColor, TextAlignmentOptions.Left, parent: CellBox);
            Order(content, "Star Left", "Star Right", "Name", "Icon", "Price Pill", "Currency Icon", "Price");

            ShopHoverTarget hover = Ensure<ShopHoverTarget>(root.gameObject);
            cell.ConfigureVisuals(index, cell.Button, cell.Content, selected, cell.Icon, cell.IconText, cell.NameText,
                cell.PriceText);
            cell.ConfigureArtwork(background, currency, gold, elif, hover);
        }

        private static void ApplyInvest(ShopView view, RectTransform panel, Sprite elif)
        {
            if (view.InvestPanel == null) return;
            var investRect = (RectTransform)view.InvestPanel.transform;
            Place(investRect, Rel("invest-cover"), LayoutBox("invest-cover").Size);
            Image cover = investRect.GetComponent<Image>();
            cover.sprite = MenuArtSetup.Load(MenuArtSetup.RoundedFill);
            cover.type = Image.Type.Sliced;
            cover.color = PaperColor;
            cover.pixelsPerUnitMultiplier = 1f;
            if (investRect.TryGetComponent(out Outline outline)) outline.enabled = false;
            Deactivate(investRect, "Invest Title");

            for (int index = 0; index < view.InvestRows.Length; index++)
            {
                ShopUpgradeRowView row = view.InvestRows[index];
                if (row == null) continue;
                var root = (RectTransform)row.transform;
                string box = index == 0 ? SelectedRowBox : $"invest-row{index + 1}-frame";
                Vector2 center = Relative(LayoutBox(box), "invest-cover");
                Place(root, center, LayoutBox(RowBox).Size);
                Image background = root.GetComponent<Image>();
                SkinArt(background, "item-slot", RowBox);
                background.raycastTarget = true;
                row.Button.transition = Selectable.Transition.None;
                if (root.TryGetComponent(out Outline focus)) focus.enabled = false;

                RectTransform selectedRect = Node(root, SelectedFrameName, Vector2.zero, LayoutBox(SelectedRowBox).Size);
                Image selected = Ensure<Image>(selectedRect.gameObject);
                SkinArt(selected, "item-slot-selected", SelectedRowBox);
                selected.raycastTarget = false;
                selected.enabled = false;
                selectedRect.SetAsFirstSibling();

                Style(row.NameText, "invest-row2-name", 32f, TextColor, TextAlignmentOptions.Left, parent: RowBox);
                Style(row.LevelText, "invest-row2-level", 24f, SoftTextColor, TextAlignmentOptions.Left,
                    parent: RowBox);
                Style(row.EffectText, "invest-row2-next", 24f, TextColor, TextAlignmentOptions.Left, parent: RowBox);
                Art(root, "Cost Pill", "price-pill", "invest-row2-cost-pill", RowBox);
                Image costIcon = Picture(root, "Cost Icon", elif, "invest-row2-cost-icon", RowBox);
                Style(row.CostText, "invest-row2-cost", 28f, TextColor, parent: RowBox);
                Order(root, SelectedFrameName, "Name", "Level", "Effect", "Cost Pill", "Cost Icon", "Cost");

                ShopHoverTarget hover = Ensure<ShopHoverTarget>(root.gameObject);
                row.ConfigureVisuals(index, row.Button, selected, row.NameText, row.LevelText, row.EffectText,
                    row.CostText);
                row.ConfigureArtwork(costIcon, hover);
            }

            if (view.InvestNoticeText != null)
                Style(view.InvestNoticeText, "invest-notice", 20f, SoftTextColor, wrap: true, parent: "invest-cover");
        }

        private static void ApplyPopup(ShopView view, Sprite star, Sprite gold, Sprite elif)
        {
            Transform overlay = view.transform;
            RectTransform popup = Node(overlay, PopupName, Vector2.zero, Vector2.zero);
            Stretch(popup);
            popup.SetAsLastSibling();
            Image dim = Ensure<Image>(popup.gameObject);
            dim.sprite = null;
            dim.color = PopupDimColor;
            dim.raycastTarget = true;
            CanvasGroup group = Ensure<CanvasGroup>(popup.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Art(popup, "Popup Panel", "purchase-panel", "purchase-panel", null);
            Art(popup, "Popup Content", "purchase-content", "purchase-content", null);
            Art(popup, "Popup Header", "purchase-header", "purchase-header", null);
            TMP_Text title = TextNode(popup, "Popup Title", ShopView.PurchaseTitle, "purchase-title", 42f, Color.white);
            Picture(popup, "Title Star Left", star, "purchase-title-star-l", null);
            Picture(popup, "Title Star Right", star, "purchase-title-star-r", null);
            Button dismiss = PopupButton(popup, "Dismiss", "button-dismiss", "purchase-dismiss");

            Image glow = Picture(popup, "Item Glow", Import("shop-glow"), "purchase-glow", null);
            glow.preserveAspect = false;
            Image picture = Picture(popup, "Item Picture", null, "purchase-icon", null);
            picture.raycastTarget = true;
            ShopHoverTarget pictureHover = Ensure<ShopHoverTarget>(picture.gameObject);
            TMP_Text name = TextNode(popup, "Item Name", "상품 이름", "purchase-name", 40f, TextColor);
            Picture(popup, "Name Star Left", star, "purchase-name-star-l", null);
            Picture(popup, "Name Star Right", star, "purchase-name-star-r", null);
            TMP_Text note = TextNode(popup, "Item Note", string.Empty, "purchase-owned", 24f, SoftTextColor);
            Divider(popup, "Divider Left", "purchase-divider-l");
            Divider(popup, "Divider Right", "purchase-divider-r");
            RectTransform clover = Node(popup, "Divider Clover", Rel("purchase-divider-clover", null),
                LayoutBox("purchase-divider-clover").Size);
            Skin(clover.gameObject, MenuArtSetup.Clover, Color.white, preserveAspect: true);
            Art(popup, "Price Pill", "price-pill", "purchase-price-pill", null);
            Image currency = Picture(popup, "Price Currency", gold, "purchase-coin", null);
            TMP_Text price = TextNode(popup, "Price", "10", "purchase-price", 36f, TextColor, TextAlignmentOptions.Left);
            Picture(popup, "Price Star Left", star, "purchase-price-star-l", null);
            Picture(popup, "Price Star Right", star, "purchase-price-star-r", null);

            Button cancel = PopupButton(popup, "Cancel", "button-cancel", "cancel-button");
            LabelOf(cancel, ShopView.CancelLabel, "cancel-label", "cancel-button");
            Button buy = PopupButton(popup, "Buy", "button-buy", "buy-button");
            TMP_Text buyLabel = LabelOf(buy, ShopView.BuyLabel, "buy-label", "buy-button");
            // Arrow keys (or the mouse) choose between the two buttons; a plum ring marks the one Enter presses.
            GameObject cancelRing = ButtonRing(cancel, "button-cancel", "cancel-button", FocusRing);
            GameObject buyRing = ButtonRing(buy, "button-buy", "buy-button", FocusRing);
            ShopHoverTarget cancelHover = Ensure<ShopHoverTarget>(cancel.gameObject);
            ShopHoverTarget buyHover = Ensure<ShopHoverTarget>(buy.gameObject);

            // The tooltip grows downward from its top edge, so it and its texts hang from the top.
            Box tipBox = LayoutBox("tooltip");
            RectTransform tip = Node(popup, TooltipName, Vector2.zero, tipBox.Size);
            tip.pivot = new Vector2(0.5f, 1f);
            tip.anchoredPosition = new Vector2(tipBox.Center.x - ShopUiArtworkLayout.CanvasWidth / 2f,
                ShopUiArtworkLayout.CanvasHeight / 2f - tipBox.Y);
            SkinArt(Ensure<Image>(tip.gameObject), "item-tooltip", "tooltip");
            CanvasGroup tipGroup = Ensure<CanvasGroup>(tip.gameObject);
            tipGroup.alpha = 0f;
            tipGroup.interactable = false;
            tipGroup.blocksRaycasts = false;
            TMP_Text tipName = TopText(tip, "Tooltip Name", "tooltip-title", 27f, TextColor);
            TopPicture(tip, "Tooltip Star Left", star, "tooltip-star-l");
            TopPicture(tip, "Tooltip Star Right", star, "tooltip-star-r");
            TMP_Text tipKind = TopText(tip, "Tooltip Kind", "tooltip-kind", 20f, SoftTextColor);
            TMP_Text tipEffect = TopText(tip, "Tooltip Effect", "tooltip-effect", 21f, TextColor);
            tipEffect.textWrappingMode = TextWrappingModes.Normal;
            tipEffect.enableAutoSizing = false;
            tipEffect.alignment = TextAlignmentOptions.Top;
            tip.SetAsLastSibling();

            view.Configure(view.Session, view.Overlay, view.GraphicRaycaster, view.UiEventSystem, view.KeeperPortrait,
                view.GoldText, view.Cells, name, tipKind, tipEffect, price, view.FeedbackText, buy, buyLabel,
                view.CloseButton);
            TMP_Text hint = view.transform.Find("Shop Panel/Input Hint")?.GetComponent<TMP_Text>();
            // The artwork first: the theme then leaves the panel artwork untinted.
            view.ConfigureArtwork(group, title, picture, glow.gameObject, pictureHover, note, currency, cancel,
                dismiss, tip, tipGroup, tipName, hint, gold, elif);
            view.ConfigureConfirmChoice(buyRing, cancelRing, buyHover, cancelHover);
            view.ConfigureTheme(view.TitleText, view.KeeperNameText, view.KeeperLineText, view.PanelImage,
                view.PanelOutline);
            view.PanelImage.color = Color.white;
            if (view.PanelOutline != null) view.PanelOutline.enabled = false;
        }

        // ---- layout helpers -------------------------------------------------------------------------------------

        private static Box LayoutBox(string id) =>
            ShopUiArtworkLayout.Boxes.TryGetValue(id, out Box box)
                ? box
                : throw new InvalidOperationException($"ShopUiArtworkLayout has no box {id}.");

        // Anchored position of a layout box inside the parent box (null: the 1920×1080 overlay).
        private static Vector2 Relative(Box box, string parent)
        {
            Vector2 origin = parent == null
                ? new Vector2(ShopUiArtworkLayout.CanvasWidth / 2f, ShopUiArtworkLayout.CanvasHeight / 2f)
                : LayoutBox(parent).Center;
            return new Vector2(box.Center.x - origin.x, origin.y - box.Center.y);
        }

        private static Vector2 Rel(string id, string parent = "shop-panel") => Relative(LayoutBox(id), parent);

        private static void SkinArt(Image image, string sprite, string box)
        {
            Undo.RecordObject(image, "Skin " + image.name);
            image.sprite = Import(sprite);
            image.color = Color.white;
            image.material = null;
            float multiplier = LayoutBox(box).SliceMultiplier;
            image.type = multiplier > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = multiplier > 0f ? multiplier : 1f;
            image.fillCenter = true;
            image.preserveAspect = false;
        }

        private static Image Art(Transform parent, string name, string sprite, string box, string parentBox = "shop-panel")
        {
            RectTransform rect = Node(parent, name, Relative(LayoutBox(box), parentBox), LayoutBox(box).Size);
            Image image = Ensure<Image>(rect.gameObject);
            SkinArt(image, sprite, box);
            image.raycastTarget = false;
            return image;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, string box,
            string parentBox = "shop-panel")
        {
            RectTransform rect = Node(parent, name, Relative(LayoutBox(box), parentBox), LayoutBox(box).Size);
            Image image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Divider(Transform parent, string name, string box)
        {
            RectTransform rect = Node(parent, name, Relative(LayoutBox(box), null), LayoutBox(box).Size);
            Image image = Ensure<Image>(rect.gameObject);
            image.sprite = null;
            image.color = DividerColor;
            image.raycastTarget = false;
        }

        private static void Style(TMP_Text text, string box, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool wrap = false,
            string parent = "shop-panel")
        {
            if (text == null) return;
            Place(text.rectTransform, Relative(LayoutBox(box), parent), LayoutBox(box).Size);
            StyleText(text, size, color, alignment);
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(16f, size);
            text.fontSizeMax = size;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.gameObject.SetActive(true);
        }

        private static TMP_Text TextNode(Transform parent, string name, string value, string box, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TMP_Text text = Ensure<TextMeshProUGUI>(Node(parent, name, Vector2.zero, Vector2.zero).gameObject);
            Style(text, box, size, color, alignment, parent: null);
            text.text = value;
            return text;
        }

        // Children of the tooltip hang from its top edge so the tooltip can grow downward.
        private static RectTransform TopNode(RectTransform tip, string name, string box)
        {
            Box tipBox = LayoutBox("tooltip");
            Box child = LayoutBox(box);
            RectTransform rect = Node(tip, name, Vector2.zero, child.Size);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(child.Center.x - tipBox.Center.x, tipBox.Y - child.Y);
            return rect;
        }

        private static TMP_Text TopText(RectTransform tip, string name, string box, float size, Color color)
        {
            TMP_Text text = Ensure<TextMeshProUGUI>(TopNode(tip, name, box).gameObject);
            StyleText(text, size, color);
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void TopPicture(RectTransform tip, string name, Sprite sprite, string box)
        {
            Image image = Ensure<Image>(TopNode(tip, name, box).gameObject);
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void SkinButton(Button button, string sprite, string box)
        {
            var rect = (RectTransform)button.transform;
            Place(rect, Rel(box), LayoutBox(box).Size);
            Image image = button.GetComponent<Image>();
            SkinArt(image, sprite, box);
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors();
        }

        public const float FocusedButtonScale = 1.1f;

        // Replaces an older focus ring with the menu grow, keeping the shop's own hover and press tint.
        private static void GrowOnFocus(Button button)
        {
            Transform ring = button.transform.Find("Focus Ring");
            if (ring != null) Undo.DestroyObjectImmediate(ring.gameObject);
            Highlight(button, null, FocusedButtonScale);
            Undo.RecordObject(button, "Colors " + button.name);
            button.colors = ButtonColors();
        }

        // A selection ring cut from the button's own silhouette (export_unity.py), sliced like the button so its
        // corners follow the button's. Hidden until the view shows it.
        private static GameObject ButtonRing(Button button, string sprite, string box, Color color)
        {
            var owner = (RectTransform)button.transform;
            float multiplier = LayoutBox(box).SliceMultiplier;
            RectTransform rect = Node(owner, "Focus Ring", Vector2.zero, Vector2.zero);
            Stretch(rect, ShopUiArtworkLayout.RingPad / multiplier);
            foreach (Transform child in rect.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            Image image = Ensure<Image>(rect.gameObject);
            image.sprite = Import(sprite + "-ring");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.fillCenter = true;
            image.color = color;
            image.raycastTarget = false;
            rect.SetAsFirstSibling();
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        private static Button PopupButton(Transform popup, string name, string sprite, string box)
        {
            RectTransform rect = Node(popup, name, Relative(LayoutBox(box), null), LayoutBox(box).Size);
            Image image = Ensure<Image>(rect.gameObject);
            SkinArt(image, sprite, box);
            image.raycastTarget = true;
            Button button = Ensure<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors();
            return button;
        }

        private static TMP_Text LabelOf(Button button, string value, string box, string buttonBox)
        {
            TMP_Text text = Ensure<TextMeshProUGUI>(Node(button.transform, "Label", Vector2.zero, Vector2.zero)
                .gameObject);
            Style(text, box, 36f, ButtonInk, parent: buttonBox);
            text.text = value;
            return text;
        }

        private static ColorBlock ButtonColors() => new()
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 0.97f, 0.9f, 1f),
            pressedColor = new Color(0.9f, 0.86f, 0.8f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.78f, 0.74f, 0.72f, 1f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        private static void Order(Transform parent, params string[] names)
        {
            int index = 0;
            foreach (string name in names)
            {
                Transform child = parent.Find(name);
                if (child == null) continue;
                child.SetSiblingIndex(index++);
            }
        }

        public static string RequiredGlyphs() =>
            ShopView.PurchaseTitle + ShopView.CancelLabel + ShopView.ArtworkInputHint + ShopView.ArtworkGoldiInputHint +
            ShopView.InvestLabel + ShopView.InvestBackLabel + ShopView.InvestTitle + ShopView.MaxLevelLabel +
            ShopView.ElifLockedLabel + ShopView.BuyLabel + ShopView.TakeLabel + "현재스택제한없음다음:0123456789/()·%" +
            ShopView.DescribeConsumable("heart") + ShopView.DescribeConsumable("sp") +
            ShopView.DescribeConsumable("bomb") + ShopView.DescribeConsumable("key") + "일반고급희귀전설";

        private static void EnsureGlyphs()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            string required = RequiredGlyphs();
            if (font == null || font.HasCharacters(required)) return;
            if (!font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing shop artwork glyphs: " + missing);
            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        public static Sprite Import(string name)
        {
            string path = Folder + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing shop artwork " + path);
            Vector4 border = ShopUiArtworkLayout.Borders.TryGetValue(name, out Vector4 value) ? value : Vector4.zero;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                           !importer.alphaIsTransparency || importer.spriteBorder != border ||
                           importer.npotScale != TextureImporterNPOTScale.None ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 100f);
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---- review capture -------------------------------------------------------------------------------------

        // Writes output/menu-review/shop-clover-*.png: the grid with sample offers, and the purchase popup.
        [MenuItem("Trickal Fan Game/UI/Capture Clover Shop UI")]
        public static void CapturePreview()
        {
            MenuScreenCapture.Capture("shop-clover", Week13FrontendSetup.GameScenePath, Week13Hud1Setup.HudRootName,
                frame => PrepareCapture(frame, false));
            MenuScreenCapture.Capture("shop-clover-popup", Week13FrontendSetup.GameScenePath,
                Week13Hud1Setup.HudRootName, frame => PrepareCapture(frame, true));
        }

        private static void PrepareCapture(RectTransform frame, bool popup)
        {
            MenuScreenCapture.ShowOnly(frame, Week22Shop0Setup.OverlayName);
            ShopView view = frame.GetComponentInChildren<ShopView>(true);
            view.Overlay.alpha = 1f;
            UserArtwork.Apply(view.KeeperPortrait, UserArtwork.Load(ShopKeeper.ArtworkKey));
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            TrickalFanGame.Item.ItemDefinition Load(string id) =>
                AssetDatabase.LoadAssetAtPath<TrickalFanGame.Item.ItemDefinition>("Assets/Items/" + id + ".asset");
            ShopOffer[] offers =
            {
                ShopOffer.ForItem(0, Load("item-01"), 10), ShopOffer.ForItem(1, Load("item-12"), 10),
                ShopOffer.ForItem(2, Load("artifact-fairy-robe"), 20).WithElifPrice(),
                ShopOffer.ForConsumable(3, catalog.Consumables[0]), ShopOffer.ForConsumable(4, catalog.Consumables[1]),
                ShopOffer.ForConsumable(5, catalog.Consumables[2]), ShopOffer.ForItem(6, Load("item-04"), 10),
            };
            for (int index = 0; index < view.Cells.Length; index++)
            {
                ShopOffer offer = index < offers.Length ? offers[index] : null;
                ShopOfferStatus status = offer == null ? ShopOfferStatus.Empty
                    : index == 6 ? ShopOfferStatus.NotEnoughGold : ShopOfferStatus.Available;
                view.Cells[index].Bind(index, offer, status, offer == null ? 0 : offer.IsElifPriced ? 2 : offer.Price,
                    index == 0);
            }
            view.GoldText.text = "128";
            if (view.ElifText != null) view.ElifText.text = "12";
            view.KeeperLineText.text = ShopView.KeeperLine;
            if (!popup)
            {
                // The capture shows the keyboard focus on 닫기 next to the selected first cell.
                view.CloseButton.transform.localScale = Vector3.one * FocusedButtonScale;
                return;
            }

            view.ConfirmPopup.alpha = 1f;
            view.ConfirmPopup.interactable = true;
            view.BuyButton.interactable = true;
            // A capture renders before the buttons run their color transition.
            foreach (Button button in new[] { view.BuyButton, view.CancelButton, view.DismissButton })
                button.targetGraphic.canvasRenderer.SetColor(Color.white);
            view.BuyFocusMark.SetActive(true);
            view.TooltipGroup.alpha = 1f;
            view.ConfirmTitleText.text = ShopView.PurchaseTitle;
            view.ConfirmIcon.sprite = view.Cells[0].Icon.sprite;
            view.ConfirmIcon.material = view.Cells[0].Icon.material;
            view.DetailNameText.text = offers[0].DisplayName;
            view.TooltipNameText.text = offers[0].DisplayName;
            view.ConfirmNoteText.text = "현재 스택 0 / 제한 없음";
            view.DetailKindText.text = "아티팩트  ·  일반";
            view.DetailEffectText.text = ArtifactEffectDescription.Build(offers[0].Item);
            view.DetailPriceText.text = "10";
            view.ConfirmCurrencyIcon.sprite = Import("gold");
            view.BuyButtonLabel.text = ShopView.BuyLabel;
        }

        // The artwork changes the general shop, the Goldi shop and the invest list: their verifiers check both looks.
        public static void SetupAndVerifyBatch()
        {
            Setup();
            Verify();
        }

        public static void Verify()
        {
            Week22Shop0Verification.Verify();
            Week23Shop1Verification.Verify();
            Week27Elif3Verification.Verify();
            Debug.Log("Clover Shop UI verification passed.");
        }

        public static void SetupVerifyAndCaptureBatch()
        {
            SetupAndVerifyBatch();
            CapturePreview();
        }
    }
}
