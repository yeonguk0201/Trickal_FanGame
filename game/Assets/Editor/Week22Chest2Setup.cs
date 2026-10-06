using System;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Chest-2: gives the chest content table its golden special reward chance, the diamond chest's guaranteed spell and
    // the implemented jjangsem spells. The table itself is built by Chest-1 and the golden exclusive pool by Flight-0;
    // this rebuilds the same asset with the Chest-2 rule values, keeping its GUID and the pool.
    public static class Week22Chest2Setup
    {
        // D2 (2026-10-05): §3.1 allows 20~30% for the golden special reward; a diamond chest always holds one spell,
        // which is a jjangsem spell 25% of the time once one is implemented.
        public const float GoldenSpecialRewardChance = 0.25f;
        public const float DiamondSpellChance = 1f;
        public const float DiamondJjangsemShare = 0.25f;

        [MenuItem("Trickal Fan Game/Week 22/Setup Chest-2 Special Rewards")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Chest-2 setup.");

            ChestContentTable table = Week22Chest1Setup.EnsureTable();
            if (table.GoldenExclusiveArtifacts.Count == 0)
                throw new InvalidOperationException(
                    "Run Flight-0 setup first: the golden exclusive pool is empty, so no special reward can drop.");
            Debug.Log($"Chest-2 setup complete: golden chests roll a {GoldenSpecialRewardChance:P0} special reward " +
                      $"from {table.GoldenExclusiveArtifacts.Count} exclusive artifact(s), diamond chests always hold " +
                      $"one spell ({DiamondJjangsemShare:P0} jjangsem, {table.JjangsemSpells.Count} implemented).");
        }
    }
}
