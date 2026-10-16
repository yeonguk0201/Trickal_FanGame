using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the runtime bomb burst over the explosion drawing in an unsaved Preview Scene.
    public sealed class BombBurstPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private BombBurstEffect effect;
        private SpriteRenderer drawing;
        private float radius = PlacedBomb.DefaultExplosionRadius, time = 0.2f, speed = 0.25f;
        private bool playing, showRing = true, showEmbers = true, showDrawing = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Bomb Burst")]
        public static void Open() => GetWindow<BombBurstPreview>("Bomb Burst");

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
            drawing = new GameObject("Explosion drawing").AddComponent<SpriteRenderer>();
            drawing.sortingOrder = 5;
            SceneManager.MoveGameObjectToScene(drawing.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Bomb burst preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated bomb burst Scene preview.");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(Vector3.zero, Quaternion.identity, Mathf.Max(1f, radius * 1.4f), true, true);
            Rebuild(true);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. The explosion drawing shows for its " +
                "game time (0.15 s). Radius is the damage radius (placed bomb: 2). Final art review uses SampleScene Play.",
                MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Damage radius", radius, 0.5f, 3f);
            bool radiusChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            showDrawing = EditorGUILayout.Toggle("Explosion drawing", showDrawing);
            showRing = EditorGUILayout.Toggle("Shockwave ring", showRing);
            showEmbers = EditorGUILayout.Toggle("Embers", showEmbers);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, BombBurstEffect.Duration);
            if (EditorGUI.EndChangeCheck() || radiusChanged) Rebuild(radiusChanged);
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 1f);
            playing = EditorGUILayout.Toggle("Loop playback", playing);
            if (GUILayout.Button("Restart")) { time = 0f; Rebuild(false); }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (drawing != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            time = Mathf.Repeat(time + delta * speed, BombBurstEffect.Duration);
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null) return;
            if (recreate || effect == null)
            {
                if (effect != null) DestroyImmediate(effect.gameObject);
                drawing.sprite = FairyKingdomArtwork.Fit("effect-bomb-explosion", radius * 2f);
                effect = BombBurstEffect.Create(Vector2.zero, radius, drawing, 4);
                if (effect == null) { Cleanup(); return; }
                SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            }
            effect.SimulateTo(time);
            effect.SetPreviewLayers(showRing, showEmbers);
            drawing.enabled = showDrawing && time < PlacedBomb.ExplosionVisualDuration;
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (drawing != null) DestroyImmediate(drawing.gameObject);
            effect = null; drawing = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
