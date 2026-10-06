using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Floor1Verification
    {
        private const int SeedCount = 1024;

        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Floor-1 Expanded Floors")]
        public static void SetupAndVerifyBatch()
        {
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week20Floor1Setup.Setup();
            Week20Floor1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Floor-1 setup must preserve the Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Floor-1 Expanded Floors")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.FloorCount == 3 && generator.FloorSettings.Count == 3 &&
                   generator.RoomContentVersion >= Week20Floor1Setup.RoomContentVersion &&
                   generator.EncounterContentVersion >= Week20Floor1Setup.EncounterContentVersion,
                "Floor-1 requires its three configured floor settings and content versions.");
            FloorGenerationSettings[] expected = Week20Floor1Setup.CreateSettings();
            for (int i = 0; i < expected.Length; i++)
                Assert(generator.FloorSettings[i].MinimumTotalRooms == expected[i].MinimumTotalRooms &&
                       generator.FloorSettings[i].MaximumTotalRooms == expected[i].MaximumTotalRooms &&
                       generator.FloorSettings[i].MinimumBossDistance == expected[i].MinimumBossDistance,
                    $"Floor {i + 1} settings differ from the confirmed Floor-1 rules.");

            HashSet<int>[] sizes = { new(), new(), new() };
            int secrets = 0, shops = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(first.TryValidate(out error), error);
                if (seed <= 64)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                    Assert(Signature(first) == Signature(repeated), $"Floor-1 seed {seed} did not reproduce.");
                }
                foreach (GeneratedFloor floor in first.Floors)
                {
                    FloorGenerationSettings settings = expected[floor.FloorNumber - 1];
                    Assert(floor.Nodes.Count >= settings.MinimumTotalRooms && floor.Nodes.Count <= settings.MaximumTotalRooms,
                        $"Seed {seed} floor {floor.FloorNumber} total size is outside its range.");
                    sizes[floor.FloorNumber - 1].Add(floor.Nodes.Count);
                    Assert(floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Start) == 1 &&
                           floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Boss) == 1 &&
                           floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Treasure) == 1 &&
                           floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Secret) <= 1 &&
                           floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Shop) <= 1,
                        $"Seed {seed} floor {floor.FloorNumber} has invalid role counts.");
                    secrets += floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Secret);
                    shops += floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Shop);
                    Dictionary<string, GeneratedRoomNode> byId = floor.Nodes.ToDictionary(node => node.RoomId);
                    Dictionary<string, int> distances = Distances(floor, byId);
                    Assert(distances[floor.BossRoomId] >= settings.MinimumBossDistance &&
                           byId[floor.BossRoomId].DirectionalConnections.Count == 1,
                        $"Seed {seed} floor {floor.FloorNumber} violates the boss end-room distance.");
                    Assert(ReachBossWithoutKeys(floor, byId),
                        $"Seed {seed} floor {floor.FloorNumber} needs a key to reach the boss.");
                    foreach (GeneratedRoomNode node in floor.Nodes)
                    {
                        Assert(node.RoomId == FloorGenerator.BuildRoomId(node.FloorNumber, node.RoomNumber),
                            "Floor-1 changed the stable room ID format.");
                        Assert(node.Template != null &&
                               (node.Role != GeneratedRoomRole.Intermediate ||
                                node.Encounter != null && node.HasDifficulty && node.ResolvedEncounterWaves.Count > 0),
                            $"Seed {seed} room {node.RoomId} has no compatible content.");
                    }
                }
            }
            for (int i = 0; i < sizes.Length; i++)
                Assert(Enumerable.Range(expected[i].MinimumTotalRooms,
                    expected[i].MaximumTotalRooms - expected[i].MinimumTotalRooms + 1).All(sizes[i].Contains),
                    $"Floor {i + 1} seeds did not cover every configured total room count.");
            int floorCount = SeedCount * 3;
            Assert(secrets > floorCount * 0.45f && secrets < floorCount * 0.55f &&
                   shops > floorCount * 0.55f && shops < floorCount * 0.65f,
                "Floor-1 changed the secret-room 50% or shop 60% generation rates.");
            ValidateInvalidSettings(generator);
            Debug.Log($"Floor-1 verification passed: {SeedCount} seeds x 3 floors, every size in 8~12/8~12/12~18, " +
                      $"boss distances 4/5/5, key-free required routes, valid content and deterministic repeats; " +
                      $"secret rooms {secrets}/{floorCount}, shops {shops}/{floorCount}; invalid settings fail explicitly.");
        }

        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            VerifyRegression(Week20Special1Verification.Verify);
            VerifyRegression(Week20Special3Verification.Verify);
            VerifyRegression(Week20Special4Verification.Verify);
            VerifyRegression(Week19Difficulty1Verification.Verify);
            VerifyRegression(Week19Encounter4Verification.Verify);
            VerifyRegression(Week14Room0RegressionVerification.Verify);
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Debug.Log("Floor-1 and all six related regressions passed.");
        }

        private static void VerifyRegression(Action verification)
        {
            // Edit Mode time does not advance between these synchronous fixtures. Reload saved references and
            // clear runtime-only transition cooldowns, rewards and inventory before every independent verification.
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            verification();
        }

        private static void ValidateInvalidSettings(FloorGenerator configured)
        {
            GameObject holder = new("Floor-1 invalid settings");
            try
            {
                FloorGenerator candidate = holder.AddComponent<FloorGenerator>();
                candidate.Configure(3, 6, 8, 3, 32, configured.RoomDefinitions.ToArray());
                FloorGenerationSettings[][] invalid =
                {
                    new[] { new FloorGenerationSettings(8, 12, 4) },
                    new FloorGenerationSettings[] { null, new(8, 12, 5), new(12, 18, 5) },
                    new[] { new FloorGenerationSettings(8, 7, 4), new(8, 12, 5), new(12, 18, 5) },
                    new[] { new FloorGenerationSettings(8, 100, 4), new(8, 12, 5), new(12, 18, 5) },
                    new[] { new FloorGenerationSettings(8, 12, 6), new(8, 12, 5), new(12, 18, 5) },
                };
                foreach (FloorGenerationSettings[] settings in invalid)
                {
                    candidate.ConfigureFloorSettings(settings);
                    Assert(!candidate.TryGenerateForSeed(1, out GeneratedFloorGraph graph, out string error) &&
                           graph == null && !string.IsNullOrWhiteSpace(error),
                        "Invalid Floor-1 settings must fail with no graph and a diagnostic.");
                }
            }
            finally { Object.DestroyImmediate(holder); }
        }

        private static Dictionary<string, int> Distances(GeneratedFloor floor, Dictionary<string, GeneratedRoomNode> byId)
        {
            Dictionary<string, int> distances = new() { [floor.StartingRoomId] = 0 };
            Queue<string> queue = new();
            queue.Enqueue(floor.StartingRoomId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (!connection.IsSecret && distances.TryAdd(connection.DestinationRoomId, distances[current] + 1))
                        queue.Enqueue(connection.DestinationRoomId);
            }
            return distances;
        }

        private static bool ReachBossWithoutKeys(GeneratedFloor floor, Dictionary<string, GeneratedRoomNode> byId)
        {
            HashSet<string> reached = new() { floor.StartingRoomId };
            Queue<string> queue = new();
            queue.Enqueue(floor.StartingRoomId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == floor.BossRoomId) return true;
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (!connection.IsSecret && !byId[connection.DestinationRoomId].RequiresKey &&
                        reached.Add(connection.DestinationRoomId)) queue.Enqueue(connection.DestinationRoomId);
            }
            return false;
        }

        private static string Signature(GeneratedFloorGraph graph) => string.Join("|", graph.Nodes.Select(node =>
            $"{node.RoomId}@{node.GridPosition}:{node.Role}:{node.ContentSeed}:{node.RequiresKey}:{node.TemplateId}:" +
            $"{node.EncounterId}:{node.Difficulty.Score}:{node.Difficulty.Tier}:{node.Difficulty.TargetTier}:" +
            string.Join(",", node.DirectionalConnections.Select(c => $"{c.Direction}>{c.DestinationRoomId}:{c.IsSecret}")) +
            string.Join("/", node.ResolvedEncounterWaves.Select(wave =>
                string.Join(",", wave.Select(spawn => $"{spawn.Role}@{spawn.SpawnPointIndex}"))))));

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
