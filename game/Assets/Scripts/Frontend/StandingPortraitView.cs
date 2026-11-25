using TrickalFanGame.Character;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    /// <summary>
    /// Shows a character's 2D Animation standing rig in place of a still portrait Image. The rig is spawned
    /// far from the scene, a private camera frames its <see cref="StandingRigFrame"/> into a render texture,
    /// and a RawImage child of the portrait displays it. The still Image returns when the character has no rig.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StandingPortraitView : MonoBehaviour
    {
        private const float StageSpacing = 100f;
        private const int MaxTextureSize = 2048;
        private static readonly Vector3 StageOrigin = new(10000f, 10000f, 0f);
        private static int nextStageSlot;
        private static Material premultipliedMaterial;

        private Image portrait;
        private GameObject rigPrefab;
        private GameObject rigInstance;
        private Camera stageCamera;
        private RawImage display;
        private RenderTexture texture;
        private int stageSlot = -1;

        public GameObject RigInstance => rigInstance;
        public Camera StageCamera => stageCamera;
        public RawImage Display => display;

        /// <summary>
        /// Swaps the portrait to the character's standing rig, or back to the still sprite when it has none.
        /// Edit mode keeps the still sprite so scene setup and verifiers never save the runtime objects,
        /// unless <paramref name="allowInEditMode"/> is set for a throwaway preview.
        /// </summary>
        public static StandingPortraitView Apply(Image image, CharacterDefinition character, bool allowInEditMode = false)
        {
            if (image == null || (!Application.isPlaying && !allowInEditMode)) return null;
            GameObject rig = character != null ? character.StandingRig : null;
            var view = image.GetComponent<StandingPortraitView>();
            if (rig == null)
            {
                if (view != null) view.ShowStill();
                return view;
            }

            if (view == null) view = image.gameObject.AddComponent<StandingPortraitView>();
            view.ShowRig(image, rig);
            return view;
        }

        private void ShowRig(Image image, GameObject prefab)
        {
            portrait = image;
            if (rigPrefab != prefab) ReleaseRig();
            rigPrefab = prefab;
            if (rigInstance == null) BuildStage();
            portrait.enabled = false;
            display.gameObject.SetActive(true);
            SetStageActive(isActiveAndEnabled);
            Refresh();
        }

        private void ShowStill()
        {
            ReleaseRig();
            rigPrefab = null;
            if (portrait != null) portrait.enabled = portrait.sprite != null;
        }

        private void BuildStage()
        {
            if (stageSlot < 0) stageSlot = nextStageSlot++;
            Vector3 stage = StageOrigin + new Vector3(stageSlot * StageSpacing, 0f, 0f);
            rigInstance = Instantiate(rigPrefab, stage, Quaternion.identity);
            rigInstance.name = $"{rigPrefab.name} (portrait stage)";
            rigInstance.hideFlags = HideFlags.DontSave;

            var cameraObject = new GameObject("Standing portrait camera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(rigInstance.transform, false);
            stageCamera = cameraObject.AddComponent<Camera>();
            stageCamera.orthographic = true;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = Color.clear;
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 50f;
            stageCamera.depth = -100f;
            UniversalAdditionalCameraData cameraData = stageCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;

            if (display == null)
            {
                var displayObject = new GameObject("Standing Rig", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
                var rect = (RectTransform)displayObject.transform;
                rect.SetParent(portrait.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                display = displayObject.AddComponent<RawImage>();
                display.raycastTarget = false;
                display.material = PremultipliedMaterial();
            }
        }

        private void LateUpdate() => Refresh();

        private void OnRectTransformDimensionsChange()
        {
            if (rigInstance != null) Refresh();
        }

        private void OnEnable() => SetStageActive(true);

        private void OnDisable() => SetStageActive(false);

        private void OnDestroy()
        {
            ReleaseRig();
            if (display != null) DestroyObject(display.gameObject);
        }

        private void SetStageActive(bool active)
        {
            if (rigInstance != null) rigInstance.SetActive(active);
        }

        /// <summary>Matches the render texture to the portrait's pixel size and frames the rig like the still sprite.</summary>
        private void Refresh()
        {
            if (rigInstance == null || display == null || stageCamera == null) return;
            Rect rect = display.rectTransform.rect;
            if (rect.width < 1f || rect.height < 1f) return;

            Canvas canvas = display.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 16, MaxTextureSize);
            int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 16, MaxTextureSize);
            if (texture == null || texture.width != width || texture.height != height)
            {
                ReleaseTexture();
                // URP's render graph refuses camera targets without a depth buffer.
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Standing portrait", hideFlags = HideFlags.DontSave, antiAliasing = 1
                };
                texture.Create();
                stageCamera.targetTexture = texture;
                display.texture = texture;
            }

            var frameComponent = rigInstance.GetComponent<StandingRigFrame>();
            Rect frame = frameComponent != null ? frameComponent.Frame : new Rect(-2.5f, 0f, 5f, 5f);
            float aspect = rect.width / rect.height;
            // Fit the whole frame inside the portrait, as Image.preserveAspect did for the still sprite.
            stageCamera.orthographicSize = aspect >= frame.width / frame.height ? frame.height * 0.5f : frame.width / aspect * 0.5f;
            stageCamera.aspect = aspect;
            stageCamera.transform.position = rigInstance.transform.TransformPoint(frame.center) + Vector3.back * 10f;
            stageCamera.transform.rotation = rigInstance.transform.rotation;
        }

        private void ReleaseRig()
        {
            if (stageCamera != null) stageCamera.targetTexture = null;
            if (rigInstance != null) DestroyObject(rigInstance);
            rigInstance = null;
            stageCamera = null;
            ReleaseTexture();
            if (display != null)
            {
                display.texture = null;
                display.gameObject.SetActive(false);
            }
        }

        private void ReleaseTexture()
        {
            if (texture == null) return;
            texture.Release();
            DestroyObject(texture);
            texture = null;
        }

        private static Material PremultipliedMaterial()
        {
            if (premultipliedMaterial != null) return premultipliedMaterial;
            Shader shader = Resources.Load<Shader>("UiEffects/PremultipliedRawImage");
            if (shader == null || !shader.isSupported) return null;
            premultipliedMaterial = new Material(shader) { name = "Standing portrait (premultiplied)", hideFlags = HideFlags.HideAndDontSave };
            return premultipliedMaterial;
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
