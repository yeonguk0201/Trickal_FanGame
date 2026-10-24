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
    public static class Week25Enemy8Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6400f, 6400f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        [MenuItem("Trickal Fan Game/Week 25/Setup and Verify Enemy-8 Lively Low Blood Sugar Fairy")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[]
                {
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "pressure"),
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "swarm"),
                })
                .Append(Week25Enemy8Setup.ArtworkPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week25Enemy8Setup.Setup();
            Dictionary<string, string> created = Week25Enemy8Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week25Enemy8Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-8 setup changed or lost the GUID for {entry.Key}.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Enemy8Setup.PrefabPath);
            Assert(prefab.GetComponents<FourWayShooterController>().Length == 1 &&
                   prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 && prefab.transform.childCount == 0,
                "Re-running Enemy-8 setup must not duplicate the fairy's components or children.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-8 Lively Low Blood Sugar Fairy")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week25Enemy8Setup.EncounterContentVersion,
                "Run Enemy-8 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Enemy8Setup.PrefabPath);
            Assert(prefab != null, $"Enemy-8 needs {Week25Enemy8Setup.PrefabPath}.");
            ValidatePrefab(prefab);
            ValidateDirectionChoice();
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateCycle(prefab);
            ValidateInterrupt(prefab);
            ValidateRoomClears(prefab);
            Debug.Log($"Enemy-8 verification passed: {Week25Enemy8Setup.DisplayName} has 30 health, knockback " +
                      $"weight {Week25Enemy8Setup.KnockbackWeight} and its own drawing; it moves 1.2 s at 2.5 mostly toward the player, warns 0.4 s on " +
                      "four arms, fires one light shot each up, down, left and right at speed 5 for 1.2 s (range 6) " +
                      "even through an obstacle, sized like the green onion, recovers 0.6 s and walks again, and a " +
                      "knockback cancels the warning. Threat 3 on melee pressure points in the pressure and swarm " +
                      $"melee slots only; {GeneratedSeedCount} seeds select it reproducibly ({counts}), runtime seed " +
                      $"{runtimeSeed} binds its Prefab, and killing both waves clears the room.");
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-8 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            // A runtime binding check stores its seed's graph in the scene's Run progress, so each check starts from
            // a freshly loaded scene.
            Action[] checks =
            {
                Verify, Week25Enemy7Verification.Verify, Week19Spawn2Verification.Verify,
                Week19Difficulty1Verification.Verify, Week19Encounter4Verification.Verify,
                FairyKingdomArtworkVerification.Verify,
            };
            foreach (Action check in checks)
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                check();
            }

            Debug.Log("Enemy-8 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            Assert(prefab.name == Week25Enemy8Setup.PrefabName &&
                   GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0,
                "The lively fairy Prefab must keep its name and have no missing script.");
            Health health = prefab.GetComponent<Health>();
            Assert(health != null && Mathf.Approximately(health.MaxHealth, 30f) &&
                   Mathf.Approximately(prefab.GetComponent<KnockbackReceiver>().KnockbackWeight,
                       Week25Enemy8Setup.KnockbackWeight),
                $"The lively fairy must have 30 health and knockback weight {Week25Enemy8Setup.KnockbackWeight}.");
            Assert(prefab.GetComponent<RangedEnemyController>() == null &&
                   prefab.GetComponent<MobileRangedEnemyController>() == null &&
                   prefab.GetComponent<LongRangeSniperController>() == null &&
                   prefab.GetComponent<EnemyChase>() == null && prefab.GetComponent<ContactDamage>() == null &&
                   prefab.GetComponent<EnemyAttackArtwork>() == null && prefab.GetComponent<LineRenderer>() == null &&
                   prefab.GetComponent<TestEnemy>() != null && prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponent<EnemyAttackPresentation>() != null,
                "The lively fairy must only shoot through its four-way controller.");

            FourWayShooterController controller = prefab.GetComponent<FourWayShooterController>();
            Assert(controller != null && Mathf.Approximately(controller.MoveSpeed, 2.5f) &&
                   Mathf.Approximately(controller.MoveDuration, 1.2f) &&
                   Mathf.Approximately(controller.TelegraphDuration, 0.4f) &&
                   Mathf.Approximately(controller.RecoveryDuration, 0.6f) &&
                   Mathf.Approximately(controller.MoveDuration + controller.TelegraphDuration +
                                       controller.RecoveryDuration, 2.2f) &&
                   Mathf.Approximately(controller.ProjectileSpeed, 5f) &&
                   Mathf.Approximately(controller.ProjectileLifetime, 1.2f) &&
                   Mathf.Approximately(controller.Range, 6f) &&
                   controller.ProjectileDamageTier == EnemyDamageTier.Light,
                "The lively fairy must move 1.2 s at 2.5, warn 0.4 s, recover 0.6 s and fire light shots at speed " +
                "5 for 1.2 s.");

            Sprite onion = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyAttackAnimationSetup.ProjectilePath);
            Sprite bread = controller.ProjectileSprite;
            Assert(bread != null && onion != null &&
                   AssetDatabase.GetAssetPath(bread) == Week25Enemy8Setup.ProjectilePath &&
                   Mathf.Approximately(bread.pixelsPerUnit, onion.pixelsPerUnit) &&
                   (bread.bounds.size - onion.bounds.size).sqrMagnitude < 0.0001f,
                "The lively fairy's shot must be drawn at the green onion's size.");

            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert(view != null && view.ArtworkId == Week25Enemy8Setup.ArtworkId && view.Visual == renderer &&
                   renderer != null && renderer.color == Color.white &&
                   AssetDatabase.GetAssetPath(renderer.sprite) == Week25Enemy8Setup.ArtworkPath,
                $"The lively fairy must show the '{Week25Enemy8Setup.ArtworkId}' drawing untinted.");
            Assert(Week25Enemy8Setup.DisplayName == "저혈당 요정-활발" &&
                   !Week25Enemy7Setup.EnemyDisplayNames.ContainsKey(Week25Enemy8Setup.PrefabName),
                "The lively fairy keeps its own display name.");
        }

        // The middle roll walks straight at the player, the ends turn 120 degrees, and most rolls lean toward it.
        private static void ValidateDirectionChoice()
        {
            Vector2 toward = new(3f, 4f);
            Assert(Vector2.Angle(FourWayShooterController.ChooseMoveDirection(toward, 0.5f), toward) < 0.01f,
                "A middle roll must walk straight at the player.");
            Assert(Mathf.Abs(Vector2.Angle(FourWayShooterController.ChooseMoveDirection(toward, 0f), toward) - 120f) <
                   0.01f && Mathf.Abs(Vector2.Angle(FourWayShooterController.ChooseMoveDirection(toward, 1f),
                       toward) - 120f) < 0.01f,
                "The extreme rolls must turn 120 degrees away from the player.");
            int leaning = 0, total = 1000;
            for (int i = 0; i < total; i++)
            {
                Vector2 direction = FourWayShooterController.ChooseMoveDirection(toward, (i + 0.5f) / total);
                Assert(Mathf.Approximately(direction.magnitude, 1f), "A move direction must be a unit vector.");
                if (Vector2.Dot(direction, toward) > 0f) leaning++;
            }

            Assert(leaning > total * 0.8f && leaning < total,
                $"Most but not all moves must lean toward the player, but {leaning} of {total} did.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.FourWayShooter, out int threat) && threat == 3 &&
                   threat == Week25Enemy8Setup.Threat,
                "The lively fairy's threat score must be 3.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.FourWayShooter, out GameObject bound, out error) &&
                   bound == prefab, $"The roster must bind the four-way role to the lively fairy. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.FourWayShooter) ==
                   SpawnPointPlacementRole.MeleePressure,
                "The lively fairy must use melee pressure SpawnPoints.");

            HashSet<string> mixed = new(Week19Spawn2Setup.Profiles.SelectMany(profile => new[]
            {
                Week19Spawn2Setup.EncounterId(profile.ProfileId, "pressure"),
                Week19Spawn2Setup.EncounterId(profile.ProfileId, "swarm"),
            }), StringComparer.Ordinal);
            int found = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                EncounterSpawnRule[] rules = definition.Waves.SelectMany(wave => wave.SpawnRules).ToArray();
                bool holds = rules.SelectMany(rule => rule.Candidates)
                    .Any(candidate => candidate.EnemyRole == EncounterEnemyRole.FourWayShooter);
                Assert(holds == mixed.Contains(definition.EncounterId),
                    $"Only the pressure and swarm Encounters may hold the lively fairy, but '{definition.EncounterId}' " +
                    "does not follow that.");
                if (!holds) continue;
                found++;
                bool pressure = definition.EncounterId.EndsWith("-pressure-v1", StringComparison.Ordinal);
                // Enemy-9 added 누루링-요정전사 to the pressure melee slots (50/25/15/10); swarm keeps 60/25/15.
                EncounterEnemyCandidate[] expected = pressure
                    ? Week25Enemy9Setup.PressureMeleeCandidates()
                    : Week25Enemy8Setup.MeleeCandidates();
                foreach (EncounterSpawnRule rule in rules.Where(rule => rule.Candidates.Any(candidate =>
                             candidate.EnemyRole == EncounterEnemyRole.FourWayShooter)))
                {
                    Assert(rule.Candidates.Count == expected.Length &&
                           expected.All(candidate => Weight(rule, candidate.EnemyRole) == candidate.Weight),
                        $"'{definition.EncounterId}' melee slots must weigh " +
                        string.Join("/", expected.Select(candidate => candidate.Weight)) + ".");
                }

                Week19Encounter4Setup.SwarmSpec swarm = Week19Encounter4Setup.Swarms.FirstOrDefault(spec =>
                    Week19Spawn2Setup.EncounterId(spec.ProfileId, "swarm") == definition.EncounterId);
                Assert(pressure
                        ? definition.DeclaredMinimumThreat == Week19Difficulty1Setup.PressureMinimumThreat &&
                          definition.DeclaredMaximumThreat == Week19Difficulty1Setup.PressureMaximumThreat
                        : swarm.ProfileId != null && definition.DeclaredMinimumThreat == swarm.MinimumThreat &&
                          definition.DeclaredMaximumThreat == swarm.MaximumThreat,
                    $"'{definition.EncounterId}' must keep its declared threat range.");
            }

            Assert(found == mixed.Count, $"Every pressure and swarm Encounter must hold the lively fairy ({found}).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            int rooms = 0, fairies = 0, combatRooms = 0;
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
                        .Where(spawn => spawn.Role == EncounterEnemyRole.FourWayShooter).ToArray();
                    if (spawns.Length == 0) continue;
                    Assert(room.EncounterId.EndsWith("-pressure-v1", StringComparison.Ordinal) ||
                           (room.EncounterId.EndsWith("-swarm-v1", StringComparison.Ordinal) &&
                            !room.EncounterId.EndsWith("-jyubi-swarm-v1", StringComparison.Ordinal)),
                        $"Seed {seed} placed a lively fairy in '{room.EncounterId}'.");
                    Assert(spawns.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role) &&
                                               (room.Template.SpawnPointRoles[spawn.SpawnPointIndex] &
                                                SpawnPointPlacementRole.MeleePressure) != 0),
                        $"Seed {seed} room '{room.RoomId}' put a lively fairy off a melee pressure point.");
                    rooms++;
                    fairies += spawns.Length;
                    perFloor[room.FloorNumber]++;
                    if (runtimeSeed == 0 && room.FloorNumber == EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.FourWayShooter)) runtimeSeed = seed;
                }
            }

            // Curve-1: it appears from its introduction floor on and never before.
            int introduction = EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.FourWayShooter);
            Assert(rooms > 0 && Enumerable.Range(1, 3).All(floor => (perFloor[floor] > 0) == (floor >= introduction)),
                $"{GeneratedSeedCount} seeds must place the lively fairy on floors {introduction}~3 only.");
            Assert(runtimeSeed > 0, $"No floor-{introduction} lively fairy room was generated for runtime verification.");
            return $"{fairies} fairies in {rooms} of {combatRooms} combat rooms, floors " +
                   $"{perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            int floor = EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.FourWayShooter);
            if (floor > 1)
                Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.FourWayShooter), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} lively fairies, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its lively fairy rooms.");
        }

        // One full cycle: walk toward the player, stop and warn, fire four ways through an obstacle, recover, walk.
        private static void ValidateCycle(GameObject prefab)
        {
            GameObject root = new("Enemy-8 Cycle Verification");
            try
            {
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 4f);
                FourWayShooterController fairy = Spawn(prefab, root.transform, Origin);
                fairy.SetTarget(player.transform);
                Physics2D.SyncTransforms();
                Rigidbody2D body = fairy.GetComponent<Rigidbody2D>();
                EnemyProjectile[] volley = null;
                fairy.VolleyFired += fired => volley = fired;

                float start = Time.time;
                fairy.BeginMove(start, 0.5f);
                Assert(fairy.State == FourWayShooterState.Moving &&
                       Vector2.Angle(fairy.MoveDirection, Vector2.down) < 0.01f && !fairy.IsWarningVisible,
                    "A middle roll must start walking straight at the player.");
                fairy.TickBehavior(start + 0.1f);
                Assert((body.linearVelocity - Vector2.down * 2.5f).sqrMagnitude < 0.0001f,
                    "The fairy must walk at speed 2.5.");

                // An Environment block between the fairy and the player: four-way shots do not need a line of fire.
                GameObject wall = new("Enemy-8 Wall", typeof(BoxCollider2D));
                wall.transform.SetParent(root.transform);
                wall.transform.position = Origin + Vector2.down * 2f;
                wall.layer = LayerMask.NameToLayer("Environment");
                Physics2D.SyncTransforms();
                Assert(!EnemyObstacleNavigator.HasLineOfFire(Origin, player.transform.position),
                    "The verification wall must block the line of fire.");

                fairy.TickBehavior(start + 1.19f);
                Assert(fairy.State == FourWayShooterState.Moving, "The fairy must keep walking for 1.2 seconds.");
                fairy.TickBehavior(start + 1.2f);
                EnemyAttackPresentation presentation = fairy.GetComponent<EnemyAttackPresentation>();
                Assert(fairy.State == FourWayShooterState.Telegraph && body.linearVelocity == Vector2.zero &&
                       fairy.IsWarningVisible && presentation.Phase == EnemyAttackPhase.Telegraph,
                    "After 1.2 seconds the fairy must stop and show its four-way warning.");
                LineRenderer[] arms = fairy.GetComponentsInChildren<LineRenderer>(true);
                Assert(arms.Length == 4 && FourWayShooterController.FireDirections.All(direction => arms.Any(arm =>
                           Vector2.Angle((Vector2)(arm.GetPosition(1) - arm.GetPosition(0)), direction) < 0.01f &&
                           Mathf.Abs(Vector2.Distance(arm.GetPosition(0), arm.GetPosition(1)) -
                                     FourWayShooterController.WarningLength) < 0.001f)),
                    "The warning must point up, down, left and right.");

                fairy.TickBehavior(start + 1.59f);
                Assert(volley == null && fairy.State == FourWayShooterState.Telegraph,
                    "The fairy must not fire during its 0.4-second warning.");
                fairy.TickBehavior(start + 1.6f);
                Assert(volley != null && volley.Length == 4 && fairy.State == FourWayShooterState.Recovering &&
                       !fairy.IsWarningVisible && presentation.Phase == EnemyAttackPhase.Recovery,
                    "After the warning the fairy must fire four shots and start recovering.");
                Sprite onion = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyAttackAnimationSetup.ProjectilePath);
                foreach (Vector2 direction in FourWayShooterController.FireDirections)
                {
                    EnemyProjectile shot = volley.Single(projectile =>
                        Vector2.Angle(projectile.Velocity, direction) < 0.01f);
                    SpriteRenderer drawn = shot.GetComponent<SpriteRenderer>();
                    Assert(Mathf.Abs(shot.Velocity.magnitude - 5f) < 0.001f && shot.IsLaunched &&
                           ((Vector2)shot.transform.position - Origin).sqrMagnitude < 0.0001f &&
                           Mathf.Approximately(shot.DamageContext.BaseDamage,
                               HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)) &&
                           drawn.sprite == fairy.ProjectileSprite && drawn.color == Color.white &&
                           Mathf.Approximately(shot.transform.localScale.x, ProjectileSizing.DrawnEnemyScale) &&
                           (drawn.sprite.bounds.size - onion.bounds.size).sqrMagnitude < 0.0001f,
                        $"The {direction} shot must be a light green-onion-sized bread shot at speed 5.");
                }

                EnemyProjectile down = volley.Single(projectile => projectile.Velocity.y < -0.1f);
                float before = player.CurrentHealth;
                Assert(down.TryHit(player.GetComponent<Collider2D>()) &&
                       Mathf.Approximately(before - player.CurrentHealth, HealthUnits.UnitsPerHeart),
                    "A shot that reaches the player must take one heart.");
                EnemyProjectile left = volley.Single(projectile => projectile.Velocity.x < -0.1f);
                left.TickLifetime(Time.time + 1.1f);
                Assert(left != null && left.IsLaunched, "A shot must fly for 1.2 seconds.");
                left.TickLifetime(Time.time + 1.2f);
                Assert(left == null, "A shot must disappear after 1.2 seconds (range 6).");

                fairy.TickBehavior(start + 2.19f);
                Assert(fairy.State == FourWayShooterState.Recovering && body.linearVelocity == Vector2.zero,
                    "The fairy must stand still through its 0.6-second recovery.");
                fairy.TickBehavior(start + 2.2f);
                Assert(fairy.State == FourWayShooterState.Moving && fairy.StateEndsAt > start + 3.39f,
                    "The fairy must walk again 2.2 seconds after the cycle began.");
            }
            finally
            {
                // The shots are created under the fairy's parent, so they go with the root.
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // A knockback during the warning cancels the volley; the next cycle starts with a new move.
        private static void ValidateInterrupt(GameObject prefab)
        {
            GameObject root = new("Enemy-8 Interrupt Verification");
            try
            {
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 4f);
                FourWayShooterController fairy = Spawn(prefab, root.transform, Origin);
                fairy.SetTarget(player.transform);
                Physics2D.SyncTransforms();
                int volleys = 0;
                fairy.VolleyFired += _ => volleys++;
                float start = Time.time;
                fairy.BeginMove(start, 0.5f);
                fairy.TickBehavior(start + 1.2f);
                Assert(fairy.State == FourWayShooterState.Telegraph, "The fairy must be warning before the knockback.");
                Assert(fairy.GetComponent<KnockbackReceiver>().Apply(Vector2.up, 4f, 0.2f, 0.1f),
                    "The knockback must apply.");
                fairy.TickBehavior(start + 1.3f);
                Assert(fairy.State == FourWayShooterState.Idle && !fairy.IsWarningVisible,
                    "A knockback must cancel the warning.");
                fairy.GetComponent<KnockbackReceiver>().Stop();
                fairy.TickBehavior(start + 1.7f);
                Assert(volleys == 0 && fairy.State == FourWayShooterState.Moving,
                    "After a knockback the fairy must walk again without firing the cancelled volley.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Both waves spawn through the room's own path and the room clears when the last fairy dies.
        private static void ValidateRoomClears(GameObject prefab)
        {
            GameObject root = new("Enemy-8 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 3).Select(index =>
                {
                    GameObject point = new($"Enemy-8 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = Origin + Vector2.right * (index + 1) * 2f;
                    return point.transform;
                }).ToArray();
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 3).ToArray(), points),
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points.Take(2).ToArray()),
                });
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                room.BeginCombat(player);
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 1 && room.AliveEnemyCount == 3,
                    "The first wave must hold three lively fairies.");
                KillAll(root);
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 2 && room.AliveEnemyCount == 2,
                    "The second wave must start with two lively fairies after the first.");
                KillAll(root);
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "Killing both waves of lively fairies must clear the room.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static FourWayShooterController Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            InvokeAwake(instance.GetComponent<Health>());
            InvokeAwake(instance.GetComponent<KnockbackReceiver>());
            InvokeAwake(instance.GetComponent<EnemyBehaviorContext>());
            InvokeAwake(instance.GetComponent<EnemyAttackPresentation>());
            FourWayShooterController controller = instance.GetComponent<FourWayShooterController>();
            InvokeAwake(controller);
            return controller;
        }

        private static void KillAll(GameObject root)
        {
            foreach (Health enemy in root.GetComponentsInChildren<Health>())
            {
                if (enemy.GetComponent<PlayerMovement>() != null || enemy.IsDead) continue;
                enemy.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
            }
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Enemy-8 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
