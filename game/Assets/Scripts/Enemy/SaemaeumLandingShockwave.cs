using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 새마음금고 착지 충격파 (2026-11-20): visual only. When the vault lands, a sharp ring and a softer gold ring burst
    // out along the floor like a sonic boom and fade as they reach the landing radius. Damage stays in the boss's
    // ResolveLanding; this never carries a collider.
    public sealed class SaemaeumLandingShockwave : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Landing Shockwave";
        public const float Duration = 0.5f;
        public const float StartRadiusFraction = 0.12f;
        // The sharp front ends just past the landing radius, so it reads as the area that hurt.
        public const float FrontReach = 1.1f;
        public const float GlowReach = 0.85f;
        public const float GlowDelay = 0.05f;
        public const float FlashDuration = 0.14f;
        // Over the floor and ground shadows, under every body.
        public const int SortingOrder = GroundShadow.FloorSortingOrder + 1;
        private static readonly Color FrontColor = new(1f, 0.96f, 0.78f, 1f);
        private static readonly Color GlowColor = new(1f, 0.78f, 0.28f, 0.6f);
        private static readonly Color FlashColor = new(1f, 0.93f, 0.7f, 0.75f);
        private static Sprite frontSprite, glowSprite;

        private float radius;
        private float age;
        private SpriteRenderer front, glow, flash;
        public float Radius => radius;
        public SpriteRenderer Front => front;
        public SpriteRenderer Glow => glow;
        public SpriteRenderer Flash => flash;

        public static SaemaeumLandingShockwave Create(Vector2 groundPoint, float landingRadius, int sortingLayerId)
        {
            var root = new GameObject(ObjectName);
            root.transform.position = groundPoint;
            var wave = root.AddComponent<SaemaeumLandingShockwave>();
            wave.radius = Mathf.Max(0.1f, landingRadius);
            wave.glow = wave.Layer("Glow ring", GlowSprite, sortingLayerId, SortingOrder);
            wave.front = wave.Layer("Shock front", FrontSprite, sortingLayerId, SortingOrder + 1);
            wave.flash = wave.Layer("Impact flash", GroundShadow.SharedSprite, sortingLayerId, SortingOrder + 1);
            wave.RenderAt(0f);
            return wave;
        }

        private SpriteRenderer Layer(string name, Sprite sprite, int sortingLayerId, int order)
        {
            var child = new GameObject(name, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        public void RenderAt(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / Duration);
            // Bursts out fast, then slows as it thins away.
            float spread = 1f - Mathf.Pow(1f - t, 3f);
            SetRing(front, Mathf.Lerp(StartRadiusFraction, FrontReach, spread), FrontColor, 1f - t * t);

            float glowT = Mathf.Clamp01((elapsed - GlowDelay) / (Duration - GlowDelay));
            float glowSpread = 1f - Mathf.Pow(1f - glowT, 2.4f);
            SetRing(glow, Mathf.Lerp(StartRadiusFraction, GlowReach, glowSpread), GlowColor,
                elapsed < GlowDelay ? 0f : 1f - glowT);

            float flashT = Mathf.Clamp01(elapsed / FlashDuration);
            SetRing(flash, Mathf.Lerp(0.25f, 0.45f, flashT), FlashColor, 1f - flashT);
        }

        private void SetRing(SpriteRenderer ring, float fraction, Color color, float alpha)
        {
            float diameter = radius * 2f * fraction;
            ring.transform.localScale = new Vector3(diameter, diameter * EnemyLobProjectile.MarkAspect, 1f);
            color.a *= Mathf.Clamp01(alpha);
            ring.color = color;
            ring.enabled = color.a > 0.001f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }

        // A thin bright band for the shock front and a wide soft one for the glow behind it; unit diameter.
        public static Sprite FrontSprite => frontSprite != null ? frontSprite : frontSprite = Ring(0.9f, 0.07f, "front");
        public static Sprite GlowSprite => glowSprite != null ? glowSprite : glowSprite = Ring(0.78f, 0.2f, "glow");

        private static Sprite Ring(float center, float halfWidth, string name)
        {
            const int size = 256;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Saemaeum shockwave " + name, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                // Harder outside edge, softer trailing inside edge, like a pressure front.
                float edge = distance >= center
                    ? 1f - Mathf.Clamp01((distance - center) / (halfWidth * 0.6f))
                    : 1f - Mathf.Clamp01((center - distance) / (halfWidth * 1.4f));
                float alpha = Mathf.SmoothStep(0f, 1f, edge);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
