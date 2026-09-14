using System;
using System.Linq;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Flow5Setup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup Flow-5 Run Result")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Flow-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene gameScene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RunSession[] sessions = FindAll<RunSession>(gameScene);
            if (sessions.Length != 1)
                throw new InvalidOperationException($"Game Scene requires one RunSession; found {sessions.Length}.");
            GameRunResultTransition[] transitions = FindAll<GameRunResultTransition>(gameScene);
            if (transitions.Length > 1)
                throw new InvalidOperationException("Game Scene contains duplicate result transitions.");

            GameRunResultTransition transition = transitions.Length == 1
                ? transitions[0]
                : Undo.AddComponent<GameRunResultTransition>(sessions[0].gameObject);
            Undo.RecordObject(transition, "Configure Flow-5 result transition");
            transition.Configure(Week13FrontendSetup.ScenePath);
            Undo.RecordObject(sessions[0], "Configure Flow-5 RunSession");
            sessions[0].ConfigureResultTransition(transition);
            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(sessions[0]);
            EditorSceneManager.MarkSceneDirty(gameScene);
            if (!EditorSceneManager.SaveScene(gameScene))
                throw new InvalidOperationException("Game Scene save failed during Flow-5 setup.");

            Week13FrontendSetup.Setup();
            Debug.Log("Week 13 Flow-5 setup complete. Run endings now open the Frontend full-screen result flow.");
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }
    }
}
