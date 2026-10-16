using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Uses the runtime quads, shader, emission rules and particle settings in an unsaved Preview Scene.
    public sealed class MuffinExplosionPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private MuffinExplosionEffect effect;
        private SpriteRenderer enemy, original;
        private Sprite[] originalFrames;
        private float radius = 1.5f, time = 0.1f, speed = 0.25f;
        private bool playing, showFlash = true, showFireball = true, showRing = true, showSparks = true,
            showSmoke = true, showEnemy = true, showOriginal;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Muffin Explosion")]
        public static void Open() => GetWindow<MuffinExplosionPreview>("Muffin Explosion");

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
            enemy = new GameObject("Reference enemy").AddComponent<SpriteRenderer>();
            enemy.sprite = FairyKingdomArtwork.Fit("crumb-minion-cream", 1f);
            SceneManager.MoveGameObjectToScene(enemy.gameObject, scene);
            original = new GameObject("Original circular-explosion").AddComponent<SpriteRenderer>();
            original.sortingOrder = 2;
            originalFrames = CombatEffectArtwork.Frames("circular-explosion", 4, 2);
            SceneManager.MoveGameObjectToScene(original.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Muffin explosion preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated muffin explosion Scene preview.");
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
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Radius is the damage radius in world " +
                "units (폭발 머핀: 1.5); the ring's outer edge stops there. Final art review uses SampleScene Play.",
                MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Damage radius", radius, 0.5f, 3f);
            bool radiusChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            showFlash = EditorGUILayout.Toggle("Core flash", showFlash);
            showFireball = EditorGUILayout.Toggle("Fireball", showFireball);
            showRing = EditorGUILayout.Toggle("Shockwave ring", showRing);
            showSparks = EditorGUILayout.Toggle("Sparks", showSparks);
            showSmoke = EditorGUILayout.Toggle("Smoke", showSmoke);
            showEnemy = EditorGUILayout.Toggle("Reference enemy", showEnemy);
            showOriginal = EditorGUILayout.Toggle("Original sheet (A/B)", showOriginal);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, MuffinExplosionEffect.Duration);
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
            if (enemy != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            time = Mathf.Repeat(time + delta * speed, MuffinExplosionEffect.Duration);
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null) return;
            if (recreate || effect == null)
            {
                if (effect != null) DestroyImmediate(effect.gameObject);
                effect = MuffinExplosionEffect.Create(Vector2.zero, radius, enemy, 7);
                if (effect == null) { Cleanup(); return; }
                SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            }
            effect.SimulateTo(time);
            bool showNew = !showOriginal;
            effect.SetPreviewLayers(showNew && showFlash, showNew && showFireball, showNew && showRing,
                showNew && showSparks, showNew && showSmoke);
            enemy.enabled = showEnemy;
            original.enabled = showOriginal && originalFrames.Length > 0 && time < MuffinExplosionEffect.Duration;
            if (original.enabled)
            {
                // The former display: the same sheet and size PlayerBasicAttackExplosion used to play.
                original.sprite = originalFrames[Mathf.Min(originalFrames.Length - 1,
                    (int)(time / MuffinExplosionEffect.Duration * originalFrames.Length))];
                CombatEffectArtwork.Size(original, radius * 2.2f, radius * 2.2f);
            }
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (enemy != null) DestroyImmediate(enemy.gameObject);
            if (original != null) DestroyImmediate(original.gameObject);
            effect = null; enemy = null; original = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
