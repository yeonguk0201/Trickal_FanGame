using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: the 날먹보호막 shield as a soap bubble around the player. It pops in when the power is used,
    // ripples when a hit takes shield, blinks once only its last heart is left and pops into droplets when it is
    // used up. How much 날먹보호막 shield remains is decided in PlayerPowerEffects.
    // Deterministic: never consumes UnityEngine.Random.
    [DisallowMultipleComponent]
    public sealed class ShieldBubble : MonoBehaviour
    {
        public const string ObjectName = "VFX shield bubble";
        // Share of the quad the bubble fills; the rest is room for the pop. Keep in sync with the shader.
        public const float BubbleRadius = 0.66f;
        // Bubble radius as a share of the drawing's larger side (the Erpin frame is 1.2 units square).
        public const float RadiusShare = 0.5f;
        public const int OrderOffset = 2;
        public const float AppearSeconds = 0.25f;
        public const float RippleSeconds = 0.45f;
        public const float PopSeconds = 0.35f;
        public const float WarningFrequency = 3.5f;
        public const float WarningLow = 0.2f;

        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int RippleId = Shader.PropertyToID("_Ripple");
        private static readonly int PopId = Shader.PropertyToID("_Pop");
        private static readonly int LayersId = Shader.PropertyToID("_Layers");

        private SpriteRenderer source;
        private Mesh quad;
        private MeshRenderer bubble;
        private MaterialPropertyBlock properties;
        private Vector4 layers = Vector4.one;
        private Vector2 centre;
        private float radius;

        public SpriteRenderer Source => source;
        public MeshRenderer Bubble => bubble;
        public float Radius => radius;
        public float Opacity { get; private set; }
        public float RippleProgress { get; private set; } = -1f;
        public float PopProgress { get; private set; } = -1f;

        // Also the Editor preview and verifier entry point: nothing shows until RenderShield or RenderPop is called.
        public static ShieldBubble Create(SpriteRenderer source)
        {
            if (source == null || source.sprite == null) return null;
            Material material = CombatEffectArtwork.Material("ShieldBubble");
            if (material == null) return null;
            Transform existing = source.transform.Find(ObjectName);
            ShieldBubble view = existing != null ? existing.GetComponent<ShieldBubble>() : null;
            if (view == null)
            {
                var host = new GameObject(ObjectName);
                host.transform.SetParent(source.transform, false);
                view = host.AddComponent<ShieldBubble>();
            }
            view.source = source;
            if (view.bubble == null) view.Initialize(material);
            return view;
        }

        private void Initialize(Material material)
        {
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Shield bubble quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
            bubble = gameObject.AddComponent<MeshRenderer>();
            bubble.sharedMaterial = material;
            bubble.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bubble.receiveShadows = false;
            bubble.enabled = false;
            // Fixed from the first frame, so walking frames of different sizes never make the bubble jitter.
            Sprite sprite = source.sprite;
            float ppu = sprite.pixelsPerUnit;
            centre = (sprite.rect.size * 0.5f - sprite.pivot) / ppu;
            radius = Mathf.Max(sprite.rect.width, sprite.rect.height) / ppu * RadiusShare;
        }

        // Fades in with a small overshoot, then blinks while only the last heart is left. The blink starts bright.
        public static float OpacityAt(float shownFor, float time, bool lastHeart)
        {
            if (shownFor < 0f) return 0f;
            float fade = Mathf.SmoothStep(0f, 1f, shownFor / AppearSeconds);
            if (!lastHeart) return fade;
            float cycle = Mathf.Repeat(time * WarningFrequency, 1f);
            return fade * Mathf.Lerp(1f, WarningLow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, cycle)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, cycle))));
        }

        public static float ScaleAt(float shownFor)
        {
            float t = Mathf.Clamp01(shownFor / AppearSeconds);
            // 0.55 -> 1.06 -> 1: a quick blow-up with a soft settle.
            return t < 0.7f
                ? Mathf.Lerp(0.55f, 1.06f, 1f - Mathf.Pow(1f - t / 0.7f, 2f))
                : Mathf.Lerp(1.06f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f));
        }

        // shownFor: seconds since the power was used. rippleAge: seconds since the last hit on it (negative: none).
        public void RenderShield(float shownFor, float time, bool lastHeart, float rippleAge)
        {
            if (bubble == null || source == null) return;
            Opacity = Visible() ? OpacityAt(shownFor, time, lastHeart) : 0f;
            RippleProgress = rippleAge >= 0f && rippleAge < RippleSeconds ? rippleAge / RippleSeconds : -1f;
            PopProgress = -1f;
            Draw(time, ScaleAt(shownFor));
        }

        public void RenderPop(float popAge, float time)
        {
            if (bubble == null || source == null) return;
            bool popping = popAge >= 0f && popAge < PopSeconds;
            Opacity = popping && Visible() ? 1f : 0f;
            RippleProgress = -1f;
            PopProgress = popping ? popAge / PopSeconds : -1f;
            Draw(time, 1f);
        }

        private bool Visible() => source.enabled && !source.forceRenderingOff && source.sprite != null;

        private void Draw(float time, float scale)
        {
            bubble.enabled = Opacity > 0.001f;
            if (!bubble.enabled) return;
            // Grows with the drawing's scale; a mirrored owner never mirrors the highlights.
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float half = radius / BubbleRadius;
            transform.localPosition = centre;
            transform.localScale = new Vector3(half * Mathf.Sign(parentScale.x), half * Mathf.Sign(parentScale.y), 1f);
            transform.rotation = Quaternion.identity;
            bubble.sortingLayerID = source.sortingLayerID;
            bubble.sortingOrder = source.sortingOrder + OrderOffset;
            properties.Clear();
            properties.SetFloat(ClockId, time);
            properties.SetFloat(OpacityId, Opacity);
            properties.SetFloat(ScaleId, scale);
            properties.SetFloat(RippleId, RippleProgress);
            properties.SetFloat(PopId, PopProgress);
            properties.SetVector(LayersId, layers);
            bubble.SetPropertyBlock(properties);
        }

        public void SetPreviewLayers(bool film, bool rim, bool highlight, bool ripple)
        {
            layers = new Vector4(film ? 1 : 0, rim ? 1 : 0, highlight ? 1 : 0, ripple ? 1 : 0);
        }

        public void Hide()
        {
            if (bubble != null) bubble.enabled = false;
            Opacity = 0f;
            RippleProgress = PopProgress = -1f;
        }

        public void Stop() => CombatEffectArtwork.Release(gameObject);

        private void OnDestroy() => CombatEffectArtwork.Release(quad);
    }
}
