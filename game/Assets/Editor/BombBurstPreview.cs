using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the lit bomb's fuse flash, then the runtime bomb burst over the explosion drawing, in an unsaved
    // Preview Scene. Negative time is the fuse; 0 is the explosion.
    public sealed class BombBurstPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private BombBurstEffect effect;
        private SpriteRenderer drawing;
        private BombFuseFlash flash;
        private Sprite bombSprite, explosionSprite;
        private const float BombWidth = 0.8f;
        private float radius = PlacedBomb.DefaultExplosionRadius, time = 0.2f, speed = 0.25f;
        private float fuse = PlacedBomb.DefaultFuseDuration;
        private bool playing, showRing = true, showEmbers = true, showDrawing = true, showScorch = true, showFlash = true;
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
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Negative time is the fuse (placed bomb " +
                "0.75 s, exploding obstacle 0.5 s); the explosion drawing then shows for its game time (0.15 s). " +
                "Radius is the damage radius (placed bomb: 2). Final art review uses SampleScene Play.",
                MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Damage radius", radius, 0.5f, 3f);
            fuse = EditorGUILayout.Slider("Fuse (seconds)", fuse, 0.2f, 1.5f);
            bool radiusChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            showFlash = EditorGUILayout.Toggle("Fuse flash", showFlash);
            showDrawing = EditorGUILayout.Toggle("Explosion drawing", showDrawing);
            showRing = EditorGUILayout.Toggle("Shockwave ring", showRing);
            showEmbers = EditorGUILayout.Toggle("Embers", showEmbers);
            showScorch = EditorGUILayout.Toggle("Scorch mark", showScorch);
            time = EditorGUILayout.Slider("Time (seconds)", time, -fuse, BombBurstEffect.Lifetime);
            if (EditorGUI.EndChangeCheck() || radiusChanged) Rebuild(radiusChanged);
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 1f);
            playing = EditorGUILayout.Toggle("Loop playback", playing);
            if (GUILayout.Button("Restart from fuse")) { time = -fuse; Rebuild(false); }
            if (GUILayout.Button("Restart from explosion")) { time = 0f; Rebuild(false); }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (drawing != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            time = Mathf.Repeat(time + fuse + delta * speed, fuse + BombBurstEffect.Lifetime) - fuse;
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null) return;
            if (recreate || effect == null)
            {
                if (effect != null) DestroyImmediate(effect.gameObject);
                explosionSprite = FairyKingdomArtwork.Fit("effect-bomb-explosion", radius * 2f);
                bombSprite = FairyKingdomArtwork.Fit("pickup-bomb", BombWidth);
                effect = BombBurstEffect.Create(Vector2.zero, radius, drawing, 4);
                if (effect == null) { Cleanup(); return; }
                SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            }
            bool lit = time < 0f;
            drawing.sprite = lit ? bombSprite : explosionSprite;
            drawing.enabled = lit || (showDrawing && time < PlacedBomb.ExplosionVisualDuration);
            if (flash == null) flash = BombFuseFlash.Attach(drawing, 0f, fuse);
            if (flash != null)
            {
                flash.Configure(0f, fuse);
                flash.RenderAt(lit && showFlash ? time + fuse : fuse);
            }
            effect.gameObject.SetActive(!lit);
            if (!lit)
            {
                effect.SimulateTo(time);
                effect.SetPreviewLayers(showRing, showEmbers, showScorch);
            }
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (drawing != null) DestroyImmediate(drawing.gameObject);
            effect = null; drawing = null; flash = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
