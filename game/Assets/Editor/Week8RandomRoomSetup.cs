using System;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week8RandomRoomSetup
    {
        public const string GeneratorObjectName = "Phase F Floor Generator";
        public const string DefinitionFolder = "Assets/Rooms/Definitions";
        public const int FixedVerificationSeed = 20260828;

        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";
        private const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase F-1 Random Room Definitions")]
        public static void Setup()
        {
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            GameObject ranged = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            GameObject charging = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (chaser == null || ranged == null || charging == null || boss == null)
            {
                Debug.LogError("Phase F-1 requires all three verified normal enemies and the verified boss prefab.");
                return;
            }

            EnsureDefinitionFolders();
            RoomDefinition[] definitions =
            {
                CreateOrUpdate("normal-chaser", RoomType.Normal, new[] { chaser }),
                CreateOrUpdate("normal-ranged", RoomType.Normal, new[] { ranged }),
                CreateOrUpdate("normal-charging", RoomType.Normal, new[] { charging }),
                CreateOrUpdate("reward-mixed", RoomType.Reward, new[] { chaser, ranged }),
                CreateOrUpdate("boss-standard", RoomType.Boss, new[] { boss }),
            };

            Array.Sort(definitions, (left, right) =>
                string.CompareOrdinal(left.RoomDefinitionId, right.RoomDefinitionId));

            GameObject generatorObject = GameObject.Find(GeneratorObjectName);
            if (generatorObject == null)
            {
                generatorObject = new GameObject(GeneratorObjectName);
                Undo.RegisterCreatedObjectUndo(generatorObject, "Create Phase F Floor Generator");
            }

            FloorGenerator generator = generatorObject.GetComponent<FloorGenerator>();
            if (generator == null)
            {
                generator = Undo.AddComponent<FloorGenerator>(generatorObject);
            }

            Undo.RecordObject(generator, "Configure Phase F Floor Generator");
            generator.Configure(FixedVerificationSeed, 3, 3, definitions);
            EditorUtility.SetDirty(generator);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = generatorObject;
            Debug.Log(
                "Phase F-1 room definitions ready: verified normal, reward, and boss encounter units " +
                "are configured for deterministic three-floor graph generation.",
                generator);
        }

        [MenuItem("Trickal Fan Game/Setup Phase F-2 Generated Room Graph Binding")]
        public static void SetupGraphBinding()
        {
            Setup();
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject generatorObject = GameObject.Find(GeneratorObjectName);
            FloorGenerator generator = generatorObject != null
                ? generatorObject.GetComponent<FloorGenerator>()
                : null;
            RoomGraphController graph = FindSceneGraph(activeScene);
            if (generator == null || graph == null)
            {
                Debug.LogError(
                    "Phase F-2 requires the F-1 generator and completed Phase E-7 room graph.");
                return;
            }

            if (!graph.TryValidateConfiguration(out string graphError))
            {
                Debug.LogError($"Phase F-2 found an invalid Phase E graph. {graphError}", graph);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase F-2 Generated Room Graph Binding");
            RoomGraphAssembler assembler = generatorObject.GetComponent<RoomGraphAssembler>();
            if (assembler == null)
            {
                assembler = Undo.AddComponent<RoomGraphAssembler>(generatorObject);
            }

            RecordGeneratedGraphObjects(graph, assembler);
            assembler.Configure(generator, graph);
            if (!assembler.TryApplyGeneratedGraph(out string error))
            {
                Debug.LogError($"Phase F-2 could not bind the generated graph. {error}", assembler);
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }

            MarkGeneratedGraphObjectsDirty(graph, assembler);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Selection.activeGameObject = graph.gameObject;
            Debug.Log(
                "Phase F-2 generated room graph binding ready: generated definitions and connections are " +
                "applied to the nine verified room instances and their RoomGraphController doorways.",
                assembler);
        }

        private static void EnsureDefinitionFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Rooms"))
            {
                AssetDatabase.CreateFolder("Assets", "Rooms");
            }

            if (!AssetDatabase.IsValidFolder(DefinitionFolder))
            {
                AssetDatabase.CreateFolder("Assets/Rooms", "Definitions");
            }
        }

        private static RoomDefinition CreateOrUpdate(
            string definitionId,
            RoomType roomType,
            GameObject[] encounterPrefabs)
        {
            string assetPath = $"{DefinitionFolder}/{definitionId}.asset";
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<RoomDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.Configure(definitionId, roomType, encounterPrefabs);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static RoomGraphController FindSceneGraph(Scene scene)
        {
            RoomGraphController match = null;
            foreach (RoomGraphController candidate in Resources.FindObjectsOfTypeAll<RoomGraphController>())
            {
                if (candidate == null || candidate.gameObject.scene != scene ||
                    candidate.gameObject.name != "Week7 Fixed Room Graph")
                {
                    continue;
                }

                if (match != null)
                {
                    Debug.LogError("Phase F-2 found multiple Week7 Fixed Room Graph objects.");
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static void RecordGeneratedGraphObjects(
            RoomGraphController graph,
            RoomGraphAssembler assembler)
        {
            Undo.RecordObject(assembler, "Configure generated graph assembler");
            foreach (RoomNode node in graph.Nodes)
            {
                Undo.RecordObject(node, "Apply generated room definition");
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller != null)
                {
                    Undo.RecordObject(controller, "Apply generated encounter");
                }

                TrickalFanGame.Item.RewardRoom reward =
                    node.ContentRoot.GetComponentInChildren<TrickalFanGame.Item.RewardRoom>(true);
                if (reward != null)
                {
                    Undo.RecordObject(reward.gameObject, "Apply generated reward room state");
                }

                foreach (RoomDoorway doorway in node.Doorways)
                {
                    if (doorway != null)
                    {
                        Undo.RecordObject(doorway, "Apply generated doorway connection");
                    }
                }
            }
        }

        private static void MarkGeneratedGraphObjectsDirty(
            RoomGraphController graph,
            RoomGraphAssembler assembler)
        {
            EditorUtility.SetDirty(assembler);
            foreach (RoomNode node in graph.Nodes)
            {
                EditorUtility.SetDirty(node);
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller != null)
                {
                    EditorUtility.SetDirty(controller);
                }

                TrickalFanGame.Item.RewardRoom reward =
                    node.ContentRoot.GetComponentInChildren<TrickalFanGame.Item.RewardRoom>(true);
                if (reward != null)
                {
                    EditorUtility.SetDirty(reward.gameObject);
                }

                foreach (RoomDoorway doorway in node.Doorways)
                {
                    if (doorway != null)
                    {
                        EditorUtility.SetDirty(doorway);
                    }
                }
            }
        }
    }
}
