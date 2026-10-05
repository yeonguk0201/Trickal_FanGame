using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Difficulty1Verification
    {
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;
        private const float CurveTolerance = 0.05f;

        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Difficulty-1 Room Difficulty")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week19Difficulty1Setup.Setup();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week19Difficulty1Setup.TablePath);
            Week19Difficulty1Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Difficulty-1 setup changed the Game Scene GUID.");
            Assert(!string.IsNullOrWhiteSpace(tableGuid) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week19Difficulty1Setup.TablePath),
                "Difficulty-1 setup changed or lost the difficulty table GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Difficulty-1 Room Difficulty")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week19Difficulty1Setup.EncounterContentVersion,
                "Run Difficulty-1 setup before verification.");

            RoomDifficultyTable table = generator.DifficultyTable;
            ValidateTable(table);
            ValidateInvalidTables();
            ValidateDeclaredThreats(generator, table);
            ValidateLayoutModifiers(generator);
            ValidateDistanceCurve(table);
            ValidateFloorRangeFailure(generator);
            ValidateGeneratedDifficulty(generator, table);
        }

        private static void ValidateTable(RoomDifficultyTable table)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(Week19Difficulty1Setup.ThreatScores.All(expected =>
                       table.TryGetThreat(expected.Role, out int score) && score == expected.Score) &&
                   table.ThreatScores.Count == Week19Difficulty1Setup.ThreatScores.Length &&
                   !table.TryGetThreat(EncounterEnemyRole.Boss, out _),
                "Threat scores must be Chaser 2 / FastChaser 3 / Ranged 3 / Sniper 4 / Charging 4 without Boss.");
            Assert(table.Classify(0) == RoomDifficultyTier.Easy &&
                   table.Classify(11) == RoomDifficultyTier.Easy &&
                   table.Classify(12) == RoomDifficultyTier.Normal &&
                   table.Classify(15) == RoomDifficultyTier.Normal &&
                   table.Classify(16) == RoomDifficultyTier.Hard &&
                   table.Classify(40) == RoomDifficultyTier.Hard,
                "Tier bands must be Easy <= 11, Normal 12~15, Hard >= 16.");
            foreach ((int floor, int maximum) in new[] { (1, 18), (2, 20), (3, 22) })
            {
                Assert(table.TryGetFloorRange(floor, out FloorDifficultyRange range) &&
                       range.MinimumScore == 0 && range.MaximumScore == maximum,
                    $"Floor {floor} difficulty range must be 0~{maximum}.");
            }
        }

        private static void ValidateInvalidTables()
        {
            (string label, Action<RoomDifficultyTable> configure)[] cases =
            {
                ("missing Sniper threat", table => table.Configure(
                    Week19Difficulty1Setup.ThreatScores.Where(entry => entry.Role != EncounterEnemyRole.Sniper)
                        .ToArray(),
                    Week19Difficulty1Setup.TierBands, Week19Difficulty1Setup.FloorRanges,
                    Week19Difficulty1Setup.DistanceWeights)),
                ("boss threat", table => table.Configure(
                    Week19Difficulty1Setup.ThreatScores.Append(new EnemyThreatScore(EncounterEnemyRole.Boss, 9))
                        .ToArray(),
                    Week19Difficulty1Setup.TierBands, Week19Difficulty1Setup.FloorRanges,
                    Week19Difficulty1Setup.DistanceWeights)),
                ("unordered tier bands", table => table.Configure(Week19Difficulty1Setup.ThreatScores,
                    new[]
                    {
                        new RoomDifficultyTierBand(RoomDifficultyTier.Easy, 0),
                        new RoomDifficultyTierBand(RoomDifficultyTier.Normal, 16),
                        new RoomDifficultyTierBand(RoomDifficultyTier.Hard, 12),
                    },
                    Week19Difficulty1Setup.FloorRanges, Week19Difficulty1Setup.DistanceWeights)),
                ("inverted floor range", table => table.Configure(Week19Difficulty1Setup.ThreatScores,
                    Week19Difficulty1Setup.TierBands, new[] { new FloorDifficultyRange(1, 10, 5) },
                    Week19Difficulty1Setup.DistanceWeights)),
                ("zero far weights", table => table.Configure(Week19Difficulty1Setup.ThreatScores,
                    Week19Difficulty1Setup.TierBands, Week19Difficulty1Setup.FloorRanges,
                    new[]
                    {
                        new RoomDifficultyDistanceWeight(RoomDifficultyTier.Easy, 1, 0),
                        new RoomDifficultyDistanceWeight(RoomDifficultyTier.Normal, 1, 0),
                        new RoomDifficultyDistanceWeight(RoomDifficultyTier.Hard, 1, 0),
                    })),
            };
            foreach ((string label, Action<RoomDifficultyTable> configure) in cases)
            {
                RoomDifficultyTable invalid = ScriptableObject.CreateInstance<RoomDifficultyTable>();
                try
                {
                    configure(invalid);
                    Assert(!invalid.TryValidate(out string error) && !string.IsNullOrWhiteSpace(error),
                        $"Difficulty table validation must reject a {label}.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(invalid);
                }
            }
        }

        private static void ValidateDeclaredThreats(FloorGenerator generator, RoomDifficultyTable table)
        {
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                foreach ((string pattern, int minimum, int maximum) in new[]
                         {
                             ("pressure", Week19Difficulty1Setup.PressureMinimumThreat,
                                 Week19Difficulty1Setup.PressureMaximumThreat),
                             ("crossfire", Week19Difficulty1Setup.CrossfireMinimumThreat,
                                 Week19Difficulty1Setup.CrossfireMaximumThreat),
                         })
                {
                    string id = Week19Spawn2Setup.EncounterId(spec.ProfileId, pattern);
                    EncounterDefinition definition = generator.EncounterDefinitions.Single(candidate =>
                        candidate.EncounterId == id);
                    Assert(table.TryComputeThreatRange(definition, out int computedMinimum,
                               out int computedMaximum, out string error), error);
                    Assert(computedMinimum == minimum && computedMaximum == maximum &&
                           definition.DeclaredMinimumThreat == minimum &&
                           definition.DeclaredMaximumThreat == maximum,
                        $"Encounter '{id}' must compute and declare threat {minimum}~{maximum}, " +
                        $"computed {computedMinimum}~{computedMaximum}.");
                    Assert(definition.TryValidateThreat(table, out error), error);
                }
            }

            EncounterDefinition sample = generator.EncounterDefinitions.First();
            int declaredMinimum = sample.DeclaredMinimumThreat;
            int declaredMaximum = sample.DeclaredMaximumThreat;
            try
            {
                foreach ((string label, int minimum, int maximum) in new[]
                         {
                             ("above the declared maximum", declaredMinimum, declaredMaximum - 1),
                             ("below the declared minimum", declaredMinimum + 1, declaredMaximum),
                             ("an undeclared range", 0, 0),
                         })
                {
                    sample.ConfigureDeclaredThreat(minimum, maximum);
                    Assert(!sample.TryValidateThreat(table, out string error) && !string.IsNullOrWhiteSpace(error),
                        $"Encounter threat validation must fail when candidate sums fall {label}.");
                    Assert(!generator.TryGenerateForSeed(1, out _, out error) &&
                           error.Contains(sample.EncounterId, StringComparison.Ordinal),
                        $"Floor generation must stop explicitly when candidate sums fall {label}.");
                }
            }
            finally
            {
                sample.ConfigureDeclaredThreat(declaredMinimum, declaredMaximum);
            }
        }

        private static void ValidateLayoutModifiers(FloorGenerator generator)
        {
            string[] obstacleLayouts = Week18Obstacle2Setup.Layouts.Select(layout => layout.TemplateId)
                .Concat(Week20Obstacle4Setup.Layouts.Select(layout => layout.TemplateId))
                .Concat(Week22Terrain0Setup.Layouts.Select(layout => layout.TemplateId)).ToArray();
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                int expected = obstacleLayouts.Contains(template.TemplateId, StringComparer.Ordinal)
                    ? Week19Difficulty1Setup.ObstacleLayoutModifier
                    : 0;
                Assert(template.LayoutDifficultyModifier == expected,
                    $"Room Template '{template.TemplateId}' Layout difficulty modifier must be {expected}.");
            }

            Assert(obstacleLayouts.All(id => generator.RoomTemplates.Any(template => template.TemplateId == id)),
                "Every obstacle Layout must be registered with its +1 difficulty modifier.");
        }

        private static void ValidateDistanceCurve(RoomDifficultyTable table)
        {
            AssertShares(table.TierWeightsAt(0, 4), new[] { 0.40f, 0.45f, 0.15f }, "nearest");
            AssertShares(table.TierWeightsAt(2, 4), new[] { 0.25f, 0.425f, 0.325f }, "middle");
            AssertShares(table.TierWeightsAt(4, 4), new[] { 0.10f, 0.40f, 0.50f }, "farthest");
            AssertShares(table.TierWeightsAt(0, 0), new[] { 0.25f, 0.425f, 0.325f }, "single-distance");
        }

        private static void AssertShares(int[] weights, float[] expected, string label)
        {
            float total = weights.Sum();
            for (int index = 0; index < expected.Length; index++)
            {
                Assert(Mathf.Abs(weights[index] / total - expected[index]) < 0.0001f,
                    $"The {label} distance tier weights must be {string.Join("/", expected)}.");
            }
        }

        private static void ValidateFloorRangeFailure(FloorGenerator generator)
        {
            Assert(generator.TryGenerateForSeed(1, out GeneratedFloorGraph graph, out string error), error);
            RoomDifficultyTable strict = ScriptableObject.CreateInstance<RoomDifficultyTable>();
            try
            {
                strict.Configure(Week19Difficulty1Setup.ThreatScores, Week19Difficulty1Setup.TierBands,
                    new[] { new FloorDifficultyRange(1, 0, 5), new FloorDifficultyRange(2, 0, 5),
                        new FloorDifficultyRange(3, 0, 5) },
                    Week19Difficulty1Setup.DistanceWeights);
                Assert(!EncounterSelector.TryAssign(graph, generator.EncounterDefinitions,
                           generator.EncounterContentVersion, strict, out error) &&
                       error.Contains("range 0~5", StringComparison.Ordinal),
                    "Encounter selection must fail explicitly when no candidate fits the floor range.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strict);
            }
        }

        private static void ValidateGeneratedDifficulty(FloorGenerator generator, RoomDifficultyTable table)
        {
            int[,] tierCounts = new int[2, 3];
            int[,] targetCounts = new int[2, 3];
            int[] floorMaximum = new int[4];
            HashSet<string> selectedEncounters = new(StringComparer.Ordinal);
            int[,] hardCapableCounts = new int[2, 3];
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(DifficultySignature(graph) == DifficultySignature(repeat),
                        $"Seed {seed} changed room difficulty or Encounter selection on regeneration.");
                }

                foreach (GeneratedFloor floor in graph.Floors)
                {
                    Dictionary<string, int> distances = Distances(floor);
                    GeneratedRoomNode[] rooms = floor.Nodes
                        .Where(node => node.Role == GeneratedRoomRole.Intermediate).ToArray();
                    if (rooms.Length == 0) continue;
                    int nearest = rooms.Min(node => distances[node.RoomId]);
                    int farthest = rooms.Max(node => distances[node.RoomId]);
                    Assert(table.TryGetFloorRange(floor.FloorNumber, out FloorDifficultyRange range),
                        $"Floor {floor.FloorNumber} has no difficulty range.");
                    foreach (GeneratedRoomNode room in rooms)
                    {
                        Assert(room.HasDifficulty, $"Seed {seed} room '{room.RoomId}' has no stored difficulty.");
                        GeneratedRoomDifficulty difficulty = room.Difficulty;
                        Assert(table.TryScore(room.ResolvedEncounterWaves, room.Template.LayoutDifficultyModifier,
                                   out int score, out error) && score == difficulty.Score,
                            $"Seed {seed} room '{room.RoomId}' score must equal resolved threat plus Layout modifier.");
                        Assert(difficulty.Tier == table.Classify(score) && range.Contains(score),
                            $"Seed {seed} room '{room.RoomId}' score {score} has a wrong tier or leaves floor range.");
                        Assert(difficulty.DistanceFromStart == distances[room.RoomId],
                            $"Seed {seed} room '{room.RoomId}' stored the wrong start distance.");
                        selectedEncounters.Add(room.EncounterId);
                        floorMaximum[floor.FloorNumber] = Math.Max(floorMaximum[floor.FloorNumber], score);

                        int available = difficulty.AvailableTierMask;
                        Assert(difficulty.IsTierAvailable(difficulty.Tier),
                            $"Seed {seed} room '{room.RoomId}' selected a tier outside its available tiers.");
                        int closestGap = Enumerable.Range(0, 3).Where(tier => (available & (1 << tier)) != 0)
                            .Min(tier => Math.Abs(tier - (int)difficulty.TargetTier));
                        Assert(Math.Abs((int)difficulty.Tier - (int)difficulty.TargetTier) == closestGap,
                            $"Seed {seed} room '{room.RoomId}' ignored its {difficulty.TargetTier} target " +
                            $"and selected {difficulty.Tier}.");

                        int bucket = farthest > nearest && distances[room.RoomId] == farthest ? 1
                            : distances[room.RoomId] == nearest && farthest > nearest ? 0 : -1;
                        if (bucket < 0) continue;
                        tierCounts[bucket, (int)difficulty.Tier]++;
                        targetCounts[bucket, (int)difficulty.TargetTier]++;
                        if (difficulty.IsTierAvailable(RoomDifficultyTier.Hard))
                            hardCapableCounts[bucket, (int)difficulty.Tier]++;
                    }
                }
            }

            float nearHard = Share(tierCounts, 0, RoomDifficultyTier.Hard);
            float farHard = Share(tierCounts, 1, RoomDifficultyTier.Hard);
            float capableNearHard = Share(hardCapableCounts, 0, RoomDifficultyTier.Hard);
            float capableFarHard = Share(hardCapableCounts, 1, RoomDifficultyTier.Hard);
            float nearCapable = Total(hardCapableCounts, 0) / (float)Total(tierCounts, 0);
            float farCapable = Total(hardCapableCounts, 1) / (float)Total(tierCounts, 1);
            Assert(tierCounts[0, (int)RoomDifficultyTier.Hard] > 0 && farHard > nearHard,
                $"Far rooms must be Hard more often than near rooms while near rooms can still be Hard " +
                $"(near Hard {nearHard:P1}, far Hard {farHard:P1}).");
            Assert(capableFarHard - capableNearHard >= 0.2f,
                $"Rooms that can host a Hard Encounter must follow the distance curve " +
                $"(near Hard {capableNearHard:P1}, far Hard {capableFarHard:P1}).");
            Assert(Mathf.Abs(Share(targetCounts, 0, RoomDifficultyTier.Hard) - 0.15f) <= CurveTolerance &&
                   Mathf.Abs(Share(targetCounts, 1, RoomDifficultyTier.Hard) - 0.50f) <= CurveTolerance &&
                   Mathf.Abs(Share(targetCounts, 0, RoomDifficultyTier.Easy) - 0.40f) <= CurveTolerance &&
                   Mathf.Abs(Share(targetCounts, 1, RoomDifficultyTier.Easy) - 0.10f) <= CurveTolerance,
                "Target tier rolls must follow the 40/45/15 -> 10/40/50 distance curve.");
            Assert(Enumerable.Range(0, 3).All(tier => tierCounts[0, tier] + tierCounts[1, tier] > 0),
                "Generated rooms must include Easy, Normal, and Hard difficulty.");
            Assert(generator.EncounterDefinitions.All(definition => selectedEncounters.Contains(definition.EncounterId)),
                "Every registered Encounter must still be selected across generated seeds.");
            Assert(floorMaximum[1] <= 18 && floorMaximum[2] <= 20 && floorMaximum[3] <= 22,
                "Generated room scores must respect the 18/20/22 floor caps.");

            Debug.Log($"Difficulty-1 verification passed: {GeneratedSeedCount} seeds; near/far Hard " +
                      $"{nearHard:P1}/{farHard:P1} (near/far Easy {Share(tierCounts, 0, RoomDifficultyTier.Easy):P1}/" +
                      $"{Share(tierCounts, 1, RoomDifficultyTier.Easy):P1}); Hard-capable rooms near/far " +
                      $"{nearCapable:P1}/{farCapable:P1} with Hard {capableNearHard:P1}/{capableFarHard:P1}; " +
                      $"target Hard {Share(targetCounts, 0, RoomDifficultyTier.Hard):P1}/" +
                      $"{Share(targetCounts, 1, RoomDifficultyTier.Hard):P1}; floor max scores " +
                      $"{floorMaximum[1]}/{floorMaximum[2]}/{floorMaximum[3]}; every room took its target tier " +
                      "or the nearest available one; declared threat violations, invalid tables, and empty floor " +
                      "ranges fail explicitly, and setup preserves GUIDs.");
        }

        private static int Total(int[,] counts, int bucket) => counts[bucket, 0] + counts[bucket, 1] + counts[bucket, 2];

        private static float Share(int[,] counts, int bucket, RoomDifficultyTier tier)
        {
            int total = counts[bucket, 0] + counts[bucket, 1] + counts[bucket, 2];
            return total == 0 ? 0f : counts[bucket, (int)tier] / (float)total;
        }

        private static Dictionary<string, int> Distances(GeneratedFloor floor)
        {
            Dictionary<string, GeneratedRoomNode> byId = floor.Nodes.ToDictionary(node => node.RoomId,
                StringComparer.Ordinal);
            Dictionary<string, int> result = new(StringComparer.Ordinal) { [floor.StartingRoomId] = 0 };
            Queue<string> queue = new();
            queue.Enqueue(floor.StartingRoomId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (!connection.IsSecret && result.TryAdd(connection.DestinationRoomId, result[current] + 1))
                        queue.Enqueue(connection.DestinationRoomId);
            }

            return result;
        }

        private static string DifficultySignature(GeneratedFloorGraph graph) =>
            string.Join("|", graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .Select(node => $"{node.RoomId}:{node.EncounterId}:{node.Difficulty.Score}:" +
                                $"{node.Difficulty.Tier}:{node.Difficulty.TargetTier}:" +
                                string.Join("/", node.ResolvedEncounterWaves.Select(wave =>
                                    string.Join(",", wave.Select(spawn => $"{spawn.Role}@{spawn.SpawnPointIndex}"))))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
