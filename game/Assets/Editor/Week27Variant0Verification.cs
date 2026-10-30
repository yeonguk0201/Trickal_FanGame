using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Variant0Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-9600f, 9600f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Variant-0 Elite Enemies")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable =
            {
                Week14Encounter2Setup.RosterPath, Week19Difficulty1Setup.TablePath, Week13FrontendSetup.GameScenePath,
            };
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week27Variant0Setup.Setup();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week27Variant0Setup.EliteDropTablePath);
            Week27Variant0Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before.Append(
                         new KeyValuePair<string, string>(Week27Variant0Setup.EliteDropTablePath, tableGuid)))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Variant-0 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-0 Elite Enemies")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Variant0Setup.EncounterContentVersion,
                "Run Variant-0 setup before verification.");

            ValidateRules();
            ValidateDropTable(assembler);
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, runtimeSeed);
            string bodies = ValidateEliteBodies(assembler);
            ValidateShots(assembler.EnemyRoster);
            ValidateRoomReward(assembler);
            Debug.Log("Variant-0 verification passed: one elite per combat room at 0/0/20% on floors 1/2/3, never a " +
                      $"쥬비 or boss, reproducible per seed ({counts}); runtime seed {runtimeSeed} binds it. Every " +
                      $"normal Encounter enemy becomes elite with x2 health, x2 knockback weight, x1.5 body with the " +
                      $"same feet, one damage tier up and x1.2 movement ({bodies}); its straight and lobbed shots are " +
                      "x1.5; bosses refuse it; its kill drops one pickup once per room even after an escaped fight.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads Encounter selection, room scores, the runtime waves, enemy damage, movement,
            // projectiles, the Encounter content version or the dev panel.
            (string Name, Action Run)[] checks =
            {
                ("Week27Enemy13", Week27Enemy13Verification.Verify),
                ("Week27Enemy12", Week27Enemy12Verification.Verify),
                ("Week27Enemy11", Week27Enemy11Verification.Verify),
                ("Week27Enemy10", Week27Enemy10Verification.Verify),
                ("Week27Curve1", Week27Curve1Verification.Verify),
                ("Week27Curve0", Week27Curve0Verification.Verify),
                ("Week27Hitbox2", Week27Hitbox2Verification.Verify),
                ("Week25Enemy9", Week25Enemy9Verification.Verify),
                ("Week25Enemy8", Week25Enemy8Verification.Verify),
                ("Week25Enemy7", Week25Enemy7Verification.Verify),
                ("Week19Spawn1", Week19Spawn1Verification.Verify),
                ("Week19Spawn2", Week19Spawn2Verification.Verify),
                ("Week19Difficulty1", Week19Difficulty1Verification.Verify),
                ("Week19Encounter4", Week19Encounter4Verification.Verify),
                ("Week20Floor1", Week20Floor1Verification.Verify),
                ("Week20DevPanel", Week20DevPanelVerification.Verify),
            };
            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                    run();
                    Debug.Log($"[Variant-0 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Variant-0 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Variant-0 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Variant-0 regression verification passed.");
        }

        private static void ValidateRules()
        {
            Assert(EnemyVariantRules.ElitePercent(1) == 0 && EnemyVariantRules.ElitePercent(2) == 0 &&
                   EnemyVariantRules.ElitePercent(3) == 20 && EnemyVariantRules.ElitePercent(4) == 30 &&
                   EnemyVariantRules.ElitePercent(6) == 30 && EnemyVariantRules.ElitePercent(7) == 40,
                "Elite chances must be 0/0/20% on floors 1~3, 30% on 4~6 and 40% after (docs/28 §9).");
            Assert(!EnemyVariantRules.IsEligible(EncounterEnemyRole.Jyubi) &&
                   !EnemyVariantRules.IsEligible(EncounterEnemyRole.Boss) &&
                   Enum.GetValues(typeof(EncounterEnemyRole)).Cast<EncounterEnemyRole>()
                       .Where(role => role is not (EncounterEnemyRole.Jyubi or EncounterEnemyRole.Boss))
                       .All(EnemyVariantRules.IsEligible),
                "Only 쥬비 and bosses may be excluded from elites among generated roles.");
            Assert(EnemyVariant.EliteVisualScale == 1.5f && EnemyVariant.EliteHealthMultiplier == 2f &&
                   EnemyVariant.EliteKnockbackWeightMultiplier == 2f && EnemyVariant.EliteMoveSpeedMultiplier == 1.2f &&
                   EnemyVariant.EliteProjectileScale == 1.5f && EnemyVariant.EliteDamageTierSteps == 1 &&
                   EnemyVariant.EliteThreatMultiplier == 2,
                "Elite values must follow docs/28 §8 (x1.5 body and shots, x2 health and knockback weight, " +
                "+1 damage tier, x1.2 movement, x2 threat).");
            Assert(EnemyVariant.ResolveDamageTier(null, EnemyDamageTier.Light) == EnemyDamageTier.Light,
                "A missing source must keep its tier.");
            Assert((int)EnemyVariantKind.None == 0 && (int)EnemyVariantKind.Elite == 1,
                "Variant kind values are append-only.");
        }

        private static void ValidateDropTable(RoomGraphAssembler assembler)
        {
            ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(
                Week27Variant0Setup.EliteDropTablePath);
            Assert(table != null && assembler.EliteKillDropTable == table,
                "The Game Scene must bind the elite kill drop table.");
            Assert(table.TryValidate(out string error), error);
            Assert(table.Entries.Count == Week27Variant0Setup.EliteDropWeights.Length &&
                   Week27Variant0Setup.EliteDropWeights.All(expected => table.Entries.Any(entry =>
                       entry.DropId == expected.DropId && entry.Weight == expected.Weight && entry.Prefab != null)) &&
                   table.Entries.All(entry => entry.DropId != "pit"),
                "The elite drop table must hold heart/sp/key/bomb/elif with their prefabs and no secret pit.");
            for (int seed = 1; seed <= 64; seed++)
                Assert(table.TryPick(seed, null, out ResourceDropEntry entry) && entry.Prefab != null,
                    $"The elite drop table must always pick a pickup (seed {seed}).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            RoomDifficultyTable table = generator.DifficultyTable;
            int[] rooms = new int[4], elites = new int[4];
            Dictionary<EncounterEnemyRole, int> roles = new();
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat),
                        $"Seed {seed} changed its Encounter, spawns or elite on regeneration.");
                }

                foreach (GeneratedRoomNode room in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
                {
                    int floor = room.FloorNumber;
                    rooms[floor]++;
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves.SelectMany(wave => wave).ToArray();
                    ResolvedEncounterSpawn[] elite = spawns.Where(spawn => spawn.Variant == EnemyVariantKind.Elite)
                        .ToArray();
                    // Variant-1 adds champions on the other enemies; Week27Variant1Verification checks them.
                    Assert(spawns.All(spawn => spawn.Variant is EnemyVariantKind.None or EnemyVariantKind.Elite ||
                                               EnemyVariant.IsChampionKind(spawn.Variant)),
                        $"Seed {seed} room '{room.RoomId}' holds an unknown variant.");
                    Assert(elite.Length <= 1, $"Seed {seed} room '{room.RoomId}' holds {elite.Length} elites.");
                    bool rolled = EnemyVariantRules.RollsElite(floor, room.ContentSeed,
                        generator.EncounterContentVersion);
                    bool eligible = spawns.Any(spawn => EnemyVariantRules.IsEligible(spawn.Role));
                    Assert(elite.Length == (rolled && eligible ? 1 : 0),
                        $"Seed {seed} room '{room.RoomId}' must hold an elite exactly when it rolls one and has an " +
                        "eligible enemy.");

                    // The stored score stays the Encounter's own; the elite adds its threat once more on top.
                    Assert(table.TryScore(room.ResolvedEncounterWaves, room.Template.LayoutDifficultyModifier,
                               out int score, out error) && room.HasDifficulty && score == room.Difficulty.Score,
                        $"Seed {seed} room '{room.RoomId}' score must stay the Encounter score. {error}");
                    Assert(table.TryScoreVariants(room.ResolvedEncounterWaves, out int extra, out error) &&
                           extra == elite.Sum(spawn => table.TryGetThreat(spawn.Role, out int threat) ? threat : -999) +
                           EnemyVariantRules.CountChampions(room.ResolvedEncounterWaves) *
                           EnemyVariant.ChampionThreatBonus,
                        $"Seed {seed} room '{room.RoomId}' elite threat must count once more. {error}");
                    if (elite.Length == 0) continue;
                    Assert(EnemyVariantRules.IsEligible(elite[0].Role),
                        $"Seed {seed} made an excluded {elite[0].Role} elite.");
                    elites[floor]++;
                    roles[elite[0].Role] = roles.TryGetValue(elite[0].Role, out int count) ? count + 1 : 1;
                    if (runtimeSeed == 0 && floor == 3) runtimeSeed = seed;
                }
            }

            Assert(elites[1] == 0 && elites[2] == 0, $"Floors 1~2 must have no elites ({elites[1]}/{elites[2]}).");
            float rate = rooms[3] == 0 ? 0f : elites[3] / (float)rooms[3];
            Assert(rate > 0.14f && rate < 0.24f,
                $"About 20% of floor-3 combat rooms must hold an elite ({elites[3]}/{rooms[3]}).");
            Assert(runtimeSeed > 0, "No floor-3 elite room was generated for runtime verification.");
            return $"floor 3 {elites[3]}/{rooms[3]} rooms; " +
                   string.Join(", ", roles.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"));
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 3;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, GeneratedRoomNode> nodes = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!nodes.TryGetValue(room.Node.RoomId, out GeneratedRoomNode node)) continue;
                IReadOnlyList<EncounterRuntimeWave> waves = room.Controller.EncounterWaves;
                Assert(waves.Count == node.ResolvedEncounterWaves.Count,
                    $"Runtime room '{node.RoomId}' lost a wave.");
                for (int wave = 0; wave < waves.Count; wave++)
                for (int index = 0; index < node.ResolvedEncounterWaves[wave].Length; index++)
                    Assert(waves[wave].VariantAt(index) == node.ResolvedEncounterWaves[wave][index].Variant,
                        $"Runtime room '{node.RoomId}' wave {wave + 1} slot {index + 1} lost its variant.");
                bool hasElite = EnemyVariantRules.CountElites(node.ResolvedEncounterWaves) > 0;
                Assert((room.Controller.EliteKillReward != null) == hasElite &&
                       (!hasElite || room.Controller.EliteKillReward.Table == assembler.EliteKillDropTable),
                    $"Runtime room '{node.RoomId}' must bind the elite reward exactly when it has an elite.");
                bound += hasElite ? 1 : 0;
            }

            Assert(bound > 0, $"Runtime seed {seed} did not bind its elite room.");
        }

        // Every normal Encounter enemy, made elite after the floor scaling, as RoomController does.
        private static string ValidateEliteBodies(RoomGraphAssembler assembler)
        {
            EncounterEnemyRoster roster = assembler.EnemyRoster;
            GameObject root = new("Variant-0 Body Verification");
            List<string> checkedNames = new();
            try
            {
                int slot = 0;
                foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole))
                             .Cast<EncounterEnemyRole>().Where(EnemyVariantRules.IsEligible))
                {
                    Assert(roster.TryResolve(role, out GameObject prefab, out string error), error);
                    GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    enemy.transform.position = Origin + Vector2.right * (slot++ * 6f);
                    Health health = enemy.GetComponent<Health>();
                    InvokeAwake(health);
                    foreach (EnemyAttackPresentation presentation in enemy.GetComponents<EnemyAttackPresentation>())
                        InvokeAwake(presentation);
                    FloorDifficultyScaler.ApplyScaling(enemy, 3);
                    float baseHealth = health.MaxHealth;
                    Vector3 baseScale = enemy.transform.localScale;
                    KnockbackReceiver knockback = enemy.GetComponent<KnockbackReceiver>();
                    float baseWeight = knockback != null ? knockback.KnockbackWeight : 0f;
                    EnemyFeet feet = enemy.GetComponentInChildren<EnemyFeet>(true);
                    Assert(EnemyFeet.TryGetBody(enemy, out CircleCollider2D body),
                        $"{prefab.name} needs a body circle.");
                    float baseBodyRadius = EnemyFeet.WorldRadiusOf(body);
                    Assert(EnemyVariant.CanApply(enemy), $"{prefab.name} ({role}) must accept an elite.");
                    Assert(EnemyStatusEffects.MoveSpeedMultiplierOf(health) == 1f,
                        $"{prefab.name} must move at its own speed before becoming elite.");
                    int lightUnits = (int)HealthUnits.CreateEnemyDamageContext(enemy, DamageSourceType.EnemyContact,
                        EnemyDamageTier.Light).BaseDamage;

                    Assert(EnemyVariant.Apply(enemy, EnemyVariantKind.Elite) && EnemyVariant.IsEliteEnemy(enemy),
                        $"{prefab.name} must become elite.");
                    Assert(!EnemyVariant.Apply(enemy, EnemyVariantKind.Elite) &&
                           Mathf.Approximately(health.MaxHealth, baseHealth * 2f),
                        $"{prefab.name} must not stack a second elite.");
                    Assert(Mathf.Approximately(health.MaxHealth, baseHealth * 2f) &&
                           Mathf.Approximately(health.CurrentHealth, health.MaxHealth),
                        $"{prefab.name} elite must have full x2 health ({health.CurrentHealth}/{health.MaxHealth}).");
                    Assert((enemy.transform.localScale - baseScale * 1.5f).sqrMagnitude < 0.0001f &&
                           Mathf.Abs(EnemyFeet.WorldRadiusOf(body) - baseBodyRadius * 1.5f) < 0.001f,
                        $"{prefab.name} elite body must be x1.5.");
                    Assert(knockback == null || Mathf.Approximately(knockback.KnockbackWeight, baseWeight * 2f),
                        $"{prefab.name} elite must have x2 knockback weight.");
                    if (feet != null)
                    {
                        Assert(Mathf.Abs(feet.WorldRadius - EnemyFeet.Radius) < 0.001f,
                            $"{prefab.name} elite feet must keep their world size ({feet.WorldRadius}).");
                        Vector2 bodyCenter = body.transform.TransformPoint(body.offset);
                        float bodyBottom = bodyCenter.y - EnemyFeet.WorldRadiusOf(body);
                        Assert(Mathf.Abs(feet.WorldCenter.y - feet.WorldRadius - bodyBottom) < 0.001f,
                            $"{prefab.name} elite feet must stand on the bottom of the grown body.");
                    }

                    foreach (EnemyAttackPresentation presentation in enemy.GetComponents<EnemyAttackPresentation>())
                    {
                        presentation.SetPhase(EnemyAttackPhase.Telegraph);
                        presentation.SetPhase(EnemyAttackPhase.Idle);
                        Assert((enemy.transform.localScale - baseScale * 1.5f).sqrMagnitude < 0.0001f,
                            $"{prefab.name} elite must keep its size through its attack phases.");
                    }

                    Assert(Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(health), 1.2f),
                        $"{prefab.name} elite must move x1.2.");
                    int eliteUnits = (int)HealthUnits.CreateEnemyDamageContext(enemy, DamageSourceType.EnemyContact,
                        EnemyDamageTier.Light).BaseDamage;
                    Assert(lightUnits == HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 3) &&
                           eliteUnits == HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Medium, 3) &&
                           EnemyVariant.ResolveDamageTier(enemy, EnemyDamageTier.Critical) == EnemyDamageTier.Critical,
                        $"{prefab.name} elite must hit one tier higher, capped at Critical.");
                    EnemyDeathExplosion blast = enemy.GetComponent<EnemyDeathExplosion>();
                    if (blast != null && blast.UsesDamageTier)
                        Assert(blast.ResolveDamageUnits() == HealthUnits.GetEnemyDamageUnits(
                                EnemyVariant.ResolveDamageTier(enemy, blast.DamageTier), 3) &&
                               EnemyVariant.ResolveDamageTier(enemy, blast.DamageTier) > blast.DamageTier,
                            $"{prefab.name} elite blast must use the raised tier.");
                    checkedNames.Add(prefab.name);
                }

                foreach (GameObject boss in assembler.FloorBossPrefabs.Where(boss => boss != null))
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(boss, root.transform);
                    Assert(!EnemyVariant.CanApply(instance) && !EnemyVariant.Apply(instance, EnemyVariantKind.Elite),
                        $"Boss {boss.name} must refuse an elite.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }

            return $"{checkedNames.Count} enemies";
        }

        private static void ValidateShots(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-0 Shot Verification");
            root.transform.position = Origin + Vector2.down * 60f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.Ranged, out GameObject prefab, out string error), error);
                GameObject plain = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                GameObject elite = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Assert(EnemyVariant.Apply(elite, EnemyVariantKind.Elite), "The ranged fixture must become elite.");

                EnemyProjectile plainShot = EnemyProjectile.Create(root.transform.position, Vector2.right, plain,
                    EnemyDamageTier.Light, 4f, 1f, null);
                EnemyProjectile eliteShot = EnemyProjectile.Create(root.transform.position, Vector2.right, elite,
                    EnemyDamageTier.Light, 4f, 1f, null);
                Assert(Mathf.Abs(eliteShot.transform.localScale.x - plainShot.transform.localScale.x * 1.5f) < 0.0001f &&
                       eliteShot.DamageContext.BaseDamage > plainShot.DamageContext.BaseDamage,
                    "An elite's straight shot must be x1.5 and hit one tier higher.");

                EnemyLobProjectile plainLob = EnemyLobProjectile.Launch(plain, root.transform.position,
                    (Vector2)root.transform.position + Vector2.right * 4f, 0f, 1f, 1f, 0.75f, EnemyDamageTier.Medium);
                EnemyLobProjectile eliteLob = EnemyLobProjectile.Launch(elite, root.transform.position,
                    (Vector2)root.transform.position + Vector2.right * 4f, 0f, 1f, 1f, 0.75f, EnemyDamageTier.Medium);
                Assert(Mathf.Approximately(eliteLob.Radius, plainLob.Radius * 1.5f) &&
                       Mathf.Abs(eliteLob.Lump.transform.localScale.x - plainLob.Lump.transform.localScale.x * 1.5f) <
                       0.0001f,
                    "An elite's lobbed shot and landing circle must be x1.5.");
                foreach (EnemyLobProjectile lob in new[] { plainLob, eliteLob })
                    if (lob.Mark != null) Object.DestroyImmediate(lob.Mark.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // An elite in a running room drops one pickup where it falls; an escaped fight brings it back without another.
        private static void ValidateRoomReward(RoomGraphAssembler assembler)
        {
            GameObject root = new("Variant-0 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 240f;
            try
            {
                Assert(assembler.EnemyRoster.TryResolve(EncounterEnemyRole.Chaser, out GameObject prefab,
                    out string error), error);
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Variant-0 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(3, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points,
                        new[] { EnemyVariantKind.None, EnemyVariantKind.Elite }),
                });
                RoomRunState state = new("variant-0-room");
                room.BindRunState(state, false);
                room.ConfigureEliteReward(new EliteKillReward(assembler.EliteKillDropTable, root.transform, state,
                    EliteKillReward.DeriveSeed(1234)));

                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                Health[] enemies = room.AliveEnemies.ToArray();
                Health elite = enemies.Single(enemy => EnemyVariant.IsEliteEnemy(enemy.gameObject));
                Health plain = enemies.Single(enemy => !EnemyVariant.IsEliteEnemy(enemy.gameObject));
                Assert(enemies.Length == 2 && Mathf.Approximately(elite.MaxHealth, plain.MaxHealth * 2f),
                    "The room must spawn its second slot as the elite.");

                Assert(room.TryAbandonCombat() && !state.HasGrantedEliteReward,
                    "Escaping the fight must not grant the elite reward.");
                room.BeginCombat(player);
                enemies = room.AliveEnemies.ToArray();
                elite = enemies.Single(enemy => EnemyVariant.IsEliteEnemy(enemy.gameObject));
                Vector3 fallen = elite.transform.position;
                elite.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, elite.MaxHealth + 1f));
                GameObject reward = room.EliteKillReward.LastSpawnedReward;
                Assert(elite.IsDead && state.HasGrantedEliteReward && reward != null &&
                       ((Vector2)(reward.transform.position - fallen)).sqrMagnitude < 0.0001f,
                    "Killing the elite must drop one pickup where it fell.");
                Assert(!room.EliteKillReward.TrySpawn(fallen),
                    "The room's elite reward must drop only once per Run.");
                foreach (Health enemy in room.AliveEnemies.ToArray())
                    enemy.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, enemy.MaxHealth + 1f));
                Assert(room.State == RoomState.Cleared, "The room must clear once the elite and the rest are dead.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Variant-0 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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

        private static string Signature(GeneratedFloorGraph graph) =>
            string.Join("|", graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate)
                .Select(node => $"{node.RoomId}:{node.EncounterId}:" + string.Join("/",
                    node.ResolvedEncounterWaves.Select(wave =>
                        string.Join(",", wave.Select(spawn => $"{spawn.Role}@{spawn.SpawnPointIndex}:{spawn.Variant}"))))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
