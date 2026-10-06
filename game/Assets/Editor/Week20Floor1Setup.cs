using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week20Floor1Setup
    {
        public const int RoomContentVersion = 8;
        public const int EncounterContentVersion = 12;

        public static FloorGenerationSettings[] CreateSettings() => new[]
        {
            new FloorGenerationSettings(8, 12, 4),
            new FloorGenerationSettings(8, 12, 5),
            new FloorGenerationSettings(12, 18, 5),
        };

        [MenuItem("Trickal Fan Game/Week 20/Setup Floor-1 Expanded Floors")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.FloorCount != 3 ||
                !generator.RoomDefinitions.Any(definition => definition != null && definition.RoomType == RoomType.Shop))
                throw new InvalidOperationException("Floor-1 requires the three-floor Game Scene and Special-4 shop setup.");

            Undo.RecordObject(generator, "Configure Floor-1 expanded floors");
            generator.ConfigureFloorSettings(CreateSettings());
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion),
                generator.RoomTemplates.ToArray());
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            EditorUtility.SetDirty(generator);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Floor-1 setup.");
            Debug.Log("Floor-1 ready: three floors, total rooms 8~12 / 8~12 / 12~18, boss distances 4 / 5 / 5. " +
                      "Totals include every special room. Nine-floor progression remains a future plan.");
        }
    }
}
