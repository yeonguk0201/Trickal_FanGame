using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss4Setup
    {
        [MenuItem("Trickal Fan Game/Week 15/Setup Boss-4 Full Run Binding")]
        public static void Setup()
        {
            Week15Boss3Setup.Setup();
            if (!string.Equals(SceneManager.GetActiveScene().path, Week13FrontendSetup.GameScenePath,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }

            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            GameObject[] bosses =
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss0Setup.BossPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss3Setup.BossPrefabPath),
            };
            if (assembler == null || bosses.Any(boss => boss == null))
                throw new InvalidOperationException("Boss-4 could not resolve the Game Scene or three boss prefabs.");

            Undo.RecordObject(assembler, "Bind complete floor boss roster");
            assembler.ConfigureFloorBossPrefabs(bosses);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Could not save the Boss-4 floor boss roster binding.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Boss-4 setup complete: three floor bosses, boss rooms, rewards, and floor transitions are bound.");
        }
    }
}
