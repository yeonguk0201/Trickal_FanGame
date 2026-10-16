using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Uses the runtime geometry, shader and clock in an unsaved Preview Scene.
    public sealed class HitSparkPreview : EditorWindow
    {
        private const float LoopLength = 0.3f;
        private Scene scene;
        private SceneView view;
        private HitSpark effect;
        private float radius = HitSpark.DefaultRadius, angle, time = HitSpark.LeadTime, speed = 0.25f;
        private Vector2 impact;
        private bool playing;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Hit Spark")]
        public static void Open() => GetWindow<HitSparkPreview>("Hit Spark");

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
            effect = HitSpark.Create(impact, Incoming(), radius);
            if (effect == null) { Cleanup(); return; }
            // Above any copied reference, as the runtime spark is above the enemy it hits.
            effect.Visual.sortingOrder = short.MaxValue;
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Hit spark preview");
            // Unity 6000.3 exposes isolated SceneView rendering internally. Guard the adapter
            // so an Editor upgrade cannot silently render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated hit spark Scene preview.");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(impact, Quaternion.identity, 1.5f, true, true);
            Rebuild();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Select character/background sprites before opening to copy scale references. No selection gives effect-only preview. Values are temporary; final art review uses SampleScene Play.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Radius", radius, 0.1f, 1.5f);
            angle = EditorGUILayout.Slider("Shot direction (degrees)", angle, 0f, 360f);
            impact = EditorGUILayout.Vector2Field("Impact position", impact);
            time = EditorGUILayout.Slider("Burst time (seconds)", time, 0, LoopLength);
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
            time = Mathf.Repeat(time + delta * speed, LoopLength);
            Rebuild();
            Repaint();
        }

        private Vector2 Incoming() =>
            new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        private void Rebuild()
        {
            if (effect == null) return;
            effect.transform.position = impact;
            effect.Configure(Incoming(), radius);
            effect.RenderAt(time);
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            effect = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
