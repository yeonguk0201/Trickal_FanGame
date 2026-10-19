using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: a lit bomb flashes red faster and faster until it explodes. A red silhouette of the bomb
    // drawing (its own shader keeps the shading) sits one order above it; a multiply tint barely shows on dark art.
    // The blink rate rises from StartFrequency to EndFrequency over the fuse, and a steady red glow builds
    // under the blinks near the end. Timing is normalized to the fuse, so the 0.5 s exploding obstacle
    // gets the same build-up as the 0.75 s placed bomb.
    [DisallowMultipleComponent]
    // After FairyKingdomArtworkView (100) has chosen this frame's sprite.
    [DefaultExecutionOrder(110)]
    public sealed class BombFuseFlash : MonoBehaviour
    {
        public const string OverlayName = "Fuse flash";
        public const float StartFrequency = 3f;
        public const float EndFrequency = 14f;
        public const float MaxOpacity = 0.95f;

        private SpriteRenderer source;
        private SpriteRenderer overlay;
        private MaterialPropertyBlock properties;
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private float armedAt;
        private float fuse;

        public SpriteRenderer Overlay => overlay;
        public float Strength { get; private set; }

        public static BombFuseFlash Attach(SpriteRenderer source, float armedAt, float fuseDuration)
        {
            if (source == null || fuseDuration <= 0f) return null;
            Material material = CombatEffectArtwork.Material("BombFuseFlash");
            if (material == null) return null;
            BombFuseFlash flash = source.GetComponent<BombFuseFlash>();
            if (flash == null) flash = source.gameObject.AddComponent<BombFuseFlash>();
            flash.source = source;
            flash.Configure(armedAt, fuseDuration);
            if (flash.overlay == null)
            {
                var owner = new GameObject(OverlayName);
                owner.transform.SetParent(source.transform, false);
                flash.overlay = owner.AddComponent<SpriteRenderer>();
                flash.overlay.sharedMaterial = material;
            }
            flash.RenderAt(0f);
            return flash;
        }

        public void Configure(float armedTime, float fuseDuration)
        {
            armedAt = armedTime;
            fuse = Mathf.Max(0f, fuseDuration);
        }

        // Blinks completed after `elapsed` seconds: the integral of a rate that rises linearly over the fuse.
        // Starts just past a blink, so the bomb is dark for a moment after it is placed.
        public static float Phase(float elapsed, float fuseDuration)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, fuseDuration));
            return 0.6f + fuseDuration * (StartFrequency * progress +
                                          (EndFrequency - StartFrequency) * progress * progress * 0.5f);
        }

        public static float Intensity(float elapsed, float fuseDuration)
        {
            if (fuseDuration <= 0f || elapsed < 0f || elapsed >= fuseDuration) return 0f;
            float progress = elapsed / fuseDuration;
            float cycle = Phase(elapsed, fuseDuration) % 1f;
            float blink = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.55f, cycle))) *
                          Mathf.Lerp(0.8f, 1f, progress);
            float glow = 0.6f * progress * progress * progress * progress;
            return MaxOpacity * Mathf.Max(blink, glow);
        }

        public void RenderAt(float elapsed)
        {
            if (overlay == null) return;
            float intensity = source != null && source.enabled && source.sprite != null
                ? Intensity(elapsed, fuse) : 0f;
            overlay.enabled = intensity > 0.001f;
            Strength = 0f;
            if (!overlay.enabled) return;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            properties ??= new MaterialPropertyBlock();
            overlay.GetPropertyBlock(properties);
            properties.SetFloat(StrengthId, intensity);
            overlay.SetPropertyBlock(properties);
            Strength = intensity;
        }

        // The explosion replaces the bomb drawing; the flash must not paint over it.
        public void Stop()
        {
            if (overlay != null) Release(overlay.gameObject);
            overlay = null;
            Release(this);
        }

        private static void Release(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        private void LateUpdate() => RenderAt(Time.time - armedAt);
    }
}
