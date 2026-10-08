using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Shop-0: rebuilds the ShopRoom Prefab around the shopkeeper (시스트 placeholder), builds the 3×3 shop screen on
    // the Game HUD, and connects its session to the room assembler. Re-running updates the same objects.
    public static class Week22Shop0Setup
    {
        public const string OverlayName = "Shop Overlay";
        public static readonly Vector2 PanelSize = new(1600f, 840f);
        public static readonly Vector2 CellSize = new(190f, 190f);
        public const float CellPitch = 212f;
        public static readonly Vector2 GridCenter = new(-20f, 40f);

        [MenuItem("Trickal Fan Game/Week 22/Setup Shop-0 Shop UI")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Shop-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ShopCatalog catalog = Week20Special4Setup.EnsureCatalog();
            ShopRoom prefab = Week20Special4Setup.EnsureShopRoomPrefab();

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView hud = FindSingle<GameHudView>(scene, "Game HUD");
            PlayerInventory inventory = FindSingle<PlayerInventory>(scene, "player inventory");
            RoomGraphAssembler assembler = FindSingle<RoomGraphAssembler>(scene, "room assembler");
            EventSystem eventSystem = FindSingle<EventSystem>(scene, "EventSystem");
            Health health = inventory.GetComponent<Health>();
            PlayerActionState actionState = inventory.GetComponent<PlayerActionState>();
            GraphicRaycaster raycaster = hud.GetComponent<GraphicRaycaster>();
            RectTransform frame = hud.transform.Find("ReferenceFrame") as RectTransform;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (health == null || actionState == null || raycaster == null || frame == null || font == null)
                throw new InvalidOperationException("Shop-0 requires the configured Game HUD, player, and TMP font.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Shop-0 Shop UI");

            bool overlayExists = frame.Find(OverlayName) != null;
            RectTransform overlayRect = Rect(frame, OverlayName, Vector2.zero, Week16Reward2Setup.ReferenceResolution);
            // Reward selection draws above the shop; a re-run keeps whatever order the scene has.
            Transform rewardOverlay = frame.Find(Week16Reward2Setup.OverlayName);
            if (!overlayExists && rewardOverlay != null)
                overlayRect.SetSiblingIndex(rewardOverlay.GetSiblingIndex());
            Image dimmer = Component<Image>(overlayRect.gameObject);
            dimmer.color = new Color(0.015f, 0.025f, 0.045f, 0.9f);
            dimmer.raycastTarget = false;
            CanvasGroup overlay = Component<CanvasGroup>(overlayRect.gameObject);
            overlay.alpha = 0f;
            overlay.interactable = false;
            overlay.blocksRaycasts = false;

            RectTransform panel = Rect(overlayRect, "Shop Panel", Vector2.zero, PanelSize);
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = ShopView.PanelColor;
            panelImage.raycastTarget = false;
            Outline panelOutline = Component<Outline>(panel.gameObject);
            panelOutline.effectColor = ShopView.PanelOutlineColor;
            panelOutline.effectDistance = ShopView.PanelOutlineDistance;

            TMP_Text title = Text(panel, "Title", ShopView.Title, new Vector2(0f, 384f), new Vector2(900f, 58f), 38f,
                font, TextAlignmentOptions.Center, false);
            title.fontStyle = FontStyles.Bold;

            Image keeperPortrait = SetupKeeperColumn(panel, font, out TMP_Text goldText, out TMP_Text keeperName,
                out TMP_Text keeperLine);
            ShopOfferCellView[] cells = new ShopOfferCellView[ShopCatalog.GridSlotCount];
            for (int index = 0; index < cells.Length; index++) cells[index] = SetupCell(panel, index, font);

            TMP_Text feedback = Text(panel, "Feedback", string.Empty, new Vector2(GridCenter.x, -306f),
                new Vector2(640f, 36f), 22f, font, TextAlignmentOptions.Center, false);
            feedback.color = new Color(1f, 0.86f, 0.5f, 1f);
            TMP_Text hint = Text(panel, "Input Hint", ShopView.InputHint, new Vector2(0f, -380f),
                new Vector2(1100f, 36f), 19f, font, TextAlignmentOptions.Center, false);
            hint.color = new Color(0.72f, 0.82f, 0.9f, 1f);

            const float detailX = 540f;
            TMP_Text detailName = Text(panel, "Detail Name", "상품 이름", new Vector2(detailX, 270f),
                new Vector2(440f, 76f), 31f, font, TextAlignmentOptions.Center, true);
            detailName.fontStyle = FontStyles.Bold;
            TMP_Text detailKind = Text(panel, "Detail Kind", "아티팩트", new Vector2(detailX, 212f),
                new Vector2(440f, 32f), 20f, font, TextAlignmentOptions.Center, false);
            detailKind.color = new Color(0.92f, 0.78f, 0.38f, 1f);
            RectTransform divider = Rect(panel, "Detail Divider", new Vector2(detailX, 184f), new Vector2(420f, 2f));
            Image dividerImage = Component<Image>(divider.gameObject);
            dividerImage.color = new Color(0.35f, 0.48f, 0.58f, 0.8f);
            dividerImage.raycastTarget = false;
            TMP_Text detailEffect = Text(panel, "Detail Effect", "효과 설명", new Vector2(detailX, 40f),
                new Vector2(420f, 260f), 22f, font, TextAlignmentOptions.TopLeft, true);
            detailEffect.lineSpacing = 8f;
            TMP_Text detailPrice = Text(panel, "Detail Price", ShopView.FormatPrice(10), new Vector2(detailX, -132f),
                new Vector2(420f, 44f), 30f, font, TextAlignmentOptions.Center, false);
            detailPrice.fontStyle = FontStyles.Bold;
            detailPrice.color = new Color(1f, 0.93f, 0.62f, 1f);
            Button buyButton = SetupActionButton(panel, "Buy", ShopView.BuyLabel, new Vector2(detailX, -208f),
                new Color(0.12f, 0.48f, 0.56f, 1f), font, out TMP_Text buyLabel);
            Button closeButton = SetupActionButton(panel, "Close", ShopView.CloseLabel, new Vector2(detailX, -286f),
                new Color(0.28f, 0.32f, 0.4f, 1f), font, out _);

            ShopSession session = Component<ShopSession>(overlayRect.gameObject);
            session.Configure(actionState, health);
            ShopView view = Component<ShopView>(overlayRect.gameObject);
            view.Configure(session, overlay, raycaster, eventSystem, keeperPortrait, goldText, cells, detailName,
                detailKind, detailEffect, detailPrice, feedback, buyButton, buyLabel, closeButton);
            // Shop-1: the view switches these between the general shop and the 골디 shop when it opens.
            view.ConfigureTheme(title, keeperName, keeperLine, panelImage, panelOutline);
            EnsureGlyphs(font, catalog);

            Undo.RecordObject(assembler, "Connect Shop-0 shop session");
            assembler.ConfigureShop(prefab, catalog);
            assembler.ConfigureShopSession(session);
            EditorUtility.SetDirty(assembler);

            foreach (Component component in overlayRect.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Shop-0 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Shop-0 setup complete: the ShopRoom Prefab has the shopkeeper instead of stalls, and the Game " +
                      $"HUD has the {ShopCatalog.GridColumns}x{ShopCatalog.GridColumns} shop screen connected to the " +
                      "room assembler.");
        }

        // The portrait is a placeholder until the 시스트 character asset exists; a sprite assigned later is kept.
        private static Image SetupKeeperColumn(Transform panel, TMP_FontAsset font, out TMP_Text goldText,
            out TMP_Text name, out TMP_Text line)
        {
            const float keeperX = -560f;
            bool portraitExists = panel.Find("Keeper Portrait") != null;
            RectTransform portraitRect = Rect(panel, "Keeper Portrait", new Vector2(keeperX, 70f),
                new Vector2(340f, 420f));
            Image portrait = Component<Image>(portraitRect.gameObject);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            if (!portraitExists || portrait.sprite == null) portrait.color = new Color(1f, 0.8f, 0.35f, 0.9f);

            name = Text(panel, "Keeper Name", ShopKeeper.DisplayName, new Vector2(keeperX, -176f),
                new Vector2(380f, 48f), 32f, font, TextAlignmentOptions.Center, false);
            name.fontStyle = FontStyles.Bold;
            line = Text(panel, "Keeper Line", ShopView.KeeperLine, new Vector2(keeperX, -226f),
                new Vector2(380f, 36f), 21f, font, TextAlignmentOptions.Center, false);
            line.color = new Color(0.7f, 0.78f, 0.86f, 1f);
            goldText = Text(panel, "Gold", $"보유 0 {ShopView.GoldSuffix}", new Vector2(keeperX, -296f),
                new Vector2(380f, 44f), 28f, font, TextAlignmentOptions.Center, false);
            goldText.fontStyle = FontStyles.Bold;
            goldText.color = new Color(1f, 0.93f, 0.62f, 1f);
            return portrait;
        }

        public static Vector2 CellPosition(int index)
        {
            int column = index % ShopCatalog.GridColumns;
            int row = index / ShopCatalog.GridColumns;
            return GridCenter + new Vector2((column - 1) * CellPitch, (1 - row) * CellPitch);
        }

        private static ShopOfferCellView SetupCell(Transform panel, int index, TMP_FontAsset font)
        {
            RectTransform cellRect = Rect(panel, $"Shop Cell {index + 1}", CellPosition(index), CellSize);
            Image background = Component<Image>(cellRect.gameObject);
            background.color = new Color(0.09f, 0.125f, 0.18f, 1f);
            background.raycastTarget = true;
            Button button = Component<Button>(cellRect.gameObject);
            button.targetGraphic = background;
            // The view moves the focus itself; uGUI navigation would move a second, different selection.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Outline focusFrame = Component<Outline>(cellRect.gameObject);
            focusFrame.effectColor = new Color(0.55f, 0.88f, 0.94f, 1f);
            focusFrame.effectDistance = new Vector2(4f, -4f);
            focusFrame.enabled = false;

            RectTransform contentRect = Rect(cellRect, "Content", Vector2.zero, CellSize);
            CanvasGroup content = Component<CanvasGroup>(contentRect.gameObject);
            content.interactable = false;
            content.blocksRaycasts = false;
            RectTransform iconRect = Rect(contentRect, "Icon", new Vector2(0f, 36f), new Vector2(84f, 84f));
            Image icon = Component<Image>(iconRect.gameObject);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            TMP_Text iconText = Text(iconRect, "Icon Text", string.Empty, Vector2.zero, new Vector2(84f, 40f), 17f,
                font, TextAlignmentOptions.Center, false);
            iconText.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            iconText.fontStyle = FontStyles.Bold;
            TMP_Text name = Text(contentRect, "Name", "상품", new Vector2(0f, -32f), new Vector2(178f, 44f), 19f,
                font, TextAlignmentOptions.Center, true);
            TMP_Text price = Text(contentRect, "Price", ShopView.FormatPrice(0), new Vector2(0f, -72f),
                new Vector2(178f, 30f), 20f, font, TextAlignmentOptions.Center, false);
            price.fontStyle = FontStyles.Bold;

            ShopOfferCellView cell = Component<ShopOfferCellView>(cellRect.gameObject);
            cell.ConfigureVisuals(index, button, content, focusFrame, icon, iconText, name, price);
            return cell;
        }

        private static Button SetupActionButton(Transform parent, string name, string label, Vector2 position,
            Color color, TMP_FontAsset font, out TMP_Text labelText)
        {
            RectTransform rect = Rect(parent, name, position, new Vector2(300f, 60f));
            Image image = Component<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            labelText = Text(rect, "Label", label, Vector2.zero, new Vector2(270f, 48f), 24f, font,
                TextAlignmentOptions.Center, false);
            labelText.fontStyle = FontStyles.Bold;
            return button;
        }

        public static string RequiredGlyphs(ShopCatalog catalog) =>
            ShopView.Title + ShopView.KeeperLine + ShopView.GoldSuffix + ShopView.FreeLabel + ShopView.SoldOutLabel +
            ShopView.ItemUnavailableLabel + ShopView.BuyLabel + ShopView.TakeLabel + ShopView.CloseLabel +
            ShopView.NotEnoughGoldMessage + ShopView.PurchasedMessage + ShopView.SoldOutMessage +
            ShopView.ItemUnavailableMessage + ShopView.EmptyCellMessage + ShopView.ConsumableKind +
            ShopView.ConsumableEffect + ShopView.InputHint + ShopKeeper.DisplayName + ShopKeeper.OpenPrompt +
            ShopView.GoldiTitle + ShopView.GoldiKeeperLine + ShopView.GoldiConsumableEffect +
            ShopKeeper.GoldiDisplayName + ShopKeeper.GreetingLine + ShopKeeper.GoldiGreetingLine +
            "보유현재스택제한없음상품이름효과설명0123456789/·" +
            string.Concat(catalog.Consumables.Select(consumable => consumable.DisplayName));

        private static void EnsureGlyphs(TMP_FontAsset font, ShopCatalog catalog)
        {
            string required = RequiredGlyphs(catalog);
            if (font.HasCharacters(required)) return;
            if (!font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Shop-0 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T Component<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing != null ? existing as RectTransform : null;
            if (existing != null && rect == null) throw new InvalidOperationException(name + " requires RectTransform.");
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Create " + name);
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "Layout " + name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment, bool wrap)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
