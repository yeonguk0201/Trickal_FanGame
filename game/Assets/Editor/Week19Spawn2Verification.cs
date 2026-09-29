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
    public static class Week19Spawn2Verification
    {
        private const int DistributionSeedCount = 10000;

        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Spawn-2 Weighted Spawn Candidates")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week19Spawn2Setup.Setup();
            Dictionary<string, string> guids = Week19Spawn2Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week19Spawn2Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Spawn-2 setup changed the Game Scene GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Spawn-2 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Spawn-2 Weighted Spawn Candidates")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.EncounterContentVersion == Week19Spawn2Setup.EncounterContentVersion,
                "Run Spawn-2 setup before verification.");

            ValidateQuickRangedPrefab(assembler.EnemyRoster);
            ValidateMobileRangedBehavior();
            ValidateDefinitions(generator);
            ValidateInvalidCandidateContracts(generator);
            ValidateWeightedSelection(generator);
            ValidateGeneratedPersistence(generator);
            ValidateRuntimeBinding(assembler);
            Debug.Log("Spawn-2 verification passed: the quick ranged placeholder circles while firing " +
                      "predictive 3/4-shot bursts at 60/40 weights with a 1.5-second rest; " +
                      "Small/Basic/Wide/Tall/Large rear slots " +
                      "select snipers at 10/20/50/50/60 weights; identical seeds persist identical per-point " +
                      "choices, every candidate appears, 256 generated graphs and runtime binding match stored " +
                      "waves, invalid weights/duplicates/boss candidates fail, and setup preserves all GUIDs.");
        }

        private static void ValidateQuickRangedPrefab(EncounterEnemyRoster roster)
        {
            GameObject quick = AssetDatabase.LoadAssetAtPath<GameObject>(Week19Spawn2Setup.QuickRangedPrefabPath);
            GameObject sniperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy4Setup.SniperPrefabPath);
            MobileRangedEnemyController quickController = quick != null
                ? quick.GetComponent<MobileRangedEnemyController>()
                : null;
            LongRangeSniperController sniper = sniperPrefab != null
                ? sniperPrefab.GetComponent<LongRangeSniperController>()
                : null;
            Assert(quickController != null && sniper != null &&
                   quick.GetComponent<RangedEnemyController>() == null &&
                   quick.GetComponent<LongRangeSniperController>() == null &&
                   quick.GetComponent<LineRenderer>() == null,
                "Spawn-2 quick ranged Prefab must exclusively use the moving ranged pattern.");
            Assert(Approximately(quickController.PreferredDistance, sniper.PreferredDistance * 0.7f) &&
                   Approximately(quickController.PreferredDistance, Week19Spawn2Setup.QuickPreferredDistance),
                "Quick ranged preferred distance must be exactly 70% of the sniper distance.");
            Assert(quickController.MoveSpeed > sniper.MoveSpeed &&
                   Approximately(quickController.ShotInterval, Week19Spawn2Setup.QuickShotInterval) &&
                   Approximately(quickController.BurstRestDuration, Week19Spawn2Setup.QuickBurstRestDuration) &&
                   Approximately(quickController.FourShotChance, Week19Spawn2Setup.QuickFourShotChance) &&
                   Approximately(quickController.MaximumPredictionTime,
                       Week19Spawn2Setup.QuickMaximumPredictionTime) &&
                   quickController.ProjectileDamageTier == EnemyDamageTier.Medium &&
                   sniper.ProjectileDamageTier == EnemyDamageTier.Heavy,
                "Quick ranged must move, predict, and burst-fire with the configured medium-damage contract.");
            Assert(roster.TryResolve(EncounterEnemyRole.Ranged, out GameObject resolved, out string error) &&
                   resolved == quick, error ?? "The Ranged role must resolve to the new quick ranged Prefab.");
        }

        private static void ValidateMobileRangedBehavior()
        {
            int threeShotBursts = 0;
            int fourShotBursts = 0;
            const int samples = 1000;
            for (int index = 0; index < samples; index++)
            {
                float roll = (index + 0.5f) / samples;
                if (MobileRangedEnemyController.SelectBurstShotCount(roll) == 3) threeShotBursts++;
                else fourShotBursts++;
            }
            Assert(threeShotBursts == 600 && fourShotBursts == 400 &&
                   MobileRangedEnemyController.SelectBurstShotCount(0.3999f) == 4 &&
                   MobileRangedEnemyController.SelectBurstShotCount(0.4f) == 3,
                "Moving ranged burst selection must use exact 60% three-shot and 40% four-shot weights.");

            Vector2 predictive = MobileRangedEnemyController.CalculatePredictiveDirection(
                Vector2.zero, Vector2.right * 5.6f, Vector2.up * 2f, 6.5f,
                Week19Spawn2Setup.QuickMaximumPredictionTime);
            Assert(predictive.x > 0.98f && predictive.y > 0.05f && predictive.y < 0.15f,
                "Moving ranged prediction must use only the configured short lead on a lateral target.");

            GameObject root = new("Spawn-2 moving ranged verification");
            List<EnemyProjectile> fired = new();
            try
            {
                Vector2 origin = Week18Obstacle0Verification.Origin;
                GameObject player = Week18Obstacle0Verification.CreatePlayer(root.transform,
                    origin + Vector2.right * Week19Spawn2Setup.QuickPreferredDistance);
                Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
                playerBody.linearVelocity = Vector2.up * 2f;
                Week18Obstacle0Verification.CreateEnemy<MobileRangedEnemyController>(root.transform,
                    out Rigidbody2D enemyBody, out MobileRangedEnemyController controller);
                controller.Configure(Week19Spawn2Setup.QuickMoveSpeed,
                    Week19Spawn2Setup.QuickDetectionRange,
                    Week19Spawn2Setup.QuickPreferredDistance, 0.8f, 0.35f,
                    Week19Spawn2Setup.QuickShotInterval,
                    Week19Spawn2Setup.QuickBurstRestDuration,
                    Week19Spawn2Setup.QuickFourShotChance, 6.5f,
                    EnemyDamageTier.Medium, 5f,
                    Week19Spawn2Setup.QuickMaximumPredictionTime);
                controller.SetTarget(player.transform);
                controller.ProjectileFired += fired.Add;
                GameObject obstacle = Week18Obstacle0Verification.CreateObstacle(root.transform,
                    origin + Vector2.right * (Week19Spawn2Setup.QuickPreferredDistance * 0.5f));
                Physics2D.SyncTransforms();

                controller.TickBehavior(0f);
                Assert(fired.Count == 0 && enemyBody.linearVelocity.sqrMagnitude > 0.5f,
                    "Moving ranged must hold predictive fire and navigate while an obstacle blocks the line.");
                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics2D.SyncTransforms();
                controller.TickBehavior(0f);
                int selectedBurstSize = controller.ShotsRemaining + 1;
                Assert((selectedBurstSize == 3 || selectedBurstSize == 4) && fired.Count == 1 &&
                       enemyBody.linearVelocity.y > 0f && fired[0].Velocity.y > 0f,
                    "Moving ranged must circle and fire a predictive shot without stopping.");
                for (int shotIndex = 1; shotIndex < selectedBurstSize; shotIndex++)
                    controller.TickBehavior(shotIndex * Week19Spawn2Setup.QuickShotInterval);
                Assert(fired.Count == selectedBurstSize && enemyBody.linearVelocity == Vector2.zero,
                    "Moving ranged must finish its selected burst and stop immediately after the last shot.");

                float lastShotTime = (selectedBurstSize - 1) * Week19Spawn2Setup.QuickShotInterval;
                controller.TickBehavior(lastShotTime + Week19Spawn2Setup.QuickBurstRestDuration - 0.01f);
                Assert(fired.Count == selectedBurstSize && enemyBody.linearVelocity == Vector2.zero &&
                       controller.IsBurstResting(
                           lastShotTime + Week19Spawn2Setup.QuickBurstRestDuration - 0.01f),
                    "Moving ranged must remain stopped until its 1.5-second burst rest ends.");
                controller.TickBehavior(lastShotTime + Week19Spawn2Setup.QuickBurstRestDuration);
                Assert(fired.Count == selectedBurstSize + 1 && enemyBody.linearVelocity.y > 0f,
                    "Moving ranged must resume circling with the next burst after its 1.5-second rest.");

                controller.NotifyBlocked();
                controller.TickBehavior(lastShotTime + Week19Spawn2Setup.QuickBurstRestDuration + 0.01f);
                Assert(controller.OrbitDirection == -1 && enemyBody.linearVelocity.y < 0f,
                    "Moving ranged must keep its orbit direction until blocked, then reverse it.");
            }
            finally
            {
                foreach (EnemyProjectile projectile in fired.Where(projectile => projectile != null))
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateDefinitions(FloorGenerator generator)
        {
            Assert(generator.EncounterDefinitions.Count == Week19Spawn2Setup.Profiles.Length * 2,
                "Spawn-2 must register exactly pressure and crossfire definitions for every profile.");
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                foreach (string pattern in new[] { "pressure", "crossfire" })
                {
                    string id = Week19Spawn2Setup.EncounterId(spec.ProfileId, pattern);
                    EncounterDefinition definition = generator.EncounterDefinitions.SingleOrDefault(candidate =>
                        candidate != null && candidate.EncounterId == id);
                    Assert(definition != null, $"Spawn-2 is missing Encounter '{id}'.");
                    Assert(definition.TryValidate(out string error), error);
                    Assert(definition.AllowedProfiles.Count == 1 &&
                           definition.AllowedProfiles[0].ProfileId == spec.ProfileId &&
                           definition.Waves.Count == 2,
                        $"Encounter '{id}' must be profile-specific and contain two waves.");
                    EncounterSpawnRule rear = pattern == "pressure"
                        ? definition.Waves[0].SpawnRules[1]
                        : definition.Waves[0].SpawnRules[0];
                    Assert(rear.HasWeightedCandidates &&
                           Weight(rear, EncounterEnemyRole.Ranged) == spec.QuickRangedWeight &&
                           Weight(rear, EncounterEnemyRole.Sniper) == spec.SniperWeight,
                        $"Encounter '{id}' does not contain the {spec.QuickRangedWeight}/{spec.SniperWeight} rear weights.");
                }
            }

            Assert(generator.EncounterDefinitions.All(definition =>
                    definition.EncounterId.StartsWith(Week19Spawn2Setup.EncounterPrefix + "-", StringComparison.Ordinal)),
                "Spawn-2 must not reuse legacy Encounter IDs after changing selection rules.");
        }

        private static void ValidateInvalidCandidateContracts(FloorGenerator generator)
        {
            RoomProfile profile = generator.RoomTemplates.First(template =>
                template.SupportsRoomType(RoomType.Normal)).Profile;
            foreach ((string label, EncounterEnemyCandidate[] candidates) in new[]
                     {
                         ("zero weight", new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 0) }),
                         ("duplicate role", new[]
                         {
                             new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 1),
                             new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 2),
                         }),
                         ("boss candidate", new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Boss, 1) }),
                     })
            {
                EncounterDefinition invalid = ScriptableObject.CreateInstance<EncounterDefinition>();
                try
                {
                    invalid.Configure("spawn-invalid-candidates", new[] { profile }, 1, 3, 1.5f, 2f,
                        new[]
                        {
                            new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                                EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                                new[] { new EncounterSpawnRule(candidates, 1, null, "all") }),
                        }, EncounterClearCondition.AllWavesCleared);
                    Assert(!invalid.TryValidate(out string error) && !string.IsNullOrWhiteSpace(error),
                        $"Spawn-2 must reject a {label} candidate list.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(invalid);
                }
            }
        }

        private static void ValidateWeightedSelection(FloorGenerator generator)
        {
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                EncounterDefinition definition = generator.EncounterDefinitions.Single(candidate =>
                    candidate.EncounterId == Week19Spawn2Setup.EncounterId(spec.ProfileId, "pressure"));
                RoomTemplateDefinition template = generator.RoomTemplates.First(candidate =>
                    candidate.Profile.ProfileId == spec.ProfileId && candidate.SupportsRoomType(RoomType.Normal));
                int sniperCount = 0;
                for (int seed = 1; seed <= DistributionSeedCount; seed++)
                {
                    Assert(definition.TryResolveWave(template, 1, null, 0, seed,
                        out ResolvedEncounterSpawn[] first, out string error), error);
                    Assert(definition.TryResolveWave(template, 1, null, 0, seed,
                        out ResolvedEncounterSpawn[] repeat, out error), error);
                    Assert(Signature(first) == Signature(repeat),
                        $"Profile '{spec.ProfileId}' changed candidates for identical seed {seed}.");
                    Assert(first.All(spawn => template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role)),
                        $"Profile '{spec.ProfileId}' selected a role-incompatible SpawnPoint.");
                    EncounterEnemyRole rearRole = first[1].Role;
                    Assert(rearRole == EncounterEnemyRole.Ranged || rearRole == EncounterEnemyRole.Sniper,
                        $"Profile '{spec.ProfileId}' rear slot selected '{rearRole}'.");
                    if (rearRole == EncounterEnemyRole.Sniper) sniperCount++;
                }

                float observed = sniperCount / (float)DistributionSeedCount;
                float expected = spec.SniperWeight / 100f;
                Assert(sniperCount > 0 && sniperCount < DistributionSeedCount &&
                       Mathf.Abs(observed - expected) <= 0.025f,
                    $"Profile '{spec.ProfileId}' expected {expected:P0} snipers but observed {observed:P1}.");
            }
        }

        private static void ValidateGeneratedPersistence(FloorGenerator generator)
        {
            HashSet<EncounterEnemyRole> seen = new();
            for (int seed = 1; seed <= 256; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                GeneratedRoomNode[] firstRooms = first.Nodes.Where(node =>
                    node.Role == GeneratedRoomRole.Intermediate).ToArray();
                GeneratedRoomNode[] repeatRooms = repeat.Nodes.Where(node =>
                    node.Role == GeneratedRoomRole.Intermediate).ToArray();
                Assert(firstRooms.Length == repeatRooms.Length, "Identical seeds changed combat room count.");
                for (int index = 0; index < firstRooms.Length; index++)
                {
                    GeneratedRoomNode room = firstRooms[index];
                    GeneratedRoomNode repeated = repeatRooms[index];
                    Assert(room.EncounterId == repeated.EncounterId &&
                           WaveSignature(room.ResolvedEncounterWaves) ==
                           WaveSignature(repeated.ResolvedEncounterWaves),
                        $"Seed {seed} room '{room.RoomId}' did not persist the same candidate selection.");
                    Assert(room.ResolvedEncounterWaves.Count == room.Encounter.Waves.Count,
                        $"Seed {seed} room '{room.RoomId}' did not store every resolved wave.");
                    foreach (ResolvedEncounterSpawn spawn in room.ResolvedEncounterWaves.SelectMany(wave => wave))
                    {
                        Assert(room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role),
                            $"Seed {seed} room '{room.RoomId}' stored an incompatible candidate.");
                        seen.Add(spawn.Role);
                    }
                }
            }

            Assert(seen.IsSupersetOf(new[]
                {
                    EncounterEnemyRole.Chaser, EncounterEnemyRole.FastChaser,
                    EncounterEnemyRole.Ranged, EncounterEnemyRole.Sniper, EncounterEnemyRole.Charging,
                }), "256 generated seeds must expose every normal enemy candidate.");
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(73, out string error), error);
            Dictionary<string, GeneratedRoomNode> generated = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!generated.TryGetValue(room.Node.RoomId, out GeneratedRoomNode node)) continue;
                Assert(room.Controller.EncounterWaves.Count == node.ResolvedEncounterWaves.Count,
                    $"Runtime room '{node.RoomId}' wave count differs from its stored candidate selection.");
                for (int waveIndex = 0; waveIndex < node.ResolvedEncounterWaves.Count; waveIndex++)
                {
                    ResolvedEncounterSpawn[] stored = node.ResolvedEncounterWaves[waveIndex];
                    EncounterRuntimeWave runtime = room.Controller.EncounterWaves[waveIndex];
                    Assert(runtime.EnemyPrefabs.Count == stored.Length, "Runtime candidate count drifted.");
                    for (int index = 0; index < stored.Length; index++)
                    {
                        Assert(assembler.EnemyRoster.TryResolve(stored[index].Role,
                                   out GameObject expected, out error) &&
                               runtime.EnemyPrefabs[index] == expected,
                            $"Runtime room '{node.RoomId}' rerolled stored candidate {index + 1}.");
                    }
                }
            }
        }

        private static int Weight(EncounterSpawnRule rule, EncounterEnemyRole role) =>
            rule.Candidates.Single(candidate => candidate.EnemyRole == role).Weight;

        private static string Signature(IEnumerable<ResolvedEncounterSpawn> spawns) =>
            string.Join("|", spawns.Select(spawn => $"{spawn.Role}:{spawn.SpawnPointIndex}"));

        private static string WaveSignature(IEnumerable<ResolvedEncounterSpawn[]> waves) =>
            string.Join("/", waves.Select(Signature));

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
