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
    public static class BossTestRoomSetup
    {
        public const string ScenePath = "Assets/Scenes/BossTestScene.unity";

        public static readonly (string Name, string PrefabPath)[] BossDefinitions =
        {
            ("Boss 1 - Buseureogi", Week15Boss0Setup.BossPrefabPath),
            ("Boss 2 - Saemaeum Vault", Week15Boss2Setup.BossPrefabPath),
            ("Boss 3 - Crayon Hero", Week15Boss3Setup.BossPrefabPath),
        };

        [MenuItem("Trickal Fan Game/Debug/Open Boss Test Room", priority = 3)]
        public static void Open()
        {
            if (EditorApplication.isPlaying) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Repair(scene);
            }
            Debug.Log("Boss Test Room: press Play to test bosses, then use Boss Selection and item/spell +1 controls.");
        }

        private static void Create()
        {
            GameObject defaultBoss = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss0Setup.BossPrefabPath);
            if (defaultBoss == null) throw new InvalidOperationException("Run Setup Boss-0 first.");
            if (!AssetDatabase.CopyAsset(ItemTestRoomSetup.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not create the Boss test scene.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ItemTestRoomController controller = Find<ItemTestRoomController>(scene);
            PlayerMovement player = Find<PlayerMovement>(scene);

            // Replace controller with BossTestRoomController
            GameObject controllerObject = controller.gameObject;
            UnityEngine.Object.DestroyImmediate(controller);
            BossTestRoomController bossController = controllerObject.AddComponent<BossTestRoomController>();

            // Configure boss controller with all boss prefabs
            GameObject[] bossPrefabs = BossDefinitions
                .Select(def => AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath))
                .Where(prefab => prefab != null)
                .ToArray();
            string[] bossNames = BossDefinitions
                .Where(def => AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath) != null)
                .Select(def => def.Name)
                .ToArray();

            bossController.Configure(
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<Health>(),
                player.GetComponent<PlayerStats>(),
                BuildItemCatalog(),
                bossPrefabs,
                bossNames,
                new Vector2(16f, 12f));

            controllerObject.name = "Boss Test Room";
            player.transform.position = new Vector3(-3f, -2f, 0f);
            Transform arena = controllerObject.transform.Find("Arena");
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
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Boss test scene.");
        }

        private static void Repair(Scene scene)
        {
            BossTestRoomController controller = Find<BossTestRoomController>(scene);
            PlayerMovement player = Find<PlayerMovement>(scene);
            GameObject[] bossPrefabs = BossDefinitions
                .Select(def => AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath))
                .Where(prefab => prefab != null)
                .ToArray();
            string[] bossNames = BossDefinitions
                .Where(def => AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath) != null)
                .Select(def => def.Name)
                .ToArray();

            controller.Configure(
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<Health>(),
                player.GetComponent<PlayerStats>(),
                BuildItemCatalog(),
                bossPrefabs,
                bossNames,
                new Vector2(16f, 12f));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not synchronize the Boss test scene.");
        }

        private static ItemTestRoomController.ItemLoadoutEntry[] BuildItemCatalog() =>
            ItemTestRoomSetup.BuildDefaultLoadout()
                .Select(entry => new ItemTestRoomController.ItemLoadoutEntry(entry.Item, 0))
                .ToArray();

        [MenuItem("Trickal Fan Game/Debug/Verify Boss Test Room", priority = 4)]
        public static void Verify()
        {
            if (EditorApplication.isPlaying) return;
            Open();
            if (SceneManager.GetActiveScene().path != ScenePath) return;
            Scene scene = SceneManager.GetActiveScene();
            BossTestRoomController controller = Find<BossTestRoomController>(scene);
            if (!controller.TryValidateConfiguration(out string error))
                throw new InvalidOperationException($"Boss test scene configuration is invalid: {error}");
            if (controller.BossPrefabCount == 0)
                throw new InvalidOperationException("Boss test scene has no boss prefabs configured.");
            if (controller.ItemCatalog.Count != 17 ||
                controller.ItemCatalog.Count(entry => entry.Item.IsActive) != 16)
                throw new InvalidOperationException(
                    "Boss test scene must expose all 16 active items/spells and legacy item-06.");

            // Test spawning each boss
            for (int i = 0; i < controller.BossPrefabCount; i++)
            {
                controller.SpawnBoss(i);
                if (controller.ActiveBoss == null)
                    throw new InvalidOperationException($"Failed to spawn boss at index {i}.");
                CrayonHeroBossPatternRuntime crayon =
                    controller.ActiveBoss.GetComponent<CrayonHeroBossPatternRuntime>();
                if (crayon != null)
                {
                    controller.SetCrayonRecognitionRadiusVisible(true);
                    if (!controller.ShowCrayonRecognitionRadius || !crayon.RecognitionRadiusVisible)
                        throw new InvalidOperationException("Boss test scene could not show Crayon recognition radius.");
                    controller.SetCrayonRecognitionRadiusVisible(false);
                }
                UnityEngine.Object.DestroyImmediate(controller.ActiveBoss.gameObject);
            }

            Debug.Log($"Boss test room verification passed: {controller.BossPrefabCount} bosses and " +
                      $"{controller.ItemCatalog.Count} individually acquirable item entries are available.");
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).First();
    }
}
