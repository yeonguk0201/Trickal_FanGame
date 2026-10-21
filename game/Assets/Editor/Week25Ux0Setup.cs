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
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // UX-0: in-game settings inside the pause menu, and the death → GAME OVER → confirm → result sequence.
    public static class Week25Ux0Setup
    {
        public const string SettingsRootName = "In-Game Settings";
        public const string SettingsPanelName = "Settings Panel";
        public const string SettingsButtonName = "Settings Button";
        public const string GameOverCanvasName = "Game Over Canvas";
        public const int GameOverSortingOrder = 60;
        public static readonly Vector2 PauseButtonSize = new(240, 52);
        public const float PauseButtonY = -334f;
        public static readonly float[] PauseButtonX = { -390f, -130f, 130f, 390f };

        [MenuItem("Trickal Fan Game/Week 25/Setup UX-0 In-game Settings and Game Over")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before UX-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindSingle<GamePauseArtifactView>(scene, "pause menu");
            RunSession session = FindSingle<RunSession>(scene, "RunSession");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            if (view.FocusScope == null || view.ResumeButton == null || view.RestartRunButton == null ||
                view.LeaveRunButton == null)
                throw new InvalidOperationException("Run HUD-7B/7C/7D setup before UX-0 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup UX-0");

            ConfigurePauseSettings(scene, view, font);
            GameOverSequence sequence = ConfigureGameOver(scene, font);
            Undo.RecordObject(session, "Configure UX-0 RunSession");
            session.ConfigureGameOverSequence(sequence);
            EnsureGlyphs(font, "설정결과 보기GAME OVER");

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            foreach (Component component in sequence.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during UX-0 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 25 UX-0 setup complete. Pause settings and the GAME OVER sequence are connected.");
        }

        private static void ConfigurePauseSettings(Scene scene, GamePauseArtifactView view, TMP_FontAsset font)
        {
            Button settingsButton = Button(view.FocusScope, SettingsButtonName, "설정", Vector2.zero,
                PauseButtonSize, font, new Color(0.14f, 0.34f, 0.42f, 1f));
            Button[] order = { view.ResumeButton, settingsButton, view.RestartRunButton, view.LeaveRunButton };
            for (int i = 0; i < order.Length; i++)
            {
                RectTransform rect = order[i].GetComponent<RectTransform>();
                Undo.RecordObject(rect, "Layout " + rect.name);
                rect.anchoredPosition = new Vector2(PauseButtonX[i], PauseButtonY);
                rect.sizeDelta = PauseButtonSize;
                Undo.RecordObject(order[i], "Navigate " + order[i].name);
                Button left = order[(i + order.Length - 1) % order.Length];
                Button right = order[(i + 1) % order.Length];
                order[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = left,
                    selectOnDown = right,
                    selectOnLeft = left,
                    selectOnRight = right
                };
            }

            // Full-screen dim that blocks the pause menu, holding a copy of the Frontend settings panel so both
            // screens show the same controls bound to the same LocalSettings.
            RectTransform root = Rect(view.transform, SettingsRootName, Vector2.zero, new Vector2(1920, 1080));
            Image dim = Component<Image>(root.gameObject);
            dim.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);
            dim.raycastTarget = true;

            Transform previous = root.Find(SettingsPanelName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            FrontendSettingsView settingsView = CopyFrontendSettingsPanel(scene, root);
            root.SetAsLastSibling();
            root.gameObject.SetActive(false);
            Undo.RecordObject(view, "Configure UX-0 Settings");
            view.ConfigureSettings(settingsButton, root.gameObject, settingsView);
        }

        private static FrontendSettingsView CopyFrontendSettingsPanel(Scene gameScene, RectTransform parent)
        {
            Scene frontend = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                FrontendSettingsView source = FindSingle<FrontendSettingsView>(frontend, "Frontend settings view");
                GameObject copy = Object.Instantiate(source.gameObject);
                Undo.RegisterCreatedObjectUndo(copy, "Copy Frontend Settings Panel");
                SceneManager.MoveGameObjectToScene(copy, gameScene);
                copy.name = SettingsPanelName;
                copy.transform.SetParent(parent, false);
                RectTransform rect = (RectTransform)copy.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                copy.SetActive(true);
                return copy.GetComponent<FrontendSettingsView>();
            }
            finally
            {
                EditorSceneManager.CloseScene(frontend, true);
            }
        }

        private static GameOverSequence ConfigureGameOver(Scene scene, TMP_FontAsset font)
        {
            GameObject canvasObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == GameOverCanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(GameOverCanvasName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create " + GameOverCanvasName);
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
            }
            Canvas canvas = Component<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GameOverSortingOrder;
            CanvasScaler scaler = Component<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            GraphicRaycaster raycaster = Component<GraphicRaycaster>(canvasObject);
            raycaster.enabled = false;

            RectTransform overlay = Rect(canvasObject.transform, "Overlay", Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            CanvasGroup overlayGroup = Component<CanvasGroup>(overlay.gameObject);

            RectTransform hole = IrisPiece(overlay, "Iris Hole");
            RectTransform left = IrisPiece(overlay, "Iris Left");
            RectTransform right = IrisPiece(overlay, "Iris Right");
            RectTransform top = IrisPiece(overlay, "Iris Top");
            RectTransform bottom = IrisPiece(overlay, "Iris Bottom");

            RectTransform titleRect = Rect(overlay, "Title", new Vector2(0, 60), new Vector2(1200, 200));
            CanvasGroup titleGroup = Component<CanvasGroup>(titleRect.gameObject);
            titleGroup.interactable = false;
            titleGroup.blocksRaycasts = false;
            TMP_Text titleText = Component<TextMeshProUGUI>(Rect(titleRect, "Label", Vector2.zero,
                new Vector2(1200, 200)).gameObject);
            titleText.font = font;
            titleText.text = "GAME OVER";
            titleText.fontSize = 128;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 8f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.96f, 0.36f, 0.4f);
            titleText.raycastTarget = false;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;

            Button confirm = Button(overlay, "Confirm Button", "결과 보기", new Vector2(0, -120),
                new Vector2(260, 60), font, new Color(0.14f, 0.34f, 0.42f, 1f));
            confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            titleRect.SetAsLastSibling();
            confirm.transform.SetAsLastSibling();

            GameOverSequence sequence = Component<GameOverSequence>(canvasObject);
            sequence.Configure(overlayGroup, hole, left, right, top, bottom, titleGroup, confirm, raycaster);
            return sequence;
        }

        private static RectTransform IrisPiece(RectTransform parent, string name)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero);
            Image image = Component<Image>(rect.gameObject);
            image.color = Color.black;
            image.raycastTarget = true;
            return rect;
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

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing as RectTransform;
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

        private static void EnsureGlyphs(TMP_FontAsset font, string required)
        {
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing UX-0 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        internal static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"{scene.path} requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }
    }
}
