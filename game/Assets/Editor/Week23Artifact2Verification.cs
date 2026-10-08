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
    // Artifact-2: the 14 confirmed artifacts (docs/12 "확정 상세 효과") with burn and shock (Passive-0 §4.7·§4.8),
    // target-state damage bonuses, execution, the hit explosion, the death ward and the kill stacks.
    public static class Week23Artifact2Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6600f, 6600f);
        // A time base whose half-second steps are exact in a float.
        private const float Start = 64f;
        private const float Tolerance = 0.001f;

        private static readonly BurnSettings Burn = new(0.2f, Week23Artifact2Setup.BurnTickDamageRatio,
            Week23Artifact2Setup.BurnDurationSeconds, Week23Artifact2Setup.BurnIntervalSeconds);
        private static readonly ShockSettings Shock = new(0.2f, Week23Artifact2Setup.ShockSlowPerStack,
            Week23Artifact2Setup.ShockDurationSeconds, Week23Artifact2Setup.ShockMaxStacks);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Artifact-2 Confirmed Artifacts")]
        public static void SetupAndVerifyBatch()
        {
            Week23Artifact2Setup.Setup();
            string[] paths = Week23Artifact2Setup.Artifacts
                .Select(spec => Week23Artifact2Setup.ItemPath(spec.ItemId)).ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week23Artifact2Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids),
                "Artifact-2 setup changed or lost an item GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Artifact-2 Confirmed Artifacts")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);
            try
            {
                ValidateContract();
                ValidateAssetsAndPool();
                ValidateStatArtifacts();
                ValidateBurnStatus();
                ValidateShockStatus();
                ValidateStatusFromItemsAndProjectile();
                ValidateBurningTargetBonus();
                ValidateShockedTargetBonus();
                ValidateExecution();
                ValidateExplosion();
                ValidateSkillCastHeal();
                ValidateCombatRoomShield();
                ValidateDeathWard();
                ValidateKillFrenzy();
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                Physics2D.SyncTransforms();
            }

            Debug.Log("Artifact-2 verification passed: effect types 47 to 60 and the item explosion damage source are " +
                      "appended, the 14 confirmed artifacts match their contracts and descriptions and sit in the " +
                      "selection reward pool, burn renews without stacking and ticks every 0.5 seconds as periodic " +
                      "player damage, shock stacks to 4 and slows by 10% per stack (bosses half), status tick " +
                      "bonuses add up, burning and shocked targets take their direct damage, skill and critical " +
                      "bonuses only from the hit types that grant them, 림의 낫 executes non-boss enemies at 20% " +
                      "with the kill credited once, 폭발 머핀 explodes on every 8th hit without counting its own " +
                      "damage, 네르의 엘드르 깃발 heals every 3rd (2nd) cast, 긴급 보호 벨트 stops at 15 hearts, " +
                      "레비의 단도 cancels one lethal hit and 슈슈슈슉 글러브 stacks to 3, ends together and " +
                      "waits out its cooldown.");
        }

        // Also re-runs the checks of the systems these artifacts build on.
        [MenuItem("Trickal Fan Game/Week 23/Verify Artifact-2 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week23Passive1Verification.VerifyWithRegressionsBatch();
            Week23Passive2Verification.Verify();
            Week23Obstacle7Verification.Verify();
            foreach (Action check in new Action[]
                     {
                         PhaseGCombatFoundationVerification.Verify, PhaseGConditionalEffectsVerification.Verify,
                         Week7PlayerCombatEventsVerification.Verify, Hp4LifeGemVerification.Verify,
                         Week16Artifact1Verification.Verify,
                     })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            Week22Jjangsem0Verification.Verify();
            Week22Spell5Verification.Verify();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            Week20DevPanelVerification.Verify();
            Debug.Log("Artifact-2 regression verification passed.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.BasicAttackDamagePercent == 46 &&
                   (int)ItemEffectType.HealOnLowerGradeSkillEveryN == 47 &&
                   (int)ItemEffectType.ShieldOnCombatRoomEntry == 48 &&
                   (int)ItemEffectType.ExecuteBelowHealth == 49 &&
                   (int)ItemEffectType.BasicAttackHitExplosion == 50 &&
                   (int)ItemEffectType.BasicAttackBurn == 51 &&
                   (int)ItemEffectType.BasicAttackShock == 52 &&
                   (int)ItemEffectType.StatusTickDamagePercent == 53 &&
                   (int)ItemEffectType.DirectDamagePercentVsBurning == 54 &&
                   (int)ItemEffectType.SkillDamagePercentVsShocked == 55 &&
                   (int)ItemEffectType.CriticalBonusVsShocked == 56 &&
                   (int)ItemEffectType.CriticalDamage == 57 &&
                   (int)ItemEffectType.NegateLethalDamageOnce == 58 &&
                   (int)ItemEffectType.KillFrenzy == 59 &&
                   (int)ItemEffectType.KillFrenzyKnockbackPercent == 60,
                "Artifact-2 must append effect types 47 to 60 without renumbering earlier effects.");
            Assert((int)DamageSourceType.PlayerStatusEffect == 11 && (int)DamageSourceType.PlayerItemExplosion == 12 &&
                   PlayerCombatEvents.IsPlayerDamage(DamageSourceType.PlayerItemExplosion) &&
                   !PlayerCombatEvents.IsPlayerDamage(DamageSourceType.PlayerBomb),
                "The item explosion damage source must be appended as player damage.");
            Assert(HealthUnits.MaximumHealthAndShieldUnits == 30,
                "Current health and shield together must stop at 15 hearts.");

            Assert(new ItemEffectEntry(ItemEffectType.BasicAttackBurn, 0.2f, configuredSecondaryMagnitude: 0.2f,
                           configuredIntervalSeconds: 0.5f, configuredDurationSeconds: 3f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.BasicAttackBurn, 0.2f, configuredSecondaryMagnitude: 0.2f,
                       configuredDurationSeconds: 3f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.BasicAttackBurn, 1.2f, configuredSecondaryMagnitude: 0.2f,
                       configuredIntervalSeconds: 0.5f, configuredDurationSeconds: 3f).TryValidate(out _),
                "The burn effect needs a chance within (0, 1], a tick damage ratio, duration and interval.");
            Assert(new ItemEffectEntry(ItemEffectType.BasicAttackShock, 0.2f, configuredSecondaryMagnitude: 0.1f,
                           configuredIntegerAmount: 4, configuredDurationSeconds: 2.5f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.BasicAttackShock, 0.2f, configuredSecondaryMagnitude: 0.1f,
                       configuredDurationSeconds: 2.5f).TryValidate(out _),
                "The shock effect needs a chance, a slow per stack, a duration and a maximum stack count.");
            Assert(new ItemEffectEntry(ItemEffectType.NegateLethalDamageOnce, configuredDurationSeconds: 5f)
                       .TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.NegateLethalDamageOnce).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.ExecuteBelowHealth, 1f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.BasicAttackHitExplosion, 1f, configuredIntegerAmount: 8)
                       .TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.HealOnLowerGradeSkillEveryN, 1f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.KillFrenzy, 0.05f, configuredIntegerAmount: 3)
                       .TryValidate(out _),
                "The death ward, execution, explosion, cast heal and kill stack effects must reject missing values.");
        }

        private static void ValidateAssetsAndPool()
        {
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>(FindObjectsInactive.Include);
            Assert(assembler != null, "Artifact-2 verification needs the Game Scene assembler.");
            Assert(Week23Artifact2Setup.Artifacts.Length == 14 &&
                   Week23Artifact2Setup.Artifacts.Select(spec => spec.ItemId).Distinct().Count() == 14,
                "Artifact-2 must define 14 artifacts with unique IDs.");

            foreach (Week23Artifact2Setup.ArtifactSpec spec in Week23Artifact2Setup.Artifacts)
            {
                ItemDefinition item = LoadItem(spec.ItemId);
                Assert(item.DisplayName == spec.DisplayName && item.Kind == ItemKind.Artifact &&
                       item.Rarity == spec.Rarity && item.IsActive && item.MaxStacks == spec.MaxStacks &&
                       item.Effects.Count == spec.Effects.Length,
                    $"{spec.ItemId} does not match its name, rarity, stack limit or effect count.");
                for (int index = 0; index < spec.Effects.Length; index++)
                {
                    ItemEffectEntry expected = spec.Effects[index];
                    ItemEffectEntry actual = item.Effects[index];
                    Assert(actual.EffectType == expected.EffectType &&
                           Mathf.Approximately(actual.Magnitude, expected.Magnitude) &&
                           Mathf.Approximately(actual.SecondaryMagnitude, expected.SecondaryMagnitude) &&
                           actual.IntegerAmount == expected.IntegerAmount &&
                           Mathf.Approximately(actual.Radius, expected.Radius) &&
                           Mathf.Approximately(actual.IntervalSeconds, expected.IntervalSeconds) &&
                           Mathf.Approximately(actual.DurationSeconds, expected.DurationSeconds),
                        $"{spec.ItemId} effect {index} ({expected.EffectType}) does not match its contract.");
                }

                string description = ArtifactEffectDescription.Build(item);
                Assert(description == spec.Description,
                    $"{spec.ItemId} must read '{spec.Description}', but reads '{description}'.");
                Assert(assembler.SelectionRewardPool.Count(pooled => pooled == item) == 1,
                    $"The selection reward pool must offer {spec.ItemId} once.");
            }

            Assert(assembler.SelectionRewardPool.All(item => item != null && item.Kind == ItemKind.Artifact &&
                                                             !GoldenChestExclusivePool.IsExclusive(item)),
                "The selection reward pool must hold only non-exclusive artifacts.");

            ItemDefinition[] assets = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null).ToArray();
            AssertUsers(assets, ItemEffectType.BasicAttackBurn, Week23Artifact2Setup.BowId,
                Week23Artifact2Setup.BranchId);
            AssertUsers(assets, ItemEffectType.BasicAttackShock, Week23Artifact2Setup.GunId);
            AssertUsers(assets, ItemEffectType.StatusTickDamagePercent, Week23Artifact2Setup.SwordId,
                Week23Artifact2Setup.RingId);
            AssertUsers(assets, ItemEffectType.ExecuteBelowHealth, Week23Artifact2Setup.ScytheId);
            AssertUsers(assets, ItemEffectType.NegateLethalDamageOnce, Week23Artifact2Setup.DaggerId);
            AssertUsers(assets, ItemEffectType.KillFrenzy, Week23Artifact2Setup.GloveId);
            AssertUsers(assets, ItemEffectType.ProjectileSpeedPercent, Week23Artifact2Setup.WindArrowId);
        }

        private static void ValidateStatArtifacts()
        {
            GameObject root = new("Artifact-2 Stat Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Health health = player.GetComponent<Health>();
                float baseHealth = stats.MaxHealth;
                float baseMove = stats.MoveSpeed;

                ItemDefinition hairpin = LoadItem(Week23Artifact2Setup.HairpinId);
                Assert(inventory.TryAcquire(hairpin) && inventory.TryAcquire(hairpin) && inventory.TryAcquire(hairpin) &&
                       !inventory.TryAcquire(hairpin) && Near(stats.MaxHealth, baseHealth + 6f) &&
                       Near(health.CurrentHealth, baseHealth + 6f),
                    "Three 돈까스 모양 머리핀 must add and fill 3 hearts, and a fourth must be rejected.");

                ItemDefinition arrow = LoadItem(Week23Artifact2Setup.WindArrowId);
                Assert(inventory.TryAcquire(arrow) && Near(stats.AttackSpeed, 1.15f) &&
                       Near(stats.MoveSpeed, baseMove * 1.05f) && Near(stats.ProjectileSpeedMultiplier, 1.15f),
                    "실라의 바람살 must give +15% attack speed, +5% move speed and +15% shot speed.");
                Assert(inventory.TryAcquire(arrow) && !inventory.TryAcquire(arrow) && Near(stats.AttackSpeed, 1.3f) &&
                       Near(stats.MoveSpeed, baseMove * 1.1f) && Near(stats.ProjectileSpeedMultiplier, 1.3f),
                    "실라의 바람살 must stack twice.");

                ItemDefinition sword = LoadItem(Week23Artifact2Setup.SwordId);
                ItemDefinition ring = LoadItem(Week23Artifact2Setup.RingId);
                Assert(inventory.TryAcquire(sword) && inventory.TryAcquire(sword) && !inventory.TryAcquire(sword) &&
                       Near(stats.AttackDamage, 11f) && Near(stats.StatusTickDamagePercentBonus, 0.2f),
                    "Two 앗따검 must give +10% attack damage and +20% status tick damage.");
                Assert(Near(stats.CriticalDamageMultiplier, 1.5f) && inventory.TryAcquire(ring) &&
                       inventory.TryAcquire(ring) && !inventory.TryAcquire(ring) &&
                       Near(stats.CriticalDamageMultiplier, 2.1f) && Near(stats.StatusTickDamagePercentBonus, 0.8f) &&
                       Near(stats.BasicAttackHitEffects.StatusTickDamageMultiplier, 1.8f),
                    "Two 탐욕의 반지 must give +60%p critical damage and add their +60% tick damage to 앗따검.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBurnStatus()
        {
            GameObject root = new("Artifact-2 Burn Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerCombatEvents events = player.GetComponent<PlayerCombatEvents>();
                int basicHits = 0;
                int kills = 0;
                events.BasicAttackHit += _ => basicHits++;
                events.EnemyKilled += _ => kills++;

                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                List<DamageContext> ticks = new();
                enemy.DamageResolved += (context, _) => ticks.Add(context);
                Assert(!EnemyStatusEffects.TryApplyBurn(enemy, player, 10f, Burn, 0.2f) &&
                       enemy.GetComponent<EnemyStatusEffects>() == null,
                    "A failed roll must not burn the enemy.");
                Assert(EnemyStatusEffects.TryApplyBurn(enemy, player, 10f, Burn, 0.19f),
                    "A roll below the chance must burn the enemy.");
                EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
                status.Clear();
                Assert(!status.IsBurning, "Clearing the status must remove the burn.");

                status.ApplyBurn(player, 10f, Burn, Start);
                Assert(status.IsBurning && Near(status.BurnTickDamage, 2f) && Near(status.BurnEndTime, Start + 3f) &&
                       Near(status.NextBurnTickTime, Start + 0.5f),
                    "Burn lasts 3 seconds, deals 20% of the attack damage and first ticks half a second later.");
                status.Tick(Start + 0.49f);
                Assert(ticks.Count == 0, "Burn must not tick before its first interval.");
                status.Tick(Start + 0.5f);
                Assert(ticks.Count == 1 && Near(enemy.CurrentHealth, 98f) &&
                       ticks[0].DeliveryType == DamageDeliveryType.Periodic &&
                       ticks[0].SourceType == DamageSourceType.PlayerStatusEffect && ticks[0].Source == player &&
                       !ticks[0].CanCritical && basicHits == 0,
                    "A burn tick is periodic player damage: no critical hit and no basic attack hit event.");

                // Burn never stacks: a new application renews the duration and its tick damage only.
                status.ApplyBurn(player, 20f, Burn, Start + 1f);
                Assert(status.IsBurning && Near(status.BurnTickDamage, 4f) && Near(status.BurnEndTime, Start + 4f) &&
                       Near(status.NextBurnTickTime, Start + 1f),
                    "Renewing burn must keep one burn, refresh 3 seconds and keep the tick schedule.");
                status.Tick(Start + 1f);
                Assert(ticks.Count == 2 && Near(enemy.CurrentHealth, 94f), "A renewed burn must tick once per interval.");
                status.Tick(Start + 4.1f);
                Assert(ticks.Count == 8 && Near(enemy.CurrentHealth, 70f) && !status.IsBurning,
                    "Burn must deal its remaining ticks and then end.");
                status.Tick(Start + 20f);
                Assert(ticks.Count == 8, "An expired burn must stop dealing damage.");

                status.ApplyBurn(player, 10f, Burn, Start + 30f, 1.4f);
                Assert(Near(status.BurnTickDamage, 2.8f), "The status tick bonus must multiply the burn tick damage.");
                status.ApplyPoison(player, 10f, new PoisonSettings(1f, 0.15f, 4f, 1f, 3), Start + 30f, 1.4f);
                Assert(Near(status.PoisonTickDamagePerStack, 2.1f) && status.IsPoisoned && status.IsBurning,
                    "The status tick bonus must multiply poison too, and poison and burn coexist.");
                status.Clear();

                Health weak = CreateEnemy(root.transform, Origin + Vector2.right * 5f, 3f);
                EnemyStatusEffects weakStatus = weak.gameObject.AddComponent<EnemyStatusEffects>();
                weakStatus.ApplyBurn(player, 10f, Burn, Start);
                weakStatus.Tick(Start + 2f);
                Assert(weak.IsDead && kills == 1 && !weakStatus.IsBurning,
                    "A burn tick that kills must credit the player exactly once and end the burn.");
                Assert(!EnemyStatusEffects.TryApplyBurn(weak, player, 10f, Burn, 0f), "A dead enemy must not be burned.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateShockStatus()
        {
            GameObject root = new("Artifact-2 Shock Verification");
            try
            {
                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                Assert(Near(EnemyStatusEffects.MoveSpeedMultiplierOf(enemy), 1f),
                    "An enemy without a status moves at full speed.");
                Assert(!EnemyStatusEffects.TryApplyShock(enemy, Shock, 0.2f) &&
                       EnemyStatusEffects.TryApplyShock(enemy, Shock, 0.19f),
                    "Shock must be applied only when the roll is below the chance.");
                EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
                status.Clear();

                status.ApplyShock(Shock, Start);
                Assert(status.IsShocked && status.ShockStacks == 1 && Near(status.MoveSpeedMultiplier, 0.9f) &&
                       Near(status.ShockEndTime, Start + 2.5f),
                    "One shock stack slows by 10% for 2.5 seconds.");
                for (int index = 0; index < 5; index++) status.ApplyShock(Shock, Start + 1f);
                Assert(status.ShockStacks == 4 && Near(status.MoveSpeedMultiplier, 0.6f) &&
                       Near(EnemyStatusEffects.MoveSpeedMultiplierOf(enemy), 0.6f) &&
                       Near(status.ShockEndTime, Start + 3.5f) && Near(enemy.CurrentHealth, 100f),
                    "Shock stops at 4 stacks (-40%), renews its duration and deals no damage.");
                status.Tick(Start + 3.4f);
                Assert(status.ShockStacks == 4, "Shock must last until its renewed duration ends.");
                status.Tick(Start + 3.5f);
                Assert(!status.IsShocked && Near(status.MoveSpeedMultiplier, 1f),
                    "Every shock stack must end together and restore the move speed.");

                GameObject bossObject = new("Artifact-2 Boss", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(BossController));
                bossObject.transform.SetParent(root.transform);
                bossObject.transform.position = Origin + Vector2.up * 10f;
                Health boss = bossObject.GetComponent<Health>();
                Invoke(boss, "Awake");
                EnemyStatusEffects bossStatus = bossObject.AddComponent<EnemyStatusEffects>();
                for (int index = 0; index < 4; index++) bossStatus.ApplyShock(Shock, Start);
                Assert(bossStatus.ShockStacks == 4 && Near(bossStatus.MoveSpeedMultiplier, 0.8f),
                    "A boss must take half of the shock slow (-5% per stack, -20% at most).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateStatusFromItemsAndProjectile()
        {
            GameObject root = new("Artifact-2 Status Item Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Assert(!stats.HasBurnSource && !stats.BasicAttackBurn.IsEnabled && !stats.BasicAttackShock.IsEnabled,
                    "Without a status artifact a basic attack carries no burn or shock.");

                ItemDefinition bow = LoadItem(Week23Artifact2Setup.BowId);
                Assert(inventory.TryAcquire(bow) && stats.HasBurnSource && Near(stats.AttackDamage, 10.3f) &&
                       Near(stats.BasicAttackBurn.Chance, 0.2f) && Near(stats.BasicAttackBurn.TickDamageRatio, 0.2f) &&
                       Near(stats.BasicAttackBurn.DurationSeconds, 3f) &&
                       Near(stats.BasicAttackBurn.IntervalSeconds, 0.5f),
                    "활활 불타활 must give +3% attack damage, a 20% burn chance and count as a burn source.");
                Assert(inventory.TryAcquire(bow) && !inventory.TryAcquire(bow) &&
                       Near(stats.BasicAttackBurn.Chance, 0.4f),
                    "A second 활활 불타활 must add its burn chance (40%).");
                Assert(inventory.TryAcquire(LoadItem(Week23Artifact2Setup.BranchId)) &&
                       Near(stats.BasicAttackBurn.Chance, 0.5f) &&
                       Near(stats.BasicAttackHitEffects.Burn.Chance, 0.5f),
                    "불타는 가지 must add its 10% burn chance to 활활 불타활.");

                ItemDefinition gun = LoadItem(Week23Artifact2Setup.GunId);
                Assert(inventory.TryAcquire(gun) && inventory.TryAcquire(gun) && !inventory.TryAcquire(gun) &&
                       Near(stats.BasicAttackShock.Chance, 0.4f) && Near(stats.BasicAttackShock.SlowPerStack, 0.1f) &&
                       Near(stats.BasicAttackShock.DurationSeconds, 2.5f) &&
                       stats.BasicAttackShock.MaximumStacks == 4 &&
                       Near(stats.BasicAttackHitEffects.Shock.Chance, 0.4f),
                    "Two 앗땃따건 must give a 40% shock chance (2.5s, -10% per stack, 4 stacks).");

                Health owner = player.GetComponent<Health>();
                DamageContext damage = new(player, DamageSourceType.PlayerProjectile, 10f);
                ProjectileHitEffects always = new(true, default, new BurnSettings(1f, 0.2f, 3f, 0.5f),
                    new ShockSettings(1f, 0.1f, 2.5f, 4), 0.4f);
                Health enemy = CreateEnemy(root.transform, Origin, 100f);
                Hit(Launch(root.transform, Origin, Vector2.right * 8f, owner, damage, always), enemy);
                EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
                Assert(Near(enemy.CurrentHealth, 90f) && status != null && status.IsBurning &&
                       Near(status.BurnTickDamage, 2.8f) && status.ShockStacks == 1 && !status.IsPoisoned,
                    "A basic attack hit must burn (with the tick bonus) and shock the enemy after its damage.");

                Health plain = CreateEnemy(root.transform, Origin + Vector2.up * 5f, 100f);
                Hit(Launch(root.transform, plain.transform.position, Vector2.right * 8f, owner, damage,
                    new ProjectileHitEffects(true)), plain);
                Assert(plain.GetComponent<EnemyStatusEffects>() == null,
                    "A shot without status effects must not burn or shock.");

                Health killed = CreateEnemy(root.transform, Origin + Vector2.up * 10f, 5f);
                Hit(Launch(root.transform, killed.transform.position, Vector2.right * 8f, owner, damage, always),
                    killed);
                Assert(killed.IsDead && killed.GetComponent<EnemyStatusEffects>() == null,
                    "An enemy killed by the hit must not be burned or shocked.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBurningTargetBonus()
        {
            GameObject root = new("Artifact-2 Burning Target Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.BranchId)),
                    "불타는 가지 must be acquirable.");

                Health burning = CreateEnemy(root.transform, Origin, 1000f);
                burning.gameObject.AddComponent<EnemyStatusEffects>().ApplyBurn(player, 10f, Burn, Start);
                Assert(Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerProjectile, 10f)), 12.5f) &&
                       Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerSkillExplosion, 10f)),
                           12.5f) &&
                       Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerItemLightning, 10f)),
                           12.5f) &&
                       Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerItemExplosion, 10f)),
                           12.5f),
                    "Basic attacks, skills and artifact hits must deal +25% to a burning enemy.");
                Assert(Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerStatusEffect, 10f,
                           deliveryType: DamageDeliveryType.Periodic)), 10f) &&
                       Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerDamageAura, 10f,
                           deliveryType: DamageDeliveryType.Periodic)), 10f) &&
                       Near(Dealt(burning, new DamageContext(player, DamageSourceType.PlayerBomb, 10f)), 10f),
                    "Status ticks, auras and bombs must not gain the burning target bonus.");

                Health plain = CreateEnemy(root.transform, Origin + Vector2.up * 5f, 1000f);
                Assert(Near(Dealt(plain, new DamageContext(player, DamageSourceType.PlayerProjectile, 10f)), 10f),
                    "An enemy that is not burning must take the plain damage.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateShockedTargetBonus()
        {
            GameObject root = new("Artifact-2 Shocked Target Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.EPadId)) &&
                       Near(stats.SkillDamageMultiplier, 1.08f),
                    "아멜리아의 E-Pad 클래식 must give +8% skill damage.");

                Health shocked = CreateEnemy(root.transform, Origin, 1000f);
                shocked.gameObject.AddComponent<EnemyStatusEffects>().ApplyShock(Shock, Start);
                Health plain = CreateEnemy(root.transform, Origin + Vector2.up * 5f, 1000f);
                DamageContext skill = stats.CreateDirectDamageContext(player, DamageSourceType.PlayerSkillExplosion,
                    stats.SkillDamageMultiplier);
                DamageContext ultimate = stats.CreateDirectDamageContext(player, DamageSourceType.PlayerUltimateImpact,
                    stats.SkillDamageMultiplier);
                DamageContext basic = stats.CreateDirectDamageContext(player, DamageSourceType.PlayerProjectile);

                // The critical roll is 1 here, so nothing is a critical hit.
                Assert(Near(Dealt(plain, skill), 10.8f) && Near(Dealt(shocked, skill), 16.8f) &&
                       Near(Dealt(shocked, ultimate), 16.8f),
                    "Skills must deal +60% (added to the skill damage stat) to a shocked enemy.");
                Assert(Near(Dealt(shocked, basic), 10f),
                    "A basic attack must not gain the skill damage bonus against a shocked enemy.");

                // Base critical chance 5%: a roll of 0.35 is a critical hit only with the +33.61%p bonus.
                DamageCalculator.SetCriticalRollProviderForTesting(() => 0.35f);
                Assert(Near(Dealt(plain, basic), 10f) && Near(Dealt(shocked, basic), 18.361f) &&
                       Near(Dealt(shocked, skill), 16.8f * 1.8361f),
                    "Basic attacks and skills must gain +33.61%p critical chance and damage against a shocked enemy.");
                Assert(Near(Dealt(shocked, new DamageContext(player, DamageSourceType.PlayerItemLightning, 10f, 1.5f)),
                        15f),
                    "Artifact hits must not gain the shocked target bonuses.");
            }
            finally
            {
                DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateExecution()
        {
            GameObject root = new("Artifact-2 Execution Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                int kills = 0;
                player.GetComponent<PlayerCombatEvents>().EnemyKilled += _ => kills++;
                Health unarmed = CreateEnemy(root.transform, Origin, 100f);
                unarmed.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 90f));
                Assert(!unarmed.IsDead, "Without 림의 낫 a weakened enemy must survive.");

                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.ScytheId)) &&
                       Near(stats.AttackDamage, 10.8f) && Near(stats.ExecuteHealthThreshold, 0.2f),
                    "림의 낫 must give +8% attack damage and a 20% execution threshold.");

                Health enemy = CreateEnemy(root.transform, Origin + Vector2.up * 5f, 100f);
                enemy.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 79f));
                Assert(!enemy.IsDead && Near(enemy.CurrentHealth, 21f), "An enemy above 20% health must survive.");
                enemy.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 1f));
                Assert(enemy.IsDead && Near(enemy.CurrentHealth, 0f) && kills == 1,
                    "An enemy left at 20% health must be executed and credited to the player once.");

                Health ticked = CreateEnemy(root.transform, Origin + Vector2.up * 10f, 100f);
                ticked.TakeDamage(new DamageContext(player, DamageSourceType.PlayerStatusEffect, 85f,
                    deliveryType: DamageDeliveryType.Periodic));
                Assert(ticked.IsDead && kills == 2, "A status tick is player damage and must execute too.");

                Health bombed = CreateEnemy(root.transform, Origin + Vector2.up * 15f, 100f);
                bombed.TakeDamage(new DamageContext(player, DamageSourceType.PlayerBomb, 85f));
                Health other = CreateEnemy(root.transform, Origin + Vector2.up * 20f, 100f);
                other.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 85f));
                Assert(!bombed.IsDead && !other.IsDead && kills == 2,
                    "Damage that is not credited to the player must not execute.");

                GameObject bossObject = new("Artifact-2 Boss", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(BossController));
                bossObject.transform.SetParent(root.transform);
                bossObject.transform.position = Origin + Vector2.up * 25f;
                Health boss = bossObject.GetComponent<Health>();
                SetMaxHealth(boss, 100f);
                boss.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 85f));
                Assert(!boss.IsDead && Near(boss.CurrentHealth, 15f) && kills == 2, "A boss must never be executed.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateExplosion()
        {
            GameObject root = new("Artifact-2 Explosion Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.MuffinId)) &&
                       Near(stats.CriticalChance, 0.1f),
                    "폭발 머핀 must give +5%p critical chance.");
                PlayerBasicAttackExplosion explosion = player.GetComponent<PlayerBasicAttackExplosion>();
                Assert(explosion != null && explosion.IsConfigured && Near(explosion.Radius, 1.5f),
                    "폭발 머핀 must configure a 1.5 radius explosion.");
                int basicHits = 0;
                player.GetComponent<PlayerCombatEvents>().BasicAttackHit += _ => basicHits++;

                Health target = CreateEnemy(root.transform, Origin, 1000f);
                Health near = CreateEnemy(root.transform, Origin + Vector2.right, 1000f);
                Health far = CreateEnemy(root.transform, Origin + Vector2.right * 3f, 1000f);
                Physics2D.SyncTransforms();
                DamageContext basic = new(player, DamageSourceType.PlayerProjectile, 10f);
                for (int index = 0; index < 7; index++) target.TakeDamage(basic);
                Assert(Near(target.CurrentHealth, 930f) && Near(near.CurrentHealth, 1000f) &&
                       explosion.HitProgress == 7 && explosion.TriggerCount == 0,
                    "Seven basic attack hits must not explode.");

                target.TakeDamage(basic);
                Assert(explosion.TriggerCount == 1 && explosion.HitProgress == 0 &&
                       Near(target.CurrentHealth, 910f) && Near(near.CurrentHealth, 990f) &&
                       Near(far.CurrentHealth, 1000f) && basicHits == 8,
                    "The 8th hit must explode for 100% of the attack damage within 1.5 and not count as a hit: " +
                    $"target {target.CurrentHealth}, near {near.CurrentHealth}, far {far.CurrentHealth}, " +
                    $"hits {basicHits}.");
                Assert(near.GetComponent<EnemyStatusEffects>() == null &&
                       !near.GetComponent<KnockbackReceiver>().IsPushed,
                    "The explosion must carry no status effect or knockback.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSkillCastHeal()
        {
            GameObject root = new("Artifact-2 Flag Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin, typeof(PlayerSkill));
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Health health = player.GetComponent<Health>();
                float baseHealth = stats.MaxHealth;
                ItemDefinition flag = LoadItem(Week23Artifact2Setup.FlagId);

                Assert(inventory.TryAcquire(flag) && Near(stats.MaxHealth, baseHealth + 2f),
                    "네르의 엘드르 깃발 must add 1 heart of maximum health.");
                PlayerSkillCastHeal castHeal = player.GetComponent<PlayerSkillCastHeal>();
                Assert(castHeal != null && castHeal.RequiredCastCount == 3, "The flag must heal every 3rd cast.");
                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 4f));
                float damaged = health.CurrentHealth;
                Assert(Near(damaged, baseHealth - 2f), "The verification hit must cost 2 hearts.");

                // The skill reports a successful cast through SalvoStarted.
                FieldInfo salvoStarted = typeof(PlayerSkill).GetField("SalvoStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(salvoStarted != null, "PlayerSkill.SalvoStarted was renamed; update the flag cast check.");
                Action cast = (Action)salvoStarted.GetValue(player.GetComponent<PlayerSkill>());
                Assert(cast != null, "The flag must listen to the lower-grade skill cast.");
                cast();
                cast();
                Assert(Near(health.CurrentHealth, damaged) && castHeal.CastProgress == 2,
                    "Two casts must not heal yet.");
                cast();
                Assert(Near(health.CurrentHealth, damaged + 1f) && castHeal.CastProgress == 0,
                    "The 3rd cast must heal half a heart.");

                Assert(inventory.TryAcquire(flag) && !inventory.TryAcquire(flag) &&
                       Near(stats.MaxHealth, baseHealth + 4f) && castHeal.RequiredCastCount == 2,
                    "A second flag must add another heart and heal every 2nd cast; a third must be rejected.");
                health.Heal(100f);
                Assert(Near(castHeal.RegisterCast(), 0f) && Near(castHeal.RegisterCast(), 0f) &&
                       castHeal.CastProgress == 0,
                    "At full health the cast count must still advance and the heal is dropped.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateCombatRoomShield()
        {
            GameObject root = new("Artifact-2 Belt Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                Health health = player.GetComponent<Health>();
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.BeltId)),
                    "긴급 보호 벨트 must be acquirable.");
                PlayerCombatRoomShield belt = player.GetComponent<PlayerCombatRoomShield>();
                Assert(belt != null && Near(belt.ShieldAmount, 1f) && Near(health.CurrentShield, 0f),
                    "The belt gives half a heart of shield per combat room and nothing when acquired.");
                Assert(Near(belt.GrantForCombatRoomEntry(), 1f) && Near(belt.GrantForCombatRoomEntry(), 1f) &&
                       Near(health.CurrentShield, 2f),
                    "Each combat room entry must add half a heart of shield to the current shield.");

                float limit = HealthUnits.MaximumHealthAndShieldUnits;
                health.SetShield(limit - health.CurrentHealth - 1f);
                Assert(Near(belt.GrantForCombatRoomEntry(), 1f) &&
                       Near(health.CurrentHealth + health.CurrentShield, limit) &&
                       Near(belt.GrantForCombatRoomEntry(), 0f) &&
                       Near(health.CurrentHealth + health.CurrentShield, limit),
                    "The belt must stop when health and shield together reach 15 hearts.");
                belt.BindRunProgress(null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateDeathWard()
        {
            GameObject root = new("Artifact-2 Dagger Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Health health = player.GetComponent<Health>();
                float baseMove = stats.MoveSpeed;
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.DaggerId)) &&
                       Near(stats.AttackDamage, 10.8f) && Near(stats.MoveSpeed, baseMove * 1.03f),
                    "레비의 단도 must give +8% attack damage and +3% move speed.");
                PlayerDeathWard ward = player.GetComponent<PlayerDeathWard>();
                Assert(ward != null && ward.IsConfigured && !ward.IsSpent, "The dagger must arm its death ward.");

                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 2f));
                float before = health.CurrentHealth;
                Assert(!ward.IsSpent && Near(before, stats.MaxHealth - 2f), "A hit that does not kill must not spend the ward.");
                ClearInvulnerability(player);

                health.SetShield(2f);
                int changes = 0;
                bool died = false;
                health.Changed += (_, _) => changes++;
                health.Died += () => died = true;
                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, before + 2f));
                Assert(ward.IsSpent && !health.IsDead && !died && changes == 0 && Near(health.CurrentHealth, before) &&
                       Near(health.CurrentShield, 2f) && health.IsInvulnerable,
                    "The first lethal hit must be cancelled whole (health and shield unchanged) and start invulnerability.");
                Assert(Near(stats.AttackDamage, 10.8f) && Near(stats.MoveSpeed, baseMove * 1.03f),
                    "The dagger's stats must stay after the ward is spent.");

                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 100f));
                Assert(!health.IsDead && Near(health.CurrentHealth, before), "The player must be invulnerable after the ward.");
                ClearInvulnerability(player);
                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 100f));
                Assert(health.IsDead && died, "The ward must work only once per Run.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateKillFrenzy()
        {
            GameObject root = new("Artifact-2 Glove Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.GloveId)) &&
                       Near(stats.AttackDamage, 10.8f),
                    "슈슈슈슉 글러브 must give +8% attack damage.");
                PlayerKillFrenzy frenzy = player.GetComponent<PlayerKillFrenzy>();
                Assert(frenzy != null && frenzy.IsConfigured && frenzy.Stacks == 0 &&
                       Near(stats.BasicAttackRoomDamageMultiplier, 1f) && Near(stats.AttackSpeed, 1f) &&
                       Near(stats.BasicAttackHitEffects.KnockbackBonus, 0f),
                    "The glove gives nothing before a kill.");

                Health enemy = CreateEnemy(root.transform, Origin, 1f);
                enemy.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 5f));
                float now = Time.time;
                AssertFrenzy(frenzy, stats, 1);
                Assert(Near(frenzy.EndTime, now + 5f), "A kill must start 5 seconds of the glove's bonus.");

                Assert(frenzy.RegisterKill(now + 1f), "A kill during the bonus must count.");
                AssertFrenzy(frenzy, stats, 2);
                Assert(frenzy.RegisterKill(now + 2f) && frenzy.RegisterKill(now + 3f),
                    "Kills during the bonus must keep counting.");
                AssertFrenzy(frenzy, stats, 3);
                Assert(Near(frenzy.EndTime, now + 8f), "Each kill must renew the 5 seconds.");
                Assert(Near(BasicAttackKnockback.ResolveSpeed(8f, 1f, stats.BasicAttackHitEffects.KnockbackBonus),
                        2.9f),
                    "Three stacks must push 45% harder.");

                frenzy.Tick(now + 7.9f);
                AssertFrenzy(frenzy, stats, 3);
                frenzy.Tick(now + 8.01f);
                AssertFrenzy(frenzy, stats, 0);
                Assert(Near(frenzy.CooldownEndTime, now + 18f),
                    "Every stack must end together and start a 10 second cooldown.");
                Assert(!frenzy.RegisterKill(now + 17.9f) && frenzy.Stacks == 0,
                    "A kill during the cooldown must not count.");
                Assert(frenzy.RegisterKill(now + 18.02f), "A kill after the cooldown must count again.");
                AssertFrenzy(frenzy, stats, 1);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertFrenzy(PlayerKillFrenzy frenzy, PlayerStats stats, int stacks)
        {
            Assert(frenzy.Stacks == stacks && Near(stats.BasicAttackRoomDamageMultiplier, 1f + 0.05f * stacks) &&
                   Near(stats.AttackSpeed, 1f + 0.08f * stacks) &&
                   Near(stats.BasicAttackHitEffects.KnockbackBonus, 0.15f * stacks) && Near(stats.AttackDamage, 10.8f),
                $"At {stacks} glove stacks basic attacks must deal +{5 * stacks}%, attack {8 * stacks}% faster and " +
                $"push {15 * stacks}% harder, but the glove holds {frenzy.Stacks} stacks.");
        }

        // Returns the health the hit removed.
        private static float Dealt(Health target, DamageContext context)
        {
            float before = target.CurrentHealth;
            target.TakeDamage(context);
            return before - target.CurrentHealth;
        }

        private static void ClearInvulnerability(GameObject player)
        {
            DamageInvulnerability invulnerability = player.GetComponent<DamageInvulnerability>();
            if (invulnerability != null) invulnerability.ResetHitWindow();
        }

        private static void AssertUsers(IEnumerable<ItemDefinition> assets, ItemEffectType type,
            params string[] expectedIds)
        {
            string[] users = assets.Where(definition => definition.Effects.Any(effect => effect.EffectType == type))
                .Select(definition => definition.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert(users.SequenceEqual(expectedIds.OrderBy(id => id, StringComparer.Ordinal)),
                $"{type} must be used only by {string.Join(", ", expectedIds)}, but is used by " +
                $"{string.Join(", ", users)}.");
        }

        private static ItemDefinition LoadItem(string itemId)
        {
            ItemDefinition item =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week23Artifact2Setup.ItemPath(itemId));
            Assert(item != null && item.ItemId == itemId && item.IsValid,
                $"Run Artifact-2 setup first: {itemId} is missing.");
            return item;
        }

        private static Projectile Launch(Transform parent, Vector2 position, Vector2 velocity, Health owner,
            DamageContext damage, ProjectileHitEffects hitEffects)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErpinProjectileArtworkSetup.BasicPrefabPath);
            Assert(prefab != null && prefab.GetComponent<Projectile>() != null,
                "The player projectile Prefab is missing.");
            GameObject shot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            shot.transform.position = position;
            Projectile projectile = shot.GetComponent<Projectile>();
            projectile.Launch(velocity, owner, damage, configuredHitEffects: hitEffects);
            return projectile;
        }

        private static void Hit(Projectile shot, Health target)
        {
            MethodInfo hit = typeof(Projectile).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(hit != null, "Projectile.Hit was renamed; update the Artifact-2 hit checks.");
            hit.Invoke(shot, new object[] { target.GetComponent<Collider2D>() });
        }

        private static GameObject CreatePlayer(Transform parent, Vector2 position, params Type[] extraComponents)
        {
            GameObject player = new("Artifact-2 Player", new[]
            {
                typeof(CircleCollider2D), typeof(PlayerCombatEvents), typeof(PlayerInventory),
            }.Concat(extraComponents).ToArray());
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            Invoke(player.GetComponent<Health>(), "Awake");
            Invoke(player.GetComponent<PlayerSP>(), "Awake");
            Invoke(player.GetComponent<PlayerStats>(), "Awake");
            Invoke(player.GetComponent<PlayerInventory>(), "Awake");
            return player;
        }

        private static Health CreateEnemy(Transform parent, Vector2 position, float maxHealth)
        {
            GameObject enemy = new("Artifact-2 Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(KnockbackReceiver));
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            enemy.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            Health health = enemy.GetComponent<Health>();
            SetMaxHealth(health, maxHealth);
            return health;
        }

        private static void SetMaxHealth(Health health, float maxHealth)
        {
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(health, "Awake");
        }

        private static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(target, null);
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= Tolerance;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
