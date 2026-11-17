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
    // Menu-2 (docs/26 §3.5): the nickname screen is the user's card scaled as one piece in the shared reference
    // frame. Controls sit in the card's pixel coordinates, keep the view references, and the back button is inside
    // the card (no bottom-left back on this screen) and there is no Erpin on the card.
    public static class Menu2NicknameVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify Menu-2 Nickname")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendTitleView title = MenuUiBuilder.FindSingle<FrontendTitleView>(scene);
            FrontendNicknameView view = MenuUiBuilder.FindSingle<FrontendNicknameView>(scene);
            Assert(title.NicknameView == view, "Title must open the only nickname view.");
            Assert(new SerializedObject(title).FindProperty("nicknamePanel").objectReferenceValue == view.gameObject,
                "Title must show and hide the nickname screen itself.");
            Assert(view.name == Menu2NicknameSetup.ScreenName && view.transform.parent == title.transform.Find("ReferenceFrame"),
                "Nickname screen must live in FrontendCanvas/ReferenceFrame.");
            Assert(scene.GetRootGameObjects().All(root => root.name != Menu2NicknameSetup.OldRootName),
                "The temporary NicknamePanel root must be gone.");
            Assert(!view.gameObject.activeSelf, "Nickname screen starts hidden; the title view opens it.");
            Transform screen = view.transform;
            Assert(screen.childCount == 1 && screen.GetChild(0).name == Menu2NicknameSetup.CardName,
                "The screen holds only the card (no leftover v3 parts or bottom-left back button).");

            var card = (RectTransform)screen.GetChild(0);
            Assert(card.sizeDelta == Menu2NicknameSetup.CardSize &&
                   card.localScale == Vector3.one * Menu2NicknameSetup.CardScale, "The card keeps its canvas and one uniform scale.");
            string[] names = card.Cast<Transform>().Select(child => child.name).ToArray();
            Assert(names.Length == names.Distinct().Count(), "Card has duplicate objects (setup rerun).");
            Image art = card.Find("CardArt")?.GetComponent<Image>();
            Assert(art != null && art.sprite != null && art.sprite.name == MenuArtSetup.NicknameCard && art.preserveAspect &&
                   !art.raycastTarget && art.type == Image.Type.Simple && art.rectTransform.sizeDelta == Menu2NicknameSetup.CardSize,
                "The card art is drawn whole, unsliced and non-interactive.");
            Assert(Text(card, "Title") == Menu2NicknameSetup.Title && Text(card, "SectionLabel") == Menu2NicknameSetup.Section,
                "Title and section text are TMP over the textless card.");

            Placed(view.NicknameInput.transform, Menu2NicknameSetup.InputRect, card);
            Placed(view.ValidationText.transform, Menu2NicknameSetup.RuleRect, card);
            Placed(view.BackButton.transform, Menu2NicknameSetup.BackRect, card);
            Placed(view.SubmitButton.transform, Menu2NicknameSetup.ConfirmRect, card);
            Assert(view.ErrorText.transform.parent == card && view.ProgressText.transform.parent == card,
                "Error and progress lines must be on the card.");
            foreach (Transform part in card.GetComponentsInChildren<Transform>(true).Where(part => part != card))
                Assert(part.localScale == Vector3.one && part.localPosition.z == 0f, part.name + " must keep unit scale on the card.");

            TMP_InputField input = view.NicknameInput;
            Image field = input.GetComponent<Image>();
            Assert(field.sprite == null && field.color.a == 0f && field.raycastTarget && input.targetGraphic == field,
                "The input draws nothing itself (the card has the field) but still takes clicks.");
            Assert(input.lineType == TMP_InputField.LineType.SingleLine && input.textViewport.GetComponent<RectMask2D>() != null,
                "Nickname input must clip one line of text.");
            Assert(((TMP_Text)input.placeholder).text == Menu2NicknameSetup.Placeholder, "Placeholder copy changed.");

            Assert(card.Find("ErpinMascot") == null, "The nickname card has no Erpin (user feedback).");

            Image warning = view.ErrorText.transform.Find("WarningIcon")?.GetComponent<Image>();
            Assert(warning != null && warning.sprite != null && warning.sprite.name == MenuArtSetup.IconWarning &&
                   !warning.raycastTarget, "The error line needs its warning icon as a child, shown with the text.");
            Assert(view.ErrorText.color == MenuUiBuilder.Warning &&
                   view.ErrorText.textWrappingMode == TextWrappingModes.Normal, "Error text must be red and wrap.");
            Assert(!view.ProgressText.gameObject.activeSelf && view.ProgressText.text == FrontendNicknameView.SubmittingText,
                "The progress line starts hidden.");

            ArtButton(view.SubmitButton, MenuArtSetup.NicknameConfirmButton);
            ArtButton(view.BackButton, MenuArtSetup.NicknameBackButton);
            Assert(input.navigation.selectOnDown == view.SubmitButton && view.SubmitButton.navigation.selectOnUp == input &&
                   view.SubmitButton.navigation.selectOnLeft == view.BackButton &&
                   view.BackButton.navigation.selectOnRight == view.SubmitButton, "Keyboard navigation must connect the controls.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                Assert(text.font == font, text.name + " must use the shared game font.");
            TMP_Text rule = view.ValidationText;
            rule.enableAutoSizing = false;
            rule.fontSize = rule.fontSizeMin;
            Assert(rule.GetPreferredValues(FrontendNicknameView.RuleMessage).x <= rule.rectTransform.rect.width + 0.5f,
                "The rule must fit between the card's clover dividers at its smallest size.");
            Debug.Log("Menu-2 nickname verification passed.");
        }

        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            Menu2NicknameSetup.Setup();
            Menu2NicknameSetup.Setup(); // rerun must not add objects
            Verify();
            Week13FrontendVerification.Verify(); // references, canvas, glyphs of every screen (reopens the scene)
            Capture();
        });

        public static void VerifyBatch() => Batch(Verify);

        [MenuItem("Trickal Fan Game/Menu/Capture Menu-2 Nickname")]
        public static void Capture()
        {
            MenuScreenCapture.Capture("menu2-nickname", frame => Show(frame, null));
            MenuScreenCapture.Capture("menu2-nickname-error", frame =>
                Show(frame, "이미 사용 중인 닉네임입니다. 다른 닉네임을 입력해주세요."));
        }

        // The view sets these at runtime; the capture shows the empty screen or one with a typed name and an error.
        private static void Show(RectTransform frame, string error)
        {
            MenuScreenCapture.ShowOnly(frame, Menu2NicknameSetup.ScreenName);
            FrontendNicknameView view = frame.GetComponentInChildren<FrontendNicknameView>(true);
            view.ValidationText.text = FrontendNicknameView.RuleMessage;
            view.ErrorText.gameObject.SetActive(false);
            if (error == null) return;
            view.ErrorText.ForceMeshUpdate(true);
            view.ShowError(error);
            view.NicknameInput.SetTextWithoutNotify("에르핀짱");
            view.NicknameInput.textComponent.text = "에르핀짱";
            view.NicknameInput.placeholder.enabled = false;
        }

        private static string Text(Transform card, string name) => card.Find(name)?.GetComponent<TMP_Text>()?.text;

        // The object must sit exactly on its layout.json rect (card pixels, top-left origin).
        private static void Placed(Transform part, Rect pixels, RectTransform card)
        {
            var rect = (RectTransform)part;
            Assert(rect.parent == card && rect.anchoredPosition == Menu2NicknameSetup.Center(pixels) &&
                   rect.sizeDelta == pixels.size, $"{part.name} must sit on its card layout rect {pixels}.");
        }

        private static void ArtButton(Button button, string sprite)
        {
            Assert(button is FrontendStartButton, $"{button.name} must use the shared menu button behaviour.");
            Image image = button.GetComponent<Image>();
            Assert(image != null && image.sprite != null && image.sprite.name == sprite && image.preserveAspect &&
                   button.targetGraphic == image, $"{button.name} must use the {sprite} art.");
            Assert(button.GetComponentInChildren<TMP_Text>(true) == null, $"{button.name}: the label is in the art, no TMP.");
            Assert(button.GetComponents<MenuPopEffect>().Length == 1 && button.transform.Find("HoverBorder") == null,
                $"{button.name} must grow on hover instead of showing outlines.");
            Assert(button.onClick.GetPersistentEventCount() == 0, $"{button.name} must not have persistent actions.");
            Assert(button.navigation.mode == Navigation.Mode.Explicit, $"{button.name} needs explicit navigation.");
        }

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
