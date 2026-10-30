using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Curve-1: enemies enter by floor with x1.5 on their first floor and x0.5 two floors later, crossfire and
    // elite-pair start on floor 2, and 산사모-순수 fires three shots with no aim lead.
    public static class Week27Curve1Verification
    {
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 32;

        private static readonly (EncounterEnemyRole Role, int Floor)[] ExpectedIntroduction =
        {
            (EncounterEnemyRole.Chaser, 1),
            (EncounterEnemyRole.FastChaser, 1),
            (EncounterEnemyRole.Sniper, 1),
            (EncounterEnemyRole.Jyubi, 1),
            (EncounterEnemyRole.Ranged, 2),
            (EncounterEnemyRole.Charging, 2),
            (EncounterEnemyRole.FourWayShooter, 2),
            (EncounterEnemyRole.SplittingChaser, 3),
            // Enemy-10.
            (EncounterEnemyRole.SelfDestruct, 2),
            // Enemy-11.
            (EncounterEnemyRole.JyubiKing, 3),
            // Enemy-12.
            (EncounterEnemyRole.Lobber, 3),
            // Enemy-13.
            (EncounterEnemyRole.Ambusher, 3),
        };

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Curve-1 Floor Enemy Pool")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => Week27Curve1Setup.Patterns
                    .Select(pattern => Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
                .Append(Week19Spawn2Setup.QuickRangedPrefabPath)
                .Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week27Curve1Setup.Setup();
            Week27Curve1Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Curve-1 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Curve-1 Floor Enemy Pool")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Curve1Setup.EncounterContentVersion,
                "Run Curve-1 setup before verification.");

            ValidateFloorPool();
            ValidateCatalog(generator);
            string generation = ValidateGeneration(generator);
            ValidateQuickRanged();
            Debug.Log("Curve-1 verification passed: floor 1 draws 불효자손/산사모-활발/고혈당/쥬비, floor 2 adds " +
                      "산사모-순수/돌진/저혈당-활발, floor 3 adds 누루링 (new x1.5, two floors below x0.5); " +
                      "crossfire and elite-pair start on floor 2; 산사모-순수 fires three shots with no lead; " +
                      $"{generation}.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Curve-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every Encounter, spawn and enemy check that resolves rooms by floor or reads 산사모-순수. Each scene
            // check starts from a freshly loaded scene, and every check runs even after a failure.
            (string Name, Action Run)[] checks =
            {
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
                ("Week19Spawn1", Week19Spawn1Verification.Verify),
                ("Week19Spawn2", Week19Spawn2Verification.Verify),
                ("Week19Difficulty1", Week19Difficulty1Verification.Verify),
                ("Week19Encounter4", Week19Encounter4Verification.Verify),
                ("Week19Tune1", Week19Tune1Verification.Verify),
                ("Week20Floor1", Week20Floor1Verification.Verify),
                ("SansamoPureMotion", SansamoPureMotionVerification.Verify),
                ("Week27Curve0", Week27Curve0Verification.Verify),
            };
            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                    run();
                    Debug.Log($"[Curve-1 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Curve-1 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Curve-1 regression failed ({failures.Count}/{checks.Length}):\n" +
                    string.Join("\n", failures));
            }

            Debug.Log("Curve-1 regression verification passed.");
        }

        private static void ValidateFloorPool()
        {
            EncounterEnemyRole[] normal = ((EncounterEnemyRole[])Enum.GetValues(typeof(EncounterEnemyRole)))
                .Where(role => role != EncounterEnemyRole.Boss).ToArray();
            Assert(normal.Length == ExpectedIntroduction.Length &&
                   normal.All(role => ExpectedIntroduction.Any(entry => entry.Role == role)),
                "Every normal Encounter role needs a Curve-1 introduction floor in this verifier.");
            foreach ((EncounterEnemyRole role, int floor) in ExpectedIntroduction)
            {
                Assert(EncounterEnemyFloorPool.IntroductionFloor(role) == floor,
                    $"{role} must first appear on floor {floor}.");
                for (int current = 1; current <= 3; current++)
                {
                    int expected = current < floor ? 0 : current == floor ? 3 : current - floor >= 2 ? 1 : 2;
                    Assert(EncounterEnemyFloorPool.WeightHalves(role, current) == expected &&
                           EncounterEnemyFloorPool.IsAvailable(role, current) == current >= floor,
                        $"{role} on floor {current} must weigh {expected} halves.");
                }
            }

            // The pressure melee slot: floor 1 keeps only the two chasers, floor 2 leans to 저혈당-활발 and 자폭형
            // (Enemy-10), and floor 3 leans to 누루링 and 잠복형 (Enemy-13) while the floor-1 chasers halve.
            EncounterEnemyCandidate[] melee = Week27Enemy13Setup.PressureMeleeCandidates();
            int[][] expectedWeights =
            {
                new[] { 150, 75, 0, 0, 0, 0 },
                new[] { 100, 50, 45, 0, 45, 0 },
                new[] { 50, 25, 30, 30, 30, 45 },
            };
            for (int floor = 1; floor <= 3; floor++)
            {
                int[] actual = melee.Select(candidate =>
                    EncounterEnemyFloorPool.EffectiveWeight(candidate, floor)).ToArray();
                Assert(actual.SequenceEqual(expectedWeights[floor - 1]),
                    $"Pressure melee weights on floor {floor} must be " +
                    $"{string.Join("/", expectedWeights[floor - 1])}, got {string.Join("/", actual)}.");
            }
        }

        private static void ValidateCatalog(FloorGenerator generator)
        {
            RoomDifficultyTable table = generator.DifficultyTable;
            Assert(generator.EncounterDefinitions.Count ==
                   Week19Spawn2Setup.Profiles.Length * Week27Curve1Setup.Patterns.Length,
                $"The Game Scene must hold {Week19Spawn2Setup.Profiles.Length * Week27Curve1Setup.Patterns.Length} " +
                $"Encounters, got {generator.EncounterDefinitions.Count}.");
            Dictionary<int, HashSet<string>> patternsByFloor = new() { [1] = new(), [2] = new(), [3] = new() };
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(Week27Curve1Setup.TryResolvePattern(definition, out string pattern),
                    $"Unknown Encounter '{definition?.EncounterId}'.");
                int minimum = Week27Curve1Setup.MinimumFloor(pattern);
                Assert(definition.MinimumFloor == minimum && definition.MaximumFloor == 3,
                    $"'{definition.EncounterId}' must allow floors {minimum}~3, got " +
                    $"{definition.MinimumFloor}~{definition.MaximumFloor}.");
                Assert(definition.TryValidate(out string error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                for (int floor = definition.MinimumFloor; floor <= definition.MaximumFloor; floor++)
                    patternsByFloor[floor].Add(pattern);

                // A floor before the range must leave a fixed later-floor slot empty, so the Encounter cannot move
                // down.
                for (int earlier = 1; earlier < minimum; earlier++)
                {
                    Assert(!table.TryComputeThreatRange(definition, earlier, out _, out _, out error) &&
                           !string.IsNullOrWhiteSpace(error),
                        $"'{definition.EncounterId}' must have no floor-{earlier} composition.");
                }
            }

            Assert(patternsByFloor[1].SetEquals(new[] { "jyubi-swarm", "pressure", "swarm" }) &&
                   patternsByFloor[2].SetEquals(Week27Curve1Setup.Patterns
                       .Except(Week27Curve1Setup.FloorThreePatterns)) &&
                   patternsByFloor[3].SetEquals(Week27Curve1Setup.Patterns),
                "Floor 1 must offer jyubi-swarm, pressure and swarm; floor 2 every pattern but jyubi-nest (Enemy-11); " +
                "floor 3 every pattern.");

            // An Encounter whose floor range reaches a floor that empties one of its rules must fail validation.
            EncounterDefinition probe = generator.EncounterDefinitions.First(definition =>
                Week27Curve1Setup.TryResolvePattern(definition, out string pattern) && pattern == "elite-pair");
            EncounterDefinition invalid = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                invalid.Configure(probe.EncounterId, probe.AllowedProfiles.ToArray(), 1, 3,
                    probe.MinimumPlayerDistance, probe.MinimumDoorDistance, probe.Waves.ToArray(),
                    probe.ClearCondition);
                Assert(!invalid.TryValidate(out string error) && error.Contains("floor 1"),
                    "A floor-1 elite-pair must fail validation because its charger enters on floor 2.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        private static string ValidateGeneration(FloorGenerator generator)
        {
            int[,] counts = new int[4, Enum.GetValues(typeof(EncounterEnemyRole)).Length];
            int[] rooms = new int[4];
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error),
                    $"Seed {seed}: {error}");
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat),
                        $"Seed {seed} changed its Encounter or spawn selection on regeneration.");
                }

                foreach (GeneratedRoomNode room in graph.Nodes.Where(node =>
                             node.Role == GeneratedRoomRole.Intermediate))
                {
                    int floor = room.FloorNumber;
                    Assert(floor >= 1 && floor <= 3, $"Seed {seed} generated combat floor {floor}.");
                    rooms[floor]++;
                    Assert(room.Encounter.MinimumFloor <= floor,
                        $"Seed {seed} placed '{room.EncounterId}' on floor {floor}.");
                    foreach (ResolvedEncounterSpawn spawn in room.ResolvedEncounterWaves.SelectMany(wave => wave))
                    {
                        Assert(EncounterEnemyFloorPool.IsAvailable(spawn.Role, floor),
                            $"Seed {seed} room '{room.RoomId}' placed {spawn.Role} on floor {floor}.");
                        counts[floor, (int)spawn.Role]++;
                    }
                }
            }

            foreach ((EncounterEnemyRole role, int introduction) in ExpectedIntroduction)
            {
                for (int floor = introduction; floor <= 3; floor++)
                {
                    Assert(counts[floor, (int)role] > 0,
                        $"{GeneratedSeedCount} seeds must place {role} on floor {floor}.");
                }
            }

            return $"{GeneratedSeedCount} seeds, combat rooms {rooms[1]}/{rooms[2]}/{rooms[3]}, " +
                   string.Join(", ", ExpectedIntroduction.Select(entry =>
                       $"{entry.Role} {counts[1, (int)entry.Role]}/{counts[2, (int)entry.Role]}/" +
                       $"{counts[3, (int)entry.Role]}"));
        }

        private static void ValidateQuickRanged()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week19Spawn2Setup.QuickRangedPrefabPath);
            MobileRangedEnemyController controller = prefab != null
                ? prefab.GetComponent<MobileRangedEnemyController>()
                : null;
            Assert(controller != null, "Curve-1 needs the 산사모-순수 Prefab.");
            Assert(Mathf.Approximately(controller.FourShotChance, Week27Curve1Setup.QuickFourShotChance) &&
                   Mathf.Approximately(controller.MaximumPredictionTime,
                       Week27Curve1Setup.QuickMaximumPredictionTime),
                $"산사모-순수 must never fire four shots and must not lead its aim, got " +
                $"{controller.FourShotChance} and {controller.MaximumPredictionTime}.");
            for (int index = 0; index <= 100; index++)
            {
                Assert(MobileRangedEnemyController.SelectBurstShotCount(index / 100f, controller.FourShotChance) == 3,
                    "Every 산사모-순수 burst must be three shots.");
            }

            Vector2 direction = MobileRangedEnemyController.CalculatePredictiveDirection(Vector2.zero,
                Vector2.right * 5.6f, Vector2.up * 3f, controller.ProjectileSpeed, controller.MaximumPredictionTime);
            Assert((direction - Vector2.right).sqrMagnitude < 0.000001f,
                "산사모-순수 must aim straight at the player's current position.");
        }

        private static string Signature(GeneratedFloorGraph graph) => string.Join("|", graph.Nodes
            .Where(node => node.Role == GeneratedRoomRole.Intermediate)
            .Select(node => $"{node.RoomId}:{node.EncounterId}:" + string.Join(";",
                node.ResolvedEncounterWaves.Select(wave => string.Join(",",
                    wave.Select(spawn => $"{spawn.Role}@{spawn.SpawnPointIndex}"))))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
