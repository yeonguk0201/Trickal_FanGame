using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public sealed class CrayonRiftPreview : EditorWindow
    {
        private const string ArtworkPath = "Assets/Editor/CrayonSlashConcept-v1.png";
        private Scene scene;
        private SceneView view;
        private CrayonRiftVisual effect;
        private GameObject references;
        private float time = CrayonRiftVisual.StrikeTime+0.09f, angle = -40,
            length = CrayonRiftVisual.DefaultLength, width = CrayonRiftVisual.DefaultWidth, speed = 0.5f;
        private float warningLength = CrayonRiftVisual.DefaultWarningLengthRatio,
            warningWidth = CrayonRiftVisual.DefaultWarningWidthRatio;
        private bool playing, warning = true, body = true, light = true, flash = true, fragments = true,
            showReferences = true;
        private double lastTick;

        [MenuItem("Trickal Fan Game/Effects/Preview Crayon Hero Rift")]
        public static void Open() => GetWindow<CrayonRiftPreview>("Crayon Hero Rift");

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            lastTick = EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            Cleanup();
        }
        private void PlayChanged(PlayModeStateChange state) { playing = false; Cleanup(); }

        private void CreatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Cleanup();
            scene = EditorSceneManager.NewPreviewScene();
            effect = CrayonRiftVisual.Create(LoadArtwork());
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            references = CreateReferences(scene);
            view = CreateInstance<SceneView>();
            var property = typeof(SceneView).GetProperty("customScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
            {
                Cleanup();
                Debug.LogError("This Unity Editor does not support an isolated rift Scene preview.");
                return;
            }
            property.SetValue(view, scene);
            view.titleContent = new GUIContent("Crayon sword cut · visual prototype v3");
            view.in2DMode = true;
            view.Show();
            view.LookAt(Vector3.zero, Quaternion.identity, 4.5f, true, true);
            RefreshFrame();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Visual prototype · no gameplay connection. Temporary isolated Scene; no damage or asset saves. " +
                "Timeline: 0.35s warning + 0.17s travel + 0.10s arrival delay, then sword cut. " +
                "The warning stays fixed. Boss/player artwork is a scale reference, not a simulated dash.", MessageType.Info);
            if (GUILayout.Button("Open / reset Scene preview")) CreatePreview();
            EditorGUI.BeginChangeCheck();
            length = EditorGUILayout.Slider("Attack artwork length",length,1,8);
            width = EditorGUILayout.Slider("Attack artwork width envelope",width,0.3f,5);
            warningLength = EditorGUILayout.Slider("Warning length / artwork",warningLength,0.1f,0.9f);
            warningWidth = EditorGUILayout.Slider("Warning width / artwork envelope",warningWidth,0.05f,0.6f);
            angle = EditorGUILayout.Slider("Angle",angle,-180,180);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Upper left → lower right")) { angle=-40; GUI.changed=true; }
                if (GUILayout.Button("Upper right → lower left")) { angle=-140; GUI.changed=true; }
            }
            warning = EditorGUILayout.Toggle("Red warning",warning);
            body = EditorGUILayout.Toggle("Thin black incision",body);
            light = EditorGUILayout.Toggle("Concept silver blade artwork",light);
            flash = EditorGUILayout.Toggle("Needle slash / cross flash",flash);
            fragments = EditorGUILayout.Toggle("Straight silver splinters",fragments);
            showReferences = EditorGUILayout.Toggle("Game floor / boss / player",showReferences);
            time = EditorGUILayout.Slider("Time (seconds)",time,0,CrayonRiftVisual.Duration+0.35f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Warning")) { time=0.2f; playing=false; GUI.changed=true; }
                if (GUILayout.Button("Cut")) { time=CrayonRiftVisual.StrikeTime+0.025f; playing=false; GUI.changed=true; }
                if (GUILayout.Button("Peak")) { time=CrayonRiftVisual.StrikeTime+0.06f; playing=false; GUI.changed=true; }
                if (GUILayout.Button("Fade")) { time=CrayonRiftVisual.StrikeTime+0.32f; playing=false; GUI.changed=true; }
            }
            if (EditorGUI.EndChangeCheck()) RefreshFrame();
            speed = EditorGUILayout.Slider("Playback speed",speed,0.05f,1);
            playing = EditorGUILayout.Toggle("Loop playback",playing);
            if (GUILayout.Button("Restart")) { time=0; RefreshFrame(); }
        }

        private void Tick()
        {
            double now=EditorApplication.timeSinceStartup;
            float delta=Mathf.Min(0.05f,(float)(now-lastTick)); lastTick=now;
            if (effect != null && view == null) { playing=false; Cleanup(); return; }
            if (!playing || effect == null) return;
            time=Mathf.Repeat(time+delta*speed,CrayonRiftVisual.Duration+0.35f);
            RefreshFrame(); Repaint();
        }
        private void RefreshFrame()
        {
            if (effect == null) return;
            effect.Configure(Vector2.zero,angle,length,width,warningLength,warningWidth);
            effect.RenderAt(time,warning,body,light,flash,fragments);
            references.SetActive(showReferences);
            if (view != null) view.Repaint();
        }
        private void Cleanup()
        {
            if (view != null) { view.Close(); view=null; }
            if (effect != null) DestroyImmediate(effect.gameObject);
            effect=null;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            references=null; scene=default;
        }

        // Copy renderers only. Never instantiate boss/player gameplay components or change original assets.
        private static GameObject CreateReferences(Scene target)
        {
            var root=new GameObject("Artwork references only");
            SceneManager.MoveGameObjectToScene(root,target);
            var lamp=new GameObject("Reference global light"); lamp.transform.SetParent(root.transform,false);
            var lighting=lamp.AddComponent<Light2D>(); lighting.lightType=Light2D.LightType.Global; lighting.intensity=1;
            GameObject floor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rooms/Prefabs/room-basic-tree-grove.prefab");
            if (floor != null)
                foreach (SpriteRenderer original in floor.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!original.enabled || original.sortingOrder != -100 || original.sprite == null) continue;
                    var copy=new GameObject("Floor reference"); copy.transform.SetParent(root.transform,false);
                    copy.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);
                    copy.transform.localScale=original.transform.lossyScale;
                    var sprite=copy.AddComponent<SpriteRenderer>(); sprite.sprite=original.sprite;
                    sprite.sharedMaterial=original.sharedMaterial; sprite.color=original.color;
                    sprite.drawMode=original.drawMode; sprite.size=original.size;
                    sprite.sortingOrder=-100;
                }
            AddArtwork(root,"Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Dash_2.png",new Vector2(-2.2f,0),2.0f);
            AddArtwork(root,"Assets/Resources/Characters/Erpin_Walking.png",new Vector2(1.8f,-0.65f),0.95f);
            return root;
        }
        private static void AddArtwork(GameObject parent,string path,Vector2 position,float height)
        {
            Sprite sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) return;
            var copy=new GameObject(Path.GetFileNameWithoutExtension(path));
            copy.transform.SetParent(parent.transform,false); copy.transform.localPosition=position;
            copy.transform.localScale=Vector3.one*(height/Mathf.Max(0.01f,sprite.bounds.size.y));
            var renderer=copy.AddComponent<SpriteRenderer>(); renderer.sprite=sprite; renderer.sortingOrder=10;
        }

        private static Texture2D LoadArtwork()
        {
            var artwork=AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPath);
            if (artwork==null) throw new InvalidOperationException("Missing reviewed sword-cut concept artwork.");
            return artwork;
        }

        // Captures the same shader and explicit clock as the interactive Scene preview.
        public static void VerifyAndExportBatch()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../output/crayon-slash-v3"));
            string[] args=Environment.GetCommandLineArgs();
            int outputArgument=Array.IndexOf(args,"-riftOutput");
            if (outputArgument>=0 && outputArgument+1<args.Length) folder=Path.GetFullPath(args[outputArgument+1]);
            Directory.CreateDirectory(folder);
            Scene target=EditorSceneManager.NewPreviewScene();
            RenderTexture texture=null;
            RenderTexture previous=RenderTexture.active;
            Texture2D image=null;
            CrayonRiftVisual visual=null;
            Camera camera=null;
            try
            {
                visual=CrayonRiftVisual.Create(LoadArtwork());
                SceneManager.MoveGameObjectToScene(visual.gameObject,target);
                Shader shader=Resources.Load<Shader>("CombatEffects/CrayonRift");
                if (UnityEditor.ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Rift shader compilation failed.");
                CreateReferences(target);
                var cameraObject=new GameObject("Rift capture camera");
                SceneManager.MoveGameObjectToScene(cameraObject,target);
                camera=cameraObject.AddComponent<Camera>(); camera.scene=target;
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic=true; camera.orthographicSize=3.2f;
                camera.transform.position=new Vector3(0,0,-10);
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.13f,0.16f,0.18f);
                texture=new RenderTexture(960,640,24,RenderTextureFormat.ARGB32);
                image=new Texture2D(960,640,TextureFormat.RGBA32,false);
                camera.targetTexture=texture;
                for (int i=0;i<85;i++)
                {
                    visual.Configure(Vector2.zero,-40,CrayonRiftVisual.DefaultLength,CrayonRiftVisual.DefaultWidth);
                    visual.RenderAt(i/50f);
                    camera.Render(); RenderTexture.active=texture;
                    image.ReadPixels(new Rect(0,0,960,640),0,0); image.Apply();
                    File.WriteAllBytes(Path.Combine(folder,$"frame-{i:000}.png"),image.EncodeToPNG());
                }
                if (UnityEditor.ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Sword-cut shader rendering failed.");
                visual.RenderAt(CrayonRiftVisual.StrikeTime+0.06f,false,false,true,false,false);
                camera.Render(); RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,960,640),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder,"silver-artwork-only.png"),image.EncodeToPNG());
                // Both diagonals and explicit off-state checks protect against leftover layers on rewind.
                foreach (float direction in new[] {-48f,-132f,90f,0f})
                {
                    visual.Configure(Vector2.zero,direction,CrayonRiftVisual.DefaultLength,CrayonRiftVisual.DefaultWidth);
                    var warningTransform=visual.transform.GetChild(0);
                    if (warningTransform.localScale.x>=1 || warningTransform.localScale.y>=1)
                        throw new InvalidOperationException("Warning must be smaller than the attack artwork.");
                    foreach (float at in new[] {0.2f,CrayonRiftVisual.StrikeTime+0.1f,CrayonRiftVisual.Duration,0.2f})
                    {
                        visual.RenderAt(at);
                        var layers=visual.GetComponentsInChildren<MeshRenderer>();
                        if (layers[0].enabled != (at<CrayonRiftVisual.StrikeTime) ||
                            layers.Skip(1).Any(layer => layer.enabled !=
                                (at>=CrayonRiftVisual.StrikeTime && at<CrayonRiftVisual.Duration)))
                            throw new InvalidOperationException("Rift layer timeline/rewind failed.");
                    }
                    visual.RenderAt(CrayonRiftVisual.StrikeTime+0.1f,false,true,false,false,false);
                    if (visual.GetComponentsInChildren<MeshRenderer>().Skip(2).Any(layer => layer.enabled))
                        throw new InvalidOperationException("Rift light layers did not switch off independently.");
                }
                Debug.Log("Crayon sword cut v3 verified and exported: 85 Unity-rendered frames at 50fps; smaller warning, " +
                    "layer toggles, timeline and rewind passed; no gameplay assets saved.");
            }
            finally
            {
                RenderTexture.active=previous;
                if (camera != null) camera.targetTexture=null;
                if (visual != null) DestroyImmediate(visual.gameObject);
                if (image != null) DestroyImmediate(image);
                if (texture != null) { texture.Release(); DestroyImmediate(texture); }
                EditorSceneManager.ClosePreviewScene(target);
            }
        }
    }
}
