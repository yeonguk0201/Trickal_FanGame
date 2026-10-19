using System.Reflection;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public sealed class SansamoPureMotionPreview : EditorWindow
    {
        private Scene scene;
        private SceneView view;
        private GameObject root;
        private SansamoPureMotion motion;
        private bool playing = true, walking = true, firing = true, flip;
        private float time, speed = 1f, angle;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Artwork/Preview Sansamo Pure Motion")]
        public static void Open() => GetWindow<SansamoPureMotionPreview>("Sansamo Pure");
        private void OnEnable()
        {
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            Cleanup();
        }
        private void PlayChanged(PlayModeStateChange state) => Cleanup();
        private void Create()
        {
            Cleanup();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            scene = EditorSceneManager.NewPreviewScene();
            root = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SansamoPureMotionSetup.PrefabPath));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;
            motion = root.GetComponent<SansamoPureMotion>();
            view = CreateInstance<SceneView>();
            PropertyInfo customScene = typeof(SceneView).GetProperty("customScene", BindingFlags.Instance | BindingFlags.NonPublic);
            if (customScene == null || !customScene.CanWrite) { Cleanup(); return; }
            customScene.SetValue(view, scene);
            view.titleContent = new GUIContent("Sansamo pure motion");
            view.in2DMode = true; view.Show();
            view.LookAt(Vector3.zero, Quaternion.identity, 1f, true, true);
            DrawPose();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit Mode: shared runtime walk + flower recoil. No AI, damage or scene saving. " +
                "Toggle walking/firing independently; verify the projectile connection in Play Mode.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) Create();
            playing = EditorGUILayout.Toggle("Play", playing);
            walking = EditorGUILayout.Toggle("Walk body", walking);
            firing = EditorGUILayout.Toggle("Flower recoil", firing);
            flip = EditorGUILayout.Toggle("Face right", flip);
            speed = EditorGUILayout.Slider("Playback speed", speed, 0.05f, 2f);
            angle = EditorGUILayout.Slider("Shot direction", angle, -180f, 180f);
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, 2f);
            if (GUILayout.Button("Restart")) time = 0f;
            DrawPose();
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (motion != null && playing)
            { time = Mathf.Repeat(time + (float)(now - lastTick) * speed, 2f); DrawPose(); Repaint(); }
            lastTick = now;
        }
        private void DrawPose()
        {
            if (motion == null) return;
            root.GetComponent<SpriteRenderer>().flipX = flip;
            motion.RenderPose(time / 0.7f, walking, firing ? time % 0.4f : 10f,
                new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)));
            view?.Repaint();
        }
        private void Cleanup()
        {
            motion = null;
            if (view != null) { view.Close(); view = null; }
            if (root != null) DestroyImmediate(root);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
        }
    }
}
