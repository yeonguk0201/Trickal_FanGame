using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Variant-2 (docs/28 §8): the undying (불사·사이온) champion. The runtime rules live in EnemyVariant,
    // EnemyVariantRules, EnemyChampionUndying and Health's lethal deferral; this setup only raises the Encounter
    // content version, because the same seed now picks champion properties from four. Re-running keeps the version.
    public static class Week27Variant2Setup
    {
        public const int EncounterContentVersion = 23;

        [MenuItem("Trickal Fan Game/Week 27/Setup Variant-2 Undying Champions")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Variant-2 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null ||
                generator.EncounterContentVersion < Week27Variant1Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Variant-2 requires the completed Variant-1 Game Scene.");
            }

            if (generator.EncounterContentVersion < EncounterContentVersion)
            {
                Undo.RecordObject(generator, "Configure Variant-2 undying champions");
                generator.ConfigureEncounters(EncounterContentVersion, generator.EncounterDefinitions.ToArray());
                EditorUtility.SetDirty(generator);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                    throw new InvalidOperationException("Game Scene save failed during Variant-2 setup.");
            }

            Debug.Log("Variant-2 ready: champions pick from puddle, shots, blast and undying, Encounter content " +
                      $"version {generator.EncounterContentVersion}.");
        }
    }
}
