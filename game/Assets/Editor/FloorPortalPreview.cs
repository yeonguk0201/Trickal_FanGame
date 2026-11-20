using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public sealed class FloorPortalPreview : EditorWindow
    {
        private PreviewRenderUtility preview;
        private SpriteRenderer sprite;
        private PortalAurora aurora;
        private bool showPortal = true;
        private bool showAurora = true;
        private bool playing = true;
        private float time;
        private float speed = 1f;
        private float zoom = 2.8f;
        private double lastTime;

        [MenuItem("Trickal Fan Game/Effects/Preview Boss Room Portal")]
        public static void Open() => GetWindow<FloorPortalPreview>("Boss room portal");

        private void OnEnable()
        {
            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Release();
        }
        private void OnPlayMode(PlayModeStateChange state) => Release();
        private void Release()
        {
            preview?.Cleanup();
            preview = null;
            sprite = null;
            aurora = null;
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing) time = (time + (float)(now - lastTime) * speed) % 12f;
            lastTime = now;
            Repaint();
        }
        private void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.transform.position = new Vector3(0, 0, -10);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.18f, 0.25f, 0.15f);
            GameObject root = new("Portal preview") { hideFlags = HideFlags.HideAndDontSave };
            root.AddComponent<BoxCollider2D>();
            sprite = root.AddComponent<SpriteRenderer>();
            root.AddComponent<FloorAdvancePortal>().Configure(null, null, 2, sprite);
            aurora = root.GetComponent<PortalAurora>();
            preview.AddSingleGO(root);
        }
        private void OnGUI()
        {
            EnsurePreview();
            playing = EditorGUILayout.Toggle("Play", playing);
            showPortal = EditorGUILayout.Toggle("Portal layer", showPortal);
            showAurora = EditorGUILayout.Toggle("Aurora layer", showAurora);
            time = EditorGUILayout.Slider("Time (12 second loop)", time, 0f, 12f);
            speed = EditorGUILayout.Slider("Speed", speed, 0.1f, 2f);
            zoom = EditorGUILayout.Slider("Camera size", zoom, 1f, 4f);
            if (GUILayout.Button("Restart")) time = 0f;
            EditorGUILayout.HelpBox("Runtime sprite and material. Outer rim stays fixed. Game background and character occlusion require Play review.", MessageType.Info);
            Rect rect = GUILayoutUtility.GetRect(100, 10000, 100, 10000);
            if (Event.current.type != EventType.Repaint || sprite == null) return;
            sprite.enabled = showPortal;
            if (aurora != null) aurora.Evaluate(time, showAurora ? 1f : 0f);
            sprite.color = Color.white;
            sprite.sharedMaterial.SetFloat("_PortalPhase", time * 2f % 4f);
            preview.camera.orthographicSize = zoom;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.Render();
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }
    }
}
