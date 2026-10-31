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
    public static class Week27Variant2Verification
    {
        // Far from every authored scene and from the Variant-0/1 fixtures.
        private static readonly Vector2 Origin = new(-9600f, -12000f);
        private const int GeneratedSeedCount = 512;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Variant-2 Undying Champions")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable =
            {
                Week14Encounter2Setup.RosterPath, Week19Difficulty1Setup.TablePath, Week13FrontendSetup.GameScenePath,
                Week27Variant1Setup.OutlineShaderPath,
            };
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week27Variant2Setup.Setup();
            Week27Variant2Setup.Setup();
            foreach (KeyValuePair<string, string> entry in before)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Variant-2 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-2 Undying Champions")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.EnemyRoster != null &&
                   generator.EncounterContentVersion == Week27Variant2Setup.EncounterContentVersion,
                "Run Variant-2 setup before verification.");

            ValidateRules();
            string counts = ValidateGeneration(generator);
            EncounterEnemyRoster roster = assembler.EnemyRoster;
            string bodies = ValidateBodies(roster);
            ValidateUndyingLife(roster);
            ValidateExclusionsAndSplit(roster);
            ValidateKillCredit(roster);
            ValidateRoom(roster);
            Debug.Log("Variant-2 verification passed: the undying (white) champion is the fourth property for every " +
                      $"eligible role ({counts}; {bodies}). A lethal hit leaves it at 85% life that runs out over " +
                      "3 s, hits shorten it, a second lethal hit ends it, it moves and attacks 1.3x faster and is " +
                      "tinted grey and see-through. Death, kill credit, splitting and the room clear wait for the " +
                      "final fall; its own self-destruct is not deferred.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Variant-2 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // Every check that reads Health deaths, champions, Encounter selection, the content version, enemy
            // attack timing or the dev panel.
            (string Name, Action Run)[] checks =
            {
                ("Week27Variant1", Week27Variant1Verification.Verify),
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
                    Debug.Log($"[Variant-2 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Variant-2 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Variant-2 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log("Variant-2 regression verification passed.");
        }

        private static void ValidateRules()
        {
            Assert((int)EnemyVariantKind.ChampionBlast == 4 && (int)EnemyVariantKind.ChampionUndying == 5,
                "Variant kind values are append-only; undying is 5.");
            Assert(EnemyVariant.ChampionKinds.Length == 4 &&
                   EnemyVariant.ChampionKinds.Last() == EnemyVariantKind.ChampionUndying &&
                   EnemyVariant.IsChampionKind(EnemyVariantKind.ChampionUndying),
                "Champions must be puddle, shots, blast and undying.");
            foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole)).Cast<EncounterEnemyRole>())
                Assert(EnemyVariantRules.ChampionKindsFor(role).Contains(EnemyVariantKind.ChampionUndying),
                    $"{role} must be able to take the undying property (no exclusion, docs/28 §8).");
            Assert(EnemyVariant.ChampionColor(EnemyVariantKind.ChampionUndying) == Color.white,
                "The undying outline must be white.");
            Assert(Mathf.Approximately(EnemyChampionUndying.LifeShare, 0.85f) &&
                   Mathf.Approximately(EnemyChampionUndying.Duration, 3f) &&
                   Mathf.Approximately(EnemyChampionUndying.SpeedMultiplier, 1.3f),
                "Undying life must be 85% of max health over 3 s with 30% faster movement and attacks.");
            Color tint = EnemyChampionUndying.BodyTint;
            Assert(tint.a < 0.8f && Mathf.Abs(tint.r - tint.g) < 0.05f && tint.r < 0.9f,
                "The undying body must be tinted grey and see-through.");
        }

        private static string ValidateGeneration(FloorGenerator generator)
        {
            int version = generator.EncounterContentVersion;
            Dictionary<EnemyVariantKind, int> kinds = EnemyVariant.ChampionKinds.ToDictionary(kind => kind, _ => 0);
            int selfDestructUndying = 0;
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode room in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
                for (int wave = 0; wave < room.ResolvedEncounterWaves.Count; wave++)
                {
                    ResolvedEncounterSpawn[] spawns = room.ResolvedEncounterWaves[wave];
                    for (int index = 0; index < spawns.Length; index++)
                    {
                        ResolvedEncounterSpawn spawn = spawns[index];
                        if (!EnemyVariant.IsChampionKind(spawn.Variant)) continue;
                        Assert(spawn.Variant == EnemyVariantRules.PickChampionKind(spawn.Role, room.ContentSeed,
                                version, wave, index),
                            $"Seed {seed} room '{room.RoomId}' must keep the seeded champion property.");
                        kinds[spawn.Variant]++;
                        if (spawn.Variant == EnemyVariantKind.ChampionUndying &&
                            spawn.Role == EncounterEnemyRole.SelfDestruct)
                            selfDestructUndying++;
                    }
                }
            }

            int total = kinds.Values.Sum();
            Assert(kinds[EnemyVariantKind.ChampionUndying] > total * 0.2f &&
                   kinds[EnemyVariantKind.ChampionUndying] < total * 0.32f,
                "About a quarter of champions must be undying (" +
                string.Join(", ", kinds.Select(entry => $"{entry.Key} {entry.Value}")) + ").");
            return string.Join(", ", kinds.Select(entry => $"{entry.Key} {entry.Value}")) +
                   $", 자폭형 undying {selfDestructUndying}";
        }

        // Every eligible enemy accepts the property, keeps its stats until it falls, and runs undying when it does.
        private static string ValidateBodies(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-2 Body Verification");
            root.transform.position = Origin;
            int made = 0;
            try
            {
                int slot = 0;
                foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole))
                             .Cast<EncounterEnemyRole>().Where(EnemyVariantRules.IsEligible))
                {
                    Assert(roster.TryResolve(role, out GameObject prefab, out string error), error);
                    GameObject enemy = Champion(prefab, root.transform, Vector2.right * (slot++ * 6f));
                    Health health = enemy.GetComponent<Health>();
                    EnemyChampionUndying undying = enemy.GetComponent<EnemyChampionUndying>();
                    Assert(undying != null && !undying.IsActive && enemy.GetComponent<EnemyChampionDeath>() == null &&
                           enemy.GetComponent<EnemyChampionOutline>().Color == Color.white,
                        $"{prefab.name} ({role}) undying champion needs its component and white outline, no death effect.");
                    Assert(Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(health), 1f) &&
                           Mathf.Approximately(EnemyVariant.AttackIntervalOf(health, 1f), 1f),
                        $"{prefab.name} must keep its speed before it falls.");
                    PlayerHit(health, health.MaxHealth + 1f);
                    Assert(!health.IsDead && undying.IsActive &&
                           Mathf.Approximately(health.CurrentHealth, health.MaxHealth * EnemyChampionUndying.LifeShare) &&
                           Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(health), 1.3f) &&
                           Mathf.Approximately(EnemyVariant.AttackIntervalOf(health, 1.3f), 1f),
                        $"{prefab.name} must run undying at 85% life, 1.3x movement and attacks.");
                    undying.Tick(undying.EndTime);
                    Assert(health.IsDead && !undying.IsActive, $"{prefab.name} must fall when its undying life ends.");
                    made++;
                }
            }
            finally
            {
                Cleanup(root);
            }

            return $"{made} undying bodies";
        }

        private static void ValidateUndyingLife(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-2 Life Verification");
            root.transform.position = Origin + Vector2.down * 60f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject chaser, out string error), error);
                GameObject enemy = Champion(chaser, root.transform, Vector2.zero);
                Health health = enemy.GetComponent<Health>();
                EnemyChampionUndying undying = enemy.GetComponent<EnemyChampionUndying>();
                EnemyChampionOutline outline = enemy.GetComponent<EnemyChampionOutline>();
                int died = 0;
                health.Died += () => died++;
                float life = health.MaxHealth * EnemyChampionUndying.LifeShare;

                // A non-lethal hit is a plain hit.
                PlayerHit(health, 1f);
                Assert(!undying.HasTriggered && Mathf.Approximately(health.CurrentHealth, health.MaxHealth - 1f),
                    "A hit that does not kill must not start undying.");

                PlayerHit(health, health.MaxHealth);
                Assert(died == 0 && !health.IsDead && undying.IsActive &&
                       Mathf.Approximately(health.CurrentHealth, life),
                    "The first lethal hit must leave the champion standing at 85% life.");

                // The life runs out evenly over 3 s.
                undying.Tick(undying.StartTime + EnemyChampionUndying.Duration * 0.5f);
                Assert(Mathf.Abs(health.CurrentHealth - life * 0.5f) < 0.01f && !health.IsDead,
                    $"Half way the life must be half gone ({health.CurrentHealth}/{life}).");

                // Hits take it down faster.
                float beforeHit = health.CurrentHealth;
                PlayerHit(health, life * 0.2f);
                Assert(Mathf.Abs(health.CurrentHealth - (beforeHit - life * 0.2f)) < 0.01f && !health.IsDead,
                    "A hit while undying must take its damage off the life.");

                // The body is grey and see-through, the white outline still shows.
                SpriteRenderer visual = EnemyChampionOutline.ResolveBodySprite(enemy);
                Color own = visual.color;
                Invoke(undying, "LateUpdate");
                Color tinted = visual.color;
                Invoke(undying, "LateUpdate");
                Assert(visual.color == tinted && tinted == own * EnemyChampionUndying.BodyTint,
                    $"The undying body must be tinted once from its own colour ({own} -> {tinted} -> {visual.color}).");
                outline.Refresh(null, 0f);
                Assert(outline.VisibleCopyCount == EnemyChampionOutline.CopyCount,
                    "The white outline must stay while undying.");

                // 30% of the life is left at 1.5 s, so it runs out 0.9 s later, before the 3 s end.
                float runsOutAt = undying.StartTime + EnemyChampionUndying.Duration * 0.8f;
                undying.Tick(runsOutAt - 0.05f);
                Assert(!health.IsDead && died == 0, "The champion must still stand just before its life runs out.");
                undying.Tick(runsOutAt + 0.05f);
                Assert(health.IsDead && died == 1 && !undying.IsActive && runsOutAt + 0.05f < undying.EndTime &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(health), 1f),
                    "The hit champion must fall once, early, when its life runs out.");
                undying.Tick(undying.EndTime);
                Assert(died == 1, "The champion must not die twice.");

                // A second lethal hit while undying ends it at once.
                GameObject second = Champion(chaser, root.transform, Vector2.right * 6f);
                Health secondHealth = second.GetComponent<Health>();
                PlayerHit(secondHealth, secondHealth.MaxHealth + 1f);
                Assert(!secondHealth.IsDead, "The fixture must be undying.");
                PlayerHit(secondHealth, secondHealth.MaxHealth + 1f);
                Assert(secondHealth.IsDead && !second.GetComponent<EnemyChampionUndying>().IsActive,
                    "A lethal hit while undying must end it; undying happens only once.");

                // A plain enemy still dies at once.
                GameObject plain = (GameObject)PrefabUtility.InstantiatePrefab(chaser, root.transform);
                plain.transform.position = (Vector2)root.transform.position + Vector2.right * 12f;
                Health plainHealth = plain.GetComponent<Health>();
                InvokeAwake(plainHealth);
                PlayerHit(plainHealth, plainHealth.MaxHealth + 1f);
                Assert(plainHealth.IsDead, "A plain enemy must still die on a lethal hit.");
            }
            finally
            {
                Cleanup(root);
            }
        }

        // 자폭형 blowing itself up is not deferred; 누루링 splits only on the final fall.
        private static void ValidateExclusionsAndSplit(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-2 Split Verification");
            root.transform.position = Origin + Vector2.down * 120f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.SelfDestruct, out GameObject bomber, out string error),
                    error);
                GameObject self = Champion(bomber, root.transform, Vector2.zero);
                Health selfHealth = self.GetComponent<Health>();
                selfHealth.TakeDamage(new DamageContext(self, DamageSourceType.Environment, selfHealth.MaxHealth + 1f));
                Assert(selfHealth.IsDead && !self.GetComponent<EnemyChampionUndying>().HasTriggered,
                    "A 자폭형 blowing itself up must die at once, not run undying.");

                Assert(roster.TryResolve(EncounterEnemyRole.SplittingChaser, out GameObject splitter, out error), error);
                GameObject nuruling = Champion(splitter, root.transform, Vector2.right * 8f);
                Health health = nuruling.GetComponent<Health>();
                EnemySplitOnDeath split = nuruling.GetComponent<EnemySplitOnDeath>();
                EnemyChampionUndying undying = nuruling.GetComponent<EnemyChampionUndying>();
                Assert(split != null, "The 누루링 fixture must split on death.");
                InvokeAwake(split);
                PlayerHit(health, health.MaxHealth + 1f);
                Assert(!health.IsDead && split.LastSplits.Count == 0,
                    "An undying 누루링 must not split while it still stands.");
                undying.Tick(undying.EndTime);
                Assert(health.IsDead && split.LastSplits.Count == split.SplitCount,
                    "An undying 누루링 must split when it finally falls.");
            }
            finally
            {
                Cleanup(root);
            }
        }

        // Kill effects follow the final fall and credit the player's last hit.
        private static void ValidateKillCredit(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-2 Credit Verification");
            root.transform.position = Origin + Vector2.down * 180f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject chaser, out string error), error);
                GameObject player = new("Variant-2 Attacker", typeof(PlayerCombatEvents));
                player.transform.SetParent(root.transform);
                PlayerCombatEvents events = player.GetComponent<PlayerCombatEvents>();
                List<PlayerEnemyKilledEvent> kills = new();
                events.EnemyKilled += kills.Add;

                GameObject enemy = Champion(chaser, root.transform, Vector2.zero);
                Health health = enemy.GetComponent<Health>();
                EnemyChampionUndying undying = enemy.GetComponent<EnemyChampionUndying>();
                health.TakeDamage(new DamageContext(player, DamageSourceType.PlayerAttack, health.MaxHealth + 1f));
                Assert(kills.Count == 0 && undying.IsActive, "The downing blow must not report a kill yet.");
                health.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 1f));
                undying.Tick(undying.EndTime);
                Assert(kills.Count == 1 && kills[0].Target == health &&
                       kills[0].KillingBlow.SourceType == DamageSourceType.PlayerProjectile,
                    "The final fall must report one kill credited to the player's last hit.");

                // Downed by the environment and never hit again: no player kill.
                GameObject other = Champion(chaser, root.transform, Vector2.right * 6f);
                Health otherHealth = other.GetComponent<Health>();
                otherHealth.TakeDamage(new DamageContext(root, DamageSourceType.Environment, otherHealth.MaxHealth + 1f));
                Assert(other.GetComponent<EnemyChampionUndying>().IsActive,
                    "An environment blow must also start undying.");
                other.GetComponent<EnemyChampionUndying>().Tick(other.GetComponent<EnemyChampionUndying>().EndTime);
                Assert(otherHealth.IsDead && kills.Count == 1,
                    "Running out after an environment blow must not credit the player.");
            }
            finally
            {
                Cleanup(root);
            }
        }

        // The room clears only once the undying champion finally falls.
        private static void ValidateRoom(EncounterEnemyRoster roster)
        {
            GameObject root = new("Variant-2 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 240f;
            try
            {
                Assert(roster.TryResolve(EncounterEnemyRole.Chaser, out GameObject prefab, out string error), error);
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Variant-2 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(2, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points,
                        new[] { EnemyVariantKind.None, EnemyVariantKind.ChampionUndying }),
                });
                room.BindRunState(new RoomRunState("variant-2-room"), false);
                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                Health[] enemies = room.AliveEnemies.ToArray();
                Health champion = enemies.Single(enemy => EnemyVariant.KindOf(enemy.gameObject) ==
                                                          EnemyVariantKind.ChampionUndying);
                foreach (Health enemy in enemies) PlayerHit(enemy, enemy.MaxHealth + 1f);
                Assert(room.State != RoomState.Cleared && room.AliveEnemies.Contains(champion) && !champion.IsDead,
                    "The room must wait while the undying champion still stands.");
                EnemyChampionUndying undying = champion.GetComponent<EnemyChampionUndying>();
                undying.Tick(undying.EndTime);
                Assert(champion.IsDead && room.State == RoomState.Cleared,
                    "The room must clear when the undying champion finally falls.");
            }
            finally
            {
                Cleanup(root);
            }
        }

        private static GameObject Champion(GameObject prefab, Transform parent, Vector2 offset, int floor = 2)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.transform.position = (Vector2)parent.position + offset;
            InvokeAwake(enemy.GetComponent<Health>());
            FloorDifficultyScaler.ApplyScaling(enemy, floor);
            EnemyFloorLevel.Apply(enemy, floor);
            Assert(EnemyVariant.Apply(enemy, EnemyVariantKind.ChampionUndying),
                $"{prefab.name} must become an undying champion.");
            return enemy;
        }

        private static void PlayerHit(Health health, float amount) =>
            health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, amount));

        private static Health CreatePlayer(Transform parent, Vector2 feetAt)
        {
            GameObject player = new("Variant-2 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
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

        private static void Cleanup(GameObject root)
        {
            Object.DestroyImmediate(root);
            EnemyChampionPuddle.ClearAll();
            EnemyChampionVolley.ClearAll();
            Physics2D.SyncTransforms();
        }

        private static void InvokeAwake(Component component) => Invoke(component, "Awake");

        private static void Invoke(Component component, string method)
        {
            component?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
