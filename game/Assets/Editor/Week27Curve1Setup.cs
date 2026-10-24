using System;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Curve-1 (user decisions, 2026-10-24, docs/28 §9): enemies enter by floor (EncounterEnemyFloorPool), so the
    // Encounters that hold a fixed floor-2 enemy start on floor 2. elite-pair keeps its fixed charger, and crossfire
    // keeps a fixed charger and would leave three snipers on floor 1, so both start on floor 2. 산사모-순수 fires
    // three shots with no aim lead while the run is in its first theme (Depth-0 adds the later-theme variant).
    // Re-running only rewrites the same assets and the Game Scene generator.
    public static class Week27Curve1Setup
    {
        public const int EncounterContentVersion = 16;
        public const int MaximumFloor = 3;
        public const float QuickFourShotChance = 0f;
        public const float QuickMaximumPredictionTime = 0f;

        public static readonly string[] Patterns = { "crossfire", "elite-pair", "jyubi-swarm", "pressure", "swarm" };
        public static readonly string[] FloorTwoPatterns = { "crossfire", "elite-pair" };

        public static int MinimumFloor(string pattern) =>
            FloorTwoPatterns.Contains(pattern, StringComparer.Ordinal) ? 2 : 1;

        public static bool TryResolvePattern(EncounterDefinition definition, out string pattern)
        {
            pattern = null;
            if (definition == null) return false;
            foreach (Week19Spawn2Setup.ProfileSpec profile in Week19Spawn2Setup.Profiles)
            foreach (string candidate in Patterns)
            {
                if (!string.Equals(definition.EncounterId,
                        Week19Spawn2Setup.EncounterId(profile.ProfileId, candidate), StringComparison.Ordinal))
                    continue;
                pattern = candidate;
                return true;
            }

            return false;
        }

        [MenuItem("Trickal Fan Game/Week 27/Setup Curve-1 Floor Enemy Pool")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Curve-1 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week25Enemy9Setup.EncounterContentVersion)
                throw new InvalidOperationException("Curve-1 requires the completed Enemy-9 Game Scene.");

            RoomDifficultyTable table = generator.DifficultyTable;
            EncounterDefinition[] definitions = generator.EncounterDefinitions.ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!TryResolvePattern(definition, out string pattern))
                    throw new InvalidOperationException(
                        $"Curve-1 does not know the floor range of Encounter '{definition?.EncounterId}'.");
                if (definition.MinimumFloor != MinimumFloor(pattern) || definition.MaximumFloor != MaximumFloor)
                {
                    Undo.RecordObject(definition, "Configure Curve-1 Encounter floors");
                    definition.ConfigureFloorRange(MinimumFloor(pattern), MaximumFloor);
                    EditorUtility.SetDirty(definition);
                }

                if (!definition.TryValidate(out string error) ||
                    !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                    throw new InvalidOperationException($"Curve-1 catalog is invalid. {error}");
            }

            ConfigureQuickRanged();

            Undo.RecordObject(generator, "Configure Curve-1 Encounter content version");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Curve-1 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Curve-1 ready: {string.Join(", ", FloorTwoPatterns)} start on floor 2, enemies enter by " +
                      "floor (new x1.5, two floors below x0.5), 산사모-순수 fires three shots with no lead, and the " +
                      $"Encounter content version is {generator.EncounterContentVersion}.");
        }

        private static void ConfigureQuickRanged()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week19Spawn2Setup.QuickRangedPrefabPath);
            MobileRangedEnemyController controller = prefab != null
                ? prefab.GetComponent<MobileRangedEnemyController>()
                : null;
            if (controller == null)
                throw new InvalidOperationException("Curve-1 requires the 산사모-순수 (QuickRangedFairy) Prefab.");

            SerializedObject serialized = new(controller);
            serialized.FindProperty("fourShotChance").floatValue = QuickFourShotChance;
            serialized.FindProperty("maximumPredictionTime").floatValue = QuickMaximumPredictionTime;
            if (serialized.ApplyModifiedProperties()) EditorUtility.SetDirty(controller);
        }
    }
}
