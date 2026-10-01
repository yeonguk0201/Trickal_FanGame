using System;
using System.Collections.Generic;
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
    public static class Week16Reward0Verification
    {
        private const int DistributionSamples = 5000;

        [MenuItem("Trickal Fan Game/Week 16/Verify Reward-0 Deterministic Candidates")]
        public static void Verify()
        {
            Week16Content0Verification.Verify();
            PhaseGRewardRunVerification.Verify();
            ValidateConfiguredMixedPool();
            ValidateDeterminismWeightsAndNoKindQuota();
            ValidateEligibilityAndFallbackBoundaries();
            Debug.Log("Week 16 Reward-0 verification passed: the unified pool produces up to three distinct, " +
                      "weighted, deterministic candidates with no kind quota and one 25% healing fallback " +
                      "for eligible-item counts below three.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Content0Setup.Setup();
            Verify();
        }

        private static void ValidateConfiguredMixedPool()
        {
            ItemDefinition[] pool = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path))
                .Where(definition => definition != null && definition.IsActive && !definition.IsSingleUse)
                .ToArray();
            Assert(pool.Any(definition => definition.Kind == ItemKind.Artifact) &&
                   pool.Any(definition => definition.Kind == ItemKind.Spell),
                "Reward-0 requires one active pool containing both Item kinds.");

            HashSet<ItemKind> observedKinds = new();
            for (int seed = 0; seed < 256; seed++)
            {
                IReadOnlyList<ItemRewardCandidate> candidates = ArtifactRewardSelector.BuildCandidates(
                    pool, null, seed, "floor-01-room-03:treasure");
                Assert(candidates.Count == ArtifactRewardSelector.MaximumCandidateCount &&
                       candidates.All(candidate => candidate.IsItem) &&
                       candidates.Select(candidate => candidate.StableId).Distinct(StringComparer.Ordinal).Count() == 3,
                    "A full configured pool must return three distinct Item candidates.");
                foreach (ItemRewardCandidate candidate in candidates)
                    observedKinds.Add(candidate.Definition.Kind);
            }

            Assert(observedKinds.SetEquals(new[] { ItemKind.Artifact, ItemKind.Spell }),
                "The unified pool did not expose both ARTIFACT and SPELL candidates across deterministic seeds.");
        }

        private static void ValidateDeterminismWeightsAndNoKindQuota()
        {
            List<ItemDefinition> definitions = new()
            {
                CreateDefinition("spell-reward0-a", ItemKind.Spell, ItemRarity.Common),
                CreateDefinition("spell-reward0-b", ItemKind.Spell, ItemRarity.Common),
                CreateDefinition("spell-reward0-c", ItemKind.Spell, ItemRarity.Common),
                CreateDefinition("artifact-reward0-a", ItemKind.Artifact, ItemRarity.Epic),
                CreateDefinition("artifact-reward0-b", ItemKind.Artifact, ItemRarity.Epic),
                CreateDefinition("artifact-reward0-c", ItemKind.Artifact, ItemRarity.Epic),
            };
            try
            {
                IReadOnlyList<ItemRewardCandidate> first = ArtifactRewardSelector.BuildCandidates(
                    definitions, null, 1600, "reward-0:determinism");
                IReadOnlyList<ItemRewardCandidate> repeated = ArtifactRewardSelector.BuildCandidates(
                    definitions, null, 1600, "reward-0:determinism");
                definitions.Reverse();
                IReadOnlyList<ItemRewardCandidate> reordered = ArtifactRewardSelector.BuildCandidates(
                    definitions, null, 1600, "reward-0:determinism");
                Assert(Signature(first) == Signature(repeated) && Signature(first) == Signature(reordered),
                    "Candidate order must be deterministic and independent of source pool ordering.");

                bool foundSingleKindSet = false;
                for (int seed = 0; seed < 256 && !foundSingleKindSet; seed++)
                {
                    IReadOnlyList<ItemRewardCandidate> candidates = ArtifactRewardSelector.BuildCandidates(
                        definitions, null, seed, "reward-0:no-kind-quota");
                    foundSingleKindSet = candidates.Count == 3 && candidates.All(candidate =>
                        candidate.IsItem && candidate.Definition.Kind == ItemKind.Spell);
                }
                Assert(foundSingleKindSet,
                    "Reward-0 must allow three candidates of one kind instead of enforcing a kind quota.");

                ItemDefinition common = definitions.Single(definition => definition.ItemId == "spell-reward0-a");
                ItemDefinition epic = definitions.Single(definition => definition.ItemId == "artifact-reward0-a");
                int commonFirst = 0;
                for (int seed = 0; seed < DistributionSamples; seed++)
                {
                    IReadOnlyList<ItemRewardCandidate> candidates = ArtifactRewardSelector.BuildCandidates(
                        new[] { common, epic }, null, seed, "reward-0:weight-sample");
                    if (candidates[0].Definition == common) commonFirst++;
                }

                float actualCommonRatio = commonFirst / (float)DistributionSamples;
                float expectedCommonRatio = ArtifactRewardSelector.CommonWeight /
                                            (float)(ArtifactRewardSelector.CommonWeight +
                                                    ArtifactRewardSelector.EpicWeight);
                Assert(Mathf.Abs(actualCommonRatio - expectedCommonRatio) <= 0.02f,
                    $"COMMON first-choice ratio {actualCommonRatio:P2} drifted from {expectedCommonRatio:P2}.");
            }
            finally
            {
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
            }
        }

        private static void ValidateEligibilityAndFallbackBoundaries()
        {
            GameObject player = new("Reward-0 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            List<ItemDefinition> definitions = new()
            {
                CreateDefinition("artifact-reward0-boundary-a", ItemKind.Artifact, ItemRarity.Common),
                CreateDefinition("artifact-reward0-boundary-b", ItemKind.Artifact, ItemRarity.Uncommon),
                CreateDefinition("spell-reward0-boundary-a", ItemKind.Spell, ItemRarity.Rare),
                CreateDefinition("spell-reward0-boundary-b", ItemKind.Spell, ItemRarity.Epic),
            };
            ItemDefinition inactive = CreateDefinition(
                "spell-reward0-inactive", ItemKind.Spell, ItemRarity.Common, false);
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");

                AssertItemAndHealingCounts(definitions, inventory, 3, 0, 3);
                Assert(inventory.TryAcquire(definitions[0]) && inventory.TryAcquire(definitions[1]),
                    "Could not reach the two-eligible boundary.");
                AssertItemAndHealingCounts(definitions, inventory, 2, 1, 3);
                Assert(inventory.TryAcquire(definitions[2]), "Could not reach the one-eligible boundary.");
                AssertItemAndHealingCounts(definitions, inventory, 1, 1, 2);
                Assert(inventory.TryAcquire(definitions[3]), "Could not reach the zero-eligible boundary.");
                AssertItemAndHealingCounts(definitions, inventory, 0, 1, 1);

                IReadOnlyList<ItemRewardCandidate> invalidPool = ArtifactRewardSelector.BuildCandidates(
                    new[] { inactive }, inventory, 1600, "reward-0:invalid-pool");
                Assert(invalidPool.Count == 0,
                    "An inactive-only pool must not hide configuration errors behind healing.");

                IReadOnlyList<ItemRewardCandidate> duplicatePool = ArtifactRewardSelector.BuildCandidates(
                    new[] { definitions[0], definitions[0] }, null, 1600, "reward-0:duplicates");
                Assert(duplicatePool.Count == 2 && duplicatePool.Count(candidate => candidate.IsItem) == 1 &&
                       duplicatePool.Count(candidate => candidate.IsHealing) == 1,
                    "Duplicate source entries must collapse to one Item candidate.");
            }
            finally
            {
                Object.DestroyImmediate(inactive);
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
                Object.DestroyImmediate(player);
            }
        }

        private static void AssertItemAndHealingCounts(IReadOnlyList<ItemDefinition> pool,
            PlayerInventory inventory, int expectedItems, int expectedHealing, int expectedTotal)
        {
            IReadOnlyList<ItemRewardCandidate> candidates = ArtifactRewardSelector.BuildCandidates(
                pool, inventory, 1600, "reward-0:boundaries");
            Assert(candidates.Count == expectedTotal &&
                   candidates.Count(candidate => candidate.IsItem) == expectedItems &&
                   candidates.Count(candidate => candidate.IsHealing) == expectedHealing &&
                   candidates.Select(candidate => candidate.StableId).Distinct(StringComparer.Ordinal).Count() == expectedTotal,
                $"Unexpected Reward-0 boundary result for {expectedItems} eligible Items.");
            ItemRewardCandidate healing = candidates.SingleOrDefault(candidate => candidate.IsHealing);
            Assert(healing == null || Mathf.Approximately(healing.HealMaxHealthRatio,
                       ArtifactRewardSelector.FallbackHealMaxHealthRatio),
                "Healing fallback must restore 25% of maximum HP.");
        }

        private static ItemDefinition CreateDefinition(string itemId, ItemKind kind, ItemRarity rarity,
            bool isActive = true)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, itemId, kind, rarity, isActive, 1,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
            return definition;
        }

        private static string Signature(IEnumerable<ItemRewardCandidate> candidates) =>
            string.Join("|", candidates.Select(candidate => candidate.StableId));

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
