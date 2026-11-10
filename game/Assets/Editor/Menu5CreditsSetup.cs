using System;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;

namespace TrickalFanGame.Editor
{
    // Menu-5 (docs/26 §3.5): the credits screen in the magic-library look. Credits-0 still builds the screen and its
    // data; it calls Apply at the end, so rerunning Credits-0 keeps this look. Rows, text and licences are unchanged.
    public static class Menu5CreditsSetup
    {
        public const string SectionRowTemplateName = "Credits Section Row Template";
        public const float EntryHeight = 104f;
        public static readonly Vector2 BackSize = new(300, 84);
        // Row width is the 1220 viewport minus the content inset and padding (1180); the licence keeps 84 + 16 free.
        public const float LicenseWidth = 1080f;
        public const float LicenseHeight = 44f; // two lines at 16px

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-5 Credits")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Menu-5 credits setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Menu-5 Credits");
            Apply(FindSingle<FrontendCreditsView>(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Frontend save failed.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("Menu-5 credits setup complete.");
        }

        public static void Apply(FrontendCreditsView view)
        {
            MenuArtSetup.Apply();
            var root = (RectTransform)view.transform;
            Skin(root.gameObject, MenuArtSetup.Background, Color.white, raycast: true);

            RectTransform board = Node(root, "CreditsBoard", new Vector2(0, -36), new Vector2(1320, 740));
            Skin(board.gameObject, MenuArtSetup.PanelMain, Color.white);
            board.SetSiblingIndex(0);
            Image erpin = Picture(root, "ErpinSticker", MenuArtSetup.SilhouetteComingSoon, new Vector2(-790, -300), new Vector2(280, 280));
            erpin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Player/Character_Erpin.png");
            erpin.transform.SetSiblingIndex(1);
            Picture(board, "BottomClover", MenuArtSetup.Clover, new Vector2(0, -362), new Vector2(60, 58));

            TMP_Text title = Require<TMP_Text>(root, "CreditsTitle");
            Image plaque = Picture(root, "TitlePlaque", MenuArtSetup.TitlePlaque, new Vector2(0, 404), new Vector2(600, 220));
            DrawBehind(plaque.transform, title.transform);
            Place(title.rectTransform, new Vector2(0, 408), new Vector2(420, 72));
            StyleText(title, 52, Ink);
            title.fontStyle = FontStyles.Normal;

            RectTransform viewport = view.ScrollRect.viewport;
            Place(viewport, new Vector2(0, -40), new Vector2(1220, 610));
            Image viewportImage = viewport.GetComponent<Image>();
            Undo.RecordObject(viewportImage, "Clear viewport");
            viewportImage.sprite = null;
            viewportImage.color = new Color(1f, 1f, 1f, 0f); // still catches wheel and drag for scrolling
            VerticalLayoutGroup layout = view.Content.GetComponent<VerticalLayoutGroup>();
            Undo.RecordObject(layout, "Row spacing");
            layout.spacing = 10f;
            layout.padding = new RectOffset(8, 8, 6, 16);

            TMP_Text note = view.SectionTemplate;
            StyleText(note, 26, InkSoft, TextAlignmentOptions.Left);
            note.fontStyle = FontStyles.Normal;
            GameObject sectionRow = SectionRow(view.Content);
            Undo.RecordObject(view, "Credits section row");
            view.ConfigureSectionRow(sectionRow, InkSoft);
            StyleEntry(view.EntryTemplate);

            if (view.EmptyMessageText != null) StyleText(view.EmptyMessageText, 24, Ink);

            var back = (FrontendStartButton)view.BackButton;
            Place((RectTransform)back.transform, new Vector2(0, -452), BackSize);
            StyleButton(back, true, BackSize, 30);
            BackArrow(back);
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
        }

        private static GameObject SectionRow(RectTransform content)
        {
            RectTransform row = Node(content, SectionRowTemplateName, Vector2.zero, new Vector2(0, 64));
            LayoutElement element = Ensure<LayoutElement>(row.gameObject);
            element.preferredHeight = 64f;
            Image ribbon = Pill(row, "Ribbon", MenuArtSetup.SliderTrack, Vector2.zero, new Vector2(640, 56), Accent);
            Anchor(ribbon.rectTransform, 0f, new Vector2(0, 0), new Vector2(640, 56));
            Image tile = Pill(ribbon.transform, "IconTile", MenuArtSetup.ButtonSecondary, Vector2.zero, new Vector2(48, 44));
            Anchor(tile.rectTransform, 0f, new Vector2(8, 0), new Vector2(48, 44));
            Picture(tile.transform, "Glyph", MenuArtSetup.IconMusic, Vector2.zero, new Vector2(28, 30));
            TMP_Text heading = Label(ribbon.transform, "Heading", null, Vector2.zero, new Vector2(560, 44), 24, RibbonText,
                TextAlignmentOptions.Left);
            Anchor(heading.rectTransform, 0f, new Vector2(70, 0), new Vector2(556, 44));
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 18f;
            heading.fontSizeMax = 24f;
            Image clover = Picture(row, "Clover", MenuArtSetup.Clover, Vector2.zero, new Vector2(40, 38));
            Anchor(clover.rectTransform, 0f, new Vector2(650, -2), new Vector2(40, 38));
            row.SetSiblingIndex(0);
            row.gameObject.SetActive(false);
            return row.gameObject;
        }

        private static void StyleEntry(FrontendCreditsEntryView entry)
        {
            var rect = (RectTransform)entry.transform;
            Ensure<LayoutElement>(rect.gameObject).preferredHeight = EntryHeight;
            Undo.RecordObject(rect, "Entry height");
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, EntryHeight);
            Image background = Skin(rect.gameObject, MenuArtSetup.PanelMain, Color.white);
            background.pixelsPerUnitMultiplier = 3.2f;

            Image icon = Picture(rect, "KindIcon", MenuArtSetup.IconStarburst, Vector2.zero, new Vector2(40, 40));
            Anchor(icon.rectTransform, 0f, new Vector2(28, 10), new Vector2(40, 40));
            Anchor(entry.PrimaryText.rectTransform, 0f, new Vector2(84, 26), new Vector2(800, 34));
            StyleText(entry.PrimaryText, 22, Ink, TextAlignmentOptions.Left);
            Anchor(entry.LicenseText.rectTransform, 0f, new Vector2(84, -18), new Vector2(LicenseWidth, LicenseHeight));
            StyleText(entry.LicenseText, 16, InkSoft, TextAlignmentOptions.Left);
            // The licence line is the attribution; it gets the full row width, wraps to a second line and is never cut.
            Undo.RecordObject(entry.LicenseText, "Licence wrap");
            entry.LicenseText.textWrappingMode = TextWrappingModes.Normal;
            entry.LicenseText.alignment = TextAlignmentOptions.TopLeft;

            Image pill = Pill(rect, "UsagePill", MenuArtSetup.SliderTrack, Vector2.zero, new Vector2(250, 36), new Color(1f, 0.93f, 0.95f));
            Anchor(pill.rectTransform, 1f, new Vector2(-22, 26), new Vector2(250, 36));
            Anchor(entry.UsageText.rectTransform, 1f, new Vector2(-32, 26), new Vector2(230, 32));
            StyleText(entry.UsageText, 18, Ink);
            Undo.RecordObject(entry.UsageText, "Usage fit");
            entry.UsageText.enableAutoSizing = true;
            entry.UsageText.fontSizeMin = 16f;
            entry.UsageText.fontSizeMax = 18f;
            DrawBehind(pill.transform, entry.UsageText.transform);
            Undo.RecordObject(entry, "Kind icon");
            entry.ConfigureKindIcon(icon, MenuArtSetup.Load(MenuArtSetup.IconStarburst), MenuArtSetup.Load(MenuArtSetup.IconMusic));
        }
    }
}
