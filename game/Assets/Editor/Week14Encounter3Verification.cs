using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter3Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Encounter-3 Waves and Revisit State")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   assembler.EncounterClearRewardPrefab != null,
                "Run Encounter-3 Setup before verification.");
            string[] baselineEncounterIds =
            {
                "crossfire-ranged", "lane-charging-ranged", "pressure-chaser-ranged",
                Week14Encounter3Setup.EncounterId,
            };
            Assert(generator.EncounterContentVersion >= Week14Encounter3Setup.ContentVersion &&
                   baselineEncounterIds.All(id => generator.EncounterDefinitions.Any(definition =>
                       definition.EncounterId == id)),
                "Encounter-3 catalog or content version is not configured.");

            EncounterDefinition twoWave = generator.EncounterDefinitions.Single(definition =>
                definition.EncounterId == Week14Encounter3Setup.EncounterId);
            ValidateDefinitionAndSelection(generator, twoWave);
            ValidateWaveStateBoundaries();
            ValidateRuntimeProgression(assembler, twoWave);
            ValidateAssemblerBinding(assembler);
            Week14Room5Verification.Verify();
            Debug.Log("Week 14 Encounter-3 verification passed: two waves wait for every required enemy, " +
                      "start and complete once, persist completed-wave and clear-reward state, restore safely, " +
                      "grant one SP pickup, and preserve Room-0~5 regressions.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Encounter3Setup.Setup();
            string encounterGuid = AssetDatabase.AssetPathToGUID(Week14Encounter3Setup.EncounterPath);
            Week14Encounter3Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) &&
                   encounterGuid == AssetDatabase.AssetPathToGUID(Week14Encounter3Setup.EncounterPath),
                "Encounter-3 Setup changed the Game Scene or two-wave Encounter GUID.");
            Verify();
        }

        private static void ValidateDefinitionAndSelection(FloorGenerator generator,
            EncounterDefinition twoWave)
        {
            Assert(twoWave.TryValidate(out string error), error);
            Assert(twoWave.MinimumFloor == 2 && twoWave.MaximumFloor == 3 && twoWave.Waves.Count == 2,
                "The reinforcement Encounter must be a two-wave floor 2-3 composition.");
            Assert(twoWave.Waves[0].StartCondition == EncounterWaveStartCondition.RoomEntered &&
                   twoWave.Waves[1].StartCondition == EncounterWaveStartCondition.PreviousWaveCleared,
                "The second wave must wait for the first wave to clear.");

            int selectedCount = 0;
            for (int seed = 1; seed <= 256; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out error), error);
                foreach (GeneratedRoomNode node in graph.Nodes.Where(node =>
                             node.EncounterId == Week14Encounter3Setup.EncounterId))
                {
                    Assert(node.FloorNumber >= 2, "The two-wave Encounter appeared before floor 2.");
                    for (int waveIndex = 0; waveIndex < 2; waveIndex++)
                    {
                        Assert(twoWave.TryResolveWave(node.Template, node.FloorNumber,
                            node.DirectionalConnections, waveIndex,
                            out ResolvedEncounterSpawn[] spawns, out error), error);
                        Assert(spawns.Length == 2 &&
                               spawns.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == 2,
                            $"Two-wave Encounter wave {waveIndex + 1} must resolve two unique SpawnPoints.");
                    }
                    selectedCount++;
                }
            }
            Assert(selectedCount > 0, "256 seeds did not select the two-wave Encounter.");
        }

        private static void ValidateWaveStateBoundaries()
        {
            RoomRunState state = new("wave-state-boundary");
            Assert(state.TryMarkWaveCompleted(1) && state.CompletedWaveCount == 1,
                "The first wave completion was not persisted.");
            Assert(!state.TryMarkWaveCompleted(1) && state.CompletedWaveCount == 1,
                "The first wave completed more than once.");
            bool rejectedGap = false;
            try { state.TryMarkWaveCompleted(3); }
            catch (InvalidOperationException) { rejectedGap = true; }
            Assert(rejectedGap, "Wave state must reject skipped completion order.");
            Assert(state.TryMarkClearRewardGranted() && !state.TryMarkClearRewardGranted(),
                "Clear reward state must be granted exactly once.");
        }

        private static void ValidateRuntimeProgression(RoomGraphAssembler assembler,
            EncounterDefinition definition)
        {
            GameObject root = new("Encounter-3 Runtime Verification");
            try
            {
                GameObject player = new("Encounter-3 Player");
                player.transform.SetParent(root.transform);
                Health playerHealth = player.AddComponent<Health>();

                Transform[] points = CreateSpawnPoints(root.transform, 4);
                GameObject chaser = Resolve(assembler, EncounterEnemyRole.Chaser);
                GameObject ranged = Resolve(assembler, EncounterEnemyRole.Ranged);
                GameObject charging = Resolve(assembler, EncounterEnemyRole.Charging);
                EncounterRuntimeWave[] waves =
                {
                    new(new[] { chaser, ranged }, new[] { points[0], points[1] }),
                    new(new[] { charging, ranged }, new[] { points[2], points[3] }),
                };
                RoomRunState state = new("runtime-two-wave-room");
                RoomController controller = CreateController(root.transform, "Primary Controller", state,
                    waves, assembler.EncounterClearRewardPrefab, out RoomClearRewardSpawner reward);
                List<GameObject> spawned = new();
                List<int> started = new();
                List<int> completed = new();
                controller.EnemySpawned += spawned.Add;
                controller.WaveStarted += started.Add;
                controller.WaveCompleted += completed.Add;

                controller.BeginCombat(playerHealth);
                Assert(controller.State == RoomState.Combat && controller.CurrentWaveNumber == 1 &&
                       controller.AliveEnemyCount == 2 && spawned.Count == 2,
                    "The first wave did not start with both required enemies.");
                Kill(spawned[0]);
                Assert(controller.AliveEnemyCount == 1 && state.CompletedWaveCount == 0 && spawned.Count == 2,
                    "A partial enemy wipe advanced the first wave.");
                Kill(spawned[1]);
                Assert(controller.CurrentWaveNumber == 2 && controller.AliveEnemyCount == 2 &&
                       state.CompletedWaveCount == 1 && spawned.Count == 4,
                    "The full first-wave wipe did not start the second wave exactly once.");
                Kill(spawned[2]);
                Assert(controller.State == RoomState.Combat && controller.AliveEnemyCount == 1 &&
                       !state.IsCleared && !state.HasGrantedClearReward,
                    "A partial second-wave wipe cleared or rewarded the room.");
                Kill(spawned[3]);
                Assert(controller.State == RoomState.Cleared && state.IsCleared &&
                       state.CompletedWaveCount == 2 && state.HasGrantedClearReward &&
                       reward.LastSpawnedReward != null && started.SequenceEqual(new[] { 1, 2 }) &&
                       completed.SequenceEqual(new[] { 1, 2 }),
                    "The final wipe did not clear and reward the room exactly once.");
                int childCountAfterClear = root.transform.childCount;
                controller.BeginCombat(playerHealth);
                Assert(spawned.Count == 4 && root.transform.childCount == childCountAfterClear,
                    "Re-entering the live cleared controller created enemies or rewards.");

                RoomController revisit = CreateController(root.transform, "Revisited Controller", state,
                    waves, assembler.EncounterClearRewardPrefab, out RoomClearRewardSpawner revisitReward);
                int revisitSpawnCount = 0;
                revisit.EnemySpawned += _ => revisitSpawnCount++;
                revisit.BeginCombat(playerHealth);
                Assert(revisit.State == RoomState.Cleared && revisitSpawnCount == 0 &&
                       !revisitReward.TrySpawn() && revisitReward.LastSpawnedReward == null,
                    "A reconstructed cleared room created duplicate enemies or rewards.");

                RoomRunState partialState = new("partial-wave-room");
                partialState.TryMarkWaveCompleted(1);
                RoomController partial = CreateController(root.transform, "Partial Controller", partialState,
                    waves, assembler.EncounterClearRewardPrefab, out _);
                int partialSpawnCount = 0;
                partial.EnemySpawned += _ => partialSpawnCount++;
                partial.BeginCombat(playerHealth);
                Assert(partial.CurrentWaveNumber == 2 && partialSpawnCount == 2 &&
                       partialState.CompletedWaveCount == 1,
                    "A reconstructed partial Encounter did not resume from its first incomplete wave.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateAssemblerBinding(RoomGraphAssembler assembler)
        {
            int seed = FindFloorTwoSeed(assembler.Generator);
            Assert(seed > 0, "Could not find a floor-2 seed containing the two-wave Encounter.");
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            Assert(assembler.TryLoadFloor(2, assembler.Graph.Player, out error), error);
            GeneratedRoomNode generated = assembler.GeneratedGraph.FindFloor(2).Nodes.First(node =>
                node.EncounterId == Week14Encounter3Setup.EncounterId);
            RoomPrefab instance = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == generated.RoomId);
            RoomClearRewardSpawner reward = instance.Controller.GetComponent<RoomClearRewardSpawner>();
            Assert(instance.Controller.WaveCount == 2 &&
                   instance.Controller.EncounterWaves.All(wave => wave.EnemyPrefabs.Count == 2) &&
                   reward != null && reward.PickupPrefab == assembler.EncounterClearRewardPrefab,
                "RoomGraphAssembler did not bind both waves and the clear reward Prefab.");
        }

        private static RoomController CreateController(Transform parent, string name, RoomRunState state,
            EncounterRuntimeWave[] waves, SPPickup rewardPrefab, out RoomClearRewardSpawner reward)
        {
            GameObject room = new(name);
            room.transform.SetParent(parent);
            room.AddComponent<BoxCollider2D>();
            RoomController controller = room.AddComponent<RoomController>();
            controller.Configure(2, 2, null, null, Array.Empty<Transform>(), Array.Empty<DoorController>());
            controller.ConfigureEncounterWaves(waves);
            reward = room.AddComponent<RoomClearRewardSpawner>();
            reward.Configure(rewardPrefab, room.transform, parent, state);
            controller.ConfigureClearReward(reward);
            controller.BindRunState(state, false);
            Assert(controller.TryValidateEncounterConfiguration(out string error), error);
            return controller;
        }

        private static Transform[] CreateSpawnPoints(Transform parent, int count)
        {
            Transform[] points = new Transform[count];
            for (int index = 0; index < count; index++)
            {
                GameObject point = new($"Verification Spawn {index + 1}");
                point.transform.SetParent(parent);
                point.transform.localPosition = new Vector3(index - 1.5f, index % 2 == 0 ? 1f : -1f, 0f);
                points[index] = point.transform;
            }
            return points;
        }

        private static GameObject Resolve(RoomGraphAssembler assembler, EncounterEnemyRole role)
        {
            Assert(assembler.EnemyRoster.TryResolve(role, out GameObject prefab, out string error), error);
            return prefab;
        }

        private static void Kill(GameObject enemy)
        {
            Health health = enemy != null ? enemy.GetComponent<Health>() : null;
            Assert(health != null && !health.IsDead, "The expected required enemy is missing or already dead.");
            health.TakeDamage(health.MaxHealth + 1f);
            Assert(health.IsDead, "The required enemy did not die during verification.");
        }

        private static int FindFloorTwoSeed(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 4096; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                if (graph.FindFloor(2).Nodes.Any(node =>
                        node.EncounterId == Week14Encounter3Setup.EncounterId)) return seed;
            }
            return -1;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
