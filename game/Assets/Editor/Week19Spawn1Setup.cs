using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week19Spawn1Setup
    {
        public const int EncounterContentVersion = 6;

        [MenuItem("Trickal Fan Game/Week 19/Setup Spawn-1 Placement Roles")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null)
            {
                throw new InvalidOperationException("Spawn-1 requires the configured Game Scene FloorGenerator.");
            }

            string[] missingLayouts = Week18Obstacle2Setup.Layouts
                .Select(layout => layout.TemplateId)
                .Where(id => generator.RoomTemplates.All(template => template == null || template.TemplateId != id))
                .ToArray();
            if (missingLayouts.Length > 0)
            {
                throw new InvalidOperationException("Spawn-1 requires Obstacle-2 layouts: " +
                                                    string.Join(", ", missingLayouts));
            }

            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(template => template != null))
            {
                SpawnPointPlacementRole[] roles = RoomTemplateDefinition.BuildDefaultSpawnPointRoles(
                    template.SpawnPoints);
                Undo.RecordObject(template, "Configure Spawn-1 placement roles");
                template.Configure(
                    template.TemplateId,
                    template.Profile,
                    template.RoomPrefabAsset,
                    template.AllowedRoomTypes.ToArray(),
                    template.DoorSlots.ToArray(),
                    template.SpawnPoints.ToArray(),
                    template.MinimumFloor,
                    template.MaximumFloor,
                    roles);
                EditorUtility.SetDirty(template);
            }

            Undo.RecordObject(generator, "Configure Spawn-1 Encounter content version");
            generator.ConfigureEncounters(
                Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Game Scene save failed during Spawn-1 setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Spawn-1 ready: every authored SpawnPoint declares melee-pressure, rear-firing, or " +
                      "charge-lane placement roles, and Encounter resolution rejects incompatible enemy roles.");
        }
    }
}
