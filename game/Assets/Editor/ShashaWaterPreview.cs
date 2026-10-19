using System.Collections.Generic;
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
        private const string FloorPath = "Assets/Rooms/Artwork/FairyVillage/floor.png";
        private const string WallPath = "Assets/Rooms/Artwork/FairyVillage/vertical-walls.png";
        private const string ErpinResource = "Characters/Erpin_Walking";
        private const int SourceOrder = 10;
        private Scene scene;
        private SceneView view;
        private GameObject owner;
        private SpriteRenderer erpin, wallArt;
        private Sprite erpinSide, erpinUp, erpinDown;
        private readonly List<SpriteRenderer> floorTiles = new();
        private ProceduralWaterStream effect;
        private float length = 5.5f, width = 0.2f, angle, time = 0.045f, speed = 0.25f, moveAngle = 90, moveSpeed;
        private bool playing, wall = true, showBody = true, showDrops = true, showSplash = true, showMuzzle = true,
            showMist = true, showBackdrop = true;
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
            // The source sprite decides the water's sorting, exactly like the player in SampleScene.
            erpin = owner.AddComponent<SpriteRenderer>();
            erpin.sortingOrder = SourceOrder;
            foreach (Sprite sprite in Resources.LoadAll<Sprite>(ErpinResource))
            {
                if (sprite.name == "Erpin_Walk_Side_0") erpinSide = sprite;
                else if (sprite.name == "Erpin_Walk_Up_0") erpinUp = sprite;
                else if (sprite.name == "Erpin_Walk_Down_0") erpinDown = sprite;
            }
            effect = ProceduralWaterStream.Create(owner);
            if (effect == null) { Cleanup(); return; }
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            CreateBackdrop();
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

        // Room floor and wall artwork from the Fairy Village set; preview-only, never saved.
        private void CreateBackdrop()
        {
            Sprite grass = null, wallRight = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FloorPath))
                if (asset is Sprite sprite && sprite.name == "grass") grass = sprite;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(WallPath))
                if (asset is Sprite sprite && sprite.name == "wall-right") wallRight = sprite;
            if (grass != null)
            {
                Vector2 tile = grass.bounds.size;
                for (float x = -9; x < 15; x += tile.x)
                for (float y = -9; y < 9; y += tile.y)
                {
                    var renderer = new GameObject("Preview floor").AddComponent<SpriteRenderer>();
                    renderer.sprite = grass;
                    renderer.sortingOrder = -100;
                    renderer.transform.position = new Vector3(x, y);
                    SceneManager.MoveGameObjectToScene(renderer.gameObject, scene);
                    floorTiles.Add(renderer);
                }
            }
            if (wallRight != null)
            {
                wallArt = new GameObject("Preview wall").AddComponent<SpriteRenderer>();
                wallArt.sprite = wallRight;
                wallArt.sortingOrder = -50;
                SceneManager.MoveGameObjectToScene(wallArt.gameObject, scene);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode · runtime VFX in a temporary Scene. Width and length are world units. " +
                "The wall stays fixed while the source moves, like walking while firing. Final art review uses SampleScene Play.",
                MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            length = EditorGUILayout.Slider("Length", length, 0.02f, 12);
            width = EditorGUILayout.Slider("Width", width, 0.05f, 0.6f);
            angle = EditorGUILayout.Slider("Direction", angle, -180, 180);
            wall = EditorGUILayout.Toggle("Wall contact", wall);
            EditorGUILayout.LabelField("Source motion", EditorStyles.boldLabel);
            moveSpeed = EditorGUILayout.Slider("Move speed (player 5)", moveSpeed, 0, 10);
            moveAngle = EditorGUILayout.Slider("Move direction", moveAngle, -180, 180);
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
            showBody = EditorGUILayout.Toggle("Body", showBody);
            showMuzzle = EditorGUILayout.Toggle("Nozzle gush", showMuzzle);
            showDrops = EditorGUILayout.Toggle("Droplets", showDrops);
            showMist = EditorGUILayout.Toggle("Mist", showMist);
            showSplash = EditorGUILayout.Toggle("Contact foam", showSplash);
            showBackdrop = EditorGUILayout.Toggle("Room floor, wall, Erpin", showBackdrop);
            time = EditorGUILayout.Slider("Burst time (seconds)", time, 0, 0.6f);
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
            time = Mathf.Repeat(time + delta * speed, 0.6f);
            Rebuild();
            Repaint();
        }

        private Vector2 Direction => new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        private Vector2 SourceAt(float t) =>
            new Vector2(Mathf.Cos(moveAngle * Mathf.Deg2Rad), Mathf.Sin(moveAngle * Mathf.Deg2Rad)) * moveSpeed * t;

        // Mirrors PlayerWaterStream.RefreshProceduralPaths: a fixed wall plane, the path re-resolved each step.
        private void ApplyPath(float t)
        {
            Vector2 direction = Direction, from = SourceAt(t);
            float reach = wall ? Mathf.Max(0, Vector2.Dot(direction * length - from, direction)) : length;
            effect.SetPath(from, from + direction * reach, wall);
        }

        private void Rebuild()
        {
            if (effect == null) return;
            Vector2 direction = Direction;
            effect.Begin(Vector2.zero, direction * length, width, 0, Duration, wall);
            effect.ResetPreviewParticles();
            const float step = 1f / 60f;
            float simulated = 0;
            while (simulated < time)
            {
                ApplyPath(simulated);
                effect.EmitAt(simulated);
                float dt = Mathf.Min(step, time - simulated);
                effect.Droplets.Simulate(dt, false, false, false);
                effect.Mist.Simulate(dt, false, false, false);
                simulated += dt;
            }
            ApplyPath(time);
            effect.RenderAt(time);
            effect.SetPreviewLayers(showBody, showDrops, showSplash, showMuzzle, showMist);
            owner.transform.position = SourceAt(time);
            UpdateBackdrop(direction);
            view.Repaint();
        }

        private void UpdateBackdrop(Vector2 direction)
        {
            foreach (SpriteRenderer tile in floorTiles) tile.enabled = showBackdrop;
            bool vertical = Mathf.Abs(direction.y) > Mathf.Abs(direction.x);
            erpin.sprite = vertical ? (direction.y > 0 ? erpinUp : erpinDown) : erpinSide;
            erpin.flipX = !vertical && direction.x > 0;
            erpin.enabled = showBackdrop;
            if (wallArt == null) return;
            wallArt.enabled = showBackdrop && wall;
            // The art's inner face points -X; turn it to face the stream and sit just past the wall plane.
            float thickness = wallArt.sprite.bounds.size.x;
            wallArt.transform.SetPositionAndRotation(direction * (length + thickness * 0.5f),
                Quaternion.Euler(0, 0, angle));
        }

        private void Cleanup()
        {
            if (view != null) { view.Close(); view = null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            if (owner != null) DestroyImmediate(owner);
            foreach (SpriteRenderer tile in floorTiles) if (tile != null) DestroyImmediate(tile.gameObject);
            floorTiles.Clear();
            if (wallArt != null) DestroyImmediate(wallArt.gameObject);
            effect = null; owner = null; erpin = null; wallArt = null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
