using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the runtime 날먹보호막 bubble around Erpin's drawing in an unsaved Preview Scene. The timeline loops
    // through a whole use: blow up, hits every interval, blinking last heart, pop.
    public sealed class ShieldBubblePreview : EditorWindow
    {
        private const float Loop = 7f;
        private Scene scene;
        private SceneView view;
        private SpriteRenderer player;
        private SpriteRenderer floor;
        private ShieldBubble bubble;
        private float hitInterval = 1.5f, time, speed = 1f;
        private int hearts = 2;
        private bool playing = true, showFloor = true, showFilm = true, showRim = true, showHighlight = true,
            showRipple = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Shield Bubble")]
        public static void Open() => GetWindow<ShieldBubblePreview>("Shield Bubble");

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
            floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, new Color(0.93f, 0.89f, 0.8f));
            texture.Apply();
            floor.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            floor.transform.localScale = new Vector3(12f, 8f, 1f);
            floor.sortingOrder = -100;
            SceneManager.MoveGameObjectToScene(floor.gameObject, scene);
            player = new GameObject("Erpin").AddComponent<SpriteRenderer>();
            player.sprite = DeathWardGlowVerification.LoadPlayerSprite();
            player.sortingOrder = 2;
            SceneManager.MoveGameObjectToScene(player.gameObject, scene);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Shield bubble preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite || player.sprite == null)
            {
                Debug.LogError("The isolated shield bubble Scene preview is not available (Unity API or Erpin sprite).");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(new Vector3(0f, 0.6f, 0f), Quaternion.identity, 1.6f, true, true);
            Rebuild();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime bubble around Erpin in a temporary Scene. 날먹보호막 gives " +
                "2 hearts; each enemy hit takes at least 1 heart. Final art review uses SampleScene Play " +
                "(F1 > — Power (R) — > Drop 날먹보호막 (4), pick it up, Fill gauge, R, then Hit 1 heart).",
                MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            hearts = EditorGUILayout.IntSlider("Shield hearts", hearts, 1, 4);
            hitInterval = EditorGUILayout.Slider("Hit interval (s)", hitInterval, 0.5f, 3f);
            showFloor = EditorGUILayout.Toggle("Floor backdrop", showFloor);
            showFilm = EditorGUILayout.Toggle("Rainbow film", showFilm);
            showRim = EditorGUILayout.Toggle("Rim", showRim);
            showHighlight = EditorGUILayout.Toggle("Highlights", showHighlight);
            showRipple = EditorGUILayout.Toggle("Hit ripple", showRipple);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, Loop);
            if (EditorGUI.EndChangeCheck()) Rebuild();
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 2f);
            playing = EditorGUILayout.Toggle("Play", playing);
            if (GUILayout.Button("Restart")) { time = 0f; Rebuild(); }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (player != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            time = Mathf.Repeat(time + delta * speed, Loop);
            Rebuild();
            Repaint();
        }

        private void Rebuild()
        {
            if (view == null || player == null) return;
            if (bubble == null)
            {
                bubble = ShieldBubble.Create(player);
                if (bubble == null) { Cleanup(); return; }
            }
            floor.enabled = showFloor;
            bubble.SetPreviewLayers(showFilm, showRim, showHighlight, showRipple);
            // The same schedule as the game: a hit every interval takes one heart, the last one pops the bubble.
            int hits = Mathf.FloorToInt(time / hitInterval);
            float popAt = hearts * hitInterval;
            if (hits < hearts)
            {
                float rippleAge = hits > 0 ? time - hits * hitInterval : -1f;
                bubble.RenderShield(time, time, hearts - hits <= 1, rippleAge);
            }
            else
            {
                bubble.RenderPop(time - popAt, time);
            }
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (floor != null)
            {
                Texture texture = floor.sprite != null ? floor.sprite.texture : null;
                if (floor.sprite != null) DestroyImmediate(floor.sprite);
                if (texture != null) DestroyImmediate(texture);
                DestroyImmediate(floor.gameObject);
            }
            if (player != null) DestroyImmediate(player.gameObject);
            floor = null;
            player = null;
            bubble = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
