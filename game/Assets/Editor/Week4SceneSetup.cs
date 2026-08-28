using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week4SceneSetup
    {
        private const string RootName = "Week4 Boss Room";
        private const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";

        [MenuItem("Trickal Fan Game/Setup Week 4 Vertical Slice")]
        public static void Setup()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning("Week 4 setup already exists.");
                return;
            }

            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = Object.FindFirstObjectByType<RunProgress>();
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TestEnemy.prefab");
            if (player == null || progress == null || enemyPrefab == null)
            {
                Debug.LogError("Run the Week 3 setup first; it provides Player, RunProgress, and TestEnemy.prefab.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 4 Vertical Slice");

            GameObject root = CreateObject(RootName, null);
            DisableOldEndWalls();
            Sprite sprite = player.GetComponent<SpriteRenderer>()?.sprite;
            BuildBossArena(root.transform, sprite);

            GameObject bossPrefab = CreateBossPrefab(enemyPrefab);
            GameObject bossObject = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, root.transform);
            bossObject.name = "Boss";
            bossObject.transform.position = new Vector2(24f, 0f);
            Health bossHealth = bossObject.GetComponent<Health>();
            BossController boss = bossObject.GetComponent<BossController>();

            GameObject roomObject = CreateObject("Boss Room", root.transform);
            roomObject.transform.position = new Vector2(24f, 0f);
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(roomObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(11.5f, 7.5f);
            RoomController room = Undo.AddComponent<RoomController>(roomObject);
            room.Configure(1, 3, progress, null, null, null);
            room.ConfigurePreplacedEnemies(new[] { bossHealth });

            RunSession session = CreateObject("Run Session", root.transform).AddComponent<RunSession>();
            session.Configure(player.GetComponent<Health>(), progress, boss);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("Week 4 setup complete. Reach the third room and defeat the boss to save a clear Run.", root);
        }

        private static GameObject CreateBossPrefab(GameObject enemyPrefab)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (prefab != null) return prefab;
            Directory.CreateDirectory("Assets/Prefabs");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            instance.name = "TestBoss";
            Object.DestroyImmediate(instance.GetComponent<EnemyChase>());
            Object.DestroyImmediate(instance.GetComponent<ContactDamage>());
            instance.AddComponent<BossController>();
            SerializedObject health = new(instance.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 25f;
            health.ApplyModifiedPropertiesWithoutUndo();
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void DisableOldEndWalls()
        {
            foreach (string wallName in new[] { "Right Wall Upper", "Right Wall Lower" })
            {
                GameObject wall = GameObject.Find(wallName);
                if (wall != null) wall.SetActive(false);
            }
        }

        private static void BuildBossArena(Transform parent, Sprite sprite)
        {
            CreateWall(parent, "Boss Room Top", new Vector2(24f, 4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Boss Room Bottom", new Vector2(24f, -4f), new Vector2(12f, 0.5f), sprite);
            CreateWall(parent, "Boss Room Right", new Vector2(30f, 0f), new Vector2(0.5f, 8f), sprite);
        }

        private static void CreateWall(Transform parent, string objectName, Vector2 position, Vector2 size, Sprite sprite)
        {
            GameObject wall = CreateObject(objectName, parent);
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.position = position;
            SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(wall);
            renderer.sprite = sprite;
            renderer.color = new Color(0.42f, 0.2f, 0.25f);
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            Undo.AddComponent<BoxCollider2D>(wall);
        }

        private static GameObject CreateObject(string objectName, Transform parent)
        {
            GameObject created = new(objectName);
            Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
            if (parent != null) created.transform.SetParent(parent);
            return created;
        }
    }
}
