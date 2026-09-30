using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter1Setup
    {
        public const string EncounterFolder = "Assets/Encounters/Definitions";
        public const int ContentVersion = 1;
        public const float MinimumPlayerDistance = 1.5f;
        public const float MinimumDoorDistance = 2f;

        [MenuItem("Trickal Fan Game/Week 14/Setup Encounter-1 Contract")]
        public static void Setup()
        {
            Week14Room5Setup.Setup();
            EnsureFolder(EncounterFolder);
            RoomProfile[] profiles =
            {
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.SmallProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room1Setup.BasicProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.WideProfilePath),
            };
            if (profiles.Any(profile => profile == null))
                throw new InvalidOperationException("Encounter-1 requires all Small, Basic, and Wide profiles.");

            EncounterDefinition[] definitions =
            {
                CreateOrUpdate("solo-chaser", EncounterEnemyRole.Chaser, profiles),
                CreateOrUpdate("solo-ranged", EncounterEnemyRole.Ranged, profiles),
                CreateOrUpdate("solo-charging", EncounterEnemyRole.Charging, profiles),
            };
            Array.Sort(definitions, (left, right) => string.CompareOrdinal(left.EncounterId, right.EncounterId));

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("Encounter-1 requires FloorGenerator.");
            Undo.RecordObject(generator, "Configure Encounter-1 catalog");
            generator.ConfigureEncounters(ContentVersion, definitions);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Encounter-1 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Encounter-1 ready: stable solo role definitions are filtered by floor, " +
                      "Small/Basic/Wide profile, SpawnPoint group, safety distances, and wave conditions.");
        }

        private static EncounterDefinition CreateOrUpdate(string encounterId, EncounterEnemyRole role,
            RoomProfile[] profiles)
        {
            string path = $"{EncounterFolder}/{encounterId}.asset";
            EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Configure(encounterId, profiles, 1, 3, MinimumPlayerDistance, MinimumDoorDistance,
                new[]
                {
                    new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                        new[] { new EncounterSpawnRule(role, 1, null, "all") }),
                }, EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
