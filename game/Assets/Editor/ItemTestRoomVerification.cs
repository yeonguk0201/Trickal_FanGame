using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Debugging;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class ItemTestRoomVerification
    {
        [MenuItem("Trickal Fan Game/Debug/Verify Item Test Room", priority = 1)]
        public static void Verify()
        {
            if (!File.Exists(ItemTestRoomSetup.ScenePath))
            {
                throw new InvalidOperationException(
                    "ItemTestScene is missing. Run Debug > Open or Create Item Test Room first.");
            }

            bool openedForVerification = false;
            Scene scene = SceneManager.GetSceneByPath(ItemTestRoomSetup.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (EditorApplication.isPlaying)
                {
                    throw new InvalidOperationException("Open ItemTestScene before Play Mode verification.");
                }

                scene = EditorSceneManager.OpenScene(ItemTestRoomSetup.ScenePath, OpenSceneMode.Additive);
                openedForVerification = true;
            }

            try
            {
                ItemTestRoomController[] controllers = FindInScene<ItemTestRoomController>(scene);
                Assert(controllers.Length == 1, "ItemTestScene must contain exactly one ItemTestRoomController.");
                ItemTestRoomController controller = controllers[0];
                Assert(controller.TryValidateConfiguration(out string error), error);

                PlayerInventory[] inventories = FindInScene<PlayerInventory>(scene);
                Assert(inventories.Length == 1, "ItemTestScene must contain exactly one PlayerInventory.");
                Assert(FindInScene<PlayerMovement>(scene).Length == 1,
                    "ItemTestScene must contain exactly one controllable Player.");

                HashSet<string> ids = controller.ItemLoadout
                    .Select(entry => entry.Item.ItemId)
                    .ToHashSet(StringComparer.Ordinal);
                int activeCount = controller.ItemLoadout.Count(entry => entry.Item.IsActive);
                Assert(controller.ItemLoadout.Count == 11 && ids.Count == 11 && activeCount == 10 && ids.Contains("item-06"),
                    "The loadout editor must expose all 10 active artifacts and the inactive item-06 compatibility item.");
                Assert(controller.EnemyPlacements.Count == 3,
                    "The default test room must expose three independently editable enemy placements.");
                Assert(controller.EnemyPlacements.All(placement =>
                        placement.EnemyPrefab != null && placement.EnemyPrefab.GetComponent<Health>() != null),
                    "Every default enemy placement must reference a prefab with Health.");
                Assert(scene.GetRootGameObjects().All(root =>
                        root.name == "Player" || root.name == "Main Camera" ||
                        root.name == "Global Light 2D" || root.name == ItemTestRoomSetup.RootName),
                    "ItemTestScene must remain isolated from random floor and legacy test roots.");
                Assert(FindInScene<TrickalFanGame.Run.RunSession>(scene).Length == 0,
                    "ItemTestScene must not contain a RunSession or attempt Backend result saving.");

                if (EditorApplication.isPlaying)
                {
                    Assert(controller.HasAppliedLoadout,
                        "The configured item loadout must be applied at Play Mode start.");
                    Assert(controller.ItemLoadout.All(entry =>
                            inventories[0].GetStackCount(entry.Item.ItemId) >= entry.StartingStacks),
                        "Every configured starting stack must be present in the Player inventory.");
                    int expectedEnemies = controller.EnemyPlacements.Count(placement => placement.Enabled);
                    Assert(controller.SpawnedEnemyCount == expectedEnemies,
                        "Every enabled enemy placement must spawn exactly one enemy in Play Mode.");
                }

                string mode = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
                Debug.Log(
                    $"Item Test Room verification passed in {mode}: isolated scene, 10 active + 1 legacy item " +
                    "loadout entries, editable stacks, three editable enemy placements, and runtime debug controls are valid.",
                    controller);
            }
            finally
            {
                if (openedForVerification)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
