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

        [MenuItem("Trickal Fan Game/Week 13/Setup Frontend Title")]
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
            Undo.SetCurrentGroupName("Setup Frontend Title");

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

            RectTransform logo = Rect(safe, "GameLogo", new Vector2(0, 150), new Vector2(960, 160));
            Image logoShape = Component<Image>(logo.gameObject);
            logoShape.color = new Color(0.1f, 0.17f, 0.26f);
            logoShape.raycastTarget = false;
            Text(logo, "LogoText", "TRICKAL FAN GAME", Vector2.zero, new Vector2(920, 112), 72);
            Text(safe, "Subtitle", "트릭컬 팬게임", new Vector2(0, 28), new Vector2(560, 48), 28);

            RectTransform buttonRect = Rect(safe, "GameStartButton", new Vector2(0, -120), new Vector2(280, 64));
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
            TMP_Text status = Text(safe, "Status", "", new Vector2(0, -220), new Vector2(800, 80), 24);
            FrontendTitleView view = Component<FrontendTitleView>(canvasObject);
            view.Configure(button, status);

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
            Debug.Log("Week 13 Frontend setup complete. Frontend is build index 0; SampleScene remains the Game Scene.");
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
            string titleCharacters = "TRICKAL FAN GAME트릭컬 팬게임" + FrontendTitleView.ReadyMessage;
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
