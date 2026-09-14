using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1BVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Setting-1B Display")]
        public static void Verify()
        {
            ValidateSupportedResolutions();
            ValidateViewportMath();
            ValidateScene(Week13FrontendSetup.ScenePath, "FrontendCamera", "FrontendCanvas");
            ValidateScene(Week13FrontendSetup.GameScenePath, "Main Camera", Week13Hud1Setup.HudRootName);
            Assert(PlayerSettings.defaultScreenWidth == LocalSettings.DefaultWidth &&
                   PlayerSettings.defaultScreenHeight == LocalSettings.DefaultHeight,
                "Player default resolution must be 1920x1080.");
            Assert(PlayerSettings.fullScreenMode == FullScreenMode.FullScreenWindow,
                "The default full-screen mode must be borderless full screen.");
            Assert(!PlayerSettings.resizableWindow,
                "Window resizing must stay disabled so only supported 16:9 sizes are exposed.");
            Debug.Log("Setting-1B verification passed: display options, persistence contract, centered 16:9 viewport, and both Scene layouts.");
        }

        public static void SetupAndVerifyBatch()
        {
            string frontendGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Setting1BSetup.Setup();
            Week13Setting1BSetup.Setup();
            Assert(frontendGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath) &&
                   gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Setting-1B setup changed a Scene GUID.");
            Verify();
            Week13Hud1Verification.Verify();
            Debug.Log("Setting-1B batch verification passed: repeated setup, stable Scene GUIDs, centered Scene layouts, and HUD-1 regression.");
        }

        private static void ValidateSupportedResolutions()
        {
            var expected = new[] { (1280, 720), (1920, 1080), (2560, 1440) };
            Assert(LocalSettings.SupportedResolutions.SequenceEqual(expected),
                "Setting-1B must expose exactly 1280x720, 1920x1080, and 2560x1440.");
            Assert(expected.All(size => LocalSettings.IsValidResolution(size.Item1, size.Item2)),
                "A supported Setting-1B resolution was rejected.");
            Assert(!LocalSettings.IsValidResolution(1600, 900) && !LocalSettings.IsValidResolution(1024, 768),
                "Unlisted resolutions must not be accepted.");

            int originalWidth = LocalSettings.ResolutionWidth;
            int originalHeight = LocalSettings.ResolutionHeight;
            bool originalFullscreen = LocalSettings.Fullscreen;
            try
            {
                LocalSettings.SetResolution(1280, 720);
                LocalSettings.Fullscreen = false;
                LocalSettings.Load();
                Assert(LocalSettings.ResolutionWidth == 1280 && LocalSettings.ResolutionHeight == 720 &&
                       !LocalSettings.Fullscreen && LocalSettings.GetResolutionIndex() == 0,
                    "Window mode and resolution must survive LocalSettings reload.");

                bool rejected = false;
                try { LocalSettings.SetResolution(1600, 900); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Assert(rejected, "SetResolution must reject a resolution outside the supported list.");
            }
            finally
            {
                LocalSettings.SetResolution(originalWidth, originalHeight);
                LocalSettings.Fullscreen = originalFullscreen;
                LocalSettings.ApplyDisplay();
            }
        }

        private static void ValidateViewportMath()
        {
            foreach ((int width, int height) in LocalSettings.SupportedResolutions)
                AssertRect(DisplayAspectController.CalculateViewport(width, height), new Rect(0f, 0f, 1f, 1f),
                    $"{width}x{height} must use the whole viewport.");

            Rect wide = DisplayAspectController.CalculateViewport(2560, 1080);
            Assert(Mathf.Approximately(wide.height, 1f) && wide.width < 1f && Mathf.Approximately(wide.x * 2f + wide.width, 1f),
                "A wide window must center the 16:9 viewport with side margins.");
            Rect tall = DisplayAspectController.CalculateViewport(1600, 1200);
            Assert(Mathf.Approximately(tall.width, 1f) && tall.height < 1f && Mathf.Approximately(tall.y * 2f + tall.height, 1f),
                "A tall window must center the 16:9 viewport with top and bottom margins.");
        }

        private static void ValidateScene(string scenePath, string cameraName, string canvasName)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(candidate => candidate.name == cameraName);
            Assert(camera.GetComponents<DisplayAspectController>().Length == 1,
                $"{scenePath} requires exactly one display aspect controller on {cameraName}.");

            Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == canvasName);
            RectTransform frame = canvas.transform.Find("ReferenceFrame") as RectTransform;
            Assert(frame != null && frame.GetComponents<FrontendLayout>().Length == 1,
                $"{scenePath} requires exactly one centered 16:9 ReferenceFrame layout.");
            Assert(frame.sizeDelta == FrontendLayout.ReferenceSize,
                $"{scenePath} ReferenceFrame must remain 1920x1080.");

            if (scenePath == Week13FrontendSetup.ScenePath)
                ValidateResolutionDropdown(scene);
        }

        private static void ValidateResolutionDropdown(Scene scene)
        {
            FrontendSettingsView settings = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FrontendSettingsView>(true)).Single();
            TMPro.TMP_Dropdown dropdown = settings.ResolutionDropdown;
            RectTransform content = dropdown.template.Find("Viewport/Content") as RectTransform;
            RectTransform item = content != null ? content.Find("Item") as RectTransform : null;
            Assert(content != null && item != null && dropdown.itemText != null,
                "Resolution dropdown requires its content, item, and item label template.");
            Assert(dropdown.template.GetComponents<CanvasGroup>().Length == 1,
                "Resolution dropdown template requires one CanvasGroup for TMP list fading.");
            Assert(Mathf.Approximately(content.sizeDelta.x, 0f) && Mathf.Approximately(item.sizeDelta.x, 0f),
                "Stretched dropdown content and items must not add width that moves labels outside the viewport mask.");
            Assert(Mathf.Approximately(content.sizeDelta.y, 40f) && Mathf.Approximately(item.sizeDelta.y, 40f) &&
                   dropdown.itemText.fontSize >= 20f && dropdown.itemText.color.a > 0.99f,
                "Resolution dropdown options require visible 20px labels in 40px rows.");
        }

        private static void AssertRect(Rect actual, Rect expected, string message)
        {
            Assert(Mathf.Approximately(actual.x, expected.x) && Mathf.Approximately(actual.y, expected.y) &&
                   Mathf.Approximately(actual.width, expected.width) && Mathf.Approximately(actual.height, expected.height), message);
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    [InitializeOnLoad]
    public static class Week13Setting1BPlayVerification
    {
        private const string PendingKey = "Week13Setting1BPlayVerification.Pending";
        private static int originalWidth;
        private static int originalHeight;
        private static bool originalFullscreen;
        private static double started;
        private static int phase;
        private static int settingsActivationFrame = -1;

        static Week13Setting1BPlayVerification() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            Week13Setting1BSetup.Setup();
            originalWidth = LocalSettings.ResolutionWidth;
            originalHeight = LocalSettings.ResolutionHeight;
            originalFullscreen = LocalSettings.Fullscreen;
            phase = 0;
            settingsActivationFrame = -1;
            started = EditorApplication.timeSinceStartup;
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                if (EditorApplication.timeSinceStartup - started > 120)
                    Finish(1, "Setting-1B Play Mode verification timed out.");
                return;
            }
            if (Time.frameCount < 8) return;

            try
            {
                if (phase == 0) VerifyFrontendAndLoadGame();
                else VerifyGame();
            }
            catch (Exception exception)
            {
                Finish(1, exception.ToString());
            }
        }

        private static void VerifyFrontendAndLoadGame()
        {
            FrontendSettingsView settings = UnityEngine.Object.FindFirstObjectByType<FrontendSettingsView>(FindObjectsInactive.Include);
            DisplayAspectController aspect = UnityEngine.Object.FindFirstObjectByType<DisplayAspectController>();
            if (settings == null || aspect == null) return;
            if (settingsActivationFrame < 0)
            {
                for (Transform current = settings.transform; current != null; current = current.parent)
                    current.gameObject.SetActive(true);
                settingsActivationFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount <= settingsActivationFrame) return;
            settings.ResolutionDropdown.Show();
            Canvas.ForceUpdateCanvases();
            GameObject list = GameObject.Find("Dropdown List");
            Week13Setting1BVerification.Assert(list != null, "Resolution dropdown did not create its runtime option list.");
            HashSet<string> visibleOptions = list.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy && label.canvasRenderer.GetAlpha() > 0f && label.rectTransform.rect.width > 0f)
                .Select(label => label.text).ToHashSet(StringComparer.Ordinal);
            Week13Setting1BVerification.Assert(LocalSettings.SupportedResolutions.All(size =>
                    visibleOptions.Contains($"{size.width} x {size.height}")),
                "The opened resolution dropdown did not render all three option labels.");
            settings.ResolutionDropdown.Hide();
            settings.ResolutionDropdown.value = 0;
            settings.FullscreenToggle.isOn = false;
            Week13Setting1BVerification.Assert(LocalSettings.ResolutionWidth == 1280 &&
                                               LocalSettings.ResolutionHeight == 720 && !LocalSettings.Fullscreen,
                "Frontend display controls did not immediately apply their values to LocalSettings.");
            LocalSettings.Load();
            Week13Setting1BVerification.Assert(LocalSettings.ResolutionWidth == 1280 &&
                                               LocalSettings.ResolutionHeight == 720 && !LocalSettings.Fullscreen,
                "Frontend display selections did not persist across reload.");
            phase = 1;
            SceneManager.LoadScene("SampleScene");
        }

        private static void VerifyGame()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene") return;
            DisplayAspectController aspect = UnityEngine.Object.FindFirstObjectByType<DisplayAspectController>();
            FrontendLayout layout = UnityEngine.Object.FindFirstObjectByType<FrontendLayout>();
            if (aspect == null || layout == null) return;
            Week13Setting1BVerification.Assert(aspect.TargetCamera.name == "Main Camera",
                "Game Scene did not retain the display aspect controller.");
            Week13Setting1BVerification.Assert(((RectTransform)layout.transform).sizeDelta == FrontendLayout.ReferenceSize,
                "Game HUD did not retain the 1920x1080 centered content frame.");
            Finish(0, "Setting-1B Play Mode verification passed: UI selection, saved window state, Scene transition, and centered Game HUD.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            LocalSettings.SetResolution(originalWidth, originalHeight);
            LocalSettings.Fullscreen = originalFullscreen;
            LocalSettings.ApplyDisplay();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }
}
