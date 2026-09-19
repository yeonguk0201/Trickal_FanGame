using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room6Verification
    {
        private static readonly Vector2 GridSpacing =
            new(RoomLayout.RoomSpacingX, RoomLayout.RoomSpacingY);

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-6 Tall Large and Boss Profiles")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            string[] baselineTemplateIds =
            {
                Week14Room1Setup.BasicTemplateId,
                Week14Room3Setup.SmallTemplateId,
                Week14Room3Setup.WideTemplateId,
                Week14Room6Setup.TallTemplateId,
                Week14Room6Setup.LargeTemplateId,
                Week14Room6Setup.BossFloor1TemplateId,
                Week14Room6Setup.BossFloor2TemplateId,
                Week14Room6Setup.BossFloor3TemplateId,
            };
            Assert(generator != null && generator.RoomContentVersion >= Week14Room6Setup.ContentVersion &&
                   baselineTemplateIds.All(id => generator.RoomTemplates.Any(template =>
                       template.TemplateId == id)),
                "Run Room-6 Setup before verification.");

            RoomTemplateDefinition[] templates = generator.RoomTemplates.ToArray();
            RoomProfile[] profiles = templates.Select(template => template.Profile).Distinct().ToArray();
            Assert(RoomContractCatalog.TryValidate(profiles, templates, out string error), error);
            ValidateExpandedProfilesAndTemplates(templates);
            ValidateFloorRangeFailure(templates.Single(template =>
                template.TemplateId == Week14Room6Setup.TallTemplateId));
            ValidateSelectionDeterminismCoverageAndSpacing(generator);
            ValidateEncounterCompatibility(generator);
            Week14Encounter3Verification.Verify();
            Debug.Log("Week 14 Room-6 verification passed: Tall tracks vertically, Large tracks both axes, " +
                      "floor 1/2/3 boss candidates use basic/tall/large profiles only on their floors, expanded " +
                      "layouts remain deterministic and non-overlapping, and Encounter-3 plus Room-0~5 regressions pass.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            string basicPrefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            Week14Room6Setup.Setup();
            Dictionary<string, string> guids = Week14Room6Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week14Room6Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) &&
                   basicPrefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath),
                "Room-6 Setup changed the Game Scene or Basic Prefab GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Room-6 Setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        private static void ValidateExpandedProfilesAndTemplates(
            IReadOnlyList<RoomTemplateDefinition> templates)
        {
            ValidateTemplate(templates, Week14Room6Setup.TallTemplateId,
                Week14Room6Setup.TallProfileId, Week14Room6Setup.TallSize,
                RoomCameraTrackingMode.Vertical, RoomType.Normal, 1, 99);
            ValidateTemplate(templates, Week14Room6Setup.LargeTemplateId,
                Week14Room6Setup.LargeProfileId, Week14Room6Setup.LargeSize,
                RoomCameraTrackingMode.Both, RoomType.Normal, 1, 99);
            ValidateTemplate(templates, Week14Room6Setup.BossFloor1TemplateId,
                Week14Room6Setup.BossFloor1ProfileId, RoomLayout.RoomSize,
                RoomCameraTrackingMode.Fixed, RoomType.Boss, 1, 1);
            ValidateTemplate(templates, Week14Room6Setup.BossFloor2TemplateId,
                Week14Room6Setup.BossFloor2ProfileId, Week14Room6Setup.TallSize,
                RoomCameraTrackingMode.Vertical, RoomType.Boss, 2, 2);
            ValidateTemplate(templates, Week14Room6Setup.BossFloor3TemplateId,
                Week14Room6Setup.BossFloor3ProfileId, Week14Room6Setup.LargeSize,
                RoomCameraTrackingMode.Both, RoomType.Boss, 3, 3);
        }

        private static void ValidateTemplate(IReadOnlyList<RoomTemplateDefinition> templates,
            string templateId, string profileId, Vector2 size, RoomCameraTrackingMode cameraMode,
            RoomType roomType, int minimumFloor, int maximumFloor)
        {
            RoomTemplateDefinition template = templates.Single(candidate =>
                candidate.TemplateId == templateId);
            Assert(template.Profile.ProfileId == profileId &&
                   Approximately(template.Profile.InteriorSize, size) &&
                   template.AllowedRoomTypes.Count == 1 && template.AllowedRoomTypes[0] == roomType &&
                   template.MinimumFloor == minimumFloor && template.MaximumFloor == maximumFloor,
                $"Room-6 template '{templateId}' has an incorrect profile, size, type, or floor range.");
            Assert(template.Profile.TryCalculateCameraFrame(RoomCameraFraming.DesignAspectRatio,
                       out RoomCameraFrame frame, out string error), error);
            Assert(frame.TrackingMode == cameraMode &&
                   Approximately(frame.CenterBounds, template.Profile.CameraBounds),
                $"Room-6 template '{templateId}' has incorrect camera framing.");
            Assert(template.TryValidate(out error), error);
        }

        private static void ValidateFloorRangeFailure(RoomTemplateDefinition source)
        {
            RoomTemplateDefinition invalid = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            try
            {
                invalid.Configure("invalid-floor-range", source.Profile, source.RoomPrefabAsset,
                    source.AllowedRoomTypes.ToArray(), source.DoorSlots.ToArray(),
                    source.SpawnPoints.ToArray(), 3, 2);
                Assert(!invalid.TryValidate(out string error) && error.Contains("invalid floor range"),
                    "A Room Template with a reversed floor range must fail explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        private static void ValidateSelectionDeterminismCoverageAndSpacing(FloorGenerator generator)
        {
            HashSet<string> normalCoverage = new(StringComparer.Ordinal);
            HashSet<string> bossCoverage = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 512; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph second, out error), error);
                Assert(Signature(first) == Signature(second),
                    "Room-6 template selection must be deterministic for the same seed and version.");
                AssertNoOverlap(first);
                foreach (GeneratedRoomNode node in first.Nodes)
                {
                    Assert(node.Template.SupportsFloor(node.FloorNumber),
                        $"Room {node.RoomId} received template '{node.TemplateId}' outside its floor range.");
                    if (node.Role == GeneratedRoomRole.Intermediate)
                        normalCoverage.Add(node.TemplateId);
                    if (node.Role == GeneratedRoomRole.Boss)
                    {
                        bossCoverage.Add(node.TemplateId);
                        string expected = node.FloorNumber switch
                        {
                            1 => Week14Room6Setup.BossFloor1TemplateId,
                            2 => Week14Room6Setup.BossFloor2TemplateId,
                            _ => Week14Room6Setup.BossFloor3TemplateId,
                        };
                        Assert(node.TemplateId == Week14Room1Setup.BasicTemplateId || node.TemplateId == expected,
                            $"Floor {node.FloorNumber} boss received another floor's boss template.");
                    }
                }
            }

            Assert(normalCoverage.Contains(Week14Room6Setup.TallTemplateId) &&
                   normalCoverage.Contains(Week14Room6Setup.LargeTemplateId),
                "512 seeds must exercise both Tall and Large normal room templates.");
            Assert(bossCoverage.Contains(Week14Room6Setup.BossFloor1TemplateId) &&
                   bossCoverage.Contains(Week14Room6Setup.BossFloor2TemplateId) &&
                   bossCoverage.Contains(Week14Room6Setup.BossFloor3TemplateId),
                "512 seeds must exercise every floor-specific boss room candidate.");
        }

        private static void ValidateEncounterCompatibility(FloorGenerator generator)
        {
            bool sawTall = false;
            bool sawLarge = false;
            for (int seed = 1; seed <= 256 && (!sawTall || !sawLarge); seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes.Where(node => node.Encounter != null))
                {
                    if (node.TemplateId == Week14Room6Setup.TallTemplateId) sawTall = true;
                    if (node.TemplateId == Week14Room6Setup.LargeTemplateId) sawLarge = true;
                    Assert(node.Encounter.Supports(node.Template.Profile, node.FloorNumber),
                        $"Encounter '{node.EncounterId}' does not support expanded profile '{node.Template.Profile.ProfileId}'.");
                    for (int waveIndex = 0; waveIndex < node.Encounter.Waves.Count; waveIndex++)
                        Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                            node.DirectionalConnections, waveIndex, out _, out error), error);
                }
            }
            Assert(sawTall && sawLarge,
                "Expanded Tall and Large rooms must both resolve actual Encounter waves.");
        }

        private static void AssertNoOverlap(GeneratedFloorGraph graph)
        {
            foreach (GeneratedFloor floor in graph.Floors)
            {
                for (int firstIndex = 0; firstIndex < floor.Nodes.Count; firstIndex++)
                for (int secondIndex = firstIndex + 1; secondIndex < floor.Nodes.Count; secondIndex++)
                {
                    GeneratedRoomNode first = floor.Nodes[firstIndex];
                    GeneratedRoomNode second = floor.Nodes[secondIndex];
                    Assert(!RoomTemplateSelector.LayoutsOverlap(first.GridPosition,
                            first.Template.Profile.InteriorSize, second.GridPosition,
                            second.Template.Profile.InteriorSize, GridSpacing),
                        $"Room-6 layouts {first.RoomId}/{first.TemplateId} and " +
                        $"{second.RoomId}/{second.TemplateId} overlap.");
                }
            }
        }

        private static string Signature(GeneratedFloorGraph graph)
        {
            StringBuilder result = new();
            foreach (GeneratedRoomNode node in graph.Nodes.OrderBy(node => node.RoomId, StringComparer.Ordinal))
                result.Append(node.RoomId).Append(':').Append(node.TemplateId).Append('|');
            return result.ToString();
        }

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static bool Approximately(Rect first, Rect second) =>
            Approximately(first.position, second.position) && Approximately(first.size, second.size);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
