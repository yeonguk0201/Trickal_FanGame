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
using AmbushState = TrickalFanGame.Enemy.AmbushEnemyController.AmbushState;

namespace TrickalFanGame.Editor
{
    public static class Week27Enemy13Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-9000f, 9000f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;
        // Ticks land just past each state's end, so float rounding of (start + duration) - start cannot fall short.
        private const float Step = 0.001f;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Enemy-13 Ambush Enemy")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[] { "pressure", "crossfire", "swarm", "elite-pair", "jyubi-swarm", "jyubi-nest" }
                    .Select(pattern => Week19Spawn2Setup.EncounterPath(profile.ProfileId, pattern)))
                .Append(Week27Enemy13Setup.ArtworkPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week15Enemy0Setup.ChaserPrefabPath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            // Only Enemy-13's own Prefab may gain a drawing; the verification below checks it directly.
            Dictionary<string, string> artwork = FairyKingdomArtworkVerification.SnapshotBoundArtwork()
                .Where(entry => !entry.Key.StartsWith(Week27Enemy13Setup.PrefabPath + ":", StringComparison.Ordinal))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            Week27Enemy13Setup.Setup();
            Dictionary<string, string> created = Week27Enemy13Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week27Enemy13Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-13 setup changed or lost the GUID for {entry.Key}.");
            }

            FairyKingdomArtworkVerification.RequireBoundArtworkUnchanged(artwork, "Enemy-13 setup");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy13Setup.PrefabPath);
            Assert(prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 &&
                   prefab.GetComponents<AmbushEnemyController>().Length == 1 &&
                   prefab.GetComponents<EnemyBehaviorContext>().Length == 1,
                "Re-running Enemy-13 setup must not duplicate the ambusher Prefab's components.");

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-13 Ambush Enemy")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Enemy13Setup.EncounterContentVersion,
                "Run Enemy-13 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week27Enemy13Setup.PrefabPath);
            Assert(prefab != null, $"Enemy-13 needs {Week27Enemy13Setup.PrefabPath}.");
            ValidatePrefab(prefab);
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateCycle(prefab);
            ValidateMissAndOthers(prefab);
            ValidateFollowsWithoutForcedBurst(prefab);
            ValidateRoomClears(prefab);
            Debug.Log($"Enemy-13 verification passed: {Week27Enemy13Setup.DisplayName} has 35 health and " +
                      "one-heart contact damage. It starts buried (invulnerable, body collider off, feet on, dim " +
                      "brown), waits until alerted and past its spawn delay, then moves under the floor at 3.2 until " +
                      "its shadow is within 0.3 of the player's (never bursting out in front of the player or after a " +
                      "long burrow). There after 1 s buried it shows a 2 x 1 red ellipse for 0.7 s, then bursts out " +
                      "(red): a player whose feet are in the ellipse takes the floor's medium damage once, a player " +
                      "outside or another enemy does not. Up it is Kinematic and knockback-immune and can be hit " +
                      "(0.3 s attack, 2 s up, 0.4 s sinking), then buried and Dynamic again. Threat 4, floor 3 only, " +
                      "pressure melee weight " +
                      $"15; {GeneratedSeedCount} seeds place it reproducibly ({counts}), runtime seed {runtimeSeed} " +
                      "binds its Prefab, and a room clears only once its ambushers are hit while up.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Enemy-13 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads the pressure list, the threat table, the enemy roster, the floor pool, the
            // knockback table, the artwork catalog or the dev panel.
            (string Name, Action Run)[] checks =
            {
                ("Week27Enemy12", Week27Enemy12Verification.Verify),
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
                    Debug.Log($"[Enemy-13 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Enemy-13 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Enemy-13 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Enemy-13 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0 &&
                   prefab.name == Week27Enemy13Setup.PrefabName && prefab.transform.localScale == Vector3.one &&
                   prefab.GetComponent<TestEnemy>() != null && prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponentInChildren<EnemyFeet>(true) != null &&
                   EnemyFeet.TryGetBody(prefab, out _),
                "The ambusher must be a full-scale enemy with feet, a body circle and a behaviour context.");
            Assert(prefab.GetComponent<EnemyChase>() == null && prefab.GetComponent<MeleeEnemyAttack>() == null &&
                   prefab.GetComponent<EnemyDeathExplosion>() == null,
                "The ambusher must hurt by bursting out and touch only: no chase, melee or death blast.");
            ContactDamage contact = prefab.GetComponent<ContactDamage>();
            Assert(contact != null && contact.DamageTier == EnemyDamageTier.Light && !contact.HalfHeartDamage &&
                   Mathf.Approximately(contact.Cooldown, 1f) &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1) == HealthUnits.UnitsPerHeart &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 3) == HealthUnits.UnitsPerHeart,
                "Touching the ambusher must cost one heart on every floor, once a second.");
            KnockbackReceiver knockback = prefab.GetComponent<KnockbackReceiver>();
            Assert(Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, 35f) &&
                   Mathf.Approximately(knockback.KnockbackWeight, 1.2f) && knockback.IsKnockbackImmune &&
                   prefab.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic,
                "The ambusher must have 35 health, knockback weight 1.2, knockback immunity and a Dynamic body.");

            AmbushEnemyController controller = prefab.GetComponent<AmbushEnemyController>();
            Assert(controller != null &&
                   Mathf.Approximately(controller.BurrowedMoveSpeed, 3.2f) &&
                   Mathf.Approximately(controller.DetectionRange, 12f) &&
                   Mathf.Approximately(controller.EmergeRange, 0.3f) &&
                   Mathf.Approximately(controller.MinimumBurrowDuration, 1f) &&
                   Mathf.Approximately(controller.EmergeTelegraphDuration, 0.7f) &&
                   Mathf.Approximately(controller.AttackDuration, 0.3f) &&
                   Mathf.Approximately(controller.SurfacedDuration, 2f) &&
                   Mathf.Approximately(controller.BurrowDuration, 0.4f) &&
                   Mathf.Approximately(controller.HitRadius, 1f) &&
                   controller.DamageTier == EnemyDamageTier.Medium,
                "The ambusher must move at 3.2 buried, detect at 12, burst within 0.3 of the feet after 1 s buried, warn " +
                "0.7 s, hit a radius-1 medium ellipse, stay up 0.3 + 2 s and sink in 0.4 s.");

            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            SpriteRenderer renderer = view != null ? view.Visual : null;
            Assert(view != null && view.ArtworkId == Week27Enemy13Setup.ArtworkId &&
                   view.Mode == FairyKingdomArtworkMode.Fixed && renderer != null && renderer.color == Color.white &&
                   Mathf.Approximately(view.LocalWidth, Week27Enemy13Setup.ArtworkWidth) &&
                   AssetDatabase.GetAssetPath(renderer.sprite) == Week27Enemy13Setup.ArtworkPath,
                $"The ambusher must show the user's '{Week27Enemy13Setup.ArtworkId}' drawing untinted at width " +
                $"{Week27Enemy13Setup.ArtworkWidth}.");

            // The chaser it was copied from keeps its chase and melee.
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath);
            Assert(chaser != null && chaser.GetComponent<EnemyChase>() != null &&
                   chaser.GetComponent<AmbushEnemyController>() == null,
                "The chaser Prefab must stay a plain chaser.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.Ambusher, out int threat) && threat == 4 &&
                   threat == Week27Enemy13Setup.Threat, "The ambusher's threat score must be 4.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.Ambusher, out GameObject bound, out error) && bound == prefab,
                $"The roster must bind the ambusher role to its Prefab. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Ambusher) ==
                   SpawnPointPlacementRole.MeleePressure, "The ambusher must use melee pressure SpawnPoints.");
            Assert(EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.Ambusher) == 3,
                "The ambusher must first appear on floor 3.");

            EncounterEnemyCandidate[] expected = Week27Enemy13Setup.PressureMeleeCandidates();
            Assert(expected.Length == 6 && expected[5].EnemyRole == EncounterEnemyRole.Ambusher &&
                   expected[5].Weight == 15,
                "The pressure melee list must be Enemy-10's five plus the ambusher at 15.");
            int found = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidate(out error), error);
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                EncounterSpawnRule[] rules = definition.Waves.SelectMany(wave => wave.SpawnRules)
                    .Where(rule => rule.Candidates.Any(candidate => candidate.EnemyRole == EncounterEnemyRole.Ambusher))
                    .ToArray();
                bool pressure = Week25Enemy9Setup.IsPressure(definition);
                Assert((rules.Length > 0) == pressure,
                    $"Only the pressure Encounters may hold the ambusher, but '{definition.EncounterId}' does not " +
                    "follow that.");
                if (!pressure) continue;
                found++;
                Assert(rules.Length == 2 && rules.All(rule => rule.Candidates.Count == expected.Length &&
                           expected.All(candidate => Weight(rule, candidate.EnemyRole) == candidate.Weight)),
                    $"'{definition.EncounterId}' melee slots in both waves must weigh " +
                    string.Join("/", expected.Select(candidate => candidate.Weight)) + ".");
                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out error) &&
                       minimum == Week19Difficulty1Setup.PressureMinimumThreat &&
                       maximum == Week19Difficulty1Setup.PressureMaximumThreat &&
                       definition.DeclaredMinimumThreat == minimum && definition.DeclaredMaximumThreat == maximum,
                    $"'{definition.EncounterId}' must keep its 10~16 range. {error}");
            }

            Assert(EncounterEnemyFloorPool.EffectiveWeight(expected[5], 2) == 0 &&
                   EncounterEnemyFloorPool.EffectiveWeight(expected[5], 3) == Week27Enemy13Setup.MeleeWeight * 3,
                "The ambusher must weigh nothing on floor 2 and x1.5 on floor 3.");
            Assert(found == Week19Spawn2Setup.Profiles.Length,
                $"Every pressure Encounter must hold the ambusher ({found}).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            int rooms = 0, bodies = 0, pressureFloorThree = 0;
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
                    bool pressure = room.EncounterId != null &&
                                    room.EncounterId.EndsWith("-pressure-v1", StringComparison.Ordinal);
                    if (room.FloorNumber == 3 && pressure) pressureFloorThree++;
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves.SelectMany(wave => wave)
                        .Where(spawn => spawn.Role == EncounterEnemyRole.Ambusher).ToArray();
                    if (spawns.Length == 0) continue;
                    Assert(pressure && room.FloorNumber == 3,
                        $"Seed {seed} placed an ambusher in '{room.EncounterId}' on floor {room.FloorNumber}.");
                    Assert(spawns.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role) &&
                                               (room.Template.SpawnPointRoles[spawn.SpawnPointIndex] &
                                                SpawnPointPlacementRole.MeleePressure) != 0),
                        $"Seed {seed} room '{room.RoomId}' put an ambusher off a melee pressure point.");
                    rooms++;
                    bodies += spawns.Length;
                    perFloor[room.FloorNumber] += spawns.Length;
                    if (runtimeSeed == 0) runtimeSeed = seed;
                }
            }

            Assert(rooms > 0 && perFloor[1] == 0 && perFloor[2] == 0 && perFloor[3] > 0,
                $"{GeneratedSeedCount} seeds must place the ambusher on floor 3 only " +
                $"({perFloor[1]}/{perFloor[2]}/{perFloor[3]}).");
            Assert(runtimeSeed > 0, "No floor-3 ambusher room was generated for runtime verification.");
            return $"{bodies} ambushers in {rooms} of {pressureFloorThree} floor-3 pressure rooms, floors " +
                   $"{perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 3;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.Ambusher), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} ambushers, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its ambusher rooms.");
        }

        // Buried, closing in, warning, bursting out onto the player, staying up and sinking again.
        private static void ValidateCycle(GameObject prefab)
        {
            GameObject root = new("Enemy-13 Cycle Verification");
            try
            {
                Vector2 at = Origin;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 6f, 100f, "Player");
                player.EnableHealthUnits();
                Health health = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(health.gameObject, 3);
                AmbushEnemyController ambusher = health.GetComponent<AmbushEnemyController>();
                EnemyBehaviorContext behavior = health.GetComponent<EnemyBehaviorContext>();
                Assert(EnemyFeet.TryGetBody(health.gameObject, out CircleCollider2D bodyCircle),
                    "The ambusher needs its body circle.");
                EnemyFeet feet = health.GetComponentInChildren<EnemyFeet>();
                float start = ambusher.StateStartedAt;

                RequireBuried(ambusher, health, bodyCircle, feet, "A spawned ambusher");
                health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(!health.IsDead && Mathf.Approximately(health.CurrentHealth, health.MaxHealth),
                    "A buried ambusher must not take damage.");

                ambusher.TickBehavior(start);
                Assert(ambusher.State == AmbushState.Burrowed && ambusher.Velocity == Vector2.zero,
                    "A buried ambusher must not move before it has a target.");
                behavior.BeginCombat(player.transform);
                behavior.BeginSpawnGrace(start);
                ambusher.TickBehavior(start);
                Assert(ambusher.Velocity == Vector2.zero, "A buried ambusher must not move during its spawn delay.");
                ClearSpawnDelay(behavior);
                ambusher.TickBehavior(start + 0.1f);
                Assert(ambusher.State == AmbushState.Burrowed && ambusher.Velocity.x > 3f &&
                       Mathf.Abs(ambusher.Velocity.magnitude - 3.2f) < 0.01f,
                    "An alerted buried ambusher must move toward the player at 3.2.");

                // In front of the player is not enough: it keeps going until it is under the feet.
                player.transform.position = at + Vector2.right * 0.8f;
                Physics2D.SyncTransforms();
                ambusher.TickBehavior(start + 1.5f);
                Assert(ambusher.State == AmbushState.Burrowed && ambusher.Velocity.x > 0.1f,
                    "0.8 from the player's feet the ambusher must keep moving toward them, not burst out.");

                // Under the feet but buried less than a second: it waits there.
                player.transform.position = at + Vector2.right * 0.2f;
                Physics2D.SyncTransforms();
                ambusher.TickBehavior(start + 0.5f);
                Assert(ambusher.State == AmbushState.Burrowed && ambusher.Velocity == Vector2.zero,
                    "Under the feet but buried under 1 s, the ambusher must wait under the floor.");
                ambusher.TickBehavior(start + 1f + Step);
                Assert(ambusher.State == AmbushState.Emerging && ambusher.Mark != null && ambusher.Mark.enabled &&
                       Vector2.Distance(ambusher.Mark.transform.position, ambusher.EmergePoint) < 0.001f &&
                       Vector2.Distance(ambusher.EmergePoint, at) < 0.001f &&
                       Mathf.Abs(ambusher.Mark.bounds.size.x - 2f) < 0.01f &&
                       Mathf.Abs(ambusher.Mark.bounds.size.y - 1f) < 0.01f &&
                       ambusher.Mark.color == EnemyLobProjectile.MarkColor(0f),
                    "Under the feet after 1 s buried, the ambusher must stop and show a 2 x 1 red ellipse where it is.");
                RequireBuried(ambusher, health, bodyCircle, feet, "A warning ambusher");

                float emergeStart = ambusher.StateStartedAt;
                float before = player.CurrentHealth;
                ambusher.TickBehavior(emergeStart + 0.69f);
                Assert(ambusher.State == AmbushState.Emerging && Mathf.Approximately(player.CurrentHealth, before),
                    "The warning must last 0.7 s without hurting.");
                ambusher.TickBehavior(emergeStart + 0.7f + Step);
                int expectedUnits = HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 3);
                Assert(ambusher.State == AmbushState.Attacking && ambusher.LastHitCount == 1 &&
                       Mathf.Approximately(before - player.CurrentHealth, expectedUnits),
                    $"Bursting out must hurt the player on the ellipse once for floor 3's medium damage " +
                    $"({expectedUnits} units).");
                Assert(!health.IsInvulnerable && bodyCircle.enabled && ambusher.Mark == null &&
                       ambusher.Visual.color == AmbushEnemyController.AttackColor,
                    "A bursting ambusher must be hittable, drop its mark and show the attack colour.");
                KnockbackReceiver knockback = health.GetComponent<KnockbackReceiver>();
                Assert(ambusher.IsAnchored && !knockback.Apply(Vector2.left, 6f, 0.3f, 0f) &&
                       !knockback.ApplyPush(Vector2.left, 6f, 0.2f) && ambusher.Velocity == Vector2.zero,
                    "Up out of the ground the ambusher must be Kinematic and ignore knockback and pushes.");

                float attackStart = ambusher.StateStartedAt;
                ambusher.TickBehavior(attackStart + 0.3f + Step);
                Assert(ambusher.State == AmbushState.Surfaced && ambusher.Visual.color == Color.white,
                    "After 0.3 s the ambusher must stand up in its plain colours.");
                health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 5f));
                Assert(Mathf.Approximately(health.CurrentHealth, health.MaxHealth - 5f),
                    "A surfaced ambusher must take damage.");
                float surfacedStart = ambusher.StateStartedAt;
                ambusher.TickBehavior(surfacedStart + 1.99f);
                Assert(ambusher.State == AmbushState.Surfaced, "The ambusher must stay up for 2 s.");
                ambusher.TickBehavior(surfacedStart + 2f + Step);
                Assert(ambusher.State == AmbushState.Burrowing && !health.IsInvulnerable,
                    "After 2 s up it must start sinking, still hittable.");
                ambusher.TickBehavior(ambusher.StateStartedAt + 0.4f + Step);
                RequireBuried(ambusher, health, bodyCircle, feet, "A sunk ambusher");
                Assert(!ambusher.IsAnchored, "A sunk ambusher must be Dynamic again so walls stop its feet.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // A player outside the ellipse and another enemy inside it are not hurt.
        private static void ValidateMissAndOthers(GameObject prefab)
        {
            GameObject root = new("Enemy-13 Miss Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 30f;
                // 0.6 below is inside the 1.2 burst range but outside the ellipse's 0.5 half height.
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.down * 0.6f, 100f, "Player");
                player.EnableHealthUnits();
                Health other = CreateHealthTarget(root.transform, "Other Enemy", at + Vector2.right * 0.5f, 50f,
                    "Enemy");
                Health health = Spawn(prefab, root.transform, at);
                EnemyFloorLevel.Apply(health.gameObject, 2);
                AmbushEnemyController ambusher = health.GetComponent<AmbushEnemyController>();
                Physics2D.SyncTransforms();
                ambusher.BeginEmerging(ambusher.StateStartedAt);
                Assert(ambusher.State == AmbushState.Emerging, "The ambusher must start its warning.");
                ambusher.TickBehavior(ambusher.StateStartedAt + 0.7f + Step);
                Assert(ambusher.State == AmbushState.Attacking && ambusher.LastHitCount == 0 &&
                       Mathf.Approximately(player.CurrentHealth, player.MaxHealth) &&
                       Mathf.Approximately(other.CurrentHealth, other.MaxHealth),
                    "Bursting out must not hurt a player outside the ellipse or another enemy inside it.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // No forced burst (user request, 2026-10-30): far from the player it keeps following however long it burrows.
        private static void ValidateFollowsWithoutForcedBurst(GameObject prefab)
        {
            GameObject root = new("Enemy-13 Follow Verification");
            try
            {
                Vector2 at = Origin + Vector2.right * 60f;
                Health player = CreateHealthTarget(root.transform, "Player", at + Vector2.right * 8f, 100f, "Player");
                Health health = Spawn(prefab, root.transform, at);
                AmbushEnemyController ambusher = health.GetComponent<AmbushEnemyController>();
                EnemyBehaviorContext behavior = health.GetComponent<EnemyBehaviorContext>();
                behavior.BeginCombat(player.transform);
                ClearSpawnDelay(behavior);
                float start = ambusher.StateStartedAt;
                ambusher.TickBehavior(start + 30f);
                Assert(ambusher.State == AmbushState.Burrowed && ambusher.Velocity.x > 3f,
                    "Far from the player the ambusher must stay buried and keep following, however long it burrows.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Buried ambushers keep the room in combat; hit while up, they die and the room clears.
        private static void ValidateRoomClears(GameObject prefab)
        {
            GameObject root = new("Enemy-13 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 180f;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Enemy-13 Spawn {index + 1}");
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
                AmbushEnemyController[] ambushers = root.GetComponentsInChildren<AmbushEnemyController>();
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2 && ambushers.Length == 2,
                    "The room must hold two ambushers.");
                foreach (AmbushEnemyController ambusher in ambushers) InvokeAwake(ambusher);
                foreach (AmbushEnemyController ambusher in ambushers)
                    ambusher.GetComponent<Health>().TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2,
                    "Buried ambushers must survive hits and keep the room in combat.");
                foreach (AmbushEnemyController ambusher in ambushers)
                {
                    ambusher.BeginEmerging(ambusher.StateStartedAt);
                    ambusher.TickBehavior(ambusher.StateStartedAt + 0.7f + Step);
                    ambusher.GetComponent<Health>().TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                }
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "The room must clear once both ambushers are killed while up.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void RequireBuried(AmbushEnemyController ambusher, Health health, CircleCollider2D bodyCircle,
            EnemyFeet feet, string subject)
        {
            Assert(ambusher.IsBurrowed && health.IsInvulnerable && !bodyCircle.enabled &&
                   feet != null && feet.Collider.enabled &&
                   ambusher.Visual.color == AmbushEnemyController.BurrowedColor,
                $"{subject} must be buried: invulnerable, body circle off, feet on and tinted dim brown.");
        }

        private static Health Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            InvokeAwake(instance.GetComponent<Health>());
            InvokeAwake(instance.GetComponent<KnockbackReceiver>());
            InvokeAwake(instance.GetComponent<EnemyBehaviorContext>());
            InvokeAwake(instance.GetComponent<AmbushEnemyController>());
            Physics2D.SyncTransforms();
            return instance.GetComponent<Health>();
        }

        private static void ClearSpawnDelay(EnemyBehaviorContext behavior)
        {
            typeof(EnemyBehaviorContext)
                .GetField("spawnActionReadyTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(behavior, float.NegativeInfinity);
        }

        private static Health CreateHealthTarget(Transform parent, string name, Vector2 position, float maxHealth,
            string layerName)
        {
            GameObject target = new($"Enemy-13 {name}", typeof(CircleCollider2D), typeof(Health));
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
            GameObject player = new("Enemy-13 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
