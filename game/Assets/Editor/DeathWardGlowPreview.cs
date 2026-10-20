using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the runtime 레비의 단도 glow on Erpin's drawing in an unsaved Preview Scene.
    public sealed class DeathWardGlowPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private SpriteRenderer player;
        private DeathWardGlow glow;
        private float duration = DeathWardGlowVerification.Duration;
        private float time = 0.06f, speed = 0.5f;
        private bool playing, flip, showOutline = true, showFlash = true, showRing = true, showSparkles = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Dagger Ward Glow")]
        public static void Open() => GetWindow<DeathWardGlowPreview>("Dagger Ward Glow");

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
            player = new GameObject("Erpin").AddComponent<SpriteRenderer>();
            player.sprite = DeathWardGlowVerification.LoadPlayerSprite();
            player.sortingOrder = 10;
            SceneManager.MoveGameObjectToScene(player.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Dagger ward preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite || player.sprite == null)
            {
                Debug.LogError("The isolated dagger ward Scene preview is not available (Unity API or Erpin sprite).");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(Vector3.zero, Quaternion.identity, 1.4f, true, true);
            Rebuild(true);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime glow on Erpin's walking frame in a temporary Scene. The game " +
                "invulnerability is 5 s; the last second blinks. Final art review uses SampleScene Play " +
                "(F1 > — Artifact — > Lethal hit).", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            duration = EditorGUILayout.Slider("Invulnerability (s)", duration, 1.5f, 8f);
            bool durationChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            flip = EditorGUILayout.Toggle("Flip drawing", flip);
            showOutline = EditorGUILayout.Toggle("Outline and shine", showOutline);
            showFlash = EditorGUILayout.Toggle("Flash", showFlash);
            showRing = EditorGUILayout.Toggle("Ring", showRing);
            showSparkles = EditorGUILayout.Toggle("Sparkles", showSparkles);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, duration);
            if (EditorGUI.EndChangeCheck() || durationChanged) Rebuild(durationChanged);
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 1f);
            playing = EditorGUILayout.Toggle("Loop playback", playing);
            if (GUILayout.Button("Restart")) { time = 0f; Rebuild(false); }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (player != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            time = Mathf.Repeat(time + delta * speed, duration);
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null || player == null) return;
            if (recreate || glow == null)
            {
                if (glow != null) glow.Stop();
                glow = DeathWardGlow.Create(player, duration);
                if (glow == null) { Cleanup(); return; }
            }
            player.flipX = flip;
            glow.RenderAt(time);
            glow.SetPreviewLayers(showOutline, showFlash, showRing, showSparkles);
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (player != null) DestroyImmediate(player.gameObject);
            player = null;
            glow = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
