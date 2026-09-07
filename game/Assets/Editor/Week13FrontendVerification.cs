using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Network;
using TrickalFanGame.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13FrontendVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Frontend Flow")]
        public static void Verify()
        {
            Scene scene = SceneManager.GetActiveScene();
            Assert(scene.path == Week13FrontendSetup.ScenePath, "Open FrontendScene before verification.");
            ValidateScene(scene);
            if (!EditorApplication.isPlaying) VerifyLayouts(scene);
            Debug.Log("Week 13 Frontend verification passed: scene isolation, build entry, references, scaler, safe area, settings (Setting-1A/1B) and screen layouts.");
        }

        public static void ValidateScene(Scene scene)
        {
            Assert(NicknameValidator.Validate("Erpin").IsValid && NicknameValidator.Validate("erpin").IsValid,
                "Unity nickname validation must accept and preserve both uppercase and lowercase Latin input.");
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Assert(enabled.Length >= 2 && enabled[0].path == Week13FrontendSetup.ScenePath,
                "Frontend must be the first enabled build scene.");
            Assert(enabled.Count(s => s.path == Week13FrontendSetup.ScenePath) == 1 &&
                enabled.Count(s => s.path == Week13FrontendSetup.GameScenePath) == 1, "Duplicate or missing build scene.");
            Component[] components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            foreach (Component component in components)
            {
                Assert(component != null, "Missing script in Frontend.");
                string ns = component.GetType().Namespace ?? "";
                Assert(!ns.StartsWith("TrickalFanGame.") || ns == "TrickalFanGame.Frontend" || component is ApiClient,
                    "Combat, Run or Backend component leaked into Frontend: " + component.GetType().Name);
                Assert(component is not Renderer && component is not Collider2D && component is not Rigidbody2D,
                    "World combat object leaked into Frontend.");
            }
            Canvas[] rootCanvases = scene.GetRootGameObjects().Select(root => root.GetComponent<Canvas>())
                .Where(rootCanvas => rootCanvas != null).ToArray();
            Assert(rootCanvases.Length == 1, "Exactly one root Canvas is required.");
            Assert(components.OfType<Camera>().Count() == 1 && components.OfType<Camera>().Single().cullingMask == 0,
                "Frontend camera must only clear the background, without rendering world objects.");
            Assert(components.OfType<EventSystem>().Count() == 1 && components.OfType<InputSystemUIInputModule>().Count() == 1,
                "Exactly one EventSystem and Input System UI module are required.");
            Canvas canvas = rootCanvases.Single();
            Assert(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.GetComponent<GraphicRaycaster>() != null,
                "Frontend requires Overlay Canvas and GraphicRaycaster.");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080) &&
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f), "Canvas Scaler differs from UI spec.");
            Assert(components.OfType<FrontendLayout>().Count() == 1, "Exactly one reference frame is required.");
            RectTransform safe = canvas.transform.Find("ReferenceFrame/SafeArea") as RectTransform;
            Assert(safe != null && safe.anchorMin == Vector2.zero && safe.anchorMax == Vector2.one &&
                safe.offsetMin == new Vector2(64, 54) && safe.offsetMax == new Vector2(-64, -54), "Safe area differs from UI spec.");
            Assert(components.OfType<FrontendTitleView>().Count() == 1 && components.OfType<Button>().Count() == 8,
                "The title, nickname, home, destination and settings screens require exactly eight buttons.");
            FrontendTitleView view = components.OfType<FrontendTitleView>().Single();
            Assert(view.TitlePanel != null && view.StartButton != null && view.StatusText != null &&
                view.StartButton is FrontendStartButton && view.HomeView != null,
                "Missing title references.");
            Assert(view.StartButton.gameObject.scene == scene && view.StatusText.gameObject.scene == scene,
                "Title references must stay inside Frontend.");
            Assert(view.StartButton.GetComponent<RectTransform>().sizeDelta == new Vector2(280, 64), "Start button must be 280 x 64.");
            TMP_Text label = view.StartButton.GetComponentInChildren<TMP_Text>();
            Assert(label != null && label.text == "GAME START" && label.fontSize == 24 && !label.raycastTarget,
                "Start label differs from UI spec.");
            Assert(components.OfType<EventSystem>().Single().firstSelectedGameObject == view.StartButton.gameObject,
                "Initial focus must point to GAME START.");
            Assert(view.StartButton.onClick.GetPersistentEventCount() == 0, "Unexpected persistent action on title button.");
            var serializedButton = new SerializedObject(view.StartButton);
            Assert(serializedButton.FindProperty("hoverBorder").objectReferenceValue != null &&
                serializedButton.FindProperty("focusBorder").objectReferenceValue != null, "Missing button border references.");
            ValidateHome(view.HomeView, scene);
            ValidateSettings(view.HomeView, scene);
            foreach (TMP_Text text in components.OfType<TMP_Text>())
            {
                Assert(text.font != null, "Missing TMP font: " + text.name);
                // The registered nickname is user data. The dynamic font resolves its allowed Korean/Latin/digit
                // glyphs during rendering, so only fixed UI copy can be preflighted here.
                // Empty strings are runtime-populated (dropdown templates, volume percentages).
                bool runtimeText = text == view.HomeView.WelcomeText ||
                    text == view.NicknameView.ValidationText ||
                    text == view.NicknameView.ErrorText ||
                    string.IsNullOrEmpty(text.text);
                if (!runtimeText)
                    Assert(text.font.HasCharacters(text.text), "Missing TMP glyph: " + text.name);
            }
            Assert(view.StatusText.font.HasCharacters(FrontendTitleView.ReadyMessage), "Missing Korean status glyphs.");
        }

        private static void ValidateHome(FrontendHomeView home, Scene scene)
        {
            Assert(home.gameObject.scene == scene && home.name == "HomeScreen", "Home must stay in FrontendScene.");
            Assert(home.GetComponent<Image>() != null && home.GetComponent<Image>().raycastTarget == false,
                "Home requires a dedicated non-interactive temporary background.");
            Assert(home.HomePanel != null && home.WelcomeText != null && home.GameStartButton != null &&
                home.SkillUpgradeButton != null && home.SettingsButton != null && home.QuitButton != null &&
                home.DestinationPanel != null && home.DestinationTitle != null && home.BackButton != null,
                "Missing Home references.");
            Button[] menuButtons = { home.GameStartButton, home.SkillUpgradeButton, home.SettingsButton, home.QuitButton };
            Assert(menuButtons.Distinct().Count() == 4 && menuButtons.All(button => button is FrontendStartButton),
                "Home requires four distinct styled buttons.");
            Assert(menuButtons.All(button => button.GetComponent<RectTransform>().sizeDelta == new Vector2(360, 64)),
                "Home buttons must be 360 x 64.");
            Assert(menuButtons.All(button => button.navigation.mode == Navigation.Mode.Explicit &&
                button.navigation.selectOnUp != null && button.navigation.selectOnDown != null),
                "Home keyboard navigation must form an explicit loop.");
            Assert(menuButtons.Append(home.BackButton).All(button => button.onClick.GetPersistentEventCount() == 0),
                "Frontend actions must be wired once at runtime, not as persistent Scene calls.");
            Assert(home.SettingsPanel != null && home.SettingsView != null, "Home requires settings panel and view.");
        }

        private static void ValidateSettings(FrontendHomeView home, Scene scene)
        {
            FrontendSettingsView settings = home.SettingsView;
            Assert(settings != null, "Settings view is required.");
            Assert(settings.gameObject.scene == scene && settings.name == "SettingsPanel", "Settings panel must stay in FrontendScene.");

            Assert(settings.MasterVolumeSlider != null, "Missing master volume slider.");
            Assert(settings.BgmVolumeSlider != null, "Missing BGM volume slider.");
            Assert(settings.SfxVolumeSlider != null, "Missing SFX volume slider.");
            Assert(settings.MasterVolumeLabel != null, "Missing master volume label.");
            Assert(settings.BgmVolumeLabel != null, "Missing BGM volume label.");
            Assert(settings.SfxVolumeLabel != null, "Missing SFX volume label.");
            Assert(settings.ResolutionDropdown != null, "Missing resolution dropdown.");
            Assert(settings.FullscreenToggle != null, "Missing fullscreen toggle.");
            Assert(settings.BackButton != null && settings.BackButton is FrontendStartButton, "Missing styled settings back button.");

            Slider[] sliders = { settings.MasterVolumeSlider, settings.BgmVolumeSlider, settings.SfxVolumeSlider };
            Assert(sliders.Distinct().Count() == 3, "Settings requires three distinct volume sliders.");
            Assert(sliders.All(s => s.minValue == 0 && s.maxValue == 1), "Volume sliders must be normalized 0-1.");

            ValidateLocalSettings();
        }

        private static void ValidateLocalSettings()
        {
            Assert(LocalSettings.SupportedResolutions.Length == 3, "Exactly 3 resolutions must be supported.");
            var expected = new (int w, int h)[] { (1280, 720), (1920, 1080), (2560, 1440) };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert(LocalSettings.SupportedResolutions[i].width == expected[i].w &&
                    LocalSettings.SupportedResolutions[i].height == expected[i].h,
                    $"Resolution {i} must be {expected[i].w}x{expected[i].h}.");
                float ratio = (float)expected[i].w / expected[i].h;
                Assert(Mathf.Abs(ratio - 16f / 9f) < 0.001f, "All resolutions must be 16:9.");
            }

            float originalMaster = LocalSettings.MasterVolume;
            float originalBgm = LocalSettings.BgmVolume;
            float originalSfx = LocalSettings.SfxVolume;
            bool originalFullscreen = LocalSettings.Fullscreen;
            int originalWidth = LocalSettings.ResolutionWidth;
            int originalHeight = LocalSettings.ResolutionHeight;

            try
            {
                LocalSettings.MasterVolume = 0.5f;
                LocalSettings.BgmVolume = 0.75f;
                LocalSettings.SfxVolume = 0.25f;
                LocalSettings.Fullscreen = false;
                LocalSettings.SetResolution(1280, 720);

                Assert(Mathf.Approximately(LocalSettings.MasterVolume, 0.5f), "Master volume save failed.");
                Assert(Mathf.Approximately(LocalSettings.BgmVolume, 0.75f), "BGM volume save failed.");
                Assert(Mathf.Approximately(LocalSettings.SfxVolume, 0.25f), "SFX volume save failed.");
                Assert(!LocalSettings.Fullscreen, "Fullscreen save failed.");
                Assert(LocalSettings.ResolutionWidth == 1280 && LocalSettings.ResolutionHeight == 720, "Resolution save failed.");
                Assert(LocalSettings.GetResolutionIndex() == 0, "Resolution index calculation failed.");

                LocalSettings.Load();
                Assert(Mathf.Approximately(LocalSettings.MasterVolume, 0.5f), "Master volume load failed.");
                Assert(Mathf.Approximately(LocalSettings.BgmVolume, 0.75f), "BGM volume load failed.");
                Assert(Mathf.Approximately(LocalSettings.SfxVolume, 0.25f), "SFX volume load failed.");
                Assert(!LocalSettings.Fullscreen, "Fullscreen load failed.");
                Assert(LocalSettings.ResolutionWidth == 1280 && LocalSettings.ResolutionHeight == 720, "Resolution load failed.");

                Assert(LocalSettings.IsValidResolution(1920, 1080), "Valid resolution rejected.");
                Assert(!LocalSettings.IsValidResolution(1600, 900), "Invalid resolution accepted.");

                Debug.Log("LocalSettings save/restore verification passed.");
            }
            finally
            {
                LocalSettings.MasterVolume = originalMaster;
                LocalSettings.BgmVolume = originalBgm;
                LocalSettings.SfxVolume = originalSfx;
                LocalSettings.Fullscreen = originalFullscreen;
                LocalSettings.SetResolution(originalWidth, originalHeight);
            }
        }

        // Exercise actual RectTransforms/TMP preferred sizes without changing the open scene.
        private static void VerifyLayouts(Scene source)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject clone = null;
            try
            {
                clone = Object.Instantiate(source.GetRootGameObjects().Single(r => r.GetComponent<Canvas>() != null));
                SceneManager.MoveGameObjectToScene(clone, preview);
                Canvas canvas = clone.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                clone.GetComponent<CanvasScaler>().enabled = false;
                FrontendLayout layout = clone.GetComponentInChildren<FrontendLayout>();
                RectTransform canvasRect = (RectTransform)clone.transform;
                RectTransform safe = (RectTransform)layout.transform.Find("SafeArea");
                FrontendTitleView view = clone.GetComponent<FrontendTitleView>();
                FrontendHomeView home = view.HomeView;
                RectTransform homeSafe = (RectTransform)home.HomePanel.transform.Find("SafeArea");
                foreach (Vector2 resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(1600, 1200), new Vector2(2560, 1080) })
                {
                    canvasRect.sizeDelta = resolution;
                    layout.ApplyLayout();
                    Canvas.ForceUpdateCanvases();
                    foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.ForceMeshUpdate(true);
                        Vector2 preferred = text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity);
                        Assert(preferred.y <= text.rectTransform.rect.height + 1, $"{resolution}: text height overflow: {text.name}");
                        Assert(text.preferredWidth <= text.rectTransform.rect.width + 1, $"{resolution}: text width overflow: {text.name}");
                        AssertInside(text.rectTransform, (RectTransform)layout.transform, resolution + " content frame: " + text.name);
                    }
                    AssertInside(safe, canvasRect, resolution + " safe area outside viewport");
                    AssertInside(view.StartButton.GetComponent<RectTransform>(), safe, resolution + " button");
                    AssertInside(homeSafe, (RectTransform)layout.transform, resolution + " home safe area");
                    foreach (Button homeButton in new[] { home.GameStartButton, home.SkillUpgradeButton, home.SettingsButton, home.QuitButton })
                        AssertInside(homeButton.GetComponent<RectTransform>(), homeSafe, resolution + " home button: " + homeButton.name);
                    AssertInside(home.BackButton.GetComponent<RectTransform>(), (RectTransform)layout.transform, resolution + " back button");
                    Assert(Mathf.Abs(((RectTransform)layout.transform).rect.width / ((RectTransform)layout.transform).rect.height - 16f / 9f) < 0.001f,
                        "Content aspect ratio changed.");
                    Debug.Log($"Week 13 layout passed: {resolution.x} x {resolution.y}; TMP bounds and safe area.");
                }
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Trickal Fan Game/Week 13/Export Home Previews (720p and 1080p)")]
        public static void ExportPreviews()
        {
            Scene source = SceneManager.GetActiveScene();
            Assert(source.path == Week13FrontendSetup.ScenePath, "Open FrontendScene first.");
            ValidateScene(source);
            string directory = "Logs/Week13FrontendPreviews";
            Directory.CreateDirectory(directory);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject clone = Object.Instantiate(source.GetRootGameObjects().Single(r => r.GetComponent<Canvas>() != null));
                SceneManager.MoveGameObjectToScene(clone, preview);
                Canvas canvas = clone.GetComponent<Canvas>();
                FrontendTitleView frontend = clone.GetComponent<FrontendTitleView>();
                frontend.TitlePanel.SetActive(false);
                frontend.HomeView.Show("FlowTester");
                clone.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                clone.transform.position = Vector3.zero;
                clone.transform.localScale = Vector3.one;
                var cameraObject = new GameObject("Title Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100;
                camera.enabled = false;
                canvas.worldCamera = camera;
                foreach (Vector2Int resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    ((RectTransform)clone.transform).sizeDelta = resolution;
                    clone.GetComponentInChildren<FrontendLayout>().ApplyLayout();
                    camera.orthographicSize = resolution.y / 2f;
                    camera.aspect = (float)resolution.x / resolution.y;
                    Canvas.ForceUpdateCanvases();
                    foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
                    RenderTexture target = new RenderTexture(resolution.x, resolution.y, 24);
                    RenderTexture previous = RenderTexture.active;
                    Texture2D image = null;
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        image = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
                        image.Apply();
                        string path = Path.Combine(directory, $"home-{resolution.x}x{resolution.y}.png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        Debug.Log("Home preview exported: " + Path.GetFullPath(path));
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previous;
                        if (image != null) Object.DestroyImmediate(image);
                        target.Release();
                        Object.DestroyImmediate(target);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void AssertInside(RectTransform child, RectTransform parent, string message)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Rect bounds = parent.rect;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = parent.InverseTransformPoint(corner);
                Assert(point.x >= bounds.xMin - 0.1f && point.x <= bounds.xMax + 0.1f &&
                    point.y >= bounds.yMin - 0.1f && point.y <= bounds.yMax + 0.1f, message);
            }
        }

        public static void SetupAndVerifyBatch()
        {
            string gameBefore = File.ReadAllText(Week13FrontendSetup.GameScenePath, System.Text.Encoding.UTF8);
            Week13FrontendSetup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            int count = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Week13FrontendSetup.Setup();
            Assert(guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath), "Setup changed scene GUID.");
            Assert(count == Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Setup duplicated scene objects.");
            Assert(gameBefore == File.ReadAllText(Week13FrontendSetup.GameScenePath, System.Text.Encoding.UTF8), "Setup changed Game Scene.");
            Verify();
            VerifyRejections();
            ExportPreviews();
            Debug.Log("Week 13 batch verification passed: setup twice, stable GUID, unchanged Game Scene, invalid-state rejection.");
        }

        private static void VerifyRejections()
        {
            Scene scene = SceneManager.GetActiveScene();
            FrontendTitleView view = Object.FindFirstObjectByType<FrontendTitleView>(FindObjectsInactive.Include);
            Button button = view.StartButton;
            TMP_Text status = view.StatusText;
            try
            {
                view.Configure(null, status);
                ExpectFailure(() => ValidateScene(scene), "missing reference");
            }
            finally { view.Configure(button, status); }
            GameObject duplicate = Object.Instantiate(button.gameObject, button.transform.parent);
            try { ExpectFailure(() => ValidateScene(scene), "duplicate button"); }
            finally { Object.DestroyImmediate(duplicate); }
            GameObject combat = new GameObject("Invalid Combat Object", typeof(BoxCollider2D));
            try { ExpectFailure(() => ValidateScene(scene), "combat object"); }
            finally { Object.DestroyImmediate(combat); }
            var builds = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = builds.Reverse().ToArray();
                ExpectFailure(() => ValidateScene(scene), "wrong boot scene");
            }
            finally { EditorBuildSettings.scenes = builds; }
            ValidateScene(scene);
        }

        private static void ExpectFailure(Action action, string label)
        {
            bool failed = false;
            try { action(); } catch (InvalidOperationException) { failed = true; }
            Assert(failed, "Validator accepted " + label);
        }

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }

    [InitializeOnLoad]
    public static class Week13FrontendPlayVerification
    {
        private const string PendingKey = "Week13FrontendPlayVerification.Pending";
        private static double started;
        private static bool hadProfile;
        private static string previousUserId;
        private static string previousNickname;

        static Week13FrontendPlayVerification()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        // Run separately without -quit: the Editor exits only after Play Mode assertions finish.
        public static void RunBatch()
        {
            hadProfile = LocalProfile.IsRegistered;
            previousUserId = LocalProfile.UserId;
            previousNickname = LocalProfile.Nickname;
            LocalProfile.ClearProfile();
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                if (EditorApplication.timeSinceStartup - started > 120) Finish(1, "Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;
            try
            {
                Week13FrontendVerification.ValidateScene(SceneManager.GetActiveScene());
                FrontendTitleView view = Object.FindFirstObjectByType<FrontendTitleView>();
                if (EventSystem.current.currentSelectedGameObject != view.StartButton.gameObject)
                    throw new InvalidOperationException("Initial keyboard focus is missing.");
                int before = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                view.StartButton.onClick.Invoke();
                if (view.NicknameView == null || !view.NicknameView.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("A new local player did not reach nickname registration.");

                var retryApi = new RetryRegistrationApiClient();
                view.NicknameView.SetApiClient(retryApi);
                string clientProfileId = LocalProfile.ClientProfileId;
                view.NicknameView.NicknameInput.text = "FlowTester";
                view.NicknameView.SubmitButton.onClick.Invoke();
                if (retryApi.Requests.Count != 1 || retryApi.Requests[0].clientProfileId != clientProfileId ||
                    LocalProfile.IsRegistered || !view.NicknameView.ErrorText.gameObject.activeInHierarchy ||
                    !view.NicknameView.SubmitButton.interactable)
                    throw new InvalidOperationException("Failed registration did not preserve a retryable local profile request.");

                view.NicknameView.SubmitButton.onClick.Invoke();
                if (retryApi.Requests.Count != 2 || retryApi.Requests.Any(request => request.clientProfileId != clientProfileId) ||
                    !LocalProfile.IsRegistered || LocalProfile.UserId != "week13-profile-user" ||
                    LocalProfile.Nickname != "FlowTester")
                    throw new InvalidOperationException("Registration retry changed identity or failed to save the successful profile.");

                FrontendHomeView home = view.HomeView;
                if (!home.gameObject.activeInHierarchy || home.WelcomeText.text != "FlowTester 님, 환영합니다")
                    throw new InvalidOperationException("Successful registration retry did not reach Home.");

                view.ShowTitleScreen();
                view.StartButton.onClick.Invoke();
                if (!home.gameObject.activeInHierarchy || view.TitlePanel.activeInHierarchy ||
                    home.WelcomeText.text != "FlowTester 님, 환영합니다" || retryApi.Requests.Count != 2)
                    throw new InvalidOperationException("A saved local profile did not reach Home.");

                VerifyDestination(home, home.GameStartButton, FrontendDestination.CharacterSelection,
                    FrontendHomeView.CharacterSelectionTitle);
                VerifyDestination(home, home.SkillUpgradeButton, FrontendDestination.SkillUpgrade,
                    FrontendHomeView.SkillUpgradeTitle);
                VerifySettingsScreen(home);
                bool quitRequested = false;
                home.OnQuitRequested += () => quitRequested = true;
                home.RequestQuit();
                if (!quitRequested) throw new InvalidOperationException("Quit action was not requested.");
                if (SceneManager.sceneCount != 1 || before != Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length)
                    throw new InvalidOperationException("Frontend navigation created objects or changed scenes.");
                Week13FrontendVerification.ValidateScene(SceneManager.GetActiveScene());
                Finish(0, "Week 13 profile retry verification passed: stable clientProfileId, retry after failure, saved user identity, registration-to-Home, restart path and combat isolation.");
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }

        private static void VerifyDestination(
            FrontendHomeView home,
            Button button,
            FrontendDestination expected,
            string expectedTitle)
        {
            button.onClick.Invoke();
            if (home.CurrentDestination != expected || !home.DestinationPanel.activeInHierarchy ||
                home.HomePanel.activeInHierarchy || home.DestinationTitle.text != expectedTitle ||
                EventSystem.current.currentSelectedGameObject != home.BackButton.gameObject)
                throw new InvalidOperationException("Home action did not reach " + expected + ".");
            home.BackButton.onClick.Invoke();
            if (home.CurrentDestination != null || !home.HomePanel.activeInHierarchy ||
                home.DestinationPanel.activeInHierarchy ||
                EventSystem.current.currentSelectedGameObject != home.GameStartButton.gameObject)
                throw new InvalidOperationException("Back did not return to Home from " + expected + ".");
        }

        private static void VerifySettingsScreen(FrontendHomeView home)
        {
            FrontendSettingsView settings = home.SettingsView;
            if (settings == null)
                throw new InvalidOperationException("Settings view is missing.");

            home.SettingsButton.onClick.Invoke();
            if (home.CurrentDestination != FrontendDestination.Settings ||
                !home.SettingsPanel.activeInHierarchy ||
                home.HomePanel.activeInHierarchy ||
                EventSystem.current.currentSelectedGameObject != settings.MasterVolumeSlider.gameObject)
                throw new InvalidOperationException("Settings button did not reach settings screen.");

            float originalMaster = LocalSettings.MasterVolume;
            float originalBgm = LocalSettings.BgmVolume;
            float originalSfx = LocalSettings.SfxVolume;
            bool originalFullscreen = LocalSettings.Fullscreen;

            try
            {
                settings.MasterVolumeSlider.value = 0.5f;
                if (!Mathf.Approximately(LocalSettings.MasterVolume, 0.5f))
                    throw new InvalidOperationException("Master volume slider did not save to LocalSettings.");
                if (settings.MasterVolumeLabel.text != "50%")
                    throw new InvalidOperationException("Master volume label did not update.");

                settings.BgmVolumeSlider.value = 0.75f;
                if (!Mathf.Approximately(LocalSettings.BgmVolume, 0.75f))
                    throw new InvalidOperationException("BGM volume slider did not save to LocalSettings.");

                settings.SfxVolumeSlider.value = 0.25f;
                if (!Mathf.Approximately(LocalSettings.SfxVolume, 0.25f))
                    throw new InvalidOperationException("SFX volume slider did not save to LocalSettings.");

                settings.FullscreenToggle.isOn = !originalFullscreen;
                if (LocalSettings.Fullscreen == originalFullscreen)
                    throw new InvalidOperationException("Fullscreen toggle did not save to LocalSettings.");
            }
            finally
            {
                LocalSettings.MasterVolume = originalMaster;
                LocalSettings.BgmVolume = originalBgm;
                LocalSettings.SfxVolume = originalSfx;
                LocalSettings.Fullscreen = originalFullscreen;
            }

            settings.BackButton.onClick.Invoke();
            if (home.CurrentDestination != null || !home.HomePanel.activeInHierarchy ||
                home.SettingsPanel.activeInHierarchy ||
                EventSystem.current.currentSelectedGameObject != home.GameStartButton.gameObject)
                throw new InvalidOperationException("Settings back button did not return to Home.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            if (hadProfile) LocalProfile.SaveProfile(previousUserId, previousNickname);
            else LocalProfile.ClearProfile();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(code);
        }

        private sealed class RetryRegistrationApiClient : IGameApiClient
        {
            public readonly System.Collections.Generic.List<CreateUserRequest> Requests = new();

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
            {
                Requests.Add(new CreateUserRequest
                {
                    clientProfileId = request.clientProfileId,
                    nickname = request.nickname
                });
                if (Requests.Count == 1)
                {
                    onError?.Invoke("NETWORK_ERROR: retry verification");
                    return;
                }

                onSuccess?.Invoke(new CreateUserResponse
                {
                    success = true,
                    data = new CreateUserData
                    {
                        id = "week13-profile-user",
                        clientProfileId = request.clientProfileId,
                        nickname = request.nickname,
                        characterProgress = Array.Empty<CreateUserCharacterProgressDto>()
                    }
                });
            }

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("Profile retry verification must not query progression.");
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("Profile retry verification must not create a Run.");
            }
        }
    }
}
