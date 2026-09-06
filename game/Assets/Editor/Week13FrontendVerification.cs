using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13FrontendVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Frontend Title")]
        public static void Verify()
        {
            Scene scene = SceneManager.GetActiveScene();
            Assert(scene.path == Week13FrontendSetup.ScenePath, "Open FrontendScene before verification.");
            ValidateScene(scene);
            if (!EditorApplication.isPlaying) VerifyLayouts(scene);
            Debug.Log("Week 13 Frontend verification passed: scene isolation, build entry, references, scaler, safe area and title layout.");
        }

        public static void ValidateScene(Scene scene)
        {
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Assert(enabled.Length >= 2 && enabled[0].path == Week13FrontendSetup.ScenePath,
                "Frontend must be the first enabled build scene.");
            Assert(enabled.Count(s => s.path == Week13FrontendSetup.ScenePath) == 1 &&
                enabled.Count(s => s.path == Week13FrontendSetup.GameScenePath) == 1, "Duplicate or missing build scene.");
            Component[] components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            foreach (Component component in components)
            {
                Assert(component != null, "Missing script in Frontend.");
                string ns = component.GetType().Namespace ?? "";
                Assert(!ns.StartsWith("TrickalFanGame.") || ns == "TrickalFanGame.Frontend",
                    "Combat, Run or Backend component leaked into Frontend: " + component.GetType().Name);
                Assert(component is not Renderer && component is not Collider2D && component is not Rigidbody2D,
                    "World combat object leaked into Frontend.");
            }
            Assert(components.OfType<Canvas>().Count() == 1, "Exactly one Canvas is required.");
            Assert(components.OfType<Camera>().Count() == 1 && components.OfType<Camera>().Single().cullingMask == 0,
                "Frontend camera must only clear the background, without rendering world objects.");
            Assert(components.OfType<EventSystem>().Count() == 1 && components.OfType<InputSystemUIInputModule>().Count() == 1,
                "Exactly one EventSystem and Input System UI module are required.");
            Canvas canvas = components.OfType<Canvas>().Single();
            Assert(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.GetComponent<GraphicRaycaster>() != null,
                "Frontend requires Overlay Canvas and GraphicRaycaster.");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080) &&
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f), "Canvas Scaler differs from UI spec.");
            Assert(components.OfType<FrontendLayout>().Count() == 1, "Exactly one reference frame is required.");
            RectTransform safe = canvas.transform.Find("ReferenceFrame/SafeArea") as RectTransform;
            Assert(safe != null && safe.anchorMin == Vector2.zero && safe.anchorMax == Vector2.one &&
                safe.offsetMin == new Vector2(64, 54) && safe.offsetMax == new Vector2(-64, -54), "Safe area differs from UI spec.");
            Assert(components.OfType<FrontendTitleView>().Count() == 1 && components.OfType<Button>().Count() == 1,
                "Exactly one title view and start button are required.");
            FrontendTitleView view = components.OfType<FrontendTitleView>().Single();
            Assert(view.StartButton != null && view.StatusText != null && view.StartButton is FrontendStartButton,
                "Missing title references.");
            Assert(view.StartButton.gameObject.scene == scene && view.StatusText.gameObject.scene == scene,
                "Title references must stay inside Frontend.");
            Assert(view.StartButton.GetComponent<RectTransform>().sizeDelta == new Vector2(280, 64), "Start button must be 280 x 64.");
            TMP_Text label = view.StartButton.GetComponentInChildren<TMP_Text>();
            Assert(label != null && label.text == "GAME START" && label.fontSize == 24 && !label.raycastTarget,
                "Start label differs from UI spec.");
            Assert(components.OfType<EventSystem>().Single().firstSelectedGameObject == view.StartButton.gameObject,
                "Initial focus must point to GAME START.");
            Assert(view.StartButton.onClick.GetPersistentEventCount() == 0, "Unexpected persistent action on title button.");
            var serializedButton = new SerializedObject(view.StartButton);
            Assert(serializedButton.FindProperty("hoverBorder").objectReferenceValue != null &&
                serializedButton.FindProperty("focusBorder").objectReferenceValue != null, "Missing button border references.");
            foreach (TMP_Text text in components.OfType<TMP_Text>())
                Assert(text.font != null && text.font.HasCharacters(text.text), "Missing TMP font or glyph: " + text.name);
            Assert(view.StatusText.font.HasCharacters(FrontendTitleView.ReadyMessage), "Missing Korean status glyphs.");
        }

        // Exercise actual RectTransforms/TMP preferred sizes without changing the open scene.
        private static void VerifyLayouts(Scene source)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject clone = null;
            try
            {
                clone = Object.Instantiate(source.GetRootGameObjects().Single(r => r.GetComponent<Canvas>() != null));
                SceneManager.MoveGameObjectToScene(clone, preview);
                Canvas canvas = clone.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                clone.GetComponent<CanvasScaler>().enabled = false;
                FrontendLayout layout = clone.GetComponentInChildren<FrontendLayout>();
                RectTransform canvasRect = (RectTransform)clone.transform;
                RectTransform safe = (RectTransform)layout.transform.Find("SafeArea");
                FrontendTitleView view = clone.GetComponent<FrontendTitleView>();
                view.RequestStart();
                foreach (Vector2 resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(1600, 1200), new Vector2(2560, 1080) })
                {
                    canvasRect.sizeDelta = resolution;
                    layout.ApplyLayout();
                    Canvas.ForceUpdateCanvases();
                    foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.ForceMeshUpdate(true);
                        Vector2 preferred = text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity);
                        Assert(preferred.y <= text.rectTransform.rect.height + 1, $"{resolution}: text height overflow: {text.name}");
                        Assert(text.preferredWidth <= text.rectTransform.rect.width + 1, $"{resolution}: text width overflow: {text.name}");
                        AssertInside(text.rectTransform, safe, resolution + " safe area: " + text.name);
                    }
                    AssertInside(safe, canvasRect, resolution + " safe area outside viewport");
                    AssertInside(view.StartButton.GetComponent<RectTransform>(), safe, resolution + " button");
                    Assert(Mathf.Abs(((RectTransform)layout.transform).rect.width / ((RectTransform)layout.transform).rect.height - 16f / 9f) < 0.001f,
                        "Content aspect ratio changed.");
                    Debug.Log($"Week 13 layout passed: {resolution.x} x {resolution.y}; TMP bounds and safe area.");
                }
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Trickal Fan Game/Week 13/Export Title Previews (720p and 1080p)")]
        public static void ExportPreviews()
        {
            Scene source = SceneManager.GetActiveScene();
            Assert(source.path == Week13FrontendSetup.ScenePath, "Open FrontendScene first.");
            ValidateScene(source);
            string directory = "Logs/Week13FrontendPreviews";
            Directory.CreateDirectory(directory);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject clone = Object.Instantiate(source.GetRootGameObjects().Single(r => r.GetComponent<Canvas>() != null));
                SceneManager.MoveGameObjectToScene(clone, preview);
                Canvas canvas = clone.GetComponent<Canvas>();
                clone.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                clone.transform.position = Vector3.zero;
                clone.transform.localScale = Vector3.one;
                var cameraObject = new GameObject("Title Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100;
                camera.enabled = false;
                canvas.worldCamera = camera;
                foreach (Vector2Int resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    ((RectTransform)clone.transform).sizeDelta = resolution;
                    clone.GetComponentInChildren<FrontendLayout>().ApplyLayout();
                    camera.orthographicSize = resolution.y / 2f;
                    camera.aspect = (float)resolution.x / resolution.y;
                    Canvas.ForceUpdateCanvases();
                    foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
                    RenderTexture target = new RenderTexture(resolution.x, resolution.y, 24);
                    RenderTexture previous = RenderTexture.active;
                    Texture2D image = null;
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        image = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
                        image.Apply();
                        string path = Path.Combine(directory, $"title-{resolution.x}x{resolution.y}.png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        Debug.Log("Title preview exported: " + Path.GetFullPath(path));
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previous;
                        if (image != null) Object.DestroyImmediate(image);
                        target.Release();
                        Object.DestroyImmediate(target);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void AssertInside(RectTransform child, RectTransform parent, string message)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Rect bounds = parent.rect;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = parent.InverseTransformPoint(corner);
                Assert(point.x >= bounds.xMin - 0.1f && point.x <= bounds.xMax + 0.1f &&
                    point.y >= bounds.yMin - 0.1f && point.y <= bounds.yMax + 0.1f, message);
            }
        }

        public static void SetupAndVerifyBatch()
        {
            string gameBefore = File.ReadAllText(Week13FrontendSetup.GameScenePath, System.Text.Encoding.UTF8);
            Week13FrontendSetup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            int count = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Week13FrontendSetup.Setup();
            Assert(guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath), "Setup changed scene GUID.");
            Assert(count == Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Setup duplicated scene objects.");
            Assert(gameBefore == File.ReadAllText(Week13FrontendSetup.GameScenePath, System.Text.Encoding.UTF8), "Setup changed Game Scene.");
            Verify();
            VerifyRejections();
            ExportPreviews();
            Debug.Log("Week 13 batch verification passed: setup twice, stable GUID, unchanged Game Scene, invalid-state rejection.");
        }

        private static void VerifyRejections()
        {
            Scene scene = SceneManager.GetActiveScene();
            FrontendTitleView view = Object.FindFirstObjectByType<FrontendTitleView>();
            Button button = view.StartButton;
            TMP_Text status = view.StatusText;
            try
            {
                view.Configure(null, status);
                ExpectFailure(() => ValidateScene(scene), "missing reference");
            }
            finally { view.Configure(button, status); }
            GameObject duplicate = Object.Instantiate(button.gameObject, button.transform.parent);
            try { ExpectFailure(() => ValidateScene(scene), "duplicate button"); }
            finally { Object.DestroyImmediate(duplicate); }
            GameObject combat = new GameObject("Invalid Combat Object", typeof(BoxCollider2D));
            try { ExpectFailure(() => ValidateScene(scene), "combat object"); }
            finally { Object.DestroyImmediate(combat); }
            var builds = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = builds.Reverse().ToArray();
                ExpectFailure(() => ValidateScene(scene), "wrong boot scene");
            }
            finally { EditorBuildSettings.scenes = builds; }
            ValidateScene(scene);
        }

        private static void ExpectFailure(Action action, string label)
        {
            bool failed = false;
            try { action(); } catch (InvalidOperationException) { failed = true; }
            Assert(failed, "Validator accepted " + label);
        }

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }

    [InitializeOnLoad]
    public static class Week13FrontendPlayVerification
    {
        private const string PendingKey = "Week13FrontendPlayVerification.Pending";
        private static double started;

        static Week13FrontendPlayVerification()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        // Run separately without -quit: the Editor exits only after Play Mode assertions finish.
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                if (EditorApplication.timeSinceStartup - started > 120) Finish(1, "Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;
            try
            {
                Week13FrontendVerification.ValidateScene(SceneManager.GetActiveScene());
                FrontendTitleView view = Object.FindFirstObjectByType<FrontendTitleView>();
                if (EventSystem.current.currentSelectedGameObject != view.StartButton.gameObject)
                    throw new InvalidOperationException("Initial keyboard focus is missing.");
                int before = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                view.StartButton.onClick.Invoke();
                ExecuteEvents.Execute(view.StartButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                view.StartButton.onClick.Invoke();
                if (view.StatusText.text != FrontendTitleView.ReadyMessage)
                    throw new InvalidOperationException("Click/submit did not reach the title status.");
                if (SceneManager.sceneCount != 1 || before != Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length)
                    throw new InvalidOperationException("Repeated start requests created objects or changed scenes.");
                Week13FrontendVerification.ValidateScene(SceneManager.GetActiveScene());
                Finish(0, "Week 13 Play Mode verification passed: initial focus, click/submit, repeated start, combat isolation.");
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(code);
        }
    }
}
