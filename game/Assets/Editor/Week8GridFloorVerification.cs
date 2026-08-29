using System;
using System.Collections.Generic;
using System.Text;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week8GridFloorVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase F-5 Seeded Grid Floors")]
        public static void Verify()
        {
            Week8GridFloorSetup.Setup();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            Week8GridFloorSetup.Setup();

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = generator != null ? generator.GetComponent<RoomGraphAssembler>() : null;
            Assert(generator != null && assembler != null && assembler.ConfiguredRoomPrefab != null,
                "Run Phase F-5 Setup before verification.");
            Assert(generator.FloorCount == 3 && generator.MinimumRoomsPerFloor == 6 &&
                   generator.MaximumRoomsPerFloor == 8 && generator.MinimumBossDistance == 3 &&
                   generator.GenerationRetryLimit == 32,
                "Phase F-5 generator settings must be 3 floors, 6-8 rooms, distance 3, and 32 retries.");
            Assert(prefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath) &&
                   CountComponents<RoomGraphAssembler>(generator.gameObject) == 1 &&
                   CountGeneratedFloorRoots(generator.transform) == 1,
                "Running Setup twice must preserve the prefab GUID and avoid duplicate components or floor roots.");

            ValidateGeneration(generator);
            ValidateFailureAndExpansion(generator);
            ValidateMissingLegacyNodesAreReplaced(generator, assembler.ConfiguredRoomPrefab);
            ValidatePrefabAndAssembly(assembler);
            Debug.Log("Phase F-5 verification passed: deterministic independent seeds, 6-8 room Random Growth, stable IDs, connectivity, required rooms, boss distance, directional slot binding, state restoration, repeated encounter prefabs, bounded failures, 8-12 configuration expansion, and idempotent Setup are valid.");
        }

        private static void ValidateGeneration(FloorGenerator generator)
        {
            const int seed = Week8RandomRoomSetup.FixedVerificationSeed;
            Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
            Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
            Assert(Signature(first) == Signature(repeated), "The same seed must reproduce all three floors exactly.");

            bool foundDifferent = false; int directionMasks = 0;
            for (int candidateSeed = seed + 1; candidateSeed < seed + 129; candidateSeed++)
            {
                Assert(generator.TryGenerateForSeed(candidateSeed, out GeneratedFloorGraph candidate, out error), error);
                foundDifferent |= Signature(candidate) != Signature(first);
                directionMasks |= CollectDirectionCombinations(candidate);
            }
            Assert(foundDifferent, "At least one different seed must change topology, role, or content.");
            const int requiredMasks = (1 << 5) | (1 << 10) | (1 << 3) | (1 << 12);
            Assert((directionMasks & requiredMasks) == requiredMasks,
                "Seed coverage must produce left+up, right+down, left+right, and up+down door combinations.");

            foreach (GeneratedFloor floor in first.Floors)
            {
                Assert(floor.Nodes.Count >= 6 && floor.Nodes.Count <= 8, $"Floor {floor.FloorNumber} is outside 6-8 rooms.");
                Assert(floor.FloorSeed != floor.TopologySeed && floor.TopologySeed != floor.ContentSeed,
                    $"Floor {floor.FloorNumber} seed streams must be independently derived.");
                int starts = 0, bosses = 0, treasures = 0;
                foreach (GeneratedRoomNode node in floor.Nodes)
                {
                    Assert(node.RoomId == FloorGenerator.BuildRoomId(node.FloorNumber, node.RoomNumber), "Room ID depends on mutable generation data.");
                    starts += node.Role == GeneratedRoomRole.Start ? 1 : 0;
                    bosses += node.Role == GeneratedRoomRole.Boss ? 1 : 0;
                    treasures += node.Role == GeneratedRoomRole.Treasure ? 1 : 0;
                }
                Assert(starts == 1 && bosses == 1 && treasures >= 1, $"Floor {floor.FloorNumber} lacks required room roles.");
            }
        }

        private static void ValidateFailureAndExpansion(FloorGenerator configured)
        {
            GameObject holder = new("Phase F-5 Generator Boundaries");
            try
            {
                FloorGenerator probe = holder.AddComponent<FloorGenerator>();
                RoomDefinition[] definitions = CopyDefinitions(configured);
                probe.Configure(1, 6, 6, 99, 2, definitions);
                Assert(!probe.TryGenerateForSeed(7711, out _, out string error) &&
                       error.Contains("7711", StringComparison.Ordinal) &&
                       error.Contains("2 attempts", StringComparison.Ordinal) &&
                       error.Contains("distance 99", StringComparison.Ordinal),
                    "Bounded generation failure must include the seed, retry limit, and failed invariant.");
                probe.Configure(1, 8, 12, 3, 32, definitions);
                Assert(probe.TryGenerateForSeed(8822, out GeneratedFloorGraph expanded, out error), error);
                Assert(expanded.Nodes.Count >= 8 && expanded.Nodes.Count <= 12,
                    "Changing settings alone must support an 8-12 room contract.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static void ValidatePrefabAndAssembly(RoomGraphAssembler assembler)
        {
            Assert(assembler.ConfiguredRoomPrefab.TryValidate(out string error), error);
            Assert(assembler.Generator.TryGenerateForSeed(Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph generated, out error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(Week8RandomRoomSetup.FixedVerificationSeed, out error), error);
            GeneratedFloor floor = generated.FindFloor(1);
            Dictionary<string, GeneratedRoomNode> generatedNodes = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in floor.Nodes) generatedNodes.Add(node.RoomId, node);

            Assert(assembler.Graph.Nodes.Count == floor.Nodes.Count, "Only the current floor must be assembled.");
            int activeRooms = 0;
            RoomNode treasureNode = null;
            foreach (RoomNode node in assembler.Graph.Nodes)
            {
                GeneratedRoomNode generatedNode = generatedNodes[node.RoomId];
                RoomPrefab instance = node.GetComponent<RoomPrefab>();
                Assert(instance != null && instance.TryValidate(out error), error);
                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                {
                    RoomDoorSlot slot = instance.FindSlot(direction);
                    bool connected = generatedNode.TryGetConnection(direction, out GeneratedRoomConnection connection);
                    Assert(slot.IsConnected == connected && slot.Seal.activeSelf != connected && slot.Blocker.gameObject.activeSelf == connected,
                        $"{node.RoomId} {direction} slot does not match generated connectivity.");
                    if (connected)
                    {
                        Assert(slot.Doorway.Destination.RoomId == connection.DestinationRoomId &&
                               slot.Doorway.DestinationEntryPoint == slot.Doorway.Destination.GetComponent<RoomPrefab>()
                                   .FindSlot(GeneratedFloorGraph.Opposite(direction)).EntryPoint,
                            $"{node.RoomId} {direction} must target the opposite destination entry point.");
                    }
                }
                RoomController controller = instance.Controller;
                for (int i = 0; i < controller.EnemyPrefabs.Count; i++)
                    Assert(controller.EnemyPrefabs[i] == generatedNode.Definition.EncounterPrefabs[i % generatedNode.Definition.EncounterPrefabs.Count],
                        "Multiple spawn points must intentionally repeat the selected definition prefab sequence.");
                if (node.IsVisible) activeRooms++;
                if (generatedNode.Role == GeneratedRoomRole.Treasure) treasureNode = node;
            }

            Assert(assembler.Graph.TryReplaceFloor(ToArray(assembler.Graph.Nodes),
                FindNode(assembler.Graph.Nodes, floor.StartingRoomId), assembler.Graph.Player, out error), error);
            activeRooms = 0; foreach (RoomNode node in assembler.Graph.Nodes) if (node.IsVisible) activeRooms++;
            Assert(activeRooms == 1, "Exactly one current room must be active after floor entry.");
            Assert(assembler.Graph.Player == null ||
                   ((Vector2)assembler.Graph.Player.transform.position -
                    (Vector2)assembler.Graph.CurrentNode.DefaultEntryPoint.position).sqrMagnitude < 0.0001f,
                "Initial floor assembly must place the Player at the starting room entry point so Player projectiles are visible in the active room.");

            Assert(treasureNode != null, "Floor 1 needs a treasure room for state restoration verification.");
            RoomRunState state = assembler.Progress.GetRoomState(treasureNode.RoomId);
            state.MarkCleared(); state.MarkArtifactClaimed();
            Assert(assembler.TryLoadFloor(2, null, out error) && assembler.Graph.Nodes.Count == generated.FindFloor(2).Nodes.Count, error);
            Assert(assembler.TryLoadFloor(1, null, out error), error);
            RoomNode restored = FindNode(assembler.Graph.Nodes, treasureNode.RoomId);
            RoomPrefab restoredPrefab = restored.GetComponent<RoomPrefab>();
            Assert(restoredPrefab.Controller.State == RoomState.Cleared && restoredPrefab.RewardRoom.HasRewarded,
                "Clear and artifact state must survive floor unloading and reassembly without rerolling.");
            Assert(CountGeneratedFloorRoots(assembler.transform) == 1, "Floor switching must clean the previous floor instance.");
        }

        private static void ValidateMissingLegacyNodesAreReplaced(
            FloorGenerator generator,
            RoomPrefab prefab)
        {
            GameObject holder = new("Phase F-5 Missing Legacy Node Regression");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                RoomGraphController graph = holder.AddComponent<RoomGraphController>();
                graph.Configure(new RoomNode[] { null }, null, null, null, progress);
                RoomGraphAssembler assembler = holder.AddComponent<RoomGraphAssembler>();
                assembler.Configure(generator, graph, progress, prefab);

                Assert(assembler.TryApplyGeneratedGraphForVerification(
                    Week8RandomRoomSetup.FixedVerificationSeed,
                    out string error),
                    $"F-5 must replace stale legacy RoomNode references before validating the new graph. {error}");
                Assert(graph.TryValidateConfiguration(out error), error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static int CollectDirectionCombinations(GeneratedFloorGraph graph)
        {
            int result = 0;
            foreach (GeneratedRoomNode node in graph.Nodes)
            {
                int mask = 0; foreach (GeneratedRoomConnection connection in node.DirectionalConnections) mask |= 1 << (int)connection.Direction;
                result |= 1 << mask;
            }
            return result;
        }
        private static string Signature(GeneratedFloorGraph graph)
        {
            StringBuilder value = new();
            foreach (GeneratedFloor floor in graph.Floors)
            {
                value.Append(floor.FloorSeed).Append('/').Append(floor.TopologySeed).Append('/').Append(floor.ContentSeed).Append('|');
                foreach (GeneratedRoomNode node in floor.Nodes)
                {
                    value.Append(node.RoomId).Append('@').Append(node.GridPosition).Append(':').Append(node.Role).Append(':')
                        .Append(node.ContentSeed).Append(':').Append(node.Definition.RoomDefinitionId);
                    foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                        value.Append('>').Append(connection.Direction).Append('=').Append(connection.DestinationRoomId);
                    value.Append('|');
                }
            }
            return value.ToString();
        }
        private static RoomDefinition[] CopyDefinitions(FloorGenerator generator)
        { RoomDefinition[] result = new RoomDefinition[generator.RoomDefinitions.Count];
          for (int i = 0; i < result.Length; i++) result[i] = generator.RoomDefinitions[i]; return result; }
        private static RoomNode[] ToArray(IReadOnlyList<RoomNode> nodes)
        { RoomNode[] result = new RoomNode[nodes.Count]; for (int i = 0; i < result.Length; i++) result[i] = nodes[i]; return result; }
        private static RoomNode FindNode(IReadOnlyList<RoomNode> nodes, string id)
        { foreach (RoomNode node in nodes) if (node.RoomId == id) return node; return null; }
        private static int CountGeneratedFloorRoots(Transform parent)
        { int count = 0; for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name.StartsWith("Generated Floor ", StringComparison.Ordinal)) count++; return count; }
        private static int CountComponents<T>(GameObject target) where T : Component => target.GetComponents<T>().Length;
        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
