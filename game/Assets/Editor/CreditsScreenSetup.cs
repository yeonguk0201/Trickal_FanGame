using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Credits-0. Builds the credits screen and the two routes into it. The entries themselves are not here:
    // they come from Assets/Resources/Credits.json, which this setup only reads to preload TMP glyphs.
    public static class CreditsScreenSetup
    {
        public const string PanelName = "CreditsPanel";
        public const string TitleButtonName = "TitleCreditsButton";
        public const string HomeButtonName = "HomeCreditsButton";
        public const string ContentName = "Content";
        public const string SectionTemplateName = "Credits Section Template";
        public const string EntryTemplateName = "Credits Entry Template";

        public const string BackLabel = "뒤로";
        public const string ButtonLabel = "크레딧";

        private static readonly Vector2 PanelSize = new(1920, 1080);
        private static readonly Vector2 ViewportSize = new(1480, 720);
        private static readonly Vector2 RowSize = new(1400, 92);

        [MenuItem("Trickal Fan Game/Credits/Setup Credits Screen")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Credits-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Week13FrontendSetup.ScenePath))
                throw new InvalidOperationException("FrontendScene is missing. Run Setup Frontend Flow first.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null)
                throw new InvalidOperationException(
                    $"The Frontend TMP font is missing at {Week13FrontendSetup.FontPath}. Run Setup Frontend Flow first; " +
                    "Credits-0 must not create a second font asset.");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendTitleView titleView = FindSingle<FrontendTitleView>(scene, "FrontendTitleView");
            FrontendHomeView homeView = FindSingle<FrontendHomeView>(scene, "FrontendHomeView");
            RectTransform frame = FindSingle<FrontendLayout>(scene, "ReferenceFrame").transform as RectTransform;
            if (frame == null) throw new InvalidOperationException("The Frontend reference frame is missing.");
            if (titleView.TitlePanel == null) throw new InvalidOperationException("The title panel is missing.");
            if (homeView.QuitButton == null) throw new InvalidOperationException("The home menu is missing.");
            RectTransform homeMenu = homeView.QuitButton.transform.parent as RectTransform;
            if (homeMenu == null) throw new InvalidOperationException("The home menu parent is missing.");

            CreditsCatalog.Section[] sections = LoadSections();
            PreloadGlyphs(font, sections);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Credits Screen");

            FrontendCreditsView creditsView = BuildPanel(frame, font);
            FrontendStartButton titleButton = CreditsButton(titleView.TitlePanel.transform, TitleButtonName,
                new Vector2(0, -330), font);
            FrontendStartButton homeButton = CreditsButton(homeMenu, HomeButtonName, new Vector2(0, -400), font);

            // Keyboard reachability: the home menu loop is owned by Setup Frontend Flow, so this only adds the
            // step down into the credits button and the step back up. Re-running that setup restores the plain
            // four-button loop, which the Credits-0 verifier then reports as missing.
            titleView.StartButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = titleButton,
                selectOnUp = titleButton
            };
            titleButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = titleView.StartButton,
                selectOnDown = titleView.StartButton
            };
            Navigation quitNavigation = homeView.QuitButton.navigation;
            quitNavigation.mode = Navigation.Mode.Explicit;
            quitNavigation.selectOnDown = homeButton;
            homeView.QuitButton.navigation = quitNavigation;
            homeButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = homeView.QuitButton,
                selectOnDown = homeView.GameStartButton
            };

            Undo.RecordObject(titleView, "Configure credits route");
            titleView.ConfigureCredits(creditsView.gameObject, creditsView, titleButton);
            Undo.RecordObject(homeView, "Configure credits route");
            homeView.ConfigureCredits(homeButton);

            creditsView.gameObject.SetActive(false);
            foreach (Component component in creditsView.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(titleView);
            EditorUtility.SetDirty(homeView);
            EditorUtility.SetDirty(titleButton);
            EditorUtility.SetDirty(homeButton);
            EditorUtility.SetDirty(titleView.StartButton);
            EditorUtility.SetDirty(homeView.QuitButton);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Week13FrontendSetup.ScenePath))
                throw new InvalidOperationException("FrontendScene save failed during Credits-0 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Credits-0 setup complete. {CreditsCatalog.AllEntries(sections).Count()} credit entries " +
                      "are loaded from Resources/Credits.json, reachable from the title screen and from Home.");
        }

        public static CreditsCatalog.Section[] LoadSections()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsCatalog.AssetPath);
            if (asset == null)
                throw new InvalidOperationException($"The credits data is missing at {CreditsCatalog.AssetPath}.");
            CreditsCatalog.Section[] sections = CreditsCatalog.Parse(asset.text);
            if (sections.Length == 0)
                throw new InvalidOperationException($"{CreditsCatalog.AssetPath} holds no credit sections.");
            return sections;
        }

        // The data is free text, so the glyphs it needs cannot be listed in code; they are read from it instead.
        private static void PreloadGlyphs(TMP_FontAsset font, CreditsCatalog.Section[] sections)
        {
            string characters = ButtonLabel + BackLabel + FrontendCreditsView.ScreenTitle +
                                FrontendCreditsView.EmptyMessage + CreditsCatalog.CollectGlyphSource(sections);
            characters = new string(characters.Distinct().Where(c => !char.IsControl(c)).ToArray());
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("The Frontend font cannot render the credits copy: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }

        private static FrontendCreditsView BuildPanel(RectTransform frame, TMP_FontAsset font)
        {
            RectTransform root = Rect(frame, PanelName, Vector2.zero, PanelSize);
            root.SetAsLastSibling();
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.035f, 0.055f, 0.095f, 1f);
            background.raycastTarget = true;

            TMP_Text heading = Text(root, "CreditsTitle", FrontendCreditsView.ScreenTitle, new Vector2(0, 440),
                new Vector2(900, 72), 44, font, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;

            RectTransform viewport = Rect(root, "CreditsViewport", new Vector2(0, -10), ViewportSize);
            Image viewportImage = Component<Image>(viewport.gameObject);
            viewportImage.color = new Color(0.02f, 0.03f, 0.055f, 0.85f);
            viewportImage.raycastTarget = true;
            Component<RectMask2D>(viewport.gameObject);

            RectTransform content = Rect(viewport, ContentName, Vector2.zero, new Vector2(-24, 0));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0, -12);
            VerticalLayoutGroup layout = Component<VerticalLayoutGroup>(content.gameObject);
            layout.padding = new RectOffset(16, 16, 0, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = Component<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = Component<ScrollRect>(viewport.gameObject);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            // Templates carry no copy: their text is filled from the data at runtime, which also keeps the
            // Frontend layout verifier from measuring placeholder strings.
            TMP_Text sectionTemplate = Text(content, SectionTemplateName, string.Empty, Vector2.zero,
                new Vector2(RowSize.x, 48), 28, font, TextAlignmentOptions.Left);
            sectionTemplate.fontStyle = FontStyles.Bold;
            sectionTemplate.color = new Color(0.55f, 0.92f, 0.86f);
            LayoutElement sectionLayout = Component<LayoutElement>(sectionTemplate.gameObject);
            sectionLayout.preferredHeight = 48f;
            sectionTemplate.gameObject.SetActive(false);

            RectTransform entryRect = Rect(content, EntryTemplateName, Vector2.zero, RowSize);
            LayoutElement entryLayout = Component<LayoutElement>(entryRect.gameObject);
            entryLayout.preferredHeight = RowSize.y;
            Image entryImage = Component<Image>(entryRect.gameObject);
            entryImage.color = new Color(0.075f, 0.105f, 0.155f, 1f);
            entryImage.raycastTarget = false;
            TMP_Text primary = Text(entryRect, "Primary", string.Empty, new Vector2(-170, 22),
                new Vector2(1020, 34), 24, font, TextAlignmentOptions.Left);
            TMP_Text usage = Text(entryRect, "Usage", string.Empty, new Vector2(570, 22),
                new Vector2(240, 30), 19, font, TextAlignmentOptions.Right);
            usage.color = new Color(0.72f, 0.8f, 0.88f);
            TMP_Text license = Text(entryRect, "License", string.Empty, new Vector2(0, -22),
                new Vector2(1360, 34), 19, font, TextAlignmentOptions.Left);
            license.color = new Color(0.8f, 0.86f, 0.94f);
            FrontendCreditsEntryView entryTemplate = Component<FrontendCreditsEntryView>(entryRect.gameObject);
            entryTemplate.ConfigureVisuals(primary, license, usage);
            entryRect.gameObject.SetActive(false);

            TMP_Text empty = Text(root, "CreditsEmptyMessage", FrontendCreditsView.EmptyMessage, new Vector2(0, -10),
                new Vector2(900, 48), 24, font, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);

            FrontendStartButton back = CreditsButton(root, "CreditsBackButton", new Vector2(0, -430), font, BackLabel);
            back.navigation = new Navigation { mode = Navigation.Mode.None };

            FrontendCreditsView view = Component<FrontendCreditsView>(root.gameObject);
            view.Configure(scroll, content, sectionTemplate, entryTemplate, empty, back);
            return view;
        }

        private static FrontendStartButton CreditsButton(Transform parent, string name, Vector2 position,
            TMP_FontAsset font, string label = ButtonLabel)
        {
            RectTransform rect = Rect(parent, name, position, new Vector2(220, 52));
            Image image = Component<Image>(rect.gameObject);
            image.color = new Color(0.18f, 0.68f, 0.64f);
            FrontendStartButton button = Component<FrontendStartButton>(rect.gameObject);
            button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            Text(rect, "Label", label, Vector2.zero, new Vector2(204, 44), 24, font, TextAlignmentOptions.Center)
                .color = new Color(0.025f, 0.06f, 0.08f);
            button.ConfigureBorders(Border(rect, "HoverBorder", 2, 0), Border(rect, "FocusBorder", 3, 2));
            return button;
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"FrontendScene requires exactly one {label}; found {matches.Length}.");
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
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Overflow, never Ellipsis: a truncated line could drop the author, link or modification notice
            // that CC BY 4.0 requires. The verifier checks that the required lines fit instead.
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static GameObject Border(RectTransform parent, string name, float thickness, float gap)
        {
            RectTransform root = Rect(parent, name, Vector2.zero, parent.sizeDelta + Vector2.one * (gap + thickness) * 2);
            Vector2 size = root.sizeDelta;
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                Vector2 edgeSize = horizontal ? new Vector2(size.x, thickness) : new Vector2(thickness, size.y);
                float direction = i % 2 == 0 ? 1 : -1;
                Vector2 position = horizontal
                    ? new Vector2(0, direction * (size.y - thickness) / 2)
                    : new Vector2(direction * (size.x - thickness) / 2, 0);
                Image edge = Component<Image>(Rect(root, "Edge" + i, position, edgeSize).gameObject);
                edge.color = Color.white;
                edge.raycastTarget = false;
            }
            return root.gameObject;
        }
    }
}
