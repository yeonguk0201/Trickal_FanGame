using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud7CSetup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-7C Leave Run")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-7C setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Week13Hud7BSetup.Setup();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindSingle<GamePauseArtifactView>(scene, "pause menu");
            RunSession session = FindSingle<RunSession>(scene, "RunSession");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform pausePanel = view.FocusScope;
            if (pausePanel == null || view.ResumeButton == null)
                throw new InvalidOperationException("Run HUD-7B setup before HUD-7C setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-7C Leave Run");

            Layout(view.ResumeButton.GetComponent<RectTransform>(), new Vector2(-150, -334), new Vector2(260, 52));
            Button leaveButton = Button(pausePanel, "Leave Run Button", "홈으로 나가기",
                new Vector2(150, -334), new Vector2(260, 52), font, new Color(0.48f, 0.18f, 0.2f, 1f));

            Navigation resumeNavigation = Explicit(view.ResumeButton, view.ResumeButton, leaveButton, leaveButton);
            view.ResumeButton.navigation = resumeNavigation;
            leaveButton.navigation = Explicit(leaveButton, leaveButton, view.ResumeButton, view.ResumeButton);

            RectTransform confirmation = Rect(view.transform, "Leave Confirmation", Vector2.zero,
                new Vector2(1120, 760));
            Image dim = Component<Image>(confirmation.gameObject);
            dim.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);
            dim.raycastTarget = true;
            CanvasGroup confirmationGroup = Component<CanvasGroup>(confirmation.gameObject);

            RectTransform dialog = Rect(confirmation, "Dialog", Vector2.zero, new Vector2(720, 360));
            Image dialogImage = Component<Image>(dialog.gameObject);
            dialogImage.color = new Color(0.07f, 0.095f, 0.14f, 1f);
            dialogImage.raycastTarget = true;
            Outline dialogOutline = Component<Outline>(dialog.gameObject);
            dialogOutline.effectColor = new Color(0.5f, 0.8f, 0.9f, 0.85f);
            dialogOutline.effectDistance = new Vector2(2f, -2f);

            TMP_Text title = Text(dialog, "Title", "현재 Run을 끝낼까요?", new Vector2(0, 104),
                new Vector2(620, 52), 32, font);
            title.fontStyle = FontStyles.Bold;
            Text(dialog, "Warning", "지금까지의 진행과 획득 아티팩트는 저장되지 않습니다.\n홈으로 돌아가도 결과 보상은 지급되지 않습니다.",
                new Vector2(0, 28), new Vector2(620, 96), 22, font);
            Button cancelButton = Button(dialog, "Cancel Button", "계속 플레이",
                new Vector2(-135, -102), new Vector2(240, 56), font, new Color(0.12f, 0.4f, 0.48f, 1f));
            Button confirmButton = Button(dialog, "Confirm Button", "Run 끝내기",
                new Vector2(135, -102), new Vector2(240, 56), font, new Color(0.48f, 0.18f, 0.2f, 1f));
            cancelButton.navigation = Explicit(cancelButton, cancelButton, confirmButton, confirmButton);
            confirmButton.navigation = Explicit(confirmButton, confirmButton, cancelButton, cancelButton);

            view.ConfigureLeaveRun(session, leaveButton, confirmationGroup, confirmButton, cancelButton, dialog);
            EnsureGlyphs(font);
            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-7C setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-7C setup complete. Leave confirmation and clean home return are connected.");
        }

        private static Navigation Explicit(Selectable up, Selectable down, Selectable left, Selectable right)
        {
            return new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }

        private static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size,
            TMP_FontAsset font, Color color)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Image image = Component<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            Button button = Component<Button>(rect.gameObject);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.selectedColor = new Color(0.55f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.88f, 0.92f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            Outline outline = Component<Outline>(rect.gameObject);
            outline.effectColor = new Color(0.5f, 0.94f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            TMP_Text text = Text(rect, "Label", label, Vector2.zero, size, 24, font);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void Layout(RectTransform rect, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Layout " + rect.name);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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

        private static T Component<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            const string required = "현재Run을끝낼까요홈으로나가기지금까지의진행과획득아티팩트는저장되지않습니다돌아가도결과보상은지급계속플레이끝내기";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing HUD-7C glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }
    }
}
