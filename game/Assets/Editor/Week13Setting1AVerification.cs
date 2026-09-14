using System;
using System.Linq;
using TrickalFanGame.Audio;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1AVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Setting-1A Audio")]
        public static void Verify()
        {
            AudioClip home = RequireClip(Week13Setting1ASetup.HomeBgmPath);
            AudioClip combat = RequireClip(Week13Setting1ASetup.CombatBgmPath);
            AudioClip click = RequireClip(Week13Setting1ASetup.UiClickPath);
            ValidateImporter(Week13Setting1ASetup.HomeBgmPath, AudioClipLoadType.Streaming);
            ValidateImporter(Week13Setting1ASetup.CombatBgmPath, AudioClipLoadType.Streaming);
            ValidateImporter(Week13Setting1ASetup.UiClickPath, AudioClipLoadType.DecompressOnLoad);
            ValidateScene(Week13FrontendSetup.ScenePath, home, combat, click);
            ValidateScene(Week13FrontendSetup.GameScenePath, home, combat, click);
            ValidateVolumeApplication(home, combat, click);
            Debug.Log("Setting-1A verification passed: clips, scene persistence, BGM routing, and volume application.");
        }

        public static void SetupAndVerifyBatch()
        {
            string frontendGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Setting1ASetup.Setup();
            Week13Setting1ASetup.Setup();
            ValidateNoDuplicates(Week13FrontendSetup.ScenePath);
            ValidateNoDuplicates(Week13FrontendSetup.GameScenePath);
            Assert(frontendGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath) &&
                   gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Setting-1A setup changed a Scene GUID.");
            Verify();
            Debug.Log("Setting-1A batch verification passed: setup twice without duplicates and stable Scene GUIDs.");
        }

        private static void ValidateScene(string scenePath, AudioClip home, AudioClip combat, AudioClip click)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameAudioController[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameAudioController>(true)).ToArray();
            Assert(controllers.Length == 1, $"{scenePath} must contain exactly one GameAudioController.");
            GameAudioController controller = controllers[0];
            Assert(controller.gameObject.name == Week13Setting1ASetup.AudioRootName,
                $"{scenePath} audio controller must use the stable root name.");
            Assert(controller.HomeBgm == home && controller.CombatBgm == combat && controller.UiClick == click,
                $"{scenePath} audio clips are not assigned correctly.");
            Assert(controller.BgmSource != null && controller.SfxSource != null &&
                   controller.BgmSource != controller.SfxSource,
                $"{scenePath} requires distinct BGM and SFX sources.");
            Assert(controller.BgmSource.loop && !controller.BgmSource.playOnAwake &&
                   !controller.SfxSource.loop && !controller.SfxSource.playOnAwake,
                $"{scenePath} audio source playback flags are invalid.");
            Assert(controller.ResolveBgmForScene(GameAudioController.FrontendSceneName) == home &&
                   controller.ResolveBgmForScene(GameAudioController.GameSceneName) == combat,
                $"{scenePath} BGM scene routing is invalid.");
        }

        private static void ValidateNoDuplicates(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameAudioController[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameAudioController>(true)).ToArray();
            Assert(controllers.Length == 1 && controllers[0].GetComponents<AudioSource>().Length == 2,
                $"Repeated Setting-1A setup created duplicate audio components in {scenePath}.");
        }

        private static void ValidateVolumeApplication(AudioClip home, AudioClip combat, AudioClip click)
        {
            float originalMaster = LocalSettings.MasterVolume;
            float originalBgm = LocalSettings.BgmVolume;
            float originalSfx = LocalSettings.SfxVolume;
            GameObject root = new("Setting-1A Volume Verification");
            try
            {
                AudioSource bgm = root.AddComponent<AudioSource>();
                AudioSource sfx = root.AddComponent<AudioSource>();
                GameAudioController controller = root.AddComponent<GameAudioController>();
                controller.Configure(home, combat, click, bgm, sfx);
                LocalSettings.MasterVolume = 0.5f;
                LocalSettings.BgmVolume = 0.8f;
                LocalSettings.SfxVolume = 0.3f;
                controller.ApplyVolumes();
                Assert(Mathf.Approximately(bgm.volume, 0.4f), "Master and BGM volumes must multiply immediately.");
                Assert(Mathf.Approximately(sfx.volume, 0.15f), "Master and SFX volumes must multiply immediately.");
                LocalSettings.Load();
                controller.ApplyVolumes();
                Assert(Mathf.Approximately(bgm.volume, 0.4f) && Mathf.Approximately(sfx.volume, 0.15f),
                    "Saved audio volumes must survive LocalSettings reload.");
            }
            finally
            {
                LocalSettings.MasterVolume = originalMaster;
                LocalSettings.BgmVolume = originalBgm;
                LocalSettings.SfxVolume = originalSfx;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AudioClip RequireClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Missing audio clip: {path}");
            return clip;
        }

        private static void ValidateImporter(string path, AudioClipLoadType expectedLoadType)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Assert(importer != null, $"Missing audio importer: {path}");
            Assert(importer.defaultSampleSettings.loadType == expectedLoadType,
                $"Unexpected load type for {path}.");
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    [InitializeOnLoad]
    public static class Week13Setting1APlayVerification
    {
        private const string PendingKey = "Week13Setting1APlayVerification.Pending";
        private static float originalMaster;
        private static float originalBgm;
        private static float originalSfx;
        private static double started;
        private static int phase;
        private static int controllerInstanceId;

        static Week13Setting1APlayVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            Week13Setting1ASetup.Setup();
            originalMaster = LocalSettings.MasterVolume;
            originalBgm = LocalSettings.BgmVolume;
            originalSfx = LocalSettings.SfxVolume;
            phase = 0;
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
                    Finish(1, "Setting-1A Play Mode verification timed out.");
                return;
            }
            if (Time.frameCount < 8) return;

            try
            {
                switch (phase)
                {
                    case 0: VerifyFrontendAndLoadGame(); break;
                    case 1: VerifyGameAndReturnFrontend(); break;
                    case 2: VerifyReturnAndPersistence(); break;
                }
            }
            catch (Exception exception)
            {
                Finish(1, exception.ToString());
            }
        }

        private static void VerifyFrontendAndLoadGame()
        {
            GameAudioController controller = GameAudioController.Instance;
            FrontendTitleView title = UnityEngine.Object.FindFirstObjectByType<FrontendTitleView>();
            if (controller == null || title == null) return;
            Week13Setting1AVerification.Assert(controller.CurrentBgm == controller.HomeBgm,
                "FrontendScene did not select the home BGM.");
            Week13Setting1AVerification.Assert(controller.IsButtonBound(title.StartButton),
                "The active title button was not bound to UI click audio.");
            int clickCount = controller.UiClickPlayCount;
            title.StartButton.onClick.Invoke();
            Week13Setting1AVerification.Assert(controller.UiClickPlayCount == clickCount + 1,
                "Invoking a UI button did not play the click cue.");

            LocalSettings.MasterVolume = 0.5f;
            LocalSettings.BgmVolume = 0.8f;
            LocalSettings.SfxVolume = 0.3f;
            Week13Setting1AVerification.Assert(Mathf.Approximately(controller.BgmSource.volume, 0.4f) &&
                                               Mathf.Approximately(controller.SfxSource.volume, 0.15f),
                "Audio sliders did not apply immediately to runtime sources.");
            controllerInstanceId = controller.GetInstanceID();
            phase++;
            SceneManager.LoadScene(GameAudioController.GameSceneName);
        }

        private static void VerifyGameAndReturnFrontend()
        {
            if (SceneManager.GetActiveScene().name != GameAudioController.GameSceneName) return;
            GameAudioController controller = GameAudioController.Instance;
            if (controller == null) return;
            Week13Setting1AVerification.Assert(controller.GetInstanceID() == controllerInstanceId &&
                                               controller.CurrentBgm == controller.CombatBgm,
                "Game Scene transition did not preserve the controller and select combat BGM.");
            phase++;
            SceneManager.LoadScene(GameAudioController.FrontendSceneName);
        }

        private static void VerifyReturnAndPersistence()
        {
            if (SceneManager.GetActiveScene().name != GameAudioController.FrontendSceneName) return;
            GameAudioController controller = GameAudioController.Instance;
            if (controller == null) return;
            LocalSettings.Load();
            Week13Setting1AVerification.Assert(controller.GetInstanceID() == controllerInstanceId &&
                                               controller.CurrentBgm == controller.HomeBgm,
                "Returning to Frontend did not preserve the controller and restore home BGM.");
            Week13Setting1AVerification.Assert(Mathf.Approximately(LocalSettings.MasterVolume, 0.5f) &&
                                               Mathf.Approximately(LocalSettings.BgmVolume, 0.8f) &&
                                               Mathf.Approximately(LocalSettings.SfxVolume, 0.3f) &&
                                               Mathf.Approximately(controller.BgmSource.volume, 0.4f) &&
                                               Mathf.Approximately(controller.SfxSource.volume, 0.15f),
                "Saved audio settings did not persist after a Scene round trip and reload.");
            Finish(0, "Setting-1A Play Mode verification passed: click SFX, immediate volumes, persistent controller, BGM switching, and saved values.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            LocalSettings.MasterVolume = originalMaster;
            LocalSettings.BgmVolume = originalBgm;
            LocalSettings.SfxVolume = originalSfx;
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }
}
