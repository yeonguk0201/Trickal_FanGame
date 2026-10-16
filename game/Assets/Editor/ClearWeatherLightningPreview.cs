using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Uses the runtime geometry, shader and clock in an unsaved Preview Scene.
    public sealed class ClearWeatherLightningPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private GameObject owner;
        private ClearWeatherLightning effect;
        private float length = 7f, width = ClearWeatherLightning.DefaultWidth, time = 0.04f, speed = 0.25f;
        private Vector2 target;
        private bool playing;
        private double lastTick;


        [MenuItem("Trickal Fan Game/Effects/Preview Clear Weather Lightning")]
        public static void Open() => GetWindow<ClearWeatherLightningPreview>("Clear Weather Lightning");

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayChange;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            lastTick = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayChange;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            Cleanup();
        }

        private void OnPlayChange(PlayModeStateChange state) { playing = false; Cleanup(); }

        private void CreatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Cleanup();
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("Lightning preview source");
            SceneManager.MoveGameObjectToScene(owner, scene);
            // Copy only artwork, never gameplay scripts, from the selected scale references.
            foreach (GameObject selected in Selection.gameObjects)
            foreach (SpriteRenderer original in selected.GetComponentsInChildren<SpriteRenderer>())
            {
                var copy = new GameObject("Reference " + original.name);
                SceneManager.MoveGameObjectToScene(copy, scene);
                copy.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                copy.transform.localScale = original.transform.lossyScale;
                var sprite = copy.AddComponent<SpriteRenderer>();
                sprite.sprite = original.sprite;
                sprite.sharedMaterial = original.sharedMaterial;
                sprite.color = original.color;
                sprite.flipX = original.flipX; sprite.flipY = original.flipY;
                sprite.sortingLayerID = original.sortingLayerID;
                sprite.sortingOrder = original.sortingOrder;
            }
            effect = ClearWeatherLightning.Create(Vector3.up * length, Vector3.zero, width);
            if (effect == null) { Cleanup(); return; }
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Clear weather lightning preview");
            // Unity 6000.3 exposes isolated SceneView rendering internally. Guard the adapter
            // so an Editor upgrade cannot silently render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated lightning Scene preview.");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(new Vector3(0, length * 0.5f), Quaternion.identity, Mathf.Max(1, length * 0.65f), true, true);
            Rebuild();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Select character/background sprites before opening to copy scale references. No selection gives effect-only preview. Impact position matches their world coordinates. Values are temporary; final art review uses SampleScene Play.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            length = EditorGUILayout.Slider("Length", length, 0.02f, 12);
            width = EditorGUILayout.Slider("Width", width, 0.015f, 0.15f);
            target = EditorGUILayout.Vector2Field("Impact position", target);
            time = EditorGUILayout.Slider("Burst time (seconds)", time, 0, 0.4f);
            if (EditorGUI.EndChangeCheck()) Rebuild();
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 1);
            playing = EditorGUILayout.Toggle("Loop playback", playing);
            if (GUILayout.Button("Restart")) { time = 0; Rebuild(); }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (effect != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || effect == null) return;
            time = Mathf.Repeat(time + delta * speed, 0.4f);
            Rebuild();
            Repaint();
        }

        private void Rebuild()
        {
            if (effect == null) return;
            effect.Configure((Vector3)target + Vector3.up * length, target, width);
            effect.RenderAt(time);
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (owner != null) DestroyImmediate(owner);
            effect = null; owner = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
