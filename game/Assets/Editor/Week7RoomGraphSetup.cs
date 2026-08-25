using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7RoomGraphSetup
    {
        private const string RootName = "Week7 Fixed Room Graph";
        private const string EnemyPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";
        private static readonly Vector2[] RoomCenters =
        {
            new(0f, -40f),
            new(16f, -40f),
            new(32f, -40f),
        };

        [MenuItem("Trickal Fan Game/Setup Week 7 Fixed Room Graph")]
        public static void Setup()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning($"{RootName} already exists. Setup was not run again.");
                return;
            }

            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = UnityEngine.Object.FindFirstObjectByType<RunProgress>();
            Camera mainCamera = Camera.main;
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            GameObject pickupObject = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            ItemPickup pickupPrefab = pickupObject != null ? pickupObject.GetComponent<ItemPickup>() : null;
            ItemDefinition[] itemPool = LoadItemPool();
            if (player == null || progress == null || mainCamera == null || enemyPrefab == null ||
                pickupPrefab == null || Array.Exists(itemPool, item => item == null))
            {
                Debug.LogError(
                    "Run the existing Week 5 setup first; Phase B reuses Player, RunProgress, Main Camera, and TestEnemy.prefab.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 7 Fixed Room Graph");

            GameObject root = CreateObject(RootName, null);
            RoomGraphController graph = Undo.AddComponent<RoomGraphController>(root);
            Sprite sprite = player.GetComponent<SpriteRenderer>()?.sprite;
            RoomBuild[] rooms = new RoomBuild[RoomCenters.Length];
            for (int index = 0; index < rooms.Length; index++)
            {
                rooms[index] = CreateRoom(
                    root.transform,
                    graph,
                    index + 1,
                    RoomCenters[index],
                    progress,
                    enemyPrefab,
                    sprite,
                    index + 1,
                    pickupPrefab,
                    itemPool);
            }

            for (int index = 0; index < rooms.Length; index++)
            {
                List<RoomDoorway> connections = new();
                if (index > 0)
                {
                    connections.Add(CreateDoorway(
                        rooms[index], rooms[index - 1], rooms[index - 1].RightEntry, graph, false));
                }

                if (index < rooms.Length - 1)
                {
                    connections.Add(CreateDoorway(
                        rooms[index], rooms[index + 1], rooms[index + 1].LeftEntry, graph, true));
                }

                rooms[index].Node.SetDoorways(connections.ToArray());
                rooms[index].Controller.Configure(
                    1,
                    index + 1,
                    progress,
                    enemyPrefab,
                    rooms[index].SpawnPoints,
                    rooms[index].Doors);
            }

            CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
            RoomCameraController roomCamera = mainCamera.GetComponent<RoomCameraController>();
            if (roomCamera == null)
            {
                roomCamera = Undo.AddComponent<RoomCameraController>(mainCamera.gameObject);
            }

            roomCamera.Configure(cameraFollow);
            RoomNode[] nodes = Array.ConvertAll(rooms, room => room.Node);
            graph.Configure(nodes, nodes[0], player, roomCamera, progress);

            player.transform.position = rooms[0].LeftEntry.position;
            Undo.SetTransformParent(progress.transform, root.transform, "Move RunProgress to fixed room graph");
            DisableLegacyGameplayRoots(root);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "Week 7 fixed room graph ready. Green doors remain solid portal barriers; enter their inner trigger " +
                "while in normal state to move the Player and camera. Targeted lower-grade shots may cross the barrier, " +
                "but every launched projectile is cleared during an actual room transition.",
                root);
        }

        private static RoomBuild CreateRoom(
            Transform parent,
            RoomGraphController graph,
            int roomNumber,
            Vector2 center,
            RunProgress progress,
            GameObject enemyPrefab,
            Sprite sprite,
            int enemyCount,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool)
        {
            GameObject nodeObject = CreateObject($"Room Node {roomNumber}", parent);
            RoomNode node = Undo.AddComponent<RoomNode>(nodeObject);
            GameObject content = CreateObject($"Room {roomNumber} Content", nodeObject.transform);
            BuildArena(content.transform, center, sprite, roomNumber);

            Transform cameraAnchor = CreateObject("Camera Anchor", nodeObject.transform).transform;
            cameraAnchor.position = center;
            Transform leftEntry = CreateObject("Left Entry", nodeObject.transform).transform;
            leftEntry.position = center + Vector2.left * 4.2f;
            Transform rightEntry = CreateObject("Right Entry", nodeObject.transform).transform;
            rightEntry.position = center + Vector2.right * 4.2f;

            GameObject encounterObject = CreateObject("Encounter", content.transform);
            encounterObject.transform.position = center;
            BoxCollider2D encounterTrigger = Undo.AddComponent<BoxCollider2D>(encounterObject);
            encounterTrigger.isTrigger = true;
            encounterTrigger.size = new Vector2(10f, 7f);
            RoomController controller = Undo.AddComponent<RoomController>(encounterObject);

            if (roomNumber == 2)
            {
                CreateReward(content.transform, center, progress, controller, pickupPrefab, itemPool, sprite);
            }

            Transform[] spawnPoints = new Transform[enemyCount];
            for (int index = 0; index < enemyCount; index++)
            {
                Transform spawn = CreateObject($"Spawn {index + 1}", encounterObject.transform).transform;
                float x = (index - (enemyCount - 1) * 0.5f) * 1.8f;
                spawn.position = center + new Vector2(x, index % 2 == 0 ? 1.4f : -1.4f);
                spawnPoints[index] = spawn;
            }

            DoorController leftDoor = CreateBlockingDoor(content.transform, "Left Door", center + Vector2.left * 5.75f, sprite);
            DoorController rightDoor = CreateBlockingDoor(content.transform, "Right Door", center + Vector2.right * 5.75f, sprite);
            if (roomNumber == 1)
            {
                CreateWall(content.transform, "Left Boundary Seal", center + Vector2.left * 5.75f,
                    new Vector2(0.5f, 2.4f), sprite, new Color(0.28f, 0.35f, 0.48f));
            }

            if (roomNumber == RoomCenters.Length)
            {
                CreateWall(content.transform, "Right Boundary Seal", center + Vector2.right * 5.75f,
                    new Vector2(0.5f, 2.4f), sprite, new Color(0.48f, 0.28f, 0.4f));
            }
            node.Configure(
                $"floor-01-room-{roomNumber:00}",
                1,
                roomNumber,
                content,
                cameraAnchor,
                leftEntry,
                Array.Empty<RoomDoorway>());

            return new RoomBuild(
                node,
                controller,
                leftEntry,
                rightEntry,
                spawnPoints,
                new[] { leftDoor, rightDoor });
        }

        private static void CreateReward(
            Transform content,
            Vector2 center,
            RunProgress progress,
            RoomController prerequisiteRoom,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            Sprite sprite)
        {
            GameObject rewardObject = CreateObject("Room 2 Reward", content);
            rewardObject.transform.position = center;
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(rewardObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(8f, 6f);
            GameObject markerObject = CreateObject("Reward Marker", rewardObject.transform);
            markerObject.transform.position = center + Vector2.up * 2.8f;
            AddVisual(markerObject, sprite, new Vector2(0.7f, 0.7f), new Color(1f, 0.82f, 0.2f, 0.7f));
            Transform dropPoint = CreateObject("Drop Point", rewardObject.transform).transform;
            dropPoint.position = center;
            ItemDropSource source = Undo.AddComponent<ItemDropSource>(rewardObject);
            source.Configure(pickupPrefab, itemPool, dropPoint, content);
            RewardRoom reward = Undo.AddComponent<RewardRoom>(rewardObject);
            reward.Configure(1, 2, progress, source, prerequisiteRoom);
        }

        private static RoomDoorway CreateDoorway(
            RoomBuild source,
            RoomBuild destination,
            Transform destinationEntry,
            RoomGraphController graph,
            bool isRightDoor)
        {
            GameObject doorwayObject = CreateObject(
                isRightDoor ? "Right Transition" : "Left Transition",
                source.Node.ContentRoot.transform);
            doorwayObject.transform.position = source.Node.CameraAnchor.position +
                                               (isRightDoor ? Vector3.right : Vector3.left) * 5.15f;
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(doorwayObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.8f, 2.5f);
            RoomDoorway doorway = Undo.AddComponent<RoomDoorway>(doorwayObject);
            doorway.Configure(graph, source.Node, destination.Node, destinationEntry, source.Controller);
            return doorway;
        }

        private static DoorController CreateBlockingDoor(
            Transform parent,
            string objectName,
            Vector2 position,
            Sprite sprite)
        {
            GameObject doorObject = CreateObject(objectName, parent);
            doorObject.layer = LayerMask.NameToLayer("Environment");
            doorObject.transform.position = position;
            AddVisual(doorObject, sprite, new Vector2(0.45f, 2.4f), new Color(0.2f, 0.75f, 0.3f));
            Undo.AddComponent<BoxCollider2D>(doorObject);
            DoorController door = Undo.AddComponent<DoorController>(doorObject);
            door.ConfigurePortalBarrier(true);
            return door;
        }

        private static void BuildArena(Transform parent, Vector2 center, Sprite sprite, int roomNumber)
        {
            Color color = Color.Lerp(
                new Color(0.28f, 0.35f, 0.48f),
                new Color(0.48f, 0.28f, 0.4f),
                (roomNumber - 1) / 2f);
            CreateWall(parent, "Top Wall", center + Vector2.up * 4f, new Vector2(12f, 0.5f), sprite, color);
            CreateWall(parent, "Bottom Wall", center + Vector2.down * 4f, new Vector2(12f, 0.5f), sprite, color);
            foreach (float x in new[] { -5.75f, 5.75f })
            {
                CreateWall(parent, x < 0 ? "Left Upper Wall" : "Right Upper Wall",
                    center + new Vector2(x, 2.6f), new Vector2(0.5f, 2.8f), sprite, color);
                CreateWall(parent, x < 0 ? "Left Lower Wall" : "Right Lower Wall",
                    center + new Vector2(x, -2.6f), new Vector2(0.5f, 2.8f), sprite, color);
            }
        }

        private static void CreateWall(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject wall = CreateObject(objectName, parent);
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.position = position;
            AddVisual(wall, sprite, size, color);
            Undo.AddComponent<BoxCollider2D>(wall);
        }

        private static void AddVisual(GameObject target, Sprite sprite, Vector2 size, Color color)
        {
            SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(target);
            renderer.sprite = sprite;
            renderer.color = color;
            target.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static void DisableLegacyGameplayRoots(GameObject phaseBRoot)
        {
            foreach (string rootName in new[]
                     {
                         "Week3 Rooms",
                         "Week4 Boss Room",
                         "Week5 Three Floor Skeleton",
                         "Week5 Item Test Pickups",
                         "Week5 Reward Drop Test",
                     })
            {
                GameObject legacyRoot = GameObject.Find(rootName);
                if (legacyRoot == null || legacyRoot == phaseBRoot)
                {
                    continue;
                }

                Undo.RecordObject(legacyRoot, "Disable legacy continuous room layout");
                legacyRoot.SetActive(false);
            }
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

        private static GameObject CreateObject(string objectName, Transform parent)
        {
            GameObject created = new(objectName);
            Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }

            return created;
        }

        private readonly struct RoomBuild
        {
            public RoomBuild(
                RoomNode node,
                RoomController controller,
                Transform leftEntry,
                Transform rightEntry,
                Transform[] spawnPoints,
                DoorController[] doors)
            {
                Node = node;
                Controller = controller;
                LeftEntry = leftEntry;
                RightEntry = rightEntry;
                SpawnPoints = spawnPoints;
                Doors = doors;
            }

            public RoomNode Node { get; }
            public RoomController Controller { get; }
            public Transform LeftEntry { get; }
            public Transform RightEntry { get; }
            public Transform[] SpawnPoints { get; }
            public DoorController[] Doors { get; }
        }
    }
}
