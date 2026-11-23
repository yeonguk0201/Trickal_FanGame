using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Density-0 (docs/28 §10.2): checks the per-size limits, floor ranges and tier bands of the difficulty table,
    // every Encounter's waves against Density0Setup.Layout, the generated SpawnPoints of every combat room, and
    // that generated floors stay inside the size limits and classify each room with its own size.
    public static class Density0Verification
    {
        private const int GeneratedSeedCount = 256;
        private const int DeterminismSeedCount = 32;

        private static readonly RoomDoorDirection[] Directions =
        {
            RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down,
        };

        [MenuItem("Trickal Fan Game/Beta/Setup and Verify Density-0 Enemies by Room Size")]
        public static void SetupAndVerifyBatch()
        {
            Density0Setup.Setup();
            Dictionary<string, string> guids = TrackedPaths().ToDictionary(path => path,
                AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Dictionary<string, string> signatures = ContentSignatures();
            Density0Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Density-0 setup changed or lost the GUID for {entry.Key}.");
            }

            foreach (KeyValuePair<string, string> entry in ContentSignatures())
            {
                Assert(signatures.TryGetValue(entry.Key, out string before) && before == entry.Value,
                    $"Re-running Density-0 setup changed '{entry.Key}'.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Beta/Verify Density-0 Enemies by Room Size")]
        public static void Verify()
        {
            FloorGenerator generator = OpenGenerator();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Density0Setup.EncounterContentVersion,
                "Run Density-0 setup before verification.");

            RoomDifficultyTable table = generator.DifficultyTable;
            ValidateTable(table);
            ValidateEncounters(generator, table);
            ValidateInvalidSizes(generator, table);
            ValidateSpawnPoints(generator);
            Dictionary<string, int> largeSeed = ValidateGeneration(generator, table);
            ValidateRuntimeBinding(assembler, largeSeed);
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads Encounter waves, SpawnPoints, room difficulty, the content version or the
            // Layout importer.
            (string Name, Action Run)[] checks =
            {
                ("RoomLayoutImporter", RoomLayoutImporterVerification.Verify),
                ("Week27Variant2", Week27Variant2Verification.Verify),
                ("Week27Variant1", Week27Variant1Verification.Verify),
                ("Week27Variant0", Week27Variant0Verification.Verify),
                ("Week27Enemy13", Week27Enemy13Verification.Verify),
                ("Week27Enemy12", Week27Enemy12Verification.Verify),
                ("Week27Enemy11", Week27Enemy11Verification.Verify),
                ("Week27Enemy10", Week27Enemy10Verification.Verify),
                ("Week27Curve1", Week27Curve1Verification.Verify),
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
                ("Week19Spawn1", Week19Spawn1Verification.Verify),
                ("Week19Spawn2", Week19Spawn2Verification.Verify),
                ("Week19Difficulty1", Week19Difficulty1Verification.Verify),
                ("Week19Encounter4", Week19Encounter4Verification.Verify),
                ("Week14Room8", Week14Room8Verification.Verify),
                ("Week20Floor1", Week20Floor1Verification.Verify),
                ("Week20DevPanel", Week20DevPanelVerification.Verify),
            };
            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                    run();
                    Debug.Log($"[Density-0 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Density-0 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Density-0 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Density-0 regression verification passed.");
        }

        private static FloorGenerator OpenGenerator()
        {
            Week18Obstacle2Setup.OpenGameScene();
            return GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)?.GetComponent<FloorGenerator>();
        }

        private static IEnumerable<string> TrackedPaths()
        {
            FloorGenerator generator = OpenGenerator();
            return Week27Curve1Setup.Patterns
                .SelectMany(pattern => Week19Spawn2Setup.Profiles.Select(profile =>
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
                .Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath)
                .Concat(generator.RoomTemplates.Where(IsCombatTemplate).SelectMany(template => new[]
                {
                    AssetDatabase.GetAssetPath(template), AssetDatabase.GetAssetPath(template.RoomPrefabAsset),
                }));
        }

        // SpawnPoints (with the number of Spawn transforms under the Prefab) and waves, so a re-run that moves,
        // duplicates or reshuffles anything is caught.
        private static Dictionary<string, string> ContentSignatures()
        {
            FloorGenerator generator = OpenGenerator();
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(IsCombatTemplate))
            {
                RoomPrefab room = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
                Transform parent = room.Controller.SpawnPoints[0].parent;
                int transforms = parent.Cast<Transform>()
                    .Count(child => child.name.StartsWith("Spawn ", StringComparison.Ordinal));
                result[template.TemplateId] = transforms + "|" + string.Join(";",
                    template.SpawnPoints.Select((point, index) => $"{point.x},{point.y}:{(int)template.SpawnPointRoles[index]}"));
            }

            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
                result[definition.EncounterId] = WaveSignature(definition);
            result["content-version"] = generator.EncounterContentVersion.ToString();
            return result;
        }

        private static string WaveSignature(EncounterDefinition definition) =>
            $"{definition.DeclaredMinimumThreat}~{definition.DeclaredMaximumThreat}|" + string.Join("/",
                definition.Waves.Select(wave => string.Join(",", wave.SpawnRules.Select(rule =>
                    rule.Count + "x" + string.Join("+", rule.Candidates.Select(candidate =>
                        $"{candidate.EnemyRole}:{candidate.Weight}"))))));

        private static bool IsCombatTemplate(RoomTemplateDefinition template) =>
            template != null && template.SupportsRoomType(RoomType.Normal) && template.Profile != null;

        private static void ValidateTable(RoomDifficultyTable table)
        {
            Assert(table.TryValidate(out string error), error);
            // The table-wide values stay the Encounter-4 / Difficulty-1 ones; they are the small size.
            Assert(table.MinimumRoomEnemies == Week19Encounter4Setup.MinimumRoomEnemies &&
                   table.MaximumRoomEnemies == Week19Encounter4Setup.MaximumRoomEnemies &&
                   table.MaximumWaveEnemies == Week19Encounter4Setup.MaximumWaveEnemies &&
                   table.MaximumWaveRearFiring == Week19Encounter4Setup.MaximumWaveRearFiring &&
                   table.TierBands.SequenceEqual(Week19Difficulty1Setup.TierBands) &&
                   table.FloorRanges.SequenceEqual(Week19Difficulty1Setup.FloorRanges),
                "Density-0 must keep the table-wide limits, tier bands and floor ranges.");

            RoomSizeDifficulty[] expected = Density0Setup.SizeDifficulties();
            Assert(table.SizeDifficulties.Count == expected.Length,
                $"The difficulty table must hold {expected.Length} room sizes.");
            foreach (RoomSizeDifficulty want in expected)
            {
                Assert(table.TryGetSizeDifficulty(want.ProfileId, out RoomSizeDifficulty size) &&
                       size.MinimumRoomEnemies == want.MinimumRoomEnemies &&
                       size.MaximumRoomEnemies == want.MaximumRoomEnemies &&
                       size.MaximumWaveEnemies == want.MaximumWaveEnemies &&
                       size.MaximumWaveRearFiring == want.MaximumWaveRearFiring &&
                       size.TierBands.SequenceEqual(want.TierBands) && size.FloorRanges.SequenceEqual(want.FloorRanges),
                    $"Room size '{want.ProfileId}' does not match Density-0.");
            }

            // docs/28 §10.2.
            (string profile, int wave)[] waves =
            {
                (Week14Room3Setup.SmallProfileId, 4), (Week14Room1Setup.BasicProfileId, 5),
                (Week14Room3Setup.WideProfileId, 6), (Week14Room6Setup.TallProfileId, 6),
                (Week14Room6Setup.LargeProfileId, 8),
            };
            foreach ((string profile, int wave) in waves)
            {
                Assert(table.TryGetEncounterLimits(profile, out _, out _, out int maximumWave, out _) &&
                       maximumWave == wave, $"Room size '{profile}' must hold {wave} enemies per wave.");
            }

            Assert(table.TryGetSizeDifficulty(Week14Room3Setup.SmallProfileId, out RoomSizeDifficulty small) &&
                   small.MaximumRoomEnemies == table.MaximumRoomEnemies &&
                   small.TierBands.SequenceEqual(table.TierBands) && small.FloorRanges.SequenceEqual(table.FloorRanges),
                "The small size must equal the table-wide values.");
            int previous = 0;
            foreach (string profile in new[]
                     {
                         Week14Room3Setup.SmallProfileId, Week14Room1Setup.BasicProfileId,
                         Week14Room3Setup.WideProfileId, Week14Room6Setup.LargeProfileId,
                     })
            {
                table.TryGetFloorRange(1, profile, out FloorDifficultyRange range);
                Assert(range.MaximumScore > previous, $"Room size '{profile}' must allow more threat than the one before.");
                previous = range.MaximumScore;
                Assert(table.Classify(range.MaximumScore, profile) == table.Classify(
                        Week19Difficulty1Setup.FloorRanges[0].MaximumScore),
                    $"Room size '{profile}' must classify its floor-1 maximum like the small size does.");
            }
        }

        private static void ValidateEncounters(FloorGenerator generator, RoomDifficultyTable table)
        {
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidate(out string error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
            }

            // Small keeps its Encounter-4 / Enemy-7 / Enemy-11 totals and threat ranges.
            Dictionary<string, (int total, int minimum, int maximum)> small = new(StringComparer.Ordinal)
            {
                ["pressure"] = (4, Week19Difficulty1Setup.PressureMinimumThreat,
                    Week19Difficulty1Setup.PressureMaximumThreat),
                ["crossfire"] = (5, Week19Difficulty1Setup.CrossfireMinimumThreat,
                    Week19Difficulty1Setup.CrossfireMaximumThreat),
                ["swarm"] = (6, 12, 18),
                ["elite-pair"] = (2, Week19Encounter4Setup.ElitePairThreat, Week19Encounter4Setup.ElitePairThreat),
                ["jyubi-swarm"] = (6, 6, 6),
                ["jyubi-nest"] = (6, Week27Enemy11Setup.MinimumThreat, Week27Enemy11Setup.MaximumThreat),
            };

            foreach (string pattern in Week27Curve1Setup.Patterns)
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                EncounterDefinition definition = Find(generator, Week19Spawn2Setup.EncounterId(spec.ProfileId, pattern));
                Density0Setup.Slot[][] layout = Density0Setup.Layout(spec.ProfileId, pattern);
                Assert(definition.Waves.Count == layout.Length,
                    $"'{definition.EncounterId}' must have {layout.Length} waves.");
                for (int waveIndex = 0; waveIndex < layout.Length; waveIndex++)
                {
                    IReadOnlyList<EncounterSpawnRule> rules = definition.Waves[waveIndex].SpawnRules;
                    Assert(rules.Count == layout[waveIndex].Length,
                        $"'{definition.EncounterId}' wave {waveIndex + 1} has the wrong rules.");
                    for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                    {
                        Density0Setup.Slot slot = layout[waveIndex][ruleIndex];
                        EncounterEnemyCandidate[] expected = Density0Setup.Candidates(slot.Kind, spec);
                        Assert(rules[ruleIndex].Count == slot.Count &&
                               rules[ruleIndex].SpawnGroupId == "all" &&
                               rules[ruleIndex].Candidates.SequenceEqual(expected),
                            $"'{definition.EncounterId}' wave {waveIndex + 1} rule {ruleIndex + 1} must be " +
                            $"{slot.Count} x {slot.Kind}.");
                    }
                }

                Assert(definition.MinimumFloor == Week27Curve1Setup.MinimumFloor(pattern) &&
                       definition.MaximumFloor == Week27Curve1Setup.MaximumFloor,
                    $"'{definition.EncounterId}' lost its Curve-1 floors.");
                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out string threatError) &&
                       definition.DeclaredMinimumThreat == minimum && definition.DeclaredMaximumThreat == maximum,
                    $"'{definition.EncounterId}' must declare the threat its candidates produce. {threatError}");

                Density0Setup.SizeSpec size = Density0Setup.Size(spec.ProfileId);
                int[] waveCounts = definition.Waves.Select(wave => wave.SpawnRules.Sum(rule => rule.Count)).ToArray();
                Assert(waveCounts.All(count => count <= size.MaximumWaveEnemies),
                    $"'{definition.EncounterId}' exceeds {size.MaximumWaveEnemies} enemies per wave.");
                // A wave has one charge lane.
                Assert(definition.Waves.All(wave => wave.SpawnRules.Where(rule =>
                        rule.Candidates.Any(candidate => candidate.EnemyRole == EncounterEnemyRole.Charging))
                        .Sum(rule => rule.Count) <= 1),
                    $"'{definition.EncounterId}' has more than one charger in a wave.");

                if (spec.ProfileId == Week14Room3Setup.SmallProfileId)
                {
                    (int total, int smallMinimum, int smallMaximum) = small[pattern];
                    Assert(waveCounts.Sum() == total && minimum == smallMinimum && maximum == smallMaximum,
                        $"Small '{pattern}' must stay {total} enemies with threat {smallMinimum}~{smallMaximum}.");
                }
                else if (spec.ProfileId == Week14Room6Setup.LargeProfileId)
                {
                    Assert(waveCounts.Length == 3 && waveCounts.Sum() <= 20,
                        $"Large '{pattern}' must be three waves of at most 20 enemies (user decision 2026-11-23).");
                }
                else if (spec.ProfileId != Week14Room1Setup.BasicProfileId)
                {
                    Assert(waveCounts.Length >= 2 && waveCounts.Length <= 3,
                        $"'{definition.EncounterId}' must have two or three waves.");
                }

                if (spec.ProfileId != Week14Room3Setup.SmallProfileId)
                {
                    EncounterDefinition smaller = Find(generator,
                        Week19Spawn2Setup.EncounterId(Week14Room3Setup.SmallProfileId, pattern));
                    Assert(waveCounts.Sum() > smaller.Waves.Sum(wave => wave.SpawnRules.Sum(rule => rule.Count)),
                        $"'{definition.EncounterId}' must hold more enemies than the small room.");
                }
            }
        }

        private static void ValidateInvalidSizes(FloorGenerator generator, RoomDifficultyTable table)
        {
            EncounterEnemyCandidate[] melee = { new(EncounterEnemyRole.Chaser, 1) };
            EncounterEnemyCandidate[] rear = { new(EncounterEnemyRole.Sniper, 1) };
            (string profile, string label, string expected, EncounterSpawnRule[][] waves)[] cases =
            {
                (Week14Room3Setup.SmallProfileId, "five in a small wave", "per wave",
                    new[] { new[] { Rule(melee, 5) } }),
                (Week14Room1Setup.BasicProfileId, "six in a basic wave", "per wave",
                    new[] { new[] { Rule(melee, 6) } }),
                (Week14Room3Setup.WideProfileId, "seven in a wide wave", "per wave",
                    new[] { new[] { Rule(melee, 7) } }),
                (Week14Room6Setup.LargeProfileId, "nine in a large wave", "per wave",
                    new[] { new[] { Rule(melee, 9) } }),
                (Week14Room6Setup.LargeProfileId, "six rear firing in a large wave", "rear firing",
                    new[] { new[] { Rule(rear, 6) } }),
                (Week14Room6Setup.LargeProfileId, "twenty-one in a large room", "outside the room range",
                    new[] { new[] { Rule(melee, 8) }, new[] { Rule(melee, 8) }, new[] { Rule(melee, 5) } }),
            };
            int index = 0;
            foreach ((string profileId, string label, string expected, EncounterSpawnRule[][] waves) in cases)
            {
                RoomProfile profile = generator.RoomTemplates.First(template =>
                    IsCombatTemplate(template) && template.Profile.ProfileId == profileId).Profile;
                EncounterDefinition invalid = ScriptableObject.CreateInstance<EncounterDefinition>();
                try
                {
                    invalid.Configure($"spawn-invalid-density-{index++}", new[] { profile }, 1, 3, 1.5f, 2f,
                        waves.Select((rules, wave) => new EncounterWaveDefinition(wave + 1,
                            wave == 0 ? EncounterWaveStartCondition.RoomEntered
                                : EncounterWaveStartCondition.PreviousWaveCleared,
                            EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, rules)).ToArray(),
                        EncounterClearCondition.AllWavesCleared);
                    Assert(!table.TryValidateEncounterSize(invalid, out string error) &&
                           error.Contains(expected, StringComparison.Ordinal),
                        $"Encounter size validation must reject {label}. {error}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(invalid);
                }
            }

            RoomDifficultyTable broken = ScriptableObject.CreateInstance<RoomDifficultyTable>();
            try
            {
                broken.Configure(table.ThreatScores.ToArray(), table.TierBands.ToArray(), table.FloorRanges.ToArray(),
                    table.DistanceWeights.ToArray());
                RoomSizeDifficulty large = Density0Setup.SizeDifficulties().Last();
                broken.ConfigureSizeDifficulties(new[] { large, large });
                Assert(!broken.TryValidate(out string error) && error.Contains("duplicated", StringComparison.Ordinal),
                    $"The difficulty table must reject a duplicated room size. {error}");
                broken.ConfigureSizeDifficulties(new[]
                {
                    new RoomSizeDifficulty(large.ProfileId, 2, large.MaximumRoomEnemies, large.MaximumWaveEnemies,
                        large.MaximumWaveEnemies + 1, large.TierBands.ToArray(),
                        large.FloorRanges.ToArray()),
                });
                Assert(!broken.TryValidate(out error) && error.Contains("inconsistent", StringComparison.Ordinal),
                    $"The difficulty table must reject more rear firing than a wave holds. {error}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(broken);
            }
        }

        private static void ValidateSpawnPoints(FloorGenerator generator)
        {
            RoomLayoutImporter.Report report = new();
            Dictionary<string, RoomLayoutDocument> documents = RoomLayoutImporter
                .ReadDocuments(RoomLayoutImporter.LayoutFolderPath, generator, report)
                .ToDictionary(document => document.Id, StringComparer.Ordinal);
            Assert(report.Rejected.Count == 0, $"Room Layouts were rejected: {report.Format()}");

            int generatedTotal = 0;
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(IsCombatTemplate))
            {
                string profileId = template.Profile.ProfileId;
                int target = Density0Setup.SpawnPointTarget(profileId);
                Assert(template.TryValidate(out string error) && template.TryValidateLayout(out error), error);
                Assert(template.SpawnPoints.Count == target,
                    $"Template '{template.TemplateId}' must have {target} SpawnPoints, not {template.SpawnPoints.Count}.");

                int authored;
                Vector2[] expected;
                if (RoomLayoutImporter.IsImportedTemplate(template))
                {
                    Assert(documents.TryGetValue(template.TemplateId, out RoomLayoutDocument document),
                        $"Imported template '{template.TemplateId}' has no Layout file.");
                    authored = document.SpawnPoints.Length;
                    expected = RoomLayoutImporter.ResolveSpawnPoints(document);
                    Assert(template.SpawnPointRoles.SequenceEqual(RoomLayoutImporter.BuildSpawnPointRoles(
                            RoomLayoutFormat.FindProfile(profileId), template.SpawnPoints)),
                        $"Imported template '{template.TemplateId}' must use the importer's SpawnPoint roles.");
                }
                else
                {
                    authored = Density0Setup.AuthoredSpawnPointCount(template);
                    expected = Density0Setup.ResolveSpawnPoints(template);
                    Vector2[] encounter4 = Week19Encounter4Setup.AddedSpawnPoints.TryGetValue(template.TemplateId,
                        out Week19Encounter4Setup.AddedSpawnPoint[] added)
                        ? added.Select(point => point.Position).ToArray()
                        : Array.Empty<Vector2>();
                    Assert(template.SpawnPoints.Skip(Week19Encounter4Setup.BaseSpawnPointCount).Take(encounter4.Length)
                            .SequenceEqual(encounter4),
                        $"Template '{template.TemplateId}' moved its Encounter-4 SpawnPoints.");
                }

                Assert(template.SpawnPoints.SequenceEqual(expected),
                    $"Template '{template.TemplateId}' SpawnPoints differ from a fresh Density-0 generation.");
                for (int index = authored; index < template.SpawnPoints.Count; index++)
                {
                    Vector2 point = template.SpawnPoints[index];
                    Assert((template.SpawnPointRoles[index] & SpawnPointPlacementRole.MeleePressure) != 0 &&
                           (template.SpawnPointRoles[index] & SpawnPointPlacementRole.RearFiring) != 0,
                        $"Template '{template.TemplateId}' generated SpawnPoint {index + 1} must take melee and rear firing.");
                    Assert(template.SpawnPoints.Where((_, other) => other != index)
                            .All(otherPoint => Vector2.Distance(point, otherPoint) >= RoomSpawnPointDensity.MinimumSpacing - 0.001f),
                        $"Template '{template.TemplateId}' generated SpawnPoint {index + 1} crowds another point.");
                    // Safe whichever doors are open.
                    Assert(template.DoorSlots.All(door =>
                            Vector2.Distance(point, door.SafeEntryPosition) >= RoomSpawnPointDensity.DoorEntryClearance &&
                            Vector2.Distance(point, door.SlotPosition) >= RoomSpawnPointDensity.DoorSlotClearance),
                        $"Template '{template.TemplateId}' generated SpawnPoint {index + 1} is too close to a door.");
                    generatedTotal++;
                }
            }

            Assert(generatedTotal > 0, "Density-0 generated no SpawnPoints.");
            Assert(Density0Setup.TryValidatePlacement(generator, generator.RoomTemplates.Where(IsCombatTemplate),
                out string placementError), placementError);
        }

        // Returns, per size, a seed whose floor holds a room of that size.
        private static Dictionary<string, int> ValidateGeneration(FloorGenerator generator, RoomDifficultyTable table)
        {
            Dictionary<string, int> sizeSeeds = new(StringComparer.Ordinal);
            Dictionary<string, int> rooms = new(StringComparer.Ordinal);
            Dictionary<string, int> enemies = new(StringComparer.Ordinal);
            Dictionary<string, int[]> tiers = new(StringComparer.Ordinal);
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
                    string profileId = room.Template.Profile.ProfileId;
                    Assert(table.TryGetEncounterLimits(profileId, out int minimumRoom, out int maximumRoom,
                        out int maximumWave, out _), $"Room size '{profileId}' has no limits.");
                    int total = room.ResolvedEncounterWaves.Sum(wave => wave.Length);
                    Assert(total >= minimumRoom && total <= maximumRoom &&
                           room.ResolvedEncounterWaves.All(wave => wave.Length <= maximumWave),
                        $"Seed {seed} room '{room.RoomId}' ({profileId}) resolved {total} enemies outside " +
                        $"{minimumRoom}~{maximumRoom} or over {maximumWave} per wave.");
                    Assert(room.ResolvedEncounterWaves.All(wave =>
                            wave.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == wave.Length &&
                            wave.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role))),
                        $"Seed {seed} room '{room.RoomId}' reused or mismatched a SpawnPoint.");
                    Assert(room.HasDifficulty && table.TryGetFloorRange(room.FloorNumber, profileId,
                               out FloorDifficultyRange range) && range.Contains(room.Difficulty.Score) &&
                           table.Classify(room.Difficulty.Score, profileId) == room.Difficulty.Tier,
                        $"Seed {seed} room '{room.RoomId}' ({profileId}) is not scored with its own size.");

                    rooms[profileId] = rooms.TryGetValue(profileId, out int count) ? count + 1 : 1;
                    enemies[profileId] = enemies.TryGetValue(profileId, out int sum) ? sum + total : total;
                    if (!tiers.TryGetValue(profileId, out int[] histogram))
                        tiers[profileId] = histogram = new int[Enum.GetValues(typeof(RoomDifficultyTier)).Length];
                    histogram[(int)room.Difficulty.Tier]++;
                    if (room.FloorNumber == 1) sizeSeeds.TryAdd(profileId, seed);
                }
            }

            foreach (Density0Setup.SizeSpec size in Density0Setup.Sizes)
            {
                Assert(rooms.ContainsKey(size.ProfileId) && sizeSeeds.ContainsKey(size.ProfileId),
                    $"{GeneratedSeedCount} seeds never generated a floor-1 '{size.ProfileId}' combat room.");
                Assert(tiers[size.ProfileId].Count(value => value > 0) >= 2,
                    $"Room size '{size.ProfileId}' rooms all land in one difficulty tier: " +
                    string.Join("/", tiers[size.ProfileId]));
            }

            double Average(string profileId) => (double)enemies[profileId] / rooms[profileId];
            Assert(Average(Week14Room1Setup.BasicProfileId) > Average(Week14Room3Setup.SmallProfileId) &&
                   Average(Week14Room3Setup.WideProfileId) > Average(Week14Room1Setup.BasicProfileId) &&
                   Average(Week14Room6Setup.LargeProfileId) > Average(Week14Room3Setup.WideProfileId),
                "Bigger rooms must hold more enemies on average.");

            Debug.Log($"Density-0 verification passed: {GeneratedSeedCount} seeds, enemies per room " +
                      string.Join(", ", Density0Setup.Sizes.Select(size =>
                          $"{size.ProfileId} {Average(size.ProfileId):0.0} ({rooms[size.ProfileId]} rooms, tiers " +
                          string.Join("/", tiers[size.ProfileId]) + ")")) +
                      "; every Encounter matches its size's waves and declared threat, generated SpawnPoints are " +
                      "safe and placeable under all door sets, and invalid sizes are rejected.");
            return sizeSeeds;
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, Dictionary<string, int> seeds)
        {
            int seed = seeds[Week14Room6Setup.LargeProfileId];
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            Dictionary<string, GeneratedRoomNode> generated = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            int checkedLarge = 0;
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
                        Assert(((Vector2)runtime.SpawnPoints[index].localPosition -
                                node.Template.SpawnPoints[stored[index].SpawnPointIndex]).sqrMagnitude < 0.0001f,
                            $"Runtime room '{node.RoomId}' rebound stored spawn {index + 1}.");
                    }
                }

                if (node.Template.Profile.ProfileId == Week14Room6Setup.LargeProfileId) checkedLarge++;
            }

            Assert(checkedLarge > 0, $"Runtime seed {seed} did not bind a large room.");
        }

        private static EncounterDefinition Find(FloorGenerator generator, string id)
        {
            EncounterDefinition definition = generator.EncounterDefinitions.SingleOrDefault(candidate =>
                candidate != null && candidate.EncounterId == id);
            Assert(definition != null, $"The Encounter catalog is missing '{id}'.");
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
