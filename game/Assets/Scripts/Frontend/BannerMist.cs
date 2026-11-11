using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // First-entry banner: it shows through a haze that clears like lifting fog, and fogs over again as it leaves.
    // The banner art is blurred and washed toward mist white, the name's edges soften, and the whole banner is a
    // little larger while hazy. Materials are runtime copies only, so the scene never stores them.
    // While hazy the banner quad grows by Padding on every side (with matching UV), so the blur spreads past the
    // art's edge instead of being cut off in a straight line where the wing tips meet it.
    [DisallowMultipleComponent]
    public sealed class BannerMist : BaseMeshEffect
    {
        public const string ShaderPath = "UiEffects/BannerMist";
        public const float DefaultBlur = 0.022f;
        public const float DefaultTextSoftness = 0.75f;
        public const float DefaultHazeScale = 1.06f;
        public const float Padding = 32f;

        private static readonly int BlurId = Shader.PropertyToID("_Blur");
        private static readonly int MistId = Shader.PropertyToID("_Mist");

        [SerializeField] private Image banner;
        [SerializeField] private TMP_Text title;
        [SerializeField, Min(0f)] private float blur = DefaultBlur;
        [SerializeField, Range(0f, 1f)] private float textSoftness = DefaultTextSoftness;
        [SerializeField, Min(1f)] private float hazeScale = DefaultHazeScale;

        private Material bannerMaterial;
        private Material titleMaterial;
        private bool padded;
        private readonly List<UIVertex> vertices = new();

        public Image Banner => banner;
        public TMP_Text Title => title;

        public void Configure(Image configuredBanner, TMP_Text configuredTitle)
        {
            banner = configuredBanner;
            title = configuredTitle;
        }

        // Alpha comes in ahead of the clearing, so the banner is first seen as a soft blur.
        public static float Alpha(float reveal) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reveal / 0.6f));

        // 1 = full fog, 0 = sharp.
        public static float Haze(float reveal) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((reveal - 0.1f) / 0.9f));

        public void Apply(float reveal)
        {
            if (Application.isPlaying) Render(reveal);
        }

        // Edit-mode review captures call this directly on a scene they reopen afterwards.
        public void Render(float reveal)
        {
            float haze = Haze(reveal);
            if (EnsureMaterials())
            {
                bannerMaterial.SetFloat(BlurId, blur * haze);
                bannerMaterial.SetFloat(MistId, haze);
                titleMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, textSoftness * haze);
            }
            transform.localScale = Vector3.one * Mathf.Lerp(1f, hazeScale, haze);
            SetPadded(haze > 0.001f);
        }

        private void SetPadded(bool value)
        {
            if (padded == value) return;
            padded = value;
            if (banner != null) banner.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!padded || !IsActive() || vh.currentVertCount == 0) return;
            vertices.Clear();
            vh.GetUIVertexStream(vertices);
            Vector2 min = vertices[0].position, max = min, uvMin = vertices[0].uv0, uvMax = uvMin;
            foreach (UIVertex v in vertices)
            {
                min = Vector2.Min(min, v.position); max = Vector2.Max(max, v.position);
                uvMin = Vector2.Min(uvMin, v.uv0); uvMax = Vector2.Max(uvMax, v.uv0);
            }
            Vector2 size = max - min;
            if (size.x <= 0f || size.y <= 0f) return;
            Vector2 pad = Vector2.one * Padding;
            Vector2 uvPad = Vector2.Scale(pad, new Vector2((uvMax.x - uvMin.x) / size.x, (uvMax.y - uvMin.y) / size.y));
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex v = vertices[i];
                Vector2 t = new((v.position.x - min.x) / size.x, (v.position.y - min.y) / size.y);
                Vector2 position = min - pad + Vector2.Scale(size + 2f * pad, t);
                Vector2 uv = uvMin - uvPad + Vector2.Scale(uvMax - uvMin + 2f * uvPad, t);
                v.position = new Vector3(position.x, position.y, v.position.z);
                v.uv0 = new Vector4(uv.x, uv.y, 0f, 0f);
                vertices[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(vertices);
        }

        private bool EnsureMaterials()
        {
            if (bannerMaterial != null && titleMaterial != null) return true;
            if (banner == null || title == null) return false;
            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null) return false;
            bannerMaterial = new Material(shader) { name = "Banner mist" };
            banner.material = bannerMaterial;
            titleMaterial = title.fontMaterial; // TMP makes and owns this runtime instance.
            return true;
        }

        protected override void OnDisable()
        {
            transform.localScale = Vector3.one;
            padded = false;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (banner != null && banner.material == bannerMaterial) banner.material = null;
            if (bannerMaterial == null) return;
            if (Application.isPlaying) Destroy(bannerMaterial);
            else DestroyImmediate(bannerMaterial); // mesh effects also run in edit mode (review captures)
        }
    }
}
