using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Elif-3 (contract 27 §4): adds the elif balance line, the 투자 button and the invest list to the Shop-0 shop
    // screen. Runtime rules live in ShopUpgrades, ShopStockBuilder, ShopRoom and RunProgress; this only builds UI
    // objects and connects them to ShopView. Re-running updates the same objects.
    public static class Week27Elif3Setup
    {
        public const string Piece = "Elif-3";
        public const string ElifTextName = "Elif";
        public const string InvestButtonName = "Invest";
        public const string InvestPanelName = "Invest Panel";
        public const string InvestNoticeName = "Invest Notice";
        public static readonly Vector2 ElifTextPosition = new(-560f, -338f);
        public static readonly Vector2 InvestButtonPosition = new(540f, -362f);
        public static readonly Vector2 InvestButtonSize = new(300f, 52f);
        // The invest list covers the 3×3 grid and stays below the shop title.
        public static readonly Vector2 InvestPanelSize = new(650f, 640f);
        public static readonly Vector2 RowSize = new(600f, 168f);
        public static readonly float[] RowY = { 150f, -38f };
        public static readonly Color ElifColor = new(0.72f, 0.88f, 1f, 1f);

        public static string RowName(int index) => $"Invest Row {index + 1}";

        [MenuItem("Trickal Fan Game/Week 27/Setup Elif-3 Shop Upgrades")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Elif-3 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ShopView[] views = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ShopView>(true)).ToArray();
            if (views.Length != 1)
                throw new InvalidOperationException("Run Shop-0 setup first: the Game Scene needs one shop screen.");
            ShopView view = views[0];
            Transform panel = view.transform.Find("Shop Panel");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (panel == null || font == null)
                throw new InvalidOperationException("Elif-3 requires the Shop-0 panel and the TMP UI font.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Elif-3 Shop Upgrades");

            TMP_Text elifText = Text(panel, ElifTextName, ShopView.FormatElifBalance(0, true), ElifTextPosition,
                new Vector2(380f, 40f), 24f, font, TextAlignmentOptions.Center, false);
            elifText.fontStyle = FontStyles.Bold;
            elifText.color = ElifColor;

            Button investButton = ActionButton(panel, InvestButtonName, ShopView.InvestLabel, InvestButtonPosition,
                InvestButtonSize, new Color(0.3f, 0.36f, 0.62f, 1f), font, out TMP_Text investLabel);

            RectTransform investRect = Rect(panel, InvestPanelName, Week22Shop0Setup.GridCenter, InvestPanelSize);
            // Drawn above the grid cells: a new panel is created last; keep it after the cells on re-runs too.
            investRect.SetAsLastSibling();
            Image investBackground = Component<Image>(investRect.gameObject);
            investBackground.color = new Color(0.075f, 0.1f, 0.16f, 1f);
            investBackground.raycastTarget = true;
            Outline investOutline = Component<Outline>(investRect.gameObject);
            investOutline.effectColor = new Color(0.45f, 0.62f, 0.9f, 0.9f);
            investOutline.effectDistance = new Vector2(2f, -2f);
            CanvasGroup investGroup = Component<CanvasGroup>(investRect.gameObject);
            investGroup.alpha = 0f;
            investGroup.interactable = false;
            investGroup.blocksRaycasts = false;

            TMP_Text title = Text(investRect, "Invest Title", ShopView.InvestTitle, new Vector2(0f, 276f),
                new Vector2(600f, 50f), 32f, font, TextAlignmentOptions.Center, false);
            title.fontStyle = FontStyles.Bold;
            title.color = ElifColor;

            ShopUpgradeRowView[] rows = new ShopUpgradeRowView[ShopUpgrades.Catalog.Length];
            for (int index = 0; index < rows.Length; index++) rows[index] = Row(investRect, index, font);

            TMP_Text notice = Text(investRect, InvestNoticeName, ShopView.InvestNotice, new Vector2(0f, -226f),
                new Vector2(600f, 100f), 20f, font, TextAlignmentOptions.Center, true);
            notice.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            Undo.RecordObject(view, "Connect Elif-3 shop upgrades");
            view.ConfigureInvest(elifText, investButton, investLabel, investGroup, rows, notice);
            EnsureGlyphs(font);

            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Elif-3 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Elif-3 setup complete: the shop screen shows the elif balance and a 투자 button that opens " +
                      $"the {rows.Length} shop upgrades over the grid.");
        }

        private static ShopUpgradeRowView Row(Transform parent, int index, TMP_FontAsset font)
        {
            RectTransform rect = Rect(parent, RowName(index), new Vector2(0f, RowY[index]), RowSize);
            Image background = Component<Image>(rect.gameObject);
            background.color = new Color(0.09f, 0.125f, 0.18f, 1f);
            background.raycastTarget = true;
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Outline focusFrame = Component<Outline>(rect.gameObject);
            focusFrame.effectColor = new Color(0.55f, 0.88f, 0.94f, 1f);
            focusFrame.effectDistance = new Vector2(4f, -4f);
            focusFrame.enabled = false;

            TMP_Text name = Text(rect, "Name", "업그레이드", new Vector2(-120f, 46f), new Vector2(330f, 44f), 28f, font,
                TextAlignmentOptions.Left, false);
            name.fontStyle = FontStyles.Bold;
            TMP_Text level = Text(rect, "Level", ShopView.FormatUpgradeLevel(0, 3), new Vector2(170f, 46f),
                new Vector2(220f, 40f), 22f, font, TextAlignmentOptions.Right, false);
            level.color = new Color(0.92f, 0.78f, 0.38f, 1f);
            TMP_Text effect = Text(rect, "Effect", string.Empty, new Vector2(0f, -4f), new Vector2(560f, 40f), 21f,
                font, TextAlignmentOptions.Left, false);
            effect.color = new Color(0.82f, 0.88f, 0.95f, 1f);
            TMP_Text cost = Text(rect, "Cost", ShopView.FormatUpgradeCost(null, 0), new Vector2(170f, -52f),
                new Vector2(220f, 40f), 24f, font, TextAlignmentOptions.Right, false);
            cost.fontStyle = FontStyles.Bold;

            ShopUpgradeRowView row = Component<ShopUpgradeRowView>(rect.gameObject);
            row.ConfigureVisuals(index, button, focusFrame, name, level, effect, cost);
            return row;
        }

        public static string RequiredGlyphs() =>
            ShopView.ElifSuffix + ShopView.ElifLockedLabel + ShopView.NotEnoughElifMessage +
            ShopView.ElifLockedMessage + ShopView.ElifOfferNote + ShopView.InvestLabel + ShopView.InvestBackLabel +
            ShopView.InvestBuyLabel + ShopView.InvestTitle + ShopView.InvestKind + ShopView.InvestNotice +
            ShopView.InvestPurchasedMessage + ShopView.InvestMaxLevelMessage + ShopView.InvestPendingSaveMessage +
            ShopView.InvestDevelopmentMessage + ShopView.MaxLevelLabel + "업그레이드다음최대단계:()%/·0123456789" +
            string.Concat(ShopUpgrades.Catalog.Select(upgrade =>
                upgrade.DisplayName + string.Concat(Enumerable.Range(1, upgrade.MaxLevel)
                    .Select(level => ShopUpgrades.DescribeLevel(upgrade.UpgradeId, level)))));

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            string required = RequiredGlyphs();
            if (font.HasCharacters(required)) return;
            if (!font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Elif-3 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static Button ActionButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
            Color color, TMP_FontAsset font, out TMP_Text labelText)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Image image = Component<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            labelText = Text(rect, "Label", label, Vector2.zero, new Vector2(size.x - 30f, size.y - 8f), 22f, font,
                TextAlignmentOptions.Center, false);
            labelText.fontStyle = FontStyles.Bold;
            return button;
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
