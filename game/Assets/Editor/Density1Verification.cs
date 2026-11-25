using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Density-1 (docs/28 §10.1): small Encounters pick the chaser at half weight, nothing else about them or the
    // bigger sizes changes, and generated small rooms really hold a lower share of chasers on every floor.
    public static class Density1Verification
    {
        private const int GeneratedSeedCount = 256;

        [MenuItem("Trickal Fan Game/Beta/Setup and Verify Density-1 Small Room Chaser Weight")]
        public static void SetupAndVerifyBatch()
        {
            Density1Setup.Setup();
            Dictionary<string, string> guids = TrackedPaths().ToDictionary(path => path,
                AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            string signature = Signature(OpenGenerator());
            Density1Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Density-1 setup changed or lost the GUID for {entry.Key}.");
            }

            Assert(signature == Signature(OpenGenerator()), "Re-running Density-1 setup changed the Encounters.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Beta/Verify Density-1 Small Room Chaser Weight")]
        public static void Verify()
        {
            FloorGenerator generator = OpenGenerator();
            Assert(generator != null && generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Density1Setup.EncounterContentVersion,
                "Run Density-1 setup before verification.");
            ValidateWeights(generator);
            ValidateGeneration(generator);
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Density0Verification.VerifyWithRegressionsBatch();
            Debug.Log("Density-1 regression verification passed.");
        }

        private static FloorGenerator OpenGenerator()
        {
            Week18Obstacle2Setup.OpenGameScene();
            return GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)?.GetComponent<FloorGenerator>();
        }

        private static IEnumerable<string> TrackedPaths() => Week27Curve1Setup.Patterns
            .SelectMany(pattern => Week19Spawn2Setup.Profiles.Select(profile =>
                Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
            .Append(Week13FrontendSetup.GameScenePath);

        private static string Signature(FloorGenerator generator) =>
            generator.EncounterContentVersion + "|" + string.Join(";", generator.EncounterDefinitions.Select(definition =>
                definition.EncounterId + ":" + definition.DeclaredMinimumThreat + "~" + definition.DeclaredMaximumThreat +
                ":" + string.Join("/", definition.Waves.Select(wave => string.Join(",", wave.SpawnRules.Select(rule =>
                    rule.Count + "x" + string.Join("+", rule.Candidates.Select(candidate =>
                        $"{candidate.EnemyRole}:{candidate.Weight}"))))))));

        private static void ValidateWeights(FloorGenerator generator)
        {
            Assert(Density1Setup.SmallChaserWeight(50) == 25 && Density1Setup.SmallChaserWeight(60) == 30 &&
                   Density1Setup.SmallChaserWeight(70) == 35, "The small chaser weights must be 25/30/35.");

            int smallChaserRules = 0;
            foreach (string pattern in Week27Curve1Setup.Patterns)
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                string id = Week19Spawn2Setup.EncounterId(spec.ProfileId, pattern);
                EncounterDefinition definition = generator.EncounterDefinitions.Single(item => item.EncounterId == id);
                Density0Setup.Slot[][] layout = Density0Setup.Layout(spec.ProfileId, pattern);
                bool small = Density0Setup.IsSmall(spec.ProfileId);
                for (int wave = 0; wave < layout.Length; wave++)
                for (int index = 0; index < layout[wave].Length; index++)
                {
                    EncounterEnemyCandidate[] original = Density0Setup.BaseCandidates(layout[wave][index].Kind, spec);
                    EncounterEnemyCandidate[] actual = definition.Waves[wave].SpawnRules[index].Candidates.ToArray();
                    Assert(actual.Select(candidate => candidate.EnemyRole)
                            .SequenceEqual(original.Select(candidate => candidate.EnemyRole)),
                        $"'{id}' wave {wave + 1} changed its candidates.");
                    for (int candidate = 0; candidate < original.Length; candidate++)
                    {
                        bool halved = small && original[candidate].EnemyRole == EncounterEnemyRole.Chaser;
                        int expected = halved
                            ? Density1Setup.SmallChaserWeight(original[candidate].Weight)
                            : original[candidate].Weight;
                        Assert(actual[candidate].Weight == expected,
                            $"'{id}' wave {wave + 1} {original[candidate].EnemyRole} must weigh {expected}, " +
                            $"not {actual[candidate].Weight}.");
                        if (halved) smallChaserRules++;
                    }
                }
            }

            Assert(smallChaserRules > 0, "No small Encounter holds a chaser candidate.");
        }

        private static void ValidateGeneration(FloorGenerator generator)
        {
            int[] smallChasers = new int[4], smallEnemies = new int[4], basicChasers = new int[4], basicEnemies = new int[4];
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode room in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
                {
                    string profileId = room.Template.Profile.ProfileId;
                    bool small = Density0Setup.IsSmall(profileId);
                    if (!small && profileId != Week14Room1Setup.BasicProfileId) continue;
                    int floor = room.FloorNumber;
                    Assert(floor >= 1 && floor <= 3, $"Seed {seed} room '{room.RoomId}' is on floor {floor}.");
                    int total = room.ResolvedEncounterWaves.Sum(wave => wave.Length);
                    int chasers = room.ResolvedEncounterWaves.Sum(wave =>
                        wave.Count(spawn => spawn.Role == EncounterEnemyRole.Chaser));
                    (small ? smallEnemies : basicEnemies)[floor] += total;
                    (small ? smallChasers : basicChasers)[floor] += chasers;
                }
            }

            List<string> shares = new();
            for (int floor = 1; floor <= 3; floor++)
            {
                Assert(smallEnemies[floor] > 0 && basicEnemies[floor] > 0,
                    $"{GeneratedSeedCount} seeds never generated small and basic rooms on floor {floor}.");
                double small = (double)smallChasers[floor] / smallEnemies[floor];
                double basic = (double)basicChasers[floor] / basicEnemies[floor];
                Assert(smallChasers[floor] > 0, $"Small rooms on floor {floor} must still hold some chasers.");
                Assert(small < basic,
                    $"Small rooms on floor {floor} must hold fewer chasers than basic rooms ({small:P1} vs {basic:P1}).");
                shares.Add($"F{floor} small {small:P1} / basic {basic:P1}");
            }

            Debug.Log($"Density-1 verification passed: {GeneratedSeedCount} seeds, chaser share " +
                      string.Join(", ", shares) + ".");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
