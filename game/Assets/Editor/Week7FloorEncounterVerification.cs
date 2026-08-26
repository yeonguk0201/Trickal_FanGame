using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7FloorEncounterVerification
    {
        private const string GraphRootName = "Week7 Fixed Room Graph";
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Verify Phase E-6 Floor Encounters")]
        public static void Verify()
        {
            Week7ThreeFloorGraphVerification.Verify();
            ValidateSceneEncounters();
            ValidateMixedSpawnRuntime();
            Debug.Log(
                "Phase E-6 floor encounter verification passed: six fixed normal compositions, three boss rooms, " +
                "boss drop roles, final-boss RunSession binding, mixed spawning, clear state, and invalid-list failures are valid.");
        }

        private static void ValidateSceneEncounters()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            RoomGraphController graph = FindSceneObject<RoomGraphController>(activeScene, GraphRootName);
            RunSession session = FindSceneObject<RunSession>(activeScene);
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            GameObject ranged = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            GameObject charging = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            Assert(graph != null && graph.Nodes.Count == 9 && session != null && session.gameObject.activeInHierarchy &&
                   chaser != null && ranged != null && charging != null,
                "Phase E-6 scene or enemy prefab prerequisites are missing.");

            BossController finalBoss = null;
            int bossCount = 0;
            foreach (RoomNode node in graph.Nodes)
            {
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                Assert(controller != null, $"{node.RoomId} is missing its RoomController.");
                Assert(controller.TryValidateEncounterConfiguration(out string error), error);
                Assert(controller.FloorNumber == node.FloorNumber && controller.RoomNumber == node.RoomNumber,
                    $"RoomController coordinates drifted from {node.RoomId}.");

                if (node.RoomNumber < 3)
                {
                    GameObject[] expected = ResolveComposition(
                        node.FloorNumber,
                        node.RoomNumber,
                        chaser,
                        ranged,
                        charging);
                    Assert(controller.EnemyPrefabs.Count == expected.Length &&
                           controller.SpawnPoints.Count == expected.Length,
                        $"{node.RoomId} composition does not match its fixed spawn count.");
                    for (int index = 0; index < expected.Length; index++)
                    {
                        Assert(controller.EnemyPrefabs[index] == expected[index],
                            $"{node.RoomId} has the wrong enemy at spawn {index + 1}.");
                    }

                    Assert(controller.PreplacedEnemies.Count == 0 &&
                           node.ContentRoot.GetComponentsInChildren<BossController>(true).Length == 0,
                        $"Normal room {node.RoomId} must not retain a preplaced boss.");
                    continue;
                }

                Assert(controller.EnemyPrefabs.Count == 0 && controller.PreplacedEnemies.Count == 1,
                    $"Boss room {node.RoomId} must use exactly one preplaced boss and no normal spawn list.");
                Health bossHealth = controller.PreplacedEnemies[0];
                BossController boss = bossHealth != null ? bossHealth.GetComponent<BossController>() : null;
                BossItemDrop drop = boss != null ? boss.GetComponent<BossItemDrop>() : null;
                Assert(boss != null && drop != null && boss.GetComponent<ItemDropSource>() != null,
                    $"Boss room {node.RoomId} is missing its boss, item source, or drop role.");
                Assert(drop.IsFinalBoss == (node.FloorNumber == 3),
                    $"Boss room {node.RoomId} has the wrong final-boss drop role.");
                bossCount++;
                if (node.FloorNumber == 3)
                {
                    finalBoss = boss;
                }
            }

            SerializedObject serializedSession = new(session);
            Assert(bossCount == 3 && finalBoss != null &&
                   serializedSession.FindProperty("boss").objectReferenceValue == finalBoss,
                "RunSession must bind only the floor 3 boss as the final Run-clear boss.");
        }

        private static void ValidateMixedSpawnRuntime()
        {
            GameObject root = new("Phase E-6 Mixed Spawn Verification");
            GameObject playerObject = new("Player");
            playerObject.transform.SetParent(root.transform);
            Health playerHealth = playerObject.AddComponent<Health>();
            playerObject.AddComponent<PlayerStats>();
            playerObject.AddComponent<PlayerMovement>();
            InvokeLifecycle(playerHealth, "Awake");

            GameObject firstTemplate = CreateEnemyTemplate(root.transform, "First Enemy Template");
            GameObject secondTemplate = CreateEnemyTemplate(root.transform, "Second Enemy Template");
            RunProgress progress = root.AddComponent<RunProgress>();
            GameObject roomObject = new("Mixed Room");
            roomObject.transform.SetParent(root.transform);
            roomObject.AddComponent<BoxCollider2D>();
            RoomController room = roomObject.AddComponent<RoomController>();
            Transform firstSpawn = new GameObject("Spawn 1").transform;
            Transform secondSpawn = new GameObject("Spawn 2").transform;
            firstSpawn.SetParent(roomObject.transform);
            secondSpawn.SetParent(roomObject.transform);
            firstSpawn.position = Vector2.left;
            secondSpawn.position = Vector2.right;
            room.Configure(1, 1, progress, null, new[] { firstSpawn, secondSpawn }, Array.Empty<DoorController>());
            room.ConfigureEnemyPrefabs(new[] { firstTemplate, secondTemplate });

            try
            {
                Assert(room.TryValidateEncounterConfiguration(out string error), error);
                room.BeginCombat(playerHealth);
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2,
                    "A valid per-spawn list must create and register both configured enemies.");

                List<Health> spawned = new();
                foreach (Health health in roomObject.GetComponentsInChildren<Health>())
                {
                    spawned.Add(health);
                }

                Assert(spawned.Count == 2 && spawned[0].gameObject.name.Contains("First Enemy Template") &&
                       spawned[1].gameObject.name.Contains("Second Enemy Template"),
                    "Mixed spawning must preserve each spawn slot's configured prefab order.");
                foreach (Health health in spawned)
                {
                    health.TakeDamage(health.MaxHealth);
                }

                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0 && progress.KillCount == 2,
                    "A mixed encounter must clear once all differently configured enemies die.");

                GameObject invalidRoomObject = new("Invalid Mixed Room");
                invalidRoomObject.transform.SetParent(root.transform);
                invalidRoomObject.AddComponent<BoxCollider2D>();
                RoomController invalidRoom = invalidRoomObject.AddComponent<RoomController>();
                invalidRoom.Configure(1, 2, progress, null, new[] { firstSpawn }, Array.Empty<DoorController>());
                invalidRoom.ConfigureEnemyPrefabs(new[] { firstTemplate, secondTemplate });
                Assert(!invalidRoom.TryValidateEncounterConfiguration(out error) && error.Contains("does not match"),
                    "A prefab/spawn count mismatch must fail encounter validation explicitly.");

                GameObject missingHealth = new("Missing Health Template");
                missingHealth.transform.SetParent(root.transform);
                invalidRoom.ConfigureEnemyPrefabs(new[] { missingHealth });
                Assert(!invalidRoom.TryValidateEncounterConfiguration(out error) && error.Contains("Health"),
                    "A configured enemy without Health must fail encounter validation explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateEnemyTemplate(Transform parent, string objectName)
        {
            GameObject template = new(objectName);
            template.transform.SetParent(parent);
            template.AddComponent<Health>();
            return template;
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

                Assert(match == null, $"Found multiple scene objects of type {typeof(T).Name}.");
                match = candidate;
            }

            return match;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
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
