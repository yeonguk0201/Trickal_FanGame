using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1BSetup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup Setting-1B Display")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Setting-1B setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureScene(Week13FrontendSetup.ScenePath, "FrontendCamera", "FrontendCanvas");
            ConfigureScene(Week13FrontendSetup.GameScenePath, "Main Camera", Week13Hud1Setup.HudRootName);

            PlayerSettings.defaultScreenWidth = LocalSettings.DefaultWidth;
            PlayerSettings.defaultScreenHeight = LocalSettings.DefaultHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;
            AssetDatabase.SaveAssets();
            Debug.Log("Setting-1B display setup completed for Frontend and Game scenes.");
        }

        private static void ConfigureScene(string scenePath, string cameraName, string canvasName)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .SingleOrDefault(candidate => candidate.name == cameraName);
            if (camera == null) throw new InvalidOperationException($"{scenePath} requires camera '{cameraName}'.");
            if (camera.GetComponent<DisplayAspectController>() == null)
                Undo.AddComponent<DisplayAspectController>(camera.gameObject);

            Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .SingleOrDefault(candidate => candidate.name == canvasName);
            if (canvas == null) throw new InvalidOperationException($"{scenePath} requires canvas '{canvasName}'.");
            RectTransform frame = canvas.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException($"{canvasName} requires ReferenceFrame.");
            FrontendLayout layout = frame.GetComponent<FrontendLayout>();
            if (layout == null) layout = Undo.AddComponent<FrontendLayout>(frame.gameObject);
            layout.ApplyLayout();

            if (scenePath == Week13FrontendSetup.ScenePath)
                ConfigureResolutionDropdown(scene);

            foreach (Component component in new Component[] { camera, camera.GetComponent<DisplayAspectController>(), frame, layout })
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save {scenePath} during Setting-1B setup.");
        }

        private static void ConfigureResolutionDropdown(Scene scene)
        {
            FrontendSettingsView settings = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FrontendSettingsView>(true)).Single();
            TMP_Dropdown dropdown = settings.ResolutionDropdown;
            if (dropdown == null || dropdown.template == null || dropdown.itemText == null)
                throw new InvalidOperationException("Resolution dropdown requires a template and item label.");

            RectTransform template = dropdown.template;
            template.sizeDelta = new Vector2(template.sizeDelta.x, 126f);
            CanvasGroup canvasGroup = template.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = Undo.AddComponent<CanvasGroup>(template.gameObject);
            RectTransform content = template.Find("Viewport/Content") as RectTransform;
            RectTransform item = content != null ? content.Find("Item") as RectTransform : null;
            if (content == null || item == null)
                throw new InvalidOperationException("Resolution dropdown template hierarchy is incomplete.");

            content.sizeDelta = new Vector2(0f, 40f);
            item.sizeDelta = new Vector2(0f, 40f);
            dropdown.itemText.fontSize = 20f;
            dropdown.itemText.color = new Color(0.92f, 0.96f, 1f, 1f);
            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(canvasGroup);
            EditorUtility.SetDirty(content);
            EditorUtility.SetDirty(item);
            EditorUtility.SetDirty(dropdown.itemText);
            EditorUtility.SetDirty(dropdown);
        }
    }
}
