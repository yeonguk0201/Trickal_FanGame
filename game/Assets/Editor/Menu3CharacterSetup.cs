using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;

namespace TrickalFanGame.Editor
{
    // Menu-3 (docs/26 §3.5): character select and skill upgrade in the magic-library look, following the user's
    // reference screens. Selection, confirmation, progress loading and upgrade requests are unchanged; this adds the
    // roster grid with "준비중" slots, the centre stage with previous/next arrows and the character info panel.
    public static class Menu3CharacterSetup
    {
        public static readonly Vector2 SlotSize = new(150, 140);
        public const int RosterSlots = 8;
        public static readonly Vector2 ConfirmSize = new(360, 88);
        public static readonly Vector2 BackSize = new(220, 64);
        public const string ErpinPath = "Assets/Characters/erpin.asset";
        public const string ErpinSlotPortraitPath = "Assets/Resources/UserArtwork/erpin-high-grade.png";
        public const string HighGradeSkillIconPath = "Assets/Resources/UserArtwork/erpin-high-grade.png";

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-3 Character Select and Skill Upgrade")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Menu-3 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MenuArtSetup.Apply();
            FillErpinPresentation();

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendHomeView home = FindSingle<FrontendHomeView>(scene);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Menu-3");

            StyleCharacterSelection(home.CharacterSelectionView);
            StyleSkillUpgrade(home.SkillUpgradeView);

            foreach (Component component in home.CharacterSelectionView.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            foreach (Component component in home.SkillUpgradeView.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Frontend save failed.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("Menu-3 setup complete. Character select and skill upgrade use the magic-library parts.");
        }

        // Display text and art for the existing erpin asset. Only empty fields are filled, so later edits stay.
        private static void FillErpinPresentation()
        {
            CharacterDefinition erpin = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ErpinPath);
            if (erpin == null) throw new InvalidOperationException("Missing " + ErpinPath);
            var data = new SerializedObject(erpin);
            SetIfEmpty(data, "epithet", "요정의 여왕");
            // User text (2026-11-10). The first draft from the mockup is replaced; other edits stay.
            SetIfEmpty(data, "introduction", Introduction, FirstIntroductionDraft, UnbrokenIntroduction);
            SetIfEmpty(data, "lowGradeSkillName", "마력탄 폭주");
            SetIfEmpty(data, "highGradeSkillName", "돌겨어어어!!! 억...?");
            SetIfEmpty(data, "basicSkillName", "요정의 빛살");
            SetIfEmpty(data, "basicSkillDescription", "지팡이로 마력탄을 발사해\n앞의 적을 공격해요.");
            SetIfEmpty(data, "slotPortrait", AssetDatabase.LoadAssetAtPath<Sprite>(ErpinSlotPortraitPath));
            SetIfEmpty(data, "basicSkillIcon", MenuArtSetup.Load(MenuArtSetup.SkillBasic));
            SetIfZero(data, "healthRating", 5);
            SetIfZero(data, "skillPointRating", 3);
            SetIfZero(data, "attackRating", 3);
            if (data.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(erpin);
        }

        // TMP breaks Korean between any two letters, so the lines are broken by hand at phrase boundaries.
        private const string Introduction =
            "요정들의 여왕.\n달콤한 디저트라면 사족을 못 쓴다.\n한심해보일 때도 있고 많이 부족한 게 사실이지만,\n" +
            "자기 나름대로 노력하는 요정이다.\n아무리 버릇없게 행동해도\n미워할 수 없는 구석이 있다.";
        private const string UnbrokenIntroduction =
            "요정들의 여왕.\n달콤한 디저트라면 사족을 못 쓴다. 한심해보일 때도 있고 많이 부족한 게 사실이지만, " +
            "자기 나름대로 노력하는 요정이다.\n아무리 버릇없게 행동해도 미워할 수 없는 구석이 있다.";
        private const string FirstIntroductionDraft =
            "트릭컬 세계관의 요정 여왕.\n작고 귀여운 몸이지만, 강력한 마법으로\n전장을 누비는 기본 플레이 캐릭터예요.";

        private static void SetIfEmpty(SerializedObject data, string field, string value, params string[] replaceable)
        {
            SerializedProperty property = data.FindProperty(field);
            if (string.IsNullOrWhiteSpace(property.stringValue) || replaceable.Contains(property.stringValue))
                property.stringValue = value;
        }

        private static void SetIfEmpty(SerializedObject data, string field, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing art for " + field);
            SerializedProperty property = data.FindProperty(field);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        private static void SetIfZero(SerializedObject data, string field, int value)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property.intValue == 0) property.intValue = value;
        }

        // ---------- Character select ----------

        private static void StyleCharacterSelection(FrontendCharacterSelectionView view)
        {
            var root = (RectTransform)view.transform;
            Skin(root.gameObject, MenuArtSetup.Background, Color.white);
            TitlePlaque(root, Require<TMP_Text>(root, "CharacterSelectionTitle"));

            // Left: roster panel, ribbon and the slot grid.
            Image roster = Pill(root, "RosterPanel", MenuArtSetup.PanelRoster, new Vector2(-640, -30), new Vector2(470, 720));
            roster.pixelsPerUnitMultiplier = 1f;
            roster.transform.SetSiblingIndex(1);
            Image rosterRibbon = Pill(root, "RosterRibbon", MenuArtSetup.SliderTrack, new Vector2(-640, 282), new Vector2(340, 58), Accent);
            rosterRibbon.transform.SetSiblingIndex(2);
            Picture(rosterRibbon.transform, "Crown", MenuArtSetup.IconCrown, new Vector2(-128, 4), new Vector2(54, 38));
            Label(rosterRibbon.transform, "Heading", "플레이어 캐릭터", new Vector2(18, 1), new Vector2(220, 44), 24, RibbonText);
            Picture(rosterRibbon.transform, "Clover", MenuArtSetup.Clover, new Vector2(190, -6), new Vector2(40, 40));

            RectTransform grid = view.CardContainer;
            Place(grid, new Vector2(-640, -68), new Vector2(SlotSize.x * 2 + 20, SlotSize.y * 4 + 16 * 3));
            HorizontalLayoutGroup oldLayout = grid.GetComponent<HorizontalLayoutGroup>();
            if (oldLayout != null) Undo.DestroyObjectImmediate(oldLayout);
            GridLayoutGroup layout = Ensure<GridLayoutGroup>(grid.gameObject);
            layout.cellSize = SlotSize;
            layout.spacing = new Vector2(20, 16);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            grid.SetSiblingIndex(3);
            StyleSlot(view.CardTemplate);
            GameObject comingSoon = ComingSoonTemplate(root);

            // Centre: pedestal, character and the previous/next arrows.
            Image pedestal = Picture(root, "Pedestal", MenuArtSetup.Pedestal, new Vector2(0, -238), new Vector2(500, 234));
            pedestal.transform.SetSiblingIndex(4);
            RectTransform previewRect = Node(root, "CharacterPreview", new Vector2(0, 20), new Vector2(500, 500));
            Image preview = Skin(previewRect.gameObject, MenuArtSetup.SilhouetteComingSoon, Color.white, preserveAspect: true);
            previewRect.SetSiblingIndex(5);
            FrontendStartButton previous = ArrowButton(root, "PreviousCharacterButton", new Vector2(-330, 30), false);
            FrontendStartButton next = ArrowButton(root, "NextCharacterButton", new Vector2(330, 30), true);

            FrontendCharacterDetailsView details = DetailsPanel(root, preview);

            // Bottom: status line, confirm and back.
            TMP_Text status = view.StatusText;
            Image statusPill = Pill(root, "StatusPill", MenuArtSetup.ButtonSecondary, new Vector2(0, -378), new Vector2(560, 44));
            Place(status.rectTransform, new Vector2(0, -378), new Vector2(520, 40));
            StyleText(status, 20, Ink);
            DrawBehind(statusPill.transform, status.transform);

            var confirm = (FrontendStartButton)view.ConfirmButton;
            Place((RectTransform)confirm.transform, new Vector2(0, -444), ConfirmSize);
            StyleButton(confirm, true, ConfirmSize, 32);
            var back = (FrontendStartButton)view.BackButton;
            Place((RectTransform)back.transform, new Vector2(-790, -450), BackSize);
            StyleButton(back, false, BackSize, 26);
            BackArrow(back);

            Undo.RecordObject(view, "Menu-3 presentation");
            view.ConfigurePresentation(details, previous, next, comingSoon, RosterSlots, statusPill.gameObject);
        }

        private static void StyleSlot(FrontendCharacterCardView card)
        {
            var rect = (RectTransform)card.transform;
            Undo.RecordObject(rect, "Slot size");
            rect.sizeDelta = SlotSize;
            var button = (FrontendStartButton)card.Button;
            // The root only catches clicks; the frame, glows and portrait are children so the glow can sit behind.
            Skin(rect.gameObject, MenuArtSetup.RoundedFill, new Color(1f, 1f, 1f, 0f), raycast: true);
            Image frame = Skin(Node(rect, "Frame", Vector2.zero, SlotSize).gameObject, MenuArtSetup.PortraitSlot, Color.white);

            // Portrait clipped to the slot's rounded inner area, filling it.
            RectTransform mask = Node(rect, "PortraitMask", new Vector2(0, -0.5f), new Vector2(127, 116)); // the purple inner area; the cream rim stays visible
            Image maskImage = Skin(mask.gameObject, MenuArtSetup.RoundedFill, Color.white);
            maskImage.pixelsPerUnitMultiplier = 0.78f; // ~23px corners like the slot's inner area
            Ensure<Mask>(mask.gameObject).showMaskGraphic = false;
            Image portrait = rect.Find("CharacterIcon")?.GetComponent<Image>() ?? Require<Image>(mask, "CharacterIcon");
            if (portrait.transform.parent != mask) Undo.SetTransformParent(portrait.transform, mask, "Mask portrait");
            float cover = Mathf.Max(mask.sizeDelta.x, mask.sizeDelta.y);
            Place(portrait.rectTransform, Vector2.zero, new Vector2(cover, cover));
            Undo.RecordObject(portrait, "Slot portrait");
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            Deactivate(rect, "CharacterName");
            Deactivate(rect, "CharacterDescription");

            // "선택됨" badge above the portrait; the old badge lived inside the selection ring.
            RectTransform badge = Node(rect, "SelectionBadge", new Vector2(0, -60), new Vector2(100, 32));
            Skin(badge.gameObject, MenuArtSetup.SliderTrack, Accent, fitHeight: 32);
            TMP_Text state = new[] { rect, rect.Find("SelectionBorder") as RectTransform, badge }
                .Where(parent => parent != null)
                .Select(parent => parent.Find("SelectionState")?.GetComponent<TMP_Text>())
                .FirstOrDefault(text => text != null);
            if (state == null) throw new InvalidOperationException("Character slot is missing SelectionState.");
            if (state.transform.parent != badge) Undo.SetTransformParent(state.transform, badge, "Badge text");
            Place(state.rectTransform, Vector2.zero, new Vector2(96, 30));
            StyleText(state, 18, RibbonText);
            Transform oldBadge = rect.Find("SelectionBorder/Badge");
            if (oldBadge != null) Undo.DestroyObjectImmediate(oldBadge.gameObject);
            badge.SetAsLastSibling();
            badge.gameObject.SetActive(false);

            // Hover, focus and selection glow softly behind the frame instead of drawing a box around it.
            GameObject selection = GlowLayer(rect, "SelectionBorder", SelectedGlow, 12f, 0); // close glow so the slot keeps its size
            Undo.RecordObject(selection.GetComponent<Image>(), "Wide glow");
            selection.GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
            GameObject focus = GlowLayer(rect, "FocusBorder", FocusGlow, 14f, 1);
            GameObject hover = GlowLayer(rect, "HoverBorder", HoverGlow, 12f, 2);
            Undo.RecordObject(button, "Slot button");
            button.targetGraphic = frame;
            button.colors = Colors();
            button.ConfigureBorders(hover, focus);
            Pop(button);
            Undo.RecordObject(card, "Slot references");
            card.ConfigureTemplate(button, Require<TMP_Text>(rect, "CharacterName"),
                Require<TMP_Text>(rect, "CharacterDescription"), state, selection, portrait);
            card.ConfigureBadge(badge.gameObject);
        }

        private static GameObject ComingSoonTemplate(RectTransform root)
        {
            RectTransform slot = Node(root, "ComingSoonTemplate", Vector2.zero, SlotSize);
            Skin(slot.gameObject, MenuArtSetup.PortraitSlot, new Color(1f, 1f, 1f, 0.85f));
            Picture(slot, "Silhouette", MenuArtSetup.SilhouetteComingSoon, new Vector2(0, 12), new Vector2(84, 90),
                new Color(1f, 1f, 1f, 0.9f));
            Label(slot, "Label", "준비중", new Vector2(0, -48), new Vector2(120, 28), 20, RibbonText);
            slot.gameObject.SetActive(false);
            return slot.gameObject;
        }

        private static FrontendStartButton ArrowButton(RectTransform root, string name, Vector2 position, bool flip)
        {
            RectTransform rect = Node(root, name, position, new Vector2(64, 96));
            Skin(rect.gameObject, MenuArtSetup.RoundedFill, new Color(1f, 1f, 1f, 0f), raycast: true);
            RectTransform iconRect = Node(rect, "Icon", Vector2.zero, new Vector2(64, 96));
            Image icon = Skin(iconRect.gameObject, MenuArtSetup.ArrowLeft, Color.white, preserveAspect: true);
            iconRect.localScale = new Vector3(flip ? -1 : 1, 1, 1);
            RemoveChild(rect, "HoverBorder");
            RemoveChild(rect, "FocusBorder");
            FrontendStartButton button = Ensure<FrontendStartButton>(rect.gameObject);
            button.targetGraphic = icon;
            button.colors = Colors();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // keyboard uses left/right on slots
            button.ConfigureBorders(null, null); // hover shows as the pop-up scale
            Pop(button);
            return button;
        }

        private static FrontendCharacterDetailsView DetailsPanel(RectTransform root, Image preview)
        {
            RectTransform panel = Node(root, "CharacterDetailsPanel", new Vector2(640, -30), new Vector2(480, 728));
            Skin(panel.gameObject, MenuArtSetup.PanelDetails, Color.white);
            RemoveChild(panel, "Face"); // user feedback: no photo, the name sits in the middle

            TMP_Text name = Label(panel, "CharacterName", "", new Vector2(0, 280), new Vector2(300, 60), 44, Ink);
            Pill(panel, "EpithetPill", MenuArtSetup.SliderTrack, new Vector2(0, 226), new Vector2(200, 40));
            TMP_Text epithet = Label(panel, "Epithet", "", new Vector2(0, 226), new Vector2(190, 36), 20, InkSoft);
            Picture(panel, "Medal", MenuArtSetup.Clover, new Vector2(178, 300), new Vector2(64, 62));
            TMP_Text intro = Label(panel, "Introduction", "", new Vector2(0, 112), new Vector2(400, 172), 20, Ink);
            Undo.RecordObject(intro, "Wrap");
            intro.textWrappingMode = TextWrappingModes.Normal;
            intro.lineSpacing = 0f;

            SectionHeader(panel, "StatsHeader", MenuArtSetup.Clover, "기본 능력치", 4);
            Image[] health = StatRow(panel, "Health", MenuArtSetup.IconHeart, "HP", -40);
            Image[] skillPoints = StatRow(panel, "SkillPoint", MenuArtSetup.IconWand, "SP", -86);
            Image[] attack = StatRow(panel, "Attack", MenuArtSetup.IconSword, "공격력", -132);

            SectionHeader(panel, "SkillHeader", MenuArtSetup.IconStarburst, "기본 스킬", -184);
            Image tile = Picture(panel, "BasicSkillTile", MenuArtSetup.PortraitSlot, new Vector2(-148, -268), new Vector2(92, 92));
            Image skillIcon = Picture(tile.transform, "Icon", MenuArtSetup.SkillBasic, Vector2.zero, new Vector2(70, 70));
            TMP_Text skillName = Label(panel, "BasicSkillName", "", new Vector2(50, -236), new Vector2(260, 32), 22, Ink,
                TextAlignmentOptions.Left);
            TMP_Text skillDescription = Label(panel, "BasicSkillDescription", "", new Vector2(50, -282),
                new Vector2(260, 56), 20, InkSoft, TextAlignmentOptions.TopLeft);
            Undo.RecordObject(skillDescription, "Wrap");
            skillDescription.textWrappingMode = TextWrappingModes.Normal;

            FrontendCharacterDetailsView details = Ensure<FrontendCharacterDetailsView>(panel.gameObject);
            details.Configure(preview, null, name, epithet, intro, health, skillPoints, attack, skillIcon, skillName,
                skillDescription);
            return details;
        }

        private static void SectionHeader(RectTransform panel, string name, string icon, string text, float y)
        {
            Picture(panel, name + "Icon", icon, new Vector2(-180, y), new Vector2(32, 32));
            Label(panel, name, text, new Vector2(-84, y), new Vector2(160, 34), 24, Ink, TextAlignmentOptions.Left);
            Node(panel, name + "Line", new Vector2(110, y), new Vector2(170, 2));
            Image line = Ensure<Image>(panel.Find(name + "Line").gameObject);
            line.sprite = null;
            line.color = MenuUiBuilder.Divider;
            line.raycastTarget = false;
        }

        private static Image[] StatRow(RectTransform panel, string name, string icon, string text, float y)
        {
            Picture(panel, name + "Icon", icon, new Vector2(-170, y), new Vector2(32, 32));
            Label(panel, name + "Label", text, new Vector2(-92, y), new Vector2(100, 32), 22, Ink, TextAlignmentOptions.Left);
            RectTransform bar = Node(panel, name + "Bar", new Vector2(88, y), new Vector2(210, 26));
            var segments = new Image[CharacterDefinition.MaxRating];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = Pill(bar, "Segment" + i, MenuArtSetup.SliderTrack, new Vector2(-84 + i * 42, 0), new Vector2(40, 24));
                segments[i].color = FrontendCharacterDetailsView.EmptySegment;
            }
            return segments;
        }

        private static void TitlePlaque(RectTransform root, TMP_Text title)
        {
            Image plaque = Picture(root, "TitlePlaque", MenuArtSetup.TitlePlaque, new Vector2(0, 410), new Vector2(600, 220));
            DrawBehind(plaque.transform, title.transform);
            Place(title.rectTransform, new Vector2(0, 414), new Vector2(420, 72)); // the cloud sits above the ribbon
            StyleText(title, 52, Ink);
        }

        // ---------- Skill upgrade ----------

        private static void StyleSkillUpgrade(FrontendSkillUpgradeView view)
        {
            var root = (RectTransform)view.transform;
            Skin(root.gameObject, MenuArtSetup.Background, Color.white);
            TitlePlaque(root, Require<TMP_Text>(root, "SkillUpgradeTitle"));

            // Left: character card.
            RectTransform card = Node(root, "CharacterCard", new Vector2(-570, -40), new Vector2(600, 720));
            Skin(card.gameObject, MenuArtSetup.PanelMain, Color.white);
            card.SetSiblingIndex(1);
            Image pedestal = Picture(card, "Pedestal", MenuArtSetup.Pedestal, new Vector2(0, -228), new Vector2(480, 225));
            Image portrait = Picture(card, "Portrait", MenuArtSetup.SilhouetteComingSoon, new Vector2(0, 16), new Vector2(430, 430));
            DrawBehind(pedestal.transform, portrait.transform);
            Image ribbon = Pill(card, "NameRibbon", MenuArtSetup.SliderTrack, new Vector2(0, 280), new Vector2(300, 66), Accent);
            Picture(card, "Crown", MenuArtSetup.IconCrown, new Vector2(-206, 288), new Vector2(76, 52));
            Picture(card, "Clover", MenuArtSetup.Clover, new Vector2(196, 270), new Vector2(46, 44));
            TMP_Text name = Label(card, "CharacterName", "", new Vector2(0, 282), new Vector2(260, 56), 36, RibbonText);
            RemoveChild(card, "EpithetPill"); // user feedback: the title stays on character select only
            RemoveChild(card, "Epithet");
            TMP_Text epithet = null;
            DrawBehind(ribbon.transform, name.transform);
            Undo.RecordObject(view, "Menu-3 skill presentation");
            view.ConfigurePresentation(name, epithet, portrait,
                Require<TMP_Text>(root, "LowGradeSkillPanel/Summary"), Require<TMP_Text>(root, "HighGradeSkillPanel/Summary"));

            // Right: board, points row and the two skill cards.
            RectTransform board = Node(root, "SkillBoard", new Vector2(300, -40), new Vector2(1090, 720));
            Skin(board.gameObject, MenuArtSetup.PanelMain, Color.white);
            board.SetSiblingIndex(2);

            RectTransform summary = Require<RectTransform>(root, "ProgressSummary");
            Place(summary, new Vector2(300, 254), new Vector2(1000, 72));
            Image summaryImage = summary.GetComponent<Image>();
            Undo.RecordObject(summaryImage, "Clear summary");
            summaryImage.sprite = null;
            summaryImage.color = new Color(1f, 1f, 1f, 0f);
            Image pointsPill = Pill(summary, "PointsPill", MenuArtSetup.SliderTrack, new Vector2(-160, 0), new Vector2(440, 60), Accent);
            pointsPill.transform.SetAsFirstSibling();
            Picture(summary, "PointsClover", MenuArtSetup.Clover, new Vector2(-376, 2), new Vector2(76, 74));
            Place(view.SkillPointsText.rectTransform, new Vector2(-130, 0), new Vector2(360, 50));
            StyleText(view.SkillPointsText, 26, RibbonText);
            Place(view.CharacterLevelText.rectTransform, new Vector2(330, 16), new Vector2(330, 32));
            StyleText(view.CharacterLevelText, 22, Ink, TextAlignmentOptions.Right);
            Place(view.ExperienceText.rectTransform, new Vector2(330, -18), new Vector2(330, 28));
            StyleText(view.ExperienceText, 18, InkSoft, TextAlignmentOptions.Right);

            SkillCard(root, "LowGradeSkillPanel", 70, MenuArtSetup.IconSkillJunior, MenuArtSetup.Load(MenuArtSetup.SkillLowGrade));
            // The high-grade tile uses the same art as the in-game HUD (UserArtwork.HudIcon(3)).
            Sprite highGrade = AssetDatabase.LoadAssetAtPath<Sprite>(HighGradeSkillIconPath);
            if (highGrade == null) throw new InvalidOperationException("Missing " + HighGradeSkillIconPath);
            SkillCard(root, "HighGradeSkillPanel", -194, MenuArtSetup.IconSkillSenior, highGrade);

            Place(view.StatusText.rectTransform, new Vector2(300, -352), new Vector2(1000, 44));
            StyleText(view.StatusText, 20, InkSoft);
            Undo.RecordObject(view.StatusText, "Wrap");
            view.StatusText.textWrappingMode = TextWrappingModes.Normal;

            var back = (FrontendStartButton)view.BackButton;
            Vector2 backSize = new(250, 64);
            Place((RectTransform)back.transform, new Vector2(-775, -450), backSize);
            StyleButton(back, false, backSize, 24);
            BackArrow(back);
            var retry = (FrontendStartButton)view.RetryButton;
            Vector2 retrySize = new(280, 64);
            Place((RectTransform)retry.transform, new Vector2(700, -450), retrySize);
            StyleButton(retry, false, retrySize, 24);
        }

        private static void SkillCard(RectTransform root, string name, float y, string gradeIcon, Sprite skillIcon)
        {
            RectTransform card = Require<RectTransform>(root, name);
            Place(card, new Vector2(300, y), new Vector2(1000, 220));
            Image background = Skin(card.gameObject, MenuArtSetup.PanelMain, new Color(1f, 1f, 1f, 0.96f));
            background.pixelsPerUnitMultiplier = 2.4f;

            Image ribbon = Pill(card, "HeadingRibbon", MenuArtSetup.SliderTrack, new Vector2(-330, 110), new Vector2(270, 54), Accent);
            TMP_Text heading = Require<TMP_Text>(card, "Heading");
            Place(heading.rectTransform, new Vector2(-300, 110), new Vector2(170, 44));
            StyleText(heading, 26, RibbonText, TextAlignmentOptions.Left);
            DrawBehind(ribbon.transform, heading.transform);
            Picture(card, "GradeIcon", gradeIcon, new Vector2(-450, 116), new Vector2(74, 54));

            Image summaryPill = Pill(card, "SummaryPill", MenuArtSetup.SliderTrack, new Vector2(20, 110), new Vector2(300, 44));
            TMP_Text summary = Require<TMP_Text>(card, "Summary");
            Place(summary.rectTransform, new Vector2(20, 110), new Vector2(280, 40));
            StyleText(summary, 20, Ink);
            DrawBehind(summaryPill.transform, summary.transform);

            Image tile = Picture(card, "SkillTile", MenuArtSetup.PortraitSlot, new Vector2(-398, -22), new Vector2(134, 134));
            Image icon = Picture(tile.transform, "Icon", MenuArtSetup.SkillLowGrade, Vector2.zero, new Vector2(108, 108));
            icon.sprite = skillIcon;

            Image levelPill = Pill(card, "LevelPill", MenuArtSetup.ButtonSecondary, new Vector2(-104, 26), new Vector2(360, 58));
            TMP_Text level = Require<TMP_Text>(card, "SkillLevel");
            Place(level.rectTransform, new Vector2(-104, 26), new Vector2(300, 48));
            StyleText(level, 30, Ink);
            DrawBehind(levelPill.transform, level.transform);
            TMP_Text effect = Require<TMP_Text>(card, "SkillEffect");
            Place(effect.rectTransform, new Vector2(-104, -46), new Vector2(420, 60));
            StyleText(effect, 20, Ink);
            Undo.RecordObject(effect, "Wrap");
            effect.textWrappingMode = TextWrappingModes.Normal;

            var upgrade = Require<FrontendStartButton>(card, "UpgradeButton");
            Vector2 size = new(300, 84);
            Place((RectTransform)upgrade.transform, new Vector2(318, -18), size);
            StyleButton(upgrade, true, size, 24);
        }
    }
}
