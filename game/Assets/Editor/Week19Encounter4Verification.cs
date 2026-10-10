using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Encounter4Verification
    {
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        private static readonly RoomDoorDirection[] Directions =
        {
            RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down,
        };

        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Encounter-4 Expanded Encounters")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week19Encounter4Setup.Setup();
            string[] tracked = Week19Encounter4Setup.CreatedAssetPaths()
                .Append(Week19Difficulty1Setup.TablePath).ToArray();
            Dictionary<string, string> guids = tracked.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Dictionary<string, int> spawnCounts = SpawnTransformCounts();
            Week19Encounter4Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Encounter-4 setup changed the Game Scene GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Encounter-4 setup changed or lost the GUID for {entry.Key}.");
            }

            foreach (KeyValuePair<string, int> entry in SpawnTransformCounts())
            {
                Assert(spawnCounts[entry.Key] == entry.Value,
                    $"Re-running Encounter-4 setup duplicated SpawnPoint transforms in '{entry.Key}'.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Encounter-4 Expanded Encounters")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week19Encounter4Setup.EncounterContentVersion,
                "Run Encounter-4 setup before verification.");

            RoomDifficultyTable table = generator.DifficultyTable;
            ValidateLimits(table);
            ValidateSpawnPoints(generator);
            ValidateDefinitions(generator, table);
            ValidateInvalidSizes(generator, table);
            ValidatePlacementForEveryDoorSet(generator);
            ValidateGeneration(generator, table, out int swarmSeed);
            ValidateRuntimeBinding(assembler, swarmSeed);
        }

        private static Dictionary<string, int> SpawnTransformCounts()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                .GetComponent<FloorGenerator>();
            return Week19Encounter4Setup.AddedSpawnPoints.Keys.ToDictionary(id => id, id =>
            {
                RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                    candidate != null && candidate.TemplateId == id);
                RoomPrefab room = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
                Transform parent = room.Controller.SpawnPoints[0].parent;
                return parent.Cast<Transform>().Count(child => child.name.StartsWith("Spawn ", StringComparison.Ordinal));
            }, StringComparer.Ordinal);
        }

        private static void ValidateLimits(RoomDifficultyTable table)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.MinimumRoomEnemies == 2 && table.MaximumRoomEnemies == 7 &&
                   table.MaximumWaveEnemies == 4 && table.MaximumWaveRearFiring == 3,
                "Encounter size limits must be 2~7 per room, 4 per wave, and 3 rear firing per wave.");
        }

        private static void ValidateSpawnPoints(FloorGenerator generator)
        {
            Dictionary<string, int> expectedCounts = new(StringComparer.Ordinal)
            {
                [Week14Room3Setup.SmallProfileId] = 4,
                [Week14Room1Setup.BasicProfileId] = 5,
                [Week14Room3Setup.WideProfileId] = 5,
                [Week14Room6Setup.TallProfileId] = 5,
                [Week14Room6Setup.LargeProfileId] = 6,
            };
            // T7: a room from the Layout importer authors all of its SpawnPoints in its text file, so it has no
            // entry in the expansion table. Its count and roles are checked here and by the importer verification.
            RoomTemplateDefinition[] imported = generator.RoomTemplates
                .Where(RoomLayoutImporter.IsImportedTemplate).ToArray();
            foreach (RoomTemplateDefinition template in imported)
            {
                Assert(template.TryValidate(out string error), error);
                Assert(template.TryValidateLayout(out error), error);
                Assert(template.SpawnPoints.Count == expectedCounts[template.Profile.ProfileId] &&
                       template.SpawnPointRoles.SequenceEqual(RoomLayoutImporter.BuildSpawnPointRoles(
                           RoomLayoutFormat.FindProfile(template.Profile.ProfileId), template.SpawnPoints)),
                    $"Imported template '{template.TemplateId}' must have " +
                    $"{expectedCounts[template.Profile.ProfileId]} SpawnPoints with the importer's roles.");
            }

            RoomTemplateDefinition[] normal = generator.RoomTemplates
                .Where(template => template != null && template.SupportsRoomType(RoomType.Normal))
                .Except(imported).ToArray();
            Assert(normal.Select(template => template.TemplateId).OrderBy(id => id, StringComparer.Ordinal)
                    .SequenceEqual(Week19Encounter4Setup.AddedSpawnPoints.Keys.OrderBy(id => id, StringComparer.Ordinal)),
                "Every authored Normal Room Template must have an Encounter-4 SpawnPoint expansion.");
            foreach (RoomTemplateDefinition template in normal)
            {
                Week19Encounter4Setup.AddedSpawnPoint[] added =
                    Week19Encounter4Setup.AddedSpawnPoints[template.TemplateId];
                Assert(template.TryValidate(out string error), error);
                Assert(template.TryValidateLayout(out error), error);
                Assert(template.SpawnPoints.Count == expectedCounts[template.Profile.ProfileId],
                    $"Template '{template.TemplateId}' must have {expectedCounts[template.Profile.ProfileId]} SpawnPoints.");

                Vector2[] basePoints = template.SpawnPoints.Take(Week19Encounter4Setup.BaseSpawnPointCount).ToArray();
                Assert(template.SpawnPointRoles.Take(Week19Encounter4Setup.BaseSpawnPointCount)
                        .SequenceEqual(RoomTemplateDefinition.BuildDefaultSpawnPointRoles(basePoints)),
                    $"Template '{template.TemplateId}' changed the roles of SpawnPoints 1~3.");
                Week18Obstacle2Setup.LayoutSpec obstacle = Week18Obstacle2Setup.Layouts.SingleOrDefault(layout =>
                    layout.TemplateId == template.TemplateId);
                Assert(obstacle == null || basePoints.SequenceEqual(obstacle.SpawnPoints),
                    $"Obstacle Layout '{template.TemplateId}' changed its authored SpawnPoints 1~3.");
                for (int index = 0; index < added.Length; index++)
                {
                    int pointIndex = Week19Encounter4Setup.BaseSpawnPointCount + index;
                    Assert(template.SpawnPoints[pointIndex] == added[index].Position &&
                           template.SpawnPointRoles[pointIndex] == added[index].Role &&
                           (added[index].Role & SpawnPointPlacementRole.RearFiring) != 0,
                        $"Template '{template.TemplateId}' SpawnPoint {pointIndex + 1} does not match Encounter-4.");
                }
            }
        }

        private static void ValidateDefinitions(FloorGenerator generator, RoomDifficultyTable table)
        {
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidateThreat(table, out string error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
            }

            foreach (Week19Encounter4Setup.SwarmSpec spec in Week19Encounter4Setup.Swarms)
            {
                EncounterDefinition swarm = Find(generator, Week19Spawn2Setup.EncounterId(spec.ProfileId, "swarm"));
                Assert(swarm.Waves.Count == 2 &&
                       swarm.Waves[0].SpawnRules.Sum(rule => rule.Count) == spec.FirstWave &&
                       swarm.Waves[1].SpawnRules.Sum(rule => rule.Count) == spec.SecondWave &&
                       swarm.Waves.SelectMany(wave => wave.SpawnRules).SelectMany(rule => rule.Candidates)
                           .All(candidate => candidate.EnemyRole == EncounterEnemyRole.Chaser ||
                                             candidate.EnemyRole == EncounterEnemyRole.FastChaser),
                    $"Swarm '{swarm.EncounterId}' must hold {spec.FirstWave}+{spec.SecondWave} melee enemies.");
                Assert(table.TryComputeThreatRange(swarm, out int minimum, out int maximum, out string error) &&
                       minimum == spec.MinimumThreat && maximum == spec.MaximumThreat &&
                       swarm.DeclaredMinimumThreat == minimum && swarm.DeclaredMaximumThreat == maximum,
                    $"Swarm '{swarm.EncounterId}' must declare threat {spec.MinimumThreat}~{spec.MaximumThreat}. {error}");

                EncounterDefinition elite = Find(generator, Week19Spawn2Setup.EncounterId(spec.ProfileId, "elite-pair"));
                EncounterEnemyRole[] eliteRoles = elite.Waves.SelectMany(wave => wave.SpawnRules)
                    .SelectMany(rule => Enumerable.Repeat(rule.EnemyRole, rule.Count)).ToArray();
                Assert(elite.Waves.Count == 1 && eliteRoles.Length == 2 &&
                       eliteRoles.Contains(EncounterEnemyRole.Sniper) && eliteRoles.Contains(EncounterEnemyRole.Charging) &&
                       elite.DeclaredMinimumThreat == Week19Encounter4Setup.ElitePairThreat &&
                       elite.DeclaredMaximumThreat == Week19Encounter4Setup.ElitePairThreat,
                    $"Elite pair '{elite.EncounterId}' must be one sniper and one charger with threat 8.");
            }
        }

        private static void ValidateInvalidSizes(FloorGenerator generator, RoomDifficultyTable table)
        {
            RoomProfile profile = generator.RoomTemplates.First(template =>
                template.SupportsRoomType(RoomType.Normal)).Profile;
            EncounterEnemyCandidate[] melee = { new(EncounterEnemyRole.Chaser, 1) };
            EncounterEnemyCandidate[] rear = { new(EncounterEnemyRole.Ranged, 1) };
            (string label, string expected, EncounterSpawnRule[][] waves)[] cases =
            {
                ("single enemy", "outside the room range", new[] { new[] { Rule(melee, 1) } }),
                ("eight enemies", "outside the room range",
                    new[] { new[] { Rule(melee, 4) }, new[] { Rule(melee, 4) } }),
                ("five in one wave", "per wave", new[] { new[] { Rule(melee, 5) } }),
                ("four rear firing in one wave", "rear firing", new[] { new[] { Rule(rear, 4) } }),
            };
            foreach ((string label, string expected, EncounterSpawnRule[][] waves) in cases)
            {
                EncounterDefinition invalid = ScriptableObject.CreateInstance<EncounterDefinition>();
                try
                {
                    invalid.Configure($"spawn-invalid-size-{cases.ToList().FindIndex(entry => entry.label == label)}",
                        new[] { profile }, 1, 3, 1.5f, 2f,
                        waves.Select((rules, index) => new EncounterWaveDefinition(index + 1,
                            index == 0 ? EncounterWaveStartCondition.RoomEntered
                                : EncounterWaveStartCondition.PreviousWaveCleared,
                            EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, rules)).ToArray(),
                        EncounterClearCondition.AllWavesCleared);
                    Assert(table.TryComputeThreatRange(invalid, out int minimum, out int maximum, out string error),
                        error);
                    invalid.ConfigureDeclaredThreat(minimum, maximum);
                    Assert(!table.TryValidateEncounterSize(invalid, out error) &&
                           error.Contains(expected, StringComparison.Ordinal),
                        $"Encounter size validation must reject a {label}. {error}");
                    Assert(generator.TryGenerateForSeed(1, out GeneratedFloorGraph graph, out error), error);
                    Assert(!EncounterSelector.TryAssign(graph, generator.EncounterDefinitions.Append(invalid).ToArray(),
                               generator.EncounterContentVersion, table, out error) &&
                           error.Contains(expected, StringComparison.Ordinal),
                        $"Encounter selection must stop explicitly for a {label}. {error}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(invalid);
                }
            }
        }

        // The new Encounters must be placeable whatever doors a generated room opens.
        private static void ValidatePlacementForEveryDoorSet(FloorGenerator generator)
        {
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(template =>
                         template != null && template.SupportsRoomType(RoomType.Normal)))
            {
                string profileId = template.Profile.ProfileId;
                EncounterDefinition swarm = Find(generator, Week19Spawn2Setup.EncounterId(profileId, "swarm"));
                EncounterDefinition elite = Find(generator, Week19Spawn2Setup.EncounterId(profileId, "elite-pair"));
                for (int mask = 1; mask < 16; mask++)
                {
                    GeneratedRoomConnection[] connections = Directions
                        .Where((_, index) => (mask & (1 << index)) != 0)
                        .Select(direction => new GeneratedRoomConnection(direction, "verification")).ToArray();
                    for (int floor = 1; floor <= 3; floor++)
                    {
                        Assert(swarm.TryValidateFor(template, floor, connections, out string error),
                            $"Swarm cannot be placed in '{template.TemplateId}' with doors {mask}. {error}");
                        Assert(elite.TryValidateFor(template, floor, connections, out error),
                            $"Elite pair cannot be placed in '{template.TemplateId}' with doors {mask}. {error}");
                    }
                }
            }
        }

        private static void ValidateGeneration(FloorGenerator generator, RoomDifficultyTable table, out int swarmSeed)
        {
            swarmSeed = 0;
            Dictionary<string, int> patternCounts = new(StringComparer.Ordinal);
            int[] enemyCountHistogram = new int[8];
            int smallRooms = 0, smallCrossfireCapable = 0;
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat),
                        $"Seed {seed} changed its Encounter or spawn selection on regeneration.");
                }

                foreach (GeneratedRoomNode room in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
                {
                    int total = room.ResolvedEncounterWaves.Sum(wave => wave.Length);
                    Assert(total >= 2 && total <= 7 && room.ResolvedEncounterWaves.All(wave => wave.Length <= 4),
                        $"Seed {seed} room '{room.RoomId}' resolved {total} enemies outside 2~7 or over 4 per wave.");
                    Assert(room.ResolvedEncounterWaves.All(wave =>
                            wave.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == wave.Length &&
                            wave.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role))),
                        $"Seed {seed} room '{room.RoomId}' reused or mismatched a SpawnPoint.");
                    Assert(room.HasDifficulty && table.Classify(room.Difficulty.Score) == room.Difficulty.Tier,
                        $"Seed {seed} room '{room.RoomId}' lost its room difficulty.");
                    enemyCountHistogram[total]++;
                    string pattern = Pattern(room.EncounterId);
                    patternCounts[pattern] = patternCounts.TryGetValue(pattern, out int count) ? count + 1 : 1;
                    if (pattern == "swarm" && swarmSeed == 0 && room.FloorNumber == 1) swarmSeed = seed;
                    if (room.Template.Profile.ProfileId != Week14Room3Setup.SmallProfileId) continue;
                    smallRooms++;
                    if (Find(generator, Week19Spawn2Setup.EncounterId(Week14Room3Setup.SmallProfileId, "crossfire"))
                        .TryValidateFor(room.Template, room.FloorNumber, room.DirectionalConnections, out _))
                        smallCrossfireCapable++;
                }
            }

            foreach (string pattern in new[] { "pressure", "crossfire", "swarm", "elite-pair" })
            {
                Assert(patternCounts.TryGetValue(pattern, out int count) && count > 0,
                    $"{GeneratedSeedCount} seeds must select the '{pattern}' Encounters.");
            }

            Assert(enemyCountHistogram[2] > 0 && enemyCountHistogram[6] + enemyCountHistogram[7] > 0,
                "Generated rooms must include both few-strong (2) and many-weak (6~7) Encounters.");
            Assert(swarmSeed > 0, "No floor-1 swarm room was generated for runtime verification.");
            Debug.Log("Encounter-4 verification passed: SpawnPoints Small 4 / Basic, Wide, Tall 5 / Large 6 keep " +
                      "points 1~3, swarm and elite pairs place under all 15 door sets, size limits 2~7 / 4 per wave / " +
                      $"3 rear firing reject invalid Encounters; {GeneratedSeedCount} seeds pattern counts " +
                      string.Join(", ", patternCounts.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}")) +
                      "; room enemy counts " + string.Join(", ", Enumerable.Range(2, 6)
                          .Select(count => $"{count}:{enemyCountHistogram[count]}")) +
                      $"; Small crossfire-capable rooms {smallCrossfireCapable}/{smallRooms}; runtime seed {swarmSeed} " +
                      "binds stored waves; setup preserves GUIDs and SpawnPoint transforms.");
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            Dictionary<string, GeneratedRoomNode> generated = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            int checkedSwarm = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!generated.TryGetValue(room.Node.RoomId, out GeneratedRoomNode node)) continue;
                Assert(room.Controller.EncounterWaves.Count == node.ResolvedEncounterWaves.Count,
                    $"Runtime room '{node.RoomId}' wave count differs from its stored selection.");
                for (int waveIndex = 0; waveIndex < node.ResolvedEncounterWaves.Count; waveIndex++)
                {
                    ResolvedEncounterSpawn[] stored = node.ResolvedEncounterWaves[waveIndex];
                    EncounterRuntimeWave runtime = room.Controller.EncounterWaves[waveIndex];
                    Assert(runtime.EnemyPrefabs.Count == stored.Length && runtime.SpawnPoints.Count == stored.Length,
                        $"Runtime room '{node.RoomId}' wave {waveIndex + 1} lost enemies.");
                    for (int index = 0; index < stored.Length; index++)
                    {
                        Assert(assembler.EnemyRoster.TryResolve(stored[index].Role, out GameObject expected, out error) &&
                               runtime.EnemyPrefabs[index] == expected &&
                               ((Vector2)runtime.SpawnPoints[index].localPosition -
                                node.Template.SpawnPoints[stored[index].SpawnPointIndex]).sqrMagnitude < 0.0001f,
                            $"Runtime room '{node.RoomId}' rebound stored spawn {index + 1}.");
                    }
                }

                if (Pattern(node.EncounterId) == "swarm") checkedSwarm++;
            }

            Assert(checkedSwarm > 0, $"Runtime seed {seed} did not bind a swarm room.");
        }

        private static string Pattern(string encounterId)
        {
            foreach (string pattern in new[] { "elite-pair", "crossfire", "pressure", "swarm" })
                if (encounterId.EndsWith($"-{pattern}-v1", StringComparison.Ordinal)) return pattern;
            return encounterId;
        }

        private static EncounterDefinition Find(FloorGenerator generator, string id)
        {
            EncounterDefinition definition = generator.EncounterDefinitions.SingleOrDefault(candidate =>
                candidate != null && candidate.EncounterId == id);
            Assert(definition != null, $"Encounter-4 catalog is missing '{id}'.");
            return definition;
        }

        private static EncounterSpawnRule Rule(EncounterEnemyCandidate[] candidates, int count) =>
            new(candidates, count, null, "all");

        private static string Signature(GeneratedFloorGraph graph) =>
            string.Join("|", graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .Select(node => $"{node.RoomId}:{node.EncounterId}:" + string.Join("/",
                    node.ResolvedEncounterWaves.Select(wave =>
                        string.Join(",", wave.Select(spawn => $"{spawn.Role}@{spawn.SpawnPointIndex}"))))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
