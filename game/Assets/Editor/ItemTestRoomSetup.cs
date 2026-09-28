using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Debugging;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class ItemTestRoomSetup
    {
        public const string ScenePath = "Assets/Scenes/ItemTestScene.unity";
        public const string RootName = "Item Test Room";

        private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly Vector2 TestRoomSize = new(16f, 9f);

        [MenuItem("Trickal Fan Game/Debug/Open or Create Item Test Room", priority = 0)]
        public static void OpenOrCreate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before opening or creating the Item Test Room.");
                return;
            }

            if (File.Exists(ScenePath))
            {
                Scene existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                bool repaired = RepairCurrentTestContent(existing);
                if (repaired)
                {
                    EditorSceneManager.MarkSceneDirty(existing);
                    EditorSceneManager.SaveScene(existing, ScenePath);
                }
                ItemTestRoomController existingController = FindInScene<ItemTestRoomController>(existing);
                Selection.activeGameObject = existingController != null ? existingController.gameObject : null;
                Debug.Log(
                    existingController != null
                        ? "Item Test Room opened. Edit the controller loadout and enemy placements, then enter Play Mode." +
                          (repaired ? " Synchronized the current item catalog and required test enemies." : string.Empty)
                        : "Item Test Room opened, but its controller is missing. Run verification for details.");
                return;
            }

            if (!File.Exists(SourceScenePath) || !AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError($"Could not copy the source scene from {SourceScenePath} to {ScenePath}.");
                return;
            }

            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindInScene<PlayerMovement>(scene);
            Camera mainCamera = FindInScene<Camera>(scene, camera => camera.CompareTag("MainCamera"));
            if (player == null || mainCamera == null)
            {
                Debug.LogError("The source scene must contain PlayerMovement and a MainCamera.");
                return;
            }

            GameObject playerObject = player.gameObject;
            GameObject cameraObject = mainCamera.gameObject;
            GameObject lightObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Global Light 2D");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != playerObject && root != cameraObject && root != lightObject)
                {
                    Object.DestroyImmediate(root);
                }
            }

            playerObject.name = "Player";
            playerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.enabled = true;
            PlayerProjectileAttack attack = playerObject.GetComponent<PlayerProjectileAttack>();
            if (attack != null)
            {
                attack.enabled = true;
            }

            mainCamera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5.25f;
            CameraFollow follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            }
            follow.SetTarget(player.transform);

            GameObject rootObject = new(RootName);
            RunProgress progress = new GameObject("Run Progress").AddComponent<RunProgress>();
            progress.transform.SetParent(rootObject.transform, false);
            progress.TryInitializeRunSeed(20260901, out _);

            Health playerHealth = playerObject.GetComponent<Health>();
            PlayerStats playerStats = playerObject.GetComponent<PlayerStats>();
            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();

            ItemTestRoomController controller = rootObject.AddComponent<ItemTestRoomController>();
            controller.Configure(
                inventory,
                playerHealth,
                playerStats,
                BuildDefaultLoadout(),
                BuildDefaultEnemyPlacements(),
                TestRoomSize);
            BuildArena(rootObject.transform, player.GetComponent<SpriteRenderer>()?.sprite);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = rootObject;
            Debug.Log(
                "Item Test Room created. Configure initial item stacks and enemy placements on Item Test Room, " +
                "then enter Play Mode. This scene is intentionally excluded from the random floor generator.",
                rootObject);
        }

        [MenuItem("Trickal Fan Game/Debug/Add Week 15 Enemy Color Samples", priority = 2)]
        public static void AddWeek15EnemyColorSamples()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before changing the Item Test Room.");
                return;
            }

            OpenOrCreate();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            ItemTestRoomController controller = FindInScene<ItemTestRoomController>(scene);
            if (controller == null)
            {
                Debug.LogError("Item Test Room controller is missing. Run verification for details.");
                return;
            }

            List<ItemTestRoomController.EnemyPlacement> placements = controller.EnemyPlacements
                .Where(IsValidPlacement)
                .ToList();
            bool changed = placements.Count != controller.EnemyPlacements.Count;
            changed |= AddPlacementIfMissing(
                placements,
                "Assets/Prefabs/SansamoEnemy.prefab",
                new Vector2(-3f, -1.5f));
            changed |= AddPlacementIfMissing(
                placements,
                "Assets/Prefabs/HighBloodSugarFairy.prefab",
                new Vector2(0f, 3f));

            if (!changed)
            {
                Debug.Log("Week 15 enemy color samples are already in the Item Test Room.", controller);
                return;
            }

            PlayerInventory inventory = FindInScene<PlayerInventory>(scene);
            PlayerMovement player = FindInScene<PlayerMovement>(scene);
            controller.Configure(
                inventory,
                player != null ? player.GetComponent<Health>() : null,
                player != null ? player.GetComponent<PlayerStats>() : null,
                controller.ItemLoadout.ToArray(),
                placements.ToArray(),
                controller.RoomSize);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log(
                "Repaired the Item Test Room enemy placements and ensured the Sansamo and High Blood Sugar Fairy color samples exist.",
                controller);
        }

        private static bool RepairCurrentTestContent(Scene scene)
        {
            bool changed = RemoveRunSessions(scene);
            ItemTestRoomController controller = FindInScene<ItemTestRoomController>(scene);
            PlayerInventory inventory = FindInScene<PlayerInventory>(scene);
            if (controller == null || inventory == null)
            {
                return changed;
            }

            if (FindInScene<RunProgress>(scene) == null)
            {
                RunProgress progress = new GameObject("Run Progress").AddComponent<RunProgress>();
                progress.transform.SetParent(controller.transform, false);
                progress.TryInitializeRunSeed(20260901, out _);
                changed = true;
            }

            string[] previousItemIds = controller.ItemLoadout
                .Where(entry => entry?.Item != null)
                .Select(entry => entry.Item.ItemId)
                .ToArray();
            ItemTestRoomController.ItemLoadoutEntry[] loadout = MergeCurrentLoadout(controller.ItemLoadout);
            List<ItemTestRoomController.EnemyPlacement> placements = controller.EnemyPlacements
                .Where(IsValidPlacement)
                .ToList();
            foreach (ItemTestRoomController.EnemyPlacement required in BuildDefaultEnemyPlacements())
            {
                if (required.EnemyPrefab != null &&
                    !placements.Any(placement => placement?.EnemyPrefab == required.EnemyPrefab))
                {
                    placements.Add(required);
                    changed = true;
                }
            }

            changed |= !previousItemIds.SequenceEqual(
                loadout.Select(entry => entry.Item.ItemId),
                System.StringComparer.Ordinal);
            controller.Configure(
                inventory,
                inventory.GetComponent<Health>(),
                inventory.GetComponent<PlayerStats>(),
                loadout,
                placements.ToArray(),
                controller.RoomSize);
            return changed;
        }

        private static bool RemoveRunSessions(Scene scene)
        {
            bool changed = false;
            foreach (TrickalFanGame.Run.RunSession session in FindAllInScene<TrickalFanGame.Run.RunSession>(scene))
            {
                Object.DestroyImmediate(session.gameObject);
                changed = true;
            }

            return changed;
        }

        internal static ItemTestRoomController.ItemLoadoutEntry[] BuildDefaultLoadout()
        {
            IEnumerable<(string ItemId, int StartingStacks)> entries = PhaseGArtifactCatalog.All
                .Select(spec => (ItemId: spec.ItemId, StartingStacks: spec.ItemId == "item-01" ? 1 : 0))
                .Concat(Week16Content0Catalog.All.Select(spec =>
                    (ItemId: spec.ItemId, StartingStacks: 0)));

            return entries
                .Select(entry =>
                {
                    ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                        $"Assets/Items/{entry.ItemId}.asset");
                    return new ItemTestRoomController.ItemLoadoutEntry(item, entry.StartingStacks);
                })
                .ToArray();
        }

        private static ItemTestRoomController.ItemLoadoutEntry[] MergeCurrentLoadout(
            IReadOnlyList<ItemTestRoomController.ItemLoadoutEntry> existing)
        {
            Dictionary<string, ItemTestRoomController.ItemLoadoutEntry> current = existing
                .Where(entry => entry?.Item != null && entry.Item.IsValid)
                .GroupBy(entry => entry.Item.ItemId)
                .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);

            return BuildDefaultLoadout()
                .Select(required => current.TryGetValue(required.Item.ItemId, out var configured)
                    ? configured
                    : required)
                .ToArray();
        }

        private static ItemTestRoomController.EnemyPlacement[] BuildDefaultEnemyPlacements()
        {
            return new[]
            {
                Placement("Assets/Prefabs/TestEnemy.prefab", new Vector2(3f, 1.5f)),
                Placement("Assets/Prefabs/RangedEnemy.prefab", new Vector2(-3f, 1.5f)),
                Placement("Assets/Prefabs/ChargingEnemy.prefab", new Vector2(3f, -1.5f)),
                Placement("Assets/Prefabs/SansamoEnemy.prefab", new Vector2(-3f, -1.5f)),
                Placement("Assets/Prefabs/HighBloodSugarFairy.prefab", new Vector2(0f, 3f)),
            };
        }

        private static bool AddPlacementIfMissing(
            List<ItemTestRoomController.EnemyPlacement> placements,
            string prefabPath,
            Vector2 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not load debug enemy prefab at {prefabPath}.");
                return false;
            }

            if (placements.Any(placement => placement?.EnemyPrefab == prefab))
            {
                return false;
            }

            placements.Add(new ItemTestRoomController.EnemyPlacement(prefab, position));
            return true;
        }

        private static bool IsValidPlacement(ItemTestRoomController.EnemyPlacement placement)
        {
            return placement != null &&
                   (!placement.Enabled ||
                    (placement.EnemyPrefab != null && placement.EnemyPrefab.GetComponent<Health>() != null));
        }

        private static ItemTestRoomController.EnemyPlacement Placement(string prefabPath, Vector2 position)
        {
            return new ItemTestRoomController.EnemyPlacement(
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                position);
        }

        private static void BuildArena(Transform parent, Sprite sharedSprite)
        {
            GameObject arena = new("Arena");
            arena.transform.SetParent(parent, false);
            float halfWidth = TestRoomSize.x * 0.5f;
            float halfHeight = TestRoomSize.y * 0.5f;
            CreateWall(arena.transform, "Top Wall", new Vector2(0f, halfHeight), new Vector2(TestRoomSize.x, 0.4f), sharedSprite);
            CreateWall(arena.transform, "Bottom Wall", new Vector2(0f, -halfHeight), new Vector2(TestRoomSize.x, 0.4f), sharedSprite);
            CreateWall(arena.transform, "Left Wall", new Vector2(-halfWidth, 0f), new Vector2(0.4f, TestRoomSize.y), sharedSprite);
            CreateWall(arena.transform, "Right Wall", new Vector2(halfWidth, 0f), new Vector2(0.4f, TestRoomSize.y), sharedSprite);
        }

        private static void CreateWall(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Sprite sharedSprite)
        {
            GameObject wall = new(objectName);
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;

            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = new Color(0.25f, 0.65f, 0.75f, 1f);
            if (sharedSprite != null && sharedSprite.bounds.size.x > 0f && sharedSprite.bounds.size.y > 0f)
            {
                wall.transform.localScale = new Vector3(
                    size.x / sharedSprite.bounds.size.x,
                    size.y / sharedSprite.bounds.size.y,
                    1f);
                collider.size = new Vector2(
                    size.x / wall.transform.localScale.x,
                    size.y / wall.transform.localScale.y);
            }
        }

        private static T FindInScene<T>(Scene scene, System.Func<T, bool> predicate = null)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] matches = root.GetComponentsInChildren<T>(true);
                foreach (T match in matches)
                {
                    if (predicate == null || predicate(match))
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
