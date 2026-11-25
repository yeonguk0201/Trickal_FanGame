using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Density-1 (docs/28 §10.1, beta feedback 2026-11-23): the chaser (불효자손) is too strong in a small room, where
    // there is no space to kite it. Its stats stay; the small Encounters only pick it half as often. The other
    // candidates, their threat scores and every bigger size keep their values. Density0Setup.Candidates applies this,
    // so re-running Density-0, Spawn-2 or Enemy-13 keeps the lower weight.
    public static class Density1Setup
    {
        public const int EncounterContentVersion = 25;
        public const int SmallChaserWeightPercent = 50;

        public static int SmallChaserWeight(int weight) =>
            Math.Max(1, (int)Math.Round(weight * SmallChaserWeightPercent / 100d));

        public static EncounterEnemyCandidate[] ForSmall(IEnumerable<EncounterEnemyCandidate> candidates) =>
            candidates.Select(candidate => candidate.EnemyRole == EncounterEnemyRole.Chaser
                ? new EncounterEnemyCandidate(candidate.EnemyRole, SmallChaserWeight(candidate.Weight))
                : candidate).ToArray();

        public static EncounterEnemyCandidate[] ForProfile(string profileId, EncounterEnemyCandidate[] candidates) =>
            Density0Setup.IsSmall(profileId) ? ForSmall(candidates) : candidates;

        public static EncounterEnemyCandidate[] ForDefinition(EncounterDefinition definition,
            EncounterEnemyCandidate[] candidates) =>
            Density0Setup.IsSmall(definition) ? ForSmall(candidates) : candidates;

        [MenuItem("Trickal Fan Game/Beta/Setup Density-1 Small Room Chaser Weight")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Density-1 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Density0Setup.EncounterContentVersion)
                throw new InvalidOperationException("Density-1 requires the completed Density-0 Game Scene.");

            RoomDifficultyTable table = generator.DifficultyTable;
            EncounterDefinition[] updated = Density0Setup.ConfigureEncounters(Week27Curve1Setup.Patterns, table);
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out string error) || !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                    throw new InvalidOperationException($"Density-1 catalog is invalid. {error}");
            }

            Undo.RecordObject(generator, "Configure Density-1 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            EditorUtility.SetDirty(generator);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Density-1 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Density-1 ready: small chaser weight x{SmallChaserWeightPercent}%, Encounter content version " +
                      $"{generator.EncounterContentVersion}.");
        }
    }
}
