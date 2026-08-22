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
    public static class Week5FloorSetup
    {
        private const string RootName = "Week5 Three Floor Skeleton";
        private const string EnemyPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";

        [MenuItem("Trickal Fan Game/Setup Week 5 Three Floor Skeleton")]
        public static void Setup()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning("Week 5 three-floor skeleton already exists.");
                return;
            }

            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = UnityEngine.Object.FindFirstObjectByType<RunProgress>();
            RunSession session = UnityEngine.Object.FindFirstObjectByType<RunSession>();
            BossController floorOneBoss = GameObject.Find("Boss")?.GetComponent<BossController>();
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            GameObject pickupObject = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            ItemPickup pickupPrefab = pickupObject != null ? pickupObject.GetComponent<ItemPickup>() : null;
            ItemDefinition[] itemPool = LoadItemPool();

            if (player == null || progress == null || session == null || floorOneBoss == null ||
                enemyPrefab == null || bossPrefab == null || pickupPrefab == null ||
                Array.Exists(itemPool, definition => definition == null))
            {
                Debug.LogError("Run the Week 5 item and reward-drop setups first; the three-floor setup reuses their assets.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 5 Three Floor Skeleton");

            GameObject root = CreateObject(RootName, null);
            DisableItemTestPickups();
            Sprite sprite = player.GetComponent<SpriteRenderer>()?.sprite;
            FloorBuild floorTwo = BuildFloor(
                root.transform, 2, -12f, progress, enemyPrefab, bossPrefab, pickupPrefab, itemPool, sprite, false);
            FloorBuild floorThree = BuildFloor(
                root.transform, 3, -24f, progress, enemyPrefab, bossPrefab, pickupPrefab, itemPool, sprite, true);

            CreateFloorExit(root.transform, "Floor 1 Exit", floorOneBoss, floorTwo.StartPoint, 2, progress,
                new Vector2(28.5f, 0f), sprite);
            CreateFloorExit(root.transform, "Floor 2 Exit", floorTwo.Boss, floorThree.StartPoint, 3, progress,
                new Vector2(28.5f, -12f), sprite);

            session.Configure(player.GetComponent<Health>(), progress, floorThree.Boss);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "Week 5 three-floor skeleton ready: floor 1 and 2 bosses drop items and unlock exits; " +
                "the floor 3 boss clears the Run without an item drop.",
                root);
        }

        private static FloorBuild BuildFloor(
            Transform parent,
            int floorNumber,
            float yOffset,
            RunProgress progress,
            GameObject enemyPrefab,
            GameObject bossPrefab,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            Sprite sprite,
            bool isFinalFloor)
        {
            GameObject floorRoot = CreateObject($"Floor {floorNumber}", parent);
            BuildArena(floorRoot.transform, yOffset, sprite, floorNumber);

            Transform startPoint = CreateObject("Start Point", floorRoot.transform).transform;
            startPoint.position = new Vector2(-4f, yOffset);

            RoomController roomOne = CreateCombatRoom(
                floorRoot.transform,
                $"Floor {floorNumber} Room 1",
                new Vector2(0f, yOffset),
                floorNumber,
                1,
                progress,
                enemyPrefab,
                new[] { new Vector2(-0.5f, yOffset + 1.5f), new Vector2(2.5f, yOffset - 1.5f) },
                new Vector2(6f, yOffset),
                sprite);

            RoomController roomTwo = CreateCombatRoom(
                floorRoot.transform,
                $"Floor {floorNumber} Room 2",
                new Vector2(12f, yOffset),
                floorNumber,
                2,
                progress,
                enemyPrefab,
                new[]
                {
                    new Vector2(10f, yOffset - 1.5f),
                    new Vector2(13f, yOffset + 1.5f),
                    new Vector2(15.5f, yOffset - 1f)
                },
                new Vector2(18f, yOffset),
                sprite);

            CreateRewardRoom(
                floorRoot.transform, floorNumber, yOffset, progress, roomTwo, pickupPrefab, itemPool, sprite);

            GameObject bossObject = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, floorRoot.transform);
            bossObject.name = $"Floor {floorNumber} Boss";
            bossObject.transform.position = new Vector2(24f, yOffset);
            BossController boss = bossObject.GetComponent<BossController>();
            Health bossHealth = bossObject.GetComponent<Health>();
            ConfigureBossDrop(boss, pickupPrefab, itemPool, isFinalFloor);

            GameObject bossRoomObject = CreateObject($"Floor {floorNumber} Boss Room", floorRoot.transform);
            bossRoomObject.transform.position = new Vector2(24f, yOffset);
            BoxCollider2D bossTrigger = Undo.AddComponent<BoxCollider2D>(bossRoomObject);
            bossTrigger.isTrigger = true;
            bossTrigger.size = new Vector2(11.5f, 7.5f);
            RoomController bossRoom = Undo.AddComponent<RoomController>(bossRoomObject);
            bossRoom.Configure(floorNumber, 4, progress, null, null, null);
            bossRoom.ConfigurePreplacedEnemies(new[] { bossHealth });

            return new FloorBuild(startPoint, boss);
        }

        private static RoomController CreateCombatRoom(
            Transform parent,
            string roomName,
            Vector2 center,
            int floorNumber,
            int roomNumber,
            RunProgress progress,
            GameObject enemyPrefab,
            Vector2[] spawnPositions,
            Vector2 doorPosition,
            Sprite sprite)
        {
            GameObject roomObject = CreateObject(roomName, parent);
            roomObject.transform.position = center;
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(roomObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(11.5f, 7.5f);

            Transform[] spawnPoints = new Transform[spawnPositions.Length];
            for (int index = 0; index < spawnPositions.Length; index++)
            {
                Transform spawn = CreateObject($"Spawn {index + 1}", roomObject.transform).transform;
                spawn.position = spawnPositions[index];
                spawnPoints[index] = spawn;
            }

            DoorController door = CreateDoor(roomObject.transform, "Exit Door", doorPosition, sprite);
            RoomController room = Undo.AddComponent<RoomController>(roomObject);
            room.Configure(floorNumber, roomNumber, progress, enemyPrefab, spawnPoints, new[] { door });
            return room;
        }

        private static void CreateRewardRoom(
            Transform parent,
            int floorNumber,
            float yOffset,
            RunProgress progress,
            RoomController prerequisiteRoom,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            Sprite sprite)
        {
            GameObject roomObject = CreateObject($"Floor {floorNumber} Reward Room", parent);
            roomObject.transform.position = new Vector2(15.5f, yOffset);
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(roomObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, 6f);

            GameObject markerObject = CreateObject("Reward Marker", roomObject.transform);
            SpriteRenderer marker = Undo.AddComponent<SpriteRenderer>(markerObject);
            marker.sprite = sprite;
            marker.color = new Color(1f, 0.82f, 0.2f, 0.45f);
            markerObject.transform.localScale = new Vector3(0.35f, 2.5f, 1f);

            Transform dropPoint = CreateObject("Drop Point", roomObject.transform).transform;
            dropPoint.position = new Vector2(17f, yOffset);
            ItemDropSource source = Undo.AddComponent<ItemDropSource>(roomObject);
            source.Configure(pickupPrefab, itemPool, dropPoint);
            RewardRoom rewardRoom = Undo.AddComponent<RewardRoom>(roomObject);
            rewardRoom.Configure(floorNumber, 3, progress, source, prerequisiteRoom);
        }

        private static void ConfigureBossDrop(
            BossController boss,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            bool isFinalBoss)
        {
            ItemDropSource source = Undo.AddComponent<ItemDropSource>(boss.gameObject);
            source.Configure(pickupPrefab, itemPool, boss.transform);
            BossItemDrop drop = Undo.AddComponent<BossItemDrop>(boss.gameObject);
            drop.Configure(isFinalBoss, source);
        }

        private static void CreateFloorExit(
            Transform parent,
            string exitName,
            BossController unlockBoss,
            Transform destination,
            int destinationFloor,
            RunProgress progress,
            Vector2 position,
            Sprite sprite)
        {
            GameObject exitObject = CreateObject(exitName, parent);
            exitObject.transform.position = position;
            BoxCollider2D collider = Undo.AddComponent<BoxCollider2D>(exitObject);
            collider.isTrigger = true;
            collider.size = new Vector2(1.2f, 3f);
            SpriteRenderer indicator = Undo.AddComponent<SpriteRenderer>(exitObject);
            indicator.sprite = sprite;
            exitObject.transform.localScale = new Vector3(0.6f, 2f, 1f);
            FloorExit floorExit = Undo.AddComponent<FloorExit>(exitObject);
            floorExit.Configure(unlockBoss, destination, destinationFloor, progress, indicator);
        }

        private static void BuildArena(Transform parent, float y, Sprite sprite, int floorNumber)
        {
            Color color = floorNumber == 2
                ? new Color(0.25f, 0.35f, 0.48f)
                : new Color(0.38f, 0.28f, 0.48f);
            CreateWall(parent, "Left Wall", new Vector2(-6f, y), new Vector2(0.5f, 8f), sprite, color);
            for (int roomIndex = 0; roomIndex < 3; roomIndex++)
            {
                float centerX = roomIndex * 12f;
                CreateWall(parent, $"Room {roomIndex + 1} Top", new Vector2(centerX, y + 4f),
                    new Vector2(12f, 0.5f), sprite, color);
                CreateWall(parent, $"Room {roomIndex + 1} Bottom", new Vector2(centerX, y - 4f),
                    new Vector2(12f, 0.5f), sprite, color);
            }

            foreach (float x in new[] { 6f, 18f })
            {
                CreateWall(parent, $"Divider {x} Upper", new Vector2(x, y + 2.6f),
                    new Vector2(0.5f, 2.8f), sprite, color);
                CreateWall(parent, $"Divider {x} Lower", new Vector2(x, y - 2.6f),
                    new Vector2(0.5f, 2.8f), sprite, color);
            }

            CreateWall(parent, "Right Wall", new Vector2(30f, y), new Vector2(0.5f, 8f), sprite, color);
        }

        private static DoorController CreateDoor(Transform parent, string name, Vector2 position, Sprite sprite)
        {
            GameObject door = CreateObject(name, parent);
            door.layer = LayerMask.NameToLayer("Environment");
            door.transform.position = position;
            AddVisual(door, sprite, new Vector2(0.5f, 2.4f), new Color(0.2f, 0.75f, 0.3f));
            Undo.AddComponent<BoxCollider2D>(door);
            return Undo.AddComponent<DoorController>(door);
        }

        private static void CreateWall(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject wall = CreateObject(name, parent);
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

        private static ItemDefinition[] LoadItemPool()
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-01.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-02.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-03.asset")
            };
        }

        private static void DisableItemTestPickups()
        {
            GameObject testPickups = GameObject.Find("Week5 Item Test Pickups");
            if (testPickups == null)
            {
                return;
            }

            Undo.RecordObject(testPickups, "Disable Week 5 item test pickups");
            testPickups.SetActive(false);
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject created = new(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }

            return created;
        }

        private readonly struct FloorBuild
        {
            public FloorBuild(Transform startPoint, BossController boss)
            {
                StartPoint = startPoint;
                Boss = boss;
            }

            public Transform StartPoint { get; }
            public BossController Boss { get; }
        }
    }
}
