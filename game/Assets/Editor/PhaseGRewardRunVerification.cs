using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGRewardRunVerification
    {
        private const int DistributionSamples = 20000;

        [MenuItem("Trickal Fan Game/Verify Phase G-7 Rewards and Full Run", priority = 1)]
        public static void Verify()
        {
            ItemDefinition[] active = PhaseGArtifactCatalog.Active
                .Select(spec => LoadDefinition(spec.ItemId))
                .ToArray();
            ItemDefinition inactiveLegacy = LoadDefinition("item-06");
            GameObject player = CreatePlayer(
                out Health health,
                out PlayerStats stats,
                out PlayerSP playerSP,
                out PlayerInventory inventory);
            GameObject progressObject = new("Phase G-7 Run Progress");
            RunProgress progress = progressObject.AddComponent<RunProgress>();
            Assert(progress.TryInitializeRunSeed(20260901, out string error), error);
            GameObject pickupTemplate = new("Phase G-7 Pickup Template");
            pickupTemplate.AddComponent<CircleCollider2D>().isTrigger = true;
            ItemPickup pickup = pickupTemplate.AddComponent<ItemPickup>();
            pickupTemplate.SetActive(false);
            GameObject dropRoot = new("Phase G-7 Drops");

            try
            {
                ValidateWeightsAndDeterminism(active, inactiveLegacy);
                ValidateMaximumStackFiltering(active, inventory);
                ValidateDropAndRevisit(active, progress, inventory, pickup, dropRoot.transform);
                ValidateFullRunPersistence(active, progress, inventory, health, stats, playerSP);
                ValidateFallbackHealing(active, progress, inventory, pickup, dropRoot.transform, health);
                ValidateDeadPlayerFallback(active, progress, inventory, pickup, dropRoot.transform, health);

                string mode = Application.isPlaying ? "Play Mode" : "Edit Mode";
                Debug.Log(
                    $"Phase G-7 rewards and full Run verification passed in {mode}: all 10 active artifacts, " +
                    "COMMON 60 / UNCOMMON 25 / RARE 12 / EPIC 3 candidate weights, deterministic seed/state " +
                    "selection, maximum-stack exclusion, no revisit reroll, claimed-room preservation, fallback " +
                    "healing, and artifact stacks/effects/shield/maximum SP persistence across floors 1-3 are valid.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropRoot);
                UnityEngine.Object.DestroyImmediate(pickupTemplate);
                UnityEngine.Object.DestroyImmediate(progressObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void ValidateWeightsAndDeterminism(
            IReadOnlyList<ItemDefinition> active,
            ItemDefinition inactiveLegacy)
        {
            Assert(active.Count == 10 && active.All(item => item.IsActive),
                "The Phase G reward pool must contain exactly 10 active artifacts.");
            Assert(!inactiveLegacy.IsActive,
                "The legacy item-06 must remain outside the active reward pool.");
            Assert(ArtifactRewardSelector.GetWeight(ItemRarity.Common) == 60 &&
                   ArtifactRewardSelector.GetWeight(ItemRarity.Uncommon) == 25 &&
                   ArtifactRewardSelector.GetWeight(ItemRarity.Rare) == 12 &&
                   ArtifactRewardSelector.GetWeight(ItemRarity.Epic) == 3,
                "Artifact rarity weights must remain COMMON 60 / UNCOMMON 25 / RARE 12 / EPIC 3.");

            ItemDefinition[] representatives =
            {
                active.First(item => item.Rarity == ItemRarity.Common),
                active.First(item => item.Rarity == ItemRarity.Uncommon),
                active.First(item => item.Rarity == ItemRarity.Rare),
                active.First(item => item.Rarity == ItemRarity.Epic),
            };
            Dictionary<ItemRarity, int> counts = Enum.GetValues(typeof(ItemRarity))
                .Cast<ItemRarity>()
                .ToDictionary(rarity => rarity, _ => 0);
            for (int seed = 1; seed <= DistributionSamples; seed++)
            {
                Assert(ArtifactRewardSelector.TryChoose(
                    representatives, null, seed, "distribution", out ItemDefinition selected),
                    "Every distribution sample must select an eligible artifact.");
                counts[selected.Rarity]++;
            }

            AssertRatio(counts[ItemRarity.Common], 0.60f, ItemRarity.Common);
            AssertRatio(counts[ItemRarity.Uncommon], 0.25f, ItemRarity.Uncommon);
            AssertRatio(counts[ItemRarity.Rare], 0.12f, ItemRarity.Rare);
            AssertRatio(counts[ItemRarity.Epic], 0.03f, ItemRarity.Epic);

            ItemDefinition[] reversed = active.Reverse().ToArray();
            Assert(ArtifactRewardSelector.TryChoose(active, null, 777, "floor-02-room-04:treasure", out ItemDefinition first) &&
                   ArtifactRewardSelector.TryChoose(active, null, 777, "floor-02-room-04:treasure", out ItemDefinition repeated) &&
                   ArtifactRewardSelector.TryChoose(reversed, null, 777, "floor-02-room-04:treasure", out ItemDefinition reordered) &&
                   first.ItemId == repeated.ItemId && first.ItemId == reordered.ItemId,
                "Equal seed and reward state must select the same artifact independently of pool ordering.");

            HashSet<string> variedResults = new(StringComparer.Ordinal);
            for (int index = 1; index <= 64; index++)
            {
                ArtifactRewardSelector.TryChoose(active, null, 777,
                    $"floor-01-room-{index:00}:treasure", out ItemDefinition varied);
                variedResults.Add(varied.ItemId);
            }
            Assert(variedResults.Count > 1,
                "Stable reward IDs must produce more than one deterministic result across a floor.");
        }

        private static void ValidateMaximumStackFiltering(
            IReadOnlyList<ItemDefinition> active,
            PlayerInventory inventory)
        {
            ItemDefinition common = active.First(item => item.Rarity == ItemRarity.Common);
            for (int stack = 0; stack < common.MaxStacks; stack++)
            {
                Assert(inventory.TryAcquire(common), "The selected Common artifact must reach its stack cap.");
            }
            Assert(!inventory.TryAcquire(common), "An artifact acquisition beyond maximum stacks must fail.");

            ItemDefinition rare = active.First(item => item.Rarity == ItemRarity.Rare);
            Assert(ArtifactRewardSelector.TryChoose(
                    new[] { common, rare }, inventory, 44, "max-stack-filter", out ItemDefinition selected) &&
                   selected.ItemId == rare.ItemId,
                "A maximum-stack candidate must be excluded before the remaining candidates are rolled.");
            Assert(!ArtifactRewardSelector.TryChoose(
                    new[] { common, LoadDefinition("item-06") }, inventory, 44, "empty-filter", out _),
                "Maximum-stack and inactive artifacts must leave no eligible item candidate.");
            Assert(!ArtifactRewardSelector.AreAllActiveArtifactsAtMaximum(
                    new[] { LoadDefinition("item-06") }, inventory),
                "An inactive-only or misconfigured pool must not qualify for fallback healing.");
        }

        private static void ValidateDropAndRevisit(
            IReadOnlyList<ItemDefinition> active,
            RunProgress progress,
            PlayerInventory inventory,
            ItemPickup pickup,
            Transform dropParent)
        {
            GameObject sourceObject = new("Phase G-7 Deterministic Treasure");
            sourceObject.transform.SetParent(dropParent, false);
            ItemDropSource source = sourceObject.AddComponent<ItemDropSource>();
            source.Configure(pickup, active.ToArray(), sourceObject.transform, dropParent);
            source.ConfigureRewardContext(progress, "floor-01-room-03:treasure");
            Assert(source.TryDrop(inventory) && source.HasDropped && source.LastDroppedDefinition != null,
                "An eligible treasure room must create exactly one artifact pickup.");
            string initialItemId = source.LastDroppedDefinition.ItemId;
            Assert(!source.TryDrop(inventory) && source.LastDroppedDefinition.ItemId == initialItemId,
                "Revisiting a rewarded source must not reroll or create a second pickup.");

            RoomRunState roomState = new("floor-01-room-03");
            roomState.MarkArtifactClaimed();
            GameObject rewardObject = new("Phase G-7 Revisited Reward Room");
            rewardObject.transform.SetParent(dropParent, false);
            rewardObject.AddComponent<BoxCollider2D>();
            rewardObject.AddComponent<ItemDropSource>();
            RewardRoom rewardRoom = rewardObject.AddComponent<RewardRoom>();
            rewardRoom.BindRunState(roomState);
            Assert(roomState.HasVisited && roomState.HasClaimedArtifact && rewardRoom.HasRewarded,
                "A room's claimed artifact state must survive recreation and binding on revisit.");
        }

        private static void ValidateFullRunPersistence(
            IReadOnlyList<ItemDefinition> active,
            RunProgress progress,
            PlayerInventory inventory,
            Health health,
            PlayerStats stats,
            PlayerSP playerSP)
        {
            foreach (ItemDefinition definition in active)
            {
                while (inventory.GetStackCount(definition.ItemId) < definition.MaxStacks)
                {
                    Assert(inventory.TryAcquire(definition),
                        $"{definition.ItemId} must be acquirable through its complete stack range.");
                }
            }

            Dictionary<string, int> stackSnapshot = active.ToDictionary(
                item => item.ItemId,
                item => inventory.GetStackCount(item.ItemId),
                StringComparer.Ordinal);
            float attackDamage = stats.AttackDamage;
            float maxHealth = health.MaxHealth;
            float shield = health.CurrentShield;
            int maximumSP = playerSP.MaxSP;
            int projectileCount = stats.ProjectileCount;
            int pierceCount = stats.PierceCount;
            PlayerDamageAura aura = inventory.GetComponent<PlayerDamageAura>();

            progress.RecordRoomEntry(1, 1);
            progress.RecordRoomEntry(1, 6);
            progress.RecordRoomEntry(2, 1);
            progress.RecordRoomEntry(2, 7);
            progress.RecordRoomEntry(3, 1);
            progress.RecordRoomEntry(3, 8);

            Assert(progress.CurrentFloor == 3 && progress.CurrentRoom == 8,
                "The synthesized full Run must reach floor 3 without replacing RunProgress.");
            Assert(active.All(item => inventory.GetStackCount(item.ItemId) == stackSnapshot[item.ItemId]),
                "All 10 artifact stack counts must persist through room and floor movement.");
            Assert(Approximately(stats.AttackDamage, attackDamage) &&
                   Approximately(health.MaxHealth, maxHealth) &&
                   Approximately(health.CurrentShield, shield) &&
                   playerSP.MaxSP == maximumSP &&
                   stats.ProjectileCount == projectileCount &&
                   stats.PierceCount == pierceCount &&
                   aura != null && aura.StackCount == 3,
                "Compound effects, shield, projectile state, damage aura, and maximum SP must persist through floors 1-3.");
        }

        private static void ValidateFallbackHealing(
            IReadOnlyList<ItemDefinition> active,
            RunProgress progress,
            PlayerInventory inventory,
            ItemPickup pickup,
            Transform dropParent,
            Health health)
        {
            health.SetShield(0f);
            health.TakeDamage(health.MaxHealth * 0.5f);
            float beforeHeal = health.CurrentHealth;
            GameObject sourceObject = new("Phase G-7 Fallback Heal");
            sourceObject.transform.SetParent(dropParent, false);
            ItemDropSource source = sourceObject.AddComponent<ItemDropSource>();
            source.Configure(pickup, active.ToArray(), sourceObject.transform, dropParent);
            source.ConfigureRewardContext(progress, "floor-03-room-07:treasure");
            Assert(source.TryDrop(inventory) && source.HasDropped && source.LastDroppedDefinition == null &&
                   Approximately(source.LastFallbackHealAmount, health.MaxHealth * ItemDropSource.FallbackHealMaxHealthRatio) &&
                   Approximately(health.CurrentHealth, beforeHeal + source.LastFallbackHealAmount),
                "When all active artifacts are capped, a living player must receive the 25% maximum-health fallback.");
        }

        private static void ValidateDeadPlayerFallback(
            IReadOnlyList<ItemDefinition> active,
            RunProgress progress,
            PlayerInventory inventory,
            ItemPickup pickup,
            Transform dropParent,
            Health health)
        {
            health.TakeDamage(health.MaxHealth * 10f);
            GameObject sourceObject = new("Phase G-7 Dead Player Fallback");
            sourceObject.transform.SetParent(dropParent, false);
            ItemDropSource source = sourceObject.AddComponent<ItemDropSource>();
            source.Configure(pickup, active.ToArray(), sourceObject.transform, dropParent);
            source.ConfigureRewardContext(progress, "floor-03-room-08:treasure");
            Assert(health.IsDead && !source.TryDrop(inventory) && !source.HasDropped &&
                   Approximately(source.LastFallbackHealAmount, 0f),
                "A dead player must not receive or consume the fallback healing reward.");
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerSP playerSP,
            out PlayerInventory inventory)
        {
            GameObject player = new("Phase G-7 Persistent Player");
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            playerSP = player.AddComponent<PlayerSP>();
            inventory = player.AddComponent<PlayerInventory>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(playerSP, "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
        }

        private static ItemDefinition LoadDefinition(string itemId)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            if (definition == null)
            {
                throw new InvalidOperationException($"Missing item definition: {itemId}");
            }
            return definition;
        }

        private static void AssertRatio(int count, float expectedRatio, ItemRarity rarity)
        {
            float actualRatio = count / (float)DistributionSamples;
            Assert(Mathf.Abs(actualRatio - expectedRatio) <= 0.015f,
                $"{rarity} distribution {actualRatio:P2} drifted outside the expected {expectedRatio:P0} weight tolerance.");
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }
            method.Invoke(component, null);
        }

        private static bool Approximately(float left, float right) => Mathf.Approximately(left, right);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
