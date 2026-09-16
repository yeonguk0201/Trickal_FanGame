using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Room4Setup
    {
        public const int ContentVersion = 1;

        [MenuItem("Trickal Fan Game/Week 14/Setup Room-4 Seeded Template Selection")]
        public static void Setup()
        {
            Week14Room3Setup.Setup();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomTemplateDefinition[] templates =
            {
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room1Setup.BasicTemplatePath),
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room3Setup.SmallTemplatePath),
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room3Setup.WideTemplatePath),
            };
            if (generator == null || templates.Any(template => template == null))
            {
                Debug.LogError("Room-4 requires the configured FloorGenerator and all Room-3 templates.");
                return;
            }

            Undo.RecordObject(generator, "Configure Room-4 seeded template catalog");
            generator.ConfigureTemplates(ContentVersion, templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Week 14 Room-4 ready: the content-versioned Small, Basic, and Wide template catalog " +
                "is connected to seeded floor generation.");
        }
    }
}
