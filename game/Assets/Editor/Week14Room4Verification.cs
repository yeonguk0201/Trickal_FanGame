using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room4Verification
    {
        private static readonly Vector2 GridSpacing =
            new(RoomLayout.RoomSpacingX, RoomLayout.RoomSpacingY);

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-4 Seeded Template Selection")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Run Room-4 Setup before verification.");
            Assert(generator.RoomContentVersion >= Week14Room4Setup.ContentVersion &&
                   generator.RoomTemplates.Any(template => template.TemplateId == Week14Room1Setup.BasicTemplateId) &&
                   generator.RoomTemplates.Any(template => template.TemplateId == Week14Room3Setup.SmallTemplateId) &&
                   generator.RoomTemplates.Any(template => template.TemplateId == Week14Room3Setup.WideTemplateId),
                "The template catalog must retain all three Room-4 baseline templates.");

            ValidateDeterminism(generator);
            ValidateCatalogOrderIndependence(generator);
            ValidateStartingRoomTemplate(generator);
            ValidateCompatibilityAndCoverage(generator);
            ValidateSpacingRules(generator);
            ValidateContentVersion(generator);
            ValidateMissingRoomTypeFailure(generator);
            Week14Room3Verification.Verify();

            Debug.Log(
                "Week 14 Room-4 verification passed: connection directions, RoomType and AABB size " +
                "compatibility filter candidates; the same seed/content version is deterministic and catalog-order " +
                "independent; every floor starts in Small while normal rooms retain varied templates; " +
                "Wide-Wide horizontal overlap is prevented; " +
                "missing compatible types fail explicitly; and Room-0~3 regressions remain valid.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            string basicPrefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            Week14Room4Setup.Setup();
            Week14Room4Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) &&
                   basicPrefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath),
                "Running Room-4 Setup changed the Game Scene or Basic Prefab GUID.");
            Verify();
        }

        private static void ValidateDeterminism(FloorGenerator generator)
        {
            Assert(generator.TryGenerateForSeed(
                Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph first,
                out string error), error);
            Assert(generator.TryGenerateForSeed(
                Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph second,
                out error), error);
            Assert(BuildTemplateSignature(first) == BuildTemplateSignature(second),
                "The same Run seed and content version must select the same template IDs.");
            Assert(first.Nodes.All(node => node.Template != null),
                "Every generated room must receive a Room-4 template.");
        }

        private static void ValidateCatalogOrderIndependence(FloorGenerator configured)
        {
            GameObject holder = new("Room-4 Catalog Order Verification");
            try
            {
                FloorGenerator reversed = holder.AddComponent<FloorGenerator>();
                reversed.Configure(
                    configured.FloorCount,
                    configured.MinimumRoomsPerFloor,
                    configured.MaximumRoomsPerFloor,
                    configured.MinimumBossDistance,
                    configured.GenerationRetryLimit,
                    CopyDefinitions(configured));
                reversed.ConfigureTemplates(
                    configured.RoomContentVersion,
                    configured.RoomTemplates.Reverse().ToArray());
                Assert(configured.TryGenerateForSeed(
                    Week8RandomRoomSetup.FixedVerificationSeed,
                    out GeneratedFloorGraph expected,
                    out string error), error);
                Assert(reversed.TryGenerateForSeed(
                    Week8RandomRoomSetup.FixedVerificationSeed,
                    out GeneratedFloorGraph actual,
                    out error), error);
                Assert(BuildTemplateSignature(expected) == BuildTemplateSignature(actual),
                    "Template selection must not depend on serialized catalog order.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateCompatibilityAndCoverage(FloorGenerator generator)
        {
            HashSet<string> selectedNormalTemplates = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 128; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                Assert(graph.TryValidate(out error), error);
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    Assert(node.Template != null && node.Template.SupportsRoomType(node.RoomType),
                        $"Room {node.RoomId} received a template incompatible with {node.RoomType}.");
                    Assert(node.Template.SupportsConnections(node.DirectionalConnections),
                        $"Room {node.RoomId} received a template missing a required connection direction.");
                    if (node.RoomType == RoomType.Normal)
                    {
                        selectedNormalTemplates.Add(node.TemplateId);
                    }
                    else if (node.RoomType == RoomType.Reward)
                    {
                        Assert(node.TemplateId == Week14Room1Setup.BasicTemplateId,
                            $"Reward room {node.RoomId} must retain the Basic Room-4 fallback.");
                    }
                }

                AssertNoLayoutOverlap(graph);
            }

            Assert(new[]
                {
                    Week14Room3Setup.SmallTemplateId,
                    Week14Room1Setup.BasicTemplateId,
                    Week14Room3Setup.WideTemplateId,
                }.All(selectedNormalTemplates.Contains),
                "Seeded normal rooms must retain coverage of Small, Basic, and Wide templates.");
        }

        private static void ValidateStartingRoomTemplate(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 128; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedFloor floor in graph.Floors)
                {
                    GeneratedRoomNode startingRoom = floor.Nodes
                        .Single(node => node.RoomId == floor.StartingRoomId);
                    Assert(startingRoom.Role == GeneratedRoomRole.Start &&
                           startingRoom.TemplateId == RoomTemplateSelector.StartingRoomTemplateId,
                        $"Floor {floor.FloorNumber} must start with the " +
                        $"{RoomTemplateSelector.StartingRoomTemplateId} template.");
                }
            }

            GameObject holder = new("Room-4 Missing Start Template Verification");
            try
            {
                FloorGenerator invalid = holder.AddComponent<FloorGenerator>();
                invalid.Configure(
                    generator.FloorCount,
                    generator.MinimumRoomsPerFloor,
                    generator.MaximumRoomsPerFloor,
                    generator.MinimumBossDistance,
                    generator.GenerationRetryLimit,
                    CopyDefinitions(generator));
                invalid.ConfigureTemplates(
                    generator.RoomContentVersion,
                    generator.RoomTemplates
                        .Where(template => template.TemplateId != RoomTemplateSelector.StartingRoomTemplateId)
                        .ToArray());
                Assert(!invalid.TryGenerateForSeed(
                           Week8RandomRoomSetup.FixedVerificationSeed,
                           out _,
                           out string error) &&
                       error.Contains("requires compatible template", StringComparison.Ordinal),
                    "A template catalog without the fixed Small starting room must fail explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateSpacingRules(FloorGenerator generator)
        {
            Assert(RoomTemplateSelector.LayoutsOverlap(
                    new RoomGridPosition(0, 0),
                    Week14Room3Setup.WideSize,
                    new RoomGridPosition(1, 0),
                    Week14Room3Setup.WideSize,
                    GridSpacing),
                "Two horizontally adjacent Wide rooms must be recognized as overlapping at 20-unit spacing.");
            Assert(!RoomTemplateSelector.LayoutsOverlap(
                    new RoomGridPosition(0, 0),
                    Week14Room3Setup.WideSize,
                    new RoomGridPosition(1, 0),
                    Week14Room3Setup.SmallSize,
                    GridSpacing),
                "A horizontally adjacent Wide and Small room should fit at 20-unit spacing.");
            Assert(!RoomTemplateSelector.LayoutsOverlap(
                    new RoomGridPosition(0, 0),
                    Week14Room3Setup.WideSize,
                    new RoomGridPosition(0, 1),
                    Week14Room3Setup.WideSize,
                    GridSpacing),
                "Vertically adjacent Wide rooms should fit at 13-unit spacing.");

            Assert(generator.TryGenerateForSeed(
                Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph graph,
                out string error), error);
            AssertNoLayoutOverlap(graph);
        }

        private static void ValidateContentVersion(FloorGenerator configured)
        {
            GameObject holder = new("Room-4 Content Version Verification");
            try
            {
                FloorGenerator alternate = holder.AddComponent<FloorGenerator>();
                alternate.Configure(
                    configured.FloorCount,
                    configured.MinimumRoomsPerFloor,
                    configured.MaximumRoomsPerFloor,
                    configured.MinimumBossDistance,
                    configured.GenerationRetryLimit,
                    CopyDefinitions(configured));
                alternate.ConfigureTemplates(
                    configured.RoomContentVersion + 1,
                    configured.RoomTemplates.ToArray());

                bool foundVersionDifference = false;
                for (int seed = 1; seed <= 64 && !foundVersionDifference; seed++)
                {
                    Assert(configured.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                    Assert(alternate.TryGenerateForSeed(seed, out GeneratedFloorGraph second, out error), error);
                    foundVersionDifference = BuildTemplateSignature(first) != BuildTemplateSignature(second);
                }

                Assert(foundVersionDifference,
                    "Changing the content version must be able to change deterministic template selection.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateMissingRoomTypeFailure(FloorGenerator configured)
        {
            GameObject holder = new("Room-4 Missing RoomType Verification");
            try
            {
                FloorGenerator invalid = holder.AddComponent<FloorGenerator>();
                invalid.Configure(
                    configured.FloorCount,
                    configured.MinimumRoomsPerFloor,
                    configured.MaximumRoomsPerFloor,
                    configured.MinimumBossDistance,
                    configured.GenerationRetryLimit,
                    CopyDefinitions(configured));
                RoomTemplateDefinition[] normalOnly = configured.RoomTemplates
                    .Where(template => template.TemplateId != Week14Room1Setup.BasicTemplateId)
                    .ToArray();
                invalid.ConfigureTemplates(configured.RoomContentVersion, normalOnly);
                Assert(!invalid.TryGenerateForSeed(
                           Week8RandomRoomSetup.FixedVerificationSeed,
                           out _,
                           out string error) &&
                       error.Contains("no template compatible with RoomType", StringComparison.Ordinal),
                    "A catalog without Reward/Boss compatibility must fail explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void AssertNoLayoutOverlap(GeneratedFloorGraph graph)
        {
            foreach (GeneratedFloor floor in graph.Floors)
            {
                for (int firstIndex = 0; firstIndex < floor.Nodes.Count; firstIndex++)
                {
                    GeneratedRoomNode first = floor.Nodes[firstIndex];
                    for (int secondIndex = firstIndex + 1; secondIndex < floor.Nodes.Count; secondIndex++)
                    {
                        GeneratedRoomNode second = floor.Nodes[secondIndex];
                        Assert(!RoomTemplateSelector.LayoutsOverlap(
                                first.GridPosition,
                                first.Template.Profile.InteriorSize,
                                second.GridPosition,
                                second.Template.Profile.InteriorSize,
                                GridSpacing),
                            $"Generated layouts {first.RoomId}/{first.TemplateId} and " +
                            $"{second.RoomId}/{second.TemplateId} overlap at {GridSpacing} spacing.");
                    }
                }
            }
        }

        private static RoomDefinition[] CopyDefinitions(FloorGenerator generator) =>
            generator.RoomDefinitions.ToArray();

        private static string BuildTemplateSignature(GeneratedFloorGraph graph)
        {
            StringBuilder signature = new();
            foreach (GeneratedFloor floor in graph.Floors.OrderBy(candidate => candidate.FloorNumber))
            {
                foreach (GeneratedRoomNode node in floor.Nodes.OrderBy(candidate => candidate.RoomNumber))
                {
                    signature.Append(node.RoomId).Append(':').Append(node.TemplateId).Append('|');
                }
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
