using System;
using System.Collections.Generic;
using System.Text;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week8RandomRoomVerification
    {
        private const int FloorCount = 3;
        private const int RoomsPerFloor = 3;

        [MenuItem("Trickal Fan Game/Verify Phase F-1 Random Room Graph")]
        public static void Verify()
        {
            FloorGenerator generator = FindConfiguredGenerator();
            ValidateDefinitionAssets(generator);
            ValidateDeterministicGraph(generator);
            ValidateInvalidInputs(generator);
            Debug.Log(
                "Phase F-1 random room graph verification passed: definition assets, fixed-seed determinism, " +
                "stable unique room IDs, reachable floor chains, room roles, and invalid-input failures are valid.");
        }

        private static FloorGenerator FindConfiguredGenerator()
        {
            GameObject generatorObject = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName);
            FloorGenerator generator = generatorObject != null
                ? generatorObject.GetComponent<FloorGenerator>()
                : null;
            Assert(generator != null,
                "Missing Phase F Floor Generator. Run Setup Phase F-1 Random Room Definitions first.");
            Assert(generator.Seed == Week8RandomRoomSetup.FixedVerificationSeed &&
                   generator.FloorCount == FloorCount && generator.RoomsPerFloor == RoomsPerFloor,
                "Phase F Floor Generator settings drifted from the verified three-floor configuration.");
            return generator;
        }

        private static void ValidateDefinitionAssets(FloorGenerator generator)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            HashSet<RoomType> types = new();
            Assert(generator.RoomDefinitions.Count == 5,
                "Phase F-1 setup must produce exactly five reusable room definitions.");
            foreach (RoomDefinition definition in generator.RoomDefinitions)
            {
                Assert(definition != null, "Phase F-1 contains a missing room definition asset.");
                Assert(definition.TryValidate(out string error), error);
                Assert(ids.Add(definition.RoomDefinitionId),
                    $"Duplicate room definition ID '{definition.RoomDefinitionId}'.");
                Assert(AssetDatabase.GetAssetPath(definition).StartsWith(
                        Week8RandomRoomSetup.DefinitionFolder + "/",
                        StringComparison.Ordinal),
                    $"Room definition {definition.RoomDefinitionId} is outside the Phase F definition folder.");
                types.Add(definition.RoomType);
            }

            Assert(types.SetEquals(new[] { RoomType.Normal, RoomType.Reward, RoomType.Boss }),
                "Room definitions must cover normal, reward, and boss types.");
        }

        private static void ValidateDeterministicGraph(FloorGenerator generator)
        {
            Assert(generator.TryGenerate(out GeneratedFloorGraph first, out string error), error);
            Assert(generator.TryGenerate(out GeneratedFloorGraph second, out error), error);
            Assert(first.TryValidate(out error), error);
            Assert(BuildSignature(first) == BuildSignature(second),
                "The same fixed seed and definitions must generate the same graph.");
            Assert(first.Nodes.Count == FloorCount * RoomsPerFloor &&
                   first.StartingRoomId == "floor-01-room-01",
                "The generated graph must contain three rooms on each of three floors and start at floor 1.");

            Dictionary<(int Floor, int Room), GeneratedRoomNode> byAddress = new();
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in first.Nodes)
            {
                Assert(ids.Add(node.RoomId), $"Generated room ID '{node.RoomId}' is duplicated.");
                Assert(node.RoomId == FloorGenerator.BuildRoomId(node.FloorNumber, node.RoomNumber),
                    $"Generated room {node.FloorNumber}/{node.RoomNumber} has unstable ID '{node.RoomId}'.");
                Assert(byAddress.TryAdd((node.FloorNumber, node.RoomNumber), node),
                    $"Generated address {node.FloorNumber}/{node.RoomNumber} is duplicated.");

                if (node.RoomNumber == 1)
                {
                    Assert(node.Role == GeneratedRoomRole.Start && node.Definition.RoomType == RoomType.Normal,
                        $"{node.RoomId} must be a normal start room.");
                }
                else if (node.RoomNumber == RoomsPerFloor)
                {
                    Assert(node.Role == GeneratedRoomRole.Boss && node.Definition.RoomType == RoomType.Boss,
                        $"{node.RoomId} must be a boss room.");
                }
                else
                {
                    Assert(node.Role == GeneratedRoomRole.Intermediate &&
                           (node.Definition.RoomType == RoomType.Normal ||
                            node.Definition.RoomType == RoomType.Reward),
                        $"{node.RoomId} must use a validated normal or reward definition.");
                }
            }

            for (int floor = 1; floor <= FloorCount; floor++)
            {
                GeneratedRoomNode start = byAddress[(floor, 1)];
                GeneratedRoomNode middle = byAddress[(floor, 2)];
                GeneratedRoomNode boss = byAddress[(floor, 3)];
                Assert(Contains(start, middle.RoomId) && Contains(middle, start.RoomId) &&
                       Contains(middle, boss.RoomId) && Contains(boss, middle.RoomId),
                    $"Floor {floor} must have a reciprocal start -> middle -> boss chain.");

                if (floor < FloorCount)
                {
                    Assert(Contains(boss, byAddress[(floor + 1, 1)].RoomId),
                        $"Floor {floor} boss must lead to floor {floor + 1} start.");
                }
            }
        }

        private static void ValidateInvalidInputs(FloorGenerator configuredGenerator)
        {
            GameObject holder = new("Phase F Invalid Generator Verification");
            FloorGenerator invalidGenerator = holder.AddComponent<FloorGenerator>();
            try
            {
                invalidGenerator.Configure(1, 1, 2, CopyDefinitions(configuredGenerator));
                Assert(!invalidGenerator.TryGenerate(out _, out string error) &&
                       error.Contains("at least three", StringComparison.Ordinal),
                    "A floor with fewer than three rooms must fail explicitly.");

                RoomDefinition[] duplicated = CopyDefinitions(configuredGenerator);
                RoomDefinition duplicate = ScriptableObject.CreateInstance<RoomDefinition>();
                duplicate.Configure(
                    duplicated[0].RoomDefinitionId,
                    duplicated[0].RoomType,
                    CopyPrefabs(duplicated[0]));
                duplicated[duplicated.Length - 1] = duplicate;
                invalidGenerator.Configure(1, 1, 3, duplicated);
                Assert(!invalidGenerator.TryGenerate(out _, out error) &&
                       error.Contains("duplicated", StringComparison.Ordinal),
                    "Duplicate room definition IDs must fail explicitly.");
                UnityEngine.Object.DestroyImmediate(duplicate);

                GeneratedRoomNode disconnectedStart = new(
                    "floor-01-room-01", 1, 1, GeneratedRoomRole.Start, configuredGenerator.RoomDefinitions[0]);
                GeneratedRoomNode disconnectedBoss = new(
                    "floor-01-room-02", 1, 2, GeneratedRoomRole.Boss,
                    FindDefinition(configuredGenerator, RoomType.Boss));
                GeneratedFloorGraph disconnected = new(
                    new[] { disconnectedStart, disconnectedBoss }, disconnectedStart.RoomId);
                Assert(!disconnected.TryValidate(out error) &&
                       error.Contains("disconnected", StringComparison.Ordinal),
                    "A disconnected generated graph must fail validation explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static RoomDefinition FindDefinition(FloorGenerator generator, RoomType type)
        {
            foreach (RoomDefinition definition in generator.RoomDefinitions)
            {
                if (definition.RoomType == type)
                {
                    return definition;
                }
            }

            throw new InvalidOperationException($"Missing {type} room definition.");
        }

        private static RoomDefinition[] CopyDefinitions(FloorGenerator generator)
        {
            RoomDefinition[] definitions = new RoomDefinition[generator.RoomDefinitions.Count];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = generator.RoomDefinitions[index];
            }

            return definitions;
        }

        private static GameObject[] CopyPrefabs(RoomDefinition definition)
        {
            GameObject[] prefabs = new GameObject[definition.EncounterPrefabs.Count];
            for (int index = 0; index < prefabs.Length; index++)
            {
                prefabs[index] = definition.EncounterPrefabs[index];
            }

            return prefabs;
        }

        private static bool Contains(GeneratedRoomNode node, string roomId)
        {
            foreach (string connectedRoomId in node.ConnectedRoomIds)
            {
                if (connectedRoomId == roomId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSignature(GeneratedFloorGraph graph)
        {
            StringBuilder signature = new();
            foreach (GeneratedRoomNode node in graph.Nodes)
            {
                signature.Append(node.RoomId).Append(':')
                    .Append(node.Definition.RoomDefinitionId).Append(':');
                foreach (string connection in node.ConnectedRoomIds)
                {
                    signature.Append(connection).Append(',');
                }

                signature.Append('|');
            }

            return signature.ToString();
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
