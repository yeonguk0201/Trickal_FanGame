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
    // Menu-2 (docs/26 §3.5): title and home in the magic-library look. The title shows only GAME START; credits
    // are on the home menu. Only the look changes; names, navigation and
    // FrontendTitleView/FrontendHomeView references stay. Credits-0 setup calls ApplyTitle and Apply at the end, so
    // rerunning it keeps this look (Frontend Flow setup is retired, Verify-0).
    public static class Menu2HomeSetup
    {
        public const string GameName = "볼따구의 번제";
        public const string HomeBackgroundPath = "Assets/Resources/UserArtwork/home-background.png";

        public static readonly Vector2 MenuButtonSize = new(360, 64); // Week13 Frontend verification keeps this size
        public static readonly Vector2 CreditsButtonSize = new(240, 60);

        // Title layout inside the title safe area (1792 x 972). The logo and button keep their art's aspect ratio.
        public static readonly Vector2 LogoPosition = new(0, 170);
        public static readonly Vector2 LogoSize = new(880, 486);
        public static readonly Vector2 StartPosition = new(0, -290);
        public static readonly Vector2 StartSize = new(660, 222);
        public const float StartLabelSize = 40f;
        public static readonly Vector2 StatusPosition = new(0, -440);

        // Home layout inside the home safe area: a smaller emblem, the welcome line, four buttons, then credits.
        public static readonly Vector2 HomeLogoPosition = new(0, 260);
        public static readonly Vector2 HomeLogoSize = new(520, 287);
        public static readonly Vector2 WelcomePosition = new(0, 80);
        public static readonly float[] HomeButtonY = { -10, -95, -180, -265 };
        public static readonly Vector2 HomeCreditsPosition = new(0, -355);
        // The label sits on the mint pill, below the clover crest (measured on UI_Menu_Title_StartButton).
        private static readonly Vector2 StartLabelOffset = new(0, -24);
        private static readonly Vector2 StartLabelBox = new(400, 60);
        private static readonly Color StartLabelColor = new(0.02f, 0.16f, 0.17f);

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-2 Title and Home")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Menu-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Menu-2 Title and Home");
            ApplyTitle(FindSingle<FrontendTitleView>(scene));
            Apply(FindSingle<FrontendHomeView>(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Frontend save failed.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("Menu-2 setup complete. Title and home use the magic-library parts.");
        }

        public static void ApplyTitle(FrontendTitleView title)
        {
            MenuArtSetup.Apply();
            AddGlyphs(GameName);

            // Same picture as the home screen (FrontendHomeView loads it at runtime), behind every Frontend screen.
            Image background = Require<Image>(title.transform, "Background");
            Sprite home = AssetDatabase.LoadAssetAtPath<Sprite>(HomeBackgroundPath);
            if (home == null) throw new InvalidOperationException("Missing home background sprite: " + HomeBackgroundPath);
            Undo.RecordObject(background, "Title background");
            background.sprite = home;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            AspectRatioFitter fit = Ensure<AspectRatioFitter>(background.gameObject);
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = home.rect.width / home.rect.height;

            Transform panel = title.TitlePanel.transform;
            // The emblem carries the game name, so the old text logo and subtitle are hidden (kept for reruns).
            Image logo = Require<Image>(panel, "GameLogo");
            Place(logo.rectTransform, LogoPosition, LogoSize);
            Skin(logo.gameObject, MenuArtSetup.TitleLogo, Color.white, preserveAspect: true);
            Deactivate(logo.transform, "LogoText");
            Deactivate(panel, "Subtitle");

            var start = (FrontendStartButton)title.StartButton;
            StyleStartButton(start);

            Place(title.StatusText.rectTransform, StatusPosition, new Vector2(900, 56));
            foreach (Component component in title.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
        }

        public static void Apply(FrontendHomeView home)
        {
            MenuArtSetup.Apply();
            AddGlyphs(GameName);
            Transform homePanel = home.HomePanel.transform;
            Transform safe = homePanel.Find("SafeArea");
            if (safe == null) throw new InvalidOperationException("Missing HomePanel/SafeArea.");
            // Temporary side bars from the first Frontend UI (teal and navy boxes); the background art replaces them.
            RemoveChild(homePanel, "LeftDecoration");
            RemoveChild(homePanel, "RightDecoration");

            // The title emblem, smaller, replaces the text title (kept hidden for reruns).
            Require<TMP_Text>(safe, "HomeTitle");
            Deactivate(safe, "HomeTitle");
            Picture(safe, "HomeLogo", MenuArtSetup.TitleLogo, HomeLogoPosition, HomeLogoSize);
            Place(home.WelcomeText.rectTransform, WelcomePosition, home.WelcomeText.rectTransform.sizeDelta);

            // Game start is the main action (lime); the other entries use the cream secondary button.
            Button[] buttons = { home.GameStartButton, home.SkillUpgradeButton, home.SettingsButton, home.QuitButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                Place((RectTransform)buttons[i].transform, new Vector2(0, HomeButtonY[i]), MenuButtonSize);
                Style(buttons[i], i == 0, MenuButtonSize);
            }
            // Credits-0 adds this button later; the first Frontend Flow run has none yet.
            if (home.CreditsButton != null)
            {
                Place((RectTransform)home.CreditsButton.transform, HomeCreditsPosition, CreditsButtonSize);
                Style(home.CreditsButton, false, CreditsButtonSize, 24f);
            }
            foreach (Component component in homePanel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        // The start button art has a clover crest, so it is not 9-sliced: it keeps its aspect ratio and the game font
        // label sits on the pill. Hover and focus grow it like the other menu buttons.
        private static void StyleStartButton(FrontendStartButton button)
        {
            var rect = (RectTransform)button.transform;
            Place(rect, StartPosition, StartSize);
            Image image = Skin(button.gameObject, MenuArtSetup.TitleStartButton, Color.white, raycast: true,
                preserveAspect: true);
            Undo.RecordObject(button, "Style " + button.name);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors();
            TMP_Text label = Require<TMP_Text>(rect, "Label");
            StyleText(label, StartLabelSize, StartLabelColor);
            Place(label.rectTransform, StartLabelOffset, StartLabelBox);
            RemoveChild(rect, "HoverBorder");
            RemoveChild(rect, "FocusBorder");
            button.ConfigureBorders(null, null);
            Pop(button);
        }

        private static void Style(Button button, bool primary, Vector2 size, float fontSize = 26f)
        {
            if (button is not FrontendStartButton menuButton)
                throw new InvalidOperationException($"{button?.name ?? "Home button"} must be a FrontendStartButton.");
            StyleButton(menuButton, primary, size, fontSize);
            foreach (Component component in menuButton.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        // The shared TMP font is dynamic; adding the new title glyphs keeps them in the atlas for builds.
        private static void AddGlyphs(string characters)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Missing Frontend font: " + Week13FrontendSetup.FontPath);
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing glyphs in the default font: " + missing);
            EditorUtility.SetDirty(font);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }
    }
}
