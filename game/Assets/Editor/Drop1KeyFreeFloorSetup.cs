using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Drop-1: floor 1 needs no key (FloorGenerator.KeyFreeFloor). The rule lives in the runtime generator and chest
    // spawner; this setup only raises the Game Scene Room content version, since the same seed now builds an unlocked
    // floor-1 shop and normal chests where golden ones used to be. Re-running keeps the version.
    public static class Drop1KeyFreeFloorSetup
    {
        public const int RoomContentVersion = 15;

        [MenuItem("Trickal Fan Game/Week 28/Setup Drop-1 Key-Free Floor 1")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Drop-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator[] generators = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FloorGenerator>(true)).ToArray();
            if (generators.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one FloorGenerator; found {generators.Length}.");
            FloorGenerator generator = generators[0];
            if (generator.RoomContentVersion >= RoomContentVersion)
            {
                Debug.Log($"Drop-1 setup: Room content version already {generator.RoomContentVersion}.");
                return;
            }

            Undo.RecordObject(generator, "Raise Drop-1 Room content version");
            generator.ConfigureTemplates(RoomContentVersion, generator.RoomTemplates.ToArray());
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Drop-1 setup.");
            Debug.Log($"Drop-1 setup complete: Room content version {generator.RoomContentVersion}.");
        }
    }
}
