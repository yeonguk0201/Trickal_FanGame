using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Encounter2Setup
    {
        public const string RosterPath = "Assets/Encounters/enemy-roster.asset";
        public const int ContentVersion = 2;
        public const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        public const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        public const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Week 14/Setup Encounter-2 Mixed Encounters")]
        public static void Setup()
        {
            Week14Encounter1Setup.Setup();
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            GameObject ranged = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            GameObject charging = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            if (chaser == null || ranged == null || charging == null)
                throw new InvalidOperationException("Encounter-2 requires all three verified normal enemy Prefabs.");

            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(RosterPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<EncounterEnemyRoster>();
                AssetDatabase.CreateAsset(roster, RosterPath);
            }
            roster.Configure(new[]
            {
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, chaser),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Ranged, ranged),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Charging, charging),
            });
            EditorUtility.SetDirty(roster);

            RoomProfile[] profiles =
            {
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.SmallProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room1Setup.BasicProfilePath),
                AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room3Setup.WideProfilePath),
            };
            if (profiles.Any(profile => profile == null))
                throw new InvalidOperationException("Encounter-2 requires Small, Basic, and Wide profiles.");

            EncounterDefinition[] definitions =
            {
                CreateOrUpdate("pressure-chaser-ranged", profiles,
                    new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, null, "all"),
                    new EncounterSpawnRule(EncounterEnemyRole.Ranged, 1, null, "all")),
                CreateOrUpdate("lane-charging-ranged", profiles,
                    new EncounterSpawnRule(EncounterEnemyRole.Charging, 1, null, "all"),
                    new EncounterSpawnRule(EncounterEnemyRole.Ranged, 1, null, "all")),
                CreateOrUpdate("crossfire-ranged", profiles,
                    new EncounterSpawnRule(EncounterEnemyRole.Ranged, 3, null, "all")),
            };
            Array.Sort(definitions, (left, right) => string.CompareOrdinal(left.EncounterId, right.EncounterId));

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null)
                throw new InvalidOperationException("Encounter-2 requires the configured FloorGenerator and RoomGraphAssembler.");
            Undo.RecordObjects(new UnityEngine.Object[] { generator, assembler }, "Configure Encounter-2 runtime");
            generator.ConfigureEncounters(ContentVersion, definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Encounter-2 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Encounter-2 ready: chaser+ranged pressure, charging+ranged lane, and " +
                      "three-direction ranged crossfire Encounters now resolve to verified enemy Prefabs and " +
                      "safe active-door SpawnPoints.");
        }

        private static EncounterDefinition CreateOrUpdate(string encounterId, RoomProfile[] profiles,
            params EncounterSpawnRule[] rules)
        {
            string path = $"{Week14Encounter1Setup.EncounterFolder}/{encounterId}.asset";
            EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Configure(encounterId, profiles, 1, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                new[]
                {
                    new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, rules),
                }, EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(definition);
            return definition;
        }
    }
}
