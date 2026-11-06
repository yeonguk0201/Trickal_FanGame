using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Pool1StackLimitVerification
    {
        private const ItemPoolSource PureStatSources = ItemPoolSource.Boss | ItemPoolSource.Shop;

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Pool-1 Stack Limits")]
        public static void SetupAndVerifyBatch()
        {
            Pool1StackLimitSetup.Setup();
            Pool1StackLimitSetup.Setup();
            Verify();
        }

        // Pool-1 changes the stack limits older artifact checks asserted and the eligibility every reward source and
        // shop uses, so those checks run after it.
        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-1 Stack Limits With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Pool0ItemPoolVerification.Verify();
            PhaseGArtifactContractVerification.Verify();
            PhaseGSimpleEffectsVerification.Verify();
            PhaseGConditionalEffectsVerification.Verify();
            PhaseGRewardRunVerification.Verify();
            Week16Spell1Verification.Verify();
            Week21Range0Verification.Verify();
            Week22Shop0Verification.Verify();
            Week23Artifact2Verification.Verify();
            Week23Passive1Verification.Verify();
            Week25Trap0Verification.Verify();
            Week26Spell6Verification.Verify();
            Debug.Log("Pool-1 stack limit verification with regressions passed.");
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-1 Stack Limits")]
        public static void Verify()
        {
            VerifyAssetLimits();
            VerifyOverallCaps();
            VerifyLastingBonusesOnly();
            VerifyCappedArtifactsLeaveRewards();
            Debug.Log("Pool-1 stack limit verification passed.");
        }

        // Pure-stat artifacts are exactly the Pool-0 pure-stat group and have no limit; every other pooled artifact
        // keeps one.
        private static void VerifyAssetLimits()
        {
            ItemDefinition[] artifacts = Pool0ItemPoolSetup.LoadArtifacts();
            string[] pureStat = Pool0ItemPoolSetup.Sources.Where(pair => pair.Value == PureStatSources)
                .Select(pair => pair.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert(pureStat.SequenceEqual(
                    Pool1StackLimitSetup.UnlimitedItemIds.OrderBy(id => id, StringComparer.Ordinal)),
                "Pool-1 unlimited artifacts must be exactly the Pool-0 pure-stat group (boss and shop pools).");

            foreach (string id in Pool0ItemPoolSetup.Sources.Keys)
            {
                ItemDefinition item = artifacts.Single(candidate => candidate.ItemId == id);
                if (Pool1StackLimitSetup.IsUnlimited(id))
                    Assert(item.MaxStacks == 0, $"{id} is a pure-stat artifact and must have no stack limit. Run Pool-1 setup.");
                else
                    Assert(item.MaxStacks > 0, $"{id} has a special effect and must keep its stack limit.");
            }
        }

        private static void VerifyOverallCaps()
        {
            WithPlayer((stats, _, _) =>
            {
                float baseAttackSpeed = stats.AttackSpeed;
                float baseMove = stats.MoveSpeed;
                stats.AddAttackSpeedPercent(20f);
                stats.AddMoveSpeedPercent(20f);
                stats.AddProjectileSpeedPercent(20f);
                stats.AddProjectileSizePercent(20f);
                stats.AddCriticalChance(20f);
                Assert(Near(stats.AttackSpeed, baseAttackSpeed * PlayerStats.MaximumAttackSpeedMultiplier) &&
                       Near(PlayerStats.MaximumAttackSpeedMultiplier, 5f),
                    "Attack speed must stop at 5 times the base.");
                Assert(Near(stats.MoveSpeed, baseMove * PlayerStats.MaximumMoveSpeedMultiplier) &&
                       Near(PlayerStats.MaximumMoveSpeedMultiplier, 2f),
                    "Move speed must stop at 2 times the base.");
                Assert(Near(stats.ProjectileSpeedMultiplier, 2f), "Shot speed must stop at 2 times.");
                Assert(Near(stats.ProjectileSizeMultiplier, ProjectileSizing.MaximumPlayerBasicSizeMultiplier) &&
                       Near(ProjectileSizing.MaximumPlayerBasicSizeMultiplier, 2.25f),
                    "Basic shot size must stop at 2.25 times.");
                Assert(Near(stats.CriticalChance, 1f), "Critical chance must stop at 100%.");
            });

            WithPlayer((stats, _, _) =>
            {
                float baseMove = stats.MoveSpeed;
                stats.AddMoveSpeedPenaltyPercent(20f);
                stats.AddTarotStep(TarotStat.ShotSpeed, -20f);
                Assert(Near(stats.MoveSpeed, baseMove * PlayerStats.MinimumMoveSpeedMultiplier) &&
                       Near(PlayerStats.MinimumMoveSpeedMultiplier, 0.1f),
                    "Move speed must not drop below 0.1 times the base.");
                Assert(Near(stats.ProjectileSpeedMultiplier, PlayerStats.MinimumProjectileSpeedMultiplier) &&
                       Near(PlayerStats.MinimumProjectileSpeedMultiplier, 0.6f),
                    "Shot speed must not drop below 0.6 times.");
            });

            WithPlayer((stats, _, _) =>
            {
                float baseDamage = stats.AttackDamage;
                float baseCritical = stats.CriticalDamageMultiplier;
                stats.AddAttackDamagePercent(20f);
                stats.AddCriticalDamage(20f);
                Assert(Near(stats.AttackDamage, baseDamage * 21f) && Near(stats.CriticalDamageMultiplier, baseCritical + 20f),
                    "Attack damage and critical damage must have no overall cap.");
                Assert(!stats.IsAtOverallCap(ItemEffectType.AttackDamagePercent) &&
                       !stats.IsAtOverallCap(ItemEffectType.CriticalDamage),
                    "Uncapped stats must never count as capped.");
            });
        }

        // Room, spell and power buffs end, so reaching a cap through them must not hide an artifact.
        private static void VerifyLastingBonusesOnly()
        {
            WithPlayer((stats, _, _) =>
            {
                stats.SetCurrentRoomAttackSpeedPercent(20f);
                stats.SetSingleUseRoomSpeedPercent(20f, 20f);
                stats.SetSingleUseRoomCritical(0f, 5f);
                Assert(Near(stats.AttackSpeed, PlayerStats.MaximumAttackSpeedMultiplier) && Near(stats.CriticalChance, 1f),
                    "Temporary buffs must still be capped.");
                Assert(!stats.IsAtOverallCap(ItemEffectType.AttackSpeedPercent) &&
                       !stats.IsAtOverallCap(ItemEffectType.MoveSpeedPercent) &&
                       !stats.IsAtOverallCap(ItemEffectType.CriticalChance),
                    "Temporary buffs must not count toward hiding an artifact.");
            });
        }

        private static void VerifyCappedArtifactsLeaveRewards()
        {
            ItemDefinition sword = Load("item-01");
            ItemDefinition awl = Load("item-12");
            ItemDefinition hairpin = Load("artifact-pork-cutlet-hairpin");
            ItemDefinition windArrow = Load("artifact-sylla-wind-arrow");
            ItemDefinition cannon = Load("artifact-kanna-cannon");
            ItemDefinition potion = Load("artifact-giant-potion");

            WithPlayer((stats, inventory, _) =>
            {
                // 5% base + 3%p per awl: the 32nd awl reaches 100%.
                int awls = AcquireWhileEligible(inventory, awl, 100);
                Assert(awls == 32 && Near(stats.CriticalChance, 1f) && !ArtifactRewardSelector.IsEligible(awl, inventory),
                    $"녹슨 송곳 must stay offered until critical chance reaches 100% (32 stacks), took {awls}.");
                Assert(ArtifactRewardSelector.TryChoose(new[] { awl, sword }, inventory, 7, "pool1-awl", out ItemDefinition chosen) &&
                       chosen == sword,
                    "A capped artifact must be skipped and the rest rolled.");
                Assert(ArtifactRewardSelector.AreAllActiveArtifactsAtMaximum(new[] { awl }, inventory),
                    "A pool of only capped artifacts must count as exhausted for the healing fallback.");

                for (int index = 0; index < 50; index++) Assert(inventory.TryAcquire(sword), "목검 must keep stacking.");
                Assert(ArtifactRewardSelector.IsEligible(sword, inventory), "급조한 목검 has no cap and must stay offered.");
            });

            WithPlayer((stats, inventory, _) =>
            {
                // Attack speed caps last: 1 + 0.15n >= 5 at n = 27 (move speed at 20, shot speed at 7).
                int arrows = AcquireWhileEligible(inventory, windArrow, 100);
                Assert(arrows == 27 && Near(stats.AttackSpeed, PlayerStats.MaximumAttackSpeedMultiplier) &&
                       Near(stats.ProjectileSpeedMultiplier, 2f),
                    $"실라의 바람살 must stay offered until all three of its stats are capped (27 stacks), took {arrows}.");
            });

            WithPlayer((stats, inventory, health) =>
            {
                // 5 hearts + 1 heart per hairpin: the 10th reaches the 15-heart ceiling.
                int hairpins = AcquireWhileEligible(inventory, hairpin, 100);
                Assert(hairpins == 10 && Near(stats.MaxHealth, HealthUnits.MaximumHealthAndShieldUnits),
                    $"돈까스 모양 머리핀 must stay offered until 15 hearts (10 stacks), took {hairpins}.");
                Assert(!inventory.AreAllEffectsAtOverallCap(potion) && ArtifactRewardSelector.IsEligible(potion, inventory),
                    "An artifact with any uncapped effect must stay offered.");

                int cannons = AcquireWhileEligible(inventory, cannon, 100);
                Assert(cannons == 3 && Near(stats.ProjectileSizeMultiplier, 2.25f),
                    $"칸나의 대포 must stay offered until the 2.25 size cap (3 stacks), took {cannons}.");
            });
        }

        private static int AcquireWhileEligible(PlayerInventory inventory, ItemDefinition item, int limit)
        {
            int count = 0;
            while (ArtifactRewardSelector.IsEligible(item, inventory))
            {
                Assert(count < limit, $"{item.ItemId} never reached an overall cap.");
                Assert(inventory.TryAcquire(item), $"{item.ItemId} must be acquirable while it is offered.");
                count++;
            }

            return count;
        }

        private static void WithPlayer(Action<PlayerStats, PlayerInventory, Health> check)
        {
            GameObject player = new("Pool-1 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerInventory>(), "Awake");
                check(player.GetComponent<PlayerStats>(), player.GetComponent<PlayerInventory>(),
                    player.GetComponent<Health>());
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Pool0ItemPoolSetup.ItemFolder}/{itemId}.asset");
            Assert(item != null, $"Missing artifact asset {itemId}.");
            return item;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
