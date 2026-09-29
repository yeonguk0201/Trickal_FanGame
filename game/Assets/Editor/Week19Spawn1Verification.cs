using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Spawn1Verification
    {
        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Spawn-1 Placement Roles")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week19Spawn1Setup.Setup();
            string[] templatePaths = FindGenerator().RoomTemplates
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();
            string[] templateGuids = templatePaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week19Spawn1Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Spawn-1 setup changed the Game Scene GUID.");
            for (int index = 0; index < templatePaths.Length; index++)
            {
                Assert(templateGuids[index] == AssetDatabase.AssetPathToGUID(templatePaths[index]),
                    $"Spawn-1 setup changed or lost the GUID for {templatePaths[index]}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Spawn-1 Placement Roles")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = FindGenerator();
            Assert(generator.EncounterContentVersion >= Week19Spawn1Setup.EncounterContentVersion,
                "Run Spawn-1 setup before verification.");

            RoomTemplateDefinition[] normalTemplates = generator.RoomTemplates
                .Where(template => template != null && template.SupportsRoomType(RoomType.Normal))
                .ToArray();
            Assert(normalTemplates.Length > 0, "Spawn-1 requires Normal room templates.");
            foreach (RoomTemplateDefinition template in normalTemplates)
            {
                Assert(template.TryValidate(out string error), error);
                Assert(template.SpawnPointRoles.Count == template.SpawnPoints.Count,
                    $"Template '{template.TemplateId}' must declare one role mask per SpawnPoint.");
                Assert(template.SpawnPointRoles.Count(role =>
                           (role & SpawnPointPlacementRole.MeleePressure) != 0) >= 2,
                    $"Template '{template.TemplateId}' needs two melee-pressure points for existing melee waves.");
                Assert(template.SpawnPointRoles.All(role =>
                           (role & SpawnPointPlacementRole.RearFiring) != 0),
                    $"Template '{template.TemplateId}' must preserve its three-point ranged crossfire.");
                Assert(template.SpawnPointRoles.Any(role =>
                           (role & SpawnPointPlacementRole.ChargeLane) != 0),
                    $"Template '{template.TemplateId}' needs a charge-lane point.");
            }

            ValidateEnemyRoleMapping();
            ValidateMismatchedPlacementFails(normalTemplates[0]);
            ValidateGeneratedRooms(generator);
            Debug.Log("Spawn-1 verification passed: all Normal layouts own melee-pressure, rear-firing, and " +
                      "charge-lane SpawnPoint roles; chaser variants, ranged variants, and chargers resolve only " +
                      "to compatible points; incompatible group and explicit-point placements fail; 512 seeds " +
                      "retain deterministic, valid Encounter assignments; setup is idempotent and preserves GUIDs.");
        }

        private static void ValidateEnemyRoleMapping()
        {
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Chaser) ==
                   SpawnPointPlacementRole.MeleePressure, "Chaser must require melee pressure.");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.FastChaser) ==
                   SpawnPointPlacementRole.MeleePressure, "Fast chaser must require melee pressure.");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Ranged) ==
                   SpawnPointPlacementRole.RearFiring, "Ranged must require rear firing.");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Sniper) ==
                   SpawnPointPlacementRole.RearFiring, "Sniper must require rear firing.");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Charging) ==
                   SpawnPointPlacementRole.ChargeLane, "Charging must require a charge lane.");
        }

        private static void ValidateMismatchedPlacementFails(RoomTemplateDefinition source)
        {
            RoomTemplateDefinition rangedOnly = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            EncounterDefinition groupMismatch = ScriptableObject.CreateInstance<EncounterDefinition>();
            EncounterDefinition pointMismatch = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                SpawnPointPlacementRole[] roles = Enumerable.Repeat(
                    SpawnPointPlacementRole.RearFiring, source.SpawnPoints.Count).ToArray();
                rangedOnly.Configure("spawn-role-ranged-only", source.Profile, source.RoomPrefabAsset,
                    new[] { RoomType.Reward }, source.DoorSlots.ToArray(), source.SpawnPoints.ToArray(),
                    source.MinimumFloor, source.MaximumFloor, roles);
                Assert(rangedOnly.TryValidate(out string error), error);

                ConfigureVerificationEncounter(groupMismatch, "spawn-role-group-mismatch",
                    source.Profile, new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, null, "all"));
                Assert(!groupMismatch.TryValidateFor(rangedOnly, 1, out error) &&
                       error.Contains("MeleePressure", StringComparison.Ordinal),
                    $"A chaser group must reject ranged-only SpawnPoints. {error}");

                ConfigureVerificationEncounter(pointMismatch, "spawn-role-point-mismatch",
                    source.Profile, new EncounterSpawnRule(EncounterEnemyRole.Charging, 1,
                        RoomTemplateDefinition.SpawnPointId(0), null));
                Assert(!pointMismatch.TryValidateFor(rangedOnly, 1, out error) &&
                       error.Contains("ChargeLane", StringComparison.Ordinal),
                    $"An explicit charger point must reject a non-charge SpawnPoint. {error}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pointMismatch);
                UnityEngine.Object.DestroyImmediate(groupMismatch);
                UnityEngine.Object.DestroyImmediate(rangedOnly);
            }
        }

        private static void ConfigureVerificationEncounter(EncounterDefinition definition, string id,
            RoomProfile profile, EncounterSpawnRule rule)
        {
            definition.Configure(id, new[] { profile }, 1, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                new[]
                {
                    new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[] { rule }),
                }, EncounterClearCondition.AllWavesCleared);
        }

        private static void ValidateGeneratedRooms(FloorGenerator generator)
        {
            int assigned = 0;
            for (int seed = 1; seed <= 512; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                Assert(first.Nodes.Select(node => node.EncounterId)
                        .SequenceEqual(repeat.Nodes.Select(node => node.EncounterId)),
                    $"Seed {seed} changed its Encounter assignment between identical generations.");
                foreach (GeneratedRoomNode node in first.Nodes.Where(node =>
                             node.Role == GeneratedRoomRole.Intermediate))
                {
                    Assert(node.Encounter != null && node.Encounter.TryValidateFor(node.Template,
                               node.FloorNumber, node.DirectionalConnections, out error),
                        $"Seed {seed} room '{node.RoomId}' has an invalid role placement. {error}");
                    for (int waveIndex = 0; waveIndex < node.Encounter.Waves.Count; waveIndex++)
                    {
                        Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                            node.DirectionalConnections, waveIndex, out ResolvedEncounterSpawn[] spawns,
                            out error), error);
                        Assert(spawns.All(spawn => node.Template.SupportsEnemyRole(
                                spawn.SpawnPointIndex, spawn.Role)),
                            $"Seed {seed} room '{node.RoomId}' resolved an enemy to an incompatible point.");
                    }

                    assigned++;
                }
            }

            Assert(assigned > 0, "Spawn-1 verification did not inspect any generated combat rooms.");
        }

        private static FloorGenerator FindGenerator()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Spawn-1 requires the Game Scene FloorGenerator.");
            return generator;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
