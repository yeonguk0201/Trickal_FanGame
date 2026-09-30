using System;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Debugging;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Boss2TestRoomSetup
    {
        public const string ScenePath = "Assets/Scenes/Boss2TestScene.unity";

        [MenuItem("Trickal Fan Game/Debug/Open Boss-2 Test Room", priority = 3)]
        public static void Open()
        {
            if (EditorApplication.isPlaying) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("Boss-2 Test Room: press Play to fight Saemaeum Vault immediately. Use Respawn Enemies and Heal / Reset HP to retry.");
        }

        private static void Create()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Run Setup Boss-2 Saemaeum Vault first.");
            if (!AssetDatabase.CopyAsset(ItemTestRoomSetup.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not create the independent Boss-2 test scene.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ItemTestRoomController controller = Find<ItemTestRoomController>(scene);
            PlayerMovement player = Find<PlayerMovement>(scene);
            controller.Configure(player.GetComponent<PlayerInventory>(), player.GetComponent<Health>(),
                player.GetComponent<PlayerStats>(), Array.Empty<ItemTestRoomController.ItemLoadoutEntry>(),
                new[] { new ItemTestRoomController.EnemyPlacement(prefab, Vector2.zero) }, new Vector2(16f, 12f));
            controller.gameObject.name = "Boss-2 Test Room";
            player.transform.position = new Vector3(-3f, -2f, 0f);
            Transform arena = controller.transform.Find("Arena");
            foreach (Transform wall in arena)
            {
                if (wall.name == "Top Wall") wall.localPosition = new Vector3(0f, 6f, 0f);
                if (wall.name == "Bottom Wall") wall.localPosition = new Vector3(0f, -6f, 0f);
                if (wall.name == "Left Wall" || wall.name == "Right Wall")
                {
                    Vector3 scale = wall.localScale;
                    scale.y *= 12f / 9f;
                    wall.localScale = scale;
                }
            }
            Camera camera = Find<Camera>(scene);
            camera.orthographicSize = 7f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            CameraFollow follow = camera.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Boss-2 test scene.");
        }

        [MenuItem("Trickal Fan Game/Debug/Verify Boss-2 Test Room", priority = 4)]
        public static void CreateAndVerifyBatch()
        {
            if (EditorApplication.isPlaying) return;
            Open();
            if (SceneManager.GetActiveScene().path != ScenePath) return;
            string guid = AssetDatabase.AssetPathToGUID(ScenePath);
            Open();
            Scene scene = SceneManager.GetActiveScene();
            ItemTestRoomController controller = Find<ItemTestRoomController>(scene);
            if (guid != AssetDatabase.AssetPathToGUID(ScenePath) || !controller.TryValidateConfiguration(out _) ||
                controller.EnemyPlacements.Count != 1 ||
                controller.EnemyPlacements[0].EnemyPrefab.GetComponent<SaemaeumVaultBossPatternRuntime>() == null)
                throw new InvalidOperationException("Boss-2 test scene configuration is invalid.");
            controller.RespawnEnemies();
            BossController boss = controller.GetComponentInChildren<BossController>();
            if (controller.SpawnedEnemyCount != 1 || boss == null || controller.ActiveBoss != boss ||
                controller.ActiveBoss.Health == null)
                throw new InvalidOperationException("Boss-2 test scene did not spawn exactly one boss.");
            UnityEngine.Object.DestroyImmediate(boss.gameObject);
            Debug.Log("Boss-2 test room verification passed: stable scene GUID, valid player references, single Saemaeum Vault spawn.");
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).First();
    }
}
