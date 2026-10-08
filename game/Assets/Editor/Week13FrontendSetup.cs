using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Frontend;
using TrickalFanGame.Run;
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
        public const string CharacterFolder = "Assets/Characters";

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
            Component<AudioListener>(cameraObject);
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
            SetupCharacterSelection(homeView, frame);
            SetupSkillUpgrade(homeView, frame);
            SetupSettings(homeView, frame);
            Component<FrontendRunLauncher>(canvasObject).Configure(homeView, GameScenePath);
            view.ConfigureHomeView(homeView);
            SetupRunResult(view, frame);
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
            Debug.Log("Week 13 Frontend setup complete. Title, profile, home and data-driven character selection are isolated from the Game Scene.");
        }

        private static void EnsureFont()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                throw new InvalidOperationException("TMP Essential Resources are missing. Import them with Window > TextMeshPro > Import TMP Essential Resources, then rerun Setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                // The asset keeps its first name; its source font is the default font (DefaultFontSetup).
                Font source = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontSetup.SourceFontPath);
                if (source == null) throw new InvalidOperationException("The default font ONE Mobile POP is missing.");
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
            string titleCharacters = "TRICKAL FAN GAME트릭컬 팬게임닉네임님환영합니다게임시작스킬강화설정나가기캐릭터선택뒤로확인선택됨할수있는없습니다해주세요완료" +
                FrontendTitleView.ReadyMessage + FrontendHomeView.PreparationMessage + FrontendHomeView.QuitMessage +
                "음량전체배경효과화면해상도x0123456789% " +
                "결과저장중완료실패서버확인후공개됩니다다시시도홈으로도달층처치플레이시간사망원인" +
                "경험치레벨업포인트획득아티팩트없음해냈어역시내가최고야으앙다음에는꼭이길거야" +
                "HUD상세스탯표시";
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

        private static void SetupCharacterSelection(FrontendHomeView homeView, RectTransform frame)
        {
            CharacterDefinition[] characters = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { CharacterFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterDefinition>)
                .Where(character => character != null && character.IsValid)
                .OrderBy(character => character.CharacterId, StringComparer.Ordinal)
                .ToArray();
            if (characters.Length == 0)
                throw new InvalidOperationException("At least one valid CharacterDefinition is required for Flow-3.");

            RectTransform root = Rect(frame, "CharacterSelectionPanel", Vector2.zero, Vector2.zero);
            Stretch(root);
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.055f, 0.09f, 0.14f);
            background.raycastTarget = false;

            Text(root, "CharacterSelectionTitle", "캐릭터 선택", new Vector2(0, 450), new Vector2(900, 64), 40);
            RectTransform cardContainer = Rect(root, "CharacterCardContainer", new Vector2(0, 70), new Vector2(1240, 520));
            HorizontalLayoutGroup layout = Component<HorizontalLayoutGroup>(cardContainer.gameObject);
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform cardRect = Rect(cardContainer, "CharacterCardTemplate", Vector2.zero, new Vector2(320, 520));
            Image cardImage = Component<Image>(cardRect.gameObject);
            cardImage.color = new Color(0.12f, 0.18f, 0.24f);
            FrontendStartButton cardButton = Component<FrontendStartButton>(cardRect.gameObject);
            cardButton.targetGraphic = cardImage;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = Color.white;
            cardButton.colors = colors;
            cardButton.ConfigureBorders(Border(cardRect, "HoverBorder", 2, 0), Border(cardRect, "FocusBorder", 3, 2));

            GameObject border = Border(cardRect, "SelectionBorder", 3, 0);
            Image icon = Component<Image>(Rect(cardRect, "CharacterIcon", new Vector2(0, 155), new Vector2(128, 128)).gameObject);
            icon.color = new Color(0.3f, 0.72f, 0.68f);
            icon.raycastTarget = false;
            TMP_Text name = Text(cardRect, "CharacterName", "캐릭터", new Vector2(0, 55), new Vector2(280, 48), 28);
            TMP_Text description = Text(cardRect, "CharacterDescription", "설명", new Vector2(0, -55), new Vector2(280, 130), 20);
            TMP_Text selection = Text(cardRect, "SelectionState", "", new Vector2(0, -205), new Vector2(280, 36), 20);
            FrontendCharacterCardView cardTemplate = Component<FrontendCharacterCardView>(cardRect.gameObject);
            cardTemplate.ConfigureTemplate(cardButton, name, description, selection, border, icon);
            cardRect.gameObject.SetActive(false);

            TMP_Text status = Text(root, "CharacterSelectionStatus", FrontendCharacterSelectionView.SelectMessage,
                new Vector2(0, -235), new Vector2(900, 48), 20);
            FrontendStartButton confirm = MenuButton(root, "CharacterConfirmButton", "게임 시작", -335);
            confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(155, -335);
            confirm.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 64);
            FrontendStartButton back = MenuButton(root, "CharacterBackButton", "뒤로", -335);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(-155, -335);
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 52);

            FrontendCharacterSelectionView view = Component<FrontendCharacterSelectionView>(root.gameObject);
            view.Configure(characters, cardContainer, cardTemplate, status, confirm, back);
            homeView.ConfigureCharacterSelection(root.gameObject, view);
            root.gameObject.SetActive(false);
        }

        private static void SetupSkillUpgrade(FrontendHomeView homeView, RectTransform frame)
        {
            RectTransform root = Rect(frame, "SkillUpgradePanel", Vector2.zero, Vector2.zero);
            Stretch(root);
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.045f, 0.075f, 0.12f);
            background.raycastTarget = false;

            TMP_Text title = Text(root, "SkillUpgradeTitle", "에르핀 강화", new Vector2(0, 445), new Vector2(900, 64), 40);
            RectTransform summary = Rect(root, "ProgressSummary", new Vector2(0, 325), new Vector2(1120, 130));
            Image summaryImage = Component<Image>(summary.gameObject);
            summaryImage.color = new Color(0.08f, 0.14f, 0.2f);
            summaryImage.raycastTarget = false;
            TMP_Text characterLevel = Text(summary, "CharacterLevel", "캐릭터 레벨  -", new Vector2(-335, 28), new Vector2(400, 40), 24);
            TMP_Text experience = Text(summary, "Experience", "경험치  -", new Vector2(-335, -28), new Vector2(400, 40), 20);
            TMP_Text points = Text(summary, "SkillPoints", "남은 스킬 포인트  -", new Vector2(300, 0), new Vector2(450, 54), 28);

            RectTransform lowPanel = SkillPanel(root, "LowGradeSkillPanel", new Vector2(-300, 35),
                "저학년 스킬", "SP 유도탄 연사");
            TMP_Text lowLevel = Text(lowPanel, "SkillLevel", "Lv. -", new Vector2(0, 52), new Vector2(420, 42), 28);
            TMP_Text lowEffect = Text(lowPanel, "SkillEffect", "서버 진행 정보가 필요합니다.", new Vector2(0, -5), new Vector2(430, 58), 20);
            FrontendStartButton lowButton = MenuButton(lowPanel, "UpgradeButton", "1 포인트로 강화", -105);
            lowButton.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 64);

            RectTransform highPanel = SkillPanel(root, "HighGradeSkillPanel", new Vector2(300, 35),
                "고학년 스킬", "무적 돌진과 광역 피해");
            TMP_Text highLevel = Text(highPanel, "SkillLevel", "Lv. -", new Vector2(0, 52), new Vector2(420, 42), 28);
            TMP_Text highEffect = Text(highPanel, "SkillEffect", "서버 진행 정보가 필요합니다.", new Vector2(0, -5), new Vector2(430, 58), 20);
            FrontendStartButton highButton = MenuButton(highPanel, "UpgradeButton", "1 포인트로 강화", -105);
            highButton.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 64);

            TMP_Text status = Text(root, "SkillUpgradeStatus", FrontendSkillUpgradeView.LoadingMessage,
                new Vector2(0, -245), new Vector2(1120, 70), 20);
            FrontendStartButton retry = MenuButton(root, "SkillProgressRetryButton", "다시 불러오기", -330);
            retry.GetComponent<RectTransform>().anchoredPosition = new Vector2(150, -330);
            retry.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 64);
            FrontendStartButton back = MenuButton(root, "SkillUpgradeBackButton", "캐릭터 선택", -330);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, -330);
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 52);

            lowButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = highButton, selectOnDown = back };
            highButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = lowButton, selectOnDown = retry };
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = lowButton, selectOnRight = retry };
            retry.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = highButton, selectOnLeft = back };

            FrontendSkillUpgradeView view = Component<FrontendSkillUpgradeView>(root.gameObject);
            view.Configure(title, characterLevel, experience, points, lowLevel, lowEffect, lowButton,
                highLevel, highEffect, highButton, status, retry, back);
            homeView.ConfigureSkillUpgrade(root.gameObject, view);
            retry.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        private static RectTransform SkillPanel(Transform parent, string name, Vector2 position, string heading, string summary)
        {
            RectTransform panel = Rect(parent, name, position, new Vector2(520, 390));
            Image image = Component<Image>(panel.gameObject);
            image.color = new Color(0.1f, 0.16f, 0.24f);
            image.raycastTarget = false;
            Text(panel, "Heading", heading, new Vector2(0, 145), new Vector2(440, 48), 28);
            Text(panel, "Summary", summary, new Vector2(0, 103), new Vector2(440, 36), 18);
            return panel;
        }

        private static void SetupRunResult(FrontendTitleView titleView, RectTransform frame)
        {
            RectTransform root = Rect(frame, "RunResultScreen", Vector2.zero, Vector2.zero);
            Stretch(root);
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.025f, 0.04f, 0.075f, 1f);
            background.raycastTarget = true;

            RectTransform safe = Rect(root, "SafeArea", Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.offsetMin = FrontendLayout.SafeMargin;
            safe.offsetMax = -FrontendLayout.SafeMargin;

            TMP_Text heading = Text(safe, "ResultTitle", "RUN CLEAR", new Vector2(0, 390), new Vector2(900, 80), 48);

            RectTransform characterPanel = Rect(safe, "CharacterReaction", new Vector2(-490, -20), new Vector2(620, 700));
            Image characterBackground = Component<Image>(characterPanel.gameObject);
            characterBackground.color = new Color(0.08f, 0.16f, 0.22f, 1f);
            TMP_Text characterName = Text(characterPanel, "CharacterName", "에르핀", new Vector2(0, 245), new Vector2(540, 60), 32);
            TMP_Text reaction = Text(characterPanel, "Reaction", "^_^", new Vector2(0, 30), new Vector2(420, 300), 84);
            RectTransform speechPanel = Rect(characterPanel, "SpeechBubble", new Vector2(0, -230), new Vector2(520, 120));
            Image speechBackground = Component<Image>(speechPanel.gameObject);
            speechBackground.color = new Color(0.92f, 0.96f, 1f, 1f);
            TMP_Text speech = Text(speechPanel, "Speech", "해냈어!", Vector2.zero, new Vector2(480, 92), 24);
            speech.color = new Color(0.04f, 0.07f, 0.1f);

            RectTransform stagePanel = Rect(safe, "SequenceStage", new Vector2(400, -20), new Vector2(860, 700));
            Image stageBackground = Component<Image>(stagePanel.gameObject);
            stageBackground.color = new Color(0.09f, 0.105f, 0.18f, 1f);
            TMP_Text stageTitle = Text(stagePanel, "StageTitle", "결과 저장 중...", new Vector2(0, 205), new Vector2(760, 90), 40);
            TMP_Text stageBody = Text(stagePanel, "StageBody", "성장 결과는 서버 확인 후 공개됩니다.",
                new Vector2(0, 35), new Vector2(740, 230), 25);
            TMP_Text saveStatus = Text(stagePanel, "SaveStatus", "", new Vector2(0, 292), new Vector2(720, 40), 18);
            saveStatus.alignment = TextAlignmentOptions.Right;

            RectTransform gauge = Rect(stagePanel, "ExperienceGauge", new Vector2(0, -105), new Vector2(620, 34));
            Image gaugeBackground = Component<Image>(gauge.gameObject);
            gaugeBackground.color = new Color(0.04f, 0.055f, 0.09f);
            Slider experienceBar = Component<Slider>(gauge.gameObject);
            RectTransform fillArea = Rect(gauge, "FillArea", Vector2.zero, new Vector2(600, 20));
            RectTransform fill = Rect(fillArea, "Fill", Vector2.zero, new Vector2(600, 20));
            Image fillImage = Component<Image>(fill.gameObject);
            fillImage.color = new Color(0.25f, 0.82f, 0.72f);
            experienceBar.fillRect = fill;
            experienceBar.targetGraphic = fillImage;
            experienceBar.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            experienceBar.minValue = 0f;
            experienceBar.maxValue = 1f;
            experienceBar.interactable = false;
            TMP_Text experienceText = Text(stagePanel, "ExperienceText", "Lv. 1    0 / 400",
                new Vector2(0, -155), new Vector2(620, 42), 20);

            Transform obsoleteRetry = stagePanel.Find("RetrySaveButton");
            if (obsoleteRetry != null) Undo.DestroyObjectImmediate(obsoleteRetry.gameObject);

            RectTransform actions = Rect(stagePanel, "ResultActions", new Vector2(0, -245), new Vector2(780, 70));
            FrontendStartButton retryRun = MenuButton(actions, "RetryRunButton", "다시 도전", 0);
            retryRun.GetComponent<RectTransform>().anchoredPosition = new Vector2(-195, 0);
            FrontendStartButton home = MenuButton(actions, "ResultHomeButton", "홈으로", 0);
            home.GetComponent<RectTransform>().anchoredPosition = new Vector2(195, 0);
            retryRun.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = home, selectOnLeft = home };
            home.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = retryRun, selectOnLeft = retryRun };

            AudioSource audioSource = Component<AudioSource>(root.gameObject);
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            FrontendRunResultView resultView = Component<FrontendRunResultView>(titleView.gameObject);
            resultView.Configure(titleView, root.gameObject, heading, characterName, reaction, speech, stageTitle,
                stageBody, experienceBar, experienceText, saveStatus, actions.gameObject, retryRun, home,
                audioSource);
            root.gameObject.SetActive(false);
        }

        private static void SetupSettings(FrontendHomeView homeView, RectTransform frame)
        {
            RectTransform root = Rect(frame, "SettingsPanel", Vector2.zero, new Vector2(720, 720));
            Image background = Component<Image>(root.gameObject);
            background.color = new Color(0.06f, 0.1f, 0.14f);
            background.raycastTarget = true;

            Text(root, "SettingsTitle", "설정", new Vector2(0, 310), new Vector2(680, 60), 40);

            // Volume Section
            Text(root, "VolumeSection", "음량 설정", new Vector2(-200, 245), new Vector2(280, 40), 28);

            TMP_Text masterLabel = Text(root, "MasterVolumeLabel", "전체 음량", new Vector2(-200, 195), new Vector2(160, 32), 20);
            masterLabel.alignment = TextAlignmentOptions.Left;
            Slider masterSlider = Slider(root, "MasterVolumeSlider", new Vector2(60, 195), new Vector2(320, 32));
            TMP_Text masterValue = Text(root, "MasterVolumeValue", "100%", new Vector2(280, 195), new Vector2(80, 32), 20);

            TMP_Text bgmLabel = Text(root, "BgmVolumeLabel", "배경음", new Vector2(-200, 145), new Vector2(160, 32), 20);
            bgmLabel.alignment = TextAlignmentOptions.Left;
            Slider bgmSlider = Slider(root, "BgmVolumeSlider", new Vector2(60, 145), new Vector2(320, 32));
            TMP_Text bgmValue = Text(root, "BgmVolumeValue", "100%", new Vector2(280, 145), new Vector2(80, 32), 20);

            TMP_Text sfxLabel = Text(root, "SfxVolumeLabel", "효과음", new Vector2(-200, 95), new Vector2(160, 32), 20);
            sfxLabel.alignment = TextAlignmentOptions.Left;
            Slider sfxSlider = Slider(root, "SfxVolumeSlider", new Vector2(60, 95), new Vector2(320, 32));
            TMP_Text sfxValue = Text(root, "SfxVolumeValue", "100%", new Vector2(280, 95), new Vector2(80, 32), 20);

            // Display Section
            Text(root, "DisplaySection", "화면 설정", new Vector2(-200, 30), new Vector2(280, 40), 28);

            TMP_Text resolutionLabel = Text(root, "ResolutionLabel", "해상도", new Vector2(-200, -20), new Vector2(160, 32), 20);
            resolutionLabel.alignment = TextAlignmentOptions.Left;
            TMP_Dropdown resolutionDropdown = Dropdown(root, "ResolutionDropdown", new Vector2(100, -20), new Vector2(220, 40));

            TMP_Text fullscreenLabel = Text(root, "FullscreenLabel", "전체 화면", new Vector2(-200, -75), new Vector2(160, 32), 20);
            fullscreenLabel.alignment = TextAlignmentOptions.Left;
            Toggle fullscreenToggle = Toggle(root, "FullscreenToggle", new Vector2(20, -75), new Vector2(40, 40));

            Text(root, "HudSection", "HUD 설정", new Vector2(-200, -140), new Vector2(280, 40), 28);
            TMP_Text detailedStatsLabel = Text(root, "DetailedStatsHudLabel", "상세 스탯 표시", new Vector2(-200, -190), new Vector2(200, 32), 20);
            detailedStatsLabel.alignment = TextAlignmentOptions.Left;
            Toggle detailedStatsToggle = Toggle(root, "DetailedStatsHudToggle", new Vector2(20, -190), new Vector2(40, 40));
            detailedStatsToggle.isOn = false;

            // Back button
            FrontendStartButton backButton = MenuButton(root, "SettingsBackButton", "뒤로", -290);
            backButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 56);

            // Configure view
            FrontendSettingsView settingsView = Component<FrontendSettingsView>(root.gameObject);
            settingsView.Configure(
                masterSlider, masterValue,
                bgmSlider, bgmValue,
                sfxSlider, sfxValue,
                resolutionDropdown,
                fullscreenToggle,
                backButton,
                detailedStatsToggle);
            homeView.ConfigureSettings(root.gameObject, settingsView);
            root.gameObject.SetActive(false);
        }

        private static Slider Slider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Slider slider = Component<Slider>(rect.gameObject);

            // Background
            RectTransform bgRect = Rect(rect, "Background", Vector2.zero, Vector2.zero);
            Stretch(bgRect);
            Image bgImage = Component<Image>(bgRect.gameObject);
            bgImage.color = new Color(0.15f, 0.2f, 0.25f);

            // Fill Area
            RectTransform fillArea = Rect(rect, "Fill Area", Vector2.zero, Vector2.zero);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(5, 0);
            fillArea.offsetMax = new Vector2(-5, 0);

            RectTransform fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = Component<Image>(fill.gameObject);
            fillImage.color = new Color(0.18f, 0.68f, 0.64f);

            // Handle Area
            RectTransform handleArea = Rect(rect, "Handle Slide Area", Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10, 0);
            handleArea.offsetMax = new Vector2(-10, 0);

            RectTransform handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(20, 0));
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(0, 1);
            Image handleImage = Component<Image>(handle.gameObject);
            handleImage.color = new Color(0.92f, 0.96f, 1);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.selectedColor = Color.white;
            slider.colors = colors;

            return slider;
        }

        private static TMP_Dropdown Dropdown(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Image bgImage = Component<Image>(rect.gameObject);
            bgImage.color = new Color(0.15f, 0.2f, 0.25f);

            TMP_Dropdown dropdown = Component<TMP_Dropdown>(rect.gameObject);

            // Label
            TMP_Text label = Text(rect, "Label", "", Vector2.zero, Vector2.zero, 18);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10, 6);
            label.rectTransform.offsetMax = new Vector2(-35, -7);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            // Arrow
            RectTransform arrowRect = Rect(rect, "Arrow", new Vector2(-15, 0), new Vector2(20, 20));
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1, 0.5f);
            Image arrowImage = Component<Image>(arrowRect.gameObject);
            arrowImage.color = new Color(0.92f, 0.96f, 1);
            arrowImage.raycastTarget = false;

            // Template
            RectTransform template = Rect(rect, "Template", new Vector2(0, -size.y / 2), new Vector2(size.x, 126));
            template.pivot = new Vector2(0.5f, 1);
            Image templateBg = Component<Image>(template.gameObject);
            templateBg.color = new Color(0.1f, 0.14f, 0.18f);
            Component<CanvasGroup>(template.gameObject);
            ScrollRect scrollRect = Component<ScrollRect>(template.gameObject);

            // Viewport
            RectTransform viewport = Rect(template, "Viewport", Vector2.zero, Vector2.zero);
            Stretch(viewport);
            Mask mask = Component<Mask>(viewport.gameObject);
            mask.showMaskGraphic = false;
            Component<Image>(viewport.gameObject).color = Color.white;

            // Content
            RectTransform content = Rect(viewport, "Content", Vector2.zero, new Vector2(0, 40));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);

            // Item
            RectTransform item = Rect(content, "Item", Vector2.zero, new Vector2(0, 40));
            item.anchorMin = new Vector2(0, 0.5f);
            item.anchorMax = new Vector2(1, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f);
            Toggle itemToggle = Component<Toggle>(item.gameObject);

            // Item Background
            RectTransform itemBgRect = Rect(item, "Item Background", Vector2.zero, Vector2.zero);
            Stretch(itemBgRect);
            Image itemBgImage = Component<Image>(itemBgRect.gameObject);
            itemBgImage.color = new Color(0.18f, 0.22f, 0.28f);

            // Item Checkmark
            RectTransform checkRect = Rect(item, "Item Checkmark", new Vector2(10, 0), new Vector2(20, 20));
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0, 0.5f);
            Image checkImage = Component<Image>(checkRect.gameObject);
            checkImage.color = new Color(0.18f, 0.68f, 0.64f);

            // Item Label
            TMP_Text itemLabel = Text(item, "Item Label", "", Vector2.zero, Vector2.zero, 20);
            itemLabel.rectTransform.anchorMin = Vector2.zero;
            itemLabel.rectTransform.anchorMax = Vector2.one;
            itemLabel.rectTransform.offsetMin = new Vector2(35, 2);
            itemLabel.rectTransform.offsetMax = new Vector2(-10, -2);
            itemLabel.alignment = TextAlignmentOptions.Left;

            itemToggle.targetGraphic = itemBgImage;
            itemToggle.graphic = checkImage;
            itemToggle.isOn = true;

            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.template = template;
            dropdown.targetGraphic = bgImage;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.selectedColor = Color.white;
            dropdown.colors = colors;

            template.gameObject.SetActive(false);

            return dropdown;
        }

        private static Toggle Toggle(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Toggle toggle = Component<Toggle>(rect.gameObject);

            // Background
            RectTransform bgRect = Rect(rect, "Background", Vector2.zero, size);
            Image bgImage = Component<Image>(bgRect.gameObject);
            bgImage.color = new Color(0.15f, 0.2f, 0.25f);

            // Checkmark
            RectTransform checkRect = Rect(rect, "Checkmark", Vector2.zero, size * 0.6f);
            Image checkImage = Component<Image>(checkRect.gameObject);
            checkImage.color = new Color(0.18f, 0.68f, 0.64f);

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.selectedColor = Color.white;
            toggle.colors = colors;

            return toggle;
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
