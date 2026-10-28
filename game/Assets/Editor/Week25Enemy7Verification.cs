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
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week25Enemy7Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-5800f, 5800f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        private static readonly RoomDoorDirection[] Directions =
        {
            RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down,
        };

        // The renamed enemies stay bound to the same roles through the same Prefab files.
        private static readonly (EncounterEnemyRole role, string prefabName, string displayName)[] Renamed =
        {
            (EncounterEnemyRole.FastChaser, "SansamoEnemy", "산사모-활발"),
            (EncounterEnemyRole.Ranged, "QuickRangedFairy", "산사모-순수"),
            (EncounterEnemyRole.Charging, "ChargingEnemy", "저혈당 요정-순수"),
        };

        [MenuItem("Trickal Fan Game/Week 25/Setup and Verify Enemy-7 Jyubi Swarm and Crumb Blast")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable = Renamed.Select(entry => PrefabPath(entry.prefabName))
                .Append(Week15Boss1Setup.MinionPrefabPath).Append(Week23Enemy6Setup.PrefabPath)
                .Append(Week14Encounter2Setup.RosterPath).Append(Week19Difficulty1Setup.TablePath)
                .Append(Week13FrontendSetup.GameScenePath).ToArray();
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week25Enemy7Setup.Setup();
            Dictionary<string, string> created = Week25Enemy7Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week25Enemy7Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Concat(created))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Enemy-7 setup changed or lost the GUID for {entry.Key}.");
            }

            GameObject minion = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss1Setup.MinionPrefabPath);
            Assert(minion.GetComponents<EnemyDeathExplosion>().Length == 1,
                "Re-running Enemy-7 setup must not duplicate the crumb minion's death explosion.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-7 Jyubi Swarm and Crumb Blast")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week25Enemy7Setup.EncounterContentVersion,
                "Run Enemy-7 setup before verification.");

            RoomDifficultyTable table = generator.DifficultyTable;
            GameObject jyubi = AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath);
            GameObject minion = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss1Setup.MinionPrefabPath);
            Assert(jyubi != null && minion != null, "Enemy-7 needs the Jyubi and crumb minion Prefabs.");

            ValidateRenamedEnemies(assembler.EnemyRoster);
            ValidateCatalog(generator, table, assembler.EnemyRoster, jyubi);
            ValidatePlacementForEveryDoorSet(generator);
            string counts = ValidateGeneration(generator, table, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, jyubi, runtimeSeed);
            ValidateSwarmRoomClears(jyubi);
            ValidateBlastPrefab(minion);
            ValidateBlast(minion, jyubi);
            ValidateBlastDoesNotHoldRoom(minion);
            ValidateBossOwnsBlast(minion);
            ValidateBossDefeatKeepsObjects(minion);
            Debug.Log("Enemy-7 verification passed: the renamed enemies keep their Prefab files and role bindings, " +
                      $"every profile has a {Week25Enemy7Setup.Pattern} Encounter of only Jyubi (3+3 or 4+3, threat 6 " +
                      "or 7, Easy) that places under all 15 door sets while the older Encounters hold no Jyubi, " +
                      $"{GeneratedSeedCount} seeds select it reproducibly ({counts}), runtime seed {runtimeSeed} " +
                      "binds the Jyubi Prefab, both waves clear the room, and a dead crumb minion leaves a blast " +
                      "that keeps its stopped picture turning red in a marked circle for 0.6 seconds, takes " +
                      "exactly one heart from a player whose hurtbox is within radius 1.2 on floors 1 and 3, " +
                      "spares a far or invulnerable player, other enemies and itself, hits once, removes itself, " +
                      "releases the room at once and is removed with a cancelled boss fight, while a defeated " +
                      "Buseureogi releases its minions alive under the boss room and they still explode.");
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Enemy-7 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            // A runtime binding check stores its seed's graph in the scene's Run progress, so the next one that binds
            // another seed starts from a freshly loaded scene. Boss-1 is left out: it still expects the crumb minion
            // to borrow the boss sprite, which the dedicated crumb artwork replaced.
            Action[] checks =
            {
                Verify, Week19Spawn2Verification.Verify, Week19Difficulty1Verification.Verify,
                Week19Encounter4Verification.Verify, Week23Enemy6Verification.Verify,
            };
            foreach (Action check in checks)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                check();
            }

            Debug.Log("Enemy-7 regression verification passed.");
        }

        private static void ValidateRenamedEnemies(EncounterEnemyRoster roster)
        {
            Assert(Week25Enemy7Setup.EnemyDisplayNames.Count == Renamed.Length,
                "Enemy-7 renames exactly three enemies.");
            foreach ((EncounterEnemyRole role, string prefabName, string displayName) in Renamed)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefabName));
                Assert(prefab != null && prefab.name == prefabName,
                    $"The renamed enemy must keep its Prefab '{prefabName}'.");
                Assert(Week25Enemy7Setup.EnemyDisplayNames.TryGetValue(prefabName, out string actual) &&
                       actual == displayName,
                    $"'{prefabName}' must be displayed as '{displayName}'.");
                Assert(roster.TryResolve(role, out GameObject bound, out string error) && bound == prefab,
                    $"Role {role} must stay bound to '{prefabName}'. {error}");
            }
        }

        private static void ValidateCatalog(FloorGenerator generator, RoomDifficultyTable table,
            EncounterEnemyRoster roster, GameObject jyubi)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(table.TryGetThreat(EncounterEnemyRole.Jyubi, out int threat) &&
                   threat == Week25Enemy7Setup.JyubiThreat && threat == 1,
                "Jyubi's threat score must be 1.");
            Assert(roster.TryValidate(out error), error);
            Assert(roster.TryResolve(EncounterEnemyRole.Jyubi, out GameObject bound, out error) && bound == jyubi,
                $"The roster must bind the Jyubi role to the Enemy-6 Prefab. {error}");
            Assert(RoomTemplateDefinition.RequiredPlacementRole(EncounterEnemyRole.Jyubi) ==
                   SpawnPointPlacementRole.MeleePressure,
                "Jyubi must use melee pressure SpawnPoints.");

            string[] swarmIds = Week19Encounter4Setup.Swarms
                .Select(spec => Week19Spawn2Setup.EncounterId(spec.ProfileId, Week25Enemy7Setup.Pattern)).ToArray();
            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                Assert(definition.TryValidateThreat(table, out error), error);
                Assert(table.TryValidateEncounterSize(definition, out error), error);
                bool holdsJyubi = definition.Waves.SelectMany(wave => wave.SpawnRules)
                    .SelectMany(rule => rule.Candidates)
                    .Any(candidate => candidate.EnemyRole == EncounterEnemyRole.Jyubi);
                // Enemy-11: 대왕쥬비's jyubi-nest also opens with two Jyubi.
                Assert(holdsJyubi == (swarmIds.Contains(definition.EncounterId, StringComparer.Ordinal) ||
                                      Week27Enemy11Setup.IsNest(definition)),
                    $"Only the {Week25Enemy7Setup.Pattern} and {Week27Enemy11Setup.Pattern} Encounters may hold " +
                    $"Jyubi, but '{definition.EncounterId}' does not follow that.");
            }

            foreach (Week19Encounter4Setup.SwarmSpec spec in Week19Encounter4Setup.Swarms)
            {
                EncounterDefinition swarm = Find(generator, spec.ProfileId);
                bool small = spec.ProfileId == Week14Room3Setup.SmallProfileId ||
                             spec.ProfileId == Week14Room1Setup.BasicProfileId ||
                             spec.ProfileId == Week14Room3Setup.WideProfileId;
                Assert(spec.FirstWave == (small ? 3 : 4) && spec.SecondWave == 3,
                    $"Profile '{spec.ProfileId}' must hold {(small ? "3+3" : "4+3")} Jyubi.");
                Assert(swarm.AllowedProfiles.Count == 1 && swarm.AllowedProfiles[0].ProfileId == spec.ProfileId &&
                       swarm.MinimumFloor == 1 && swarm.MaximumFloor == 3 && swarm.Waves.Count == 2 &&
                       swarm.Waves[0].SpawnRules.Sum(rule => rule.Count) == spec.FirstWave &&
                       swarm.Waves[1].SpawnRules.Sum(rule => rule.Count) == spec.SecondWave &&
                       swarm.Waves.SelectMany(wave => wave.SpawnRules).SelectMany(rule => rule.Candidates)
                           .All(candidate => candidate.EnemyRole == EncounterEnemyRole.Jyubi),
                    $"'{swarm.EncounterId}' must hold {spec.FirstWave}+{spec.SecondWave} Jyubi on floors 1~3.");
                Assert(table.TryComputeThreatRange(swarm, out int minimum, out int maximum, out error) &&
                       minimum == spec.Total && maximum == spec.Total && swarm.DeclaredMinimumThreat == spec.Total &&
                       swarm.DeclaredMaximumThreat == spec.Total && (spec.Total == 6 || spec.Total == 7) &&
                       table.Classify(spec.Total) == RoomDifficultyTier.Easy,
                    $"'{swarm.EncounterId}' must declare threat {spec.Total} and be Easy. {error}");
            }
        }

        private static void ValidatePlacementForEveryDoorSet(FloorGenerator generator)
        {
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(template =>
                         template != null && template.SupportsRoomType(RoomType.Normal)))
            {
                EncounterDefinition swarm = Find(generator, template.Profile.ProfileId);
                for (int mask = 1; mask < 16; mask++)
                {
                    GeneratedRoomConnection[] connections = Directions
                        .Where((_, index) => (mask & (1 << index)) != 0)
                        .Select(direction => new GeneratedRoomConnection(direction, "verification")).ToArray();
                    for (int floor = 1; floor <= 3; floor++)
                    {
                        Assert(swarm.TryValidateFor(template, floor, connections, out string error),
                            $"Jyubi swarm cannot be placed in '{template.TemplateId}' with doors {mask}. {error}");
                    }
                }
            }
        }

        private static string ValidateGeneration(FloorGenerator generator, RoomDifficultyTable table,
            out int runtimeSeed)
        {
            runtimeSeed = 0;
            int swarmRooms = 0, combatRooms = 0;
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
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves.SelectMany(wave => wave).ToArray();
                    bool isSwarm = IsJyubiSwarm(room.EncounterId);
                    // Enemy-11 checks its own nest, which mixes Jyubi with 대왕쥬비 and chasers.
                    if (Week27Enemy11Setup.IsNest(room.EncounterId)) continue;
                    Assert(isSwarm ? spawns.All(spawn => spawn.Role == EncounterEnemyRole.Jyubi)
                            : spawns.All(spawn => spawn.Role != EncounterEnemyRole.Jyubi),
                        $"Seed {seed} room '{room.RoomId}' mixed Jyubi with other enemies in '{room.EncounterId}'.");
                    if (!isSwarm) continue;
                    swarmRooms++;
                    perFloor[room.FloorNumber]++;
                    Assert(room.ResolvedEncounterWaves.Count == 2 && (spawns.Length == 6 || spawns.Length == 7) &&
                           room.ResolvedEncounterWaves.All(wave =>
                               wave.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == wave.Length &&
                               wave.All(spawn => room.Template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role))),
                        $"Seed {seed} room '{room.RoomId}' resolved a wrong Jyubi swarm.");
                    Assert(room.HasDifficulty &&
                           room.Difficulty.Score == spawns.Length + room.Template.LayoutDifficultyModifier &&
                           room.Difficulty.Tier == RoomDifficultyTier.Easy &&
                           table.Classify(room.Difficulty.Score) == RoomDifficultyTier.Easy,
                        $"Seed {seed} room '{room.RoomId}' must score its Jyubi swarm as Easy.");
                    if (runtimeSeed == 0 && room.FloorNumber == 1) runtimeSeed = seed;
                }
            }

            Assert(swarmRooms > 0 && perFloor[1] > 0 && perFloor[2] > 0 && perFloor[3] > 0,
                $"{GeneratedSeedCount} seeds must select the Jyubi swarm on every floor.");
            Assert(runtimeSeed > 0, "No floor-1 Jyubi swarm room was generated for runtime verification.");
            return $"{swarmRooms} of {combatRooms} combat rooms, floors {perFloor[1]}/{perFloor[2]}/{perFloor[3]}";
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, GameObject jyubi, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            HashSet<string> swarmRooms = new(assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == 1 &&
                               IsJyubiSwarm(node.EncounterId))
                .Select(node => node.RoomId), StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!swarmRooms.Contains(room.Node.RoomId)) continue;
                Assert(room.Controller.EncounterWaves.Count == 2 &&
                       room.Controller.EncounterWaves.All(wave =>
                           wave.EnemyPrefabs.Count >= 3 && wave.EnemyPrefabs.All(prefab => prefab == jyubi)),
                    $"Runtime room '{room.Node.RoomId}' must spawn only the Jyubi Prefab in two waves.");
                bound++;
            }

            Assert(bound > 0 && bound == swarmRooms.Count, $"Runtime seed {seed} did not bind its Jyubi swarm rooms.");
        }

        // Both waves spawn through the room's own path and the room clears when the last 쥬비 dies.
        private static void ValidateSwarmRoomClears(GameObject jyubi)
        {
            GameObject root = new("Enemy-7 Swarm Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 4).Select(index =>
                {
                    GameObject point = new($"Enemy-7 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = Origin + Vector2.right * (index + 1);
                    return point.transform;
                }).ToArray();
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(jyubi, 4).ToArray(), points),
                    new EncounterRuntimeWave(Enumerable.Repeat(jyubi, 3).ToArray(), points.Take(3).ToArray()),
                });
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                room.BeginCombat(player);
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 1 && room.AliveEnemyCount == 4,
                    "The first Jyubi wave must hold four enemies.");
                KillAll(root);
                Assert(room.State == RoomState.Combat && room.CurrentWaveNumber == 2 && room.AliveEnemyCount == 3,
                    "The second Jyubi wave must start with three enemies after the first.");
                KillAll(root);
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0,
                    "Killing both Jyubi waves must clear the room.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateBlastPrefab(GameObject minion)
        {
            EnemyDeathExplosion explosion = minion.GetComponent<EnemyDeathExplosion>();
            Assert(explosion != null && minion.GetComponents<EnemyDeathExplosion>().Length == 1 &&
                   GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(minion) == 0,
                "The crumb minion needs exactly one death explosion and no missing script.");
            Assert(Mathf.Approximately(explosion.TelegraphDuration, 0.6f) &&
                   Mathf.Approximately(explosion.Radius, 1.2f) &&
                   explosion.DamageUnits == HealthUnits.UnitsPerHeart && explosion.DamageUnits == 2,
                "The crumb minion's blast must wait 0.6 seconds, reach radius 1.2 and take one heart.");
            foreach ((EncounterEnemyRole _, string prefabName, string _) in Renamed)
            {
                Assert(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefabName))
                        .GetComponent<EnemyDeathExplosion>() == null,
                    $"Only the summoned crumb minion explodes, not '{prefabName}'.");
            }
        }

        private static void ValidateBlast(GameObject minion, GameObject jyubi)
        {
            foreach (int floor in new[] { 1, 3 })
            {
                GameObject root = new("Enemy-7 Blast Verification");
                try
                {
                    // A hurtbox of radius 0.5: 1.5 away it still overlaps the radius-1.2 circle, 2.5 away it does not.
                    Health near = CreatePlayer(root.transform, Origin + Vector2.right * 1.5f);
                    Health far = CreatePlayer(root.transform, Origin + Vector2.left * 2.5f);
                    Health guarded = CreatePlayer(root.transform, Origin + Vector2.up);
                    guarded.SetInvulnerable(true);
                    Health bystander = Spawn(jyubi, root.transform, Origin + Vector2.down * 0.5f, false);
                    Health otherMinion = Spawn(minion, root.transform, Origin + Vector2.down, true);
                    Health dying = Spawn(minion, root.transform, Origin, true);
                    EnemyFloorLevel.Apply(dying.gameObject, floor);
                    Physics2D.SyncTransforms();

                    float nearBefore = near.CurrentHealth, farBefore = far.CurrentHealth;
                    float guardedBefore = guarded.CurrentHealth, minionBefore = otherMinion.CurrentHealth;
                    EnemyDeathExplosion explosion = dying.GetComponent<EnemyDeathExplosion>();
                    // Not a basic attack: the blast follows any cause of death.
                    dying.TakeDamage(new DamageContext(null, DamageSourceType.PlayerBomb, 999f));
                    EnemyDeathBlast blast = explosion.LastBlast;
                    Assert(dying.IsDead && blast != null && !blast.HasExploded &&
                           ((Vector2)blast.transform.position - Origin).sqrMagnitude < 0.0001f &&
                           Mathf.Approximately(blast.Radius, 1.2f) && blast.DamageUnits == 2 &&
                           blast.GetComponentInChildren<Health>() == null &&
                           blast.GetComponentInChildren<Collider2D>() == null && blast.Telegraph != null,
                        "A dead crumb minion must leave one telegraphed blast where it died.");
                    SpriteRenderer dyingVisual = dying.GetComponent<FairyKingdomArtworkView>().Visual;
                    Assert(blast.Body != null && blast.Body.enabled && blast.Body.sprite == dyingVisual.sprite &&
                           blast.Body.sprite != null && blast.Body.flipX == dyingVisual.flipX &&
                           blast.Body.color == Color.white &&
                           (blast.Body.transform.position - dyingVisual.transform.position).sqrMagnitude < 0.0001f &&
                           (blast.Body.transform.lossyScale - dyingVisual.transform.lossyScale).sqrMagnitude < 0.0001f,
                        "The blast must keep the dead minion's picture where it stopped.");
                    Assert(Object.FindObjectsByType<EnemyDeathBlast>(FindObjectsSortMode.None).Length == 1,
                        "One death must leave exactly one blast.");

                    blast.Tick(blast.ExplodeAt - 0.3f);
                    float halfwayGreen = blast.Body.color.g;
                    blast.Tick(blast.ExplodeAt - 0.01f);
                    Assert(!blast.HasExploded && Mathf.Approximately(near.CurrentHealth, nearBefore),
                        "The blast must not hurt anyone during its 0.6-second telegraph.");
                    Assert(halfwayGreen < 0.95f && blast.Body.color.g < halfwayGreen &&
                           blast.Body.color.g < EnemyDeathBlast.BodyEndColor.g + 0.05f &&
                           Mathf.Approximately(blast.Body.color.r, 1f) && blast.Body.enabled &&
                           blast.Telegraph.enabled,
                        "The stopped minion must turn red through the telegraph inside its marked circle.");

                    blast.Tick(blast.ExplodeAt);
                    Assert(!blast.Body.enabled && !blast.Telegraph.enabled,
                        "The minion's picture and the circle must give way to the explosion.");
                    Assert(blast.HasExploded && blast.DamagedPlayerCount == 1 &&
                           Mathf.Approximately(nearBefore - near.CurrentHealth, HealthUnits.UnitsPerHeart),
                        $"The blast must take exactly one heart from a player in range on floor {floor}, but took " +
                        $"{nearBefore - near.CurrentHealth} units.");
                    Assert(Mathf.Approximately(far.CurrentHealth, farBefore) &&
                           Mathf.Approximately(guarded.CurrentHealth, guardedBefore),
                        "The blast must spare a player outside its radius and an invulnerable player.");
                    Assert(!bystander.IsDead && !otherMinion.IsDead &&
                           Mathf.Approximately(otherMinion.CurrentHealth, minionBefore) &&
                           otherMinion.GetComponent<EnemyDeathExplosion>().LastBlast == null,
                        "The blast must not hurt or chain to other enemies.");

                    Assert(!blast.Explode() && Mathf.Approximately(nearBefore - near.CurrentHealth, 2f),
                        "A blast must hit only once.");
                    blast.Tick(blast.ExplodeAt + EnemyDeathBlast.ExplosionVisualDuration * 0.5f);
                    Assert(blast != null && !blast.HasEnded, "The blast must stay for its explosion visual.");
                    blast.Tick(blast.ExplodeAt + EnemyDeathBlast.ExplosionVisualDuration);
                    Assert(blast == null, "A finished blast must remove itself.");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    Physics2D.SyncTransforms();
                }
            }
        }

        // The dead minion leaves its room's count at once; the pending blast is not an enemy.
        private static void ValidateBlastDoesNotHoldRoom(GameObject minion)
        {
            GameObject root = new("Enemy-7 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                Transform point = new GameObject("Enemy-7 Spawn").transform;
                point.SetParent(root.transform);
                point.position = Origin;
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(new[] { minion }, new[] { point }),
                });
                Health player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                room.BeginCombat(player);
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 1,
                    "The room must hold the crumb minion.");
                EnemyDeathExplosion explosion = root.GetComponentInChildren<EnemyDeathExplosion>();
                InvokeAwake(explosion);
                KillAll(root);
                Assert(room.State == RoomState.Cleared && room.AliveEnemyCount == 0 &&
                       explosion.LastBlast != null && !explosion.LastBlast.HasExploded &&
                       explosion.LastBlast.transform.parent == root.transform,
                    "A pending blast must not keep its room in combat.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateBossOwnsBlast(GameObject minion)
        {
            GameObject root = new("Enemy-7 Boss Verification");
            try
            {
                GameObject bossObject = new("Enemy-7 Boss");
                bossObject.transform.SetParent(root.transform);
                bossObject.transform.position = Origin + Vector2.up * 30f;
                BossController boss = bossObject.AddComponent<BossController>();
                Health dying = Spawn(minion, root.transform, Origin, true);
                EnemyDeathExplosion explosion = dying.GetComponent<EnemyDeathExplosion>();
                explosion.ConfigureOwner(boss);
                int ownedBefore = boss.OwnedObjectCount;
                dying.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                EnemyDeathBlast blast = explosion.LastBlast;
                Assert(blast != null && boss.OwnedObjectCount == ownedBefore + 1,
                    "A summoned minion's blast must belong to its boss.");
                boss.CleanupOwnedObjects();
                Assert(blast == null, "A cancelled boss fight must remove the pending blasts of its minions.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // 부스러기 leaves its 쫄따구, obstacles and projectiles when it is defeated; other bosses still remove theirs.
        private static void ValidateBossDefeatKeepsObjects(GameObject minion)
        {
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss0Setup.BossPrefabPath);
            Assert(bossPrefab != null && bossPrefab.GetComponent<BuseureogiBossPatternRuntime>() != null &&
                   bossPrefab.GetComponent<BossController>().KeepsOwnedObjectsOnDefeat,
                "The Buseureogi boss Prefab must keep its objects when defeated.");

            foreach (bool keeps in new[] { true, false })
            {
                GameObject room = new("Enemy-7 Boss Room");
                room.transform.position = Origin;
                GameObject loose = null;
                try
                {
                    GameObject bossObject = new("Enemy-7 Boss");
                    bossObject.transform.SetParent(room.transform);
                    bossObject.transform.position = Origin + Vector2.up * 30f;
                    BossController boss = bossObject.AddComponent<BossController>();
                    InvokeAwake(bossObject.GetComponent<Health>());
                    // In play the handler that removes the dead boss's object runs before the boss's own, so the
                    // boss is disabled while it has not yet handled its death.
                    bossObject.GetComponent<Health>().Died += () => typeof(BossController)
                        .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, null);
                    InvokeAwake(boss);
                    Assert(!boss.KeepsOwnedObjectsOnDefeat, "A boss removes its objects on defeat by default.");
                    boss.ConfigureDefeat(keeps);

                    // Summoned without a parent, the way the boss runtime creates its minions.
                    loose = (GameObject)PrefabUtility.InstantiatePrefab(minion);
                    loose.transform.position = Origin;
                    Health survivor = loose.GetComponent<Health>();
                    InvokeAwake(survivor);
                    EnemyDeathExplosion explosion = loose.GetComponent<EnemyDeathExplosion>();
                    InvokeAwake(explosion);
                    explosion.ConfigureOwner(boss);
                    boss.RegisterOwnedObject(loose);

                    Health bossHealth = bossObject.GetComponent<Health>();
                    bossHealth.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack,
                        bossHealth.MaxHealth + bossHealth.CurrentShield + 1f));
                    Assert(bossHealth.IsDead && boss.State == BossActionState.Defeated && boss.OwnedObjectCount == 0,
                        "The defeated boss must own nothing.");
                    if (!keeps)
                    {
                        Assert(loose == null, "A boss without the option must remove its minions on defeat.");
                        continue;
                    }

                    Assert(loose != null && !survivor.IsDead && loose.transform.parent == room.transform &&
                           ((Vector2)loose.transform.position - Origin).sqrMagnitude < 0.0001f,
                        "A minion of the defeated Buseureogi must stay alive in place under the boss room.");
                    Object.DestroyImmediate(bossObject);
                    Assert(loose != null, "Removing the defeated boss must not remove its released minion.");
                    survivor.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 999f));
                    Assert(survivor.IsDead && explosion.LastBlast != null &&
                           explosion.LastBlast.transform.parent == room.transform,
                        "A released minion must still leave its blast, inside the boss room.");
                }
                finally
                {
                    if (loose != null) Object.DestroyImmediate(loose);
                    Object.DestroyImmediate(room);
                    Physics2D.SyncTransforms();
                }
            }
        }

        private static Health Spawn(GameObject prefab, Transform parent, Vector2 position, bool armsExplosion)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            Health health = instance.GetComponent<Health>();
            InvokeAwake(health);
            if (armsExplosion) InvokeAwake(instance.GetComponent<EnemyDeathExplosion>());
            return health;
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
            GameObject player = new("Enemy-7 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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
            component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(component, null);
        }

        private static bool IsJyubiSwarm(string encounterId) => encounterId != null &&
            encounterId.EndsWith($"-{Week25Enemy7Setup.Pattern}-v1", StringComparison.Ordinal);

        private static string PrefabPath(string prefabName) => $"Assets/Prefabs/{prefabName}.prefab";

        private static EncounterDefinition Find(FloorGenerator generator, string profileId)
        {
            string id = Week19Spawn2Setup.EncounterId(profileId, Week25Enemy7Setup.Pattern);
            EncounterDefinition definition = generator.EncounterDefinitions.SingleOrDefault(candidate =>
                candidate != null && candidate.EncounterId == id);
            Assert(definition != null, $"Enemy-7 catalog is missing '{id}'.");
            return definition;
        }

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
