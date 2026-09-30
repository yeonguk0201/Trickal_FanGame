using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7FloorEncounterSetup
    {
        private const string GraphRootName = "Week7 Fixed Room Graph";
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";
        private const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase E-6 Floor Encounters")]
        public static void Setup()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            RoomGraphController graph = FindSceneObject<RoomGraphController>(activeScene, GraphRootName);
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = graph != null ? graph.GetComponentInChildren<RunProgress>(true) : null;
            RunSession session = FindSceneObject<RunSession>(activeScene);
            GameObject chaserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            GameObject rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            GameObject chargingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            GameObject pickupObject = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            ItemPickup pickupPrefab = pickupObject != null ? pickupObject.GetComponent<ItemPickup>() : null;
            ItemDefinition[] itemPool = LoadItemPool();

            if (graph == null || graph.Nodes.Count != 9 || player == null || progress == null || session == null ||
                chaserPrefab == null || rangedPrefab == null || chargingPrefab == null || bossPrefab == null ||
                pickupPrefab == null || Array.Exists(itemPool, item => item == null))
            {
                Debug.LogError(
                    "Phase E-6 requires the completed E-5 graph, all three normal enemy prefabs, TestBoss, " +
                    "RunSession, and the Week 5 item assets.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase E-6 Floor Encounters");
            BossController finalBoss = null;

            foreach (RoomNode node in graph.Nodes)
            {
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller == null)
                {
                    Debug.LogError($"{node.RoomId} is missing its RoomController.", node);
                    Undo.RevertAllDownToGroup(undoGroup);
                    return;
                }

                Undo.RecordObject(controller, "Configure floor encounter");
                if (node.RoomNumber < 3)
                {
                    GameObject[] composition = ResolveComposition(
                        node.FloorNumber,
                        node.RoomNumber,
                        chaserPrefab,
                        rangedPrefab,
                        chargingPrefab);
                    if (composition.Length != controller.SpawnPoints.Count)
                    {
                        Debug.LogError(
                            $"{node.RoomId} has {controller.SpawnPoints.Count} spawn points but needs " +
                            $"{composition.Length} configured enemies.",
                            controller);
                        Undo.RevertAllDownToGroup(undoGroup);
                        return;
                    }

                    RemoveBosses(node.ContentRoot);
                    controller.ConfigureEnemyPrefabs(composition);
                    controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
                    EditorUtility.SetDirty(controller);
                    continue;
                }

                controller.ConfigureEnemyPrefabs(Array.Empty<GameObject>());
                BossController boss = CreateBoss(
                    node,
                    controller,
                    bossPrefab,
                    pickupPrefab,
                    itemPool,
                    node.FloorNumber == 3);
                if (boss == null)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return;
                }

                if (node.FloorNumber == 3)
                {
                    finalBoss = boss;
                }
            }

            if (finalBoss == null)
            {
                Debug.LogError("Phase E-6 could not create the floor 3 final boss.");
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }

            Undo.SetTransformParent(session.transform, null, "Move RunSession out of disabled legacy root");
            if (!session.gameObject.activeSelf)
            {
                Undo.RecordObject(session.gameObject, "Activate RunSession");
                session.gameObject.SetActive(true);
            }

            Undo.RecordObject(session, "Assign floor 3 final boss");
            session.Configure(player.GetComponent<Health>(), progress, finalBoss);
            EditorUtility.SetDirty(session);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Selection.activeGameObject = finalBoss.gameObject;
            Debug.Log(
                "Phase E-6 floor encounters ready: two normal encounters and one boss per floor; " +
                "the floor 3 boss is connected to RunSession as the final boss.",
                graph);
        }

        private static BossController CreateBoss(
            RoomNode node,
            RoomController controller,
            GameObject bossPrefab,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            bool isFinalBoss)
        {
            RemoveBosses(node.ContentRoot);
            GameObject bossObject = PrefabUtility.InstantiatePrefab(bossPrefab, node.ContentRoot.transform) as GameObject;
            if (bossObject == null)
            {
                Debug.LogError($"Failed to instantiate the boss for {node.RoomId}.", node);
                return null;
            }

            Undo.RegisterCreatedObjectUndo(bossObject, $"Create Floor {node.FloorNumber} Boss");
            bossObject.name = isFinalBoss ? "Floor 3 Final Boss" : $"Floor {node.FloorNumber} Boss";
            int spawnIndex = Mathf.Min(1, controller.SpawnPoints.Count - 1);
            if (spawnIndex < 0 || controller.SpawnPoints[spawnIndex] == null)
            {
                Debug.LogError($"{node.RoomId} has no valid boss spawn point.", controller);
                return null;
            }

            bossObject.transform.position = controller.SpawnPoints[spawnIndex].position;
            bossObject.transform.localScale = Vector3.one * (isFinalBoss ? 1.25f : 1.1f);
            SpriteRenderer renderer = bossObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = node.FloorNumber switch
                {
                    1 => new Color(0.9f, 0.35f, 0.3f),
                    2 => new Color(0.65f, 0.35f, 0.9f),
                    _ => new Color(0.95f, 0.2f, 0.65f),
                };
            }

            BossController boss = bossObject.GetComponent<BossController>();
            Health health = bossObject.GetComponent<Health>();
            if (boss == null || health == null)
            {
                Debug.LogError($"{BossPrefabPath} is missing BossController or Health.", bossObject);
                return null;
            }

            ItemDropSource source = bossObject.GetComponent<ItemDropSource>();
            if (source == null)
            {
                source = Undo.AddComponent<ItemDropSource>(bossObject);
            }

            source.Configure(pickupPrefab, itemPool, bossObject.transform, node.ContentRoot.transform);
            BossItemDrop drop = bossObject.GetComponent<BossItemDrop>();
            if (drop == null)
            {
                drop = Undo.AddComponent<BossItemDrop>(bossObject);
            }

            drop.Configure(isFinalBoss, source);
            controller.ConfigurePreplacedEnemies(new[] { health });
            EditorUtility.SetDirty(controller);
            return boss;
        }

        private static void RemoveBosses(GameObject contentRoot)
        {
            foreach (BossController existing in contentRoot.GetComponentsInChildren<BossController>(true))
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }

        private static GameObject[] ResolveComposition(
            int floor,
            int room,
            GameObject chaser,
            GameObject ranged,
            GameObject charging)
        {
            return (floor, room) switch
            {
                (1, 1) => new[] { chaser },
                (1, 2) => new[] { charging, ranged },
                (2, 1) => new[] { ranged },
                (2, 2) => new[] { chaser, charging },
                (3, 1) => new[] { charging },
                (3, 2) => new[] { chaser, ranged },
                _ => Array.Empty<GameObject>(),
            };
        }

        private static T FindSceneObject<T>(Scene scene, string requiredName = null) where T : Component
        {
            T match = null;
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                if (candidate == null || candidate.gameObject.scene != scene ||
                    (requiredName != null && candidate.gameObject.name != requiredName))
                {
                    continue;
                }

                if (match != null)
                {
                    Debug.LogError($"Phase E-6 found multiple scene objects of type {typeof(T).Name}.");
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static ItemDefinition[] LoadItemPool()
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-01.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-02.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-03.asset"),
            };
        }
    }
}
