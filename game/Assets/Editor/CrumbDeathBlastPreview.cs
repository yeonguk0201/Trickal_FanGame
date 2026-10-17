using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the runtime crumb death blast around the effect-crumb-death drawing in an unsaved Preview Scene.
    public sealed class CrumbDeathBlastPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private CrumbDeathBlastEffect effect;
        private SpriteRenderer body, drawing;
        private float radius = EnemyDeathExplosion.DefaultRadius, artWidth = CrumbDeathBlastVerification.ArtWidth;
        private float time = 0.12f, speed = 0.25f;
        private bool playing, showDrawing = true, showFlash = true, showPuff = true, showRing = true,
            showDroplets = true, showCrumbs = true, showSmoke = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Crumb Death Blast")]
        public static void Open() => GetWindow<CrumbDeathBlastPreview>("Crumb Death Blast");

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
            // Stands in for the dead minion's picture: only its sorting is used, like in the game.
            body = new GameObject("Body sorting").AddComponent<SpriteRenderer>();
            body.sortingOrder = 5;
            drawing = new GameObject("effect-crumb-death").AddComponent<SpriteRenderer>();
            drawing.sortingOrder = body.sortingOrder + 3;
            SceneManager.MoveGameObjectToScene(body.gameObject, scene);
            SceneManager.MoveGameObjectToScene(drawing.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Crumb death blast preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated crumb death blast Scene preview.");
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
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. The pink drawing shows for its game " +
                $"time ({EnemyDeathBlast.BodyBurstDuration} s) at 1.2 x the minion's width. Radius is the damage radius " +
                "(crumb minion: 1.2). Final art review uses SampleScene Play.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Damage radius", radius, 0.5f, 3f);
            artWidth = EditorGUILayout.Slider("Drawing width", artWidth, 0.3f, 3f);
            bool sizeChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            showDrawing = EditorGUILayout.Toggle("Pink drawing", showDrawing);
            showFlash = EditorGUILayout.Toggle("Core flash", showFlash);
            showPuff = EditorGUILayout.Toggle("Pink cloud", showPuff);
            showRing = EditorGUILayout.Toggle("Shockwave ring", showRing);
            showDroplets = EditorGUILayout.Toggle("Goo droplets", showDroplets);
            showCrumbs = EditorGUILayout.Toggle("Crumbs", showCrumbs);
            showSmoke = EditorGUILayout.Toggle("Smoke", showSmoke);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, CrumbDeathBlastEffect.Duration);
            if (EditorGUI.EndChangeCheck() || sizeChanged) Rebuild(sizeChanged);
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
            time = Mathf.Repeat(time + delta * speed, CrumbDeathBlastEffect.Duration);
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null) return;
            if (recreate || effect == null)
            {
                if (effect != null) DestroyImmediate(effect.gameObject);
                drawing.sprite = FairyKingdomArtwork.Fit(EnemyDeathBlast.BodyBurstArtwork, artWidth);
                effect = CrumbDeathBlastEffect.Create(Vector2.zero, radius, body, 4);
                if (effect == null) { Cleanup(); return; }
                SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            }
            effect.SimulateTo(time);
            effect.SetPreviewLayers(showFlash, showPuff, showRing, showDroplets, showCrumbs, showSmoke);
            CrumbDeathBlastVerification.ShowDrawing(drawing, showDrawing, time);
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (drawing != null) DestroyImmediate(drawing.gameObject);
            if (body != null) DestroyImmediate(body.gameObject);
            effect = null; drawing = null; body = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
