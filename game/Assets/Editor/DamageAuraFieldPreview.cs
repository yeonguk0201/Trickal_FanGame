using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Plays the runtime 광기의 가면 floor field around Erpin's drawing in an unsaved Preview Scene.
    public sealed class DamageAuraFieldPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private SpriteRenderer player;
        private SpriteRenderer floor;
        private DamageAuraField field;
        private float radius = DamageAuraFieldVerification.Radius, interval = 1f;
        private float time, speed = 1f;
        private int stacks = 1;
        private bool playing = true, showFloor = true, showFill = true, showRipples = true, showRim = true,
            showTick = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Damage Aura Field")]
        public static void Open() => GetWindow<DamageAuraFieldPreview>("Damage Aura Field");

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
            // A plain light floor close to the room colour, so the pale red reads as it does in game.
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
            view.titleContent = new GUIContent("Damage aura preview");
            // Same guarded adapter as the Shasha preview: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite || player.sprite == null)
            {
                Debug.LogError("The isolated damage aura Scene preview is not available (Unity API or Erpin sprite).");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(Vector3.zero, Quaternion.identity, 3.6f, true, true);
            Rebuild();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime field around Erpin in a temporary Scene. The mask aura is " +
                "2.5 m with a 1 s tick. Final art review uses SampleScene Play " +
                "(F1 > — Artifact — > Show artifact drops > Drop 광기의 가면).", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            radius = EditorGUILayout.Slider("Radius (m)", radius, 0.5f, 5f);
            stacks = EditorGUILayout.IntSlider("Mask stacks", stacks, 1, DamageAuraField.MaxStacks);
            interval = EditorGUILayout.Slider("Tick interval (s)", interval, 0.3f, 3f);
            showFloor = EditorGUILayout.Toggle("Floor backdrop", showFloor);
            showFill = EditorGUILayout.Toggle("Fill", showFill);
            showRipples = EditorGUILayout.Toggle("Ripples", showRipples);
            showRim = EditorGUILayout.Toggle("Wobbling rim", showRim);
            showTick = EditorGUILayout.Toggle("Tick ring", showTick);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, 20f);
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
            time = Mathf.Repeat(time + delta * speed, 20f);
            Rebuild();
            Repaint();
        }

        private void Rebuild()
        {
            if (view == null || player == null) return;
            if (field == null)
            {
                field = DamageAuraField.Create(player.transform, player.sortingLayerID);
                if (field == null) { Cleanup(); return; }
            }
            floor.enabled = showFloor;
            field.SetPreviewLayers(showFill, showRipples, showRim, showTick);
            // Ticks on the same schedule as the game: the first one an interval after the start.
            float sinceTick = time >= interval ? Mathf.Repeat(time, interval) : -1f;
            field.RenderAt(time, radius, stacks, Mathf.Clamp01(time / DamageAuraField.FadeSeconds), sinceTick);
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
            field = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
