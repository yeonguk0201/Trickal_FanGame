using System;
using System.Reflection;
using TrickalFanGame.Character;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week8FloorDifficultyVerification
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";
        private const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";
        private const string CharacterAssetPath = "Assets/Characters/erpin.asset";

        [MenuItem("Trickal Fan Game/Verify Phase E-8 Floor Difficulty Scaling")]
        public static void Verify()
        {
            VerifyMultiplierValues();
            VerifyChaserScaling();
            VerifyRangedScaling();
            VerifyChargingScaling();
            VerifyBossScaling();
            VerifyRoomControllerIntegration();
            VerifyFullRunProgression();

            Debug.Log(
                "Phase E-8 floor difficulty scaling verification passed: " +
                "all normal enemies and bosses gain health on every floor and resolve tiered heart damage, " +
                "RoomController applies scaling without replacing pattern tuning, and a selected character " +
                "can clear all nine rooms from floor 1 through the floor 3 final boss.");
        }

        private static void VerifyMultiplierValues()
        {
            Assert(Mathf.Approximately(FloorDifficultyScaler.GetHealthMultiplier(1), 1.0f),
                "Floor 1 health multiplier must be 1.0.");
            Assert(Mathf.Approximately(FloorDifficultyScaler.GetHealthMultiplier(2), 1.5f),
                "Floor 2 health multiplier must be 1.5.");
            Assert(Mathf.Approximately(FloorDifficultyScaler.GetHealthMultiplier(3), 2.0f),
                "Floor 3 health multiplier must be 2.0.");

            // Half-heart units per tier and floor (docs/idea-implementation/01-player-heart-health.md, section 5).
            int[,] expectedUnits =
            {
                { 2, 2, 3 },
                { 3, 4, 5 },
                { 4, 6, 7 },
                { 6, 7, 8 },
            };
            foreach (EnemyDamageTier tier in Enum.GetValues(typeof(EnemyDamageTier)))
            {
                int previousUnits = 0;
                for (int floor = 1; floor <= 3; floor++)
                {
                    int units = HealthUnits.GetEnemyDamageUnits(tier, floor);
                    int expected = expectedUnits[(int)tier, floor - 1];
                    Assert(units == expected, $"{tier} floor {floor} must deal {expected} units, got {units}.");
                    Assert(units >= HealthUnits.MinimumEnemyDamageUnits,
                        $"{tier} floor {floor} must deal at least one heart.");
                    Assert(units >= previousUnits, $"{tier} damage must not decrease on floor {floor}.");
                    previousUnits = units;
                }
            }

            Assert(HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Critical, 4) ==
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Critical, 3),
                "Floors beyond the table must reuse the floor 3 row.");
        }

        private static void VerifyChaserScaling()
        {
            VerifyPrefabScaling(ChaserPrefabPath, "Chaser",
                prefab => prefab.GetComponent<ContactDamage>().DamageTier, EnemyDamageTier.Heavy);

            GameObject prefab = LoadPrefab(ChaserPrefabPath);
            for (int floor = 1; floor <= 3; floor++)
            {
                GameObject instance = Object.Instantiate(prefab);
                GameObject playerObject = CreateUnitPlayer(out Health playerHealth);
                try
                {
                    FloorDifficultyScaler.ApplyScaling(instance, floor);
                    float before = playerHealth.CurrentHealth;
                    Assert(instance.GetComponent<ContactDamage>().TryApplyDamage(playerHealth, 0f),
                        $"Chaser floor {floor} contact must hit a living player.");
                    int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Heavy, floor);
                    Assert(Mathf.Approximately(before - playerHealth.CurrentHealth, expectedUnits),
                        $"Chaser floor {floor} contact must remove {expectedUnits} units, " +
                        $"removed {before - playerHealth.CurrentHealth}.");
                }
                finally
                {
                    Object.DestroyImmediate(playerObject);
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void VerifyRangedScaling()
        {
            VerifyPrefabScaling(RangedPrefabPath, "Ranged",
                prefab => prefab.GetComponent<RangedEnemyController>().ProjectileDamageTier,
                EnemyDamageTier.Heavy);
        }

        private static void VerifyChargingScaling()
        {
            VerifyPrefabScaling(ChargingPrefabPath, "Charging",
                prefab => prefab.GetComponent<ChargingEnemyController>().ChargeDamageTier,
                EnemyDamageTier.Heavy);
        }

        private static void VerifyBossScaling()
        {
            VerifyPrefabScaling(BossPrefabPath, "Boss",
                prefab => prefab.GetComponent<BossController>().ProjectileDamageTier,
                EnemyDamageTier.Medium);
        }

        private static void VerifyPrefabScaling(
            string path,
            string label,
            Func<GameObject, EnemyDamageTier> readTier,
            EnemyDamageTier expectedTier)
        {
            GameObject prefab = LoadPrefab(path);
            float baseHealth = prefab.GetComponent<Health>().MaxHealth;
            Assert(readTier(prefab) == expectedTier,
                $"{label} must use the {expectedTier} damage tier, got {readTier(prefab)}.");

            for (int floor = 1; floor <= 3; floor++)
            {
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    FloorDifficultyScaler.ApplyScaling(instance, floor);
                    Health health = instance.GetComponent<Health>();
                    float expectedHealth = baseHealth * FloorDifficultyScaler.GetHealthMultiplier(floor);

                    Assert(Mathf.Approximately(health.MaxHealth, expectedHealth),
                        $"{label} floor {floor} health must be {expectedHealth}, got {health.MaxHealth}.");
                    Assert(EnemyFloorLevel.Resolve(instance) == floor && readTier(instance) == expectedTier,
                        $"{label} floor {floor} must record its floor and keep the {expectedTier} tier.");
                    DamageContext context = HealthUnits.CreateEnemyDamageContext(
                        instance, DamageSourceType.EnemyContact, expectedTier);
                    Assert(Mathf.Approximately(context.BaseDamage,
                               HealthUnits.GetEnemyDamageUnits(expectedTier, floor)),
                        $"{label} floor {floor} damage context must carry the floor {floor} tier units.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static GameObject CreateUnitPlayer(out Health health)
        {
            GameObject playerObject = new("Floor Difficulty Unit Player");
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = playerObject.AddComponent<Health>();
            PlayerStats stats = playerObject.AddComponent<PlayerStats>();
            playerObject.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            Assert(health.UsesHealthUnits && Mathf.Approximately(health.CurrentHealth, 10f),
                "PlayerStats must switch the player to half-heart units with 10 starting units.");
            return playerObject;
        }

        private static void VerifyRoomControllerIntegration()
        {
            string roomControllerPath = "Assets/Scripts/Room/RoomController.cs";
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(roomControllerPath);
            Assert(script != null, $"RoomController script not found at {roomControllerPath}.");

            string sourceCode = script.text;
            Assert(sourceCode.Contains("FloorDifficultyScaler.ApplyScaling"),
                "RoomController must call FloorDifficultyScaler.ApplyScaling when spawning enemies.");
        }

        private static void VerifyFullRunProgression()
        {
            Week7FloorTransitionVerification.Verify();

            float originalTimeScale = Time.timeScale;
            GameObject root = new("Phase E-8 Full Run Verification");
            try
            {
                GameObject playerObject = new("Full Run Player");
                playerObject.transform.SetParent(root.transform);
                playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
                Health playerHealth = playerObject.AddComponent<Health>();
                playerObject.AddComponent<PlayerStats>();
                PlayerMovement player = playerObject.AddComponent<PlayerMovement>();
                InvokeLifecycle(playerHealth, "Awake");
                InvokeLifecycle(player, "Awake");

                RunProgress progress = root.AddComponent<RunProgress>();
                RoomGraphController graph = root.AddComponent<RoomGraphController>();
                SetPrivateField(graph, "transitionCooldown", 0f);
                RuntimeRoom[] rooms = new RuntimeRoom[9];
                BossController finalBoss = null;

                for (int index = 0; index < rooms.Length; index++)
                {
                    int floor = index / 3 + 1;
                    int room = index % 3 + 1;
                    rooms[index] = CreateRuntimeRoom(root.transform, floor, room, progress);
                    if (floor == 3 && room == 3)
                    {
                        finalBoss = rooms[index].Enemy.GetComponent<BossController>();
                    }
                }

                for (int index = 0; index < rooms.Length - 1; index++)
                {
                    GameObject doorwayObject = new($"Run Link {index + 1}");
                    doorwayObject.transform.SetParent(rooms[index].Content.transform);
                    doorwayObject.AddComponent<BoxCollider2D>();
                    RoomDoorway doorway = doorwayObject.AddComponent<RoomDoorway>();
                    doorway.Configure(
                        graph,
                        rooms[index].Node,
                        rooms[index + 1].Node,
                        rooms[index + 1].Entry,
                        rooms[index].Controller,
                        true);
                    rooms[index].Node.SetDoorways(new[] { doorway });
                    rooms[index].ForwardDoorway = doorway;
                }

                RoomNode[] nodes = new RoomNode[rooms.Length];
                for (int index = 0; index < rooms.Length; index++)
                {
                    nodes[index] = rooms[index].Node;
                }

                graph.Configure(nodes, rooms[0].Node, player, null, progress);
                Assert(graph.TryValidateConfiguration(out string graphError), graphError);

                RunSession session = root.AddComponent<RunSession>();
                session.Configure(playerHealth, progress, finalBoss);
                session.SetWaitForCharacterSelection(true);
                session.SetResultSavingEnabled(false);
                CharacterDefinition character =
                    AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterAssetPath);
                Assert(character != null && character.IsValid,
                    $"Missing valid character definition at {CharacterAssetPath}.");
                CharacterSelectionUI selection = root.AddComponent<CharacterSelectionUI>();
                selection.Configure(session, new[] { character }, player, null);
                InvokeLifecycle(selection, "Awake");
                InvokePrivate(selection, "Select", character);
                Assert(session.HasStarted && session.CharacterId == character.CharacterId,
                    "Selecting the configured character must begin the Run with its stable ID.");

                InvokeLifecycle(graph, "Start");
                Assert(graph.CurrentNode == rooms[0].Node && progress.CurrentFloor == 1 && progress.CurrentRoom == 1,
                    "The Run must start at floor 1 room 1.");

                for (int index = 0; index < rooms.Length; index++)
                {
                    RuntimeRoom current = rooms[index];
                    current.Controller.BeginCombat(playerHealth);
                    Assert(current.Controller.State == RoomState.Combat && current.Controller.AliveEnemyCount == 1,
                        $"{current.Node.RoomId} must start one registered encounter.");
                    current.Enemy.TakeDamage(current.Enemy.MaxHealth);
                    Assert(current.Controller.State == RoomState.Cleared,
                        $"{current.Node.RoomId} must clear after its encounter dies.");

                    if (index < rooms.Length - 1)
                    {
                        Assert(current.ForwardDoorway.IsOpen && current.ForwardDoorway.TryEnter(player),
                            $"{current.Node.RoomId} must transition to the next Run room after clear.");
                        Assert(graph.CurrentNode == rooms[index + 1].Node,
                            $"Run transition after {current.Node.RoomId} reached the wrong room.");
                    }
                }

                Assert(session.HasEnded && session.IsCleared && playerHealth.IsDead == false,
                    "Killing the floor 3 final boss must end the living player's Run as cleared.");
                Assert(progress.CurrentFloor == 3 && progress.CurrentRoom == 3 &&
                       progress.KillCount == 9 && progress.IsProgressionStopped,
                    "A full clear must stop at floor 3 room 3 with all nine encounters recorded.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        private static RuntimeRoom CreateRuntimeRoom(
            Transform parent,
            int floor,
            int room,
            RunProgress progress)
        {
            string roomId = $"floor-{floor:00}-room-{room:00}";
            GameObject nodeObject = new(roomId);
            nodeObject.transform.SetParent(parent);
            RoomNode node = nodeObject.AddComponent<RoomNode>();
            GameObject content = new("Content");
            content.transform.SetParent(nodeObject.transform);
            Transform anchor = new GameObject("Camera Anchor").transform;
            anchor.SetParent(nodeObject.transform);
            Transform entry = new GameObject("Entry").transform;
            entry.SetParent(nodeObject.transform);
            entry.position = new Vector2(room * 3f, -floor * 3f);

            GameObject encounterObject = new("Encounter");
            encounterObject.transform.SetParent(content.transform);
            encounterObject.AddComponent<BoxCollider2D>();
            RoomController controller = encounterObject.AddComponent<RoomController>();
            controller.Configure(
                floor,
                room,
                progress,
                null,
                Array.Empty<Transform>(),
                Array.Empty<DoorController>());

            GameObject enemyObject = new(room == 3 ? $"Floor {floor} Boss" : "Normal Enemy");
            enemyObject.transform.SetParent(content.transform);
            Health enemy = enemyObject.AddComponent<Health>();
            if (room == 3)
            {
                BossController boss = enemyObject.AddComponent<BossController>();
                InvokeLifecycle(boss, "Awake");
            }
            else
            {
                enemyObject.AddComponent<ContactDamage>();
            }

            InvokeLifecycle(enemy, "Awake");
            controller.ConfigurePreplacedEnemies(new[] { enemy });
            InvokeLifecycle(controller, "Awake");
            node.Configure(roomId, floor, room, content, anchor, entry, Array.Empty<RoomDoorway>());
            return new RuntimeRoom(node, content, entry, controller, enemy);
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            InvokePrivate(component, methodName);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, $"Missing enemy prefab at {path}.");
            return prefab;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class RuntimeRoom
        {
            public RuntimeRoom(
                RoomNode node,
                GameObject content,
                Transform entry,
                RoomController controller,
                Health enemy)
            {
                Node = node;
                Content = content;
                Entry = entry;
                Controller = controller;
                Enemy = enemy;
            }

            public RoomNode Node { get; }
            public GameObject Content { get; }
            public Transform Entry { get; }
            public RoomController Controller { get; }
            public Health Enemy { get; }
            public RoomDoorway ForwardDoorway { get; set; }
        }
    }
}
