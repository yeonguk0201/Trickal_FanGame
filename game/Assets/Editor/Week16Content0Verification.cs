using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week16Content0Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Content-0 First Item Contracts")]
        public static void Verify()
        {
            Week16Item0Verification.Verify();
            Assert(Week16Content0Catalog.All.Count == 6,
                "Content-0 must contain exactly three spells and three artifacts.");
            Assert(Week16Content0Catalog.All.Count(spec => spec.Kind == ItemKind.Spell) == 3 &&
                   Week16Content0Catalog.All.Count(spec => spec.Kind == ItemKind.Artifact) == 3,
                "Content-0 kind distribution must remain three SPELL and three ARTIFACT definitions.");
            Assert(Week16Content0Catalog.All.Select(spec => spec.ItemId).Distinct(StringComparer.Ordinal).Count() == 6,
                "Content-0 stable IDs must be unique.");

            foreach (Week16Content0Spec expected in Week16Content0Catalog.All)
            {
                string path = $"Assets/Items/{expected.ItemId}.asset";
                ItemDefinition actual = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                Assert(actual != null, $"Missing Content-0 asset: {expected.ItemId}");
                Assert(actual.IsValid && actual.IsActive, $"Content-0 asset must be active and valid: {expected.ItemId}");
                Assert(actual.ItemId == expected.ItemId && actual.DisplayName == expected.DisplayName &&
                       actual.Kind == expected.Kind && actual.Rarity == expected.Rarity &&
                       actual.MaxStacks == expected.MaxStacks,
                    $"Content-0 base contract mismatch: {expected.ItemId}");
                AssertEffects(actual.Effects, expected.Effects, expected.ItemId);

                string description = ArtifactEffectDescription.Build(actual);
                Assert(!string.IsNullOrWhiteSpace(description) &&
                       !actual.Effects.Any(effect => description.Contains(effect.EffectType.ToString(),
                           StringComparison.Ordinal)),
                    $"Content-0 requires a player-facing effect description: {expected.ItemId}");
            }

            VerifyRepositoryWideIdUniqueness();
            VerifyRequiredFieldFailures();
            Debug.Log("Week 16 Content-0 verification passed: six active item contracts, stable IDs, kinds, " +
                      "rarities, stack limits, effect parameters, and descriptions match the catalog.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Content0Setup.Setup();
            string[] paths = Week16Content0Catalog.All
                .Select(spec => $"Assets/Items/{spec.ItemId}.asset")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();

            Week16Content0Setup.Setup();
            Assert(paths.All(path => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null),
                "Content-0 setup lost an item asset when run twice.");
            Assert(guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Content-0 setup changed an item asset GUID when run twice.");
            Verify();
        }

        private static void VerifyRepositoryWideIdUniqueness()
        {
            ItemDefinition[] definitions = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path))
                .Where(definition => definition != null)
                .ToArray();
            Assert(definitions.Select(definition => definition.ItemId)
                    .Distinct(StringComparer.Ordinal).Count() == definitions.Length,
                "Every ItemDefinition asset must have a repository-wide unique stable ID.");
        }

        private static void VerifyRequiredFieldFailures()
        {
            AssertInvalid(new ItemEffectEntry(ItemEffectType.HealOverTimeBelowHealthOnce,
                configuredMagnitude: 0.45f, configuredHealthThreshold: 0.30f),
                "life-gem duration");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.HealOverTimeBelowHealthOnce,
                configuredMagnitude: 0.45f, configuredDurationSeconds: 3f),
                "life-gem health threshold");
            AssertInvalid(new ItemEffectEntry(ItemEffectType.BasicAttackHitLightning,
                configuredMagnitude: 1.50f),
                "clear-weather hit count");
        }

        private static void AssertEffects(IReadOnlyList<ItemEffectEntry> actual,
            IReadOnlyList<ItemEffectEntry> expected, string itemId)
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
                       Approximately(left.ScaleMultiplier, right.ScaleMultiplier) &&
                       Approximately(left.DurationSeconds, right.DurationSeconds),
                    $"Effect {index} value mismatch for {itemId}.");
            }
        }

        private static void AssertInvalid(ItemEffectEntry effect, string scenario)
        {
            Assert(!effect.TryValidate(out _), $"Invalid {scenario} contract should be rejected.");
        }

        private static bool Approximately(float left, float right) => Mathf.Approximately(left, right);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
