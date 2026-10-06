using System;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter3Setup
    {
        public const int ContentVersion = 3;
        public const string EncounterId = "reinforcement-pressure";
        public const string EncounterPath = Week14Encounter1Setup.EncounterFolder + "/" + EncounterId + ".asset";

        [MenuItem("Trickal Fan Game/Week 14/Setup Encounter-3 Waves and Revisit State")]
        public static void Setup()
        {
            Week14Encounter2Setup.Setup();
            RoomProfile[] profiles =
            {
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.SmallProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room1Setup.BasicProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.WideProfilePath),
            };
            if (profiles.Any(profile => profile == null))
                throw new InvalidOperationException("Encounter-3 requires all Room Profiles.");
            // Resource-3 replaced the guaranteed SP clear reward with one seeded room clear drop roll.
            ResourceDropTable clearDrop = Week17Resource3Setup.EnsureDropTable();

            EncounterDefinition twoWave = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EncounterPath);
            if (twoWave == null)
            {
                twoWave = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(twoWave, EncounterPath);
            }
            twoWave.Configure(EncounterId, profiles, 2, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                new[]
                {
                    new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[]
                        {
                            new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, null, "all"),
                            new EncounterSpawnRule(EncounterEnemyRole.Ranged, 1, null, "all"),
                        }),
                    new EncounterWaveDefinition(2, EncounterWaveStartCondition.PreviousWaveCleared,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[]
                        {
                            new EncounterSpawnRule(EncounterEnemyRole.Charging, 1, null, "all"),
                            new EncounterSpawnRule(EncounterEnemyRole.Ranged, 1, null, "all"),
                        }),
                }, EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(twoWave);

            EncounterDefinition[] definitions = AssetDatabase.FindAssets("t:EncounterDefinition",
                    new[] { Week14Encounter1Setup.EncounterFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EncounterDefinition>)
                .Where(definition => definition != null &&
                    (definition.EncounterId == EncounterId ||
                     definition.EncounterId == "pressure-chaser-ranged" ||
                     definition.EncounterId == "lane-charging-ranged" ||
                     definition.EncounterId == "crossfire-ranged"))
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            if (definitions.Length != 4)
                throw new InvalidOperationException("Encounter-3 requires the three mixed Encounters and one two-wave Encounter.");

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null)
                throw new InvalidOperationException("Encounter-3 requires the configured generator and assembler.");
            Undo.RecordObjects(new UnityEngine.Object[] { generator, assembler }, "Configure Encounter-3 runtime");
            generator.ConfigureEncounters(ContentVersion, definitions);
            assembler.ConfigureEncounterClearDrop(clearDrop);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Encounter-3 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Encounter-3 ready: the reinforcement Encounter runs two required-enemy waves, " +
                      "persists wave and clear-reward state, and rolls the room clear drop once on clear.");
        }
    }
}
