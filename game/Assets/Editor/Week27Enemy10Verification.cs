using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Enemy10Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-7200f, 7200f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;
        private const float TimeTolerance = 0.001f;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Enemy-10 Self Destruct")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[]
                {
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "pressure"),
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "crossfire"),
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "swarm"),
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "elite-pair"),
                })
                .Append(Week27Enemy10Setup.ArtworkPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week15Boss1Setup.MinionPrefabPath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            // Its own Prefab may move from the placeholder to the user's drawing; nothing else may change.
            Dictionary<string, string> artwork = FairyKingdomArtworkVerification.SnapshotBoundArtwork()
                .Where(entry => !entry.Key.StartsWith(Week27Enemy10Setup.PrefabPath + ":", StringComparison.Ordinal))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            Week27Enemy10Setup.Setup();
            Dictionary<string, string> created = Week27Enemy10Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week27Enemy10Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-10 setup changed or lost the GUID for {entry.Key}.");
            }

            // Every other bound drawing stays as it was.
            FairyKingdomArtworkVerification.RequireBoundArtworkUnchanged(artwork, "Enemy-10 setup");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy10Setup.PrefabPath);
            Assert(prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 &&
                   prefab.GetComponents<EnemyChase>().Length == 1 &&
                   prefab.GetComponents<EnemyDeathExplosion>().Length == 1 &&
                   prefab.GetComponents<SelfDestructEnemy>().Length == 1,
                "Re-running Enemy-10 setup must not duplicate the self-destruct Prefab's components.");

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-10 Self Destruct")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Enemy10Setup.EncounterContentVersion,
                "Run Enemy-10 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy10Setup.PrefabPath);
            Assert(prefab != null, $"Enemy-10 needs {Week27Enemy10Setup.PrefabPath}.");
            ValidatePrefab(prefab);
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateTriggerAndBlast(prefab);
            ValidateDeathWhileWarning(prefab);
            ValidateDeathBeforeWarning(prefab);
            ValidateChain(prefab);
            ValidateRoomClears(prefab);
            Debug.Log($"Enemy-10 verification passed: {Week27Enemy10Setup.DisplayName} has 30 health and speed 2.8, " +
                      "no melee or contact damage; within 1.2 of an alerted target it stops and warns 1 s (not " +
                      "during the spawn delay), then dies and blows up at once. The radius-1.5 blast hurts the " +
                      "player at the floor's medium tier, other enemies for 30 as environment damage, and breaks " +
                      "obstacles like a bomb. Killed while warning it explodes after the time left, killed before " +
                      "after the full second, a blast that kills another self-destruct leaves that one's own blast, " +
                      "and the crumb minion's blast is unchanged. Threat 3, floors 2~3, pressure 50/25/15/10/15 and " +
                      $"swarm 60/25/15/15; {GeneratedSeedCount} seeds place it reproducibly ({counts}), runtime seed " +
                      $"{runtimeSeed} binds its Prefab, and a room clears after its self-destructs blow up.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-10 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads the shared melee candidate lists, the threat table, the enemy roster, the death
            // blast or the floor pool. Each starts from a freshly loaded scene, and every check runs after a failure.
            (string Name, Action Run)[] checks =
            {
                ("Week27Curve1", Week27Curve1Verification.Verify),
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
                ("CrumbDeathBlast", CrumbDeathBlastVerification.Verify),
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
                    Debug.Log($"[Enemy-10 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Enemy-10 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Enemy-10 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Enemy-10 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0 &&
                   prefab.name == Week27Enemy10Setup.PrefabName && prefab.transform.localScale == Vector3.one &&
                   prefab.GetComponent<TestEnemy>() != null && prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponent<EnemyAttackArtwork>() == null &&
                   prefab.GetComponent<EnemyMovementAnimator>() == null,
                "The self-destruct enemy must be a full-scale chaser without the old chaser's drawn frames.");
            Assert(prefab.GetComponent<MeleeEnemyAttack>() == null &&
                   prefab.GetComponent<EnemyAttackPresentation>() == null &&
                   prefab.GetComponent<ContactDamage>() == null && prefab.GetComponent<EnemySplitOnDeath>() == null,
                "The self-destruct enemy must hurt only by exploding: no melee, contact damage or split.");
            Assert(Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, 30f) &&
                   Mathf.Approximately(prefab.GetComponent<KnockbackReceiver>().KnockbackWeight, 0.8f) &&
                   Mathf.Approximately(prefab.GetComponent<EnemyChase>().MoveSpeed, 2.8f) &&
                   Mathf.Approximately(prefab.GetComponent<EnemyChase>().DetectionRange, 8f) &&
                   Mathf.Approximately(prefab.GetComponent<EnemyChase>().StopDistance, 0f),
                "The self-destruct enemy must have 30 health, knockback weight 0.8, speed 2.8, detection 8 and " +
                "no stop distance.");

            EnemyDeathExplosion explosion = prefab.GetComponent<EnemyDeathExplosion>();
            SelfDestructEnemy controller = prefab.GetComponent<SelfDestructEnemy>();
            Assert(explosion != null && controller != null &&
                   Mathf.Approximately(controller.TriggerRange, 1.2f) &&
                   Mathf.Approximately(explosion.TelegraphDuration, 1f) &&
                   Mathf.Approximately(explosion.Radius, 1.5f) &&
                   explosion.UsesDamageTier && explosion.DamageTier == EnemyDamageTier.Medium &&
                   explosion.HitsEnemiesAndObstacles &&
                   Mathf.Approximately(explosion.EnemyDamage, PlacedBomb.DefaultEnemyDamage) &&
                   Mathf.Approximately(PlacedBomb.DefaultEnemyDamage, 30f),
                "The self-destruct enemy must trigger within 1.2 and warn 1 s before a radius-1.5 blast that deals " +
                "medium damage to the player and 30 to enemies.");

            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert(view != null && view.ArtworkId == Week27Enemy10Setup.ArtworkId && view.Visual == renderer &&
                   view.Mode == FairyKingdomArtworkMode.Fixed && renderer != null && renderer.color == Color.white &&
                   Mathf.Approximately(view.LocalWidth, Week27Enemy10Setup.ArtworkWidth) &&
                   AssetDatabase.GetAssetPath(renderer.sprite) == Week27Enemy10Setup.ArtworkPath,
                $"The self-destruct enemy must show the '{Week27Enemy10Setup.ArtworkId}' drawing untinted at width {Week27Enemy10Setup.ArtworkWidth}.");

            // The crumb minion keeps its Enemy-7 blast: 0.6 s, radius 1.2, one heart, the player only.
            EnemyDeathExplosion crumb = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss1Setup.MinionPrefabPath)
                ?.GetComponent<EnemyDeathExplosion>();
            Assert(crumb != null && !crumb.UsesDamageTier && !crumb.HitsEnemiesAndObstacles &&
                   Mathf.Approximately(crumb.TelegraphDuration, EnemyDeathExplosion.DefaultTelegraphDuration) &&
                   Mathf.Approximately(crumb.Radius, EnemyDeathExplosion.DefaultRadius) &&
                   crumb.DamageUnits == HealthUnits.UnitsPerHeart && crumb.GetComponent<SelfDestructEnemy>() == null,
                "The crumb minion's death blast must stay 0.6 s, radius 1.2, one heart and player-only.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.SelfDestruct, out int threat) && threat == 3 &&
                   threat == Week27Enemy10Setup.Threat, "The self-destruct enemy's threat score must be 3.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.SelfDestruct, out GameObject bound, out error) &&
                   bound == prefab, $"The roster must bind the self-destruct role to its Prefab. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.SelfDestruct) ==
                   SpawnPointPlacementRole.MeleePressure,
                "The self-destruct enemy must use melee pressure SpawnPoints.");
            Assert(EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.SelfDestruct) == 2,
                "The self-destruct enemy must first appear on floor 2.");

            int pressureFound = 0, swarmFound = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidate(out error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                EncounterSpawnRule[] rules = definition.Waves.SelectMany(wave => wave.SpawnRules).ToArray();
                bool holds = rules.SelectMany(rule => rule.Candidates)
                    .Any(candidate => candidate.EnemyRole == EncounterEnemyRole.SelfDestruct);
                bool pressure = Week25Enemy9Setup.IsPressure(definition);
                bool swarm = Week27Enemy10Setup.IsSwarm(definition);
                Assert(holds == (pressure || swarm),
                    $"Only the pressure and swarm Encounters may hold the self-destruct enemy, but " +
                    $"'{definition.EncounterId}' does not follow that.");
                if (!holds) continue;
                if (pressure) pressureFound++;
                else swarmFound++;

                EncounterEnemyCandidate[] expected = pressure
                    ? Week27Enemy10Setup.PressureMeleeCandidates()
                    : Week27Enemy10Setup.SwarmMeleeCandidates();
                foreach (EncounterSpawnRule rule in rules.Where(rule => rule.Candidates.Any(candidate =>
                             candidate.EnemyRole == EncounterEnemyRole.SelfDestruct)))
                {
                    Assert(rule.Candidates.Count == expected.Length &&
                           expected.All(candidate => Weight(rule, candidate.EnemyRole) == candidate.Weight),
                        $"'{definition.EncounterId}' melee slots must weigh " +
                        string.Join("/", expected.Select(candidate => candidate.Weight)) + ".");
                }

                // Threat 3 never exceeds the slot's existing maximum, so the declared ranges stay as they were.
                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out error) &&
                       minimum == definition.DeclaredMinimumThreat && maximum == definition.DeclaredMaximumThreat,
                    $"'{definition.EncounterId}' must still compute its declared threat range. {error}");
                if (pressure)
                {
                    Assert(definition.DeclaredMinimumThreat == Week19Difficulty1Setup.PressureMinimumThreat &&
                           definition.DeclaredMaximumThreat == Week19Difficulty1Setup.PressureMaximumThreat,
                        $"'{definition.EncounterId}' must keep the pressure range.");
                }
                else
                {
                    Week19Encounter4Setup.SwarmSpec spec = Week19Encounter4Setup.Swarms.First(entry =>
                        Week19Spawn2Setup.EncounterId(entry.ProfileId, "swarm") == definition.EncounterId);
                    Assert(definition.DeclaredMinimumThreat == spec.MinimumThreat &&
                           definition.DeclaredMaximumThreat == spec.MaximumThreat,
                        $"'{definition.EncounterId}' must keep its swarm range.");
                }
            }

            Assert(pressureFound == Week19Spawn2Setup.Profiles.Length &&
                   swarmFound == Week19Encounter4Setup.Swarms.Length,
                $"Every pressure and swarm Encounter must hold the self-destruct enemy ({pressureFound}, {swarmFound}).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            int rooms = 0, bodies = 0, combatRooms = 0;
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
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves.SelectMany(wave => wave)
                        .Where(spawn => spawn.Role == EncounterEnemyRole.SelfDestruct).ToArray();
                    if (spawns.Length == 0) continue;
                    Assert(Week25Enemy9Setup.IsPressure(room.Encounter) || Week27Enemy10Setup.IsSwarm(room.Encounter),
                        $"Seed {seed} placed a self-destruct enemy in '{room.EncounterId}'.");
                    Assert(spawns.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role) &&
                                               (room.Template.SpawnPointRoles[spawn.SpawnPointIndex] &
                                                SpawnPointPlacementRole.MeleePressure) != 0),
                        $"Seed {seed} room '{room.RoomId}' put a self-destruct enemy off a melee pressure point.");
                    rooms++;
                    bodies += spawns.Length;
                    perFloor[room.FloorNumber] += spawns.Length;
                    if (runtimeSeed == 0 && room.FloorNumber == 2) runtimeSeed = seed;
                }
            }

            Assert(rooms > 0 && perFloor[1] == 0 && perFloor[2] > 0 && perFloor[3] > 0,
                $"{GeneratedSeedCount} seeds must place the self-destruct enemy on floors 2~3 only " +
                $"({perFloor[1]}/{perFloor[2]}/{perFloor[3]}).");
            Assert(runtimeSeed > 0, "No floor-2 self-destruct room was generated for runtime verification.");
            return $"{bodies} self-destructs in {rooms} of {combatRooms} combat rooms, floors " +
                   $"{perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 2;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.SelfDestruct), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} self-destructs, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its self-destruct rooms.");
        }

        // Out of range or during the spawn delay nothing happens. In range it stops and warns; after one second
        // it dies and the blast goes off at once, hurting the player, nearby enemies and obstacles.
        private static void ValidateTriggerAndBlast(GameObject prefab)
        {
            GameObject root = new("Enemy-10 Trigger Verification");
            GameObject progressHolder = new("Enemy-10 Progress");
            try
            {
                Vector2 at = Origin;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 3f, 100f, "Player");
                player.EnableHealthUnits();
                Health bomber = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(bomber.gameObject, 3);
                SelfDestructEnemy controller = bomber.GetComponent<SelfDestructEnemy>();
                EnemyDeathExplosion explosion = bomber.GetComponent<EnemyDeathExplosion>();
                EnemyBehaviorContext behavior = bomber.GetComponent<EnemyBehaviorContext>();

                Health enemy = CreateHealthTarget(root.transform, "Enemy", at + Vector2.left * 1f, 100f, "Enemy");
                Health farEnemy = CreateHealthTarget(root.transform, "Far Enemy", at + Vector2.left * 3f, 100f, "Enemy");
                DamageSourceType enemyHitSource = DamageSourceType.Unknown;
                enemy.DamageResolved += (context, _) => enemyHitSource = context.SourceType;
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                RoomRunState state = new("floor-03-room-02");
                DestructibleObstacle nearRock = CreateObstacle(root.transform, at + Vector2.up * 1f, "rock-near");
                DestructibleObstacle farRock = CreateObstacle(root.transform, at + Vector2.down * 2.6f, "rock-far");
                foreach (DestructibleObstacle obstacle in new[] { nearRock, farRock })
                    obstacle.Bind(state, 11, root.transform, progress);
                Physics2D.SyncTransforms();

                float now = Time.time;
                controller.Tick(now);
                Assert(!controller.IsPrimed, "A self-destruct enemy must not warn before it is alerted.");
                behavior.BeginCombat(player.transform);
                controller.Tick(now);
                Assert(!controller.IsPrimed, "A self-destruct enemy must not warn while the player is 3 away.");

                player.transform.position = at + Vector2.right * 1f;
                Physics2D.SyncTransforms();
                behavior.BeginSpawnGrace(now);
                controller.Tick(now);
                Assert(!controller.IsPrimed, "A self-destruct enemy must not warn during its spawn delay.");
                ClearSpawnDelay(behavior);

                controller.Tick(now);
                Assert(controller.IsPrimed && Mathf.Abs(controller.ExplodeAt - (now + 1f)) < TimeTolerance &&
                       behavior.IsMovementSuppressed && !bomber.IsDead,
                    "Within 1.2 of its target the self-destruct enemy must stop and warn for one second.");
                Assert(controller.Telegraph != null && controller.Telegraph.enabled &&
                       Mathf.Abs(controller.Telegraph.bounds.size.x - 3f) < 0.01f,
                    "The warning circle must show the blast's 1.5 radius.");

                // Moving away does not call the blast off.
                player.transform.position = at + Vector2.right * 1.2f;
                Physics2D.SyncTransforms();
                controller.Tick(now + 0.5f);
                Assert(controller.IsPrimed && !bomber.IsDead && controller.Visual != null &&
                       controller.Visual.color != Color.white &&
                       controller.Visual.color == EnemyDeathBlast.BodyColor(0.5f),
                    "Half way through the warning the self-destruct enemy must be half reddened and still alive.");

                float playerBefore = player.CurrentHealth;
                controller.Tick(now + 1f);
                EnemyDeathBlast blast = explosion.LastBlast;
                Assert(bomber.IsDead && blast != null && blast.HasExploded && blast.HitsEnemiesAndObstacles &&
                       Vector2.Distance(blast.transform.position, at) < 0.001f,
                    "When the warning ends the self-destruct enemy must die and blow up on its spot at once.");
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 3);
                Assert(blast.DamageUnits == expectedUnits && blast.DamagedPlayerCount == 1 &&
                       Mathf.Approximately(playerBefore - player.CurrentHealth, expectedUnits),
                    $"The blast must hurt the player for floor 3's medium damage ({expectedUnits} units).");
                Assert(Mathf.Approximately(enemy.CurrentHealth, 100f - PlacedBomb.DefaultEnemyDamage) &&
                       Mathf.Approximately(farEnemy.CurrentHealth, 100f) && blast.DamagedEnemyCount == 1 &&
                       enemyHitSource == DamageSourceType.Environment,
                    "The blast must deal the bomb's 30 to an enemy inside its radius as environment damage only.");
                Assert(nearRock.IsBroken && !farRock.IsBroken && blast.BrokenObstacleCount == 1,
                    "The blast must break an obstacle inside its radius like a bomb and leave a far one.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        // Killed 0.4 s into its warning, the blast keeps the 0.6 s that was left on the spot where it died.
        private static void ValidateDeathWhileWarning(GameObject prefab)
        {
            GameObject root = new("Enemy-10 Warning Death Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 20f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 1f, 100f, "Player");
                player.EnableHealthUnits();
                Health bomber = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(bomber.gameObject, 1);
                SelfDestructEnemy controller = bomber.GetComponent<SelfDestructEnemy>();
                bomber.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();

                float now = Time.time;
                controller.Prime(now - 0.4f);
                Assert(controller.IsPrimed, "The warning must start when primed.");
                bomber.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                EnemyDeathBlast blast = bomber.GetComponent<EnemyDeathExplosion>().LastBlast;
                Assert(bomber.IsDead && blast != null && !blast.HasExploded &&
                       Mathf.Abs(blast.ExplodeAt - (now + 0.6f)) < TimeTolerance &&
                       blast.Body != null && blast.Body.color != Color.white,
                    "A self-destruct enemy killed while warning must leave a reddened blast with the time left.");
                float playerBefore = player.CurrentHealth;
                blast.Tick(now + 0.59f);
                Assert(!blast.HasExploded && Mathf.Approximately(player.CurrentHealth, playerBefore),
                    "The blast must not go off before the warning's time runs out.");
                blast.Tick(now + 0.61f);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 1);
                Assert(blast.HasExploded &&
                       Mathf.Approximately(playerBefore - player.CurrentHealth, expectedUnits),
                    $"The blast must then hurt the player for floor 1's medium damage ({expectedUnits} units).");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Killed before it warns, it still leaves a full one-second blast where it died.
        private static void ValidateDeathBeforeWarning(GameObject prefab)
        {
            GameObject root = new("Enemy-10 Early Death Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 40f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 1f, 100f, "Player");
                player.EnableHealthUnits();
                Health bomber = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(bomber.gameObject, 2);
                Physics2D.SyncTransforms();

                float now = Time.time;
                bomber.TakeDamage(new DamageContext(null, DamageSourceType.PlayerBomb, 999f));
                EnemyDeathBlast blast = bomber.GetComponent<EnemyDeathExplosion>().LastBlast;
                Assert(bomber.IsDead && !bomber.GetComponent<SelfDestructEnemy>().IsPrimed && blast != null &&
                       !blast.HasExploded && Mathf.Abs(blast.ExplodeAt - (now + 1f)) < TimeTolerance &&
                       Mathf.Abs(blast.Radius - 1.5f) < 0.0001f,
                    "A self-destruct enemy killed before warning must leave a full one-second radius-1.5 blast.");
                float playerBefore = player.CurrentHealth;
                blast.Tick(now + 0.99f);
                Assert(!blast.HasExploded, "The early blast must wait the full second.");
                blast.Tick(now + 1.01f);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 2);
                Assert(blast.HasExploded &&
                       Mathf.Approximately(playerBefore - player.CurrentHealth, expectedUnits),
                    $"The early blast must hurt the player for floor 2's medium damage ({expectedUnits} units).");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // A blast that kills another self-destruct leaves that one's own full-length blast behind.
        private static void ValidateChain(GameObject prefab)
        {
            GameObject root = new("Enemy-10 Chain Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 60f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.down * 1f, 100f, "Player");
                player.EnableHealthUnits();
                Health first = Spawn(prefab, root.transform, at);
                Health second = Spawn(prefab, root.transform, at + Vector2.right * 1f);
                first.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                Physics2D.SyncTransforms();

                float now = Time.time;
                SelfDestructEnemy controller = first.GetComponent<SelfDestructEnemy>();
                controller.Tick(now);
                Assert(controller.IsPrimed, "The first self-destruct enemy must warn with the player 1 away.");
                controller.Tick(now + 1f);
                Assert(first.IsDead && first.GetComponent<EnemyDeathExplosion>().LastBlast.HasExploded,
                    "The first self-destruct enemy must blow up.");
                EnemyDeathBlast chained = second.GetComponent<EnemyDeathExplosion>().LastBlast;
                Assert(second.IsDead && chained != null && !chained.HasExploded &&
                       Mathf.Abs(chained.ExplodeAt - (Time.time + 1f)) < TimeTolerance,
                    "A self-destruct enemy killed by another's blast must leave its own one-second blast.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Both self-destructs spawn through the room's own path; once they blow up the room clears.
        private static void ValidateRoomClears(GameObject prefab)
        {
            GameObject root = new("Enemy-10 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 80f;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Enemy-10 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(2, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points),
                });
                // Edit mode runs no Awake, so the room's own spawn hook arms the blast as Awake would.
                room.EnemySpawned += spawned =>
                {
                    InvokeAwake(spawned.GetComponent<EnemyDeathExplosion>());
                    InvokeAwake(spawned.GetComponent<SelfDestructEnemy>());
                };
                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                SelfDestructEnemy[] bombers = root.GetComponentsInChildren<SelfDestructEnemy>();
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2 && bombers.Length == 2,
                    "The room must hold two self-destruct enemies.");

                float now = Time.time;
                bombers[0].Prime(now - 1f);
                bombers[0].Tick(now);
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 1,
                    "One blown-up self-destruct must leave the other holding the room.");
                bombers[1].Prime(now - 1f);
                bombers[1].Tick(now);
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "The room must clear once every self-destruct has blown up.");
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
            InvokeAwake(instance.GetComponent<EnemyDeathExplosion>());
            InvokeAwake(instance.GetComponent<SelfDestructEnemy>());
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
            GameObject target = new($"Enemy-10 {name}", typeof(CircleCollider2D), typeof(Health));
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
            GameObject player = new("Enemy-10 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
