using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss3Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Boss-3 Crayon Hero")]
        public static void Verify()
        {
            Week15Boss2Verification.Verify();
            ValidatePrefabAndLargeRoom();
            ValidateTemporaryMinionRoles();
            ValidateMapSlashAndApproachSwing();
            ValidateSummonDamageGateAndCleanup();
            ValidatePatternCadenceAndDash();
            ValidateGoldenTransitionAndTempo();
            ValidateFloorThreeRuntimeBinding();
            Debug.Log("Week 15 Boss-3 verification passed: Crayon Hero continuously pressures with weighted " +
                      "chase swings, one-to-three aimed dashes, four-role damage-gated summons, a scarce slash, " +
                      "and a faster golden phase with three independently stacking slash lines.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week15Boss3Setup.Setup();
            string[] paths = AllStableAssetPaths();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week15Boss3Setup.Setup();
            for (int index = 0; index < paths.Length; index++)
                Assert(!string.IsNullOrWhiteSpace(guids[index]) &&
                       guids[index] == AssetDatabase.AssetPathToGUID(paths[index]),
                    $"Boss-3 setup changed the GUID for {paths[index]}.");
            Verify();
        }

        private static void ValidatePrefabAndLargeRoom()
        {
            GameObject prefab = Load<GameObject>(Week15Boss3Setup.BossPrefabPath);
            Sprite sprite = Load<Sprite>(Week15Boss3Setup.BossSpritePath);
            BossController boss = prefab != null ? prefab.GetComponent<BossController>() : null;
            CrayonHeroBossPatternRuntime runtime = prefab != null
                ? prefab.GetComponent<CrayonHeroBossPatternRuntime>()
                : null;
            CircleCollider2D collider = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
            Assert(prefab != null && sprite != null && boss != null && runtime != null && collider != null,
                "Boss-3 prefab is missing its sprite, controller, runtime, or collider.");
            Assert(prefab.GetComponent<BuseureogiBossPatternRuntime>() == null &&
                   prefab.GetComponent<SaemaeumVaultBossPatternRuntime>() == null,
                "Boss-3 must not retain a previous floor boss runtime.");
            Assert(Mathf.Approximately(prefab.transform.localScale.x, Week15Boss1Setup.BossScale) &&
                   Mathf.Approximately(prefab.transform.localScale.y, Week15Boss1Setup.BossScale),
                "Crayon Hero must use the same authored size as Buseureogi and Saemaeum Vault.");
            Assert(boss.DisplayName == "크레용사용" && boss.PhaseCount == 2 && boss.Patterns.Count == 4,
                "Boss-3 HUD identity or four-pattern/two-phase contract is missing.");
            Assert(boss.Patterns.Select(item => item.Execution).SequenceEqual(new[]
                {
                    BossPatternExecution.CrayonHeroMapSlash,
                    BossPatternExecution.CrayonHeroSummonMinions,
                    BossPatternExecution.CrayonHeroApproachSwing,
                    BossPatternExecution.CrayonHeroDashChain,
                }), "Boss-3 patterns are not bound in the authored order.");
            Assert(runtime.SlashLength >= 18f && Mathf.Approximately(runtime.SlashWidth, 1.15f) &&
                   Mathf.Approximately(runtime.SlashLockedDelay, 0.2f) &&
                   runtime.SummonsPerPattern == 4 &&
                   Mathf.Approximately(runtime.SummonReactivationHealthFraction, 0.15f) &&
                   Mathf.Approximately(runtime.SwingWindup, 0.3f) &&
                   Mathf.Approximately(runtime.SwingInterval, 0.7f) &&
                   Mathf.Approximately(runtime.SwingRange, 2.4f) &&
                   Mathf.Approximately(runtime.SwingWidth, 3.2f) &&
                   runtime.SwingStartOffset > collider.bounds.extents.x &&
                   Mathf.Approximately(runtime.SwingTriggerRange, runtime.SwingAttackReach * 1.625f) &&
                   Mathf.Approximately(runtime.CurrentApproachSpeed, 2.5f) &&
                   Mathf.Approximately(runtime.DashSpeed, 15f) &&
                   Mathf.Approximately(runtime.DashDuration, 0.17f),
                "Boss-3 pressure movement, summon gate, swing, or dash tuning drifted from verified values.");
            Assert(Mathf.Approximately(boss.Patterns[1].TelegraphDuration, 0.425f) &&
                   Mathf.Approximately(boss.Patterns[1].ActiveDuration, 0.1f) &&
                   Mathf.Approximately(boss.Patterns[1].RecoveryDuration, 0.4f),
                "Boss-3 summon stop time must remain approximately half of its first implementation.");
            Assert(Mathf.Approximately(boss.Patterns[3].RecoveryDuration, 0.3f),
                "Boss-3 must stop for 0.3 seconds after the complete dash chain.");
            Assert(prefab.GetComponent<SpriteRenderer>() == null &&
                   prefab.GetComponentInChildren<SpriteRenderer>()?.sprite == sprite,
                "Boss-3 must keep the authored sprite on a visual child.");
            Assert(collider.radius * 2f < Mathf.Min(sprite.bounds.size.x, sprite.bounds.size.y),
                "Boss-3 collision must remain smaller than its visible body.");

            RoomProfile profile = Load<RoomProfile>(Week14Room6Setup.BossFloor3ProfilePath);
            Assert(profile != null && profile.ProfileId == Week14Room6Setup.BossFloor3ProfileId,
                "Boss-3 requires the stable floor-three Large room profile.");
            float diameter = collider.radius * 2f * Week15Boss1Setup.BossScale;
            Assert(diameter < Mathf.Min(profile.MovementBounds.width, profile.MovementBounds.height) * 0.35f,
                "Boss-3 collision silhouette is too large for the floor-three room.");
        }

        private static void ValidateTemporaryMinionRoles()
        {
            ValidateMinion(Week15Boss3Setup.ArcherPrefabPath, Week15Boss3Setup.ArcherSpritePath, true);
            ValidateMinion(Week15Boss3Setup.MagePrefabPath, Week15Boss3Setup.MageSpritePath, true);
            ValidateMinion(Week15Boss3Setup.AxePrefabPath, Week15Boss3Setup.AxeSpritePath, false);
            ValidateMinion(Week15Boss3Setup.ShieldPrefabPath, Week15Boss3Setup.ShieldSpritePath, false);
        }

        private static void ValidateMinion(string prefabPath, string spritePath, bool sniper)
        {
            GameObject prefab = Load<GameObject>(prefabPath);
            Sprite sprite = Load<Sprite>(spritePath);
            Assert(prefab != null && sprite != null && prefab.GetComponent<SpriteRenderer>()?.sprite == sprite,
                $"Boss-3 minion art is not bound at {prefabPath}.");
            Assert((prefab.GetComponent<LongRangeSniperController>() != null) == sniper,
                $"Boss-3 temporary role mismatch at {prefabPath}.");
            Assert(sniper || (prefab.GetComponent<EnemyChase>() != null &&
                              prefab.GetComponent<MeleeEnemyAttack>() != null),
                $"Boss-3 melee minion must keep the fast chase/melee behavior at {prefabPath}.");
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Assert(importer != null && importer.alphaIsTransparency && !importer.mipmapEnabled,
                $"Boss-3 sprite import contract is missing at {spritePath}.");
        }

        private static void ValidateMapSlashAndApproachSwing()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out CrayonHeroBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                target.transform.position = new Vector2(8f, 0f);
                float before = target.GetComponent<Health>().CurrentHealth;
                runtime.BeginCombat(target.transform, 3303, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroMapSlash, 1f);
                target.transform.position = new Vector2(0f, 8f);
                runtime.TickPattern(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroMapSlash, 0.9f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.CrayonHeroMapSlash, 1.2f);
                runtime.TryExecute(BossPatternExecution.CrayonHeroMapSlash);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroMapSlash, 1.09f);
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before) &&
                       runtime.LockedDirection.y > 0.99f,
                    "Map slash must preserve 0.2 seconds of dodge time after locking its direction.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroMapSlash, 1.1f);
                Assert(target.GetComponent<Health>().CurrentHealth < before,
                    "Map slash must resolve on its locked line after the 0.2-second dodge window.");

                target.GetComponent<Health>().ResetHealth();
                target.transform.position = new Vector2(5f, 0f);
                runtime.BeginCombat(target.transform, 3304, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.CrayonHeroApproachSwing, 3f);
                runtime.TryExecute(BossPatternExecution.CrayonHeroApproachSwing);
                before = target.GetComponent<Health>().CurrentHealth;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroApproachSwing, 0.1f);
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before) &&
                       !runtime.IsSwingPreparing,
                    "Approaching must not create an immediate sword hit.");
                target.transform.position = (Vector2)root.transform.position +
                                            Vector2.right * (runtime.SwingAttackReach * 1.29f);
                Rigidbody2D bossBody = root.GetComponent<Rigidbody2D>();
                bossBody.linearVelocity = new Vector2(0.75f, 0f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroApproachSwing, 0.2f);
                Assert(runtime.IsSwingPreparing && runtime.AttackHitboxVisible &&
                       bossBody.linearVelocity.x > 0.7f &&
                       Vector2.Distance(root.transform.position, target.transform.position) >
                       runtime.SwingAttackReach,
                    "Sword swing must start without stopping movement inside the 130% recognition circle.");
                LineRenderer swingHitbox = GameObject.Find("Crayon Hero Sword Swing Hitbox")
                    ?.GetComponent<LineRenderer>();
                Assert(swingHitbox != null && swingHitbox.startColor.a >= 0.8f &&
                       swingHitbox.sortingOrder > root.GetComponentInChildren<SpriteRenderer>().sortingOrder &&
                       Mathf.Abs(Vector2.Distance(root.transform.position,
                           swingHitbox.GetPosition(0)) - runtime.SwingStartOffset) < 0.01f &&
                       Mathf.Abs(Vector2.Distance(swingHitbox.GetPosition(0),
                           swingHitbox.GetPosition(1)) - runtime.SwingRange) < 0.01f &&
                       Mathf.Approximately(swingHitbox.startWidth, runtime.SwingWidth),
                    "Sword hitbox must be a large visible box beginning outside the boss body.");
                target.transform.position = (Vector2)root.transform.position +
                                            Vector2.right * (runtime.SwingStartOffset + 0.2f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroApproachSwing, 0.49f);
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before),
                    "Sword swing must preserve its short readable windup.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroApproachSwing, 0.51f);
                Assert(target.GetComponent<Health>().CurrentHealth < before &&
                       runtime.CompletedSwings == 1 && runtime.SwingCountTarget is >= 2 and <= 3,
                    "Sword swing must hit after its windup and continue a two-to-three swing pressure chain.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSummonDamageGateAndCleanup()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out CrayonHeroBossPatternRuntime runtime, out Health health, out GameObject target);
            try
            {
                runtime.ConfigureSummons(new[]
                {
                    Load<GameObject>(Week15Boss3Setup.ArcherPrefabPath),
                    Load<GameObject>(Week15Boss3Setup.MagePrefabPath),
                    Load<GameObject>(Week15Boss3Setup.AxePrefabPath),
                    Load<GameObject>(Week15Boss3Setup.ShieldPrefabPath),
                }, 4, 2.5f, 0.15f);
                runtime.BeginCombat(target.transform, 4404, 0f);
                Assert(runtime.TryExecute(BossPatternExecution.CrayonHeroSummonMinions) &&
                       runtime.LivingMinionCount == 4 && !runtime.SummonAvailable,
                    "Boss-3 must summon exactly two ranged and two melee minions in its first wave.");
                string[] names = UnityEngine.Object.FindObjectsByType<TestEnemy>(FindObjectsSortMode.None)
                    .Select(item => item.gameObject.name).ToArray();
                Assert(names.Count(name => name.Contains("CrayonArcherMinion")) == 1 &&
                       names.Count(name => name.Contains("CrayonMageMinion")) == 1 &&
                       names.Count(name => name.Contains("CrayonAxeMinion")) == 1 &&
                       names.Count(name => name.Contains("CrayonShieldMinion")) == 1,
                    "Boss-3 summon wave must contain each authored ranged/melee role exactly once.");
                health.TakeDamage(new DamageContext(target, DamageSourceType.PlayerAttack, 14f));
                Assert(!runtime.CanSelect(BossPatternExecution.CrayonHeroSummonMinions),
                    "Boss-3 summon must stay locked before taking 15% maximum-health damage.");
                health.TakeDamage(new DamageContext(target, DamageSourceType.PlayerAttack, 1f));
                Assert(runtime.CanSelect(BossPatternExecution.CrayonHeroSummonMinions) &&
                       runtime.TryExecute(BossPatternExecution.CrayonHeroSummonMinions) &&
                       runtime.LivingMinionCount == 8 && !runtime.SummonAvailable,
                    "Boss-3 summon must reactivate once after 15% damage without a living cap.");
                Assert(boss.OwnedObjectCount >= 8,
                    "Boss-3 summons must be owned by the boss for room/death cleanup.");
                boss.CancelCombat();
                Assert(boss.OwnedObjectCount == 0,
                    "Boss-3 cancellation must clean all summoned minions and telegraphs.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidatePatternCadenceAndDash()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out CrayonHeroBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 4455, 0f);
                Assert(!runtime.CanSelect(BossPatternExecution.CrayonHeroMapSlash) &&
                       runtime.GetSelectionWeight(BossPatternExecution.CrayonHeroApproachSwing) == 45 &&
                       runtime.GetSelectionWeight(BossPatternExecution.CrayonHeroDashChain) == 30 &&
                       runtime.GetSelectionWeight(BossPatternExecution.CrayonHeroSummonMinions) == 10 &&
                       runtime.GetSelectionWeight(BossPatternExecution.CrayonHeroMapSlash) == 15,
                    "Boss-3 must begin with weighted pressure attacks rather than an immediate map slash.");
                runtime.OnPatternStateChanged(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroApproachSwing, 1f);
                runtime.OnPatternStateChanged(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroDashChain, 2f);
                runtime.OnPatternStateChanged(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroSummonMinions, 3f);
                runtime.TickPattern(BossActionState.Cooldown, BossPatternExecution.SignalOnly, 8.99f);
                Assert(runtime.CompletedPressureActions == 3 &&
                       !runtime.CanSelect(BossPatternExecution.CrayonHeroMapSlash),
                    "Three pressure attacks must not bypass the absolute nine-second slash cooldown.");
                runtime.TickPattern(BossActionState.Cooldown, BossPatternExecution.SignalOnly, 9f);
                Assert(runtime.CanSelect(BossPatternExecution.CrayonHeroMapSlash),
                    "Map slash must become selectable after three pressure actions and nine seconds.");
                runtime.OnPatternStateChanged(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroMapSlash, 10f);
                Assert(runtime.CompletedPressureActions == 0 &&
                       !runtime.CanSelect(BossPatternExecution.CrayonHeroMapSlash) &&
                       runtime.NextSlashEligibleAt >= 18f,
                    "Map slash must reset pressure progress and start a fresh absolute cooldown.");

                Assert(Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, false) == 1) == 25 &&
                       Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, false) == 2) == 50 &&
                       Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, false) == 3) == 25 &&
                       Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, true) == 1) == 25 &&
                       Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, true) == 2) == 30 &&
                       Enumerable.Range(0, 100).Count(roll =>
                           CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, true) == 3) == 45,
                    "Dash count distribution must remain 25/50/25 normally and 25/30/45 when golden.");

                runtime.BeginCombat(target.transform, 4466, 0f);
                target.transform.position = Vector2.right * 4f;
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroDashChain, 0.2f);
                target.transform.position = Vector2.up * 4f;
                runtime.TickPattern(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroDashChain, 0.19f);
                Assert(runtime.LockedDirection.y > 0.99f,
                    "Initial dash telegraph must track the moving player for 0.2 seconds.");
                runtime.TryExecute(BossPatternExecution.CrayonHeroDashChain);
                SetPrivateField(runtime, "dashCountTarget", 3);
                Vector2 firstDirection = runtime.LockedDirection;
                target.transform.position = Vector2.left * 4f;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroDashChain, 0.25f);
                Assert(Vector2.Dot(firstDirection, runtime.LockedDirection) > 0.99f,
                    "Dash direction must stay fixed after the dash begins.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroDashChain, 0.37f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroDashChain, 0.42f);
                Assert(!runtime.IsDashInMotion && runtime.LockedDirection.x < -0.99f,
                    "The 0.1-second gap between dashes must track the player before relocking.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroDashChain, 0.48f);
                Assert(runtime.IsDashInMotion && runtime.LockedDirection.x < -0.99f,
                    "The next dash must lock the direction acquired during its retarget window.");

                Rigidbody2D bossBody = root.GetComponent<Rigidbody2D>();
                bossBody.linearVelocity = new Vector2(3f, 2f);
                runtime.OnPatternStateChanged(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroDashChain, 0.78f);
                runtime.TickPattern(BossActionState.Recovery,
                    BossPatternExecution.CrayonHeroDashChain, 0.6f);
                Assert(bossBody.linearVelocity.sqrMagnitude <= 0.0001f,
                    "Boss-3 must remain fully stopped during the 0.3-second post-dash recovery.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateGoldenTransitionAndTempo()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out CrayonHeroBossPatternRuntime runtime, out Health health, out GameObject target);
            try
            {
                boss.ConfigurePhaseTwo(0.5f, 0.78f);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("phase-test", BossPatternExecution.CrayonHeroMapSlash,
                        1f, 0.2f, 1f, 0f),
                });
                boss.BeginCombat(target.transform, 5505, 0f);
                health.TakeDamage(new DamageContext(target, DamageSourceType.PlayerAttack, 55f));
                Assert(boss.State == BossActionState.PhaseTransition && health.IsInvulnerable,
                    "Boss-3 must enter one invulnerable golden transition at 50% HP.");
                boss.TickBehavior(Time.time + 0.51f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroMapSlash, Time.time + 1f);
                SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>();
                Assert(boss.CurrentPhase == 2 && Mathf.Approximately(boss.CurrentTempoMultiplier, 0.78f) &&
                       !health.IsInvulnerable && renderer.color != Color.white &&
                       runtime.CurrentApproachSpeed > 2.5f,
                    "Golden phase must release invulnerability, tint the same sprite, and accelerate tempo/movement.");

                target.GetComponent<DamageInvulnerability>().ResetHitWindow();
                // The three golden lines deliberately use narrow 0.57-wide lanes; keep the
                // probe inside their shared near-boss overlap instead of testing the side lanes
                // at a distance where their 15-degree spread has already separated.
                target.transform.position = Vector2.right * 0.8f;
                float before = target.GetComponent<Health>().CurrentHealth;
                runtime.BeginCombat(target.transform, 5515, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.CrayonHeroMapSlash, 1f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.CrayonHeroMapSlash, 1.2f);
                runtime.TryExecute(BossPatternExecution.CrayonHeroMapSlash);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.CrayonHeroMapSlash, 0.2f);
                Assert(runtime.ActiveSlashDirectionCount == 3 &&
                       Mathf.Approximately(before - target.GetComponent<Health>().CurrentHealth,
                           3 * HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Critical, 1)),
                    "Golden slash must expose three lines at -15/0/+15 degrees and stack all three hits nearby.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateFloorThreeRuntimeBinding()
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            GameObject expected = Load<GameObject>(Week15Boss3Setup.BossPrefabPath);
            Assert(assembler != null && expected != null && assembler.ResolveBossPrefab(1) != null &&
                   assembler.ResolveBossPrefab(2) == Load<GameObject>(Week15Boss2Setup.BossPrefabPath) &&
                   assembler.ResolveBossPrefab(3) == expected,
                "Floor-specific boss roster must resolve Crayon Hero only on floor three.");

            int seed = assembler.Progress.HasRunSeed ? assembler.Progress.RunSeed : 3303;
            assembler.Progress.ResetProgress();
            Assert(assembler.Progress.TryInitializeRunSeed(seed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);
            Assert(assembler.TryLoadFloor(3, assembler.Graph.Player, out error), error);
            RoomPrefab bossRoom = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.Definition.RoomType == RoomType.Boss);
            BossController spawned = null;
            bossRoom.Controller.EnemySpawned += enemy => spawned = enemy?.GetComponent<BossController>();
            bossRoom.Controller.BeginCombat(assembler.Graph.Player.GetComponent<Health>());
            Assert(spawned != null && spawned.DisplayName == "크레용사용" &&
                   spawned.GetComponent<CrayonHeroBossPatternRuntime>() != null,
                "Generated floor-three boss room did not spawn Crayon Hero.");
        }

        private static GameObject CreateRuntimeBoss(out BossController boss,
            out CrayonHeroBossPatternRuntime runtime, out Health health, out GameObject target)
        {
            GameObject root = new GameObject("Crayon Hero Runtime Verification");
            health = root.AddComponent<Health>();
            SetPrivateField(health, "maxHealth", 100f);
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            root.AddComponent<KnockbackReceiver>();
            root.AddComponent<CircleCollider2D>().radius = 0.38f;
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<SpriteRenderer>();
            boss = root.AddComponent<BossController>();
            runtime = root.AddComponent<CrayonHeroBossPatternRuntime>();
            Invoke(health, "Awake");
            Invoke(runtime, "Awake");
            Invoke(boss, "Awake");
            health.ResetHealth();
            boss.ConfigureHud("크레용사용", 2);
            boss.ConfigurePhaseTwo(0.5f, 0.78f);
            runtime.ConfigureSlash(18f, 1.15f, 0.2f, EnemyDamageTier.Critical);
            runtime.ConfigureSwing(2.5f, 1.35f, 2.4f, 3.2f, 0.15f, 0.3f, 0.7f, 0.12f, EnemyDamageTier.Heavy);
            runtime.ConfigureDash(15f, 0.17f, 0.1f, 1.8f, EnemyDamageTier.Heavy);
            runtime.ConfigureSelection(3, 9f, 7.5f, 45, 30, 10, 15);

            target = new GameObject("Crayon Hero Player Target");
            target.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Health targetHealth = target.AddComponent<Health>();
            SetPrivateField(targetHealth, "maxHealth", 100f);
            target.AddComponent<PlayerMovement>();
            Invoke(targetHealth, "Awake");
            targetHealth.ResetHealth();
            return root;
        }

        private static string[] AllStableAssetPaths() => new[]
        {
            Week15Boss3Setup.BossPrefabPath,
            Week15Boss3Setup.ArcherPrefabPath,
            Week15Boss3Setup.MagePrefabPath,
            Week15Boss3Setup.AxePrefabPath,
            Week15Boss3Setup.ShieldPrefabPath,
            Week15Boss3Setup.BossSpritePath,
            Week15Boss3Setup.ArcherSpritePath,
            Week15Boss3Setup.MageSpritePath,
            Week15Boss3Setup.AxeSpritePath,
            Week15Boss3Setup.ShieldSpritePath,
        };

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Missing verification field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, $"Missing verification hook {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
