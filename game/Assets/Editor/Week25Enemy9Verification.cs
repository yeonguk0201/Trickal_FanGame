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
    public static class Week25Enemy9Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6600f, 6600f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        [MenuItem("Trickal Fan Game/Week 25/Setup and Verify Enemy-9 Nururing Fairy Warrior")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Week19Spawn2Setup.Profiles
                .SelectMany(profile => new[]
                {
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "pressure"),
                    Week19Spawn2Setup.EncounterPath(profile.ProfileId, "crossfire"),
                })
                .Append(Week25Enemy9Setup.ArtworkPath).Append(FairyKingdomArtworkSetup.CatalogPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Dictionary<string, string> artwork = FairyKingdomArtworkVerification.SnapshotBoundArtwork();
            Week25Enemy9Setup.Setup();
            Dictionary<string, string> created = Week25Enemy9Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week25Enemy9Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-9 setup changed or lost the GUID for {entry.Key}.");
            }

            // Only the two new Prefabs may gain a drawing; every other bound drawing stays as it was.
            FairyKingdomArtworkVerification.RequireBoundArtworkUnchanged(artwork, "Enemy-9 setup");

            foreach (string path in Week25Enemy9Setup.CreatedAssetPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(prefab.GetComponents<FairyKingdomArtworkView>().Length == 1 &&
                       prefab.GetComponents<EnemyChase>().Length == 1 &&
                       prefab.GetComponents<EnemySplitOnDeath>().Length <= 1 && prefab.transform.childCount == 0,
                    $"Re-running Enemy-9 setup must not duplicate the components or children of {path}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-9 Nururing Fairy Warrior")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week25Enemy9Setup.EncounterContentVersion,
                "Run Enemy-9 setup before verification.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Enemy9Setup.PrefabPath);
            GameObject split = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Enemy9Setup.SplitPrefabPath);
            Assert(prefab != null && split != null,
                $"Enemy-9 needs {Week25Enemy9Setup.PrefabPath} and {Week25Enemy9Setup.SplitPrefabPath}.");
            ValidatePrefabs(prefab, split);
            ValidateCatalog(generator, generator.DifficultyTable, assembler.EnemyRoster, prefab);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, prefab, runtimeSeed);
            ValidateSplitOnEveryCause(prefab, split);
            ValidateRoomClears(prefab, split);
            Debug.Log($"Enemy-9 verification passed: {Week25Enemy9Setup.DisplayName} has 40 health, speed 2.5, " +
                      "knockback weight 1.2 and the chaser's lunge at medium damage. Whatever kills it (attack, bomb, " +
                      "poison, trap) it splits once into two 0.7-scale small nururing 0.5 to its left and right (10 " +
                      "health, speed 3.25, light contact, knockback weight 0.7) that chase its target after the " +
                      "0.3-second spawn delay and never split again. Threat 4 on melee pressure points in the " +
                      $"pressure melee slots only (50/25/15/10, declared 10~16); {GeneratedSeedCount} seeds select " +
                      $"it reproducibly ({counts}), runtime seed {runtimeSeed} binds its Prefab, and a wave or room " +
                      "clears only after the splits die too.");
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-9 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            // A runtime binding check stores its seed's graph in the scene's Run progress, so each check starts from
            // a freshly loaded scene.
            Action[] checks =
            {
                Verify, Week25Enemy8Verification.Verify, Week25Enemy7Verification.Verify,
                Week19Spawn2Verification.Verify, Week19Difficulty1Verification.Verify,
                Week19Encounter4Verification.Verify, FairyKingdomArtworkVerification.Verify,
            };
            foreach (Action check in checks)
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                check();
            }

            Debug.Log("Enemy-9 regression verification passed.");
        }

        private static void ValidatePrefabs(GameObject prefab, GameObject split)
        {
            foreach (GameObject candidate in new[] { prefab, split })
            {
                Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(candidate) == 0 &&
                       candidate.GetComponent<EnemyAttackArtwork>() == null &&
                       candidate.GetComponent<EnemyMovementAnimator>() == null &&
                       candidate.GetComponent<TestEnemy>() != null &&
                       candidate.GetComponent<EnemyBehaviorContext>() != null,
                    $"{candidate.name} must be a chaser without the old chaser's drawn frames.");
                FairyKingdomArtworkView view = candidate.GetComponent<FairyKingdomArtworkView>();
                SpriteRenderer renderer = candidate.GetComponent<SpriteRenderer>();
                Assert(view != null && view.ArtworkId == Week25Enemy9Setup.ArtworkId && view.Visual == renderer &&
                       renderer != null && renderer.color == Color.white &&
                       Mathf.Approximately(view.LocalWidth, Week25Enemy9Setup.ArtworkWidth) &&
                       AssetDatabase.GetAssetPath(renderer.sprite) == Week25Enemy9Setup.ArtworkPath,
                    $"{candidate.name} must show the '{Week25Enemy9Setup.ArtworkId}' drawing untinted.");
            }

            Assert(prefab.name == Week25Enemy9Setup.PrefabName && prefab.transform.localScale == Vector3.one,
                "The nururing Prefab must keep its name at full scale.");
            Assert(Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, 40f) &&
                   Mathf.Approximately(prefab.GetComponent<KnockbackReceiver>().KnockbackWeight, 1.2f) &&
                   Mathf.Approximately(prefab.GetComponent<EnemyChase>().MoveSpeed, 2.5f) &&
                   prefab.GetComponent<ContactDamage>().DamageTier == EnemyDamageTier.Light,
                "The nururing must have 40 health, knockback weight 1.2, speed 2.5 and light contact damage.");
            MeleeEnemyAttack melee = prefab.GetComponent<MeleeEnemyAttack>();
            MeleeEnemyAttack chaser = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath)
                .GetComponent<MeleeEnemyAttack>();
            Assert(melee != null && prefab.GetComponent<EnemyAttackPresentation>() != null &&
                   melee.DamageTier == EnemyDamageTier.Medium && melee.IsLunge &&
                   Mathf.Approximately(melee.AttackRange, chaser.AttackRange) &&
                   Mathf.Approximately(melee.TelegraphDuration, chaser.TelegraphDuration) &&
                   Mathf.Approximately(melee.ActiveDuration, chaser.ActiveDuration) &&
                   Mathf.Approximately(melee.RecoveryDuration, chaser.RecoveryDuration) &&
                   Mathf.Approximately(melee.LungeTriggerRange, chaser.LungeTriggerRange) &&
                   Mathf.Approximately(melee.LungeDistance, chaser.LungeDistance) &&
                   Mathf.Approximately(melee.LungeReach, chaser.LungeReach),
                "The nururing must use the chaser's melee lunge at medium damage.");
            EnemySplitOnDeath splitter = prefab.GetComponent<EnemySplitOnDeath>();
            Assert(splitter != null && splitter.SplitPrefab == split && splitter.SplitCount == 2 &&
                   Mathf.Approximately(splitter.SideOffset, 0.5f),
                "The nururing must split into two small nururing 0.5 to its left and right.");

            Assert(split.name == Week25Enemy9Setup.SplitPrefabName &&
                   split.GetComponent<EnemySplitOnDeath>() == null &&
                   split.GetComponent<MeleeEnemyAttack>() == null &&
                   split.GetComponent<EnemyAttackPresentation>() == null,
                "A small nururing must only ram and never split again.");
            Assert(Mathf.Approximately(split.GetComponent<Health>().MaxHealth, 10f) &&
                   Mathf.Approximately(split.GetComponent<EnemyChase>().MoveSpeed, 3.25f) &&
                   Mathf.Approximately(split.GetComponent<EnemyChase>().StopDistance, 0f) &&
                   Mathf.Approximately(split.GetComponent<KnockbackReceiver>().KnockbackWeight, 0.7f) &&
                   split.GetComponent<ContactDamage>().DamageTier == EnemyDamageTier.Light &&
                   HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1) == HealthUnits.UnitsPerHeart,
                "A small nururing must have 10 health, speed 3.25, knockback weight 0.7 and one-heart contact damage.");
            Assert(Mathf.Approximately(split.transform.localScale.x, 0.7f) &&
                   Mathf.Approximately(split.transform.localScale.y, 0.7f) &&
                   split.transform.localScale.x < prefab.transform.localScale.x,
                "A small nururing must be drawn and collide at 0.7 of the body's size.");
            Assert(Mathf.Approximately(EnemyBehaviorContext.SpawnActionDelaySeconds, 0.3f),
                "A split's spawn delay before acting must be 0.3 seconds.");
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject prefab)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.SplittingChaser, out int threat) && threat == 4 &&
                   threat == Week25Enemy9Setup.Threat,
                "The nururing's threat score must be 4.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.SplittingChaser, out GameObject bound, out error) &&
                   bound == prefab, $"The roster must bind the splitting role to the nururing. {error}");
            Assert(roster.Bindings.All(binding => binding.Prefab == null ||
                                                  binding.Prefab.name != Week25Enemy9Setup.SplitPrefabName),
                "Small nururing only appear by splitting, never from an Encounter.");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.SplittingChaser) ==
                   SpawnPointPlacementRole.MeleePressure,
                "The nururing must use melee pressure SpawnPoints.");

            EncounterEnemyCandidate[] expected = Week25Enemy9Setup.PressureMeleeCandidates();
            int found = 0;
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                EncounterSpawnRule[] rules = definition.Waves.SelectMany(wave => wave.SpawnRules).ToArray();
                bool holds = rules.SelectMany(rule => rule.Candidates)
                    .Any(candidate => candidate.EnemyRole == EncounterEnemyRole.SplittingChaser);
                bool pressure = Week25Enemy9Setup.IsPressure(definition);
                Assert(holds == pressure,
                    $"Only the pressure Encounters may hold the nururing, but '{definition.EncounterId}' does not " +
                    "follow that.");
                if (!holds) continue;
                found++;
                foreach (EncounterSpawnRule rule in rules.Where(rule => rule.Candidates.Any(candidate =>
                             candidate.EnemyRole == EncounterEnemyRole.SplittingChaser)))
                {
                    Assert(rule.Candidates.Count == expected.Length &&
                           expected.All(candidate => Weight(rule, candidate.EnemyRole) == candidate.Weight),
                        $"'{definition.EncounterId}' melee slots must weigh 50/25/15/10.");
                }

                Assert(table.TryComputeThreatRange(definition, out int minimum, out int maximum, out error) &&
                       minimum == 10 && maximum == 16 && definition.DeclaredMinimumThreat == 10 &&
                       definition.DeclaredMaximumThreat == 16 &&
                       Week19Difficulty1Setup.PressureMaximumThreat == 16,
                    $"'{definition.EncounterId}' must compute and declare threat 10~16. {error}");
            }

            Assert(found == Week19Spawn2Setup.Profiles.Length,
                $"Every pressure Encounter must hold the nururing ({found}).");
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
                        .Where(spawn => spawn.Role == EncounterEnemyRole.SplittingChaser).ToArray();
                    if (spawns.Length == 0) continue;
                    Assert(room.EncounterId.EndsWith("-pressure-v1", StringComparison.Ordinal),
                        $"Seed {seed} placed a nururing in '{room.EncounterId}'.");
                    Assert(spawns.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role) &&
                                               (room.Template.SpawnPointRoles[spawn.SpawnPointIndex] &
                                                SpawnPointPlacementRole.MeleePressure) != 0),
                        $"Seed {seed} room '{room.RoomId}' put a nururing off a melee pressure point.");
                    rooms++;
                    bodies += spawns.Length;
                    perFloor[room.FloorNumber]++;
                    if (runtimeSeed == 0 && room.FloorNumber == 1) runtimeSeed = seed;
                }
            }

            Assert(rooms > 0 && perFloor[1] > 0 && perFloor[2] > 0 && perFloor[3] > 0,
                $"{GeneratedSeedCount} seeds must place the nururing on every floor.");
            Assert(runtimeSeed > 0, "No floor-1 nururing room was generated for runtime verification.");
            return $"{bodies} nururing in {rooms} of {combatRooms} combat rooms, floors " +
                   $"{perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject prefab, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            Dictionary<string, int> expected = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == 1)
                .ToDictionary(node => node.RoomId, node => node.ResolvedEncounterWaves.SelectMany(wave => wave)
                    .Count(spawn => spawn.Role == EncounterEnemyRole.SplittingChaser), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!expected.TryGetValue(room.Node.RoomId, out int count) || count == 0) continue;
                int actual = room.Controller.EncounterWaves.Sum(wave => wave.EnemyPrefabs.Count(p => p == prefab));
                Assert(actual == count,
                    $"Runtime room '{room.Node.RoomId}' must spawn {count} nururing, but binds {actual}.");
                bound++;
            }

            Assert(bound > 0 && bound == expected.Count(entry => entry.Value > 0),
                $"Runtime seed {seed} did not bind its nururing rooms.");
        }

        // Every kind of death splits the body once; the splits chase its target after the spawn delay and never
        // split again.
        private static void ValidateSplitOnEveryCause(GameObject prefab, GameObject splitPrefab)
        {
            GameObject root = new("Enemy-9 Split Verification");
            try
            {
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 6f);
                DamageSourceType[] causes =
                {
                    DamageSourceType.PlayerAttack, DamageSourceType.PlayerBomb, DamageSourceType.PlayerStatusEffect,
                    DamageSourceType.Environment, DamageSourceType.Unknown,
                };
                for (int index = 0; index < causes.Length; index++)
                {
                    Vector2 at = Origin + Vector2.right * (index * 4f);
                    Health body = Spawn(prefab, root.transform, at);
                    EnemyFloorLevel.Apply(body.gameObject, 2);
                    body.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                    EnemySplitOnDeath splitter = body.GetComponent<EnemySplitOnDeath>();
                    int raised = 0;
                    splitter.SplitSpawned += _ => raised++;
                    body.TakeDamage(new DamageContext(null, causes[index], 999f));
                    Assert(body.IsDead && raised == 1 && splitter.LastSplits.Count == 2,
                        $"A nururing killed by {causes[index]} must split into two.");

                    Vector3[] expected = { (Vector3)at + Vector3.left * 0.5f, (Vector3)at + Vector3.right * 0.5f };
                    foreach (Health split in splitter.LastSplits)
                    {
                        Assert(split.name.StartsWith(Week25Enemy9Setup.SplitPrefabName, StringComparison.Ordinal) &&
                               split.transform.parent == root.transform && !split.IsDead &&
                               split.GetComponent<EnemySplitOnDeath>() == null,
                            "A split must be a small nururing next to the body that cannot split again.");
                        Assert(expected.Any(position => (split.transform.position - position).sqrMagnitude < 0.0001f),
                            $"A split must appear 0.5 to the left or right of the body, not at {split.transform.position}.");
                        // Curve-0: normal enemies, splits included, keep their base health on every floor.
                        Assert(EnemyFloorLevel.Resolve(split.gameObject) == 2 &&
                               Mathf.Approximately(split.MaxHealth, splitPrefab.GetComponent<Health>().MaxHealth),
                            "A split outside a room must keep the body's floor and its base health.");
                        EnemyBehaviorContext behavior = split.GetComponent<EnemyBehaviorContext>();
                        float actionReady = (float)typeof(EnemyBehaviorContext)
                            .GetField("spawnActionReadyTime", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(behavior);
                        Assert(behavior.IsAlerted && behavior.Target == player.transform &&
                               behavior.IsSpawnActionDelayed && behavior.IsActionSuppressed &&
                               Mathf.Abs(actionReady - Time.time - 0.3f) < 0.0001f &&
                               behavior.IsSpawnContactGraceActive(Time.time + 0.29f),
                            "A split must chase the body's target but not act for 0.3 seconds.");
                    }

                    Assert((splitter.LastSplits[0].transform.position - splitter.LastSplits[1].transform.position)
                           .magnitude > 0.99f, "The two splits must stand apart, left and right.");
                    foreach (Health split in splitter.LastSplits.ToArray())
                        split.TakeDamage(new DamageContext(null, causes[index], 999f));
                    Assert(splitter.LastSplits.All(split => split.IsDead) &&
                           root.GetComponentsInChildren<Health>().All(health => health == player || health.IsDead),
                        "Killing a split must not create more enemies.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Both waves spawn through the room's own path. Killing the bodies leaves their splits holding the wave, and
        // the room clears only when the last split dies.
        private static void ValidateRoomClears(GameObject prefab, GameObject splitPrefab)
        {
            GameObject root = new("Enemy-9 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Enemy-9 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = Origin + Vector2.right * (index + 1) * 3f;
                    return point.transform;
                }).ToArray();
                room.Configure(2, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points),
                    new EncounterRuntimeWave(new[] { prefab }, points.Take(1).ToArray()),
                });
                // Edit mode runs no Awake, so the room's own spawn hook arms the split exactly as Awake would.
                room.EnemySpawned += spawned => InvokeAwake(spawned.GetComponent<EnemySplitOnDeath>());
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                room.BeginCombat(player);
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 1 && room.AliveEnemyCount == 2,
                    "The first wave must hold two nururing.");
                float bodyHealth = root.GetComponentsInChildren<EnemySplitOnDeath>()[0].GetComponent<Health>().MaxHealth;

                Assert(room.KillAliveEnemiesForDevelopment() == 2, "Both nururing of the first wave must die.");
                Health[] splits = root.GetComponentsInChildren<Health>()
                    .Where(health => !health.IsDead && health.GetComponent<PlayerMovement>() == null).ToArray();
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 1 && room.AliveEnemyCount == 4 &&
                       splits.Length == 4 && splits.All(split => split.GetComponent<EnemySplitOnDeath>() == null &&
                                                                 split.transform.parent == root.transform),
                    "Killing the first wave's nururing must leave four splits holding the wave.");
                Assert(splits.All(split => EnemyFloorLevel.Resolve(split.gameObject) == 2 &&
                                           Mathf.Approximately(split.MaxHealth / 10f, bodyHealth / 40f)),
                    "Room splits must scale with the room's floor like the body.");

                KillAll(root, splits.Take(3));
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 1 && room.AliveEnemyCount == 1,
                    "One live split must still hold the first wave.");
                KillAll(root, splits.Skip(3));
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 2 && room.AliveEnemyCount == 1,
                    "The second wave must start after the last split of the first dies.");

                KillAll(root, null);
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 2,
                    "The second wave's nururing must leave two splits before the room clears.");
                KillAll(root, null);
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "Killing every nururing and split must clear the room.");
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
            InvokeAwake(instance.GetComponent<EnemySplitOnDeath>());
            return instance.GetComponent<Health>();
        }

        // Kills the given enemies, or every live enemy under the root when none are given.
        private static void KillAll(GameObject root, IEnumerable<Health> targets)
        {
            foreach (Health enemy in (targets ?? root.GetComponentsInChildren<Health>()).ToArray())
            {
                if (enemy == null || enemy.GetComponent<PlayerMovement>() != null || enemy.IsDead) continue;
                enemy.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
            }
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Enemy-9 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
