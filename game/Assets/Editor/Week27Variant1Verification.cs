using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Variant1Verification
    {
        // Far from every authored scene (and from Variant-0's fixtures) so the objects never meet scene content.
        private static readonly Vector2 Origin = new(-9600f, -9600f);
        private const int GeneratedSeedCount = 512;
        private const int DeterminismSeedCount = 64;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Variant-1 Champion Enemies")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable =
            {
                Week14Encounter2Setup.RosterPath, Week19Difficulty1Setup.TablePath, Week13FrontendSetup.GameScenePath,
                Week27Variant0Setup.EliteDropTablePath, Week27Variant1Setup.OutlineShaderPath,
            };
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week27Variant1Setup.Setup();
            Week27Variant1Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Variant-1 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-1 Champion Enemies")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.DifficultyTable != null &&
                   generator.EncounterContentVersion >= Week27Variant1Setup.EncounterContentVersion,
                "Run Variant-1 setup before verification.");

            ValidateRules();
            string counts = ValidateGeneration(generator, out int runtimeSeed);
            ValidateRuntimeBinding(assembler, runtimeSeed);
            string bodies = ValidateChampionBodies(assembler);
            ValidateOutline(assembler.EnemyRoster);
            ValidateDeathEffects(assembler.EnemyRoster);
            ValidateRoom(assembler.EnemyRoster);
            Debug.Log("Variant-1 verification passed: champions roll per eligible enemy at 0/10/15% on floors " +
                      $"1/2/3, never on a 쥬비, boss or the room's elite, reproducible per seed ({counts}); runtime " +
                      $"seed {runtimeSeed} binds them. Every normal Encounter enemy takes each death property it " +
                      $"may ({bodies}) with its outline colour and unchanged stats; 자폭형 refuses the blast. The " +
                      "outline follows the sprite or the movement mesh with 8 line and 8 glow copies behind the body " +
                      "and hides on death. The puddle arms, hurts feet inside once per second at the light tier and " +
                      "ends; the volley fires 4 ways (8 for 저혈당 요정-활발) after its flash; the blast is the " +
                      "쫄따구 blast; room transitions clear puddles and volleys.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads Encounter selection, room scores, the runtime waves, enemy death, the Encounter
            // content version or the dev panel.
            (string Name, Action Run)[] checks =
            {
                ("Week27Variant0", Week27Variant0Verification.Verify),
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
                    Debug.Log($"[Variant-1 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Variant-1 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Variant-1 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Variant-1 regression verification passed.");
        }

        private static void ValidateRules()
        {
            Assert(EnemyVariantRules.ChampionPercent(1) == 0 && EnemyVariantRules.ChampionPercent(2) == 10 &&
                   EnemyVariantRules.ChampionPercent(3) == 15 && EnemyVariantRules.ChampionPercent(4) == 20 &&
                   EnemyVariantRules.ChampionPercent(6) == 20 && EnemyVariantRules.ChampionPercent(7) == 25,
                "Champion chances must be 0/10/15% on floors 1~3, 20% on 4~6 and 25% after (docs/28 §9).");
            Assert((int)EnemyVariantKind.None == 0 && (int)EnemyVariantKind.Elite == 1 &&
                   (int)EnemyVariantKind.ChampionPuddle == 2 && (int)EnemyVariantKind.ChampionShots == 3 &&
                   (int)EnemyVariantKind.ChampionBlast == 4,
                "Variant kind values are append-only.");
            Assert(EnemyVariant.ChampionThreatBonus == 1, "A champion adds 1 threat (docs/28 §8).");
            Assert(EnemyVariant.ChampionKinds.Length == 3 && EnemyVariant.ChampionKinds.All(EnemyVariant.IsChampionKind) &&
                   !EnemyVariant.IsChampionKind(EnemyVariantKind.None) &&
                   !EnemyVariant.IsChampionKind(EnemyVariantKind.Elite),
                "Variant-1 has exactly the puddle, shots and blast champions.");
            foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole)).Cast<EncounterEnemyRole>())
            {
                EnemyVariantKind[] kinds = EnemyVariantRules.ChampionKindsFor(role);
                Assert(role == EncounterEnemyRole.SelfDestruct
                        ? kinds.Length == 2 && !kinds.Contains(EnemyVariantKind.ChampionBlast)
                        : kinds.Length == 3,
                    $"{role} must take {(role == EncounterEnemyRole.SelfDestruct ? "every property but the blast" : "every property")}.");
            }

            Color green = EnemyVariant.ChampionColor(EnemyVariantKind.ChampionPuddle);
            Color purple = EnemyVariant.ChampionColor(EnemyVariantKind.ChampionShots);
            Color yellow = EnemyVariant.ChampionColor(EnemyVariantKind.ChampionBlast);
            Assert(green.g > green.r && green.g > green.b && purple.b > purple.g && purple.r > purple.g &&
                   yellow.r > yellow.b && yellow.g > yellow.b,
                "Outline colours must be green (puddle), purple (shots) and yellow (blast).");
        }

        private static string ValidateGeneration(FloorGenerator generator, out int runtimeSeed)
        {
            runtimeSeed = 0;
            RoomDifficultyTable table = generator.DifficultyTable;
            int version = generator.EncounterContentVersion;
            int[] eligible = new int[4], champions = new int[4];
            Dictionary<EnemyVariantKind, int> kinds = EnemyVariant.ChampionKinds.ToDictionary(kind => kind, _ => 0);
            Dictionary<EncounterEnemyRole, int> roles = new();
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat),
                        $"Seed {seed} changed its Encounter, spawns or variants on regeneration.");
                }

                foreach (GeneratedRoomNode room in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
                {
                    int floor = room.FloorNumber;
                    for (int wave = 0; wave < room.ResolvedEncounterWaves.Count; wave++)
                    {
                        ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves[wave];
                        for (int index = 0; index < spawns.Length; index++)
                        {
                            ResolvedEncounterSpawn spawn = spawns[index];
                            bool champion = EnemyVariant.IsChampionKind(spawn.Variant);
                            if (!EnemyVariantRules.IsEligible(spawn.Role))
                            {
                                Assert(spawn.Variant == EnemyVariantKind.None,
                                    $"Seed {seed} room '{room.RoomId}' made an excluded {spawn.Role} a variant.");
                                continue;
                            }

                            if (spawn.Variant == EnemyVariantKind.Elite) continue;
                            eligible[floor]++;
                            bool rolled = EnemyVariantRules.RollsChampion(floor, room.ContentSeed, version, wave,
                                index);
                            Assert(champion == rolled,
                                $"Seed {seed} room '{room.RoomId}' wave {wave + 1} slot {index + 1} must be a " +
                                "champion exactly when it rolls one.");
                            if (!champion) continue;
                            Assert(spawn.Variant == EnemyVariantRules.PickChampionKind(spawn.Role, room.ContentSeed,
                                       version, wave, index) &&
                                   EnemyVariantRules.ChampionKindsFor(spawn.Role).Contains(spawn.Variant),
                                $"Seed {seed} room '{room.RoomId}' gave {spawn.Role} the wrong property.");
                            champions[floor]++;
                            kinds[spawn.Variant]++;
                            roles[spawn.Role] = roles.TryGetValue(spawn.Role, out int count) ? count + 1 : 1;
                            if (runtimeSeed == 0 && floor == 2) runtimeSeed = seed;
                        }
                    }

                    // The elite stays exactly Variant-0's pick for the same seed and version.
                    Assert(EnemyVariantRules.CountElites(room.ResolvedEncounterWaves) ==
                           (EnemyVariantRules.RollsElite(floor, room.ContentSeed, version) &&
                            room.ResolvedEncounterWaves.SelectMany(wave => wave)
                                .Any(spawn => EnemyVariantRules.IsEligible(spawn.Role)) ? 1 : 0),
                        $"Seed {seed} room '{room.RoomId}' lost or gained an elite.");
                    Assert(table.TryScore(room.ResolvedEncounterWaves, room.Template.LayoutDifficultyModifier,
                               out int score, out error) && room.HasDifficulty && score == room.Difficulty.Score,
                        $"Seed {seed} room '{room.RoomId}' score must stay the Encounter score. {error}");
                    Assert(table.TryScoreVariants(room.ResolvedEncounterWaves, out int extra, out error) &&
                           extra == room.ResolvedEncounterWaves.SelectMany(wave => wave)
                               .Where(spawn => spawn.Variant == EnemyVariantKind.Elite)
                               .Sum(spawn => table.TryGetThreat(spawn.Role, out int threat) ? threat : -999) +
                           EnemyVariantRules.CountChampions(room.ResolvedEncounterWaves),
                        $"Seed {seed} room '{room.RoomId}' variant threat must add 1 per champion. {error}");
                }
            }

            Assert(champions[1] == 0, $"Floor 1 must have no champions ({champions[1]}).");
            float rate2 = eligible[2] == 0 ? 0f : champions[2] / (float)eligible[2];
            float rate3 = eligible[3] == 0 ? 0f : champions[3] / (float)eligible[3];
            Assert(rate2 > 0.08f && rate2 < 0.12f,
                $"About 10% of floor-2 eligible enemies must be champions ({champions[2]}/{eligible[2]}).");
            Assert(rate3 > 0.13f && rate3 < 0.17f,
                $"About 15% of floor-3 eligible enemies must be champions ({champions[3]}/{eligible[3]}).");
            int total = kinds.Values.Sum();
            Assert(kinds.Values.All(count => count > total * 0.25f && count < total * 0.42f),
                "Champion properties must be spread about evenly (" +
                string.Join(", ", kinds.Select(entry => $"{entry.Key} {entry.Value}")) + ").");
            Assert(runtimeSeed > 0, "No floor-2 champion was generated for runtime verification.");
            return $"floor 2 {champions[2]}/{eligible[2]}, floor 3 {champions[3]}/{eligible[3]}; " +
                   string.Join(", ", kinds.Select(entry => $"{entry.Key} {entry.Value}")) + "; " +
                   string.Join(", ", roles.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"));
        }

        private static void ValidateRuntimeBinding(RoomGraphAssembler assembler, int seed)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            const int floor = 2;
            Assert(assembler.TryLoadFloor(floor, assembler.Graph.Player, out error), error);
            Dictionary<string, GeneratedRoomNode> nodes = assembler.GeneratedGraph.Nodes
                .Where(node => node.Role == GeneratedRoomRole.Intermediate && node.FloorNumber == floor)
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            int bound = 0;
            foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
            {
                if (!nodes.TryGetValue(room.Node.RoomId, out GeneratedRoomNode node)) continue;
                IReadOnlyList<EncounterRuntimeWave> waves = room.Controller.EncounterWaves;
                for (int wave = 0; wave < waves.Count; wave++)
                for (int index = 0; index < node.ResolvedEncounterWaves[wave].Length; index++)
                {
                    EnemyVariantKind expected = node.ResolvedEncounterWaves[wave][index].Variant;
                    Assert(waves[wave].VariantAt(index) == expected,
                        $"Runtime room '{node.RoomId}' wave {wave + 1} slot {index + 1} lost its variant.");
                    if (EnemyVariant.IsChampionKind(expected)) bound++;
                }
            }

            Assert(bound > 0, $"Runtime seed {seed} did not bind its champion.");
        }

        // Every normal Encounter enemy takes each property it may, after the floor scaling, as RoomController does.
        private static string ValidateChampionBodies(RoomGraphAssembler assembler)
        {
            EncounterEnemyRoster roster = assembler.EnemyRoster;
            GameObject root = new("Variant-1 Body Verification");
            int made = 0;
            try
            {
                int slot = 0;
                foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole))
                             .Cast<EncounterEnemyRole>().Where(EnemyVariantRules.IsEligible))
                {
                    Assert(roster.TryResolve(role, out GameObject prefab, out string error), error);
                    foreach (EnemyVariantKind kind in EnemyVariant.ChampionKinds)
                    {
                        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                        enemy.transform.position = Origin + Vector2.right * (slot++ * 6f);
                        Health health = enemy.GetComponent<Health>();
                        InvokeAwake(health);
                        FloorDifficultyScaler.ApplyScaling(enemy, 3);
                        float baseHealth = health.MaxHealth;
                        Vector3 baseScale = enemy.transform.localScale;
                        bool allowed = EnemyVariantRules.ChampionKindsFor(role).Contains(kind);
                        Assert(EnemyVariant.CanApply(enemy, kind) == allowed,
                            $"{prefab.name} ({role}) must {(allowed ? "accept" : "refuse")} {kind}.");
                        if (!allowed)
                        {
                            Assert(!EnemyVariant.Apply(enemy, kind) && EnemyVariant.KindOf(enemy) == EnemyVariantKind.None,
                                $"{prefab.name} must stay plain after refusing {kind}.");
                            continue;
                        }

                        Assert(EnemyVariant.Apply(enemy, kind) && EnemyVariant.IsChampionEnemy(enemy) &&
                               EnemyVariant.KindOf(enemy) == kind, $"{prefab.name} must become a {kind} champion.");
                        Assert(!EnemyVariant.Apply(enemy, EnemyVariantKind.Elite) &&
                               !EnemyVariant.Apply(enemy, EnemyVariantKind.ChampionPuddle),
                            $"{prefab.name} champion must not stack another variant.");
                        EnemyChampionDeath death = enemy.GetComponent<EnemyChampionDeath>();
                        EnemyChampionOutline outline = enemy.GetComponent<EnemyChampionOutline>();
                        Assert(death != null && death.Kind == kind && outline != null &&
                               outline.Color == EnemyVariant.ChampionColor(kind),
                            $"{prefab.name} {kind} champion needs its death property and outline colour.");
                        Assert(Mathf.Approximately(health.MaxHealth, baseHealth) &&
                               (enemy.transform.localScale - baseScale).sqrMagnitude < 0.0001f &&
                               Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(health), 1f) &&
                               EnemyVariant.ResolveDamageTier(enemy, EnemyDamageTier.Light) == EnemyDamageTier.Light &&
                               Mathf.Approximately(EnemyVariant.ProjectileScaleOf(enemy), 1f),
                            $"{prefab.name} champion must keep its health, size, speed, damage and shots.");
                        made++;
                    }
                }

                foreach (GameObject boss in assembler.FloorBossPrefabs.Where(boss => boss != null))
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(boss, root.transform);
                    Assert(EnemyVariant.ChampionKinds.All(kind => !EnemyVariant.Apply(instance, kind)),
                        $"Boss {boss.name} must refuse champions.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }

            return $"{made} champions";
        }

        private static void ValidateOutline(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-1 Outline Verification");
            root.transform.position = Origin + Vector2.down * 60f;
            try
            {
                // 저격 (고혈당 요정) draws its sprite; the chaser (TestEnemy) hops on a movement mesh.
                Assert(roster.TryResolve(EncounterEnemyRole.Sniper, out GameObject spritePrefab, out string error),
                    error);
                GameObject spriteEnemy = Champion(spritePrefab, root.transform, EnemyVariantKind.ChampionShots,
                    Vector2.zero);
                EnemyChampionOutline outline = spriteEnemy.GetComponent<EnemyChampionOutline>();
                outline.Refresh(null, 0f);
                SpriteRenderer body = outline.CurrentBody as SpriteRenderer;
                Assert(body != null && body.enabled && !body.forceRenderingOff && body.sprite != null &&
                       outline.VisibleCopyCount == EnemyChampionOutline.CopyCount &&
                       outline.SpriteCopies.All(copy => copy != null && copy.enabled && copy.sprite == body.sprite &&
                                                        copy.transform.parent == body.transform),
                    "The outline must draw 16 copies of the visible sprite.");
                Color purple = EnemyVariant.ChampionColor(EnemyVariantKind.ChampionShots);
                MaterialPropertyBlock block = new();
                for (int index = 0; index < outline.SpriteCopies.Count; index++)
                {
                    SpriteRenderer copy = outline.SpriteCopies[index];
                    bool glow = index >= EnemyChampionOutline.CopyCount / 2;
                    float pixels = glow ? EnemyChampionOutline.GlowPixels : EnemyChampionOutline.OutlinePixels;
                    float distance = ((Vector2)(copy.transform.position - body.transform.position)).magnitude;
                    copy.GetPropertyBlock(block);
                    Color tint = block.GetColor("_OutlineColor");
                    // Within 5%: a tilted or skewed body moves the flattened copy by a hair.
                    Assert(Mathf.Abs(distance - pixels * outline.LastWorldPerPixel) <
                           pixels * outline.LastWorldPerPixel * 0.05f &&
                           copy.sortingOrder == body.sortingOrder - (glow ? 2 : 1) &&
                           Mathf.Abs(tint.r - purple.r) < 0.01f && Mathf.Abs(tint.b - purple.b) < 0.01f &&
                           (glow ? tint.a < 0.5f : tint.a > 0.9f) && copy.sharedMaterial != null &&
                           copy.sharedMaterial.shader.name == "TrickalFanGame/Champion Outline",
                        $"Outline copy {index} must sit {pixels} screen pixels out behind the body in purple " +
                        $"(distance {distance:0.#####}/{pixels * outline.LastWorldPerPixel:0.#####}, order " +
                        $"{copy.sortingOrder}/{body.sortingOrder}, tint {tint}, shader " +
                        $"{(copy.sharedMaterial != null ? copy.sharedMaterial.shader.name : "none")}).");
                }

                Assert(Mathf.Approximately(EnemyChampionOutline.WorldPerPixel(null), outline.LastWorldPerPixel),
                    "Without a camera the outline must use its fallback pixel size.");

                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject meshPrefab, out error), error);
                GameObject meshEnemy = Champion(meshPrefab, root.transform, EnemyVariantKind.ChampionPuddle,
                    Vector2.right * 6f);
                EnemyMovementAnimator animator = meshEnemy.GetComponent<EnemyMovementAnimator>();
                Assert(animator != null && animator.Motion == EnemyMovementMotion.Hop,
                    "The chaser fixture must hop on a movement mesh.");
                InvokeAwake(animator);
                Invoke(animator, "LateUpdate");
                MeshRenderer mesh = meshEnemy.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(renderer => renderer.name == "Enemy Movement Artwork");
                Assert(mesh != null && mesh.enabled && meshEnemy.GetComponent<SpriteRenderer>().forceRenderingOff,
                    "The chaser fixture must draw its movement mesh in place of the sprite.");
                EnemyChampionOutline meshOutline = meshEnemy.GetComponent<EnemyChampionOutline>();
                meshOutline.Refresh(null, 0f);
                Mesh shared = mesh.GetComponent<MeshFilter>().sharedMesh;
                Assert(meshOutline.CurrentBody == mesh &&
                       meshOutline.VisibleCopyCount == EnemyChampionOutline.CopyCount &&
                       meshOutline.MeshCopies.All(copy => copy != null && copy.enabled &&
                                                          copy.GetComponent<MeshFilter>().sharedMesh == shared &&
                                                          copy.sortingOrder < mesh.sortingOrder) &&
                       (meshOutline.SpriteCopies == null || meshOutline.SpriteCopies.All(copy => !copy.enabled)),
                    "The outline must follow the movement mesh with copies that share its deformed mesh.");

                Health health = meshEnemy.GetComponent<Health>();
                health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, health.MaxHealth + 1f));
                meshOutline.Refresh(null, 0f);
                Assert(health.IsDead && meshOutline.CurrentBody == null && meshOutline.VisibleCopyCount == 0 &&
                       meshOutline.MeshCopies.All(copy => !copy.enabled),
                    "The outline must hide once the champion dies.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EnemyChampionPuddle.ClearAll();
                EnemyChampionVolley.ClearAll();
            }
        }

        private static void ValidateDeathEffects(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-1 Death Verification");
            root.transform.position = Origin + Vector2.down * 120f;
            try
            {
                const int floor = 2;
                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject chaser, out string error), error);

                // Puddle: arms without hurting, hurts feet inside once per second, ignores feet outside, ends.
                Vector2 puddleAt = (Vector2)root.transform.position;
                GameObject puddleEnemy = Champion(chaser, root.transform, EnemyVariantKind.ChampionPuddle, Vector2.zero,
                    floor);
                Health inside = CreatePlayer(root.transform, puddleAt);
                Health outside = CreatePlayer(root.transform,
                    puddleAt + new Vector2(EnemyChampionPuddle.HalfWidth * 0.9f, EnemyChampionPuddle.HalfHeight * 1.3f));
                Physics2D.SyncTransforms();
                Kill(puddleEnemy);
                EnemyChampionPuddle puddle = puddleEnemy.GetComponent<EnemyChampionDeath>().LastEffect
                    ?.GetComponent<EnemyChampionPuddle>();
                Assert(puddle != null && puddle.transform.parent == root.transform &&
                       ((Vector2)puddle.transform.position - puddleAt).sqrMagnitude < 0.0001f &&
                       EnemyChampionPuddle.Live.Contains(puddle),
                    "A puddle champion must leave its puddle where it died, in its room.");
                Assert(puddle.DamageUnits == HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, floor),
                    "The puddle must hurt at the light tier of the champion's floor.");
                Assert(puddle.Visual != null && puddle.Visual.sprite != null &&
                       puddle.Visual.sprite.name == EnemyChampionPuddle.ArtworkId &&
                       Mathf.Abs(puddle.Visual.sprite.bounds.size.x - EnemyChampionPuddle.ArtWidth) < 0.01f &&
                       puddle.Visual.sprite.bounds.extents.y > EnemyChampionPuddle.HalfHeight,
                    "The puddle must draw the 독 장판 art at its width, taller than its harmful ellipse.");
                Assert(Mathf.Approximately(puddle.EndTime - puddle.StartTime, 7f),
                    "The puddle must last 7 seconds.");
                float insideBefore = inside.CurrentHealth, outsideBefore = outside.CurrentHealth;
                puddle.Tick(puddle.StartTime + EnemyChampionPuddle.ArmDuration * 0.5f);
                Assert(puddle.HitCount == 0 && Mathf.Approximately(inside.CurrentHealth, insideBefore),
                    "The puddle must not hurt while it spreads.");
                puddle.Tick(puddle.ArmedAt);
                Assert(puddle.HitCount == 1 && inside.CurrentHealth < insideBefore &&
                       Mathf.Approximately(outside.CurrentHealth, outsideBefore),
                    "The armed puddle must hurt only the feet inside its ellipse.");
                puddle.Tick(puddle.ArmedAt + EnemyChampionPuddle.HitInterval * 0.5f);
                Assert(puddle.HitCount == 1, "The puddle must wait a second between hits.");
                Assert(!puddle.IsHarmful(puddle.FadeAt) && puddle.IsHarmful(puddle.FadeAt - 0.01f),
                    "The puddle must stop hurting when it starts to fade.");
                puddle.Tick(puddle.EndTime);
                Assert(puddle == null || puddle.HasEnded, "The puddle must end after its duration.");

                // Volley: flashes, then fires plain light-tier shots 4 ways (8 for 저혈당 요정-활발).
                foreach ((EncounterEnemyRole role, int directions) in new[]
                         {
                             (EncounterEnemyRole.Chaser, 4), (EncounterEnemyRole.FourWayShooter, 8),
                         })
                {
                    Assert(roster.TryResolve(role, out GameObject prefab, out error), error);
                    GameObject shooter = Champion(prefab, root.transform, EnemyVariantKind.ChampionShots,
                        Vector2.right * 20f, floor);
                    Vector2 diedAt = shooter.transform.position;
                    Kill(shooter);
                    EnemyChampionVolley volley = shooter.GetComponent<EnemyChampionDeath>().LastEffect
                        ?.GetComponent<EnemyChampionVolley>();
                    Assert(volley != null && volley.DirectionCount == directions &&
                           ((Vector2)volley.transform.position - diedAt).sqrMagnitude < 0.0001f,
                        $"A {role} shots champion must leave a {directions}-way volley where it died.");
                    volley.Tick(volley.FireAt - 0.01f);
                    Assert(!volley.HasFired && volley.Shots.Count == 0, "The volley must flash before it fires.");
                    volley.Tick(volley.FireAt);
                    Assert(volley.HasFired && volley.Shots.Count == directions &&
                           volley.Shots.All(shot => shot != null && shot.transform.parent == root.transform &&
                                                    Mathf.Abs(shot.Velocity.magnitude -
                                                              EnemyChampionVolley.ProjectileSpeed) < 0.001f &&
                                                    (int)shot.DamageContext.BaseDamage ==
                                                    HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, floor) &&
                                                    shot.GetComponent<SpriteRenderer>().color ==
                                                    EnemyChampionVolley.ShotColor),
                        $"The {role} volley must fire {directions} purple light-tier shots into the room.");
                    Assert(volley.Shots.Select(shot => shot.Velocity.normalized).Distinct().Count() == directions,
                        "Each volley shot must fly its own way.");
                    foreach (EnemyProjectile shot in volley.Shots) Object.DestroyImmediate(shot.gameObject);
                }

                // Blast: the 쫄따구 blast where it died.
                GameObject blaster = Champion(chaser, root.transform, EnemyVariantKind.ChampionBlast,
                    Vector2.right * 40f, floor);
                Vector2 blastAt = blaster.transform.position;
                Kill(blaster);
                EnemyDeathBlast blast = blaster.GetComponent<EnemyChampionDeath>().LastEffect
                    ?.GetComponent<EnemyDeathBlast>();
                Assert(blast != null && !blast.HasExploded &&
                       ((Vector2)blast.transform.position - blastAt).sqrMagnitude < 0.0001f &&
                       Mathf.Approximately(blast.Radius, EnemyDeathExplosion.DefaultRadius) &&
                       blast.DamageUnits == EnemyDeathExplosion.DefaultDamageUnits && !blast.HitsEnemiesAndObstacles,
                    "A blast champion must leave the 쫄따구 blast (radius 1.2, one heart, player only).");
                Assert(blaster.GetComponent<EnemyDeathExplosion>() == null,
                    "The blast champion must not add a second explosion component.");

                // Room transitions remove what is left.
                GameObject lingering = Champion(chaser, root.transform, EnemyVariantKind.ChampionPuddle,
                    Vector2.right * 60f, floor);
                Kill(lingering);
                GameObject lateShooter = Champion(chaser, root.transform, EnemyVariantKind.ChampionShots,
                    Vector2.right * 70f, floor);
                Kill(lateShooter);
                Assert(EnemyChampionPuddle.Live.Count > 0 && EnemyChampionVolley.Live.Any(volley => !volley.HasFired),
                    "The transition fixture must hold a puddle and an unfired volley.");
                EnemyChampionPuddle.ClearAll();
                EnemyChampionVolley.ClearAll();
                Assert(EnemyChampionPuddle.Live.Count == 0 && EnemyChampionVolley.Live.Count == 0,
                    "Room transitions must clear champion puddles and volleys.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EnemyChampionPuddle.ClearAll();
                EnemyChampionVolley.ClearAll();
                Physics2D.SyncTransforms();
            }
        }

        // A running room spawns its champion slot as a champion, and its death leaves the property without holding
        // the clear back.
        private static void ValidateRoom(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-1 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 240f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject prefab, out string error), error);
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Variant-1 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(2, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points,
                        new[] { EnemyVariantKind.None, EnemyVariantKind.ChampionPuddle }),
                });
                room.BindRunState(new RoomRunState("variant-1-room"), false);
                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                Health[] enemies = room.AliveEnemies.ToArray();
                Health champion = enemies.Single(enemy => EnemyVariant.IsChampionEnemy(enemy.gameObject));
                Health plain = enemies.Single(enemy => !EnemyVariant.IsChampionEnemy(enemy.gameObject));
                Assert(Mathf.Approximately(champion.MaxHealth, plain.MaxHealth) &&
                       champion.GetComponent<EnemyChampionOutline>() != null,
                    "The room must spawn its second slot as a puddle champion with the same health.");
                foreach (Health enemy in enemies)
                    enemy.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, enemy.MaxHealth + 1f));
                Assert(room.State == RoomState.Cleared && EnemyChampionPuddle.Live.Count == 1,
                    "The room must clear at once while the champion's puddle stays.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EnemyChampionPuddle.ClearAll();
                EnemyChampionVolley.ClearAll();
                Physics2D.SyncTransforms();
            }
        }

        private static GameObject Champion(GameObject prefab, Transform parent, EnemyVariantKind kind, Vector2 offset,
            int floor = 3)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.transform.position = (Vector2)parent.position + offset;
            InvokeAwake(enemy.GetComponent<Health>());
            FloorDifficultyScaler.ApplyScaling(enemy, floor);
            EnemyFloorLevel.Apply(enemy, floor);
            Assert(EnemyVariant.Apply(enemy, kind), $"{prefab.name} must become a {kind} champion.");
            return enemy;
        }

        private static void Kill(GameObject enemy)
        {
            Health health = enemy.GetComponent<Health>();
            health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, health.MaxHealth + 1f));
            Assert(health.IsDead, $"{enemy.name} must die.");
        }

        private static Health CreatePlayer(Transform parent, Vector2 feetAt)
        {
            GameObject player = new("Variant-1 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
            player.transform.SetParent(parent);
            player.layer = LayerMask.NameToLayer("Player");
            PlayerFeet.Ensure(player, out _);
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            player.transform.position = feetAt;
            player.transform.position = (Vector3)(feetAt - (movement.FeetPosition - feetAt));
            Health health = player.GetComponent<Health>();
            InvokeAwake(health);
            health.EnableHealthUnits();
            return health;
        }

        private static void InvokeAwake(Component component) => Invoke(component, "Awake");

        private static void Invoke(Component component, string method)
        {
            component?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
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
