using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13FrontendSetup
    {
        public const string ScenePath = "Assets/Scenes/FrontendScene.unity";
        public const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        public const string FontPath = "Assets/Fonts/Frontend Noto Sans KR.asset";

        [MenuItem("Trickal Fan Game/Week 13/Setup Frontend Flow")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Frontend setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(GameScenePath)) throw new InvalidOperationException("Existing Game Scene is missing.");
            EnsureFont();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
            {
                // Do not leave combat scenes loaded alongside the title in Editor Play Mode.
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                    if (SceneManager.GetSceneAt(i) != scene)
                        EditorSceneManager.CloseScene(SceneManager.GetSceneAt(i), true);
            }
            SceneManager.SetActiveScene(scene);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Frontend Flow");

            GameObject cameraObject = Root(scene, "FrontendCamera");
            Camera titleCamera = Component<Camera>(cameraObject);
            titleCamera.clearFlags = CameraClearFlags.SolidColor;
            titleCamera.backgroundColor = new Color(0.045f, 0.065f, 0.12f);
            titleCamera.cullingMask = 0;
            titleCamera.orthographic = true;
            EditorUtility.SetDirty(titleCamera);

            GameObject canvasObject = Root(scene, "FrontendCanvas", typeof(RectTransform));
            Canvas canvas = Component<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = Component<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = FrontendLayout.ReferenceSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Component<GraphicRaycaster>(canvasObject);
            Image background = Component<Image>(Rect(canvasObject.transform, "Background", Vector2.zero, Vector2.zero).gameObject);
            Stretch(background.rectTransform);
            background.color = new Color(0.045f, 0.065f, 0.12f);
            background.raycastTarget = false;

            RectTransform frame = Rect(canvasObject.transform, "ReferenceFrame", Vector2.zero, FrontendLayout.ReferenceSize);
            Component<FrontendLayout>(frame.gameObject).ApplyLayout();
            RectTransform safe = Rect(frame, "SafeArea", Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.offsetMin = FrontendLayout.SafeMargin;
            safe.offsetMax = -FrontendLayout.SafeMargin;

            RectTransform titlePanel = Rect(safe, "TitlePanel", Vector2.zero, Vector2.zero);
            Stretch(titlePanel);
            MoveDirectChild(safe, titlePanel, "GameLogo");
            MoveDirectChild(safe, titlePanel, "Subtitle");
            MoveDirectChild(safe, titlePanel, "GameStartButton");
            MoveDirectChild(safe, titlePanel, "Status");

            RectTransform logo = Rect(titlePanel, "GameLogo", new Vector2(0, 150), new Vector2(960, 160));
            Image logoShape = Component<Image>(logo.gameObject);
            logoShape.color = new Color(0.1f, 0.17f, 0.26f);
            logoShape.raycastTarget = false;
            Text(logo, "LogoText", "TRICKAL FAN GAME", Vector2.zero, new Vector2(920, 112), 72);
            Text(titlePanel, "Subtitle", "트릭컬 팬게임", new Vector2(0, 28), new Vector2(560, 48), 28);

            RectTransform buttonRect = Rect(titlePanel, "GameStartButton", new Vector2(0, -120), new Vector2(280, 64));
            Image buttonImage = Component<Image>(buttonRect.gameObject);
            buttonImage.color = new Color(0.18f, 0.68f, 0.64f);
            FrontendStartButton button = Component<FrontendStartButton>(buttonRect.gameObject);
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            Text(buttonRect, "Label", "GAME START", Vector2.zero, new Vector2(268, 52), 24).color = new Color(0.025f, 0.06f, 0.08f);
            GameObject hover = Border(buttonRect, "HoverBorder", 2, 0);
            GameObject focus = Border(buttonRect, "FocusBorder", 3, 2);
            button.ConfigureBorders(hover, focus);
            TMP_Text status = Text(titlePanel, "Status", "", new Vector2(0, -220), new Vector2(800, 80), 24);
            FrontendTitleView view = Component<FrontendTitleView>(canvasObject);
            view.Configure(button, status);
            view.ConfigureTitlePanel(titlePanel.gameObject);

            FrontendHomeView homeView = SetupHome(frame);
            view.ConfigureHomeView(homeView);
            homeView.gameObject.SetActive(false);

            GameObject events = Root(scene, "FrontendEventSystem");
            EventSystem eventSystem = Component<EventSystem>(events);
            Component<InputSystemUIInputModule>(events);
            eventSystem.firstSelectedGameObject = button.gameObject;
            foreach (var component in canvasObject.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
            foreach (var component in events.GetComponents<Component>()) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Frontend save failed.");
            var remainingScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => s.path == GameScenePath ? new EditorBuildSettingsScene(GameScenePath, true) : s).ToList();
            if (!remainingScenes.Any(s => s.path == GameScenePath))
                remainingScenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(remainingScenes).ToArray();
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 Frontend setup complete. Title, profile and home flow are isolated from the Game Scene.");
        }

        private static void EnsureFont()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                throw new InvalidOperationException("TMP Essential Resources are missing. Import them with Window > TextMeshPro > Import TMP Essential Resources, then rerun Setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
                if (source == null) throw new InvalidOperationException("Noto Sans KR font is missing.");
                font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                font.name = "Frontend Noto Sans KR";
                AssetDatabase.CreateAsset(font, FontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            // Keep the preloaded title glyphs across Editor shutdown and player builds.
            var fontSettings = new SerializedObject(font);
            fontSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            fontSettings.ApplyModifiedPropertiesWithoutUndo();
            string titleCharacters = "TRICKAL FAN GAME트릭컬 팬게임닉네임님환영합니다게임시작스킬강화설정나가기캐릭터선택뒤로" +
                FrontendTitleView.ReadyMessage + FrontendHomeView.PreparationMessage + FrontendHomeView.QuitMessage;
            if (!font.HasCharacters(titleCharacters) && !font.TryAddCharacters(titleCharacters, out string missing))
                throw new InvalidOperationException("Missing title glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Root(Scene scene, string name, params Type[] types)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(o => o.name == name).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate root: " + name);
            if (matches.Length == 1) return matches[0];
            var created = new GameObject(name, types);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
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
            var rect = existing != null ? existing as RectTransform : null;
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

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static FrontendHomeView SetupHome(RectTransform frame)
        {
            RectTransform root = Rect(frame, "HomeScreen", Vector2.zero, Vector2.zero);
            Stretch(root);
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.075f, 0.12f, 0.16f);
            background.raycastTarget = false;

            RectTransform homePanel = Rect(root, "HomePanel", Vector2.zero, Vector2.zero);
            Stretch(homePanel);
            RectTransform homeSafe = Rect(homePanel, "SafeArea", Vector2.zero, Vector2.zero);
            Stretch(homeSafe);
            homeSafe.offsetMin = FrontendLayout.SafeMargin;
            homeSafe.offsetMax = -FrontendLayout.SafeMargin;

            Image leftDecoration = Component<Image>(Rect(homePanel, "LeftDecoration", new Vector2(-760, 0), new Vector2(260, 1080)).gameObject);
            leftDecoration.color = new Color(0.1f, 0.25f, 0.28f, 0.75f);
            leftDecoration.raycastTarget = false;
            Image rightDecoration = Component<Image>(Rect(homePanel, "RightDecoration", new Vector2(760, 0), new Vector2(260, 1080)).gameObject);
            rightDecoration.color = new Color(0.16f, 0.17f, 0.3f, 0.75f);
            rightDecoration.raycastTarget = false;

            Text(homeSafe, "HomeTitle", "TRICKAL FAN GAME", new Vector2(0, 330), new Vector2(900, 100), 56);
            TMP_Text welcome = Text(homeSafe, "Welcome", "닉네임 님, 환영합니다", new Vector2(0, 235), new Vector2(900, 56), 28);
            FrontendStartButton gameStart = MenuButton(homeSafe, "HomeGameStartButton", "게임 시작", 100);
            FrontendStartButton skill = MenuButton(homeSafe, "HomeSkillUpgradeButton", "스킬 강화", 15);
            FrontendStartButton settings = MenuButton(homeSafe, "HomeSettingsButton", "설정", -70);
            FrontendStartButton quit = MenuButton(homeSafe, "HomeQuitButton", "나가기", -155);
            SetVerticalNavigation(gameStart, skill, settings, quit);

            RectTransform destination = Rect(root, "DestinationPanel", Vector2.zero, Vector2.zero);
            Stretch(destination);
            TMP_Text destinationTitle = Text(destination, "DestinationTitle", FrontendHomeView.CharacterSelectionTitle,
                new Vector2(0, 140), new Vector2(900, 90), 48);
            TMP_Text destinationMessage = Text(destination, "DestinationMessage", FrontendHomeView.PreparationMessage,
                new Vector2(0, 30), new Vector2(900, 80), 26);
            FrontendStartButton back = MenuButton(destination, "BackButton", "뒤로", -130);
            destination.gameObject.SetActive(false);

            FrontendHomeView view = Component<FrontendHomeView>(root.gameObject);
            view.Configure(homePanel.gameObject, welcome, gameStart, skill, settings, quit,
                destination.gameObject, destinationTitle, destinationMessage, back);
            return view;
        }

        private static FrontendStartButton MenuButton(Transform parent, string name, string label, float y)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0, y), new Vector2(360, 64));
            Image image = Component<Image>(rect.gameObject);
            image.color = new Color(0.18f, 0.68f, 0.64f);
            FrontendStartButton button = Component<FrontendStartButton>(rect.gameObject);
            button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            Text(rect, "Label", label, Vector2.zero, new Vector2(344, 52), 24).color = new Color(0.025f, 0.06f, 0.08f);
            button.ConfigureBorders(Border(rect, "HoverBorder", 2, 0), Border(rect, "FocusBorder", 3, 2));
            return button;
        }

        private static void SetVerticalNavigation(params FrontendStartButton[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length]
                };
            }
        }

        private static void MoveDirectChild(Transform source, Transform destination, string name)
        {
            Transform child = source.Find(name);
            if (child == null || child.parent != source) return;
            Undo.SetTransformParent(child, destination, "Group " + name);
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.96f, 1);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
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
                Vector2 position = horizontal ? new Vector2(0, direction * (size.y - thickness) / 2)
                    : new Vector2(direction * (size.x - thickness) / 2, 0);
                Image edge = Component<Image>(Rect(root, "Edge" + i, position, edgeSize).gameObject);
                edge.color = Color.white;
                edge.raycastTarget = false;
            }
            return root.gameObject;
        }
    }
}
