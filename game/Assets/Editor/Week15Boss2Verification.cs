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
            ValidatePrefabAndTallRoom();
            ValidateElasticReadabilityAndSeededJumps();
            ValidateApproachFanVolleys();
            ValidateUnlimitedPhaseOneHealingAndPhaseTwo();
            ValidateLandingDamageAndKnockback();
            ValidateGroundedContact();
            ValidateJumpArcAndCollisionRestoration();
            ValidateWallAndCornerLandings();
            ValidateLandingTelegraphCleanup();
            ValidateFloorTwoRuntimeBinding();
            Week15Boss1Verification.Verify();
            Debug.Log("Week 15 Boss-2 verification passed: Saemaeum Vault keeps its own sprite/prefab, " +
                      "fires five five-projectile fan volleys, repeats seeded 3-5 / 4-6 longer jumps, damages " +
                      "and knocks back at the locked landing point, heals 15 without a phase-one use cap, " +
                      "excludes healing in phase two, accelerates, and cleans landing telegraphs.");
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
            Assert(prefab.GetComponent<SpriteRenderer>() == null &&
                   prefab.GetComponentInChildren<SpriteRenderer>()?.transform != prefab.transform,
                "Boss-2 elastic animation must live on a visual child so its collider never stretches.");
            Assert(boss.DisplayName == "새마음금고" && boss.PhaseCount == 2 && boss.Patterns.Count == 3,
                "Boss-2 HUD identity or its three-pattern/two-phase contract is missing.");
            Assert(boss.Patterns.Select(pattern => pattern.Execution).SequenceEqual(new[]
                {
                    BossPatternExecution.SaemaeumApproachThrow,
                    BossPatternExecution.SaemaeumJumpSequence,
                    BossPatternExecution.SaemaeumTreasureHeal,
                }), "Boss-2 patterns are not bound in the authored order.");
            Assert(runtime.ProjectilesPerVolley == 5 && runtime.VolleysPerPattern == 5 &&
                   Mathf.Approximately(runtime.FanSpreadDegrees, 48f),
                "Boss-2 approach throw must use five 5-projectile fan volleys.");
            Assert(Mathf.Approximately(runtime.JumpDistance, 4.8f) &&
                   runtime.PhaseTwoMinimumJumps == 4 && Mathf.Approximately(runtime.HealPerUse, 15f),
                "Boss-2 jump distance or phase-one healing amount was not rebalanced.");

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
            float diameter = collider.radius * 2f * Week15Boss0Setup.BossVisualScale;
            Assert(diameter < Mathf.Min(profile.MovementBounds.width, profile.MovementBounds.height) * 0.25f,
                "Boss-2 collision silhouette is too large for the floor-two room.");
        }

        private static void ValidateElasticReadabilityAndSeededJumps()
        {
            List<int> first = SampleJumpCounts(31415, 12, false);
            List<int> repeat = SampleJumpCounts(31415, 12, false);
            List<int> secondPhase = SampleJumpCounts(31415, 12, true);
            Assert(first.SequenceEqual(repeat), "Boss-2 jump counts must repeat for the same content seed.");
            Assert(first.All(count => count >= 3 && count <= 5) && first.Distinct().Count() > 1,
                "Boss-2 phase one must produce seeded jump sequences varying from 3 to 5.");
            Assert(secondPhase.All(count => count >= 4 && count <= 6) && secondPhase.Contains(4),
                "Boss-2 phase two must increase the jump-count range to 4-6.");

            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph,
                    BossPatternExecution.SaemaeumJumpSequence, 0.95f);
                Vector3 rootScale = root.transform.localScale;
                Assert(runtime.CurrentVisualScale.x > runtime.CurrentVisualScale.y * 1.8f,
                    "Dangerous jump crouch must be substantially wider and lower than idle movement.");
                Assert(root.transform.localScale == rootScale,
                    "Jump crouch changed the root scale and therefore the collision silhouette.");
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 4.75f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                float flightSampleTime = 0.95f + 3.8f * (0.5f / runtime.PlannedJumpCount);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, flightSampleTime);
                Assert(runtime.CurrentVisualScale.y > runtime.CurrentVisualScale.x * 1.5f,
                    "Boss-2 flight must stretch vertically so it cannot be mistaken for ordinary movement.");
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

        private static void ValidateApproachFanVolleys()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out _, out GameObject target);
            try
            {
                runtime.ConfigureVolley(5, 5, 48f);
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 3f);
                runtime.TryExecute(BossPatternExecution.SaemaeumApproachThrow);
                Assert(boss.OwnedObjectCount == 5,
                    "Boss-2 must fire five projectiles in its first fan volley.");

                BossProjectile[] firstVolley = UnityEngine.Object.FindObjectsByType<BossProjectile>(
                    FindObjectsSortMode.None);
                Vector2[] velocities = firstVolley.Select(projectile =>
                        GetPrivateField<Vector2>(projectile, "velocity").normalized)
                    .ToArray();
                Assert(velocities.Length == 5 && velocities.Distinct().Count() == 5,
                    "Boss-2 first volley did not spread five projectiles into distinct fan directions.");
                float minimumAngle = velocities.Min(velocity => Vector2.SignedAngle(Vector2.right, velocity));
                float maximumAngle = velocities.Max(velocity => Vector2.SignedAngle(Vector2.right, velocity));
                Assert(maximumAngle - minimumAngle >= 47f,
                    "Boss-2 projectile fan is narrower than its configured spread.");

                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 0.55f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 1.1f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 1.65f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 2.2f);
                Assert(boss.OwnedObjectCount == 25,
                    "Boss-2 approach pattern must stop after five fan volleys of five projectiles.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumApproachThrow, 2.95f);
                Assert(boss.OwnedObjectCount == 25,
                    "Boss-2 fired more than five volleys in one approach pattern.");
            }
            finally
            {
                boss.CancelCombat();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateUnlimitedPhaseOneHealingAndPhaseTwo()
        {
            GameObject root = CreateRuntimeBoss(out BossController boss,
                out SaemaeumVaultBossPatternRuntime runtime, out Health health, out GameObject target, 100f);
            try
            {
                boss.ConfigureHud("새마음금고", 1);
                health.TakeDamage(60f);
                runtime.BeginCombat(target.transform, 19, 0f);
                float before = health.CurrentHealth;
                ExecuteFullHeal(runtime, 0f);
                Assert(Mathf.Approximately(health.CurrentHealth, before + runtime.HealPerUse),
                    "One treasure-eating pattern must heal exactly 15 health.");
                ExecuteFullHeal(runtime, 2f);
                ExecuteFullHeal(runtime, 4f);
                Assert(runtime.HealUses == 3 &&
                       Mathf.Approximately(health.CurrentHealth, before + runtime.HealPerUse * 3f) &&
                       runtime.CanSelect(BossPatternExecution.SaemaeumTreasureHeal),
                    "Boss-2 phase-one healing must remain selectable without a use-count limit.");

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

        private static void ValidateLandingDamageAndKnockback()
        {
            GameObject root = CreateRuntimeBoss(out BossController _,
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
                float firstLandingTime = 3.8f / runtime.PlannedJumpCount + 0.001f;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, firstLandingTime);
                Assert(Mathf.Approximately(targetHealth.CurrentHealth, before - runtime.LandingDamage),
                    "Boss-2 landing did not damage the player at the locked landing point.");
                Assert(receiver.IsKnockedBack &&
                       Mathf.Approximately(receiver.CurrentVelocity.magnitude, runtime.LandingKnockbackSpeed),
                    "Boss-2 landing did not apply Buseureogi-style knockback.");
            }
            finally
            {
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
                            root.transform.localScale = Vector3.one * 1.8f;
                            root.AddComponent<CircleCollider2D>().radius = 0.38f;
                            Vector2 interiorHalf = half - Vector2.one * 0.2f;
                            target.transform.position = (Vector2)arena.transform.position +
                                Vector2.Scale(edge, interiorHalf - Vector2.one * 0.5f);
                            root.transform.position = target.transform.position - (Vector3)(edge.normalized * 3f);
                            Physics2D.SyncTransforms();
                            runtime.BeginCombat(target.transform, 88, 0f);
                            runtime.OnPatternStateChanged(BossActionState.Active,
                                BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                            runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                            Vector2 destination = GetPrivateField<Vector2>(runtime, "jumpDestination");
                            Vector2 local = destination - (Vector2)arena.transform.position;
                            Assert(Mathf.Abs(local.x) + 0.684f <= interiorHalf.x + 0.001f &&
                                   Mathf.Abs(local.y) + 0.684f <= interiorHalf.y + 0.001f,
                                "Landing body overlaps a wall.");
                            float before = target.GetComponent<Health>().CurrentHealth;
                            runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence,
                                3.8f / runtime.PlannedJumpCount + 0.001f);
                            Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth,
                                    before - runtime.LandingDamage),
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
                       Mathf.Approximately(health.CurrentHealth, before - 2f),
                    "Saemaeum ground contact must deal two damage.");
                Assert(!contact.TryApplyDamage(health, 0.79f),
                    "Continuous ground contact must respect its 0.8 second cooldown.");
            }
            finally
            {
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
                float duration = 3.8f / runtime.PlannedJumpCount;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, duration * 0.5f);
                Assert(collider.isTrigger, "Airborne boss must not physically push the player before landing.");
                Assert(visual.position.y - root.transform.position.y > 1.9f,
                    "Jump midpoint must visibly rise above the ground trajectory.");
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before),
                    "Airborne travel must not apply landing damage early.");
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, duration + 0.001f);
                Assert(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, before - runtime.LandingDamage),
                    "A nearby player must be hit at landing instead of being overshot by the maximum jump distance.");
                boss.CancelCombat();
                Assert(!collider.isTrigger && visual.localPosition == Vector3.zero,
                    "Cancelled jumps must restore solid collision and grounded visuals.");

                runtime.BeginCombat(target.transform, 88, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                target.transform.position = new Vector2(-5f, -4f);
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
                float after = target.GetComponent<Health>().CurrentHealth;
                runtime.TickPattern(BossActionState.Active,
                    BossPatternExecution.SaemaeumJumpSequence, 3.8f);
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
            boss.ConfigurePhaseTwo(0f, 0.75f);
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
