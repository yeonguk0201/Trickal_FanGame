using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud7BSetup
    {
        public const string EventSystemName = "Game HUD EventSystem";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-7B Pause Menu")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-7B setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindSingle<GamePauseArtifactView>(scene, "pause artifact view");
            PlayerStats stats = FindSingle<PlayerStats>(scene, "player stats");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform panel = view.transform.Find("Pause Panel") as RectTransform;
            if (panel == null) throw new InvalidOperationException("Run HUD-3C setup before HUD-7B setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-7B Pause Menu");

            TMP_Text title = panel.Find("Title")?.GetComponent<TMP_Text>();
            if (title == null) throw new InvalidOperationException("HUD-3C pause title is missing.");
            Undo.RecordObject(title, "Configure Pause Title");
            title.text = "일시정지";

            TMP_Text hint = panel.Find("Resume Hint")?.GetComponent<TMP_Text>();
            if (hint != null)
            {
                Undo.RecordObject(hint.rectTransform, "Layout Resume Hint");
                hint.rectTransform.anchoredPosition = new Vector2(0, 292);
                hint.text = "ESC를 다시 누르거나 아래 버튼으로 게임에 돌아갑니다.";
            }

            RectTransform statsPanel = Rect(panel, "Detailed Stats", new Vector2(-382, -12), new Vector2(300, 560));
            Image statsBackground = Component<Image>(statsPanel.gameObject);
            statsBackground.color = new Color(0.055f, 0.08f, 0.12f, 1f);
            statsBackground.raycastTarget = false;
            TMP_Text statsTitle = Text(statsPanel, "Section Title", "상세 스탯", new Vector2(0, 244),
                new Vector2(260, 40), 28, font, TextAlignmentOptions.Center);
            statsTitle.fontStyle = FontStyles.Bold;
            TMP_Text statsText = Text(statsPanel, "Values", "공격력  1", new Vector2(0, -2),
                new Vector2(244, 432), 22, font, TextAlignmentOptions.TopLeft);
            statsText.textWrappingMode = TextWrappingModes.NoWrap;
            statsText.lineSpacing = 24f;

            TMP_Text artifactTitle = Text(panel, "Artifact Section Title", "보유 아티팩트", new Vector2(164, 256),
                new Vector2(700, 40), 28, font, TextAlignmentOptions.Center);
            artifactTitle.fontStyle = FontStyles.Bold;
            RectTransform viewport = panel.Find("Artifact Viewport") as RectTransform;
            if (viewport == null) throw new InvalidOperationException("HUD-3C artifact viewport is missing.");
            Undo.RecordObject(viewport, "Layout Artifact Viewport");
            viewport.anchoredPosition = new Vector2(164, -18);
            viewport.sizeDelta = new Vector2(700, 520);

            RectTransform template = viewport.Find("Content/Artifact Detail Template") as RectTransform;
            if (template == null) throw new InvalidOperationException("HUD-3C artifact template is missing.");
            LayoutArtifactTemplate(template);

            Button resumeButton = Button(panel, "Resume Button", "게임으로 돌아가기", new Vector2(0, -334),
                new Vector2(260, 52), font);
            Navigation navigation = new() { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = resumeButton;
            navigation.selectOnDown = resumeButton;
            navigation.selectOnLeft = resumeButton;
            navigation.selectOnRight = resumeButton;
            resumeButton.navigation = navigation;

            GraphicRaycaster raycaster = view.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
            if (raycaster == null) throw new InvalidOperationException("Game HUD GraphicRaycaster is missing.");
            raycaster.enabled = false;
            EnsureEventSystem(scene, resumeButton.gameObject);
            view.ConfigureMenu(stats, statsText, resumeButton, panel, raycaster);
            EnsureGlyphs(font);

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-7B setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-7B setup complete. Detailed stats, resume control, focus confinement and restoration are connected.");
        }

        private static void LayoutArtifactTemplate(RectTransform template)
        {
            Undo.RecordObject(template, "Layout Artifact Template");
            template.sizeDelta = new Vector2(652, 104);
            Layout(template, "Icon", new Vector2(-274, 0), new Vector2(72, 72));
            Layout(template, "Name", new Vector2(-166, 22), new Vector2(144, 32));
            Layout(template, "Stack", new Vector2(-66, 22), new Vector2(48, 32));
            Layout(template, "Description", new Vector2(126, -22), new Vector2(318, 52));
        }

        private static void Layout(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null) throw new InvalidOperationException("HUD-3C artifact template child is missing: " + name);
            Undo.RecordObject(rect, "Layout " + name);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureEventSystem(Scene scene, GameObject firstSelected)
        {
            EventSystem[] systems = FindAll<EventSystem>(scene);
            EventSystem eventSystem;
            if (systems.Length == 0)
            {
                GameObject owner = new(EventSystemName);
                Undo.RegisterCreatedObjectUndo(owner, "Create Game HUD EventSystem");
                SceneManager.MoveGameObjectToScene(owner, scene);
                eventSystem = Undo.AddComponent<EventSystem>(owner);
                Undo.AddComponent<InputSystemUIInputModule>(owner);
            }
            else if (systems.Length == 1)
            {
                eventSystem = systems[0];
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                    Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            }
            else
            {
                throw new InvalidOperationException("Game Scene must not contain multiple EventSystems.");
            }
            Undo.RecordObject(eventSystem, "Configure Game HUD EventSystem");
            eventSystem.firstSelectedGameObject = firstSelected;
        }

        private static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size,
            TMP_FontAsset font)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Image image = Component<Image>(rect.gameObject);
            image.color = new Color(0.12f, 0.4f, 0.48f, 1f);
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
            TMP_Text text = Text(rect, "Label", label, Vector2.zero, size, 24, font, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            const string required = "일시정지상세스탯공격력속도이동치명타율피해투사체관통보유아티팩트게임으로돌아가기버튼아래다시누르거나옵니다불수없0123456789.%ESC";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing HUD-7B glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = FindAll<T>(scene);
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

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
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
