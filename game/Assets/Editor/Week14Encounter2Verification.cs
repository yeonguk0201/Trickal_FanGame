using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter2Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Encounter-2 Mixed Encounters")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null,
                "Run Encounter-2 Setup before verification.");
            Assert(generator.EncounterContentVersion == Week14Encounter2Setup.ContentVersion &&
                   generator.EncounterDefinitions.Count == 3,
                "Encounter-2 catalog or content version is not configured.");

            ValidateRoster(assembler.EnemyRoster);
            ValidateDefinitions(generator.EncounterDefinitions);
            ValidateSelectionAndSafety(generator);
            ValidateRuntimeBinding(assembler);
            ValidateFailurePaths(assembler.EnemyRoster);
            Week14Room5Verification.Verify();
            Debug.Log("Week 14 Encounter-2 verification passed: three mixed compositions select " +
                      "deterministically, resolve unique active-door-safe SpawnPoints, bind the correct verified " +
                      "enemy Prefabs, instantiate every role, reject invalid rosters, and preserve Room-0~5 regressions.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Encounter2Setup.Setup();
            string[] guids = Paths().Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week14Encounter2Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) &&
                   guids.SequenceEqual(Paths().Select(AssetDatabase.AssetPathToGUID)),
                "Encounter-2 Setup changed the Game Scene, roster, or Encounter asset GUIDs.");
            Verify();
        }

        private static void ValidateRoster(EncounterEnemyRoster roster)
        {
            Assert(roster.TryValidate(out string error), error);
            Assert(roster.Bindings.Count == 3, "Encounter-2 roster must contain exactly three normal roles.");
            AssertPrefab(roster, EncounterEnemyRole.Chaser, Week14Encounter2Setup.ChaserPrefabPath,
                typeof(EnemyChase));
            AssertPrefab(roster, EncounterEnemyRole.Ranged, Week14Encounter2Setup.RangedPrefabPath,
                typeof(RangedEnemyController));
            AssertPrefab(roster, EncounterEnemyRole.Charging, Week14Encounter2Setup.ChargingPrefabPath,
                typeof(ChargingEnemyController));
        }

        private static void ValidateDefinitions(IReadOnlyList<EncounterDefinition> definitions)
        {
            Assert(EncounterContractCatalog.TryValidate(definitions, out string error), error);
            Dictionary<string, EncounterDefinition> byId = definitions.ToDictionary(
                definition => definition.EncounterId, StringComparer.Ordinal);
            Assert(byId.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(new[]
                { "pressure-chaser-ranged", "lane-charging-ranged", "crossfire-ranged" }),
                "Encounter-2 catalog must contain the three approved mixed compositions.");
            AssertRoles(byId["pressure-chaser-ranged"], EncounterEnemyRole.Chaser, EncounterEnemyRole.Ranged);
            AssertRoles(byId["lane-charging-ranged"], EncounterEnemyRole.Charging, EncounterEnemyRole.Ranged);
            AssertRoles(byId["crossfire-ranged"], EncounterEnemyRole.Ranged,
                EncounterEnemyRole.Ranged, EncounterEnemyRole.Ranged);
            foreach (EncounterDefinition definition in definitions)
                Assert(definition.MinimumFloor == 1 && definition.MaximumFloor == 3 &&
                       definition.AllowedProfiles.Count == 3 && definition.Waves.Count == 1,
                    $"Encounter '{definition.EncounterId}' profile, floor, or wave contract drifted.");
        }

        private static void ValidateSelectionAndSafety(FloorGenerator generator)
        {
            HashSet<string> selected = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 256; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes.Where(node =>
                             node.Role == GeneratedRoomRole.Intermediate))
                {
                    Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                            node.DirectionalConnections, 0, out ResolvedEncounterSpawn[] spawns, out error), error);
                    Assert(spawns.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == spawns.Length,
                        $"Encounter '{node.EncounterId}' reused a SpawnPoint in {node.RoomId}.");
                    ValidateSafety(node, spawns);
                    if (node.EncounterId == "pressure-chaser-ranged")
                    {
                        ResolvedEncounterSpawn chaser = spawns.Single(spawn => spawn.Role == EncounterEnemyRole.Chaser);
                        ResolvedEncounterSpawn ranged = spawns.Single(spawn => spawn.Role == EncounterEnemyRole.Ranged);
                        Assert(node.Template.SpawnPoints[chaser.SpawnPointIndex].sqrMagnitude <=
                               node.Template.SpawnPoints[ranged.SpawnPointIndex].sqrMagnitude + 0.0001f,
                            "Pressure Encounter must keep the ranged enemy at least as far out as the chaser.");
                    }
                    selected.Add(node.EncounterId);
                }
            }
            Assert(selected.SetEquals(generator.EncounterDefinitions.Select(definition => definition.EncounterId)),
                "256 seeds must exercise all three Encounter-2 compositions.");
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler)
        {
            int seed = FindSeedWithAllEncountersOnFirstFloor(assembler.Generator);
            Assert(seed > 0, "Could not find a first-floor seed containing all Encounter-2 compositions.");
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            Dictionary<string, GeneratedRoomNode> generated = assembler.GeneratedGraph.FindFloor(1).Nodes
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            Dictionary<string, RoomPrefab> instances = assembler.CurrentFloorRoot
                .GetComponentsInChildren<RoomPrefab>(true)
                .ToDictionary(instance => instance.Node.RoomId, StringComparer.Ordinal);
            Health playerHealth = assembler.Graph.Player.GetComponent<Health>();
            Assert(playerHealth != null && !playerHealth.IsDead,
                "Encounter-2 runtime verification requires the configured living player.");

            foreach (GeneratedRoomNode node in generated.Values.Where(node =>
                         node.Role == GeneratedRoomRole.Intermediate))
            {
                RoomController controller = instances[node.RoomId].Controller;
                Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                        node.DirectionalConnections, 0, out ResolvedEncounterSpawn[] expected, out error), error);
                Assert(controller.TryValidateEncounterConfiguration(out error), error);
                Assert(controller.EnemyPrefabs.Count == expected.Length &&
                       controller.SpawnPoints.Count == expected.Length,
                    $"Room {node.RoomId} did not receive its resolved Encounter size.");
                for (int index = 0; index < expected.Length; index++)
                {
                    Assert(assembler.EnemyRoster.TryResolve(expected[index].Role,
                        out GameObject expectedPrefab, out error), error);
                    Assert(controller.EnemyPrefabs[index] == expectedPrefab &&
                           Approximately(controller.SpawnPoints[index].localPosition,
                               node.Template.SpawnPoints[expected[index].SpawnPointIndex]),
                        $"Room {node.RoomId} role or SpawnPoint binding does not match Encounter '{node.EncounterId}'.");
                }

                List<GameObject> spawned = new();
                controller.EnemySpawned += spawned.Add;
                controller.BeginCombat(playerHealth);
                Assert(spawned.Count == expected.Length && controller.AliveEnemyCount == expected.Length,
                    $"Room {node.RoomId} did not instantiate every Encounter enemy.");
                for (int index = 0; index < spawned.Count; index++)
                    Assert(RoleMatches(spawned[index], expected[index].Role),
                        $"Room {node.RoomId} instantiated the wrong role at SpawnPoint {index + 1}.");
            }
        }

        private static void ValidateFailurePaths(EncounterEnemyRoster validRoster)
        {
            EncounterEnemyRoster duplicate = ScriptableObject.CreateInstance<EncounterEnemyRoster>();
            EncounterEnemyRoster missing = ScriptableObject.CreateInstance<EncounterEnemyRoster>();
            try
            {
                GameObject chaser = validRoster.Bindings.Single(binding =>
                    binding.Role == EncounterEnemyRole.Chaser).Prefab;
                duplicate.Configure(new[]
                {
                    new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, chaser),
                    new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, chaser),
                });
                missing.Configure(new[]
                    { new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, chaser) });
                Assert(!duplicate.TryValidate(out string error) && error.Contains("duplicates role"),
                    "Duplicate role bindings must fail explicitly.");
                Assert(!missing.TryResolve(EncounterEnemyRole.Ranged, out _, out error) &&
                       error.Contains("no Prefab"),
                    "A missing runtime role mapping must fail explicitly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(missing);
            }
        }

        private static void ValidateSafety(GeneratedRoomNode node, IReadOnlyList<ResolvedEncounterSpawn> spawns)
        {
            HashSet<RoomDoorDirection> active = new(node.DirectionalConnections.Select(connection =>
                connection.Direction));
            foreach (ResolvedEncounterSpawn spawn in spawns)
            {
                Vector2 point = node.Template.SpawnPoints[spawn.SpawnPointIndex];
                foreach (RoomTemplateDoor door in node.Template.DoorSlots.Where(door => active.Contains(door.Direction)))
                    Assert(Vector2.Distance(point, door.SafeEntryPosition) + 0.0001f >=
                               node.Encounter.MinimumPlayerDistance &&
                           Vector2.Distance(point, door.SlotPosition) + 0.0001f >=
                               node.Encounter.MinimumDoorDistance,
                        $"Encounter '{node.EncounterId}' placed an enemy too close to active door {door.Direction}.");
            }
        }

        private static int FindSeedWithAllEncountersOnFirstFloor(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 4096; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                HashSet<string> ids = graph.FindFloor(1).Nodes
                    .Where(node => node.Encounter != null)
                    .Select(node => node.EncounterId)
                    .ToHashSet(StringComparer.Ordinal);
                if (ids.Count == 3) return seed;
            }
            return -1;
        }

        private static void AssertRoles(EncounterDefinition definition, params EncounterEnemyRole[] expected)
        {
            EncounterEnemyRole[] actual = definition.Waves[0].SpawnRules
                .SelectMany(rule => Enumerable.Repeat(rule.EnemyRole, rule.Count)).ToArray();
            Assert(actual.SequenceEqual(expected), $"Encounter '{definition.EncounterId}' role order drifted.");
        }

        private static void AssertPrefab(EncounterEnemyRoster roster, EncounterEnemyRole role,
            string path, Type componentType)
        {
            Assert(roster.TryResolve(role, out GameObject prefab, out string error), error);
            Assert(prefab == AssetDatabase.LoadAssetAtPath<GameObject>(path) &&
                   prefab.GetComponent(componentType) != null,
                $"Enemy roster role '{role}' is not bound to the verified Prefab.");
        }

        private static bool RoleMatches(GameObject enemy, EncounterEnemyRole role) => role switch
        {
            EncounterEnemyRole.Chaser => enemy.GetComponent<EnemyChase>() != null,
            EncounterEnemyRole.Ranged => enemy.GetComponent<RangedEnemyController>() != null,
            EncounterEnemyRole.Charging => enemy.GetComponent<ChargingEnemyController>() != null,
            _ => false,
        };

        private static string[] Paths() => new[]
        {
            Week14Encounter2Setup.RosterPath,
            Week14Encounter1Setup.EncounterFolder + "/pressure-chaser-ranged.asset",
            Week14Encounter1Setup.EncounterFolder + "/lane-charging-ranged.asset",
            Week14Encounter1Setup.EncounterFolder + "/crossfire-ranged.asset",
        };

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
