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
    public static class Week13Hud7DSetup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-7D Restart Run")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-7D setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Week13Hud7CSetup.Setup();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindSingle<GamePauseArtifactView>(scene, "pause menu");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-7D Restart Run");

            Layout(view.ResumeButton.GetComponent<RectTransform>(), new Vector2(-270, -334), new Vector2(240, 52));
            Button restartButton = Button(view.FocusScope, "Restart Run Button", "처음부터 다시하기",
                Vector2.zero + new Vector2(0, -334), new Vector2(240, 52), font,
                new Color(0.42f, 0.32f, 0.12f, 1f));
            Layout(view.LeaveRunButton.GetComponent<RectTransform>(), new Vector2(270, -334), new Vector2(240, 52));

            view.ResumeButton.navigation = Horizontal(view.LeaveRunButton, restartButton);
            restartButton.navigation = Horizontal(view.ResumeButton, view.LeaveRunButton);
            view.LeaveRunButton.navigation = Horizontal(restartButton, view.ResumeButton);

            TMP_Text title = view.ConfirmationFocusScope.Find("Title")?.GetComponent<TMP_Text>();
            TMP_Text warning = view.ConfirmationFocusScope.Find("Warning")?.GetComponent<TMP_Text>();
            TMP_Text actionLabel = view.ConfirmLeaveButton.GetComponentInChildren<TMP_Text>();
            if (title == null || warning == null || actionLabel == null)
                throw new InvalidOperationException("HUD-7C confirmation text references are missing.");
            view.ConfigureRestartRun(restartButton, title, warning, actionLabel);
            EnsureGlyphs(font);

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-7D setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-7D setup complete. Same-character restart is connected to a fresh Run launch.");
        }

        private static Navigation Horizontal(Selectable left, Selectable right)
        {
            return new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = left,
                selectOnDown = right,
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
            TMP_Text text = Component<TextMeshProUGUI>(Rect(rect, "Label", Vector2.zero, size).gameObject);
            text.font = font;
            text.text = label;
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            return button;
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
            const string required = "처음부터다시하기시작할까요현재Run의진행과획득아티팩트는저장되지않습니다같은캐릭터로새시작합니다";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing HUD-7D glyphs: " + missing);
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
