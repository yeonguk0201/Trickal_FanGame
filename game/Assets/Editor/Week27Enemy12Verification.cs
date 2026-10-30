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
    public static class Week27Enemy12Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-8000f, 8000f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;
        private const float TimeTolerance = 0.001f;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Enemy-12 Lobbing Enemy")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[] { "pressure", "crossfire", "swarm", "elite-pair", "jyubi-swarm", "jyubi-nest" }
                    .Select(pattern => Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
                .Append(Week27Enemy12Setup.ArtworkPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week27Enemy12Setup.MotionAtlasPath).Append(Week27Enemy12Setup.ProjectilePath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week15Enemy0Setup.RangedPrefabPath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            // Enemy-12's own Prefab is the one whose drawing setup may change (placeholder -> 저혈당 요정-광기); the
            // verification below checks its drawing directly.
            Dictionary<string, string> artwork = FairyKingdomArtworkVerification.SnapshotBoundArtwork()
                .Where(entry => !entry.Key.StartsWith(Week27Enemy12Setup.PrefabPath + ":", StringComparison.Ordinal))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            Week27Enemy12Setup.Setup();
            Dictionary<string, string> created = Week27Enemy12Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week27Enemy12Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-12 setup changed or lost the GUID for {entry.Key}.");
            }

            // Only the new Prefab may gain a drawing; every other bound drawing stays as it was.
            FairyKingdomArtworkVerification.RequireBoundArtworkUnchanged(artwork, "Enemy-12 setup");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy12Setup.PrefabPath);
            Assert(prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 &&
                   prefab.GetComponents<LobbingEnemyController>().Length == 1 &&
                   prefab.GetComponents<LobbingFairyMotion>().Length == 1 &&
                   prefab.GetComponents<EnemyBehaviorContext>().Length == 1,
                "Re-running Enemy-12 setup must not duplicate the lobber Prefab's components.");

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-12 Lobbing Enemy")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Enemy12Setup.EncounterContentVersion,
                "Run Enemy-12 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy12Setup.PrefabPath);
            Assert(prefab != null, $"Enemy-12 needs {Week27Enemy12Setup.PrefabPath}.");
            ValidatePrefab(prefab);
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateThrowAndLanding(prefab);
            ValidateOverObstacle(prefab);
            ValidateMissAndOthers(prefab);
            ValidateDistanceKeeping(prefab);
            ValidateWindUpCancel(prefab);
            ValidateMotion(prefab);
            ValidateShadowAim(prefab);
            ValidateRoomTransitionClears(prefab);
            ValidateRoomClears(prefab);
            Debug.Log($"Enemy-12 verification passed: {Week27Enemy12Setup.DisplayName} has 30 health, speed 2, no " +
                      "contact damage; not before it is alerted or during the spawn delay, it winds up 0.4 s inside " +
                      "3.5~7 of the player and lobs to where the player's shadow lay (1 s flight, a red 1.5 x 0.75 ellipse shown " +
                      "the whole way, no collider in the air), then waits 2.4 s. It throws with an obstacle in the " +
                      "way without breaking it, hurts only a player whose feet are inside the ellipse at the floor's medium tier " +
                      "(not other enemies, not after the player steps out), still lands after the thrower dies, " +
                      "backs off inside 3.5, closes in beyond 7, and knockback calls off a wind-up. Room transitions " +
                      "remove lumps in the air with their marks. Threat 4, floor 3 only, crossfire rear weight 20; " +
                      $"{GeneratedSeedCount} seeds place it reproducibly ({counts}), runtime seed {runtimeSeed} " +
                      "binds its Prefab, and a room clears after its lobbers die. The user's motion shows ready/raised " +
                      "in the wind-up and release/follow-through after the throw, facing the target, and the 0.8 " +
                      "crowbar spins from 0.2 above the ground.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-12 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads the crossfire/pressure lists, the threat table, the enemy roster, the floor
            // pool, the knockback table or the dev panel. Each starts from a freshly loaded scene, and every check
            // runs after a failure.
            (string Name, Action Run)[] checks =
            {
                ("Week27Enemy11", Week27Enemy11Verification.Verify),
                ("Week27Enemy10", Week27Enemy10Verification.Verify),
                ("Week27Curve1", Week27Curve1Verification.Verify),
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
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
                    Debug.Log($"[Enemy-12 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Enemy-12 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Enemy-12 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Enemy-12 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0 &&
                   prefab.name == Week27Enemy12Setup.PrefabName && prefab.transform.localScale == Vector3.one &&
                   prefab.GetComponent<TestEnemy>() != null && prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponentInChildren<EnemyFeet>(true) != null,
                "The lobber must be a full-scale enemy with feet and a behaviour context.");
            Assert(prefab.GetComponent<RangedEnemyController>() == null &&
                   prefab.GetComponent<ContactDamage>() == null && prefab.GetComponent<MeleeEnemyAttack>() == null &&
                   prefab.GetComponent<EnemyDeathExplosion>() == null,
                "The lobber must hurt only with its lob: no straight shots, contact damage, melee or death blast.");
            Assert(Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, 30f) &&
                   Mathf.Approximately(prefab.GetComponent<KnockbackReceiver>().KnockbackWeight, 1f),
                "The lobber must have 30 health and knockback weight 1.");

            LobbingEnemyController controller = prefab.GetComponent<LobbingEnemyController>();
            Assert(controller != null &&
                   Mathf.Approximately(controller.MoveSpeed, 2f) &&
                   Mathf.Approximately(controller.DetectionRange, 10f) &&
                   Mathf.Approximately(controller.MinimumAttackDistance, 3.5f) &&
                   Mathf.Approximately(controller.MaximumAttackDistance, 7f) &&
                   Mathf.Approximately(controller.AttackInterval, 2.4f) &&
                   Mathf.Approximately(controller.WindUpDuration, 0.4f) &&
                   Mathf.Approximately(controller.FlightDuration, 1f) &&
                   Mathf.Approximately(controller.ArcHeight, 2.2f) &&
                   Mathf.Approximately(controller.LandingRadius, 0.75f) &&
                   controller.DamageTier == EnemyDamageTier.Medium,
                "The lobber must move at 2, detect at 10, throw from 3.5~7 every 2.4 s after a 0.4 s wind-up, and " +
                "land a radius-0.75 medium lob after a 1 s, 2.2-high flight.");

            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert(view != null && view.ArtworkId == Week27Enemy12Setup.ArtworkId && view.Visual == renderer &&
                   view.Mode == FairyKingdomArtworkMode.Fixed && renderer != null && renderer.color == Color.white &&
                   Mathf.Approximately(view.LocalWidth, Week27Enemy12Setup.ArtworkWidth) &&
                   AssetDatabase.GetAssetPath(renderer.sprite) == Week27Enemy12Setup.ArtworkPath,
                $"The lobber must show the user's '{Week27Enemy12Setup.ArtworkId}' drawing untinted at width 1.07.");

            LobbingFairyMotion motion = prefab.GetComponent<LobbingFairyMotion>();
            Assert(motion != null && AssetDatabase.GetAssetPath(motion.Atlas) == Week27Enemy12Setup.MotionAtlasPath &&
                   motion.NeutralContent.height > 0f &&
                   AssetDatabase.GetAssetPath(controller.ProjectileSprite) == Week27Enemy12Setup.ProjectilePath &&
                   Mathf.Approximately(controller.LaunchHeight, Week27Enemy12Setup.LaunchHeight),
                "The lobber must carry the user's throw motion atlas and crowbar, thrown from 0.2 above its root.");

            // The ranged enemy it was copied from keeps its straight shooter.
            GameObject ranged = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.RangedPrefabPath);
            Assert(ranged != null && ranged.GetComponent<RangedEnemyController>() != null &&
                   ranged.GetComponent<LobbingEnemyController>() == null &&
                   ranged.GetComponent<FairyKingdomArtworkView>() == null,
                "The ranged enemy Prefab must stay an unbound straight shooter.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.Lobber, out int threat) && threat == 4 &&
                   threat == Week27Enemy12Setup.Threat, "The lobber's threat score must be 4.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.Lobber, out GameObject bound, out error) && bound == prefab,
                $"The roster must bind the lobber role to its Prefab. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Lobber) ==
                   SpawnPointPlacementRole.RearFiring, "The lobber must use rear firing SpawnPoints.");
            Assert(EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.Lobber) == 3,
                "The lobber must first appear on floor 3.");

            int found = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidate(out error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                EncounterSpawnRule[] rules = definition.Waves.SelectMany(wave => wave.SpawnRules)
                    .Where(rule => rule.Candidates.Any(candidate => candidate.EnemyRole == EncounterEnemyRole.Lobber))
                    .ToArray();
                bool crossfire = Week27Enemy12Setup.IsCrossfire(definition);
                Assert((rules.Length > 0) == crossfire,
                    $"Only the crossfire Encounters may hold the lobber, but '{definition.EncounterId}' does not " +
                    "follow that.");
                if (!crossfire) continue;
                found++;

                Week19Spawn2Setup.ProfileSpec spec = Week19Spawn2Setup.Profiles.First(entry =>
                    Week19Spawn2Setup.EncounterId(entry.ProfileId, "crossfire") == definition.EncounterId);
                EncounterEnemyCandidate[] expected = Week27Enemy12Setup.CrossfireRearCandidates(spec);
                Assert(rules.Length == 1 && rules[0].Count == 3 &&
                       definition.Waves[0].SpawnRules.Any(rule => rule.Candidates.Any(candidate =>
                           candidate.EnemyRole == EncounterEnemyRole.Lobber)) &&
                       rules[0].Candidates.Count == expected.Length &&
                       expected.All(candidate => Weight(rules[0], candidate.EnemyRole) == candidate.Weight),
                    $"'{definition.EncounterId}' rear slots must be three of " +
                    string.Join("/", expected.Select(candidate => candidate.Weight)) + " (산사모-순수/sniper/lobber).");

                // Threat 4 equals the sniper's, so crossfire keeps 15~19, and floor 2 cannot draw the lobber.
                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out error) &&
                       minimum == Week19Difficulty1Setup.CrossfireMinimumThreat &&
                       maximum == Week19Difficulty1Setup.CrossfireMaximumThreat &&
                       definition.DeclaredMinimumThreat == minimum && definition.DeclaredMaximumThreat == maximum &&
                       definition.MinimumFloor == 2 && definition.MaximumFloor == 3,
                    $"'{definition.EncounterId}' must keep its 15~19 range on floors 2~3. {error}");
                Assert(EncounterEnemyFloorPool.EffectiveWeight(expected[2], 2) == 0 &&
                       EncounterEnemyFloorPool.EffectiveWeight(expected[2], 3) == Week27Enemy12Setup.RearWeight * 3,
                    "The lobber must weigh nothing on floor 2 and x1.5 on floor 3.");
            }

            Assert(found == Week19Spawn2Setup.Profiles.Length,
                $"Every crossfire Encounter must hold the lobber ({found}).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            int rooms = 0, bodies = 0, combatRooms = 0, crossfireFloorThree = 0;
            int[] perFloor = new int[4];
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
                    if (room.FloorNumber == 3 && Week27Enemy12Setup.IsCrossfire(room.EncounterId)) crossfireFloorThree++;
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves.SelectMany(wave => wave)
                        .Where(spawn => spawn.Role == EncounterEnemyRole.Lobber).ToArray();
                    if (spawns.Length == 0) continue;
                    Assert(Week27Enemy12Setup.IsCrossfire(room.EncounterId) && room.FloorNumber == 3,
                        $"Seed {seed} placed a lobber in '{room.EncounterId}' on floor {room.FloorNumber}.");
                    Assert(spawns.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role) &&
                                               (room.Template.SpawnPointRoles[spawn.SpawnPointIndex] &
                                                SpawnPointPlacementRole.RearFiring) != 0),
                        $"Seed {seed} room '{room.RoomId}' put a lobber off a rear firing point.");
                    rooms++;
                    bodies += spawns.Length;
                    perFloor[room.FloorNumber] += spawns.Length;
                    if (runtimeSeed == 0) runtimeSeed = seed;
                }
            }

            Assert(rooms > 0 && perFloor[1] == 0 && perFloor[2] == 0 && perFloor[3] > 0,
                $"{GeneratedSeedCount} seeds must place the lobber on floor 3 only " +
                $"({perFloor[1]}/{perFloor[2]}/{perFloor[3]}).");
            Assert(runtimeSeed > 0, "No floor-3 lobber room was generated for runtime verification.");
            return $"{bodies} lobbers in {rooms} of {crossfireFloorThree} floor-3 crossfire rooms " +
                   $"({combatRooms} combat rooms), floors {perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 3;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.Lobber), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} lobbers, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its lobber rooms.");
        }

        // Not before it is alerted or during the spawn delay. In range it stops and winds up 0.4 s, then lobs to the
        // player's spot at that moment: no collider, the circle shown at once, a parabola, medium damage on landing.
        private static void ValidateThrowAndLanding(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Throw Verification");
            try
            {
                Vector2 at = Origin;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 5f, 100f, "Player");
                player.EnableHealthUnits();
                Health lobberHealth = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(lobberHealth.gameObject, 3);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                EnemyBehaviorContext behavior = lobberHealth.GetComponent<EnemyBehaviorContext>();
                Physics2D.SyncTransforms();

                float now = Time.time;
                lobber.TickBehavior(now);
                Assert(!lobber.IsWindingUp && lobber.LastProjectile == null,
                    "A lobber must not wind up before it is alerted.");
                behavior.BeginCombat(player.transform);
                behavior.BeginSpawnGrace(now);
                lobber.TickBehavior(now);
                Assert(!lobber.IsWindingUp, "A lobber must not wind up during its spawn delay.");
                ClearSpawnDelay(behavior);

                lobber.TickBehavior(now);
                Assert(lobber.IsWindingUp && Mathf.Abs(lobber.WindUpEndsAt - (now + 0.4f)) < TimeTolerance &&
                       lobber.Velocity == Vector2.zero && lobber.LastProjectile == null,
                    "Five away the lobber must stop and wind up for 0.4 s before throwing.");

                // The landing is the player's spot when the wind-up ends, not when it began.
                Vector2 landing = at + new Vector2(4.5f, 1f);
                player.transform.position = landing;
                Physics2D.SyncTransforms();
                lobber.TickBehavior(now + 0.2f);
                Assert(lobber.IsWindingUp && lobber.LastProjectile == null, "The wind-up must last 0.4 s.");
                lobber.TickBehavior(now + 0.4f);
                EnemyLobProjectile lob = lobber.LastProjectile;
                Assert(lob != null && !lobber.IsWindingUp &&
                       Vector2.Distance(lob.Destination, landing) < 0.001f &&
                       Mathf.Abs(lob.LandAt - (now + 1.4f)) < TimeTolerance &&
                       Mathf.Abs(lobber.NextAttackTime - (now + 0.4f + 2.4f)) < TimeTolerance,
                    "After the wind-up the lobber must throw at the player's spot, landing 1 s later, and wait 2.4 s.");
                Assert(lob.GetComponentInChildren<Collider2D>() == null && lob.GetComponent<Rigidbody2D>() == null,
                    "A lob in the air must have no collider, so nothing stops it.");
                Assert(lob.Lump.sprite == lobber.ProjectileSprite && lob.Lump.color == Color.white &&
                       Mathf.Abs(Mathf.Max(lob.Lump.bounds.size.x, lob.Lump.bounds.size.y) -
                                 EnemyLobProjectile.DrawnSize) < 0.02f &&
                       Mathf.Abs(lob.Height - 0.2f) < 0.001f,
                    "The crowbar must fly at its 0.8 drawn size, leaving the hand 0.2 above the ground.");
                float startRotation = lob.Rotation;
                Assert(lob.Mark != null && lob.Mark.enabled &&
                       Vector2.Distance(lob.Mark.transform.position, landing) < 0.001f &&
                       Mathf.Abs(lob.Mark.bounds.size.x - 1.5f) < 0.01f &&
                       Mathf.Abs(lob.Mark.bounds.size.y - 0.75f) < 0.01f &&
                       lob.Mark.color == EnemyLobProjectile.MarkColor(0f) &&
                       EnemyLobProjectile.MarkColor(1f).g < EnemyDeathBlast.TelegraphColor(1f).g,
                    "The landing mark must be a 1.5 x 0.75 ellipse on the landing spot, redder than the " +
                    "self-destruct warning.");
                // A quarter second in: 720 degrees a second is half a turn (a full turn would look unchanged).
                lob.Tick(now + 0.65f);
                float quarterRotation = lob.Rotation;

                lob.Tick(now + 0.9f);
                Assert(Mathf.Abs(Mathf.DeltaAngle(startRotation, quarterRotation)) > 90f,
                    "The crowbar must spin as it flies.");
                // The parabola sits on a start height that falls to the ground: 0.2 x 0.5 + 2.2 half way.
                Assert(!lob.HasLanded && Mathf.Abs(lob.Height - 2.3f) < 0.01f &&
                       Vector2.Distance(lob.transform.position, Vector2.Lerp(at, landing, 0.5f)) < 0.001f &&
                       lob.Mark.color == EnemyLobProjectile.MarkColor(0.5f),
                    "Half way the lob must fly 2.3 high over the midpoint with the circle half filled.");

                // The player steps a little but stays inside the circle.
                player.transform.position = landing + Vector2.right * 0.3f;
                Physics2D.SyncTransforms();
                float before = player.CurrentHealth;
                lob.Tick(now + 1.39f);
                Assert(!lob.HasLanded && Mathf.Approximately(player.CurrentHealth, before),
                    "The lob must not hurt before it lands.");
                lob.Tick(now + 1.4f);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 3);
                Assert(lob.HasLanded && lob.DamagedPlayerCount == 1 &&
                       lob.DamageContext.SourceType == DamageSourceType.EnemyProjectile &&
                       Mathf.Approximately(before - player.CurrentHealth, expectedUnits),
                    $"The lob must hurt a player inside the circle for floor 3's medium damage ({expectedUnits} units).");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // An obstacle that blocks a straight shot does not stop a lob, and the lob does not break it.
        private static void ValidateOverObstacle(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Obstacle Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 30f;
                Vector2 playerAt = at + Vector2.right * 5f;
                Health player = CreateHealthTarget(root.transform, "Player", playerAt, 100f, "Player");
                player.EnableHealthUnits();
                Health lobberHealth = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(lobberHealth.gameObject, 2);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                DestructibleObstacle rock = CreateObstacle(root.transform, at + Vector2.right * 2.5f, "rock-between");
                Physics2D.SyncTransforms();
                Assert(!EnemyObstacleNavigator.HasLineOfFire(at, playerAt),
                    "The verification obstacle must block a straight shot.");

                float now = Time.time;
                lobber.TickBehavior(now);
                Assert(lobber.IsWindingUp, "The lobber must wind up even with no line of fire.");
                lobber.TickBehavior(now + 0.4f);
                EnemyLobProjectile lob = lobber.LastProjectile;
                Assert(lob != null, "The lobber must throw over the obstacle.");
                float before = player.CurrentHealth;
                lob.Tick(now + 0.9f);
                lob.Tick(now + 1.4f);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 2);
                Assert(lob.HasLanded && Mathf.Approximately(before - player.CurrentHealth, expectedUnits) &&
                       !rock.IsBroken,
                    $"The lob must land behind the obstacle for floor 2's medium damage ({expectedUnits} units) and " +
                    "leave the obstacle whole.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // A player who stepped out is not hurt, another enemy in the circle is not hurt, and a lob still lands after
        // its thrower dies.
        private static void ValidateMissAndOthers(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Miss Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 60f;
                Vector2 landing = at + Vector2.right * 5f;
                Health player = CreateHealthTarget(root.transform, "Player", landing, 100f, "Player");
                player.EnableHealthUnits();
                Health second = CreateHealthTarget(root.transform, "Second Player", landing + Vector2.up * 6f, 100f,
                    "Player");
                second.EnableHealthUnits();
                Health enemy = CreateHealthTarget(root.transform, "Enemy", landing + Vector2.down * 0.3f, 100f, "Enemy");
                Health lobberHealth = Spawn(prefab, root.transform, at);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();

                float now = Time.time;
                lobber.BeginWindUp(now);
                lobber.TickBehavior(now + 0.4f);
                EnemyLobProjectile lob = lobber.LastProjectile;
                Assert(lob != null, "The lobber must throw.");
                lobberHealth.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(lobberHealth.IsDead && lob != null && !lob.HasLanded,
                    "A lob in the air must keep flying after its thrower dies.");

                // The player steps 0.5 down: inside the old 0.75 circle but out of the 0.375-tall ellipse.
                player.transform.position = landing + Vector2.down * 0.5f;
                Physics2D.SyncTransforms();
                float playerBefore = player.CurrentHealth;
                lob.Tick(now + 1.4f);
                Assert(lob.HasLanded && lob.DamagedPlayerCount == 0 &&
                       Mathf.Approximately(player.CurrentHealth, playerBefore) &&
                       Mathf.Approximately(second.CurrentHealth, 100f) && Mathf.Approximately(enemy.CurrentHealth, 100f),
                    "A lob must hurt no one when the player stepped out, and never another enemy.");
                Assert(lob == null || lob.Mark == null,
                    "A landed lob must remove its landing circle.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // Inside 3.5 it backs away, beyond 7 it walks in, and between it holds still.
        private static void ValidateDistanceKeeping(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Distance Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 90f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 2f, 100f, "Player");
                Health lobberHealth = Spawn(prefab, root.transform, at);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();

                float now = Time.time;
                lobber.TickBehavior(now);
                Assert(!lobber.IsWindingUp && lobber.Velocity.x < -1.9f && Mathf.Abs(lobber.Velocity.magnitude - 2f) < 0.01f,
                    $"Two away the lobber must back off at speed 2, got {lobber.Velocity}.");

                player.transform.position = at + Vector2.right * 9f;
                Physics2D.SyncTransforms();
                lobber.TickBehavior(now);
                Assert(!lobber.IsWindingUp && lobber.Velocity.x > 1.9f,
                    $"Nine away the lobber must walk toward the player, got {lobber.Velocity}.");

                player.transform.position = at + Vector2.right * 6f;
                Physics2D.SyncTransforms();
                lobber.TickBehavior(now);
                Assert(lobber.IsWindingUp && lobber.Velocity == Vector2.zero,
                    "Six away the lobber must stand still and wind up.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // Knockback during the wind-up calls it off without a throw; the next wind-up starts over.
        private static void ValidateWindUpCancel(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Cancel Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 120f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 5f, 100f, "Player");
                Health lobberHealth = Spawn(prefab, root.transform, at);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();

                float now = Time.time;
                lobber.TickBehavior(now);
                SpriteRenderer visual = lobberHealth.GetComponent<FairyKingdomArtworkView>().Visual;
                lobber.TickBehavior(now + 0.3f);
                Assert(lobber.IsWindingUp && visual.color == Color.white,
                    "The lobber must wind up five away without tinting its body (the drawn motion shows it).");
                Assert(lobberHealth.GetComponent<KnockbackReceiver>().Apply(Vector2.left, 6f, 0.3f, 0f),
                    "The verification knockback must apply.");
                lobber.TickBehavior(now + 0.4f);
                Assert(!lobber.IsWindingUp && lobber.LastProjectile == null,
                    "Knockback must call off the wind-up without a throw.");
                Assert(visual.color == Color.white, "A called-off wind-up must leave the body untinted.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // The throw motion: ready then raised during the wind-up, release then follow-through after the throw, and
        // otherwise the idle drawing. It faces the target while throwing and keeps the root and collider as they were.
        private static void ValidateMotion(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Motion Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 210f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 5f, 100f, "Player");
                Health lobberHealth = Spawn(prefab, root.transform, at);
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                LobbingFairyMotion motion = lobberHealth.GetComponent<LobbingFairyMotion>();
                SpriteRenderer visual = lobberHealth.GetComponent<SpriteRenderer>();
                Collider2D collider = lobberHealth.GetComponent<Collider2D>();
                Bounds colliderBounds = collider.bounds;
                Sprite idle = FairyKingdomArtwork.Fit(Week27Enemy12Setup.ArtworkId, Week27Enemy12Setup.ArtworkWidth);
                visual.flipX = false;

                (float WindUp, float ThrowAge, bool Suppressed, int Frame)[] poses =
                {
                    (0.1f, float.PositiveInfinity, false, LobbingFairyMotion.ReadyFrame),
                    (0.6f, float.PositiveInfinity, false, LobbingFairyMotion.RaisedFrame),
                    (-1f, 0.05f, false, LobbingFairyMotion.ReleaseFrame),
                    (-1f, 0.3f, false, LobbingFairyMotion.FollowThroughFrame),
                    (-1f, 1f, false, -1),
                    (0.6f, float.PositiveInfinity, true, -1),
                };
                foreach ((float windUp, float throwAge, bool suppressed, int frame) in poses)
                {
                    motion.RenderPose(windUp, throwAge, suppressed);
                    Assert(motion.CurrentFrame == frame &&
                           visual.sprite == (frame < 0 ? idle : motion.Frame(frame)) && visual.sprite != null,
                        $"Wind-up {windUp}, throw age {throwAge}, suppressed {suppressed} must show frame {frame}, " +
                        $"got {motion.CurrentFrame}.");
                }

                // The ready pose stands as tall as the idle drawing, so switching does not pop the size.
                Sprite ready = motion.Frame(LobbingFairyMotion.ReadyFrame);
                float readyHeight = motion.NeutralContent.height * ready.rect.height / ready.pixelsPerUnit;
                Assert(Mathf.Abs(readyHeight - idle.bounds.size.y) < 0.01f,
                    $"The ready pose must stand {idle.bounds.size.y:0.00} tall like the idle drawing, got {readyHeight:0.00}.");

                motion.RenderPose(0.6f, float.PositiveInfinity);
                Assert(visual.flipX, "With the target to the right the lobber must face right while throwing.");
                Assert(lobberHealth.transform.localScale == Vector3.one && collider.bounds == colliderBounds,
                    "The motion must not change the root scale or the collider.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // A real player is aimed at by the feet (where the shadow lies), not the body centre, and standing still there
        // is hit; the ellipse test reads the same ground point.
        private static void ValidateShadowAim(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Shadow Aim Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 240f;
                Health player = CreatePlayer(root.transform, at + Vector2.right * 5f);
                Health lobberHealth = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(lobberHealth.gameObject, 1);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();
                Vector2 feet = player.GetComponentInChildren<PlayerFeet>().WorldCenter;
                Assert(feet.y < player.transform.position.y - 0.1f &&
                       Vector2.Distance(EnemyLobProjectile.GroundPoint(player.transform), feet) < 0.001f,
                    "The ground point of a player without a drawn shadow must be its feet, below the body centre.");

                float now = Time.time;
                lobber.BeginWindUp(now);
                lobber.TickBehavior(now + 0.4f);
                EnemyLobProjectile lob = lobber.LastProjectile;
                Assert(lob != null && Vector2.Distance(lob.Destination, feet) < 0.001f,
                    "The lob must land on the player's feet, where the shadow lies.");
                float before = player.CurrentHealth;
                lob.Tick(now + 1.4f);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 1);
                Assert(lob.HasLanded && Mathf.Approximately(before - player.CurrentHealth, expectedUnits),
                    $"A player standing still must be hit for floor 1's medium damage ({expectedUnits} units).");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // Moving to another room removes a lob in the air together with its landing circle.
        private static void ValidateRoomTransitionClears(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Transition Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 150f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 5f, 100f, "Player");
                Health lobberHealth = Spawn(prefab, root.transform, at);
                LobbingEnemyController lobber = lobberHealth.GetComponent<LobbingEnemyController>();
                lobberHealth.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                float now = Time.time;
                lobber.BeginWindUp(now);
                lobber.TickBehavior(now + 0.4f);
                EnemyLobProjectile lob = lobber.LastProjectile;
                Assert(lob != null && lob.Mark != null, "The lobber must throw before the transition.");
                GameObject mark = lob.Mark.gameObject;
                typeof(RoomGraphController).GetMethod("ClearTransientProjectiles",
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
                Assert(lob == null && mark == null,
                    "A room transition must remove the lob in the air and its landing circle.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        // Two lobbers spawn through the room's own path; once both die the room clears.
        private static void ValidateRoomClears(GameObject prefab)
        {
            GameObject root = new("Enemy-12 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 180f;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Enemy-12 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(3, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points),
                });
                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                LobbingEnemyController[] lobbers = root.GetComponentsInChildren<LobbingEnemyController>();
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2 && lobbers.Length == 2,
                    "The room must hold two lobbers.");
                foreach (LobbingEnemyController lobber in lobbers)
                    lobber.GetComponent<Health>().TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "The room must clear once both lobbers die.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ClearLobs();
                Physics2D.SyncTransforms();
            }
        }

        private static void ClearLobs()
        {
            foreach (EnemyLobProjectile lob in Object.FindObjectsByType<EnemyLobProjectile>(FindObjectsSortMode.None))
                lob.Cancel();
            foreach (GameObject mark in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                         .Where(renderer => renderer.name == EnemyLobProjectile.MarkName)
                         .Select(renderer => renderer.gameObject).ToArray())
                Object.DestroyImmediate(mark);
        }

        private static Health Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            InvokeAwake(instance.GetComponent<Health>());
            InvokeAwake(instance.GetComponent<KnockbackReceiver>());
            InvokeAwake(instance.GetComponent<EnemyBehaviorContext>());
            InvokeAwake(instance.GetComponent<LobbingEnemyController>());
            Physics2D.SyncTransforms();
            return instance.GetComponent<Health>();
        }

        private static void ClearSpawnDelay(EnemyBehaviorContext behavior)
        {
            typeof(EnemyBehaviorContext)
                .GetField("spawnActionReadyTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(behavior, float.NegativeInfinity);
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, Vector2 position, string obstacleId)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.transform.position = position;
            DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
            obstacle.Configure(obstacleId, DestructibleObstacle.DefaultRequiredHits, obstacle.DropTable,
                instance.GetComponentInChildren<SpriteRenderer>(true));
            return obstacle;
        }

        private static Health CreateHealthTarget(Transform parent, string name, Vector2 position, float maxHealth,
            string layerName)
        {
            GameObject target = new($"Enemy-12 {name}", typeof(CircleCollider2D), typeof(Health));
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.layer = LayerMask.NameToLayer(layerName);
            Health health = target.GetComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            InvokeAwake(health);
            return health;
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Enemy-12 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
