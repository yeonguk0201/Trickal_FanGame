using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: 레비의 단도 cancelling a lethal hit (PlayerDeathWard). A short burst marks the moment (a gold-white
    // flash, a gold ring behind the player and eight sparkles flying out), then a gold outline with a light band
    // sweeping up the body lasts the whole invulnerability and blinks fast in its last second. Over the head the
    // dagger's own artwork floats on a flame-colored glow (white center, yellow, orange edge): it fades in over 0.5 s and fades out over 0.5 s after the
    // invulnerability ends, about half the player drawing's width, while the glow behind it pulses out and back. The post-hit blink of
    // PlayerHitPassThrough may run under it; the outline does not follow that blink. Invulnerability, its length
    // and the enemy pass-through stay in PlayerDeathWard and DamageInvulnerability.
    // Deterministic: never consumes UnityEngine.Random.
    [DisallowMultipleComponent]
    // After PlayerWalkAnimator and the hit blink have chosen this frame's sprite.
    [DefaultExecutionOrder(110)]
    public sealed class DeathWardGlow : MonoBehaviour
    {
        public const string OutlineName = "VFX dagger ward outline";
        public const float FadeIn = 0.12f;
        public const float WarningTime = 1f;
        public const float WarningFrequency = 6f;
        public const float WarningLow = 0.2f;
        public const float MaxOpacity = 0.95f;
        public const float ShinePeriod = 1.1f;
        // Outline width in the drawing's local units (the Erpin frame is 1.2 units square).
        public const float OutlineWidth = 0.03f;

        public const float BurstDuration = 0.42f;
        public const float FlashEnd = 0.1f;
        public const float RingEnd = 0.38f;
        public const int SparkleCount = 8;
        // Burst reach as a share of the drawing's larger side.
        public const float ReachShare = 0.6f;

        public const string IconArtwork = "UserArtwork/artifact-levi-dagger";
        public const string IconName = "VFX dagger ward icon";
        public const float IconFadeIn = 0.5f;
        public const float IconFadeOut = 0.5f;
        // The dagger's drawn width as a share of the player drawing's width.
        public const float IconWidthShare = 0.42f;
        // The drawn dagger covers this share of its square artwork (measured 1023 x 1174 of 1254 px).
        public const float IconArtWidthShare = 0.82f;
        public const float IconArtHeightShare = 0.94f;
        // Gap over the head as a share of the player drawing's width.
        public const float IconGapShare = 0.05f;
        public const float IconBob = 0.02f;
        public const float IconBobPeriod = 1.6f;
        // Glow radius as a share of the dagger's drawn width: wide enough to show around the dagger.
        public const float IconGlowShare = 1.9f;
        // The glow swells and settles once per period (a slow hum), between these sizes and opacities.
        public const float IconPulsePeriod = 1.3f;
        public const float IconPulseMinScale = 0.8f;
        public const float IconPulseMaxScale = 1.15f;
        public const float IconPulseMinOpacity = 0.35f;
        public const float IconPulseMaxOpacity = 0.95f;

        private SpriteRenderer source;
        private Health health;
        private Mesh outlineMesh, quad;
        private MeshRenderer outline, flash, ring;
        private readonly MeshRenderer[] sparkles = new MeshRenderer[SparkleCount];
        private readonly Vector3[] outlineVertices = new Vector3[4];
        private readonly Vector2[] outlineUvs = new Vector2[4];
        private Transform burst, iconRoot;
        private SpriteRenderer icon;
        private MeshRenderer iconGlow;
        private Vector2 iconAnchor;
        private MaterialPropertyBlock properties;
        private float startedAt;
        private float duration;
        private float reach = 0.7f;
        private bool runtime;

        public SpriteRenderer Source => source;
        public MeshRenderer Outline => outline;
        public MeshRenderer Flash => flash;
        public MeshRenderer Ring => ring;
        public MeshRenderer[] Sparkles => sparkles;
        public float Duration => duration;
        public float Reach => reach;
        public float OutlineOpacity { get; private set; }
        public float RingOuterRadius { get; private set; }
        public float FarthestSparkle { get; private set; }
        public SpriteRenderer Icon => icon;
        public MeshRenderer IconGlow => iconGlow;
        public float IconOpacity { get; private set; }
        // The dagger's drawn width in the player's local units; 0 until measured from the first visible frame.
        public float IconWidth { get; private set; }
        public float Lifetime => Mathf.Max(duration + IconFadeOut, BurstDuration);

        public static DeathWardGlow Play(SpriteRenderer source, float startTime, float invulnerabilityDuration)
        {
            if (!Application.isPlaying) return null;
            DeathWardGlow glow = Create(source, invulnerabilityDuration);
            if (glow == null) return null;
            glow.runtime = true;
            glow.startedAt = startTime;
            glow.RenderAt(0f);
            return glow;
        }

        // Also the Editor preview and verifier entry point: nothing moves until RenderAt is called.
        public static DeathWardGlow Create(SpriteRenderer source, float invulnerabilityDuration)
        {
            if (source == null || invulnerabilityDuration <= 0f) return null;
            Material material = CombatEffectArtwork.Material("DeathWard");
            if (material == null) return null;
            DeathWardGlow glow = source.GetComponent<DeathWardGlow>();
            if (glow == null) glow = source.gameObject.AddComponent<DeathWardGlow>();
            glow.source = source;
            glow.health = source.GetComponentInParent<Health>();
            glow.duration = invulnerabilityDuration;
            if (glow.outline == null) glow.Initialize(material);
            return glow;
        }

        private void Initialize(Material material)
        {
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Death ward quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            outlineMesh = new Mesh { name = "Death ward outline" };
            outlineMesh.MarkDynamic();
            outlineMesh.vertices = outlineVertices;
            outlineMesh.uv = outlineUvs;
            outlineMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            outline = CreateRenderer(OutlineName, transform, outlineMesh, material);
            burst = new GameObject("VFX dagger ward burst").transform;
            burst.SetParent(transform, false);
            ring = CreateRenderer("Ring", burst, quad, material);
            flash = CreateRenderer("Flash", burst, quad, material);
            for (int i = 0; i < SparkleCount; i++)
                sparkles[i] = CreateRenderer("Sparkle " + i, burst, quad, material);
            Sprite dagger = Resources.Load<Sprite>(IconArtwork);
            if (dagger == null) return;
            iconRoot = new GameObject(IconName).transform;
            iconRoot.SetParent(transform, false);
            iconGlow = CreateRenderer("Glow", iconRoot, quad, material);
            var iconObject = new GameObject("Dagger");
            iconObject.transform.SetParent(iconRoot, false);
            icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = dagger;
            icon.enabled = false;
        }

        private static MeshRenderer CreateRenderer(string name, Transform parent, Mesh mesh, Material material)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        // The outline over the whole invulnerability: fades in, pulses gently, then blinks fast in the last second.
        public static float OutlineOpacityAt(float elapsed, float invulnerabilityDuration)
        {
            if (elapsed < 0f || elapsed >= invulnerabilityDuration) return 0f;
            float fade = Mathf.SmoothStep(0f, 1f, elapsed / FadeIn);
            float remaining = invulnerabilityDuration - elapsed;
            float level;
            if (remaining < WarningTime)
            {
                // Starts bright and ends dark, so the end of the invulnerability is the end of a blink.
                float cycle = Mathf.Repeat(remaining * WarningFrequency, 1f);
                level = Mathf.Lerp(WarningLow, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.6f, cycle)));
            }
            else
            {
                level = 0.85f + 0.15f * Mathf.Cos(elapsed * Mathf.PI * 2f * 1.2f);
            }
            return MaxOpacity * fade * level;
        }

        // The dagger over the head: fades in at the start, stays, and fades out after the invulnerability ends.
        public static float IconOpacityAt(float elapsed, float invulnerabilityDuration)
        {
            if (elapsed < 0f || elapsed >= invulnerabilityDuration + IconFadeOut) return 0f;
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed, invulnerabilityDuration) / IconFadeIn);
            float fadeOut = elapsed <= invulnerabilityDuration ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - invulnerabilityDuration) / IconFadeOut);
            return fadeIn * fadeOut;
        }

        public static float ShineAt(float elapsed) => Mathf.Lerp(-0.3f, 1.5f, Mathf.Repeat(elapsed / ShinePeriod, 1f));

        public void RenderAt(float elapsed)
        {
            if (outline == null || source == null) return;
            bool visible = source.enabled && !source.forceRenderingOff && source.sprite != null;
            RenderOutline(elapsed, visible);
            RenderBurst(elapsed, visible);
            RenderIcon(elapsed, visible);
        }

        private void RenderIcon(float elapsed, bool visible)
        {
            if (icon == null) return;
            IconOpacity = visible ? IconOpacityAt(elapsed, duration) : 0f;
            icon.enabled = iconGlow.enabled = IconOpacity > 0.001f;
            if (!icon.enabled) return;
            if (IconWidth <= 0f)
            {
                // Measured once, so the dagger does not jitter with the walking frames' slightly different bounds.
                Bounds drawing = source.sprite.bounds;
                IconWidth = drawing.size.x * IconWidthShare;
                float artHeight = IconWidth / IconArtWidthShare * IconArtHeightShare;
                iconAnchor = new Vector2(drawing.center.x,
                    drawing.max.y + drawing.size.x * IconGapShare + artHeight * 0.5f);
            }
            float flip = source.flipX ? -1f : 1f;
            float bob = IconBob * Mathf.Sin(elapsed * Mathf.PI * 2f / IconBobPeriod);
            iconRoot.localPosition = new Vector3(iconAnchor.x * flip, iconAnchor.y + bob, 0f);
            float artSide = IconWidth / IconArtWidthShare;
            Vector2 spriteSize = icon.sprite.bounds.size;
            icon.transform.localScale = new Vector3(artSide / spriteSize.x, artSide / spriteSize.y, 1f);
            icon.sortingLayerID = source.sortingLayerID;
            icon.sortingOrder = source.sortingOrder + 5;
            icon.color = new Color(1f, 1f, 1f, IconOpacity);
            float pulse = IconPulseAt(elapsed);
            float glowRadius = IconWidth * IconGlowShare * Mathf.Lerp(IconPulseMinScale, IconPulseMaxScale, pulse);
            iconGlow.transform.localScale = new Vector3(glowRadius, glowRadius, 1f);
            SetLayer(iconGlow, source.sortingLayerID, source.sortingOrder + 4, 4f,
                IconOpacity * Mathf.Lerp(IconPulseMinOpacity, IconPulseMaxOpacity, pulse));
        }

        // 0 to 1: a quick swell and a slower settle each period, so the glow hums rather than blinks.
        public static float IconPulseAt(float elapsed)
        {
            float phase = Mathf.Repeat(elapsed / IconPulsePeriod, 1f);
            return phase < 0.4f ? Mathf.SmoothStep(0f, 1f, phase / 0.4f)
                : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.4f) / 0.6f);
        }

        private void RenderOutline(float elapsed, bool visible)
        {
            OutlineOpacity = visible ? OutlineOpacityAt(elapsed, duration) : 0f;
            outline.enabled = OutlineOpacity > 0.001f;
            if (!outline.enabled) return;
            Sprite sprite = source.sprite;
            Texture texture = sprite.texture;
            Rect rect = sprite.rect;
            float ppu = sprite.pixelsPerUnit;
            Vector2 min = -sprite.pivot / ppu;
            Vector2 size = rect.size / ppu;
            float pad = OutlineWidth * 1.5f;
            float flip = source.flipX ? -1f : 1f;
            float flipY = source.flipY ? -1f : 1f;
            Vector2 uvMin = new(rect.xMin / texture.width, rect.yMin / texture.height);
            Vector2 uvSize = new(rect.width / texture.width, rect.height / texture.height);
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = new(i % 2 == 0 ? min.x - pad : min.x + size.x + pad,
                    i < 2 ? min.y - pad : min.y + size.y + pad);
                outlineVertices[i] = new Vector3(corner.x * flip, corner.y * flipY, 0f);
                outlineUvs[i] = uvMin + Vector2.Scale((corner - min) / size, uvSize);
            }
            outlineMesh.vertices = outlineVertices;
            outlineMesh.uv = outlineUvs;
            outlineMesh.RecalculateBounds();
            outline.sortingLayerID = source.sortingLayerID;
            outline.sortingOrder = source.sortingOrder + 1;
            properties.Clear();
            properties.SetTexture("_MainTex", texture);
            properties.SetFloat("_Mode", 0f);
            properties.SetFloat("_Opacity", OutlineOpacity);
            properties.SetVector("_UvRect", new Vector4(uvMin.x, uvMin.y, uvMin.x + uvSize.x, uvMin.y + uvSize.y));
            properties.SetVector("_OutlineUv", new Vector4(OutlineWidth / size.x * uvSize.x,
                OutlineWidth / size.y * uvSize.y, 0f, 0f));
            properties.SetFloat("_Shine", ShineAt(elapsed));
            outline.SetPropertyBlock(properties);
        }

        private void RenderBurst(float elapsed, bool visible)
        {
            bool active = visible && elapsed >= 0f && elapsed < BurstDuration;
            RingOuterRadius = 0f;
            FarthestSparkle = 0f;
            if (!active)
            {
                flash.enabled = ring.enabled = false;
                foreach (MeshRenderer sparkle in sparkles) sparkle.enabled = false;
                return;
            }
            Sprite sprite = source.sprite;
            float ppu = sprite.pixelsPerUnit;
            Vector2 size = sprite.rect.size / ppu;
            burst.localPosition = (sprite.rect.size * 0.5f - sprite.pivot) / ppu;
            reach = Mathf.Max(size.x, size.y) * ReachShare;
            int layer = source.sortingLayerID, order = source.sortingOrder;

            // The ring stays behind the player so the drawing reads through it; flash and sparkles go over.
            ring.enabled = elapsed < RingEnd;
            if (ring.enabled)
            {
                float progress = elapsed / RingEnd;
                float outer = Mathf.Lerp(0.25f, 1f, EaseOut(progress));
                float thickness = Mathf.Lerp(0.12f, 0.03f, progress);
                RingOuterRadius = outer * reach;
                ring.transform.localScale = new Vector3(reach, reach, 1f);
                SetLayer(ring, layer, order - 1, 2f,
                    0.9f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, progress))),
                    outer - thickness, thickness);
            }

            flash.enabled = elapsed < FlashEnd;
            if (flash.enabled)
            {
                float flashSize = reach * 0.7f * (0.6f + 0.4f * EaseOut(elapsed / 0.04f));
                flash.transform.localScale = new Vector3(flashSize, flashSize, 1f);
                SetLayer(flash, layer, order + 2, 1f,
                    0.95f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, FlashEnd, elapsed))));
            }

            float sparkleProgress = elapsed / BurstDuration;
            for (int i = 0; i < SparkleCount; i++)
            {
                MeshRenderer sparkle = sparkles[i];
                // Even angles with every other sparkle a little shorter and later, so the burst is not a clean ring.
                float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                float share = i % 2 == 0 ? 1f : 0.72f;
                float progress = Mathf.Clamp01((sparkleProgress - (i % 2 == 0 ? 0f : 0.08f)) / 0.92f);
                sparkle.enabled = progress > 0f && progress < 1f;
                if (!sparkle.enabled) continue;
                float distance = reach * share * Mathf.Lerp(0.2f, 1f, EaseOut(progress));
                FarthestSparkle = Mathf.Max(FarthestSparkle, distance);
                sparkle.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, progress * 90f);
                float sparkleSize = reach * 0.16f * share * Mathf.Pow(1f - progress, 0.7f);
                sparkle.transform.localScale = new Vector3(sparkleSize, sparkleSize, 1f);
                SetLayer(sparkle, layer, order + 3, 3f,
                    1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, progress)));
            }
        }

        private void SetLayer(MeshRenderer renderer, int layer, int order, float mode, float opacity,
            float ringRadius = 0.5f, float thickness = 0.05f)
        {
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            properties.Clear();
            properties.SetFloat("_Mode", mode);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_Ring", ringRadius);
            properties.SetFloat("_Thickness", thickness);
            renderer.SetPropertyBlock(properties);
        }

        public void SetPreviewLayers(bool showOutline, bool showFlash, bool showRing, bool showSparkles,
            bool showIcon = true)
        {
            if (outline == null) return;
            if (icon != null)
            {
                icon.enabled &= showIcon;
                iconGlow.enabled &= showIcon;
            }
            outline.enabled &= showOutline;
            flash.enabled &= showFlash;
            ring.enabled &= showRing;
            foreach (MeshRenderer sparkle in sparkles) sparkle.enabled &= showSparkles;
        }

        public void Stop()
        {
            if (outline != null) CombatEffectArtwork.Release(outline.gameObject);
            if (burst != null) CombatEffectArtwork.Release(burst.gameObject);
            if (iconRoot != null) CombatEffectArtwork.Release(iconRoot.gameObject);
            outline = flash = ring = iconGlow = null;
            icon = null;
            burst = iconRoot = null;
            CombatEffectArtwork.Release(this);
        }

        private void LateUpdate()
        {
            if (!runtime) return;
            float elapsed = Time.time - startedAt;
            if ((health != null && health.IsDead) || elapsed >= Lifetime)
            {
                Stop();
                return;
            }
            RenderAt(elapsed);
        }

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(quad);
            CombatEffectArtwork.Release(outlineMesh);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
    }
}
