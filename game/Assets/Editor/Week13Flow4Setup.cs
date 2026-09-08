using System;
using System.Linq;
using TrickalFanGame.Character;
using TrickalFanGame.Meta;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Flow4Setup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup Flow-4 Run Launch")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Flow-4 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene gameScene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RunSession session = FindSingle<RunSession>(gameScene, "RunSession");
            PlayerProgressClient progress = FindSingle<PlayerProgressClient>(gameScene, "PlayerProgressClient");
            CharacterSelectionUI legacySelection = FindSingle<CharacterSelectionUI>(gameScene, "legacy CharacterSelectionUI");
            GameRunBootstrap[] existing = gameScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameRunBootstrap>(true)).ToArray();
            if (existing.Length > 1)
                throw new InvalidOperationException("Game Scene contains duplicate GameRunBootstrap components.");

            GameRunBootstrap bootstrap = existing.Length == 1
                ? existing[0]
                : Undo.AddComponent<GameRunBootstrap>(session.gameObject);
            Undo.RecordObject(bootstrap, "Configure Flow-4 Run bootstrap");
            bootstrap.Configure(session, progress, legacySelection);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(gameScene);
            if (!EditorSceneManager.SaveScene(gameScene))
                throw new InvalidOperationException("Game Scene save failed during Flow-4 setup.");

            Week13FrontendSetup.Setup();
            Debug.Log("Week 13 Flow-4 setup complete. Frontend launch identity now initializes the existing Game Run once.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }
    }
}
