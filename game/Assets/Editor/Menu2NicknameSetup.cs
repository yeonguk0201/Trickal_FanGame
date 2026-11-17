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
    // Menu-2 (docs/26 §3.5): the nickname screen as the user's card (output/menu-components-v4/README.md). The card
    // art keeps its 1448 x 1086 canvas; every control is a child of one card root placed in the art's own pixel
    // coordinates (layout.json, top-left origin), and the root is scaled as a whole. Title and section text are TMP
    // (the user erased them from the art); the back and confirm buttons carry their labels in the art.
    // The first run moves the temporary root NicknamePanel into FrontendCanvas/ReferenceFrame; reruns only restyle.
    public static class Menu2NicknameSetup
    {
        public const string ScreenName = "NicknameScreen";
        public const string CardName = "NicknameCard";
        public const string OldRootName = "NicknamePanel";
        public const string Title = "닉네임 설정";
        public const string Section = "닉네임 입력";
        public const string Placeholder = "닉네임을 입력하세요";

        public static readonly Vector2 CardSize = new(1448, 1086);
        public const float CardScale = 0.9f;

        // Card pixel rects: left, top, width, height (layout.json; title and section measured on the art).
        public static readonly Rect TitleRect = new(484, 138, 480, 90);
        public static readonly Rect SectionRect = new(534, 344, 380, 60);
        public static readonly Rect InputRect = new(367, 500, 655, 80);
        public static readonly Rect ErrorRect = new(354, 614, 780, 64);
        public static readonly Rect RuleRect = new(574, 688, 306, 43);
        // User feedback: confirm was larger than back. Both are 117 high (confirm keeps its art's 3.6 aspect) and the
        // pair is centred on the card with the original 34 px gap.
        public static readonly Rect BackRect = new(348, 775, 297, 117);
        public static readonly Rect ConfirmRect = new(679, 775, 421, 117);

        public const float TitleFontSize = 64f;
        public const float SectionFontSize = 40f;
        public const float InputFontSize = 44f;
        public static readonly Color TitleInk = new(0.38f, 0.14f, 0.36f);

        // Objects from the earlier v3 assembly; the card art replaces them.
        private static readonly string[] Retired =
        {
            "NicknameBoard", "CastleFooter", "FloralCornerLeft", "FloralCornerRight", "TitlePlaque", "Title",
            "SectionRibbon", "AttentionRays", "ErpinMascot", "BackButton"
        };

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-2 Nickname")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Menu-2 nickname setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Menu-2 Nickname");
            Apply(FindSingle<FrontendTitleView>(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Frontend save failed.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("Menu-2 nickname setup complete.");
        }

        public static void Apply(FrontendTitleView title)
        {
            MenuArtSetup.Apply();
            Menu2HomeSetup.AddGlyphs(Title + Section + Placeholder + FrontendNicknameView.SubmittingText +
                                     FrontendNicknameView.RuleMessage);
            FrontendNicknameView view = Migrate(title);
            var screen = (RectTransform)view.transform;
            foreach (string name in Retired) RemoveChild(screen, name);

            RectTransform card = Node(screen, CardName, Vector2.zero, CardSize);
            Undo.RecordObject(card, "Card scale");
            card.localScale = Vector3.one * CardScale;
            RectTransform art = Node(card, "CardArt", Vector2.zero, CardSize);
            Skin(art.gameObject, MenuArtSetup.NicknameCard, Color.white, preserveAspect: true);

            TMP_Text heading = Label(card, "Title", Title, Center(TitleRect), TitleRect.size, TitleFontSize, TitleInk);
            TMP_Text section = Label(card, "SectionLabel", Section, Center(SectionRect), SectionRect.size, SectionFontSize,
                RibbonText);

            TMP_InputField input = StyleInput(view.NicknameInput, card);
            RemoveChild(card, "ErpinMascot"); // user feedback: no Erpin on the nickname card

            TMP_Text error = view.ErrorText;
            Move(error.transform, card);
            // Centred like the reference; the view puts the icon just left of the text, so keep room on both sides.
            Place(error.rectTransform, Center(ErrorRect), ErrorRect.size - new Vector2(100, 0));
            StyleText(error, 26, Warning);
            Undo.RecordObject(error, "Error wrap");
            error.textWrappingMode = TextWrappingModes.Normal;
            error.enableAutoSizing = true;
            error.fontSizeMin = 20f;
            error.fontSizeMax = 26f;
            // The icon is a child, so the view's show/hide of the error text shows the pair together.
            Image icon = Picture(error.rectTransform, "WarningIcon", MenuArtSetup.IconWarning, Vector2.zero,
                new Vector2(36, 34));
            Anchor(icon.rectTransform, 0f, new Vector2(-46, 0), new Vector2(36, 34));

            // "확인 중..." uses the error line; the view clears the error while the request is out.
            TMP_Text progress = Label(card, "ProgressText", FrontendNicknameView.SubmittingText, Center(ErrorRect),
                ErrorRect.size, 26, InkSoft);
            Undo.RecordObject(progress.gameObject, "Hide progress");
            progress.gameObject.SetActive(false);

            TMP_Text rule = view.ValidationText;
            Move(rule.transform, card);
            Place(rule.rectTransform, Center(RuleRect), RuleRect.size);
            StyleText(rule, 26, InkSoft);
            Undo.RecordObject(rule, "Rule fit");
            rule.enableAutoSizing = true;
            rule.fontSizeMin = 18f;
            rule.fontSizeMax = 26f;
            rule.text = FrontendNicknameView.RuleMessage;

            FrontendStartButton submit = EnsureMenuButton(view.SubmitButton, "SubmitButton");
            Move(submit.transform, card);
            ArtButton(submit, MenuArtSetup.NicknameConfirmButton, ConfirmRect);
            FrontendStartButton back = BackButton(card);
            ArtButton(back, MenuArtSetup.NicknameBackButton, BackRect);

            // Draw order back to front.
            Transform[] order =
            {
                art, heading.transform, section.transform, input.transform, error.transform,
                progress.transform, rule.transform, back.transform, submit.transform
            };
            for (int i = 0; i < order.Length; i++) order[i].SetSiblingIndex(i);

            Navigate(input, down: submit);
            Navigate(submit, up: input, left: back);
            Navigate(back, up: input, right: submit);

            Undo.RecordObject(view, "Nickname menu");
            view.Configure(input, rule, error, submit);
            view.ConfigureMenu(back, progress);
            Undo.RecordObject(view.gameObject, "Hide nickname");
            view.gameObject.SetActive(false); // the title view opens it like the other screens
            foreach (Component component in screen.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        // Card pixel (top-left origin) to a position relative to the card centre.
        public static Vector2 Point(Vector2 pixel) => new(pixel.x - CardSize.x / 2f, CardSize.y / 2f - pixel.y);

        public static Vector2 Center(Rect rect) => Point(rect.center);

        // Moves the temporary root NicknamePanel's controls into a NicknameScreen under the reference frame.
        private static FrontendNicknameView Migrate(FrontendTitleView title)
        {
            var frame = title.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Missing FrontendCanvas/ReferenceFrame.");
            FrontendNicknameView old = title.NicknameView;
            if (old == null) throw new InvalidOperationException("FrontendTitleView has no nickname view.");
            if (old.transform.parent == frame) return old;

            if (old.transform.parent != null || old.name != OldRootName)
                throw new InvalidOperationException($"Unexpected nickname view at {old.name}; expected the root {OldRootName}.");
            RectTransform screen = Node(frame, ScreenName, Vector2.zero, FrontendLayout.ReferenceSize);
            screen.SetSiblingIndex(frame.Find("SafeArea").GetSiblingIndex() + 1);
            FrontendNicknameView view = Ensure<FrontendNicknameView>(screen.gameObject);
            foreach (Component moved in new Component[] { old.NicknameInput, old.ValidationText, old.ErrorText, old.SubmitButton })
                Move(moved.transform, screen);
            view.Configure(old.NicknameInput, old.ValidationText, old.ErrorText, old.SubmitButton);
            Undo.RecordObject(title, "Nickname screen");
            title.ConfigureNicknamePanel(screen.gameObject, view);
            EditorUtility.SetDirty(title);
            Undo.DestroyObjectImmediate(old.gameObject); // the old root and its pixel-sized Canvas
            return view;
        }

        // Reparents in local units (the old pixel canvas and the scaled card would otherwise change the size).
        private static void Move(Transform child, Transform parent)
        {
            if (child.parent != parent) Undo.SetTransformParent(child, parent, false, "Move " + child.name);
            Undo.RecordObject(child, "Reset " + child.name);
            child.localScale = Vector3.one;
            child.localRotation = Quaternion.identity;
            child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y, 0f);
        }

        // The card art draws the field; the input keeps a clear Image so a click anywhere on it focuses.
        private static TMP_InputField StyleInput(TMP_InputField input, Transform card)
        {
            Move(input.transform, card);
            Undo.RecordObject(input.gameObject, "Rename input");
            input.name = "NicknameInput";
            Place((RectTransform)input.transform, Center(InputRect), InputRect.size);
            Image image = Ensure<Image>(input.gameObject);
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            image.type = Image.Type.Simple;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Undo.RecordObject(input, "Style input");
            input.targetGraphic = image;
            input.transition = Selectable.Transition.None;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = font;
            input.customCaretColor = true;
            input.caretColor = Ink;
            input.caretWidth = 3;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

            RectTransform area = input.textViewport;
            if (area == null) throw new InvalidOperationException("Nickname input has no text viewport.");
            Stretch(area);
            StyleText(input.textComponent, InputFontSize, Ink, TextAlignmentOptions.MidlineLeft);
            var placeholder = (TMP_Text)input.placeholder;
            StyleText(placeholder, InputFontSize, new Color(InkSoft.r, InkSoft.g, InkSoft.b, 0.65f),
                TextAlignmentOptions.MidlineLeft);
            Undo.RecordObject(placeholder, "Placeholder");
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.text = Placeholder;
            foreach (TMP_Text text in new[] { input.textComponent, placeholder }) Stretch(text.rectTransform);
            return input;
        }

        // Button art with its label baked in: no TMP label, grows on hover/focus, dims when disabled.
        private static void ArtButton(FrontendStartButton button, string sprite, Rect rect)
        {
            Place((RectTransform)button.transform, Center(rect), rect.size);
            Image image = Skin(button.gameObject, sprite, Color.white, raycast: true, preserveAspect: true);
            Undo.RecordObject(button, "Style " + button.name);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors();
            foreach (string name in new[] { "Label", "Text (TMP)", "ArrowIcon", "HoverBorder", "FocusBorder" })
                RemoveChild(button.transform, name);
            button.ConfigureBorders(null, null);
            Pop(button);
        }

        // The temporary submit button is a plain Button; the shared menu behaviour needs FrontendStartButton.
        private static FrontendStartButton EnsureMenuButton(Button button, string name)
        {
            GameObject owner = button.gameObject;
            Undo.RecordObject(owner, "Rename " + name);
            owner.name = name;
            if (button is FrontendStartButton menuButton) return menuButton;
            if (button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException(name + " has persistent click actions; move them before restyling.");
            Undo.DestroyObjectImmediate(button);
            return Undo.AddComponent<FrontendStartButton>(owner);
        }

        private static FrontendStartButton BackButton(Transform card)
        {
            RectTransform rect = Node(card, "BackButton", Center(BackRect), BackRect.size);
            Ensure<Image>(rect.gameObject);
            return Ensure<FrontendStartButton>(rect.gameObject);
        }

        private static void Navigate(Selectable selectable, Selectable up = null, Selectable down = null,
            Selectable left = null, Selectable right = null)
        {
            Undo.RecordObject(selectable, "Navigation " + selectable.name);
            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }
    }
}
