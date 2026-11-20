using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss2Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Boss-2 Saemaeum Vault")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            ValidatePrefabAndTallRoom();
            ValidateElasticReadabilityAndSeededJumps();
            ValidateApproachWalkThrow();
            ValidateLandingBurst();
            ValidateGoldPillars();
            ValidateGoldMeteorRain();
            ValidateGoldCage();
            ValidatePillarWaves();
            ValidateUnlimitedPhaseOneHealingAndPhaseTwo();
            ValidateLandingDamageAndKnockback();
            ValidateSlimeScatter();
            ValidateGroundedContact();
            ValidateJumpArcAndCollisionRestoration();
            ValidateJumpCurveFeel();
            ValidateFinalJumpCompletesBeforeRecovery();
            ValidateWallAndCornerLandings();
            ValidateStandingAreaAndLandingShadow();
            ValidateLandingTelegraphCleanup();
            ValidatePhaseTwoWarningAndLandingShockwave();
            ValidateFloorTwoRuntimeBinding();
            Week15Boss1Verification.Verify();
            Debug.Log("Week 15 Boss-2 verification passed: Saemaeum Vault keeps its own sprite/prefab, " +
                      "walks throwing three-shot fans, jumps 2 / 3 times with a turning treasure ring outside each " +
                      "landing, heals 138 only in phase one, slows with slime to 0.55x, raises hit-counted gold " +
                      "pillars that block its shots (each blocked shot cracking them) and break under its landings, and rains gold lumps in phase two.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week15Boss2Setup.Setup();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week15Boss2Setup.BossPrefabPath);
            string spriteGuid = AssetDatabase.AssetPathToGUID(Week15Boss2Setup.BossSpritePath);
            Week15Boss2Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(prefabGuid) &&
                   prefabGuid == AssetDatabase.AssetPathToGUID(Week15Boss2Setup.BossPrefabPath),
                "Boss-2 setup changed the Saemaeum Vault prefab GUID.");
            Assert(!string.IsNullOrWhiteSpace(spriteGuid) &&
                   spriteGuid == AssetDatabase.AssetPathToGUID(Week15Boss2Setup.BossSpritePath),
                "Boss-2 setup changed the Saemaeum Vault sprite GUID.");
            Verify();
        }

        private static void ValidatePrefabAndTallRoom()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Week15Boss2Setup.BossSpritePath);
            BossController boss = prefab != null ? prefab.GetComponent<BossController>() : null;
            SaemaeumVaultBossPatternRuntime runtime = prefab != null
                ? prefab.GetComponent<SaemaeumVaultBossPatternRuntime>()
                : null;
            CircleCollider2D collider = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
            Assert(prefab != null && sprite != null && boss != null && runtime != null && collider != null,
                "Boss-2 prefab is missing its sprite, controller, runtime, or collider.");
            Assert(prefab.GetComponent<BuseureogiBossPatternRuntime>() == null,
                "Boss-2 must not retain the floor-one Buseureogi runtime.");
            EnemyFeet prefabFeet = prefab.GetComponentInChildren<EnemyFeet>(true);
            Assert(prefabFeet != null && prefabFeet.gameObject.activeSelf &&
                   (collider.excludeLayers.value & EnemyFeet.TerrainMask) == EnemyFeet.TerrainMask,
                "Boss-2 must walk on feet: the body leaves walls, obstacles and pits to the feet.");
            Assert(prefab.GetComponent<SpriteRenderer>() == null &&
                   prefab.GetComponentInChildren<SpriteRenderer>()?.transform != prefab.transform,
                "Boss-2 elastic animation must live on a visual child so its collider never stretches.");
            Assert(Mathf.Approximately(prefab.transform.localScale.x, Week15Boss1Setup.BossScale) &&
                   Mathf.Approximately(prefab.transform.localScale.y, Week15Boss1Setup.BossScale),
                "Saemaeum Vault must use the doubled boss body scale.");
            Assert(boss.DisplayName == "새마음금고" && boss.PhaseCount == 2 && boss.Patterns.Count == 8,
                "Boss-2 HUD identity or its eight-pattern/two-phase contract is missing.");
            Assert(boss.Patterns.Select(pattern => pattern.Execution).SequenceEqual(new[]
                {
                    BossPatternExecution.SaemaeumApproachWalk,
                    BossPatternExecution.SaemaeumJumpSequence,
                    BossPatternExecution.SaemaeumTreasureHeal,
                    BossPatternExecution.SaemaeumSlimeScatter,
                    BossPatternExecution.SaemaeumGoldPillars,
                    BossPatternExecution.SaemaeumGoldMeteor,
                    BossPatternExecution.SaemaeumGoldCage,
                    BossPatternExecution.SaemaeumPillarWave,
                }), "Boss-2 patterns are not bound in the authored order.");
            Assert(boss.Patterns.Select(pattern => pattern.PatternId).SequenceEqual(new[]
                {
                    "saemaeum-approach-walk", "saemaeum-rage-jump-sequence", "saemaeum-eat-treasure",
                    "saemaeum-slime-scatter", "saemaeum-golden-pillars", "saemaeum-gold-meteor",
                    "saemaeum-gold-cage", "saemaeum-pillar-wave",
                }), "Boss-2 pattern IDs changed; the removed fan throw ID must not be reused.");
            Assert(runtime.LargeSlimeCountThisPhase == 1 && runtime.SmallSlimeCountThisPhase == 4 &&
                   Mathf.Approximately(runtime.SlimePuddleRadius, 1.2f) &&
                   Mathf.Approximately(runtime.SmallSlimePuddleRadius, 0.5f) &&
                   Mathf.Approximately(runtime.SlimeSpeedMultiplier, 0.55f) && runtime.SlimeRequiredHits == 3 &&
                   runtime.MaximumSlimePuddles == 48 && runtime.LandingLeavesSlime,
                "Boss-2 slime scatter must spit one large (1.2) and four obstacle-sized (0.5) puddles that slow " +
                "to 0.55x and pop after 3 hits.");
            Assert(Mathf.Approximately(runtime.JumpDistance, 7f) &&
                   runtime.PhaseOneMinimumJumps == 2 && runtime.PhaseOneMaximumJumps == 2 &&
                   runtime.PhaseTwoMinimumJumps == 3 && runtime.PhaseTwoMaximumJumps == 3 &&
                   Mathf.Approximately(runtime.PhaseOneJumpDuration, 1.5f) &&
                   Mathf.Approximately(runtime.PhaseTwoJumpDuration, 1.4f) &&
                   Mathf.Approximately(runtime.PreJumpCrouchDuration, 0.8f) &&
                   Mathf.Approximately(runtime.PhaseOneJumpRest, 1.0f) &&
                   Mathf.Approximately(runtime.PhaseTwoJumpRest, 0.85f) &&
                   Mathf.Approximately(runtime.HealPerUse, 138f) &&
                   Mathf.Approximately(runtime.LandingRadius, 2.875f) &&
                   runtime.LandingDamageTier == EnemyDamageTier.Medium,
                "Boss-2 must jump 2 / 3 times with 1.5 / 1.4 jumps, 1.0 / 0.85 rests and a 0.8s wind-up.");
            BossPatternDefinition jump = boss.Patterns.Single(pattern =>
                pattern.Execution == BossPatternExecution.SaemaeumJumpSequence);
            Assert(Mathf.Abs(jump.GetTelegraphDuration(1f) - 0.8f) < 0.01f &&
                   Mathf.Abs(jump.GetTelegraphDuration(0.88f) - 0.8f) < 0.01f,
                "The first jump must crouch for 0.8s in both phases.");
            Assert(GetPrivateField<int>(runtime, "phaseOneLandingBurstCount") == 12 &&
                   GetPrivateField<int>(runtime, "phaseTwoLandingBurstCount") == 16 &&
                   Mathf.Approximately(GetPrivateField<float>(runtime, "phaseOneLandingBurstSpeed"), 4.5f) &&
                   Mathf.Approximately(GetPrivateField<float>(runtime, "phaseTwoLandingBurstSpeed"), 5.0f) &&
                   runtime.LandingBurstDamageTier == EnemyDamageTier.Light &&
                   Mathf.Approximately(runtime.LandingBurstSpawnRadius, 2.875f + 0.2f),
                "Boss-2 landings must scatter 12 / 16 light treasure shots at 4.5 / 5.0 just outside the radius.");
            Assert(GetPrivateField<int>(runtime, "phaseOnePillars") == 7 &&
                   GetPrivateField<int>(runtime, "phaseTwoPillars") == 9 &&
                   Mathf.Approximately(GetPrivateField<float>(runtime, "phaseOnePillarWarning"), 0.8f) &&
                   Mathf.Approximately(GetPrivateField<float>(runtime, "phaseTwoPillarWarning"), 0.7f) &&
                   Mathf.Approximately(runtime.PillarStagger, 0.12f) &&
                   Mathf.Approximately(runtime.PillarHitRadius, 0.6f) &&
                   runtime.PillarDamageTier == EnemyDamageTier.Light && runtime.PillarRequiredHits == 3 &&
                   runtime.PillarTargetEvery == 3,
                "Boss-2 gold pillars must be 7 / 9 per cast, warn 0.8 / 0.7, hurt lightly, break after 3 hits and " +
                "aim one in three at the player.");
            Assert(Mathf.Approximately(runtime.MeteorMinimumDuration, 8f) &&
                   Mathf.Approximately(runtime.MeteorMaximumDuration, 10f) && runtime.MeteorOpeningCount == 5 &&
                   Mathf.Approximately(runtime.MeteorOpeningInterval, 0.35f) &&
                   Mathf.Approximately(runtime.MeteorInterval, 0.45f) && runtime.MeteorTargetEvery == 3 &&
                   runtime.MeteorLeadsPlayer &&
                   Mathf.Approximately(runtime.MeteorWarning, 0.9f) &&
                   Mathf.Approximately(runtime.MeteorRadius, 0.9f) &&
                   runtime.MeteorDamageTier == EnemyDamageTier.Light &&
                   Mathf.Approximately(runtime.MeteorCooldownAfterRain, 8f),
                "Boss-2 gold rain must last 8-10s: five lumps 0.35s apart, then one every 0.45s, one in three aimed and the next one leading.");
            TextureImporter importer = AssetImporter.GetAtPath(Week15Boss2Setup.BossSpritePath) as TextureImporter;
            Assert(importer != null && importer.alphaIsTransparency && importer.mipmapEnabled == false,
                "Boss-2 sprite must preserve transparency and disable mipmaps.");
            Assert(collider.radius * 2f < Mathf.Min(sprite.bounds.size.x, sprite.bounds.size.y),
                "Boss-2 collision must remain smaller than its visible body.");

            RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room6Setup.BossFloor2ProfilePath);
            Assert(profile != null && profile.ProfileId == Week14Room6Setup.BossFloor2ProfileId,
                "Boss-2 requires the stable floor-two Tall room profile.");
            Assert(profile.MovementBounds.height > profile.MovementBounds.width * 0.6f,
                "Boss-2 Tall room does not provide enough vertical jump space.");
            float diameter = collider.radius * 2f * Week15Boss1Setup.BossScale;
            Assert(diameter < Mathf.Min(profile.MovementBounds.width, profile.MovementBounds.height) * 0.4f,
                "Boss-2 collision silhouette is too large for the floor-two room.");
        }

        private static void ValidateElasticReadabilityAndSeededJumps()
        {
            List<int> first = SampleJumpCounts(31415, 12, false);
            List<int> repeat = SampleJumpCounts(31415, 12, false);
            List<int> secondPhase = SampleJumpCounts(31415, 12, true);
            Assert(first.SequenceEqual(repeat), "Boss-2 jump counts must repeat for the same content seed.");
            Assert(first.All(count => count == 2), "Boss-2 phase one must always jump twice.");
            Assert(secondPhase.All(count => count == 3), "Boss-2 phase two must always jump three times.");

            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.SaemaeumJumpSequence, 0.95f);
                Vector3 rootScale = root.transform.localScale;
                Assert(runtime.CurrentVisualScale == Vector3.one,
                    "Jump preparation must keep original artwork proportions while runtime deformation is disabled.");
                Assert(root.transform.localScale == rootScale,
                    "Jump crouch changed the root scale and therefore the collision silhouette.");
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 4.75f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                float flightSampleTime = GetPrivateField<float>(runtime, "phaseOneJumpDuration") * 0.5f;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, flightSampleTime);
                Assert(runtime.CurrentVisualScale == Vector3.one,
                    "Boss-2 flight must keep original artwork proportions while runtime deformation is disabled.");
                Assert(root.transform.localScale == rootScale,
                    "Jump flight changed the root scale and therefore the collision silhouette.");
                Assert(boss.OwnedObjectCount == 1,
                    "Each jump must expose one landing-position telegraph before impact.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 2026-11-21: the walk throws again, a narrow three-shot fan aimed at the player, five volleys per walk.
        private static void ValidateApproachWalkThrow()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                Assert(runtime.ApproachVolleys == 5 && runtime.ApproachProjectilesPerVolley == 3 &&
                       Mathf.Approximately(runtime.ApproachThrowInterval, 0.55f) &&
                       Mathf.Approximately(runtime.ApproachSpreadDegrees, 30f) &&
                       Mathf.Approximately(runtime.ApproachProjectileSpeed, 5.5f),
                    "Boss-2 approach must throw five volleys of three shots, 30 degrees wide, 0.55s apart at 5.5.");
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachWalk, 3f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumApproachWalk),
                    "Boss-2 approach walk did not execute.");
                List<BossProjectile> first = FindBossProjectiles();
                Assert(runtime.FiredApproachVolleys == 1 && first.Count == 3,
                    $"Boss-2 approach must throw its first three-shot volley at once. shots={first.Count}.");
                Vector2 toPlayer = ((Vector2)target.transform.position - (Vector2)root.transform.position).normalized;
                List<float> angles = first.Select(shot => Vector2.SignedAngle(toPlayer,
                    GetPrivateField<Vector2>(shot, "velocity"))).OrderBy(angle => angle).ToList();
                Assert(Mathf.Abs(angles[0] + 15f) < 0.5f && Mathf.Abs(angles[1]) < 0.5f &&
                       Mathf.Abs(angles[2] - 15f) < 0.5f,
                    $"Boss-2 approach fan must center on the player. angles={string.Join(",", angles)}.");
                for (int step = 1; step <= 30; step++)
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumApproachWalk, step * 0.1f);
                Vector2 velocity = root.GetComponent<Rigidbody2D>().linearVelocity;
                Assert(velocity.x > 0.1f && Mathf.Abs(velocity.y) < 0.01f,
                    "Boss-2 approach must still walk toward the player.");
                int shots = FindBossProjectiles().Count;
                Assert(runtime.FiredApproachVolleys == 5 && shots == 15,
                    $"Boss-2 approach must stop after five volleys. volleys={runtime.FiredApproachVolleys}, shots={shots}.");
            }
            finally
            {
                foreach (BossProjectile shot in FindBossProjectiles()) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateLandingBurst()
        {
            foreach (bool phaseTwo in new[] { false, true })
            {
                GameObject root = CreateRuntimeBoss(out BossController boss,
                    out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
                try
                {
                    // Close to the start, so the whole ring fits in the fallback arena.
                    target.transform.position = new Vector2(1f, 0f);
                    if (phaseTwo) boss.SetPhase(2);
                    runtime.BeginCombat(target.transform, 5, 0f);
                    runtime.OnPatternStateChanged(BossActionState.Active,
                        BossPatternExecution.SaemaeumJumpSequence, 6f);
                    runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                    float duration = phaseTwo ? runtime.PhaseTwoJumpDuration : runtime.PhaseOneJumpDuration;
                    float rest = phaseTwo ? runtime.PhaseTwoJumpRest : runtime.PhaseOneJumpRest;
                    Assert(FindBossProjectiles().Count == 0, "No treasure may fly before the first landing.");
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                        duration + 0.001f);
                    List<BossProjectile> first = FindBossProjectiles();
                    int expected = phaseTwo ? 16 : 12;
                    float speed = phaseTwo ? 5.0f : 4.5f;
                    Vector2 landing = target.transform.position;
                    Assert(first.Count == expected, $"Each landing must scatter {expected} shots. got={first.Count}.");
                    foreach (BossProjectile shot in first)
                    {
                        Vector2 offset = (Vector2)shot.transform.position - landing;
                        Vector2 velocity = GetPrivateField<Vector2>(shot, "velocity");
                        Assert(Mathf.Abs(offset.magnitude - runtime.LandingBurstSpawnRadius) < 0.01f &&
                               offset.magnitude > runtime.LandingRadius,
                            "Landing shots must start just outside the landing radius.");
                        Assert(Mathf.Abs(velocity.magnitude - speed) < 0.01f &&
                               Vector2.Angle(offset, velocity) < 0.5f,
                            "Landing shots must fly straight outward at the phase speed.");
                    }
                    Assert(runtime.LandingBurstDamageTier == EnemyDamageTier.Light,
                        "Landing shots must deal light damage.");

                    // 2026-11-21: the landing leaves one large puddle on the spot and a touching ring of small ones
                    // out to the landing radius.
                    List<BossSlimePuddle> puddles = runtime.SlimePuddles.ToList();
                    int smallCount = runtime.LandingSmallSlimeCount;
                    Assert(smallCount == 14 && Mathf.Approximately(runtime.LandingSlimeRingRadius, 2.375f) &&
                           puddles.Count == 1 + smallCount,
                        $"A landing must leave one large and 14 small puddles. got={puddles.Count}.");
                    Assert(Vector2.Distance(puddles[0].transform.position, landing) < 0.001f &&
                           Mathf.Approximately(puddles[0].Radius, 1.2f),
                        "The large landing puddle must lie where the boss landed.");
                    foreach (BossSlimePuddle small in puddles.Skip(1))
                        Assert(Mathf.Approximately(small.Radius, 0.5f) &&
                               Mathf.Abs(Vector2.Distance(small.transform.position, landing) - 2.375f) < 0.001f,
                            "Small landing puddles must ring the large one inside the landing radius.");

                    // Lands, stands, then crouches for the last 0.8s of the rest before the next jump.
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                        duration + 0.01f);
                    Assert(runtime.JumpPoseIndex == 0, "The vault must stand right after landing.");
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                        duration + rest - 0.4f);
                    Assert(runtime.JumpPoseIndex == 1 && runtime.JumpCycleFrameIndex == 1 && runtime.CurrentJump == 0,
                        "The vault must crouch before its next jump.");

                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                        duration * 2f + rest + 0.001f);
                    List<BossProjectile> second = FindBossProjectiles().Except(first).ToList();
                    Assert(second.Count == expected, "The second landing must scatter its own ring.");
                    float firstAngle = MinimumRingAngle(first, landing);
                    Vector2 secondLanding = GetPrivateField<Vector2>(runtime, "jumpStart");
                    float secondAngle = MinimumRingAngle(second, secondLanding);
                    float halfGap = 180f / expected;
                    Assert(Mathf.Abs(Mathf.DeltaAngle(firstAngle, secondAngle) - halfGap) < 0.5f ||
                           Mathf.Abs(Mathf.DeltaAngle(secondAngle, firstAngle) - halfGap) < 0.5f,
                        $"The next landing must turn the ring by half a gap. first={firstAngle}, second={secondAngle}.");
                }
                finally
                {
                    boss.CancelCombat();
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void ValidateGoldPillars()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 61, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.SaemaeumGoldPillars, 0.8f);
                Assert(runtime.JumpCycleFrameIndex == 1, "The pillar summon must crouch during its warning.");
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumGoldPillars, 0.6f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumGoldPillars), "Gold pillars did not execute.");
                List<SaemaeumGoldPillar> pillars = runtime.Pillars.ToList();
                Assert(pillars.Count == 7, "Phase one must raise seven gold pillars.");
                Rect arena = GetPrivateField<Rect>(runtime, "arenaBounds");
                for (int index = 0; index < pillars.Count; index++)
                {
                    SaemaeumGoldPillar pillar = pillars[index];
                    Assert(!pillar.HasEmerged && pillar.Mark != null && pillar.Obstacle == null &&
                           pillar.GetComponent<Collider2D>() == null,
                        "A pillar must only mark the floor before it bursts out.");
                    Assert(Mathf.Approximately(pillar.EmergeAt, 0.8f + index * 0.12f),
                        "Pillars must burst 0.8s after the mark, 0.12s apart.");
                    Assert(arena.Contains(pillar.transform.position), "Pillars must stand inside the arena.");
                    Assert(pillar.TargetsPlayer == (index % 3 == 0),
                        "One pillar in three, starting with the first, must aim at the player.");
                }
                Assert(Vector2.Distance(pillars[0].transform.position, target.transform.position) < 0.001f,
                    "The first aimed pillar must rise where the player stands.");
                float besidePlayer = Vector2.Distance(pillars[3].transform.position, target.transform.position);
                Assert(besidePlayer >= SaemaeumVaultBossPatternRuntime.PillarSpacing - 0.001f &&
                       besidePlayer <= SaemaeumVaultBossPatternRuntime.PillarSpacing * 2f + 0.001f,
                    $"A later aimed pillar must rise right next to the player. distance={besidePlayer}.");
                AssertPillarsApart(pillars, "one cast");

                // Bursting under the player hurts once and knocks back; the pillar then stays as an obstacle.
                SaemaeumGoldPillar first = pillars[0];
                target.transform.position = first.transform.position + Vector3.right * 0.3f;
                Health targetHealth = target.GetComponent<Health>();
                float before = targetHealth.CurrentHealth;
                first.Tick(first.EmergeAt);
                Assert(first.HasEmerged && first.Mark == null && first.LastHitCount == 1 &&
                       Mathf.Approximately(targetHealth.CurrentHealth,
                           before - HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)) &&
                       target.GetComponent<KnockbackReceiver>().IsKnockedBack,
                    "A pillar bursting under the player must deal light damage and knock back once.");
                Collider2D pillarBody = first.GetComponent<Collider2D>();
                Assert(pillarBody.GetComponent<DestructibleObstacle>().BlocksFlight &&
                       !RoomMovementClass.IsLowObstacle(pillarBody),
                    "A gold pillar must be a high obstacle like a tree that a flying player cannot pass over.");
                first.Tick(first.EmergeAt + 1f);
                Assert(Mathf.Approximately(targetHealth.CurrentHealth,
                        before - HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)),
                    "A standing pillar must not hurt again.");
                DestructibleObstacle obstacle = first.GetComponent<DestructibleObstacle>();
                Collider2D body = first.GetComponent<Collider2D>();
                Assert(first.gameObject.layer == LayerMask.NameToLayer("Environment") && obstacle != null &&
                       body != null && !body.isTrigger && first.Obstacle != null &&
                       first.Obstacle.BlocksBossProjectiles && obstacle.RequiredHits == 3 &&
                       obstacle.DropTable == null && obstacle.TryValidate(out _),
                    "An emerged pillar must be a solid, hit-counted Environment obstacle that blocks boss shots.");
                Assert(first.GetComponent<Health>() == null && first.GetComponentInChildren<Health>() == null,
                    "A pillar must not be an enemy: no Health for kills, on-hit items, status effects or homing.");
                SpriteRenderer drawing = first.GetComponentInChildren<SpriteRenderer>();
                Assert(drawing != null && drawing.sprite != null, "An emerged pillar must be drawn.");

                // Boss shots stop on a pillar, and each one it blocks wears it down like a player hit (2026-11-21).
                BossProjectile shot = BossProjectile.Create(first.transform.position + Vector3.left * 2f,
                    Vector2.right * 5f, root, EnemyDamageTier.Light, null);
                InvokeWith(shot, "OnTriggerEnter2D", body);
                Assert(shot == null && obstacle.HitsTaken == 1 && !obstacle.IsBroken,
                    "A gold pillar must stop the boss's shot and take one hit from it.");

                // Hits are counted, not damage.
                Assert(obstacle.RegisterPlayerHit() && !obstacle.IsBroken, "A pillar must survive its second hit.");
                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken &&
                       !BossSummonedObstacle.Live.Contains(first.Obstacle),
                    "A pillar must break on its third hit.");

                // The boss walking into a pillar breaks it.
                SaemaeumGoldPillar second = pillars[1];
                second.Tick(second.EmergeAt);
                Assert(second.Obstacle.TryBreakForBoss(boss) && second == null,
                    "The boss must break a pillar by walking into it.");

                // A landing breaks the pillars near it.
                SaemaeumGoldPillar third = pillars[2];
                third.Tick(third.EmergeAt);
                target.transform.position = third.transform.position + Vector3.up * 1.5f;
                root.transform.position = third.transform.position + Vector3.left * 3f;
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 10f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                    runtime.PhaseOneJumpDuration + 0.001f);
                Assert(third == null, "A boss landing must break the pillars inside its landing radius.");

                boss.SetPhase(2);
                int pending = runtime.Pillars.Count(item => item != null && !item.HasEmerged);
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumGoldPillars, 0.6f);
                runtime.TryExecute(BossPatternExecution.SaemaeumGoldPillars);
                AssertPillarsApart(runtime.Pillars.Where(item => item != null && item.gameObject.activeSelf).ToList(),
                    "a second cast next to standing pillars");
                Assert(runtime.Pillars.Count(item => item != null && !item.HasEmerged) == pending + 9 &&
                       Mathf.Approximately(runtime.PillarWarningThisPhase, 0.7f),
                    "Phase two must raise nine pillars after a 0.7s warning.");
                boss.CancelCombat();
                Assert(runtime.Pillars.All(item => item == null) && BossSummonedObstacle.Live.Count == 0,
                    "Leaving the boss fight must remove every pillar and mark.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateGoldMeteorRain()
        {
            Assert(Mathf.Approximately(SaemaeumGoldRain.ShadowOffset(4, 5, 0.35f, 0.45f), 1.4f) &&
                   Mathf.Approximately(SaemaeumGoldRain.ShadowOffset(5, 5, 0.35f, 0.45f), 1.85f) &&
                   Mathf.Approximately(SaemaeumGoldRain.ShadowOffset(6, 5, 0.35f, 0.45f), 2.3f),
                "Gold rain must drop five lumps 0.35s apart and then one every 0.45s.");
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target, 1000f);
            try
            {
                target.transform.position = new Vector2(2f, 1f);
                runtime.BeginCombat(target.transform, 73, 0f);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumGoldMeteor),
                    "Gold rain must not be used in phase one.");
                boss.SetPhase(2);
                Assert(runtime.CanSelect(BossPatternExecution.SaemaeumGoldMeteor),
                    "Gold rain must be available in phase two.");
                runtime.OnPatternStateChanged(BossActionState.Telegraph, BossPatternExecution.SaemaeumGoldMeteor, 0.9f);
                Assert(runtime.JumpCycleFrameIndex == 1, "The gold rain cast must crouch during its warning.");
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumGoldMeteor, 0.3f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumGoldMeteor), "Gold rain did not execute.");
                SaemaeumGoldRain rain = runtime.GoldRain;
                float duration = rain.SpawnEndsAt - rain.StartedAt;
                Assert(rain != null && duration >= 8f - 0.001f && duration <= 10f + 0.001f,
                    $"A gold rain must last 8-10 seconds. duration={duration}.");
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumGoldMeteor),
                    "A new rain must not start while one is falling.");
                Assert(rain.SpawnedCount == 1 && rain.Meteors[0].TargetsPlayer &&
                       Vector2.Distance(rain.Meteors[0].Landing, target.transform.position) < 0.001f,
                    "The first lump must fall on the player at once.");

                // The player runs right, so the leading lump lands ahead of them by one warning of movement.
                Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
                targetBody.linearVelocity = new Vector2(1f, 0f);
                Assert(Vector2.Distance(targetBody.linearVelocity, new Vector2(1f, 0f)) < 0.001f,
                    "The test player's velocity could not be set.");

                // The boss is free again after the short cast; the rain goes on by itself.
                runtime.OnPatternStateChanged(BossActionState.Recovery, BossPatternExecution.SaemaeumGoldMeteor, 0.7f);
                Rect arena = GetPrivateField<Rect>(runtime, "arenaBounds");
                List<SaemaeumGoldMeteor> all = new(rain.Meteors);
                for (float now = 0.05f; now < duration + 0.5f; now += 0.05f)
                {
                    rain.Tick(now);
                    foreach (SaemaeumGoldMeteor meteor in rain.Meteors)
                        if (meteor != null && !all.Contains(meteor)) all.Add(meteor);
                }
                int expectedCount = 0;
                while (SaemaeumGoldRain.ShadowOffset(expectedCount, 5, 0.35f, 0.45f) < duration) expectedCount++;
                Assert(all.Count == expectedCount && rain.SpawnedCount == expectedCount,
                    $"Gold rain dropped {all.Count} lumps instead of {expectedCount}.");
                for (int index = 0; index < all.Count; index++)
                {
                    Assert(all[index].TargetsPlayer == (index % 3 == 0),
                        "Exactly one lump in three, starting with the first, must aim at the player.");
                    Assert(all[index].LeadsPlayer == (index % 3 == 1),
                        "The lump right after each aimed one must lead the player's movement.");
                    Assert(arena.Contains(all[index].Landing), "Every lump must land inside the arena.");
                    Assert(Mathf.Approximately(all[index].ImpactAt - SaemaeumGoldRain.ShadowOffset(index, 5, 0.35f, 0.45f),
                            0.9f), "Every lump must land 0.9s after its shadow appears.");
                }

                Assert(Vector2.Distance(all[1].Landing, (Vector2)target.transform.position + new Vector2(0.9f, 0f)) < 0.001f,
                    $"The leading lump must land where the player will be at impact. landing={all[1].Landing}.");
                targetBody.linearVelocity = Vector2.zero;

                // The aimed lump hurts the player standing on its mark.
                Health targetHealth = target.GetComponent<Health>();
                float before = targetHealth.CurrentHealth;
                SaemaeumGoldMeteor aimed = all[0];
                aimed.Tick(aimed.ImpactAt);
                Assert(aimed == null && Mathf.Approximately(targetHealth.CurrentHealth,
                        before - HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)),
                    "A gold lump must deal light damage to the player on its mark and disappear.");

                // A lump breaks the pillar it lands on.
                SaemaeumGoldMeteor breaker = all.First(item => item != null);
                SaemaeumGoldPillar pillar = SaemaeumGoldPillar.Create(breaker.Landing, 0f, 0f, 0.6f,
                    EnemyDamageTier.Light, 7f, 0.2f, 5, root, null, 0);
                pillar.Tick(0f);
                Assert(pillar.HasEmerged && pillar.Obstacle != null, "The test pillar did not emerge.");
                breaker.Tick(breaker.ImpactAt);
                Assert(pillar == null, "A falling gold lump must break the pillar it lands on.");

                foreach (SaemaeumGoldMeteor meteor in all.Where(item => item != null).ToArray())
                    meteor.Tick(meteor.ImpactAt);
                rain.Tick(duration + 1f);
                Assert(rain == null || rain.IsFinished, "The rain must end once its lumps have landed.");
                float ready = runtime.MeteorReadyAt;
                Assert(Mathf.Approximately(ready, duration + 0.9f + 8f),
                    "The next rain must wait 8s after the last lump lands.");
                runtime.TickPattern(BossActionState.Recovery, BossPatternExecution.SaemaeumGoldMeteor, ready - 0.01f);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumGoldMeteor),
                    "Gold rain came back before its cooldown.");
                runtime.TickPattern(BossActionState.Recovery, BossPatternExecution.SaemaeumGoldMeteor, ready + 0.01f);
                Assert(runtime.CanSelect(BossPatternExecution.SaemaeumGoldMeteor),
                    "Gold rain must be available again after its cooldown.");

                // A rain stops when its boss is gone.
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumGoldMeteor, ready + 1f);
                runtime.TryExecute(BossPatternExecution.SaemaeumGoldMeteor);
                SaemaeumGoldRain second = runtime.GoldRain;
                boss.CancelCombat();
                Assert(second == null && UnityEngine.Object.FindObjectsByType<SaemaeumGoldMeteor>(
                        FindObjectsSortMode.None).Length == 0,
                    "Leaving the boss fight must stop the rain and remove its lumps.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 2026-11-21: phase two rings the player with pillars whose gaps the player's feet cannot pass, the pillars
        // crumble on their own later, and only the jump sequence may follow the cage.
        private static void ValidateGoldCage()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                Assert(runtime.CagePillars == 14 && Mathf.Approximately(runtime.CageRadius, 2.4f) &&
                       Mathf.Approximately(runtime.CageWarning, 0.8f) &&
                       Mathf.Approximately(runtime.CageLifetime, 6f),
                    "The gold cage must ring the player with 14 pillars 2.4 away, warn 0.8s and last 6s.");
                float gap = 2f * Mathf.PI * runtime.CageRadius / runtime.CagePillars -
                            SaemaeumGoldPillar.BodyRadius * 2f;
                Assert(gap < TrickalFanGame.Player.PlayerFeet.Radius * 2f,
                    $"The gold cage gaps ({gap}) must be narrower than the player's feet.");

                target.transform.position = new Vector2(3.7f, 0.5f);
                runtime.BeginCombat(target.transform, 23, 0f);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumGoldCage),
                    "The gold cage must not be used in phase one.");
                boss.SetPhase(2);
                Assert(runtime.CanSelect(BossPatternExecution.SaemaeumGoldCage),
                    "The gold cage must be available in phase two.");
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumGoldCage, 0.6f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumGoldCage), "The gold cage did not execute.");
                List<SaemaeumGoldPillar> cage = runtime.Pillars.ToList();
                Assert(cage.Count == 14, $"An open cage must raise all 14 pillars, found {cage.Count}.");
                foreach (SaemaeumGoldPillar pillar in cage)
                    Assert(Mathf.Abs(Vector2.Distance(pillar.transform.position, target.transform.position) - 2.4f) <
                           0.001f && pillar.EmergeAt >= 0.8f - 0.001f && pillar.EmergeAt <= 0.8f + 14 * 0.02f &&
                           Mathf.Approximately(pillar.Lifetime, 6f),
                        "Cage pillars must stand on the ring around the player and burst after the warning.");
                AssertPillarsApart(cage, "the gold cage");

                Assert(runtime.JumpQueued && runtime.CanSelect(BossPatternExecution.SaemaeumJumpSequence) &&
                       !runtime.CanSelect(BossPatternExecution.SaemaeumApproachWalk) &&
                       !runtime.CanSelect(BossPatternExecution.SaemaeumPillarWave) &&
                       !runtime.CanSelect(BossPatternExecution.SaemaeumGoldCage),
                    "Only the jump sequence may follow the gold cage.");

                SaemaeumGoldPillar crumbling = cage[0];
                crumbling.Tick(crumbling.EmergeAt);
                Assert(crumbling.HasEmerged && crumbling.Obstacle != null, "The cage pillar did not burst out.");
                crumbling.Tick(crumbling.EmergeAt + 5.9f);
                Assert(crumbling != null && !crumbling.Obstacle.IsBroken, "A cage pillar crumbled too early.");
                crumbling.Tick(crumbling.EmergeAt + 6f);
                Assert(crumbling == null, "A cage pillar must crumble 6s after it bursts out.");

                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 5f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence), "The follow-up jump failed.");
                Assert(!runtime.JumpQueued && runtime.CanSelect(BossPatternExecution.SaemaeumApproachWalk),
                    "The other patterns must return once the jump starts.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 2026-11-21: the boss stands still and rows of pillars sweep out, each mark appearing as the row reaches it.
        private static void ValidatePillarWaves()
        {
            foreach (SaemaeumPillarWaveShape shape in new[]
                         { SaemaeumPillarWaveShape.Line, SaemaeumPillarWaveShape.Grid, SaemaeumPillarWaveShape.Fan })
            {
                GameObject root = CreateRuntimeBoss(out BossController boss,
                    out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
                try
                {
                    Assert(Mathf.Approximately(runtime.WaveSpacing, 1.2f) &&
                           Mathf.Approximately(runtime.WaveSpeed, 12f) &&
                           Mathf.Approximately(runtime.WaveWarning, 0.55f) &&
                           Mathf.Approximately(runtime.WaveLifetime, 5f) &&
                           Mathf.Approximately(runtime.WaveGridOffset, 1.8f) &&
                           Mathf.Approximately(runtime.WaveGridHalfLength, 4.2f) &&
                           Mathf.Approximately(runtime.WaveFanDegrees, 30f),
                        "Pillar waves must keep their 1.2 spacing, 12/s sweep, 0.55s warning and 5s lifetime.");
                    Assert(1.2f - SaemaeumGoldPillar.BodyRadius * 2f < TrickalFanGame.Player.PlayerFeet.Radius * 2f,
                        "Pillar wave rows must be too tight for the player's feet.");
                    if (shape == SaemaeumPillarWaveShape.Grid) target.transform.position = new Vector2(3f, 0f);
                    runtime.BeginCombat(target.transform, 31, 0f);
                    Vector2 player = target.transform.position;
                    runtime.ForceNextWaveShape(shape);
                    runtime.OnPatternStateChanged(BossActionState.Telegraph,
                        BossPatternExecution.SaemaeumPillarWave, 0.6f);
                    Assert(runtime.JumpCycleFrameIndex == 1, "The pillar wave must crouch during its warning.");
                    runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumPillarWave, 1.4f);
                    Assert(runtime.TryExecute(BossPatternExecution.SaemaeumPillarWave), "The pillar wave did not run.");
                    Assert(runtime.LastWaveShape == shape, $"The forced {shape} wave was not used.");
                    List<SaemaeumGoldPillar> wave = runtime.Pillars.ToList();
                    // Rows from the boss start just outside its body, where the nearest pillar stands.
                    float rowStart = wave.Count > 0 ? wave.Min(pillar => ((Vector2)pillar.transform.position).magnitude) : 0f;
                    Rect arena = GetPrivateField<Rect>(runtime, "arenaBounds");
                    Assert(wave.Count >= (shape == SaemaeumPillarWaveShape.Line ? 4 : 12),
                        $"The {shape} wave raised only {wave.Count} pillars.");
                    AssertPillarsApart(wave, $"the {shape} wave");
                    foreach (SaemaeumGoldPillar pillar in wave)
                    {
                        Vector2 spot = pillar.transform.position;
                        Assert(arena.Contains(spot) && Mathf.Approximately(pillar.EmergeAt - pillar.MarkShownAt, 0.55f) &&
                               Mathf.Approximately(pillar.Lifetime, 5f),
                            $"A {shape} wave pillar must stand in the arena and burst 0.55s after its mark.");
                        if (shape == SaemaeumPillarWaveShape.Grid)
                        {
                            Assert(Mathf.Abs(Mathf.Abs(spot.x - player.x) - 1.8f) < 0.001f ||
                                   Mathf.Abs(Mathf.Abs(spot.y - player.y) - 1.8f) < 0.001f,
                                $"A # wave pillar at {spot} is off its four rows.");
                        }
                        else
                        {
                            float turn = Vector2.SignedAngle(player, spot);
                            bool onRow = Mathf.Abs(turn) < 0.5f || (shape == SaemaeumPillarWaveShape.Fan &&
                                Mathf.Abs(Mathf.Abs(turn) - 30f) < 0.5f);
                            Assert(onRow, $"A {shape} wave pillar at {spot} is off its row (turn {turn}).");
                            Assert(Mathf.Abs(pillar.MarkShownAt - (spot.magnitude - rowStart) / 12f) < 0.01f,
                                "Row marks must appear as the sweep reaches them, nearest first.");
                        }
                    }
                    if (shape == SaemaeumPillarWaveShape.Fan)
                        foreach (float row in new[] { -30f, 0f, 30f })
                            Assert(wave.Any(pillar => Mathf.Abs(Vector2.SignedAngle(player,
                                    pillar.transform.position) - row) < 0.5f),
                                $"The fan wave is missing its {row} degree row.");

                    SaemaeumGoldPillar last = wave.OrderBy(pillar => pillar.MarkShownAt).Last();
                    Assert(last.MarkShownAt > 0.1f, "The wave must sweep, not mark every spot at once.");
                    last.Tick(0f);
                    Assert(last.Mark != null && !last.Mark.enabled, "A far mark must stay hidden until the row reaches it.");
                    last.Tick(last.MarkShownAt);
                    Assert(last.Mark.enabled, "A mark must show when the row reaches it.");
                }
                finally
                {
                    boss.CancelCombat();
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertPillarsApart(IReadOnlyList<SaemaeumGoldPillar> pillars, string context)
        {
            for (int a = 0; a < pillars.Count; a++)
            for (int b = a + 1; b < pillars.Count; b++)
                Assert(Vector2.Distance(pillars[a].transform.position, pillars[b].transform.position) >=
                       SaemaeumVaultBossPatternRuntime.PillarSpacing - 0.002f,
                    $"Gold pillars must not overlap ({context}).");
        }

        private static List<BossProjectile> FindBossProjectiles() =>
            UnityEngine.Object.FindObjectsByType<BossProjectile>(FindObjectsSortMode.None).ToList();

        private static float MinimumRingAngle(IEnumerable<BossProjectile> ring, Vector2 center) => ring
            .Select(shot => Mathf.Repeat(Vector2.SignedAngle(Vector2.right,
                (Vector2)shot.transform.position - center), 360f))
            .Min();

        private static void ValidateUnlimitedPhaseOneHealingAndPhaseTwo()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out Health health, out GameObject target, 1000f);
            try
            {
                boss.ConfigureHud("새마음금고", 1);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "A full-health boss must not select treasure healing.");
                health.TakeDamage(600f);
                runtime.BeginCombat(target.transform, 19, 0f);
                float before = health.CurrentHealth;
                ExecuteFullHeal(runtime, 0f);
                Assert(Mathf.Approximately(health.CurrentHealth, before + runtime.HealPerUse),
                    "One treasure-eating pattern must heal exactly 138 health.");
                ExecuteFullHeal(runtime, 2f);
                ExecuteFullHeal(runtime, 4f);
                Assert(runtime.HealUses == 3 &&
                       Mathf.Approximately(health.CurrentHealth, before + runtime.HealPerUse * 3f) &&
                       runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "Boss-2 phase-one healing must remain selectable without a use-count limit.");

                health.Heal(health.MaxHealth);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "Healing back to full must remove treasure healing from the candidates.");
                health.TakeDamage(1f);
                Assert(runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "Taking damage again must make phase-one healing available again.");

                boss.ConfigureHud("새마음금고", 2);
                boss.SetPhase(1);
                float phaseOneSpeed = runtime.CurrentApproachSpeed;
                boss.SetPhase(2);
                Assert(!runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "Phase two must permanently exclude treasure healing.");
                Assert(runtime.CurrentApproachSpeed > phaseOneSpeed,
                    "Phase two must increase Saemaeum Vault's approach speed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSlimeScatter()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                target.transform.position = new Vector2(1.5f, -1f);
                runtime.ConfigureSlime(1, 4, 2, 5, 1.2f, 0.5f, 0.55f, 3, 6);
                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.SaemaeumSlimeScatter, 0.8f);
                Assert(runtime.JumpCycleFrameIndex == 1, "Slime scatter must gather low during its warning.");
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumSlimeScatter, 1.4f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumSlimeScatter),
                    "Slime scatter did not execute.");
                List<float> radii = runtime.SlimeRadiiThisPhase();
                Assert(runtime.SlimeBlobs.Count == 5, "Phase one must spit one large and four small lumps.");
                Sprite jelly = Resources.Load<Sprite>("CombatEffects/saemaeum-jelly-projectile");
                Assert(jelly != null && runtime.SlimeBlobs.All(blob =>
                    blob.GetComponentInChildren<SpriteRenderer>().sprite == jelly),
                    "Every airborne slime scatter projectile must use the jelly sprite.");
                Assert(Vector2.Distance(runtime.SlimeBlobs[0].Destination, target.transform.position) < 0.001f,
                    "The first slime lump must land where the player stands.");
                for (int a = 0; a < runtime.SlimeBlobs.Count; a++)
                {
                    Assert(runtime.SlimeBlobs[a].Mark != null, "Every slime lump must mark its landing spot.");
                    for (int b = a + 1; b < runtime.SlimeBlobs.Count; b++)
                        Assert(Vector2.Distance(runtime.SlimeBlobs[a].Destination,
                                runtime.SlimeBlobs[b].Destination) >=
                               Mathf.Max(1f, radii[a] + radii[b]) - 0.001f,
                            "Slime puddles in the open must not overlap each other.");
                }
                Assert(runtime.SlimePuddles.Count == 0, "Puddles must not appear before the lumps land.");

                float landed = runtime.SlimeFlightDuration + 4 * runtime.SlimeLaunchStagger + 0.01f;
                foreach (BossSlimeBlob blob in runtime.SlimeBlobs.ToArray()) blob.Tick(landed);
                Assert(runtime.SlimePuddles.Count == 5 && BossSlimePuddle.Live.Count == 5,
                    $"Every landed lump must leave one slime puddle. own={runtime.SlimePuddles.Count}, live={BossSlimePuddle.Live.Count}.");
                Assert(Mathf.Approximately(runtime.SlimePuddles[0].Radius, 1.2f) &&
                       runtime.SlimePuddles.Skip(1).All(item => Mathf.Approximately(item.Radius, 0.5f)),
                    "The large puddle must land first, on the player, followed by four obstacle-sized ones.");
                Assert(GameObject.Find(BossSlimeBlob.MarkName) == null,
                    "Landing marks must disappear once the lumps land.");

                BossSlimePuddle under = runtime.SlimePuddles[0];
                Assert(under.Contains(target.transform.position), "The first puddle must cover the player.");
                under.Tick(landed);
                EnvironmentSlow slow = target.GetComponent<EnvironmentSlow>();
                Assert(slow != null && Mathf.Approximately(slow.MultiplierAt(landed), 0.55f),
                    "Standing in a slime puddle must slow the player to 0.55x.");
                Assert(Mathf.Approximately(slow.MultiplierAt(landed + BossSlimePuddle.SlowLinger + 0.01f), 1f),
                    "The slime slow must end shortly after the player leaves the puddle.");

                Assert(under.RegisterPlayerHit() && under.RegisterPlayerHit() && !under.IsPopped,
                    "A slime puddle must survive its first two hits.");
                Assert(under.RegisterPlayerHit() && under == null && BossSlimePuddle.Live.Count == 4,
                    "A slime puddle must pop on its third hit.");
                BossSlimePuddle other = runtime.SlimePuddles.First(item => item != null);
                Assert(BossSlimePuddle.HitInCircle(other.transform.position, 0.1f) >= 1 && other.HitsTaken == 1,
                    "Player area attacks must count one hit on a puddle they touch.");

                // A second wave past the cap of six dries up the oldest puddles first.
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumSlimeScatter, 3f);
                runtime.TryExecute(BossPatternExecution.SaemaeumSlimeScatter);
                foreach (BossSlimeBlob blob in runtime.SlimeBlobs.Where(item => item != null).ToArray())
                    blob.Tick(landed + 1f);
                Assert(BossSlimePuddle.Live.Count == 6, $"Slime puddles must stop at the cap of six. " +
                    $"live={BossSlimePuddle.Live.Count}.");

                boss.SetPhase(2);
                Assert(runtime.LargeSlimeCountThisPhase == 2 && runtime.SmallSlimeCountThisPhase == 5,
                    "Phase two must spit two large and five small slime lumps.");
                boss.CancelCombat();
                Assert(BossSlimePuddle.Live.Count == 0 && GameObject.Find(BossSlimeBlob.ObjectName) == null,
                    "Leaving the boss fight must remove every slime lump and puddle.");
            }
            finally
            {
                foreach (BossSlimePuddle puddle in BossSlimePuddle.Live.ToArray())
                    if (puddle != null) UnityEngine.Object.DestroyImmediate(puddle.gameObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateLandingDamageAndKnockback()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                Health targetHealth = target.GetComponent<Health>();
                KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
                float before = targetHealth.CurrentHealth;
                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                target.transform.position += Vector3.right * 2.8f;
                float firstLandingTime = GetPrivateField<float>(runtime, "phaseOneJumpDuration") + 0.001f;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, firstLandingTime);
                Assert(Mathf.Approximately(targetHealth.CurrentHealth, before - HealthUnits.GetEnemyDamageUnits(runtime.LandingDamageTier, 1)),
                    "Boss-2 landing did not damage the player inside the enlarged impact radius.");
                Assert(receiver.IsKnockedBack &&
                       Mathf.Approximately(receiver.CurrentVelocity.magnitude, runtime.LandingKnockbackSpeed),
                    "Boss-2 landing did not apply Buseureogi-style knockback.");
            }
            finally
            {
                // The landing leaves slime puddles owned by the boss.
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateWallAndCornerLandings()
        {
            foreach (bool testRoom in new[] { true, false })
            {
                GameObject arena = new("Translated landing verification arena");
                arena.transform.position = new Vector3(20f, 30f, 0f);
                if (testRoom) arena.AddComponent<TrickalFanGame.Debugging.ItemTestRoomController>();
                else arena.AddComponent<RoomNode>();
                Vector2 half = testRoom ? new Vector2(8f, 6f) : new Vector2(8f, 6.75f);
                try
                {
                    foreach (string side in new[] { "Left", "Right", "Top", "Bottom" })
                    {
                        GameObject wall = new(side + " Wall");
                        wall.transform.SetParent(arena.transform, false);
                        bool vertical = side == "Left" || side == "Right";
                        wall.transform.localPosition = vertical
                            ? new Vector3(side == "Left" ? -half.x : half.x, 0f)
                            : new Vector3(0f, side == "Bottom" ? -half.y : half.y);
                        wall.AddComponent<BoxCollider2D>().size = vertical
                            ? new Vector2(0.4f, half.y * 2f) : new Vector2(half.x * 2f, 0.4f);
                    }
                    foreach (Vector2 edge in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                                 new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), Vector2.one })
                    {
                        GameObject root = CreateRuntimeBoss(out _, out SaemaeumVaultBossPatternRuntime runtime,
                            out _, out GameObject target);
                        try
                        {
                            root.transform.SetParent(arena.transform, false);
                            root.transform.localScale = Vector3.one * Week15Boss1Setup.BossScale;
                            CircleCollider2D bodyCollider = root.AddComponent<CircleCollider2D>();
                            bodyCollider.radius = 0.38f;
                            Vector2 interiorHalf = half - Vector2.one * 0.2f;
                            target.transform.position = (Vector2)arena.transform.position +
                                Vector2.Scale(edge, interiorHalf - Vector2.one * 0.5f);
                            root.transform.position = target.transform.position - (Vector3)(edge.normalized * 3f);
                            Physics2D.SyncTransforms();
                            runtime.BeginCombat(target.transform, 88, 0f);
                            runtime.OnPatternStateChanged(BossActionState.Active,
                                BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                            EnemyFeet.GroundCircle(root, out Vector2 feetNow, out float feetRadius);
                            runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                            Vector2 destination = GetPrivateField<Vector2>(runtime, "jumpDestination");
                            Vector2 local = destination + (feetNow - (Vector2)root.transform.position) -
                                            (Vector2)arena.transform.position;
                            Assert(Mathf.Abs(local.x) + feetRadius <= interiorHalf.x + 0.001f &&
                                   Mathf.Abs(local.y) + feetRadius <= interiorHalf.y + 0.001f,
                                "Landing feet overlap a wall.");
                            // Boss-Pattern-0: by its feet it reaches the wall like other enemies, closer than its
                            // whole body circle would allow.
                            float bodyRadius = bodyCollider.radius * root.transform.lossyScale.x;
                            if (Mathf.Abs(edge.x) > 0.5f)
                                Assert(Mathf.Abs(local.x) + bodyRadius > interiorHalf.x - 0.001f,
                                    $"A wall landing must come closer than the body circle. edge={edge}.");
                            float before = target.GetComponent<Health>().CurrentHealth;
                            runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                                GetPrivateField<float>(runtime, "phaseOneJumpDuration") + 0.001f);
                            Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth,
                                    before - HealthUnits.GetEnemyDamageUnits(runtime.LandingDamageTier, 1)),
                                $"Wall/corner landing missed: testRoom={testRoom}, edge={edge}.");
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(target);
                            UnityEngine.Object.DestroyImmediate(root);
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(arena); }
            }
        }

        // 2026-11-19: near walls the landing shadow sat apart from where the boss landed. Inside the walls a hedge
        // strip is still out of reach, so the arena is cut to the room's movement bounds, and the shadow is drawn at
        // the boss's own ground shadow rather than its root.
        private static void ValidateStandingAreaAndLandingShadow()
        {
            GameObject arena = new("Standing area verification arena");
            arena.transform.position = new Vector3(40f, -13f, 0f);
            RoomProfile profile = ScriptableObject.CreateInstance<RoomProfile>();
            profile.Configure("verification-basic", new Vector2(16f, 9f), new Rect(-6.5f, -3.5f, 13f, 7f),
                new Rect(-5f, -2.5f, 10f, 5f), 5f, new Rect(-8f, -4.5f, 16f, 9f), null);
            RoomNode node = arena.AddComponent<RoomNode>();
            node.ApplyRoomProfile(profile);
            Vector2 half = new(8f, 4.5f);
            try
            {
                foreach (string side in new[] { "Left", "Right", "Top", "Bottom" })
                {
                    GameObject wall = new(side + " Wall");
                    wall.transform.SetParent(arena.transform, false);
                    bool vertical = side == "Left" || side == "Right";
                    wall.transform.localPosition = vertical
                        ? new Vector3(side == "Left" ? -half.x + 0.25f : half.x - 0.25f, 0f)
                        : new Vector3(0f, side == "Bottom" ? -half.y + 0.25f : half.y - 0.25f);
                    wall.AddComponent<BoxCollider2D>().size = vertical
                        ? new Vector2(0.5f, half.y * 2f) : new Vector2(half.x * 2f, 0.5f);
                }
                Physics2D.SyncTransforms();
                foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, 1f), new Vector2(1f, -1f) })
                {
                    GameObject root = CreateRuntimeBoss(out _, out SaemaeumVaultBossPatternRuntime runtime,
                        out _, out GameObject target);
                    try
                    {
                        root.transform.SetParent(arena.transform, false);
                        root.transform.localScale = Vector3.one * Week15Boss1Setup.BossScale;
                        CircleCollider2D body = root.AddComponent<CircleCollider2D>();
                        body.radius = 0.38f;
                        // The player presses into the hedge corner, at the edge of where it can stand.
                        target.transform.position = arena.transform.position + (Vector3)Vector2.Scale(corner,
                            new Vector2(6.5f, 3.5f));
                        root.transform.position = target.transform.position - (Vector3)(corner.normalized * 4f);
                        Physics2D.SyncTransforms();
                        runtime.BeginCombat(target.transform, 88, 0f);
                        Rect bounds = GetPrivateField<Rect>(runtime, "arenaBounds");
                        Vector2 center = arena.transform.position;
                        Assert(Mathf.Abs(bounds.xMin - (center.x - 6.5f)) < 0.01f &&
                               Mathf.Abs(bounds.xMax - (center.x + 6.5f)) < 0.01f &&
                               Mathf.Abs(bounds.yMin - (center.y - 3.5f)) < 0.01f &&
                               Mathf.Abs(bounds.yMax - (center.y + 3.5f)) < 0.01f,
                            $"The jump arena must be the room's standing area, not the hedge strip. arena={bounds}.");
                        // Measured on the ground: a jump turns the colliders into triggers.
                        EnemyFeet.GroundCircle(root, out Vector2 feetNow, out float feetRadius);
                        Assert(feetRadius > EnemyFeet.Radius, "The boss must stand on boss-sized feet.");
                        runtime.OnPatternStateChanged(BossActionState.Active,
                            BossPatternExecution.SaemaeumJumpSequence, 6f);
                        runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                        Vector2 destination = GetPrivateField<Vector2>(runtime, "jumpDestination");
                        Vector2 shadow = runtime.LandingTelegraphPosition;
                        float radius = body.radius * Week15Boss1Setup.BossScale;
                        Vector2 feet = destination + (feetNow - (Vector2)root.transform.position);
                        Assert(bounds.Contains(feet + Vector2.left * (feetRadius - 0.001f)) &&
                               bounds.Contains(feet + Vector2.right * (feetRadius - 0.001f)) &&
                               bounds.Contains(feet + Vector2.down * (feetRadius - 0.001f)) &&
                               bounds.Contains(feet + Vector2.up * (feetRadius - 0.001f)),
                            $"A corner landing must keep the feet on the standing floor. corner={corner}, " +
                            $"bounds={bounds}, destination={destination}, feet={feet}, feetRadius={feetRadius}, " +
                            $"root={root.transform.position}, feetNow={feetNow}, " +
                            $"rootArea={EnemyFeet.StandingRootArea(root, bounds)}.");
                        Assert(Vector2.Distance(feet, target.transform.position) < radius + 0.5f,
                            $"A corner landing must reach the player pressed into the corner. corner={corner}.");
                        runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                            runtime.PhaseOneJumpDuration + 0.001f);
                        Vector2 landed = root.GetComponent<Rigidbody2D>().position;
                        Vector2 landedShadow = landed + runtime.GroundShadowOffset();
                        Assert(Vector2.Distance(landedShadow, shadow) < 0.01f &&
                               Vector2.Distance(landed, destination) < 0.01f,
                            $"The landing shadow must be where the boss's own shadow lands. corner={corner}, " +
                            $"telegraph={shadow}, landed={landedShadow}.");
                        Assert(shadow.y < destination.y - radius * 0.5f,
                            "The landing shadow must sit at the boss's feet, below its root.");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(target);
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arena);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static void ValidateGroundedContact()
        {
            GameObject root = CreateRuntimeBoss(out _, out _, out _, out GameObject target);
            try
            {
                Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                Assert(body.bodyType == RigidbodyType2D.Kinematic && body.useFullKinematicContacts,
                    "Saemaeum must resist player pushing while still reporting ground contacts.");
                target.AddComponent<TrickalFanGame.Player.PlayerMovement>();
                Health health = target.GetComponent<Health>();
                float before = health.CurrentHealth;
                ContactDamage contact = root.GetComponent<ContactDamage>();
                Assert(contact != null && contact.TryApplyDamage(health, 0f) &&
                       contact.DamageTier == EnemyDamageTier.Light &&
                       Mathf.Approximately(health.CurrentHealth,
                           before - HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)) &&
                       HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 2) == HealthUnits.UnitsPerHeart,
                    "Saemaeum ground contact must deal one heart on its floor.");
                Assert(!contact.TryApplyDamage(health, 0.79f),
                    "Continuous ground contact must respect its 0.8 second cooldown.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 2026-11-20: the drawn phase-two warning keeps the vault invulnerable, and each landing sends out a
        // visual-only ring that ends just past the landing radius and is cleared with the boss.
        private static void ValidatePhaseTwoWarningAndLandingShockwave()
        {
            BossController prefabBoss = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath)
                .GetComponent<BossController>();
            Assert(Mathf.Approximately(prefabBoss.PhaseTransitionDuration, 1.6f),
                "Boss-2 phase-two warning must last 1.6s.");
            for (int i = 0; i < SaemaeumVaultPhaseTwoArtwork.FrameCount; i++)
            {
                Sprite frame = SaemaeumVaultPhaseTwoArtwork.Frame(i);
                Assert(frame != null && frame.rect.size == new Vector2(1024f, 1024f) &&
                       Mathf.Approximately(frame.pixelsPerUnit, 400f) && frame.pivot == new Vector2(512f, 512f),
                    "Phase-two warning poses must share the movement frames' 400 PPU and centered pivot.");
            }
            Assert(SaemaeumVaultPhaseTwoArtwork.FrameAt(0f) == 0 && SaemaeumVaultPhaseTwoArtwork.FrameAt(0.2f) == 1 &&
                   SaemaeumVaultPhaseTwoArtwork.FrameAt(0.5f) == 2 && SaemaeumVaultPhaseTwoArtwork.FrameAt(0.9f) == 3,
                "Phase-two warning must play startled, annoyed, roaring, seething in order.");

            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out Health health, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 31, 0f);
                runtime.OnPatternStateChanged(BossActionState.PhaseTransition, BossPatternExecution.SignalOnly, 1.6f);
                Assert(health.IsInvulnerable, "The vault must be invulnerable during its phase-two warning.");
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.SaemaeumJumpSequence, 2.4f);
                Assert(!health.IsInvulnerable, "The vault must take damage again once phase two starts.");

                // No treasure ring here, so the boss owns only the landing ring and the landing's slime puddles.
                runtime.ConfigureLandingBurst(0, 0, 4.5f, 5f, EnemyDamageTier.Light, 0.2f);
                InvokeWith(runtime, "ResolveLanding", new Vector2(-3f, 2f), 0);
                SaemaeumLandingShockwave wave = runtime.LastShockwave;
                Assert(wave != null && wave.GetComponentInChildren<Collider2D>() == null &&
                       (Vector2)wave.transform.position == new Vector2(-3f, 2f) + runtime.GroundShadowOffset() &&
                       Mathf.Approximately(wave.Radius, runtime.LandingRadius) &&
                       boss.OwnedObjectCount == 1 + runtime.SlimePuddles.Count,
                    "A landing must spread a boss-owned, collision-free ring from the landed shadow.");
                wave.RenderAt(0.02f);
                float early = wave.Front.transform.localScale.x;
                wave.RenderAt(SaemaeumLandingShockwave.Duration * 0.99f);
                Assert(wave.Front.transform.localScale.x > early * 4f &&
                       wave.Front.transform.localScale.x <= runtime.LandingRadius * 2f * SaemaeumLandingShockwave.FrontReach + 0.001f &&
                       Mathf.Approximately(wave.Front.transform.localScale.y, wave.Front.transform.localScale.x * EnemyLobProjectile.MarkAspect) &&
                       wave.Front.color.a < 0.05f && wave.Front.sortingOrder < 0,
                    "The ring must burst out along the floor to just past the landing radius and fade under bodies.");
                boss.CancelCombat();
                Assert(boss.OwnedObjectCount == 0 && runtime.LastShockwave == null,
                    "Room cleanup must remove a running landing ring.");
            }
            finally
            {
                if (runtime.LastShockwave != null) UnityEngine.Object.DestroyImmediate(runtime.LastShockwave.gameObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateLandingTelegraphCleanup()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                Assert(boss.OwnedObjectCount == 1, "Boss-2 did not register its landing telegraph.");
                boss.CancelCombat();
                Assert(boss.OwnedObjectCount == 0,
                    "Boss-2 room deactivation must remove its pending landing telegraph.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateFinalJumpCompletesBeforeRecovery()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
                boss.ConfigurePhaseTwo(0f, 0.88f);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("final-jump-continuity",
                        BossPatternExecution.SaemaeumJumpSequence, 0f, 3.8f, 1.15f, 0f),
                });
                runtime.ConfigureJumps(6, 6, 6, 6, 5.5f, 2.875f, EnemyDamageTier.Medium, 7f, 0.2f,
                    0.76f, 0.72f, 0.20f, 0.15f);
                boss.SetPhase(2);
                boss.BeginCombat(target.transform, 99, 0f);
                float activeStart = boss.StateEndsAt;
                boss.TickBehavior(activeStart);

                // Six phase-two jumps with a rest between each pair, but none after the final landing.
                float expectedEnd = activeStart + 6f * 0.72f + 5f * 0.15f;
                Assert(boss.State == BossActionState.Active &&
                       Mathf.Approximately(boss.StateEndsAt, expectedEnd),
                    $"The active state must end at the actual final landing instead of the old fixed duration. " +
                    $"state={boss.State}, phase={boss.CurrentPhase}, jumps={runtime.PlannedJumpCount}, " +
                    $"actualEnd={boss.StateEndsAt}, expectedEnd={expectedEnd}.");

                boss.TickBehavior(expectedEnd - 0.001f);
                Assert(boss.State == BossActionState.Active && GetPrivateField<bool>(runtime, "jumping"),
                    "The boss left its active state before the final jump completed.");

                boss.TickBehavior(expectedEnd + 0.001f);
                Transform visual = root.transform.Find("Visual");
                Assert(boss.State == BossActionState.Recovery &&
                       !GetPrivateField<bool>(runtime, "jumping") &&
                       GetPrivateField<int>(runtime, "resolvedJumpCount") == 6 &&
                       !collider.isTrigger && visual.localPosition == Vector3.zero,
                    "The final jump must land and restore collision before recovery begins.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateJumpCurveFeel()
        {
            // Launch fast, hang near the apex, then fall faster than the rise into the ground hit.
            float Height(float flight) => SaemaeumVaultBossPatternRuntime.JumpHeightFraction(flight);
            float Travel(float flight) => SaemaeumVaultBossPatternRuntime.JumpTravelFraction(flight);
            Assert(Height(0f) == 0f && Height(1f) == 0f && Mathf.Approximately(Height(0.6f), 1f),
                "Boss-2 jump must leave and return to the ground with its apex at 60% of the flight.");
            Assert(Height(0.1f) > 0.3f, "Boss-2 jump must launch quickly instead of easing off the ground.");
            float launchRate = Height(0.05f) / 0.05f;
            float landingRate = Height(0.95f) / 0.05f;
            Assert(landingRate > launchRate * 1.3f,
                $"Boss-2 jump must slam down faster than it rises. launch={launchRate}, landing={landingRate}.");
            Assert(Travel(0f) == 0f && Mathf.Approximately(Travel(1f), 1f) && Travel(0.1f) > 0.12f &&
                   (1f - Travel(0.95f)) / 0.05f > 0.5f,
                "Boss-2 jump must keep ground momentum from takeoff into the landing.");
        }

        private static void ValidateJumpArcAndCollisionRestoration()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                Collider2D collider = root.AddComponent<CircleCollider2D>();
                Transform visual = root.transform.Find("Visual");
                target.transform.position = new Vector2(2f, 0f);
                float before = target.GetComponent<Health>().CurrentHealth;
                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                float duration = GetPrivateField<float>(runtime, "phaseOneJumpDuration");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, duration * 0.5f);
                Assert(collider.isTrigger, "Airborne boss must not physically push the player before landing.");
                Assert(visual.position.y - root.transform.position.y > 1.9f,
                    "Jump midpoint must visibly rise above the ground trajectory.");
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before),
                    "Airborne travel must not apply landing damage early.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, duration + 0.001f);
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before - HealthUnits.GetEnemyDamageUnits(runtime.LandingDamageTier, 1)),
                    "A nearby player must be hit at landing instead of being overshot by the maximum jump distance.");
                boss.CancelCombat();
                Assert(!collider.isTrigger && visual.localPosition == Vector3.zero,
                    "Cancelled jumps must restore solid collision and grounded visuals.");

                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                target.transform.position = new Vector2(-5f, -4f);
                float sequenceDuration = runtime.PlannedJumpCount * runtime.PhaseOneJumpDuration +
                    (runtime.PlannedJumpCount - 1) * runtime.PhaseOneJumpRest;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, sequenceDuration);
                float after = target.GetComponent<Health>().CurrentHealth;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, sequenceDuration);
                Assert(!collider.isTrigger && visual.localPosition == Vector3.zero &&
                       Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, after),
                    "Final landing must restore collision/height and never repeat its damage.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateFloorTwoRuntimeBinding()
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            GameObject expected = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath);
            Assert(assembler != null && expected != null,
                "Boss-2 requires the Game Scene assembler and Saemaeum Vault prefab.");
            Assert(assembler.ResolveBossPrefab(1) != null &&
                   assembler.ResolveBossPrefab(2) == expected &&
                   assembler.ResolveBossPrefab(3) != null,
                "The floor-specific boss roster must resolve Saemaeum Vault only on floor two.");

            const int seed = 2202;
            assembler.Progress.ResetProgress();
            Assert(assembler.Progress.TryInitializeRunSeed(seed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);
            Assert(assembler.TryLoadFloor(2, assembler.Graph.Player, out error), error);
            RoomPrefab bossRoom = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.Definition.RoomType == RoomType.Boss);
            BossController spawned = null;
            bossRoom.Controller.EnemySpawned += enemy => spawned = enemy?.GetComponent<BossController>();
            bossRoom.Controller.BeginCombat(assembler.Graph.Player.GetComponent<Health>());
            Assert(spawned != null && spawned.DisplayName == "새마음금고" &&
                   spawned.GetComponent<SaemaeumVaultBossPatternRuntime>() != null,
                "The generated floor-two boss room spawned Buseureogi instead of Saemaeum Vault.");
            Assert(Vector3.Distance(spawned.transform.position, bossRoom.Node.transform.position) < 0.001f,
                "The boss must spawn at the room center, independently of the enemy spawn markers.");
            Sprite expectedSprite = AssetDatabase.LoadAllAssetsAtPath(Week15Boss2Setup.BossSpritePath)
                .OfType<Sprite>().FirstOrDefault();
            Assert(expectedSprite != null && spawned.GetComponentInChildren<SpriteRenderer>()?.sprite == expectedSprite,
                "The generated floor-two boss did not use Boss_SaemaeumGeumgo.png.");
        }

        private static List<int> SampleJumpCounts(int seed, int count, bool phaseTwo)
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                boss.ConfigureHud("새마음금고", 2);
                if (phaseTwo) boss.SetPhase(2);
                runtime.BeginCombat(target.transform, seed, 0f);
                List<int> result = new();
                for (int index = 0; index < count; index++)
                {
                    runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                    result.Add(runtime.PlannedJumpCount);
                }
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ExecuteFullHeal(SaemaeumVaultBossPatternRuntime runtime, float start)
        {
            runtime.OnPatternStateChanged(BossActionState.Active,
                BossPatternExecution.SaemaeumTreasureHeal, start + 1.8f);
            runtime.TryExecute(BossPatternExecution.SaemaeumTreasureHeal);
            ExecuteFullHealTicks(runtime, start);
        }

        private static void ExecuteFullHealTicks(SaemaeumVaultBossPatternRuntime runtime, float start)
        {
            for (int index = 1; index <= 4; index++)
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumTreasureHeal, start + index * 0.449f);
        }

        private static GameObject CreateRuntimeBoss(out BossController boss,
            out SaemaeumVaultBossPatternRuntime runtime, out Health health, out GameObject target,
            float maximumHealth = 20f)
        {
            GameObject root = new("Saemaeum Vault Runtime Verification");
            health = root.AddComponent<Health>();
            SetPrivateField(health, "maxHealth", maximumHealth);
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            root.AddComponent<KnockbackReceiver>();
            GameObject visual = new("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<SpriteRenderer>();
            boss = root.AddComponent<BossController>();
            runtime = root.AddComponent<SaemaeumVaultBossPatternRuntime>();
            Invoke(health, "Awake");
            Invoke(runtime, "Awake");
            Invoke(boss, "Awake");
            boss.ConfigureHud("새마음금고", 2);
            boss.ConfigurePhaseTwo(0f, 0.88f);
            target = new GameObject("Moving Player Target");
            target.transform.position = new Vector2(4.8f, 0f);
            target.AddComponent<Health>();
            target.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            target.AddComponent<KnockbackReceiver>();
            Invoke(target.GetComponent<Health>(), "Awake");
            Invoke(target.GetComponent<KnockbackReceiver>(), "Awake");
            return root;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Missing verification field {target.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Missing verification field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static void InvokeWith(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, $"Missing verification hook {target.GetType().Name}.{methodName}.");
            method.Invoke(target, arguments);
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, $"Missing verification hook {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
