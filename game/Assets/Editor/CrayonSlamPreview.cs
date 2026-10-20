using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Crayon hero slam in an unsaved Preview Scene: runtime meshes, shaders, layout and clock.
    // No pattern, damage or Prefab script runs; only the boss artwork is copied as a scale reference.
    public sealed class CrayonSlamPreview : EditorWindow
    {
        private const string BossPrefab = "Assets/Prefabs/CrayonHeroBoss.prefab";
        private Scene scene;
        private SceneView view;
        private GameObject boss;
        private LineRenderer roomOutline, warningLine;
        private Material lineMaterial;
        private LayeredSlamEffect effect;
        private Vector2 roomSize = new(15f, 11f), bossPosition = new(3f, -1f);
        private float direction = 180f, warningLength = 18f, height = 7f, time = 0.12f, speed = 0.25f;
        private bool playing, showBlade = true, showFlash = true, showGround = true, showDebris = true,
            showBoss = true, showWarning = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Crayon Hero Slam")]
        public static void Open() => GetWindow<CrayonSlamPreview>("Crayon Hero Slam");

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
            boss = new GameObject("Reference crayon hero");
            SceneManager.MoveGameObjectToScene(boss, scene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab);
            if (prefab != null)
                foreach (SpriteRenderer original in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!original.enabled || original.sprite == null) continue;
                    var copy = new GameObject("Artwork " + original.name);
                    copy.transform.SetParent(boss.transform, false);
                    copy.transform.localPosition = original.transform.position - prefab.transform.position;
                    copy.transform.localScale = original.transform.lossyScale;
                    var sprite = copy.AddComponent<SpriteRenderer>();
                    sprite.sprite = original.sprite;
                    sprite.color = original.color;
                    sprite.flipX = original.flipX;
                    sprite.sortingLayerID = original.sortingLayerID;
                    sprite.sortingOrder = original.sortingOrder;
                }
            lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            roomOutline = Line("Room bounds", new Color(1f, 1f, 1f, 0.5f), 0.05f, true);
            warningLine = Line("Warning path", new Color(1f, 0.25f, 0.05f, 0.6f), 0.08f, false);
            view = CreateInstance<SceneView>();
            view.titleContent = new GUIContent("Crayon hero slam preview");
            // Same guarded adapter as the other effect previews: never render or edit the user's active Scene.
            var customScene = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite)
            {
                Debug.LogError("This Unity Editor does not support the isolated slam Scene preview.");
                Cleanup();
                return;
            }
            customScene.SetValue(view, scene);
            view.in2DMode = true;
            view.Show();
            view.LookAt(new Vector3(0f, 2f), Quaternion.identity, Mathf.Max(roomSize.x, roomSize.y) * 0.75f, true, true);
            Rebuild(true);
        }

        private LineRenderer Line(string name, Color color, float width, bool loop)
        {
            var owner = new GameObject(name);
            SceneManager.MoveGameObjectToScene(owner, scene);
            var line = owner.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
            line.loop = loop;
            line.useWorldSpace = true;
            line.sortingOrder = -10;
            return line;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime slam layers in a temporary Scene. The path uses the same " +
                "SlamLightEffect layout as Play (warning start, -0.25 ground offset, room fit). Values are temporary; " +
                "final art review uses Boss Test Room Play.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            roomSize = EditorGUILayout.Vector2Field("Room size", roomSize);
            bossPosition = EditorGUILayout.Vector2Field("Boss / warning start", bossPosition);
            direction = EditorGUILayout.Slider("Direction (degrees)", direction, -180f, 180f);
            using (new EditorGUILayout.HorizontalScope())
                foreach (float preset in new[] { 180f, 0f, 90f, -90f, 135f, -45f })
                    if (GUILayout.Button(preset + "°")) direction = preset;
            warningLength = EditorGUILayout.Slider("Warning length", warningLength, 0.5f, 24f);
            height = EditorGUILayout.Slider("Blade height (5-9)", height, 5f, 9f);
            bool layoutChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            showFlash = EditorGUILayout.Toggle("Falling flash (v22)", showFlash);
            showBlade = EditorGUILayout.Toggle("Painted blade", showBlade);
            showGround = EditorGUILayout.Toggle("Ground shock", showGround);
            showDebris = EditorGUILayout.Toggle("Debris", showDebris);
            showBoss = EditorGUILayout.Toggle("Reference boss", showBoss);
            showWarning = EditorGUILayout.Toggle("Warning path", showWarning);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, LayeredSlamEffect.Duration);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Flash falling")) time = 0.05f;
                if (GUILayout.Button("Impact")) time = SlamLightEffect.FallSeconds;
                if (GUILayout.Button("Erosion")) time = 0.3f;
                if (GUILayout.Button("Peak hold")) time = 0.6f;
            }
            bool frameChanged = EditorGUI.EndChangeCheck();
            if (layoutChanged || frameChanged) Rebuild(layoutChanged);
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 1f);
            playing = EditorGUILayout.Toggle("Loop playback", playing);
            if (GUILayout.Button("Restart")) { time = 0f; Rebuild(false); }
            if (effect != null)
                EditorGUILayout.LabelField("Flash beams", effect.FlashBeamCount.ToString());
            else if (view != null)
                EditorGUILayout.HelpBox("The boss is outside the room or the path is empty.", MessageType.Warning);
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastTick));
            lastTick = now;
            if (boss != null && view == null) { playing = false; Cleanup(); return; }
            if (!playing || view == null) return;
            // A short pause after the effect ends separates the loops.
            time = Mathf.Repeat(time + delta * speed, LayeredSlamEffect.Duration + 0.3f);
            Rebuild(false);
            Repaint();
        }

        private void Rebuild(bool recreate)
        {
            if (view == null) return;
            var room = new Rect(-roomSize * 0.5f, roomSize);
            Vector2 aim = new(Mathf.Cos(direction * Mathf.Deg2Rad), Mathf.Sin(direction * Mathf.Deg2Rad));
            Vector2 end = bossPosition + aim * warningLength;
            if (recreate || effect == null)
            {
                if (effect != null) DestroyImmediate(effect.gameObject);
                effect = null;
                if (SlamLightEffect.TryGetFittedPathLayout(bossPosition, end, height, room, out Vector2 center,
                    out float length, out float fittedHeight, out float angle, out bool reverse))
                {
                    effect = LayeredSlamEffect.CreatePreview(center, length, fittedHeight, angle, room, reverse);
                    if (effect != null) SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
                }
                roomOutline.positionCount = 4;
                roomOutline.SetPositions(new Vector3[] { new(room.xMin, room.yMin), new(room.xMax, room.yMin),
                    new(room.xMax, room.yMax), new(room.xMin, room.yMax) });
                warningLine.positionCount = 2;
                warningLine.SetPositions(new Vector3[] { bossPosition, end });
                boss.transform.position = bossPosition;
            }
            boss.SetActive(showBoss);
            warningLine.enabled = showWarning;
            if (effect != null)
            {
                effect.SetPreviewLayers(showBlade, showFlash, showGround, showDebris);
                effect.RenderAt(Mathf.Min(time, LayeredSlamEffect.Duration));
            }
            view.Repaint();
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (boss != null) DestroyImmediate(boss);
            if (roomOutline != null) DestroyImmediate(roomOutline.gameObject);
            if (warningLine != null) DestroyImmediate(warningLine.gameObject);
            if (lineMaterial != null) DestroyImmediate(lineMaterial);
            effect = null; boss = null; roomOutline = null; warningLine = null; lineMaterial = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
