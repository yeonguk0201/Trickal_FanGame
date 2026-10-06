using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Passive-1: the basic attack knockback formula replaces the physical shove (Passive-0 §4.6), 칸나의 대포 grows the
    // basic attack shot (§4.1) and 비비의 콧물 poisons enemies on basic attack hits (§4.7·§4.8).
    public static class Week23Passive1Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6200f, 6200f);
        private const float Step = 0.02f;

        private static readonly PoisonSettings SnotPoison = new(
            Week23Passive1Setup.PoisonChance,
            Week23Passive1Setup.PoisonTickDamageRatio,
            Week23Passive1Setup.PoisonDurationSeconds,
            Week23Passive1Setup.PoisonIntervalSeconds,
            Week23Passive1Setup.PoisonMaxStacks);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Passive-1 Cannon and Snot")]
        public static void SetupAndVerifyBatch()
        {
            Week23Passive1Setup.Setup();
            string[] paths = CreatedAssetPaths();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week23Passive1Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids),
                "Passive-1 setup changed or lost an item or enemy Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-1 Cannon and Snot")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            ValidateContract();
            ValidateAssetsAndPool();
            ValidateKnockbackFormula();
            ValidateKnockbackWeights();
            ValidatePush();
            ValidatePushBlockers();
            ValidatePushPhysics();
            ValidateProjectileKnockback();
            ValidateProjectileSize();
            ValidatePoisonStats();
            ValidatePoisonStatus();
            ValidatePoisonFromProjectile();
            Debug.Log("Passive-1 verification passed: effect types 43 and 44 are appended, 칸나의 대포 and 비비의 콧물 " +
                      "are Rare two-stack artifacts in the selection reward pool, basic attack shots are triggers " +
                      "that push only by the knockback formula (0.2 at the base, shot speed ratio limited to " +
                      "0.5~2.0, divided by each enemy Prefab's knockback weight, bosses at weight 4) without " +
                      "stunning or interrupting and never during a dash, lunge or boss pattern, the cannon scales " +
                      "the shot's look and collision radius together up to radius 0.45 without changing damage, " +
                      "and poison stacks to 3, renews its 4 seconds, ticks every second as periodic player damage " +
                      "that credits the kill once and comes only from basic attack hits that dealt damage.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            // The older projectile and enemy checks place their objects around the world origin, so they run in
            // an empty scene before the Game Scene is opened.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PhaseGProjectileEffectsVerification.Verify();
            Week7RangedEnemyVerification.Verify();
            Week7DamageContextVerification.Verify();
            Week15Enemy4Verification.Verify();
            Week19Spawn2Verification.Verify();
            PhaseGArtifactContractVerification.Verify();
            Verify();
            Week21Range0Verification.Verify();
            Week23Enemy6Verification.Verify();
            Week16Reward3Verification.Verify();
            Debug.Log("Passive-1 regression verification passed.");
        }

        private static string[] CreatedAssetPaths() =>
            new[]
                {
                    Week23Passive1Setup.ItemPath(Week23Passive1Setup.CannonId),
                    Week23Passive1Setup.ItemPath(Week23Passive1Setup.SnotId),
                }
                .Concat(Week23Passive1Setup.KnockbackWeights.Select(entry => entry.Path)).ToArray();

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.ProjectileSizePercent == 43 && (int)ItemEffectType.BasicAttackPoison == 44 &&
                   (int)ItemEffectType.FreeCurrentShopOffers == 42,
                "Passive-1 must append effect types 43 and 44 without renumbering earlier effects.");
            Assert((int)DamageSourceType.PlayerBomb == 10 && (int)DamageSourceType.PlayerStatusEffect == 11,
                "The status effect damage source must be appended after the existing sources.");

            Assert(!new ItemEffectEntry(ItemEffectType.ProjectileSizePercent).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.ProjectileSizePercent, 0.5f).TryValidate(out _),
                "The shot size effect needs a positive magnitude.");
            Assert(PoisonEntry(0.15f, 0.15f, 4f, 1f, 3).TryValidate(out _) &&
                   !PoisonEntry(0f, 0.15f, 4f, 1f, 3).TryValidate(out _) &&
                   !PoisonEntry(1.5f, 0.15f, 4f, 1f, 3).TryValidate(out _) &&
                   !PoisonEntry(0.15f, 0f, 4f, 1f, 3).TryValidate(out _) &&
                   !PoisonEntry(0.15f, 0.15f, 0f, 1f, 3).TryValidate(out _) &&
                   !PoisonEntry(0.15f, 0.15f, 4f, 0f, 3).TryValidate(out _) &&
                   !PoisonEntry(0.15f, 0.15f, 4f, 1f, 0).TryValidate(out _),
                "The poison effect needs a chance within (0, 1], a tick damage ratio, duration, interval and stacks.");
        }

        private static void ValidateAssetsAndPool()
        {
            ItemDefinition cannon = LoadItem(Week23Passive1Setup.CannonId);
            ItemDefinition snot = LoadItem(Week23Passive1Setup.SnotId);
            Assert(cannon.DisplayName == Week23Passive1Setup.CannonName && cannon.Kind == ItemKind.Artifact &&
                   cannon.Rarity == ItemRarity.Rare && cannon.IsActive && cannon.MaxStacks == 2 &&
                   cannon.Effects.Count == 1 &&
                   cannon.Effects[0].EffectType == ItemEffectType.ProjectileSizePercent &&
                   Mathf.Approximately(cannon.Effects[0].Magnitude, 0.5f),
                "칸나의 대포 must be a Rare artifact with two stacks of +50% shot size.");
            Assert(snot.DisplayName == Week23Passive1Setup.SnotName && snot.Kind == ItemKind.Artifact &&
                   snot.Rarity == ItemRarity.Rare && snot.IsActive && snot.MaxStacks == 2 &&
                   snot.Effects.Count == 1 && snot.Effects[0].EffectType == ItemEffectType.BasicAttackPoison &&
                   Mathf.Approximately(snot.Effects[0].Magnitude, 0.15f) &&
                   Mathf.Approximately(snot.Effects[0].SecondaryMagnitude, 0.15f) &&
                   Mathf.Approximately(snot.Effects[0].DurationSeconds, 4f) &&
                   Mathf.Approximately(snot.Effects[0].IntervalSeconds, 1f) && snot.Effects[0].IntegerAmount == 3,
                "비비의 콧물 must be a Rare artifact with two stacks of a 15% poison chance (4s, 1s ticks, 15%, 3 stacks).");
            Assert(ArtifactEffectDescription.Build(cannon) == "투사체 크기 +50%",
                "칸나의 대포 must describe its shot size bonus.");
            Assert(ArtifactEffectDescription.Build(snot) ==
                   "기본 공격 적중 시 15% 확률로 중독 (4초간 1초마다 공격력의 15% 피해, 최대 3중첩)",
                "비비의 콧물 must describe its poison.");

            ItemDefinition[] assets = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null).ToArray();
            Assert(UsersOf(assets, ItemEffectType.ProjectileSizePercent)
                       .SequenceEqual(new[] { Week23Passive1Setup.CannonId }) &&
                   UsersOf(assets, ItemEffectType.BasicAttackPoison)
                       .SequenceEqual(new[] { Week23Passive1Setup.SnotId }),
                "Only 칸나의 대포 may use the shot size effect and only 비비의 콧물 the poison effect.");

            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>(FindObjectsInactive.Include);
            Assert(assembler != null && assembler.SelectionRewardPool.Contains(cannon) &&
                   assembler.SelectionRewardPool.Contains(snot),
                "The selection reward pool must offer both Passive-1 artifacts.");
            Assert(assembler.SelectionRewardPool.Count(item => item == cannon) == 1 &&
                   assembler.SelectionRewardPool.Count(item => item == snot) == 1 &&
                   assembler.SelectionRewardPool.All(item => item != null && item.Kind == ItemKind.Artifact &&
                                                             !GoldenChestExclusivePool.IsExclusive(item)),
                "The selection reward pool must hold each Passive-1 artifact once and stay artifact-only.");
        }

        private static void ValidateKnockbackFormula()
        {
            float baseSpeed = BasicAttackKnockback.ResolveSpeed(8f, 1f);
            Assert(Mathf.Approximately(baseSpeed, 2f) &&
                   Mathf.Approximately(baseSpeed * BasicAttackKnockback.Duration, 0.2f),
                "A base shot must push a weight 1 enemy at 2 per second for 0.1 seconds (0.2 units).");
            Assert(Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(4f, 1f), 1f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(1f, 1f), 1f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(16f, 1f), 4f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(40f, 1f), 4f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(12f, 1f), 3f),
                "The shot speed ratio must scale the push and stay within 0.5~2.0.");
            Assert(Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(8f, 4f), 0.5f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(8f, 0.5f), 4f) &&
                   Mathf.Approximately(BasicAttackKnockback.ResolveSpeed(8f, 1f, 0.5f), 3f),
                "The push must be divided by the knockback weight (bosses 25%) and scaled by the knockback bonus.");
        }

        private static void ValidateKnockbackWeights()
        {
            Dictionary<string, float> expected =
                Week23Passive1Setup.KnockbackWeights.ToDictionary(entry => entry.Path, entry => entry.Weight);
            Assert(expected.Count == Week23Passive1Setup.KnockbackWeights.Length,
                "The knockback weight table must list each enemy Prefab once.");

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                KnockbackReceiver receiver = prefab != null ? prefab.GetComponent<KnockbackReceiver>() : null;
                bool isEnemy = receiver != null && (prefab.layer == enemyLayer ||
                                                    prefab.GetComponent<BossController>() != null);
                if (!isEnemy)
                {
                    Assert(!expected.ContainsKey(path), $"'{path}' is in the knockback weight table but is not an enemy.");
                    continue;
                }

                Assert(expected.TryGetValue(path, out float weight),
                    $"Enemy Prefab '{path}' has no entry in the Passive-1 knockback weight table.");
                Assert(Mathf.Approximately(receiver.KnockbackWeight, weight),
                    $"'{path}' must have knockback weight {weight}, found {receiver.KnockbackWeight}.");
                Assert(prefab.GetComponent<BossController>() == null ||
                       Mathf.Approximately(weight, Week23Passive1Setup.BossKnockbackWeight),
                    $"Boss Prefab '{path}' must have knockback weight {Week23Passive1Setup.BossKnockbackWeight}.");
                expected.Remove(path);
            }

            Assert(expected.Count == 0,
                "Knockback weight table entries without an enemy Prefab: " + string.Join(", ", expected.Keys));
        }

        private static void ValidatePush()
        {
            GameObject root = new("Passive-1 Push Verification");
            try
            {
                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                KnockbackReceiver receiver = enemy.GetComponent<KnockbackReceiver>();
                Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
                float now = Time.time;

                Assert(!receiver.ApplyPush(Vector2.zero, 2f, 0.1f) && !receiver.ApplyPush(Vector2.right, 0f, 0.1f) &&
                       !receiver.ApplyPush(Vector2.right, 2f, 0f) && !receiver.IsPushed,
                    "A push needs a direction, a speed and a duration.");
                Assert(receiver.ApplyPush(Vector2.right * 5f, 2f, 0.1f) && receiver.IsPushed &&
                       Approximately(receiver.PushVelocity, Vector2.right * 2f),
                    "A push must move the enemy along the shot direction at the resolved speed.");
                Assert(!receiver.IsActive && !receiver.IsKnockedBack && !receiver.IsStunned &&
                       Approximately(body.linearVelocity, Vector2.zero),
                    "A basic attack push must not stun the enemy, suppress its actions or take over its velocity.");

                Assert(receiver.ApplyPush(Vector2.up, 3f, 0.1f) && Approximately(receiver.PushVelocity, Vector2.up * 3f),
                    "A second push must replace the first instead of adding to it.");
                Assert(Approximately(receiver.TickPush(now + 0.05f, Step), Vector2.up * 3f * Step) && receiver.IsPushed,
                    "A running push must displace the body by its velocity each physics step.");
                Assert(Approximately(receiver.TickPush(now + 0.1f, Step), Vector2.zero) && !receiver.IsPushed &&
                       Approximately(receiver.PushVelocity, Vector2.zero),
                    "A push must end after its duration.");

                Assert(receiver.ApplyPush(Vector2.right, 2f, 0.1f) && receiver.Apply(Vector2.left, 8f) &&
                       !receiver.IsPushed && receiver.IsKnockedBack,
                    "An explicit knockback (dash) must replace a running push.");
                Assert(!receiver.ApplyPush(Vector2.right, 2f, 0.1f),
                    "A basic attack push must not override an explicit knockback.");
                receiver.Stop();

                Assert(receiver.ApplyPush(Vector2.right, 2f, 0.1f), "The enemy must be pushable again after a knockback.");
                enemy.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 1000f));
                Assert(enemy.IsDead && Approximately(receiver.TickPush(now, Step), Vector2.zero) && !receiver.IsPushed &&
                       !receiver.ApplyPush(Vector2.right, 2f, 0.1f),
                    "A dead enemy must not be pushed.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidatePushBlockers()
        {
            GameObject root = new("Passive-1 Push Blocker Verification");
            try
            {
                GameObject chargerObject = new("Passive-1 Charger", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(ChargingEnemyController));
                chargerObject.transform.SetParent(root.transform);
                chargerObject.transform.position = Origin;
                ChargingEnemyController charger = chargerObject.GetComponent<ChargingEnemyController>();
                KnockbackReceiver chargerReceiver = chargerObject.GetComponent<KnockbackReceiver>();
                PropertyInfo chargerState = typeof(ChargingEnemyController).GetProperty(nameof(charger.State));
                chargerState.SetValue(charger, ChargingEnemyState.Windup);
                Assert(chargerReceiver.ApplyPush(Vector2.right, 2f, 0.1f) && !chargerReceiver.IsActive &&
                       charger.State == ChargingEnemyState.Windup,
                    "A push during a wind-up must move the enemy without interrupting the wind-up.");
                chargerState.SetValue(charger, ChargingEnemyState.Dashing);
                Assert(Approximately(chargerReceiver.TickPush(Time.time, Step), Vector2.zero) &&
                       !chargerReceiver.IsPushed && !chargerReceiver.ApplyPush(Vector2.right, 2f, 0.1f),
                    "A dashing enemy must not be pushed, and a running push must stop when the dash starts.");

                GameObject meleeObject = new("Passive-1 Melee", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(MeleeEnemyAttack));
                meleeObject.transform.SetParent(root.transform);
                meleeObject.transform.position = Origin + Vector2.up * 5f;
                KnockbackReceiver meleeReceiver = meleeObject.GetComponent<KnockbackReceiver>();
                FieldInfo lunging = typeof(MeleeEnemyAttack).GetField("lunging",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(lunging != null, "MeleeEnemyAttack.lunging was renamed; update the lunge push check.");
                Assert(meleeReceiver.ApplyPush(Vector2.right, 2f, 0.1f), "A melee enemy must be pushable between lunges.");
                lunging.SetValue(meleeObject.GetComponent<MeleeEnemyAttack>(), true);
                Assert(!meleeReceiver.ApplyPush(Vector2.right, 2f, 0.1f), "A lunging enemy must not be pushed.");

                GameObject bossObject = new("Passive-1 Boss", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(BossController));
                bossObject.transform.SetParent(root.transform);
                bossObject.transform.position = Origin + Vector2.up * 10f;
                BossController boss = bossObject.GetComponent<BossController>();
                KnockbackReceiver bossReceiver = bossObject.GetComponent<KnockbackReceiver>();
                bossReceiver.ConfigureWeight(Week23Passive1Setup.BossKnockbackWeight);
                PropertyInfo bossState = typeof(BossController).GetProperty(nameof(boss.State));
                Assert(BasicAttackKnockback.TryApply(bossObject.GetComponent<Health>(), Vector2.right * 8f) &&
                       Approximately(bossReceiver.PushVelocity, Vector2.right * 0.5f) && !bossReceiver.IsActive,
                    "A boss between patterns must be pushed at 25% without being suppressed.");
                foreach (BossActionState blocked in new[] { BossActionState.Active, BossActionState.PhaseTransition })
                {
                    bossState.SetValue(boss, blocked);
                    Assert(!BasicAttackKnockback.TryApply(bossObject.GetComponent<Health>(), Vector2.right * 8f),
                        $"A boss in the {blocked} state must not be pushed.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidatePushPhysics()
        {
            GameObject root = new("Passive-1 Push Physics Verification");
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;

                // A Dynamic enemy travels about 0.2 and keeps its own velocity afterwards.
                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                KnockbackReceiver receiver = enemy.GetComponent<KnockbackReceiver>();
                Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
                Physics2D.SyncTransforms();
                Assert(BasicAttackKnockback.TryApply(enemy, Vector2.right * 8f), "The base shot must push the enemy.");
                float time = Time.time;
                for (int step = 0; step < 10; step++)
                {
                    receiver.TickPush(time, Step);
                    Physics2D.Simulate(Step);
                    time += Step;
                }

                float travelled = body.position.x - Origin.x;
                Assert(Mathf.Abs(travelled - 0.2f) <= 0.05f && Mathf.Abs(body.position.y - Origin.y) <= 0.001f,
                    $"A base shot must push a weight 1 enemy about 0.2 units, but it moved {travelled}.");
                Assert(!receiver.IsPushed && Approximately(body.linearVelocity, Vector2.zero),
                    "The push must end and leave the enemy's own velocity untouched.");

                // A shot is a trigger, so overlapping an enemy does not shove it.
                Vector2 shoveOrigin = Origin + Vector2.up * 20f;
                Health still = CreateEnemy(root.transform, shoveOrigin, 100f);
                Health owner = CreatePlayer(root.transform, shoveOrigin + Vector2.down * 20f);
                Projectile shot = CreateShot(root.transform, shoveOrigin + Vector2.left * 0.5f);
                shot.Launch(Vector2.right * 8f, owner, new DamageContext(owner.gameObject,
                    DamageSourceType.PlayerProjectile, 10f));
                Assert(shot.GetComponentsInChildren<Collider2D>().All(collider => collider.isTrigger),
                    "A launched basic attack shot must only have trigger colliders.");
                Physics2D.SyncTransforms();
                for (int step = 0; step < 10; step++) Physics2D.Simulate(Step);
                Assert(Approximately(still.GetComponent<Rigidbody2D>().position, shoveOrigin),
                    "A shot passing through an enemy must not shove it through physics.");

                // A Kinematic boss stops at solid scenery instead of being pushed through it.
                Vector2 wallOrigin = Origin + Vector2.up * 40f;
                Health boss = CreateEnemy(root.transform, wallOrigin, 100f);
                boss.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                GameObject wall = new("Passive-1 Wall", typeof(BoxCollider2D));
                wall.transform.SetParent(root.transform);
                wall.layer = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
                wall.transform.position = wallOrigin + Vector2.right * 1.02f;
                wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 4f);
                Physics2D.SyncTransforms();
                KnockbackReceiver bossReceiver = boss.GetComponent<KnockbackReceiver>();
                Assert(bossReceiver.ApplyPush(Vector2.right, 2f, 0.1f) &&
                       Approximately(bossReceiver.TickPush(Time.time, Step), Vector2.zero),
                    "A Kinematic body must not be pushed into a wall.");
                Assert(bossReceiver.ApplyPush(Vector2.left, 2f, 0.1f) &&
                       Approximately(bossReceiver.TickPush(Time.time, Step), Vector2.left * 2f * Step),
                    "A Kinematic body must still be pushed away from a wall.");
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateProjectileKnockback()
        {
            GameObject root = new("Passive-1 Projectile Knockback Verification");
            try
            {
                Health owner = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                DamageContext damage = new(owner.gameObject, DamageSourceType.PlayerProjectile, 10f);

                Health light = CreateEnemy(root.transform, Origin, 100f, 0.5f);
                Hit(Launch(root.transform, Origin, Vector2.up * 8f, owner, damage, new ProjectileHitEffects(true)), light);
                Assert(Approximately(light.GetComponent<KnockbackReceiver>().PushVelocity, Vector2.up * 4f),
                    "A hit must push a weight 0.5 enemy at twice the base speed along the shot direction.");

                Health fast = CreateEnemy(root.transform, Origin, 100f);
                Hit(Launch(root.transform, Origin, Vector2.right * 4f, owner, damage, new ProjectileHitEffects(true)),
                    fast);
                Assert(Approximately(fast.GetComponent<KnockbackReceiver>().PushVelocity, Vector2.right),
                    "A slow shot must push less (shot speed ratio 0.5).");

                Health untouched = CreateEnemy(root.transform, Origin, 100f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, default), untouched);
                Assert(!untouched.GetComponent<KnockbackReceiver>().IsPushed &&
                       Mathf.Approximately(untouched.CurrentHealth, 90f),
                    "A shot without the basic attack hit effects must only deal its damage.");

                Health killed = CreateEnemy(root.transform, Origin, 5f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, new ProjectileHitEffects(true)),
                    killed);
                Assert(killed.IsDead && !killed.GetComponent<KnockbackReceiver>().IsPushed,
                    "An enemy killed by the hit must not be pushed.");

                // A split shot keeps the knockback of the shot it came from.
                Health first = CreateEnemy(root.transform, Origin, 100f);
                Projectile parent = Launch(root.transform, Origin, Vector2.right * 8f, owner, damage,
                    new ProjectileHitEffects(true), 1, new ProjectileSplitSettings(3, 0.3f, 4f, 20f, 0.5f));
                Projectile[] before = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
                Hit(parent, first);
                Projectile[] splits = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                    .Except(before).ToArray();
                try
                {
                    Assert(splits.Length == 3 && splits.All(split => split.IsSplitProjectile),
                        "The piercing shot must split into three shots.");
                    Health second = CreateEnemy(root.transform, Origin, 100f);
                    Hit(splits[0], second);
                    Assert(second.GetComponent<KnockbackReceiver>().IsPushed &&
                           Mathf.Approximately(second.GetComponent<KnockbackReceiver>().PushVelocity.magnitude, 2f),
                        "A split shot must push by the same formula.");
                }
                finally
                {
                    foreach (Projectile split in splits)
                        if (split != null) Object.DestroyImmediate(split.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateProjectileSize()
        {
            Assert(Mathf.Approximately(ProjectileSizing.WorldCollisionRadius(ProjectileSizing.PlayerBasicScale), 0.2f) &&
                   Mathf.Approximately(ProjectileSizing.MaximumPlayerBasicSizeMultiplier, 2.25f),
                "The basic attack shot radius is 0.2 and may grow to 0.45.");

            GameObject prefab = LoadShotPrefab();
            GameObject player = CreateAttacker("Passive-1 Size Player");
            ItemDefinition cannon = LoadItem(Week23Passive1Setup.CannonId);
            try
            {
                PlayerProjectileAttack attack = player.GetComponent<PlayerProjectileAttack>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                PlayerStats stats = player.GetComponent<PlayerStats>();
                float damage = attack.CurrentDamage;
                float speed = attack.ProjectileSpeed;
                float lifetime = attack.ProjectileLifetime;

                Assert(Mathf.Approximately(attack.ProjectileSizeMultiplier, 1f) &&
                       Mathf.Approximately(SpawnShotRadius(player, prefab, out float baseScale), 0.2f) &&
                       Mathf.Approximately(baseScale, prefab.transform.localScale.x),
                    "Without the cannon the basic attack shot keeps its Prefab size (radius 0.2).");
                Assert(inventory.TryAcquire(cannon) && Mathf.Approximately(attack.ProjectileSizeMultiplier, 1.5f) &&
                       Mathf.Approximately(SpawnShotRadius(player, prefab, out float firstScale), 0.3f) &&
                       Mathf.Approximately(firstScale, prefab.transform.localScale.x * 1.5f),
                    "One cannon must scale the shot's look and collision radius by 1.5 (radius 0.3).");
                Assert(inventory.TryAcquire(cannon) && Mathf.Approximately(attack.ProjectileSizeMultiplier, 2f) &&
                       Mathf.Approximately(SpawnShotRadius(player, prefab, out float secondScale), 0.4f) &&
                       Mathf.Approximately(secondScale, prefab.transform.localScale.x * 2f),
                    "Two cannons must scale the shot's look and collision radius by 2 (radius 0.4).");
                Assert(!inventory.TryAcquire(cannon), "칸나의 대포 must stop at two stacks.");
                Assert(Mathf.Approximately(attack.CurrentDamage, damage) &&
                       Mathf.Approximately(attack.ProjectileSpeed, speed) &&
                       Mathf.Approximately(attack.ProjectileLifetime, lifetime),
                    "The cannon must not change damage, shot speed or range.");

                stats.AddProjectileSizePercent(5f);
                Assert(Mathf.Approximately(attack.ProjectileSizeMultiplier, 2.25f) &&
                       Mathf.Approximately(SpawnShotRadius(player, prefab, out _),
                           ProjectileSizing.MaximumPlayerBasicRadius),
                    "The shot radius must stop at 0.45 however large the size bonus is.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidatePoisonStats()
        {
            GameObject player = CreateAttacker("Passive-1 Poison Player");
            ItemDefinition snot = LoadItem(Week23Passive1Setup.SnotId);
            try
            {
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(!stats.BasicAttackPoison.IsEnabled && stats.BasicAttackHitEffects.AppliesKnockback &&
                       !stats.BasicAttackHitEffects.Poison.IsEnabled,
                    "Without 비비의 콧물 a basic attack only carries the knockback.");

                Assert(inventory.TryAcquire(snot), "비비의 콧물 must be acquirable.");
                PoisonSettings one = stats.BasicAttackHitEffects.Poison;
                Assert(one.IsEnabled && Mathf.Approximately(one.Chance, 0.15f) &&
                       Mathf.Approximately(one.TickDamageRatio, 0.15f) &&
                       Mathf.Approximately(one.DurationSeconds, 4f) && Mathf.Approximately(one.IntervalSeconds, 1f) &&
                       one.MaximumStacks == 3,
                    "One 비비의 콧물 must give a 15% poison chance (4s, 1s ticks, 15% per stack, 3 stacks).");
                Assert(inventory.TryAcquire(snot) && Mathf.Approximately(stats.BasicAttackPoison.Chance, 0.3f) &&
                       stats.BasicAttackPoison.MaximumStacks == 3 &&
                       Mathf.Approximately(stats.BasicAttackPoison.TickDamageRatio, 0.15f),
                    "A second 비비의 콧물 must add its chance (30%) without changing the poison itself.");
                Assert(!inventory.TryAcquire(snot), "비비의 콧물 must stop at two stacks.");

                stats.AddBasicAttackPoison(5f, 0.15f, 4f, 1f, 3);
                Assert(Mathf.Approximately(stats.BasicAttackPoison.Chance, 1f), "The poison chance must stop at 100%.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Physics2D.SyncTransforms();
            }

            Assert(EnemyStatusEffects.RollsPoison(SnotPoison, 0.149f) && !EnemyStatusEffects.RollsPoison(SnotPoison, 0.15f) &&
                   !EnemyStatusEffects.RollsPoison(default, 0f),
                "Poison must be applied only when the roll is below the chance.");
        }

        private static void ValidatePoisonStatus()
        {
            GameObject root = new("Passive-1 Poison Verification");
            try
            {
                Health owner = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerCombatEvents events = owner.GetComponent<PlayerCombatEvents>();
                int basicHits = 0;
                int kills = 0;
                events.BasicAttackHit += _ => basicHits++;
                events.EnemyKilled += _ => kills++;

                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                List<(DamageContext Context, DamageResult Result)> ticks = new();
                enemy.DamageResolved += (context, result) => ticks.Add((context, result));
                float start = Time.time;

                Assert(!EnemyStatusEffects.TryApplyPoison(enemy, owner.gameObject, 10f, SnotPoison, 0.5f) &&
                       enemy.GetComponent<EnemyStatusEffects>() == null,
                    "A failed roll must not poison the enemy.");
                Assert(EnemyStatusEffects.TryApplyPoison(enemy, owner.gameObject, 10f, SnotPoison, 0f),
                    "A successful roll must poison the enemy.");
                EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
                Assert(status != null && status.IsPoisoned && status.PoisonStacks == 1 &&
                       Mathf.Approximately(status.PoisonTickDamagePerStack, 1.5f) &&
                       Mathf.Approximately(status.PoisonEndTime, start + 4f) &&
                       Mathf.Approximately(status.NextPoisonTickTime, start + 1f),
                    "Poison starts at one stack for 4 seconds with its first tick one second later.");

                status.Tick(start + 0.99f);
                Assert(ticks.Count == 0 && Mathf.Approximately(enemy.CurrentHealth, 100f),
                    "Poison must not tick before its first interval.");
                status.Tick(start + 1f);
                Assert(ticks.Count == 1 && Mathf.Approximately(enemy.CurrentHealth, 98.5f),
                    "One poison stack must deal 15% of the attack damage per tick.");
                Assert(ticks[0].Context.DeliveryType == DamageDeliveryType.Periodic &&
                       ticks[0].Context.SourceType == DamageSourceType.PlayerStatusEffect &&
                       ticks[0].Context.Source == owner.gameObject && !ticks[0].Context.CanCritical &&
                       !ticks[0].Result.IsCritical && basicHits == 0,
                    "A poison tick is periodic player damage: no critical hit and no basic attack hit event.");

                // A new application adds a stack and renews the duration without moving the next tick.
                status.ApplyPoison(owner.gameObject, 10f, SnotPoison, start + 1.5f);
                Assert(status.PoisonStacks == 2 && Mathf.Approximately(status.PoisonEndTime, start + 5.5f) &&
                       Mathf.Approximately(status.NextPoisonTickTime, start + 2f),
                    "Renewing poison adds a stack and 4 fresh seconds but keeps the tick schedule.");
                status.Tick(start + 2f);
                Assert(ticks.Count == 2 && Mathf.Approximately(enemy.CurrentHealth, 95.5f),
                    "Two poison stacks must deal 30% of the attack damage per tick.");

                // The tick damage follows the attack damage at the latest application, and stacks stop at 3.
                status.ApplyPoison(owner.gameObject, 20f, SnotPoison, start + 2.5f);
                status.ApplyPoison(owner.gameObject, 20f, SnotPoison, start + 2.6f);
                Assert(status.PoisonStacks == 3 && Mathf.Approximately(status.PoisonTickDamagePerStack, 3f) &&
                       Mathf.Approximately(status.PoisonEndTime, start + 6.6f),
                    "Poison must stop at 3 stacks and fix its tick damage when it is applied.");
                status.Tick(start + 3f);
                Assert(ticks.Count == 3 && Mathf.Approximately(enemy.CurrentHealth, 86.5f),
                    "Three poison stacks at 20 attack damage must deal 9 per tick.");

                // Every stack ends together when the duration runs out; the remaining ticks land first.
                status.Tick(start + 6.7f);
                Assert(ticks.Count == 6 && Mathf.Approximately(enemy.CurrentHealth, 59.5f) && !status.IsPoisoned &&
                       status.PoisonStacks == 0,
                    "Poison must deal its remaining ticks and then lose every stack at once.");
                status.Tick(start + 20f);
                Assert(ticks.Count == 6, "Expired poison must stop dealing damage.");

                Assert(EnemyStatusEffects.TryApplyPoison(enemy, owner.gameObject, 10f, SnotPoison, 0f) &&
                       status.PoisonStacks == 1,
                    "Poison applied after it expired must start again from one stack.");
                status.Clear();
                Assert(!status.IsPoisoned, "Clearing the status must remove the poison.");

                // A tick that kills is the player's kill, reported once.
                Health weak = CreateEnemy(root.transform, Origin, 2f);
                Assert(EnemyStatusEffects.TryApplyPoison(weak, owner.gameObject, 20f, SnotPoison, 0f),
                    "The weak enemy must be poisoned.");
                EnemyStatusEffects weakStatus = weak.GetComponent<EnemyStatusEffects>();
                weakStatus.Tick(Time.time + 1f);
                weakStatus.Tick(Time.time + 2f);
                Assert(weak.IsDead && kills == 1 && basicHits == 0 && !weakStatus.IsPoisoned,
                    "A poison tick that kills must credit the player exactly once and end the poison.");
                Assert(!EnemyStatusEffects.TryApplyPoison(weak, owner.gameObject, 20f, SnotPoison, 0f),
                    "A dead enemy must not be poisoned.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidatePoisonFromProjectile()
        {
            GameObject root = new("Passive-1 Projectile Poison Verification");
            try
            {
                Health owner = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerCombatEvents events = owner.GetComponent<PlayerCombatEvents>();
                int basicHits = 0;
                events.BasicAttackHit += _ => basicHits++;
                DamageContext damage = new(owner.gameObject, DamageSourceType.PlayerProjectile, 10f, 2f);
                PoisonSettings always = new(1f, 0.15f, 4f, 1f, 3);
                ProjectileHitEffects poisonous = new(true, always);

                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, poisonous), enemy);
                EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
                Assert(Mathf.Approximately(enemy.CurrentHealth, 80f) && basicHits == 1 && status != null &&
                       status.PoisonStacks == 1 && Mathf.Approximately(status.PoisonTickDamagePerStack, 1.5f) &&
                       enemy.GetComponent<KnockbackReceiver>().IsPushed,
                    "A basic attack hit deals its damage, reports the hit, then poisons (15% of the attack damage, " +
                    "not of the multiplied hit) and pushes the enemy.");
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, poisonous), enemy);
                Assert(status.PoisonStacks == 2 && basicHits == 2, "Each basic attack hit rolls poison on its own.");

                Health plain = CreateEnemy(root.transform, Origin, 100f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, new ProjectileHitEffects(true)),
                    plain);
                Assert(plain.GetComponent<EnemyStatusEffects>() == null, "A shot without poison must not poison.");

                Health invulnerable = CreateEnemy(root.transform, Origin, 100f);
                invulnerable.SetInvulnerable(true);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, poisonous), invulnerable);
                Assert(invulnerable.GetComponent<EnemyStatusEffects>() == null &&
                       Mathf.Approximately(invulnerable.CurrentHealth, 100f),
                    "A hit that dealt no damage must not poison.");

                Health killed = CreateEnemy(root.transform, Origin, 5f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, poisonous), killed);
                Assert(killed.IsDead && killed.GetComponent<EnemyStatusEffects>() == null,
                    "An enemy killed by the hit must not be poisoned.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static ItemEffectEntry PoisonEntry(float chance, float ratio, float duration, float interval,
            int stacks) =>
            new(ItemEffectType.BasicAttackPoison, chance, configuredSecondaryMagnitude: ratio,
                configuredIntegerAmount: stacks, configuredIntervalSeconds: interval,
                configuredDurationSeconds: duration);

        private static string[] UsersOf(IEnumerable<ItemDefinition> assets, ItemEffectType type) =>
            assets.Where(definition => definition.Effects.Any(effect => effect.EffectType == type))
                .Select(definition => definition.ItemId).ToArray();

        private static ItemDefinition LoadItem(string itemId)
        {
            ItemDefinition item =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week23Passive1Setup.ItemPath(itemId));
            Assert(item != null && item.ItemId == itemId && item.IsValid, $"Run Passive-1 setup first: {itemId} is missing.");
            return item;
        }

        private static GameObject LoadShotPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErpinProjectileArtworkSetup.BasicPrefabPath);
            Assert(prefab != null && prefab.GetComponent<Projectile>() != null &&
                   prefab.GetComponent<CircleCollider2D>() != null,
                "The player projectile Prefab is missing.");
            return prefab;
        }

        private static Projectile CreateShot(Transform parent, Vector2 position)
        {
            GameObject shot = (GameObject)PrefabUtility.InstantiatePrefab(LoadShotPrefab(), parent);
            shot.transform.position = position;
            return shot.GetComponent<Projectile>();
        }

        private static Projectile Launch(Transform parent, Vector2 position, Vector2 velocity, Health owner,
            DamageContext damage, ProjectileHitEffects hitEffects, int pierces = 0,
            ProjectileSplitSettings split = default)
        {
            Projectile shot = CreateShot(parent, position);
            shot.Launch(velocity, owner, damage, pierces, split, configuredHitEffects: hitEffects);
            return shot;
        }

        private static void Hit(Projectile shot, Health target)
        {
            MethodInfo hit = typeof(Projectile).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(hit != null, "Projectile.Hit was renamed; update the Passive-1 hit checks.");
            hit.Invoke(shot, new object[] { target.GetComponent<Collider2D>() });
        }

        // Fires one basic attack shot from the player and returns its world collision radius.
        private static float SpawnShotRadius(GameObject player, GameObject prefab, out float scale)
        {
            PlayerProjectileAttack attack = player.GetComponent<PlayerProjectileAttack>();
            MethodInfo spawn = typeof(PlayerProjectileAttack).GetMethod("SpawnProjectile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(spawn != null, "PlayerProjectileAttack.SpawnProjectile was renamed; update the shot size check.");
            Projectile[] before = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            spawn.Invoke(attack, new object[] { Vector2.right });
            Projectile[] spawned = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Except(before)
                .ToArray();
            try
            {
                Assert(spawned.Length == 1, $"One basic attack must spawn one shot, found {spawned.Length}.");
                Transform shot = spawned[0].transform;
                CircleCollider2D collider = spawned[0].GetComponent<CircleCollider2D>();
                SpriteRenderer renderer = spawned[0].GetComponentInChildren<SpriteRenderer>();
                Assert(Mathf.Approximately(shot.localScale.x, shot.localScale.y) && renderer != null &&
                       Mathf.Approximately(renderer.transform.lossyScale.x / shot.localScale.x,
                           prefab.GetComponentInChildren<SpriteRenderer>().transform.lossyScale.x /
                           prefab.transform.localScale.x),
                    "The shot's look must scale by the same factor as the shot itself.");
                scale = shot.localScale.x;
                return collider.radius * shot.lossyScale.x;
            }
            finally
            {
                foreach (Projectile shot in spawned) Object.DestroyImmediate(shot.gameObject);
            }
        }

        private static GameObject CreateAttacker(string name)
        {
            GameObject player = new(name, typeof(CircleCollider2D), typeof(PlayerProjectileAttack),
                typeof(PlayerInventory));
            player.transform.position = Origin + Vector2.down * 40f;
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Invoke(player.GetComponent<Health>(), "Awake");
            Invoke(player.GetComponent<PlayerSP>(), "Awake");
            Invoke(player.GetComponent<PlayerStats>(), "Awake");
            Invoke(player.GetComponent<PlayerMovement>(), "Awake");
            Invoke(player.GetComponent<PlayerInventory>(), "Awake");
            PlayerProjectileAttack attack = player.GetComponent<PlayerProjectileAttack>();
            Invoke(attack, "Awake");
            SerializedObject serialized = new(attack);
            serialized.FindProperty("projectilePrefab").objectReferenceValue =
                LoadShotPrefab().GetComponent<Projectile>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Passive-1 Player", typeof(PlayerMovement), typeof(CircleCollider2D),
                typeof(PlayerCombatEvents));
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = player.GetComponent<Health>();
            Invoke(health, "Awake");
            health.EnableHealthUnits();
            return health;
        }

        private static Health CreateEnemy(Transform parent, Vector2 position, float maxHealth, float weight = 1f)
        {
            GameObject enemy = new("Passive-1 Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(KnockbackReceiver));
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            enemy.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            Health health = enemy.GetComponent<Health>();
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(health, "Awake");
            enemy.GetComponent<KnockbackReceiver>().ConfigureWeight(weight);
            return health;
        }

        private static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(target, null);
        }

        private static bool Approximately(Vector2 actual, Vector2 expected) =>
            (actual - expected).sqrMagnitude <= 0.0001f * 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
