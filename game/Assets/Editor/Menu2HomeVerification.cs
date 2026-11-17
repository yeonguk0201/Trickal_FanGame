using System;
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
    // Menu-2 (docs/26 §3.5): the title shows the home background, the game name emblem and the clover start button;
    // home buttons use the shared menu button art. Every button grows on hover/focus and has no outline.
    public static class Menu2HomeVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify Menu-2 Title and Home")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendTitleView title = MenuUiBuilder.FindSingle<FrontendTitleView>(scene);
            Image background = title.transform.Find("Background")?.GetComponent<Image>();
            Assert(background != null && background.sprite != null &&
                   AssetDatabase.GetAssetPath(background.sprite) == Menu2HomeSetup.HomeBackgroundPath &&
                   background.color == Color.white, "Title must show the home background.");
            Assert(background.GetComponent<AspectRatioFitter>()?.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent,
                "Title background must cover the screen without stretching.");
            Transform panel = title.TitlePanel.transform;
            Image logo = panel.Find("GameLogo")?.GetComponent<Image>();
            Assert(logo != null && logo.sprite != null && logo.sprite.name == MenuArtSetup.TitleLogo && logo.preserveAspect,
                "Title must show the game name emblem.");
            Assert(!logo.transform.Find("LogoText").gameObject.activeSelf && !panel.Find("Subtitle").gameObject.activeSelf,
                "The old text logo and subtitle must be hidden.");
            Image start = title.StartButton.GetComponent<Image>();
            Assert(start.sprite != null && start.sprite.name == MenuArtSetup.TitleStartButton && start.preserveAspect &&
                   title.StartButton.targetGraphic == start, "GAME START must use the clover button art.");
            Assert(((RectTransform)title.StartButton.transform).sizeDelta == Menu2HomeSetup.StartSize, "Start button size changed.");
            Assert(title.StartButton.transform.Find("HoverBorder") == null &&
                   title.StartButton.GetComponents<MenuPopEffect>().Length == 1, "GAME START must grow, not show outlines.");
            TMP_Text startLabel = title.StartButton.transform.Find("Label").GetComponent<TMP_Text>();
            Assert(startLabel.GetPreferredValues(startLabel.text).x <= startLabel.rectTransform.sizeDelta.x,
                "GAME START label is wider than its box.");
            Assert(title.CreditsButton == null, "The title shows only GAME START; credits are on the home menu.");
            Assert(!Overlaps((RectTransform)title.StartButton.transform, title.StatusText.rectTransform) &&
                   !Overlaps(logo.rectTransform, (RectTransform)title.StartButton.transform),
                "Title logo, start button and status must not overlap.");

            FrontendHomeView home = MenuUiBuilder.FindSingle<FrontendHomeView>(scene);
            Transform homeSafe = home.HomePanel.transform.Find("SafeArea");
            Assert(!homeSafe.Find("HomeTitle").gameObject.activeSelf, "The home text title must be hidden.");
            Image homeLogo = homeSafe.Find("HomeLogo")?.GetComponent<Image>();
            Assert(homeLogo != null && homeLogo.sprite != null && homeLogo.sprite.name == MenuArtSetup.TitleLogo &&
                   homeLogo.preserveAspect && homeLogo.rectTransform.sizeDelta.x < logo.rectTransform.sizeDelta.x,
                "Home must show a smaller game name emblem.");
            Assert(home.HomePanel.transform.Find("LeftDecoration") == null && home.HomePanel.transform.Find("RightDecoration") == null,
                "The temporary side boxes must be gone from home.");
            RectTransform[] stack =
            {
                homeLogo.rectTransform, home.WelcomeText.rectTransform, (RectTransform)home.GameStartButton.transform,
                (RectTransform)home.SkillUpgradeButton.transform, (RectTransform)home.SettingsButton.transform,
                (RectTransform)home.QuitButton.transform, (RectTransform)home.CreditsButton.transform
            };
            for (int i = 1; i < stack.Length; i++)
                Assert(!Overlaps(stack[i - 1], stack[i]) && stack[i].anchoredPosition.y < stack[i - 1].anchoredPosition.y,
                    $"Home {stack[i - 1].name} and {stack[i].name} must stack top to bottom without overlap.");
            Check(home.GameStartButton, MenuArtSetup.ButtonPrimary, Menu2HomeSetup.MenuButtonSize);
            Check(home.SkillUpgradeButton, MenuArtSetup.ButtonSecondary, Menu2HomeSetup.MenuButtonSize);
            Check(home.SettingsButton, MenuArtSetup.ButtonSecondary, Menu2HomeSetup.MenuButtonSize);
            Check(home.QuitButton, MenuArtSetup.ButtonSecondary, Menu2HomeSetup.MenuButtonSize);
            Assert(home.CreditsButton != null, "Home requires the credits button (Credits-0).");
            Check(home.CreditsButton, MenuArtSetup.ButtonSecondary, Menu2HomeSetup.CreditsButtonSize);
            Debug.Log("Menu-2 title and home verification passed.");
        }

        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            Menu2HomeSetup.Setup();
            Menu2HomeSetup.Setup(); // rerun must not add objects
            Verify();
            Week13FrontendVerification.Verify(); // references, focus, safe area and text bounds of every screen
            Capture();
        });

        public static void VerifyBatch() => Batch(Verify);

        [MenuItem("Trickal Fan Game/Menu/Capture Menu-2 Title and Home")]
        public static void Capture()
        {
            MenuScreenCapture.Capture("menu2-title", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, "SafeArea");
                FrontendTitleView title = frame.GetComponentInParent<FrontendTitleView>();
                foreach (Transform child in frame.Find("SafeArea"))
                    child.gameObject.SetActive(child.gameObject == title.TitlePanel);
            });
            MenuScreenCapture.Capture("menu2-home", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, "HomeScreen");
                FrontendHomeView home = frame.GetComponentInChildren<FrontendHomeView>(true);
                home.HomePanel.SetActive(true);
                home.DestinationPanel.SetActive(false);
                home.SettingsPanel.SetActive(false);
                home.CharacterSelectionPanel.SetActive(false);
                home.SkillUpgradePanel.SetActive(false);
                home.WelcomeText.text = "교주 님, 환영합니다";
            });
        }

        private static void Check(Button button, string sprite, Vector2 size)
        {
            Assert(button is FrontendStartButton, $"{button?.name} must be a FrontendStartButton.");
            Image image = button.GetComponent<Image>();
            Assert(image != null && image.sprite != null && image.sprite.name == sprite && image.type == Image.Type.Sliced,
                $"{button.name} must use the sliced {sprite} art.");
            Assert(image.color == Color.white && button.targetGraphic == image, $"{button.name} keeps the art untinted.");
            Assert(((RectTransform)button.transform).sizeDelta == size, $"{button.name} must be {size.x} x {size.y}.");
            Assert(button.transform.Find("HoverBorder") == null && button.transform.Find("FocusBorder") == null,
                $"{button.name} must not have outline borders.");
            Assert(button.GetComponents<MenuPopEffect>().Length == 1, $"{button.name} needs one hover grow effect.");
            Assert(button.navigation.mode == Navigation.Mode.Explicit, $"{button.name} keeps keyboard navigation.");
            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            Assert(label != null && !string.IsNullOrEmpty(label.text), $"{button.name} keeps its label.");
            Assert(label.GetPreferredValues(label.text).x <= label.rectTransform.rect.width + 0.5f,
                $"{button.name}: label is wider than the button.");
            Assert(button.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Label") == 1,
                $"{button.name} has duplicate labels.");
        }

        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            Rect first = Bounds(a), second = Bounds(b);
            return first.Overlaps(second);
        }

        // Centre-anchored rects in the same safe area; position and size are enough.
        private static Rect Bounds(RectTransform rect) =>
            new(rect.anchoredPosition - rect.sizeDelta / 2f, rect.sizeDelta);

        private static void Batch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
