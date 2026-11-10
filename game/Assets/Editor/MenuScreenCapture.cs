using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Menu slices: renders a Frontend screen to output/menu-review/*.png for art review, without Play Mode.
    // The scene is reopened from disk afterwards, so the temporary camera and sample values are never saved.
    // Needs graphics (do not pass -nographics). This is review material, not a substitute for the Play check.
    public static class MenuScreenCapture
    {
        public static readonly Vector2Int[] Resolutions = { new(1920, 1080), new(1280, 720) };

        public static string OutputFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "output", "menu-review"));

        // prepare receives the ReferenceFrame and shows the screen to capture.
        public static string[] Capture(string name, Action<RectTransform> prepare)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            try
            {
                Canvas canvas = scene.GetRootGameObjects().Single(root => root.name == "FrontendCanvas").GetComponent<Canvas>();
                var frame = (RectTransform)canvas.transform.Find("ReferenceFrame");
                prepare(frame);
                Directory.CreateDirectory(OutputFolder);
                return Resolutions.Select(size => Render(canvas, frame, name, size)).ToArray();
            }
            finally
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            }
        }

        // Shows only the named children of the frame, the way the Frontend views switch screens.
        public static void ShowOnly(RectTransform frame, params string[] names)
        {
            foreach (Transform child in frame) child.gameObject.SetActive(names.Contains(child.name));
        }

        private static string Render(Canvas canvas, RectTransform frame, string name, Vector2Int size)
        {
            var texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("MenuCaptureCamera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.065f, 0.12f);
                camera.orthographic = true;
                camera.targetTexture = texture;
                camera.cullingMask = ~0;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                scaler.enabled = false; // re-enabling recomputes the scale for this render size
                scaler.enabled = true;
                Canvas.ForceUpdateCanvases();
                frame.GetComponent<Frontend.FrontendLayout>()?.ApplyLayout();
                LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
                foreach (TMP_Text text in frame.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new UnityEngine.Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string path = Path.Combine(OutputFolder, $"{name}-{size.x}x{size.y}.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("Menu capture: " + path);
                return path;
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
