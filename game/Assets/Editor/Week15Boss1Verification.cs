using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Character;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss1Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Boss-1 Buseureogi")]
        public static void Verify()
        {
            ValidateImportedArtAndBindings();
            ValidateBossContract();
            ValidateContactDamageAndFacing();
            ValidateBossBreaksObstacle();
            ValidateThreePatternExecution();
            ValidateSeededPlacementAndLimits();
            Week15Boss0Verification.Verify();
            Debug.Log("Week 15 Boss-1 verification passed: supplied Buseureogi/obstacle/Erpin art is bound, " +
                      "the boss chases and deals contact damage without being easily pushed, all three patterns execute, " +
                      "spawns remain inside the real room bounds, obstacles cannot drop SP, " +
                      "obstacles use seeded-random positions and damage/knock back players caught at impact, " +
                      "obstacles block minions but break on boss contact, the floor-one room stays Basic-sized, " +
                      "phase two pauses then accelerates seeded-random patterns, Erpin uses the corrected flip direction, " +
                      "and summon/obstacle caps and owned-object cleanup hold.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            string bossGuid = AssetDatabase.AssetPathToGUID(Week15Boss0Setup.BossPrefabPath);
            Week15Boss1Setup.Setup();
            Dictionary<string, string> tracked = TrackedPaths().Where(path => !string.IsNullOrWhiteSpace(
                    AssetDatabase.AssetPathToGUID(path)))
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week15Boss1Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Boss-1 Setup changed the Game Scene GUID.");
            Assert(bossGuid == AssetDatabase.AssetPathToGUID(Week15Boss0Setup.BossPrefabPath),
                "Boss-1 Setup changed the boss prefab GUID.");
            foreach (KeyValuePair<string, string> entry in tracked)
                Assert(entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Boss-1 Setup changed the GUID for {entry.Key}.");
            Verify();
        }

        private static void ValidateImportedArtAndBindings()
        {
            foreach (string path in new[]
                     {
                         Week15Boss1Setup.BossSpritePath,
                         Week15Boss1Setup.CreamSpritePath,
                         Week15Boss1Setup.DoughSpritePath,
                         Week15Boss1Setup.ErpinSpritePath,
                     })
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert(AssetDatabase.LoadAssetAtPath<Sprite>(path) != null && importer != null &&
                       importer.textureType == TextureImporterType.Sprite &&
                       importer.spriteImportMode == SpriteImportMode.Single &&
                       Mathf.Approximately(importer.spritePixelsPerUnit, 1000f) &&
                       importer.alphaIsTransparency && !importer.mipmapEnabled,
                    $"Supplied Boss-1 art has incorrect import settings: {path}.");
            }

            GameObject bossPrefab = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            Assert(bossPrefab.GetComponentInChildren<SpriteRenderer>().sprite ==
                   Load<Sprite>(Week15Boss1Setup.BossSpritePath),
                "Buseureogi body art is not assigned to the boss prefab.");
            GameObject minion = Load<GameObject>(Week15Boss1Setup.MinionPrefabPath);
            Assert(minion != null && minion.GetComponent<SpriteRenderer>().sprite ==
                   Load<Sprite>(Week15Boss1Setup.BossSpritePath) && minion.GetComponent<EnemyChase>() != null,
                "Crumb minion must temporarily reuse Buseureogi art and the verified melee behavior.");
            ValidateObstacle(Week15Boss1Setup.CreamPrefabPath, Week15Boss1Setup.CreamSpritePath);
            ValidateObstacle(Week15Boss1Setup.DoughPrefabPath, Week15Boss1Setup.DoughSpritePath);

            CharacterDefinition erpin = Load<CharacterDefinition>("Assets/Characters/erpin.asset");
            Assert(erpin != null && erpin.Portrait == Load<Sprite>(Week15Boss1Setup.ErpinSpritePath),
                "Erpin CharacterDefinition must expose the supplied portrait.");
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            Assert(player != null && player.GetComponent<SpriteRenderer>().sprite == erpin.Portrait &&
                   player.GetComponent<KnockbackReceiver>() != null,
                "The Game Scene Player must render Erpin art and accept obstacle-impact knockback.");
        }

        private static void ValidateObstacle(string prefabPath, string spritePath)
        {
            GameObject prefab = Load<GameObject>(prefabPath);
            Rigidbody2D body = prefab != null ? prefab.GetComponent<Rigidbody2D>() : null;
            CircleCollider2D collider = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
            TestEnemy obstacle = prefab != null ? prefab.GetComponent<TestEnemy>() : null;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert(prefab != null && prefab.layer == LayerMask.NameToLayer("Enemy") &&
                   prefab.GetComponent<SpriteRenderer>()?.sprite == Load<Sprite>(spritePath) &&
                   prefab.GetComponent<Health>() != null && obstacle != null &&
                   !obstacle.AllowsSPDrop && obstacle.DestroyOnBossCollision &&
                   body != null && body.bodyType == RigidbodyType2D.Static &&
                   collider != null && !collider.isTrigger && collider.layerOverridePriority > 0 &&
                   (collider.includeLayers.value & 1 << enemyLayer) != 0 &&
                   Physics2D.GetIgnoreLayerCollision(enemyLayer, enemyLayer) &&
                   !Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("Player"), enemyLayer),
                $"Obstacle prefab is not visible, solid, destructible, and player-attackable: {prefabPath}.");
        }

        private static void ValidateBossBreaksObstacle()
        {
            GameObject bossObject = PrefabUtility.InstantiatePrefab(
                Load<GameObject>(Week15Boss0Setup.BossPrefabPath)) as GameObject;
            GameObject obstacleObject = PrefabUtility.InstantiatePrefab(
                Load<GameObject>(Week15Boss1Setup.CreamPrefabPath)) as GameObject;
            try
            {
                Health obstacleHealth = obstacleObject.GetComponent<Health>();
                TestEnemy obstacle = obstacleObject.GetComponent<TestEnemy>();
                Invoke(obstacleHealth, "Awake");
                Invoke(obstacle, "Awake");
                Assert(!obstacle.TryBreakForBoss(null),
                    "Only a BossController may break a Buseureogi obstacle on contact.");
                Assert(obstacle.TryBreakForBoss(bossObject.GetComponent<BossController>()) && obstacleObject == null,
                    "Buseureogi must destroy its own solid obstacle instead of being blocked by it.");
            }
            finally
            {
                if (obstacleObject != null) UnityEngine.Object.DestroyImmediate(obstacleObject);
                if (bossObject != null) UnityEngine.Object.DestroyImmediate(bossObject);
            }
        }

        private static void ValidateBossContract()
        {
            GameObject prefab = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            BossController boss = prefab != null ? prefab.GetComponent<BossController>() : null;
            BuseureogiBossPatternRuntime runtime = prefab != null
                ? prefab.GetComponent<BuseureogiBossPatternRuntime>()
                : null;
            Assert(boss != null && runtime != null && boss.DisplayName == "부스러기" && boss.PhaseCount == 2,
                "Boss-1 prefab is missing the Buseureogi runtime or HUD identity.");
            Assert(Mathf.Approximately(boss.PhaseTransitionDuration, 1.25f) &&
                   Mathf.Approximately(boss.PhaseTwoTempoMultiplier, 0.72f) &&
                   Mathf.Approximately(runtime.PhaseTwoSpeedMultiplier, 1.35f),
                "Boss-1 phase-two preparation, tempo, or movement multiplier is incorrect.");
            Assert(boss.Patterns.Select(pattern => pattern.Execution).ToHashSet().SetEquals(new[]
                {
                    BossPatternExecution.BuseureogiApproachVolley,
                    BossPatternExecution.BuseureogiSummonMinions,
                    BossPatternExecution.BuseureogiThrowObstacle,
                }), "Boss-1 must configure exactly its approach volley, summon, and obstacle patterns.");
            Assert(runtime.MinionPrefab == Load<GameObject>(Week15Boss1Setup.MinionPrefabPath) &&
                   runtime.ObstaclePrefabs.Count == 2 && runtime.ObstaclePrefabs.All(item => item != null) &&
                   runtime.MaximumMinions == 4 && runtime.ObstaclesPerThrowLimit == 3 &&
                   Mathf.Approximately(runtime.ObstacleImpactRadius, 1.1f) &&
                   runtime.ObstacleImpactDamageTier == EnemyDamageTier.Light &&
                   Mathf.Approximately(runtime.ObstacleKnockbackSpeed, 7f) &&
                   runtime.MinionOffsets.Count == 12 && runtime.MinionOffsets.All(offset =>
                       Mathf.Abs(offset.magnitude - 1.8f) < 0.01f),
                "Boss-1 spawned-content references or caps are incorrect.");
            Assert(runtime.ObstacleOffsets.Count == 4 && runtime.ObstacleOffsets.All(offset =>
                       Mathf.Abs(offset.x) > 1.45f && Mathf.Abs(offset.y) > 1.45f),
                "Every obstacle candidate must remain outside the open horizontal/vertical door cross.");
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            ContactDamage contactDamage = prefab.GetComponent<ContactDamage>();
            Assert(Mathf.Approximately(prefab.transform.localScale.x, Week15Boss1Setup.BossScale) &&
                   Mathf.Approximately(prefab.transform.localScale.y, Week15Boss1Setup.BossScale),
                "Buseureogi must use the doubled boss body scale.");
            Assert(body != null && body.bodyType == RigidbodyType2D.Dynamic && body.mass >= 25f &&
                   (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                "Buseureogi must retain dynamic chase motion while resisting player collision pushes.");
            Assert(contactDamage != null && contactDamage.DamageTier == EnemyDamageTier.Heavy &&
                   Mathf.Approximately(contactDamage.Cooldown, 0.8f) &&
                   Mathf.Approximately(runtime.ApproachSpeed, 2.25f),
                "Buseureogi contact damage or chase speed is not configured.");
        }

        private static void ValidateContactDamageAndFacing()
        {
            GameObject bossInstance = PrefabUtility.InstantiatePrefab(
                Load<GameObject>(Week15Boss0Setup.BossPrefabPath)) as GameObject;
            GameObject playerObject = new("Boss-1 Contact Target");
            try
            {
                playerObject.AddComponent<SpriteRenderer>();
                playerObject.AddComponent<Rigidbody2D>();
                Health health = playerObject.AddComponent<Health>();
                playerObject.AddComponent<PlayerStats>();
                playerObject.AddComponent<PlayerActionState>();
                playerObject.AddComponent<DamageInvulnerability>();
                PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
                PlayerSPDropper spDropper = playerObject.AddComponent<PlayerSPDropper>();
                Invoke(health, "Awake");
                Invoke(movement, "Awake");

                float initialHealth = health.CurrentHealth;
                ContactDamage contactDamage = bossInstance.GetComponent<ContactDamage>();
                Assert(contactDamage.TryApplyDamage(health, 0f) &&
                       Mathf.Approximately(health.CurrentHealth,
                           initialHealth - HealthUnits.GetEnemyDamageUnits(contactDamage.DamageTier, 1)),
                    "Buseureogi contact must immediately damage a living player.");

                SpriteRenderer renderer = playerObject.GetComponent<SpriteRenderer>();
                movement.SetFacingDirection(Vector2.left);
                Assert(!renderer.flipX, "Erpin's supplied art must remain unflipped when moving left.");
                movement.SetFacingDirection(Vector2.up);
                Assert(!renderer.flipX, "Vertical movement must preserve Erpin's last horizontal facing.");
                movement.SetFacingDirection(Vector2.right);
                Assert(renderer.flipX, "Erpin's supplied art must flip when moving right.");
                Assert(!spDropper.CanDropFrom(Load<GameObject>(Week15Boss1Setup.CreamPrefabPath)
                           .GetComponent<Health>()) &&
                       !spDropper.CanDropFrom(Load<GameObject>(Week15Boss1Setup.DoughPrefabPath)
                           .GetComponent<Health>()),
                    "Boss-created obstacles must never be eligible for an SP drop.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                if (bossInstance != null) UnityEngine.Object.DestroyImmediate(bossInstance);
            }
        }

        private static void ValidateSeededPlacementAndLimits()
        {
            Vector2 first = SampleFirstObstaclePosition(9182);
            Vector2 repeated = SampleFirstObstaclePosition(9182);
            Vector2 differentSeed = SampleFirstObstaclePosition(9183);
            Assert(first == repeated, "The same content seed must reproduce the first obstacle candidate.");
            Assert(first != differentSeed,
                "Different content seeds must produce different continuous obstacle landing positions.");

            GameObject root = CreateRuntimeBoss(out BossController boss, out BuseureogiBossPatternRuntime runtime,
                out GameObject target);
            try
            {
                Rect expectedBounds = Load<RoomProfile>(Week14Room6Setup.BossFloor1ProfilePath).EncounterBounds;
                RoomProfile basicProfile = Load<RoomProfile>(Week14Room1Setup.BasicProfilePath);
                RoomProfile bossRoomProfile = Load<RoomProfile>(Week14Room6Setup.BossFloor1ProfilePath);
                Assert(bossRoomProfile.InteriorSize == RoomLayout.RoomSize &&
                       bossRoomProfile.MovementBounds == basicProfile.MovementBounds &&
                       bossRoomProfile.EncounterBounds == basicProfile.EncounterBounds &&
                       bossRoomProfile.CameraBounds == basicProfile.CameraBounds,
                    "The floor-one Buseureogi room must stay fixed to the Basic room size and camera frame.");
                Assert(runtime.ArenaBounds == expectedBounds && runtime.ArenaOrigin == expectedBounds.center,
                    "Boss-1 must resolve spawn bounds from the containing room, not its offset spawn point.");

                target.transform.position = new Vector2(4f, 0f);
                runtime.TickPattern(BossActionState.Telegraph,
                    BossPatternExecution.BuseureogiApproachVolley, 0.1f);
                Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
                Vector2 targetDirection = ((Vector2)target.transform.position - (Vector2)boss.transform.position).normalized;
                Assert(Mathf.Approximately(body.linearVelocity.magnitude, runtime.ApproachSpeed) &&
                       Vector2.Dot(body.linearVelocity.normalized, targetDirection) > 0.99f,
                    "Approach-volley telegraph and active time must chase the player.");
                runtime.TickPattern(BossActionState.Recovery,
                    BossPatternExecution.BuseureogiApproachVolley, 0.2f);
                Assert(body.linearVelocity == Vector2.zero, "Buseureogi must stop chasing during recovery.");

                Assert(runtime.PrepareObstacleTelegraph() && runtime.HasPendingObstacleTelegraph &&
                       runtime.PendingObstacleCount == 3,
                    "Obstacle telegraph must visibly reserve three seeded-random landing positions before impact.");
                Vector2[] firstWavePositions = runtime.PendingObstaclePositions.ToArray();
                Assert(firstWavePositions.All(position => runtime.IsInsideArena(position)) &&
                       firstWavePositions.All(position =>
                           Mathf.Abs(position.x - runtime.ArenaOrigin.x) > 1.45f &&
                           Mathf.Abs(position.y - runtime.ArenaOrigin.y) > 1.45f) &&
                       firstWavePositions.Any(position => !runtime.ObstacleOffsets.Contains(
                           position - runtime.ArenaOrigin)),
                    "Random obstacle positions must stay inside the arena and outside the open door cross.");

                Health targetHealth = target.GetComponent<Health>();
                KnockbackReceiver targetKnockback = target.GetComponent<KnockbackReceiver>();
                target.transform.position = firstWavePositions[0];
                float healthBeforeImpact = targetHealth.CurrentHealth;
                Assert(runtime.CommitPendingObstacle() && !runtime.HasPendingObstacleTelegraph,
                    "Obstacle impact must replace every landing telegraph with a destructible obstacle.");
                Assert(Mathf.Approximately(targetHealth.CurrentHealth,
                           healthBeforeImpact -
                           HealthUnits.GetEnemyDamageUnits(runtime.ObstacleImpactDamageTier, 1)) &&
                       targetKnockback.IsKnockedBack &&
                       Mathf.Approximately(targetKnockback.CurrentVelocity.magnitude,
                           runtime.ObstacleKnockbackSpeed),
                    "A player standing in a landing marker must take damage and be knocked out of the impact.");
                Invoke(target.GetComponent<PlayerMovement>(), "FixedUpdate");
                Assert(Mathf.Approximately(targetKnockback.CurrentVelocity.magnitude,
                           runtime.ObstacleKnockbackSpeed),
                    "Player movement must not overwrite obstacle knockback while the impact is active.");
                Assert(runtime.PrepareObstacleTelegraph() && runtime.PendingObstacleCount == 3 &&
                       runtime.PendingObstaclePositions.Where((position, index) =>
                           (position - firstWavePositions[index]).sqrMagnitude > 0.01f).Any(),
                    "A second throw must reserve three new random positions even while three obstacles remain.");
                Assert(runtime.CommitPendingObstacle() && runtime.LiveObstacleCount == 6,
                    "Each throw must add three obstacles instead of treating three live obstacles as a field cap.");

                int firstWave = runtime.SpawnMinions(4);
                int secondWave = runtime.SpawnMinions(4);
                Assert(firstWave == 4 && secondWave == 0 && runtime.LiveMinionCount == runtime.MaximumMinions &&
                       runtime.LiveMinions.All(item =>
                           Mathf.Abs(((Vector2)item.transform.position - (Vector2)boss.transform.position).magnitude -
                                     1.8f) < 0.01f),
                    "Each crumb summon must create four minions around the boss and stop at the field cap.");
                Assert(runtime.LiveMinions.All(item => runtime.IsInsideArena(item.transform.position)) &&
                       runtime.LiveObstacles.All(item => runtime.IsInsideArena(item.transform.position)),
                    "Every summoned enemy and obstacle must remain inside the room walls.");

                int ownedBeforeDeath = boss.OwnedObjectCount;
                Assert(ownedBeforeDeath >= runtime.LiveMinionCount + runtime.LiveObstacleCount,
                    "Every minion and obstacle must be registered for boss-owned cleanup.");
                boss.Health.TakeDamage(999f);
                Assert(boss.OwnedObjectCount == 0,
                    "Boss death must clean up every surviving crumb minion and obstacle.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateThreePatternExecution()
        {
            GameObject prefab = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            GameObject target = new("Boss-1 Pattern Target");
            target.transform.position = new Vector2(2f, 1f);
            target.AddComponent<Health>();
            try
            {
                BossController boss = instance.GetComponent<BossController>();
                BuseureogiBossPatternRuntime runtime = instance.GetComponent<BuseureogiBossPatternRuntime>();
                Invoke(instance.GetComponent<Health>(), "Awake");
                Invoke(runtime, "Awake");
                Invoke(boss, "Awake");
                HashSet<string> activePatterns = new(StringComparer.Ordinal);
                boss.BeginCombat(target.transform, 444, 0f);
                for (int step = 1; step <= 600 && activePatterns.Count < 3; step++)
                {
                    boss.TickBehavior(step * 0.1f);
                    if (boss.State == BossActionState.Active) activePatterns.Add(boss.CurrentPatternId);
                }

                Assert(activePatterns.SetEquals(new[]
                    {
                        "buseureogi-approach-volley",
                        "buseureogi-summon-crumbs",
                        "buseureogi-throw-obstacle",
                    }), "The deterministic runtime must execute every Boss-1 pattern, not only serialize it.");
                int projectileCount = UnityEngine.Object.FindObjectsByType<BossProjectile>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                Assert(runtime.LiveMinionCount > 0 && runtime.LiveObstacleCount > 0 && projectileCount > 0,
                    $"Boss-1 execution must create a projectile, crumb minion, and destructible obstacle. " +
                    $"Observed projectiles={projectileCount}, minions={runtime.LiveMinionCount}, " +
                    $"obstacles={runtime.LiveObstacleCount}.");

                boss.Health.TakeDamage(boss.Health.MaxHealth * 0.51f);
                Assert(boss.State == BossActionState.PhaseTransition && boss.CurrentPhase == 1 &&
                       runtime.CurrentApproachSpeed == runtime.ApproachSpeed &&
                       boss.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                    "Crossing 50% health must cancel the current pattern and pause for phase-two preparation.");
                float phaseTransitionEndsAt = boss.StateEndsAt;
                boss.TickBehavior(phaseTransitionEndsAt + 0.01f);
                Assert(boss.CurrentPhase == 2 && boss.State == BossActionState.Telegraph &&
                       Mathf.Approximately(boss.CurrentTempoMultiplier, 0.72f) &&
                       Mathf.Approximately(runtime.CurrentApproachSpeed,
                           runtime.ApproachSpeed * runtime.PhaseTwoSpeedMultiplier) &&
                       runtime.CurrentShotInterval < 0.42f,
                    "Phase two must resume with a seeded-random pattern, faster timing, and faster movement.");
                BossPatternDefinition phaseTwoPattern = boss.Patterns.First(pattern =>
                    pattern.PatternId == boss.CurrentPatternId);
                Assert(Mathf.Approximately(boss.StateEndsAt - phaseTransitionEndsAt,
                           phaseTwoPattern.TelegraphDuration * boss.PhaseTwoTempoMultiplier),
                    "Phase-two pattern telegraphs must use the faster tempo multiplier.");
                boss.CancelCombat();
                Assert(boss.OwnedObjectCount == 0,
                    "Cancelling a Boss-1 encounter must remove pattern-created content.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Vector2 SampleFirstObstaclePosition(int seed)
        {
            GameObject root = CreateRuntimeBoss(out _, out BuseureogiBossPatternRuntime runtime,
                out GameObject target, seed);
            try
            {
                Assert(runtime.TrySpawnObstacle(), "A safe Boss-1 candidate should accept one obstacle.");
                Assert(runtime.LiveObstacleCount == BuseureogiBossPatternRuntime.ObstaclesPerThrow,
                    "A Boss-1 obstacle throw should create three safe obstacles.");
                return runtime.LiveObstacles[0].transform.position;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateRuntimeBoss(out BossController boss,
            out BuseureogiBossPatternRuntime runtime, out GameObject target, int seed = 71)
        {
            GameObject root = new("Boss-1 Runtime Verification Room");
            RoomNode room = root.AddComponent<RoomNode>();
            room.ApplyRoomProfile(Load<RoomProfile>(Week14Room6Setup.BossFloor1ProfilePath));
            GameObject bossObject = new("Boss-1 Runtime Verification");
            bossObject.transform.SetParent(root.transform, false);
            bossObject.transform.localPosition = new Vector2(-3f, 2f);
            bossObject.AddComponent<SpriteRenderer>().sprite = Load<Sprite>(Week15Boss1Setup.BossSpritePath);
            Rigidbody2D body = bossObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 25f;
            Health health = bossObject.AddComponent<Health>();
            bossObject.AddComponent<KnockbackReceiver>();
            boss = bossObject.AddComponent<BossController>();
            runtime = bossObject.AddComponent<BuseureogiBossPatternRuntime>();
            runtime.Configure(
                Load<GameObject>(Week15Boss1Setup.MinionPrefabPath),
                new[]
                {
                    Load<GameObject>(Week15Boss1Setup.CreamPrefabPath),
                    Load<GameObject>(Week15Boss1Setup.DoughPrefabPath),
                },
                Week15Boss1Setup.MinionOffsets,
                Week15Boss1Setup.ObstacleOffsets);
            runtime.ConfigureLimits(4, 3);
            runtime.ConfigureObstacleImpact(1.1f, EnemyDamageTier.Light, 7f, 0.2f);
            runtime.ConfigureMovement(2.25f, 1.35f);
            Invoke(health, "Awake");
            Invoke(runtime, "Awake");
            Invoke(boss, "Awake");
            target = new GameObject("Boss-1 Verification Target");
            Rigidbody2D targetBody = target.AddComponent<Rigidbody2D>();
            targetBody.gravityScale = 0f;
            Health targetHealth = target.AddComponent<Health>();
            KnockbackReceiver targetKnockback = target.AddComponent<KnockbackReceiver>();
            PlayerStats targetStats = target.AddComponent<PlayerStats>();
            target.AddComponent<PlayerActionState>();
            target.AddComponent<DamageInvulnerability>();
            PlayerMovement targetMovement = target.AddComponent<PlayerMovement>();
            Invoke(targetHealth, "Awake");
            Invoke(targetKnockback, "Awake");
            Invoke(targetStats, "Awake");
            Invoke(targetMovement, "Awake");
            boss.BeginCombat(target.transform, seed, 0f);
            return root;
        }

        private static IEnumerable<string> TrackedPaths() => new[]
        {
            Week15Boss1Setup.BossSpritePath,
            Week15Boss1Setup.CreamSpritePath,
            Week15Boss1Setup.DoughSpritePath,
            Week15Boss1Setup.ErpinSpritePath,
            Week15Boss1Setup.MinionPrefabPath,
            Week15Boss1Setup.CreamPrefabPath,
            Week15Boss1Setup.DoughPrefabPath,
            Week15Boss0Setup.BossPrefabPath,
            "Assets/Characters/erpin.asset",
        };

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, $"Missing verification hook {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
