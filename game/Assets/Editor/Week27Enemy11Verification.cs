using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Enemy11Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-7600f, 7600f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;
        private const float TimeTolerance = 0.001f;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Enemy-11 Jyubi King")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[] { "pressure", "crossfire", "swarm", "elite-pair", "jyubi-swarm" }
                    .Select(pattern => Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
                .Append(Week23Enemy6Setup.PrefabPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Dictionary<string, string> artwork = FairyKingdomArtworkVerification.SnapshotBoundArtwork();
            Week27Enemy11Setup.Setup();
            Dictionary<string, string> created = Week27Enemy11Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week27Enemy11Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-11 setup changed or lost the GUID for {entry.Key}.");
            }

            // Only the new Prefab may gain a drawing; every other bound drawing stays as it was.
            FairyKingdomArtworkVerification.RequireBoundArtworkUnchanged(artwork, "Enemy-11 setup");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy11Setup.PrefabPath);
            Assert(prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 &&
                   prefab.GetComponents<JyubiKingController>().Length == 1 &&
                   prefab.GetComponents<EnemyFlight>().Length == 1 &&
                   prefab.GetComponents<ContactDamage>().Length == 1 &&
                   prefab.GetComponents<EnemyContactRecoil>().Length == 1,
                "Re-running Enemy-11 setup must not duplicate the Jyubi king Prefab's components.");

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-11 Jyubi King")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Enemy11Setup.EncounterContentVersion,
                "Run Enemy-11 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy11Setup.PrefabPath);
            GameObject jyubi = AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath);
            Assert(prefab != null && jyubi != null, $"Enemy-11 needs {Week27Enemy11Setup.PrefabPath} and the Jyubi.");
            ValidatePrefab(prefab, jyubi);
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateMovement(prefab);
            ValidateSummonsInRoom(prefab, jyubi);
            Debug.Log($"Enemy-11 verification passed: {Week27Enemy11Setup.DisplayName} is the Jyubi drawing at twice " +
                      "its size with 50 health, light contact damage and the Jyubi bounce. It flies at 3, backs off " +
                      "inside 3 and closes in beyond 5 of the player while circling and wobbling, waits out its spawn " +
                      "delay, then 1.5 s later and every 3 s glows 0.6 s and releases two Jyubi that join the room; " +
                      "it stops at eight of its own, tops up one when one dies, and the Jyubi stay and hold the room " +
                      "after it dies. Threat 6, floor 3 only, jyubi-nest 14~17 in every profile; " +
                      $"{GeneratedSeedCount} seeds place it reproducibly ({counts}), and runtime seed {runtimeSeed} " +
                      "binds its Prefab.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-11 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads the Encounter catalog, the threat table, the enemy roster, the floor pool, the
            // Jyubi Prefab or the enemy knockback table. Each starts from a freshly loaded scene, and every check runs
            // after a failure.
            (string Name, Action Run)[] checks =
            {
                ("Week27Enemy10", Week27Enemy10Verification.Verify),
                ("Week27Curve1", Week27Curve1Verification.Verify),
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
                ("Week23Enemy6", Week23Enemy6Verification.Verify),
                ("Week23Passive1", Week23Passive1Verification.Verify),
                ("Week19Spawn1", Week19Spawn1Verification.Verify),
                ("Week19Spawn2", Week19Spawn2Verification.Verify),
                ("Week19Difficulty1", Week19Difficulty1Verification.Verify),
                ("Week19Encounter4", Week19Encounter4Verification.Verify),
                ("Week20Floor1", Week20Floor1Verification.Verify),
                ("FairyKingdomArtwork", FairyKingdomArtworkVerification.Verify),
                ("Week20DevPanel", Week20DevPanelVerification.Verify),
            };
            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                    run();
                    Debug.Log($"[Enemy-11 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Enemy-11 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Enemy-11 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Enemy-11 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab, GameObject jyubi)
        {
            Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0 &&
                   prefab.name == Week27Enemy11Setup.PrefabName &&
                   prefab.transform.localScale == Vector3.one * Week27Enemy11Setup.Scale &&
                   Mathf.Approximately(prefab.transform.localScale.x, jyubi.transform.localScale.x * 2f),
                "The Jyubi king must be the Jyubi at twice its scale.");
            Assert(prefab.GetComponent<EnemyChase>() == null && prefab.GetComponent<EnemyFlight>() != null &&
                   prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponent<EnemyContactRecoil>() != null &&
                   prefab.GetComponent<MeleeEnemyAttack>() == null,
                "The Jyubi king must fly on its own controller (no straight chase) and keep the Jyubi bounce.");
            Assert(Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, 50f) &&
                   Mathf.Approximately(prefab.GetComponent<KnockbackReceiver>().KnockbackWeight, 1.5f),
                "The Jyubi king must have 50 health and knockback weight 1.5.");
            ContactDamage contact = prefab.GetComponent<ContactDamage>();
            Assert(contact != null && contact.DamageTier == EnemyDamageTier.Light && !contact.HalfHeartDamage &&
                   Mathf.Approximately(contact.Cooldown, 1f),
                "The Jyubi king's touch must deal light damage (not half a heart) once a second.");

            JyubiKingController controller = prefab.GetComponent<JyubiKingController>();
            Assert(controller != null && controller.SummonPrefab == jyubi &&
                   Mathf.Approximately(controller.MoveSpeed, 3f) &&
                   Mathf.Approximately(controller.PreferredDistance, 4f) &&
                   Mathf.Approximately(controller.DistanceBand, 1f) &&
                   Mathf.Approximately(controller.FirstSummonDelay, 1.5f) &&
                   Mathf.Approximately(controller.SummonInterval, 3f) &&
                   Mathf.Approximately(controller.SummonTelegraph, 0.6f) &&
                   controller.SummonCount == 2 && controller.MaxSummons == 8 &&
                   Mathf.Approximately(controller.SummonSideOffset, 0.7f),
                "The Jyubi king must fly at 3 about 4 from the player and release two Jyubi every 3 s after a " +
                "0.6 s glow, at most eight of its own.");

            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            FairyKingdomArtworkView jyubiView = jyubi.GetComponent<FairyKingdomArtworkView>();
            Assert(view != null && jyubiView != null && view.ArtworkId == "jyubi" && view.Visual != null &&
                   view.Visual.sprite == jyubiView.Visual.sprite && view.Visual.color == Color.white &&
                   Mathf.Approximately(view.LocalWidth, jyubiView.LocalWidth),
                "The Jyubi king must show the Jyubi drawing untinted, enlarged only by its root scale.");

            // 쥬비 itself is unchanged: one hit kills it and its touch takes half a heart.
            Assert(Mathf.Approximately(jyubi.GetComponent<Health>().MaxHealth, Week23Enemy6Setup.MaxHealth) &&
                   jyubi.GetComponent<ContactDamage>().HalfHeartDamage &&
                   jyubi.GetComponent<EnemyChase>() != null && jyubi.GetComponent<JyubiKingController>() == null,
                "The Jyubi Prefab must stay a one-hit, half-heart chaser.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.JyubiKing, out int threat) && threat == 6 &&
                   threat == Week27Enemy11Setup.Threat, "The Jyubi king's threat score must be 6.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.JyubiKing, out GameObject bound, out error) &&
                   bound == prefab, $"The roster must bind the Jyubi king role to its Prefab. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.JyubiKing) ==
                   SpawnPointPlacementRole.RearFiring,
                "The Jyubi king must start on a rear SpawnPoint.");
            Assert(EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.JyubiKing) == 3 &&
                   Week27Curve1Setup.MinimumFloor(Week27Enemy11Setup.Pattern) == 3,
                "The Jyubi king and its nest must first appear on floor 3.");

            int nests = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidate(out error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                bool holds = definition.Waves.SelectMany(wave => wave.SpawnRules).SelectMany(rule => rule.Candidates)
                    .Any(candidate => candidate.EnemyRole == EncounterEnemyRole.JyubiKing);
                bool nest = Week27Enemy11Setup.IsNest(definition);
                Assert(holds == nest,
                    $"Only the {Week27Enemy11Setup.Pattern} Encounters may hold the Jyubi king, but " +
                    $"'{definition.EncounterId}' does not follow that.");
                if (!nest) continue;
                nests++;

                Assert(definition.MinimumFloor == 3 && definition.MaximumFloor == 3 &&
                       definition.AllowedProfiles.Count == 1 && definition.Waves.Count == 2,
                    $"'{definition.EncounterId}' must be a two-wave floor-3 Encounter of one profile.");
                EncounterSpawnRule[] first = definition.Waves[0].SpawnRules.ToArray();
                EncounterSpawnRule[] second = definition.Waves[1].SpawnRules.ToArray();
                Assert(first.Length == 2 && first[0].Count == 1 && first[0].Candidates.Count == 1 &&
                       first[0].Candidates[0].EnemyRole == EncounterEnemyRole.JyubiKing &&
                       first[1].Count == 2 && first[1].Candidates.All(candidate =>
                           candidate.EnemyRole == EncounterEnemyRole.Jyubi),
                    $"'{definition.EncounterId}' must open with one Jyubi king and two Jyubi.");
                Assert(second.Length == 1 && second[0].Count == 3 && second[0].Candidates.Count == 2 &&
                       Weight(second[0], EncounterEnemyRole.Chaser) == 70 &&
                       Weight(second[0], EncounterEnemyRole.FastChaser) == 30,
                    $"'{definition.EncounterId}' must follow with three chasers weighed 70/30.");
                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out error) &&
                       minimum == 14 && maximum == 17 && definition.DeclaredMinimumThreat == 14 &&
                       definition.DeclaredMaximumThreat == 17,
                    $"'{definition.EncounterId}' must compute and declare threat 14~17. {error}");
                for (int floor = 1; floor <= 2; floor++)
                {
                    Assert(!table.TryComputeThreatRange(definition, floor, out _, out _, out error),
                        $"'{definition.EncounterId}' must have no floor-{floor} composition.");
                }
            }

            Assert(nests == Week19Spawn2Setup.Profiles.Length,
                $"Every profile must have one {Week27Enemy11Setup.Pattern} Encounter, found {nests}.");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            int nestRooms = 0, combatRooms = 0, floorThreeRooms = 0;
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
                    combatRooms++;
                    if (room.FloorNumber == 3) floorThreeRooms++;
                    ResolvedEncounterSpawn[] kings = room.ResolvedEncounterWaves.SelectMany(wave => wave)
                        .Where(spawn => spawn.Role == EncounterEnemyRole.JyubiKing).ToArray();
                    bool nest = Week27Enemy11Setup.IsNest(room.EncounterId);
                    Assert(nest == kings.Length > 0,
                        $"Seed {seed} room '{room.RoomId}' placed a Jyubi king outside a nest, or a nest without one.");
                    if (!nest) continue;
                    Assert(room.FloorNumber == 3 && kings.Length == 1 &&
                           room.ResolvedEncounterWaves.Count == 2 &&
                           room.ResolvedEncounterWaves[0].Count(spawn => spawn.Role == EncounterEnemyRole.Jyubi) == 2 &&
                           room.ResolvedEncounterWaves[1].Length == 3 &&
                           room.ResolvedEncounterWaves[1].All(spawn => spawn.Role == EncounterEnemyRole.Chaser ||
                                                                      spawn.Role == EncounterEnemyRole.FastChaser),
                        $"Seed {seed} room '{room.RoomId}' resolved a wrong Jyubi nest.");
                    ResolvedEncounterSpawn king = kings[0];
                    Assert(room.Template.SupportsEnemyRole(king.SpawnPointIndex, king.Role) &&
                           (room.Template.SpawnPointRoles[king.SpawnPointIndex] &
                            SpawnPointPlacementRole.RearFiring) != 0,
                        $"Seed {seed} room '{room.RoomId}' put the Jyubi king off a rear point.");
                    Assert(room.HasDifficulty && room.Difficulty.Score >= 14,
                        $"Seed {seed} room '{room.RoomId}' must score its Jyubi nest at 14 or more.");
                    nestRooms++;
                    if (runtimeSeed == 0) runtimeSeed = seed;
                }
            }

            Assert(nestRooms > 0, $"{GeneratedSeedCount} seeds must select a Jyubi nest on floor 3.");
            return $"{nestRooms} nests in {floorThreeRooms} floor-3 of {combatRooms} combat rooms";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 3;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.JyubiKing), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} Jyubi kings, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its Jyubi nest rooms.");
        }

        // Holds its distance band, keeps buzzing (the path changes over time) and never beats its speed.
        private static void ValidateMovement(GameObject prefab)
        {
            Assert(Mathf.Approximately(JyubiKingController.DistancePull(4f, 4f, 1f), 0f) &&
                   Mathf.Approximately(JyubiKingController.DistancePull(3f, 4f, 1f), 0f) &&
                   Mathf.Approximately(JyubiKingController.DistancePull(5f, 4f, 1f), 0f) &&
                   Mathf.Approximately(JyubiKingController.DistancePull(2f, 4f, 1f), 1f) &&
                   Mathf.Approximately(JyubiKingController.DistancePull(6f, 4f, 1f), -1f) &&
                   Mathf.Approximately(JyubiKingController.DistancePull(5.5f, 4f, 1f), -0.5f),
                "The Jyubi king must hold 3~5 from the player, backing off closer and closing in farther.");

            GameObject root = new("Enemy-11 Movement Verification");
            try
            {
                // Beyond its 20 detection range at first.
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 30f);
                Health king = Spawn(prefab, root.transform, Origin);
                JyubiKingController controller = king.GetComponent<JyubiKingController>();
                EnemyBehaviorContext behavior = king.GetComponent<EnemyBehaviorContext>();
                Rigidbody2D body = king.GetComponent<Rigidbody2D>();

                float far = 0f, near = 0f;
                HashSet<string> headings = new(StringComparer.Ordinal);
                for (int step = 0; step < 400; step++)
                {
                    float time = step * 0.05f;
                    Vector2 farVelocity = controller.DesiredVelocity(Vector2.up * 10f, Vector2.zero, time);
                    Vector2 nearVelocity = controller.DesiredVelocity(Vector2.up * 1f, Vector2.zero, time);
                    Assert(farVelocity.magnitude <= controller.MoveSpeed + 0.0001f &&
                           nearVelocity.magnitude <= controller.MoveSpeed + 0.0001f,
                        "The Jyubi king must never fly faster than its move speed.");
                    far += farVelocity.y;
                    near += nearVelocity.y;
                    Vector2 held = controller.DesiredVelocity(Vector2.up * 4f, Vector2.zero, time);
                    headings.Add(Mathf.RoundToInt(Mathf.Atan2(held.y, held.x) * Mathf.Rad2Deg / 45f).ToString());
                }

                Assert(far < 0f && near > 0f,
                    "On average the Jyubi king must close in from far away and back off when the player is close.");
                Assert(headings.Count >= 4,
                    "Holding its distance, the Jyubi king must keep buzzing in changing directions, not hover.");

                // Not alerted yet, it stays put; alerted and past the spawn delay, it moves.
                Physics2D.SyncTransforms();
                body.linearVelocity = Vector2.zero;
                behavior.Disengage();
                controller.TickMovement(Time.time);
                Assert(body.linearVelocity == Vector2.zero && !behavior.IsAlerted,
                    "The Jyubi king must not move before it notices the player.");
                player.transform.position = Origin + Vector2.down * 10f;
                Physics2D.SyncTransforms();
                behavior.BeginCombat(player.transform);
                behavior.BeginSpawnGrace(Time.time);
                controller.TickMovement(Time.time);
                Assert(body.linearVelocity == Vector2.zero, "The Jyubi king must wait out its spawn delay.");
                ClearSpawnDelay(behavior);
                controller.TickMovement(Time.time);
                Assert(body.linearVelocity.sqrMagnitude > 0f &&
                       body.linearVelocity.magnitude <= controller.MoveSpeed + 0.0001f,
                    "An alerted Jyubi king must fly.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // The Jyubi king spawns through the room's own path. Its Jyubi join the room, stop at eight, top up after a
        // death, and stay and hold the room after it dies.
        private static void ValidateSummonsInRoom(GameObject prefab, GameObject jyubi)
        {
            GameObject root = new("Enemy-11 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 40f;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                GameObject point = new("Enemy-11 Spawn");
                point.transform.SetParent(root.transform);
                point.transform.position = (Vector2)root.transform.position + Vector2.right * 4f;
                Transform[] points = { point.transform };
                room.Configure(3, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[] { new EncounterRuntimeWave(new[] { prefab }, points) });
                // Edit mode runs no Awake, so the room's own spawn hook wakes the king as Awake would. The room
                // alerts it right after this hook.
                room.EnemySpawned += spawned =>
                {
                    InvokeAwake(spawned.GetComponent<Health>());
                    InvokeAwake(spawned.GetComponent<KnockbackReceiver>());
                    InvokeAwake(spawned.GetComponent<EnemyBehaviorContext>());
                    InvokeAwake(spawned.GetComponent<JyubiKingController>());
                };
                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 6f);
                room.BeginCombat(player);
                JyubiKingController controller = root.GetComponentInChildren<JyubiKingController>();
                Health king = controller != null ? controller.GetComponent<Health>() : null;
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 1 && king != null &&
                       controller.GetComponent<EnemyBehaviorContext>().IsAlerted,
                    "The room must hold one alerted Jyubi king.");

                float now = Time.time;
                controller.Tick(now);
                Assert(!controller.IsSummoning && Mathf.Abs(controller.NextSummonAt - (now + 1.5f)) < TimeTolerance,
                    "The Jyubi king's first summon must come 1.5 s after it starts acting.");
                controller.Tick(now + 1.49f);
                Assert(!controller.IsSummoning, "The Jyubi king must not summon before its first delay.");
                controller.Tick(now + 1.5f);
                Assert(controller.IsSummoning && room.AliveEnemyCount == 1,
                    "The Jyubi king must start glowing when its summon comes due.");
                controller.Tick(now + 1.8f);
                Assert(controller.IsSummoning && controller.Visual != null && controller.Visual.color != Color.white &&
                       room.AliveEnemyCount == 1,
                    "Half way through the 0.6 s glow the Jyubi king must glow and hold its Jyubi back.");
                controller.Tick(now + 2.11f);
                Health[] firstPair = controller.LastSummons.ToArray();
                Assert(!controller.IsSummoning && controller.Visual.color == Color.white && firstPair.Length == 2 &&
                       controller.AliveSummonCount == 2 && room.AliveEnemyCount == 3,
                    "After the glow the Jyubi king must release two Jyubi into its room, got summoning " +
                    $"{controller.IsSummoning}, color {controller.Visual.color}, released {firstPair.Length}, alive " +
                    $"{controller.AliveSummonCount}, room {room.AliveEnemyCount}.");
                Vector3 kingPosition = controller.transform.position;
                Assert(firstPair.All(summon => summon.GetComponent<EnemyFlight>() != null &&
                                               Mathf.Approximately(summon.MaxHealth, Week23Enemy6Setup.MaxHealth) &&
                                               summon.transform.parent == root.transform &&
                                               summon.GetComponent<EnemyBehaviorContext>().IsAlerted) &&
                       firstPair.Any(summon => Vector2.Distance(summon.transform.position,
                           kingPosition + Vector3.left * 0.7f) < 0.001f) &&
                       firstPair.Any(summon => Vector2.Distance(summon.transform.position,
                           kingPosition + Vector3.right * 0.7f) < 0.001f),
                    "The two Jyubi must be alerted Jyubi of the room, 0.7 to the king's left and right.");

                // Every 3 s after the first: 4, 6, 8 alive. The fourth summon comes due at the cap and is skipped.
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    // Each summon is timed from when the previous one started (1.5 s + 0.01 margin, then 3 s each).
                    float due = now + 1.5f + cycle * 3f + cycle * 0.01f;
                    controller.Tick(due - 0.02f);
                    Assert(!controller.IsSummoning, $"Summon {cycle + 1} must wait for its 3 s interval.");
                    controller.Tick(due + 0.01f);
                    controller.Tick(due + 0.62f);
                    Assert(controller.AliveSummonCount == 2 + cycle * 2 && room.AliveEnemyCount == 3 + cycle * 2,
                        $"Summon {cycle + 1} must bring the Jyubi to {2 + cycle * 2}.");
                }

                float capped = now + 1.5f + 4 * 3f + 0.05f;
                controller.Tick(capped);
                Assert(!controller.IsSummoning && controller.AliveSummonCount == 8 &&
                       Mathf.Abs(controller.NextSummonAt - (capped + 3f)) < TimeTolerance,
                    "With eight of its Jyubi alive the Jyubi king must skip a summon and try again 3 s later.");

                firstPair[0].TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(controller.AliveSummonCount == 7 && room.AliveEnemyCount == 8,
                    "A killed Jyubi must leave the king's count and the room.");
                controller.Tick(capped + 3.01f);
                controller.Tick(capped + 3.62f);
                Assert(controller.LastSummons.Count == 1 && controller.AliveSummonCount == 8 &&
                       room.AliveEnemyCount == 9,
                    "Below the cap the Jyubi king must release only enough Jyubi to reach eight.");

                List<Health> remaining = root.GetComponentsInChildren<Health>()
                    .Where(health => health != king && health != player && !health.IsDead).ToList();
                king.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                controller.Tick(capped + 30f);
                Assert(king.IsDead && room.State == RoomState.Combat && room.AliveEnemyCount == 8 &&
                       remaining.Count == 8 && remaining.All(health => !health.IsDead),
                    "When the Jyubi king dies its eight Jyubi must stay and hold the room, and it summons no more.");
                foreach (Health summon in remaining)
                    summon.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "The room must clear once the king's Jyubi are gone too.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static Health Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            InvokeAwake(instance.GetComponent<Health>());
            InvokeAwake(instance.GetComponent<KnockbackReceiver>());
            InvokeAwake(instance.GetComponent<EnemyBehaviorContext>());
            InvokeAwake(instance.GetComponent<JyubiKingController>());
            return instance.GetComponent<Health>();
        }

        private static void ClearSpawnDelay(EnemyBehaviorContext behavior)
        {
            typeof(EnemyBehaviorContext)
                .GetField("spawnActionReadyTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(behavior, float.NegativeInfinity);
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Enemy-11 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            PlayerFeet.Ensure(player, out _);
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = player.GetComponent<Health>();
            InvokeAwake(health);
            health.EnableHealthUnits();
            return health;
        }

        private static void InvokeAwake(Component component)
        {
            component?.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(component, null);
        }

        private static int Weight(EncounterSpawnRule rule, EncounterEnemyRole role) =>
            rule.Candidates.Where(candidate => candidate.EnemyRole == role).Sum(candidate => candidate.Weight);

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
