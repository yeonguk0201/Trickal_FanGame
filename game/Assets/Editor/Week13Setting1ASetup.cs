using System;
using System.IO;
using TrickalFanGame.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1ASetup
    {
        public const string HomeBgmPath = "Assets/Audio/BGM/BGM_Home.ogg";
        public const string CombatBgmPath = "Assets/Audio/BGM/BGM_Combat.ogg";
        public const string UiClickPath = "Assets/Audio/SFX/SFX_UI_Click.wav";
        public const string AudioRootName = "Game Audio";

        [MenuItem("Trickal Fan Game/Week 13/Setup Setting-1A Audio")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Setting-1A setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AudioClip homeBgm = RequireClip(HomeBgmPath);
            AudioClip combatBgm = RequireClip(CombatBgmPath);
            AudioClip uiClick = RequireClip(UiClickPath);
            ConfigureImporter(HomeBgmPath, AudioClipLoadType.Streaming);
            ConfigureImporter(CombatBgmPath, AudioClipLoadType.Streaming);
            ConfigureImporter(UiClickPath, AudioClipLoadType.DecompressOnLoad);

            ConfigureScene(Week13FrontendSetup.ScenePath, homeBgm, combatBgm, uiClick);
            ConfigureScene(Week13FrontendSetup.GameScenePath, homeBgm, combatBgm, uiClick);
            AssetDatabase.SaveAssets();
            Debug.Log("Setting-1A audio setup completed for Frontend and Game scenes.");
        }

        private static void ConfigureScene(string scenePath, AudioClip homeBgm, AudioClip combatBgm, AudioClip uiClick)
        {
            if (!File.Exists(scenePath)) throw new InvalidOperationException($"Scene is missing: {scenePath}");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == AudioRootName) root = candidate;
            if (root == null)
            {
                root = new GameObject(AudioRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create game audio root");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            GameAudioController controller = root.GetComponent<GameAudioController>();
            if (controller == null) controller = Undo.AddComponent<GameAudioController>(root);
            AudioSource[] sources = root.GetComponents<AudioSource>();
            while (sources.Length < 2)
            {
                Undo.AddComponent<AudioSource>(root);
                sources = root.GetComponents<AudioSource>();
            }

            controller.Configure(homeBgm, combatBgm, uiClick, sources[0], sources[1]);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(sources[0]);
            EditorUtility.SetDirty(sources[1]);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static AudioClip RequireClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Audio clip is missing or failed to import: {path}");
            return clip;
        }

        private static void ConfigureImporter(string path, AudioClipLoadType loadType)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                throw new InvalidOperationException($"Audio importer is unavailable: {path}");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                ? AudioCompressionFormat.PCM
                : AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = loadType != AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }
    }
}
