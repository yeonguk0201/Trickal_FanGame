using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter1Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Encounter-1 Contract")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.EncounterDefinitions.Count == 3 &&
                   generator.EncounterContentVersion == Week14Encounter1Setup.ContentVersion,
                "Run Encounter-1 Setup before verification.");
            EncounterDefinition[] definitions = generator.EncounterDefinitions.ToArray();
            Assert(EncounterContractCatalog.TryValidate(definitions, out string error), error);
            ValidateAssets(definitions);
            ValidateDeterminism(generator);
            ValidateCompatibilityAndCoverage(generator);
            ValidateFailurePaths(generator, definitions);
            Week14Room5Verification.Verify();
            Debug.Log("Week 14 Encounter-1 verification passed: stable IDs, profiles, floors, enemy roles/counts, " +
                      "SpawnPoint/group references, entry/door safety distances, wave/clear conditions, seeded " +
                      "selection, catalog-order independence, failure paths, and Room-0~5 regressions are valid.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Encounter1Setup.Setup();
            string[] encounterGuids = Paths().Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week14Encounter1Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) &&
                   encounterGuids.SequenceEqual(Paths().Select(AssetDatabase.AssetPathToGUID)),
                "Encounter-1 Setup changed the Game Scene or Encounter asset GUIDs.");
            Verify();
        }

        private static void ValidateAssets(IReadOnlyList<EncounterDefinition> definitions)
        {
            HashSet<EncounterEnemyRole> roles = new();
            foreach (EncounterDefinition definition in definitions)
            {
                Assert(definition.MinimumFloor == 1 && definition.MaximumFloor == 3 &&
                       Mathf.Approximately(definition.MinimumPlayerDistance, 1.5f) &&
                       Mathf.Approximately(definition.MinimumDoorDistance, 2f) &&
                       definition.AllowedProfiles.Count == 3 && definition.Waves.Count == 1 &&
                       definition.ClearCondition == EncounterClearCondition.AllWavesCleared,
                    $"Encounter '{definition.EncounterId}' contract values drifted.");
                EncounterWaveDefinition wave = definition.Waves[0];
                Assert(wave.WaveNumber == 1 && wave.StartCondition == EncounterWaveStartCondition.RoomEntered &&
                       wave.CompletionCondition == EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated &&
                       wave.SpawnRules.Count == 1 && wave.SpawnRules[0].Count == 1 &&
                       wave.SpawnRules[0].SpawnGroupId == "all" &&
                       string.IsNullOrEmpty(wave.SpawnRules[0].SpawnPointId),
                    $"Encounter '{definition.EncounterId}' wave contract drifted.");
                roles.Add(wave.SpawnRules[0].EnemyRole);
            }
            Assert(roles.SetEquals(new[]
                { EncounterEnemyRole.Chaser, EncounterEnemyRole.Ranged, EncounterEnemyRole.Charging }),
                "Encounter-1 assets must cover the three existing normal enemy roles without owning Prefabs.");
        }

        private static void ValidateDeterminism(FloorGenerator configured)
        {
            Assert(configured.TryGenerateForSeed(20260915, out GeneratedFloorGraph first, out string error), error);
            Assert(configured.TryGenerateForSeed(20260915, out GeneratedFloorGraph second, out error), error);
            Assert(Signature(first) == Signature(second), "Same seed/version must select the same Encounter IDs.");

            GameObject holder = new("Encounter-1 Order Verification");
            try
            {
                FloorGenerator reversed = holder.AddComponent<FloorGenerator>();
                reversed.Configure(configured.FloorCount, configured.MinimumRoomsPerFloor,
                    configured.MaximumRoomsPerFloor, configured.MinimumBossDistance,
                    configured.GenerationRetryLimit, configured.RoomDefinitions.ToArray());
                reversed.ConfigureTemplates(configured.RoomContentVersion, configured.RoomTemplates.ToArray());
                reversed.ConfigureEncounters(configured.EncounterContentVersion,
                    configured.EncounterDefinitions.Reverse().ToArray());
                Assert(reversed.TryGenerateForSeed(20260915, out GeneratedFloorGraph actual, out error), error);
                Assert(Signature(first) == Signature(actual),
                    "Encounter selection must not depend on serialized catalog order.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static void ValidateCompatibilityAndCoverage(FloorGenerator generator)
        {
            HashSet<string> selected = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 128; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (node.Role != GeneratedRoomRole.Intermediate)
                    {
                        Assert(node.Encounter == null, $"Non-combat room {node.RoomId} received an Encounter.");
                        continue;
                    }
                    bool compatible = node.Encounter != null && node.Encounter.TryValidateFor(
                        node.Template, node.FloorNumber, out error);
                    Assert(compatible,
                        $"Room {node.RoomId} received an incompatible Encounter. {error}");
                    selected.Add(node.EncounterId);
                }
            }
            Assert(selected.SetEquals(generator.EncounterDefinitions.Select(definition => definition.EncounterId)),
                "128 seeds must exercise every Encounter-1 definition.");
        }

        private static void ValidateFailurePaths(FloorGenerator generator,
            IReadOnlyList<EncounterDefinition> validDefinitions)
        {
            RoomProfile basic = AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room1Setup.BasicProfilePath);
            RoomTemplateDefinition basicTemplate = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(
                Week14Room1Setup.BasicTemplatePath);
            EncounterDefinition invalidPoint = CreateTemporary("invalid-point", new[] { basic }, 1, 3, 1.5f, 2f,
                new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, "spawn-99", null));
            EncounterDefinition unsafeDistance = CreateTemporary("unsafe-distance", new[] { basic }, 1, 3, 100f, 2f,
                new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, "spawn-01", null));
            EncounterDefinition wrongFloor = CreateTemporary("floor-three-only", new[] { basic }, 3, 3, 1.5f, 2f,
                new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, "spawn-01", null));
            try
            {
                Assert(!invalidPoint.TryValidateFor(basicTemplate, 1, out string error) &&
                       error.Contains("spawn-99", StringComparison.Ordinal),
                    "Missing SpawnPoint IDs must fail explicitly.");
                Assert(!unsafeDistance.TryValidateFor(basicTemplate, 1, out error) &&
                       error.Contains("safety distance", StringComparison.Ordinal),
                    "Entry-proximate spawns must fail safety validation.");
                Assert(!wrongFloor.TryValidateFor(basicTemplate, 1, out error) &&
                       error.Contains("floor 1", StringComparison.Ordinal),
                    "Unsupported floors must fail explicitly.");
                Assert(!EncounterContractCatalog.TryValidate(
                           new[] { validDefinitions[0], validDefinitions[0] }, out error) &&
                       error.Contains("duplicates Encounter ID", StringComparison.Ordinal),
                    "Duplicate Encounter IDs must fail explicitly.");

                Assert(generator.TryGenerateForSeed(1, out GeneratedFloorGraph graph, out error), error);
                Assert(!EncounterSelector.TryAssign(graph, new[] { wrongFloor }, 1, out error) &&
                       error.Contains("no Encounter compatible", StringComparison.Ordinal),
                    "A catalog without a floor/profile-compatible Encounter must fail explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidPoint);
                UnityEngine.Object.DestroyImmediate(unsafeDistance);
                UnityEngine.Object.DestroyImmediate(wrongFloor);
            }
        }

        private static EncounterDefinition CreateTemporary(string id, RoomProfile[] profiles, int minimumFloor,
            int maximumFloor, float playerDistance, float doorDistance, EncounterSpawnRule rule)
        {
            EncounterDefinition definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            definition.Configure(id, profiles, minimumFloor, maximumFloor, playerDistance, doorDistance,
                new[] { new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                    EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[] { rule }) },
                EncounterClearCondition.AllWavesCleared);
            return definition;
        }

        private static string Signature(GeneratedFloorGraph graph)
        {
            StringBuilder result = new();
            foreach (GeneratedRoomNode node in graph.Nodes.OrderBy(node => node.RoomId, StringComparer.Ordinal))
                if (node.Role == GeneratedRoomRole.Intermediate)
                    result.Append(node.RoomId).Append(':').Append(node.EncounterId).Append('|');
            return result.ToString();
        }

        private static string[] Paths() => new[]
        {
            Week14Encounter1Setup.EncounterFolder + "/solo-chaser.asset",
            Week14Encounter1Setup.EncounterFolder + "/solo-ranged.asset",
            Week14Encounter1Setup.EncounterFolder + "/solo-charging.asset",
        };

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
