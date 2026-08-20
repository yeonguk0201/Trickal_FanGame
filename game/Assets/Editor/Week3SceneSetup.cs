using System.Collections.Generic;
using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week3SceneSetup
    {
        private const string RootName = "Week3 Rooms";
        private const string EnemyPrefabPath = "Assets/Prefabs/TestEnemy.prefab";

        [MenuItem("Trickal Fan Game/Setup Week 3 Demo Scene")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Open the game scene before running the Week 3 setup.");
                return;
            }

            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning($"{RootName} already exists. Setup was not run again.");
                return;
            }

            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            TestEnemy enemySource = Object.FindFirstObjectByType<TestEnemy>();
            Camera mainCamera = Camera.main;
            if (player == null || enemySource == null || mainCamera == null)
            {
                Debug.LogError("Week 3 setup needs a Player, TestEnemy, and Main Camera in the scene.");
                return;
            }

            GameObject enemyPrefab = CreateEnemyPrefab(enemySource.gameObject);
            if (enemyPrefab == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 3 Demo Scene");

            GameObject root = CreateObject(RootName, null);
            RunProgress runProgress = CreateObject("Run Progress", root.transform).AddComponent<RunProgress>();

            Sprite sharedSprite = player.GetComponent<SpriteRenderer>()?.sprite;
            DisableLegacyPrototypeObjects(enemySource.gameObject);
            BuildArena(root.transform, sharedSprite);

            player.transform.position = new Vector3(-4f, 0f, 0f);
            mainCamera.transform.position = new Vector3(-4f, 0f, -10f);
            CameraFollow cameraFollow = Undo.AddComponent<CameraFollow>(mainCamera.gameObject);
            cameraFollow.SetTarget(player.transform);

            CreateRoom(root.transform, "Room 1", Vector2.zero, 1, runProgress, enemyPrefab,
                new[] { new Vector2(-0.5f, 1.5f), new Vector2(2.5f, -1.5f) },
                new[] { new Vector2(6f, 0f) }, sharedSprite);

            CreateRoom(root.transform, "Room 2", new Vector2(12f, 0f), 2, runProgress, enemyPrefab,
                new[] { new Vector2(10f, -1.5f), new Vector2(13f, 1.5f), new Vector2(15.5f, -1f) },
                new[] { new Vector2(18f, 0f) }, sharedSprite);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Week 3 demo scene setup complete: two connected rooms are ready.", root);
        }

        private static GameObject CreateEnemyPrefab(GameObject enemySource)
        {
            Directory.CreateDirectory("Assets/Prefabs");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            prefab = PrefabUtility.SaveAsPrefabAsset(enemySource, EnemyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Failed to create {EnemyPrefabPath}.");
            }

            return prefab;
        }

        private static void DisableLegacyPrototypeObjects(GameObject enemySource)
        {
            Undo.RecordObject(enemySource, "Disable enemy prototype");
            enemySource.SetActive(false);

            GameObject oldWall = GameObject.Find("Wall");
            if (oldWall != null)
            {
                Undo.RecordObject(oldWall, "Disable old wall");
                oldWall.SetActive(false);
            }
        }

        private static void BuildArena(Transform parent, Sprite sprite)
        {
            CreateWall(parent, "Left Wall", new Vector2(-6f, 0f), new Vector2(0.5f, 8f), sprite);
            CreateWall(parent, "Room 1 Top", new Vector2(0f, 4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Room 1 Bottom", new Vector2(0f, -4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Room 1 Exit Upper", new Vector2(6f, 2.6f), new Vector2(0.5f, 2.8f), sprite);
            CreateWall(parent, "Room 1 Exit Lower", new Vector2(6f, -2.6f), new Vector2(0.5f, 2.8f), sprite);

            CreateWall(parent, "Room 2 Top", new Vector2(12f, 4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Room 2 Bottom", new Vector2(12f, -4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Room 2 Entry Upper", new Vector2(6f, 2.6f), new Vector2(0.5f, 2.8f), sprite);
            CreateWall(parent, "Room 2 Entry Lower", new Vector2(6f, -2.6f), new Vector2(0.5f, 2.8f), sprite);
            CreateWall(parent, "Right Wall Upper", new Vector2(18f, 2.6f), new Vector2(0.5f, 2.8f), sprite);
            CreateWall(parent, "Right Wall Lower", new Vector2(18f, -2.6f), new Vector2(0.5f, 2.8f), sprite);
        }

        private static void CreateRoom(
            Transform parent,
            string roomName,
            Vector2 center,
            int roomNumber,
            RunProgress runProgress,
            GameObject enemyPrefab,
            IReadOnlyList<Vector2> spawnPositions,
            IReadOnlyList<Vector2> doorPositions,
            Sprite sprite)
        {
            GameObject roomObject = CreateObject(roomName, parent);
            roomObject.transform.position = center;
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(roomObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(11.5f, 7.5f);

            Transform[] spawnPoints = new Transform[spawnPositions.Count];
            for (int index = 0; index < spawnPositions.Count; index++)
            {
                GameObject spawnPoint = CreateObject($"Spawn {index + 1}", roomObject.transform);
                spawnPoint.transform.position = spawnPositions[index];
                spawnPoints[index] = spawnPoint.transform;
            }

            DoorController[] doors = new DoorController[doorPositions.Count];
            for (int index = 0; index < doorPositions.Count; index++)
            {
                doors[index] = CreateDoor(roomObject.transform, $"Door {index + 1}", doorPositions[index], sprite);
            }

            RoomController room = Undo.AddComponent<RoomController>(roomObject);
            room.Configure(1, roomNumber, runProgress, enemyPrefab, spawnPoints, doors);
        }

        private static DoorController CreateDoor(Transform parent, string objectName, Vector2 position, Sprite sprite)
        {
            GameObject door = CreateObject(objectName, parent);
            door.layer = LayerMask.NameToLayer("Environment");
            door.transform.position = position;
            AddVisual(door, sprite, new Vector2(0.5f, 2.4f), new Color(0.2f, 0.75f, 0.3f));
            Undo.AddComponent<BoxCollider2D>(door);
            return Undo.AddComponent<DoorController>(door);
        }

        private static void CreateWall(Transform parent, string objectName, Vector2 position, Vector2 size, Sprite sprite)
        {
            GameObject wall = CreateObject(objectName, parent);
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.position = position;
            AddVisual(wall, sprite, size, new Color(0.35f, 0.38f, 0.45f));
            Undo.AddComponent<BoxCollider2D>(wall);
        }

        private static void AddVisual(GameObject target, Sprite sprite, Vector2 size, Color color)
        {
            SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(target);
            renderer.sprite = sprite;
            renderer.color = color;
            target.transform.localScale = new Vector3(size.x, size.y, 1f);
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
    }
}
