using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Uses the runtime geometry, material and emission rules in an unsaved Preview Scene.
    public sealed class ShashaWaterPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private GameObject owner;
        private ProceduralWaterStream effect;
        private float length = 5.5f, width = 0.2f, angle, time = 0.045f, speed = 0.25f;
        private bool playing, wall = true, showBody = true, showDrops = true, showSplash = true;
        private double lastTick;
        private const float Duration = 0.12f;

        [MenuItem("Trickal Fan Game/Effects/Preview Shasha Water")]
        public static void Open() => GetWindow<ShashaWaterPreview>("Shasha Water");

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
            owner = new GameObject("Shasha preview source");
            SceneManager.MoveGameObjectToScene(owner, scene);
            effect = ProceduralWaterStream.Create(owner);
            if (effect == null) { Cleanup(); return; }
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Shasha water preview");
            // Unity 6000.3 exposes isolated SceneView rendering internally. Guard the adapter
            // so an Editor upgrade cannot silently render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated Shasha Scene preview.");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(new Vector3(length * 0.5f, 0), Quaternion.identity, Mathf.Max(1, length * 0.65f), true, true);
            Rebuild();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Width and length are world units. Final art review uses SampleScene Play.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            length = EditorGUILayout.Slider("Length", length, 0.02f, 12);
            width = EditorGUILayout.Slider("Width", width, 0.05f, 0.6f);
            angle = EditorGUILayout.Slider("Direction", angle, -180, 180);
            wall = EditorGUILayout.Toggle("Wall contact", wall);
            showBody = EditorGUILayout.Toggle("Body", showBody);
            showDrops = EditorGUILayout.Toggle("Droplets", showDrops);
            showSplash = EditorGUILayout.Toggle("Contact foam", showSplash);
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
            Vector2 direction = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            effect.Begin(Vector2.zero, direction * length, width, 0, Duration, wall);
            effect.ResetPreviewParticles();
            const float step = 1f / 60f;
            float simulated = 0;
            while (simulated < time)
            {
                effect.EmitAt(simulated);
                float dt = Mathf.Min(step, time - simulated);
                effect.Droplets.Simulate(dt, false, false, false);
                simulated += dt;
            }
            effect.RenderAt(time);
            effect.SetPreviewLayers(showBody, showDrops, showSplash);
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
