using System;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource3Setup
    {
        public const string DropTableFolder = "Assets/Items/Drops";
        public const string RoomClearDropTablePath = DropTableFolder + "/room-clear-drop-table.asset";
        public const string SpPickupPrefabPath = "Assets/Prefabs/SPPickup.prefab";
        public const float RoomClearDropChance = 0.33f;

        public static readonly (string dropId, int weight)[] RoomClearWeights =
        {
            ("heart", 30),
            ("sp", 30),
            ("elif", 20),
            ("key", 12),
            ("bomb", 8),
        };

        // Scenes whose player carried the removed enemy-kill SP dropper; its leftover missing script is stripped.
        public static readonly string[] PlayerScenePaths =
        {
            Week13FrontendSetup.GameScenePath,
            "Assets/Scenes/ItemTestScene.unity",
            "Assets/Scenes/BossTestScene.unity",
            "Assets/Scenes/Boss2TestScene.unity",
        };

        [MenuItem("Trickal Fan Game/Week 17/Setup Resource-3 Room Clear Drops")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string previousScene = SceneManager.GetActiveScene().path;
            EnsureDropTable();
            foreach (string scenePath in PlayerScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool changed = RemoveMissingPlayerScripts(scene);
                if (scenePath == Week13FrontendSetup.GameScenePath)
                {
                    changed |= ConfigureAssembler();
                }

                if (changed && !EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"Resource-3 setup could not save {scenePath}.");
                }
            }

            if (!string.IsNullOrEmpty(previousScene) && previousScene != SceneManager.GetActiveScene().path)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            Debug.Log("Resource-3 room clear drops ready: each combat room rolls once on clear with a 33% chance " +
                      "to drop heart 30 / SP 30 / elif 20 / key 12 / bomb 8, and enemy kills no longer drop SP.");
        }

        public static ResourceDropTable EnsureDropTable()
        {
            return EnsureTable(RoomClearDropTablePath, RoomClearDropChance, RoomClearWeights,
                "Configure Resource-3 room clear drops");
        }

        // Shared by every drop source (room clear, obstacles): creates or updates a table asset in place.
        public static ResourceDropTable EnsureTable(string assetPath, float dropChance,
            (string dropId, int weight)[] weights, string undoLabel)
        {
            ResourceDropEntry[] entries = weights
                .Select(entry => new ResourceDropEntry(entry.dropId, LoadDropPrefab(entry.dropId), entry.weight))
                .ToArray();
            if (!AssetDatabase.IsValidFolder(DropTableFolder))
            {
                AssetDatabase.CreateFolder("Assets/Items", "Drops");
            }

            ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(assetPath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ResourceDropTable>();
                AssetDatabase.CreateAsset(table, assetPath);
            }

            Undo.RecordObject(table, undoLabel);
            table.Configure(dropChance, entries);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        // Stable drop IDs to pickup prefabs. "pit" is the Special-3 secret pit, which leads to the floor's secret room.
        public static GameObject LoadDropPrefab(string dropId)
        {
            switch (dropId)
            {
                case "heart":
                    HealthPickup heart = Week17Resource0Setup.EnsurePrefab(out string error);
                    return heart != null
                        ? heart.gameObject
                        : throw new InvalidOperationException($"Drops require the Resource-0 heart pickup. {error}");
                case "sp":
                    GameObject sp = AssetDatabase.LoadAssetAtPath<GameObject>(SpPickupPrefabPath);
                    return sp != null && sp.GetComponent<SPPickup>() != null
                        ? sp
                        : throw new InvalidOperationException($"Drops require the SP pickup at {SpPickupPrefabPath}.");
                case "elif":
                case "key":
                case "bomb":
                    if (!Week17Resource1Setup.EnsurePrefabs(out error))
                        throw new InvalidOperationException($"Drops require the Resource-1 pickups. {error}");
                    RunResourceType type = dropId == "elif" ? RunResourceType.Elif
                        : dropId == "key" ? RunResourceType.Key : RunResourceType.Bomb;
                    return Week17Resource1Setup.LoadPrefab(type).gameObject;
                case "pit":
                    return Week20Special3Setup.EnsureSecretPitPrefab().gameObject;
                default:
                    throw new InvalidOperationException($"Unknown drop '{dropId}'.");
            }
        }

        private static bool ConfigureAssembler()
        {
            // Opening a scene unloads assets held only from C#, so the table is loaded again after the scene opens.
            ResourceDropTable table =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(RoomClearDropTablePath);
            if (table == null)
            {
                throw new InvalidOperationException($"Resource-3 drop table is missing at {RoomClearDropTablePath}.");
            }

            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (assembler == null)
            {
                throw new InvalidOperationException("Resource-3 requires the game scene RoomGraphAssembler.");
            }

            if (assembler.EncounterClearDropTable == table)
            {
                return false;
            }

            Undo.RecordObject(assembler, "Configure Resource-3 room clear drops");
            assembler.ConfigureEncounterClearDrop(table);
            EditorUtility.SetDirty(assembler);
            return true;
        }

        private static bool RemoveMissingPlayerScripts(Scene scene)
        {
            int removed = 0;
            foreach (PlayerMovement player in Object.FindObjectsByType<PlayerMovement>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != scene ||
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(player.gameObject) == 0)
                {
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(player.gameObject, "Remove removed SP dropper");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player.gameObject);
            }

            return removed > 0;
        }
    }
}
