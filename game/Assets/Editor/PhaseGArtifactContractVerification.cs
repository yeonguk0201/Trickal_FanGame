using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGArtifactContractVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase G-1 Artifact Contracts")]
        public static void Verify()
        {
            Assert(PhaseGArtifactCatalog.Active.Count == 10, "The Phase G catalog must contain exactly 10 active artifacts.");
            Assert(PhaseGArtifactCatalog.All.Select(spec => spec.ItemId).Distinct().Count() == PhaseGArtifactCatalog.All.Count,
                "Artifact IDs must be unique.");

            foreach (PhaseGArtifactSpec expected in PhaseGArtifactCatalog.All)
            {
                ItemDefinition actual = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{expected.ItemId}.asset");
                Assert(actual != null, $"Missing artifact asset: {expected.ItemId}");
                Assert(actual.IsValid, $"Invalid artifact contract: {expected.ItemId}");
                Assert(actual.ItemId == expected.ItemId, $"Item ID mismatch for {expected.ItemId}.");
                Assert(actual.DisplayName == expected.DisplayName, $"Display name mismatch for {expected.ItemId}.");
                Assert(actual.Kind == ItemKind.Artifact, $"Legacy item must remain an artifact: {expected.ItemId}.");
                Assert(actual.Rarity == expected.Rarity, $"Rarity mismatch for {expected.ItemId}.");
                Assert(actual.IsActive == expected.IsActive, $"Active state mismatch for {expected.ItemId}.");
                Assert(actual.MaxStacks == expected.MaxStacks, $"Maximum stack mismatch for {expected.ItemId}.");
                AssertEffects(actual.Effects, expected.Effects, expected.ItemId);
            }

            ItemDefinition legacyMultiShot = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-06.asset");
            Assert(!legacyMultiShot.IsActive, "item-06 must be excluded from the active reward pool.");
            Assert(legacyMultiShot.Effects.Count == 1 &&
                   legacyMultiShot.Effects[0].EffectType == ItemEffectType.MultiShot &&
                   legacyMultiShot.Effects[0].IntegerAmount == 1,
                "item-06 must retain its legacy multi-shot meaning.");

            ItemDefinition telescope = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-15.asset");
            Assert(telescope.IsActive && telescope.DisplayName == "장난감 망원경",
                "item-15 must be the active telescope artifact.");

            VerifyRequiredFieldFailures();
            Debug.Log("Phase G-1 artifact contract verification passed: Unity has 10 matching active artifacts, " +
                      "compound effects and required fields are valid, and item-06 remains an inactive legacy multi-shot ID.");
        }

        private static void AssertEffects(
            IReadOnlyList<ItemEffectEntry> actual,
            IReadOnlyList<ItemEffectEntry> expected,
            string itemId)
        {
            Assert(actual.Count == expected.Count, $"Effect count mismatch for {itemId}.");
            for (int index = 0; index < expected.Count; index++)
            {
                ItemEffectEntry left = actual[index];
                ItemEffectEntry right = expected[index];
                Assert(left.TryValidate(out string error), $"Invalid effect on {itemId}: {error}");
                Assert(left.EffectType == right.EffectType &&
                       Approximately(left.Magnitude, right.Magnitude) &&
                       Approximately(left.HealthThreshold, right.HealthThreshold) &&
                       Approximately(left.SecondaryMagnitude, right.SecondaryMagnitude) &&
                       left.IntegerAmount == right.IntegerAmount &&
                       Approximately(left.MinimumDistance, right.MinimumDistance) &&
                       Approximately(left.MaximumDistance, right.MaximumDistance) &&
                       Approximately(left.Radius, right.Radius) &&
                       Approximately(left.IntervalSeconds, right.IntervalSeconds) &&
                       Approximately(left.SpreadAngleDegrees, right.SpreadAngleDegrees) &&
                       Approximately(left.ScaleMultiplier, right.ScaleMultiplier),
                    $"Effect {index} value mismatch for {itemId}.");
            }
        }

        private static void VerifyRequiredFieldFailures()
        {
            AssertInvalid(new ItemEffectEntry(ItemEffectType.MoveSpeedPercentBelowHealth, 0.25f), "health threshold");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.MaxHealthDamageAura, 0.01f), "radius and interval");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.AttackDamageAura, 0.20f), "attack aura radius and interval");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.HealOnKillEveryN, 1f), "kill count");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.HealOnKillEveryN, configuredIntegerAmount: 2), "heal amount");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.DistanceDamage, 0.40f, configuredMinimumDistance: 3f),
                "maximum distance");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.SplitAfterPierce, configuredIntegerAmount: 3),
                "split secondary fields");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.SkillProjectileBonusAtSP, configuredHealthThreshold: 1f),
                "projectile count");
        }

        private static void AssertInvalid(ItemEffectEntry effect, string scenario)
        {
            Assert(!effect.TryValidate(out _), $"Invalid {scenario} contract should be rejected.");
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
