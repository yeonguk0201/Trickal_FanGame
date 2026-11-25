using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Character;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    /// <summary>
    /// Checks the Erpin standing rig: every part is skinned to the shared skeleton, weights are normalized,
    /// the rest pose matches the art exactly and the idle loop moves the parts within a small range.
    /// <see cref="RenderPosesBatch"/> also writes CPU-deformed pose frames for art review.
    /// </summary>
    public static class ErpinStandingRigVerification
    {
        private const float RestTolerance = 0.002f;
        private const float MaxIdleDisplacementPixels = 48f;
        private static readonly float[] SampleTimes = { 0f, 0.45f, 0.9f, 1.35f, 1.8f, 2.25f, 2.7f, 3.15f };
        private static readonly string ReviewDirectory =
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/art-review/erpin-standing"));

        [MenuItem("Trickal Fan Game/Artwork/Verify Erpin Standing Rig")]
        public static void Verify()
        {
            ErpinStandingRigSetup.LayerManifest layers = ErpinStandingRigSetup.LoadLayers();
            ErpinStandingRigSetup.RigManifest rig = ErpinStandingRigSetup.LoadRig();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErpinStandingRigSetup.PrefabPath);
            Require(prefab != null, $"Missing {ErpinStandingRigSetup.PrefabPath}. Run Setup Erpin Standing Rig.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(ErpinStandingRigSetup.PreviewScenePath) != null,
                $"Missing {ErpinStandingRigSetup.PreviewScenePath}.");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var motion = instance.GetComponent<StandingIdleMotion>();
                Require(motion != null && motion.BreathBone != null && motion.SwayCount > 0, "Idle motion is not wired to the skeleton.");
                for (int i = 0; i < motion.SwayCount; i++)
                    Require(motion.GetSway(i).bone != null && motion.GetSway(i).period > 0f, $"Sway {i} has no bone or period.");

                SpriteSkin[] skins = instance.GetComponentsInChildren<SpriteSkin>(true);
                Require(skins.Length == layers.layers.Length, $"Expected {layers.layers.Length} skinned parts, found {skins.Length}.");
                foreach (ErpinStandingRigSetup.LayerEntry layer in layers.layers)
                {
                    SpriteSkin skin = skins.FirstOrDefault(s => s.name == layer.name);
                    Require(skin != null, $"Part '{layer.name}' is missing.");
                    var renderer = skin.GetComponent<SpriteRenderer>();
                    Require(renderer.sortingOrder == layer.order, $"'{layer.name}' must draw at PSD order {layer.order}.");
                    VerifyPart(skin, ErpinStandingRigSetup.RigFor(rig, layer).bones);
                }

                float maxMove = 0f;
                var rest = skins.ToDictionary(s => s, Deform);
                foreach (float time in SampleTimes)
                {
                    motion.Apply(time);
                    foreach (SpriteSkin skin in skins)
                    {
                        Vector3[] posed = Deform(skin);
                        for (int v = 0; v < posed.Length; v++)
                            maxMove = Mathf.Max(maxMove, Vector3.Distance(posed[v], rest[skin][v]) * rig.pixelsPerUnit);
                    }
                }
                motion.ReleaseRestPose();
                Require(maxMove > 2f, $"Idle loop barely moves the parts ({maxMove:0.0}px).");
                Require(maxMove <= MaxIdleDisplacementPixels,
                    $"Idle loop moves a vertex {maxMove:0.0}px, past the {MaxIdleDisplacementPixels}px limit that keeps hidden fills covered.");

                var frame = instance.GetComponent<StandingRigFrame>();
                Vector3 canvasMin = ErpinStandingRigSetup.ToUnits(rig, 0f, layers.canvas[1]);
                Require(frame != null && Vector2.Distance(frame.Frame.min, canvasMin) < 0.001f &&
                        Vector2.Distance(frame.Frame.size, new Vector2(layers.canvas[0], layers.canvas[1]) / rig.pixelsPerUnit) < 0.001f,
                    "The portrait frame must be the PSD canvas so the rig sits where the still portrait did.");
                var unlit = AssetDatabase.LoadAssetAtPath<Material>(ErpinStandingRigSetup.UnlitMaterialPath);
                Require(skins.All(s => s.GetComponent<SpriteRenderer>().sharedMaterial == unlit),
                    "Every part must use the unlit sprite material so menu 2D lights cannot tint it.");

                var erpin = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Menu3CharacterSetup.ErpinPath);
                Require(erpin != null && erpin.StandingRig == prefab, "Erpin's character definition is not linked to the standing rig.");
                VerifyPortrait(erpin);

                Debug.Log($"ErpinStandingRigVerification passed: {skins.Length} skinned parts, rest pose exact, " +
                          $"idle peak displacement {maxMove:0.0}px, menu portrait renders the rig and falls back to the still.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static void VerifyBatch() => RunBatch(Verify);

        /// <summary>Runs Setup, Verify, then renders pose frames to output/art-review/erpin-standing.</summary>
        public static void SetupVerifyAndRenderBatch() => RunBatch(() =>
        {
            ErpinStandingRigSetup.Setup();
            Verify();
            RenderPoses();
        });

        [MenuItem("Trickal Fan Game/Artwork/Render Erpin Standing Poses")]
        public static void RenderPoses()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErpinStandingRigSetup.PrefabPath);
            Require(prefab != null, $"Missing {ErpinStandingRigSetup.PrefabPath}.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var probes = new GameObject("Erpin standing render probe");
            GameObject instance = Object.Instantiate(prefab);
            var materials = new List<Material>();
            var meshes = new List<Mesh>();
            Camera camera = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                SceneManager.MoveGameObjectToScene(probes, scene);
                instance.SetActive(false);
                var motion = instance.GetComponent<StandingIdleMotion>();
                SpriteSkin[] skins = instance.GetComponentsInChildren<SpriteSkin>(true);
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                Require(shader != null, "No sprite shader for the render probe.");

                const float cell = 5.4f;
                int columns = SampleTimes.Length / 2, rows = 2;
                for (int frame = 0; frame < SampleTimes.Length; frame++)
                {
                    motion.Apply(SampleTimes[frame]);
                    var offset = new Vector3((frame % columns - (columns - 1) * 0.5f) * cell, ((rows - 1) * 0.5f - frame / columns) * cell - 2.5f, 0f);
                    foreach (SpriteSkin skin in skins)
                    {
                        Sprite sprite = skin.GetComponent<SpriteRenderer>().sprite;
                        Vector3[] deformed = Deform(skin);
                        var mesh = new Mesh
                        {
                            vertices = deformed, uv = sprite.uv, colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), deformed.Length).ToArray(),
                            triangles = sprite.triangles.Select(i => (int)i).ToArray()
                        };
                        meshes.Add(mesh);
                        var material = new Material(shader) { mainTexture = sprite.texture };
                        materials.Add(material);
                        var part = new GameObject(skin.name);
                        part.transform.SetParent(probes.transform, false);
                        part.transform.SetPositionAndRotation(skin.transform.position + offset, skin.transform.rotation);
                        part.transform.localScale = skin.transform.lossyScale;
                        part.layer = 31;
                        part.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = part.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.sortingOrder = skin.GetComponent<SpriteRenderer>().sortingOrder;
                    }
                }
                motion.ReleaseRestPose();

                camera = new GameObject("Erpin standing render camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = cell * rows * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.3f, 0.55f, 0.4f);
                int height = 1600, width = Mathf.RoundToInt(height * columns / (float)rows);
                target = new RenderTexture(width, height, 24);
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(ReviewDirectory);
                string file = Path.Combine(ReviewDirectory, "idle-poses.png");
                File.WriteAllBytes(file, image.EncodeToPNG());
                Debug.Log($"Erpin standing idle poses rendered at {string.Join(", ", SampleTimes)} s to {file}. Edit-mode CPU deform; Play review separate.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (image != null) Object.DestroyImmediate(image);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                foreach (Material material in materials) Object.DestroyImmediate(material);
                foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// Drives <see cref="StandingPortraitView"/> on a throwaway 500x500 portrait like the select screen's:
        /// the still Image hides, the rig renders transparent around the character, and a character
        /// without a rig brings the still back and frees the stage.
        /// </summary>
        private static void VerifyPortrait(CharacterDefinition erpin)
        {
            var canvasObject = new GameObject("Standing portrait probe canvas", typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var imageObject = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image)) { hideFlags = HideFlags.DontSave };
            imageObject.transform.SetParent(canvasObject.transform, false);
            ((RectTransform)imageObject.transform).sizeDelta = new Vector2(500f, 500f);
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = erpin.Portrait;
            image.preserveAspect = true;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                StandingPortraitView view = StandingPortraitView.Apply(image, erpin, allowInEditMode: true);
                Require(view != null && view.RigInstance != null && view.StageCamera != null, "Portrait did not spawn the standing rig.");
                Require(!image.enabled && view.Display.gameObject.activeSelf && view.Display.texture is RenderTexture,
                    "Portrait must hide the still Image and show the rig's render texture.");
                Require(Vector3.Distance(view.RigInstance.transform.position, Vector3.zero) > 1000f,
                    "The portrait stage must sit far away from the menu scene.");

                view.RigInstance.GetComponent<StandingIdleMotion>().Apply(1.35f);
                var target = (RenderTexture)view.Display.texture;
                view.StageCamera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                Require(pixels.GetPixelBilinear(0.5f, 0.6f).a > 0.9f, "The rig is not drawn in the middle of the portrait.");
                Require(pixels.GetPixel(2, 2).a < 0.01f && pixels.GetPixel(target.width - 3, target.height - 3).a < 0.01f,
                    "The portrait background must stay transparent.");
                Directory.CreateDirectory(ReviewDirectory);
                File.WriteAllBytes(Path.Combine(ReviewDirectory, "menu-portrait.png"), pixels.EncodeToPNG());

                StandingPortraitView.Apply(image, null, allowInEditMode: true);
                Require(image.enabled && view.RigInstance == null && !view.Display.gameObject.activeSelf,
                    "A character without a rig must bring the still portrait back and free the stage.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                var leftover = image.GetComponent<StandingPortraitView>();
                if (leftover != null) StandingPortraitView.Apply(image, null, allowInEditMode: true);
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static void VerifyPart(SpriteSkin skin, string[] expectedBones)
        {
            Sprite sprite = skin.GetComponent<SpriteRenderer>().sprite;
            Require(sprite != null, $"'{skin.name}' has no sprite.");
            Matrix4x4[] bindPoses = sprite.GetBindPoses().ToArray();
            Transform[] bones = skin.boneTransforms;
            Require(bindPoses.Length == expectedBones.Length, $"'{skin.name}' sprite has {bindPoses.Length} bones, rig lists {expectedBones.Length}.");
            Require(bones != null && bones.Length == bindPoses.Length && bones.All(b => b != null), $"'{skin.name}' bone transforms are incomplete.");
            Require(skin.rootBone != null, $"'{skin.name}' has no root bone.");
            for (int i = 0; i < bones.Length; i++)
            {
                Require(bones[i].name == expectedBones[i], $"'{skin.name}' bone {i} is {bones[i].name}, expected {expectedBones[i]}.");
                Matrix4x4 restBone = skin.transform.localToWorldMatrix * bindPoses[i].inverse;
                Require(Vector3.Distance(restBone.GetPosition(), bones[i].position) < RestTolerance &&
                        Quaternion.Angle(restBone.rotation, bones[i].rotation) < 0.05f,
                    $"'{skin.name}' bind pose for {bones[i].name} does not match the skeleton rest pose.");
            }

            BoneWeight[] weights = sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
            Require(weights.Length == sprite.GetVertexCount() && weights.Length > 3, $"'{skin.name}' has no skin weights.");
            foreach (BoneWeight weight in weights)
            {
                float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                Require(Mathf.Abs(sum - 1f) < 0.001f, $"'{skin.name}' has a vertex whose weights sum to {sum}.");
                Require(weight.boneIndex0 < bones.Length && weight.boneIndex1 < bones.Length &&
                        weight.boneIndex2 < bones.Length && weight.boneIndex3 < bones.Length, $"'{skin.name}' weight points past its bones.");
            }
        }

        /// <summary>Linear blend skinning in the part's local space, matching what SpriteSkin draws.</summary>
        private static Vector3[] Deform(SpriteSkin skin)
        {
            Sprite sprite = skin.GetComponent<SpriteRenderer>().sprite;
            Vector3[] positions = sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
            BoneWeight[] weights = sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
            Matrix4x4[] bindPoses = sprite.GetBindPoses().ToArray();
            Transform[] bones = skin.boneTransforms;
            var skinning = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                skinning[i] = skin.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bindPoses[i];

            var result = new Vector3[positions.Length];
            for (int v = 0; v < positions.Length; v++)
            {
                BoneWeight w = weights[v];
                Vector3 p = positions[v];
                result[v] = skinning[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0 + skinning[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1 +
                            skinning[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2 + skinning[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
            }
            return result;
        }

        private static void RunBatch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
