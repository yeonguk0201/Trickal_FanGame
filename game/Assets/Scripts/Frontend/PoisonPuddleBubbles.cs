using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Effect-0 (docs/28): visual only. A few small purple bubbles that well up inside a poison puddle (the 독 장판 trap
    // and the 죽으면 장판 champion's puddle), rise a little, then pop. Kept faint and sparse so it never competes
    // with bodies or shots. Each slot waits a moment, grows at a random point of the floor ellipse, drifts up and pops.
    // Never consumes UnityEngine.Random. Added only during Play, so Scenes and Prefabs are untouched.
    public sealed class PoisonPuddleBubbles : MonoBehaviour
    {
        public const string ObjectName = "Poison Bubbles";
        public static readonly Color BubbleColor = new(0.7f, 0.42f, 0.9f, 0.85f);
        public const float MinSize = 0.08f;
        public const float MaxSize = 0.15f;
        public const float MinRise = 0.2f;
        public const float MaxRise = 0.34f;
        public const float MinLife = 0.9f;
        public const float MaxLife = 1.3f;
        public const float MinWait = 0.2f;
        public const float MaxWait = 1.1f;
        public const float PopDuration = 0.12f;
        // Bubbles start this far inside the floor ellipse so they never sit on the drawn rim.
        public const float InnerFraction = 0.72f;
        private const int TextureSize = 32;

        private static Sprite sharedSprite;
        private static int nextSeed = 1;

        private SpriteRenderer[] bubbles;
        private Vector2[] origins;
        private float[] sizes, rises, lives, ages, waits;
        private System.Random random;
        private Vector2 halfExtents = new(0.4f, 0.3f);
        private float alpha = 1f;

        public int Count => bubbles != null ? bubbles.Length : 0;

        // halfExtents: the floor ellipse of the puddle around owner. sortingSource: the puddle's renderer, drawn just under.
        public static PoisonPuddleBubbles Attach(Transform owner, Vector2 halfExtents, int count, Renderer sortingSource)
        {
            if (!Application.isPlaying || owner == null) return null;
            Transform existing = owner.Find(ObjectName);
            if (existing != null && existing.TryGetComponent(out PoisonPuddleBubbles found))
            {
                found.SetArea(halfExtents);
                return found;
            }

            GameObject host = new(ObjectName);
            host.transform.SetParent(owner, false);
            PoisonPuddleBubbles effect = host.AddComponent<PoisonPuddleBubbles>();
            effect.Initialize(Mathf.Max(1, count), halfExtents, sortingSource);
            return effect;
        }

        public void SetArea(Vector2 configuredHalfExtents) =>
            halfExtents = new Vector2(Mathf.Max(0.05f, configuredHalfExtents.x), Mathf.Max(0.05f, configuredHalfExtents.y));

        // Follows the puddle's own fade.
        public void SetAlpha(float value) => alpha = Mathf.Clamp01(value);

        private void Initialize(int count, Vector2 area, Renderer sorting)
        {
            SetArea(area);
            random = new System.Random(nextSeed++ * 7919);
            int layer = sorting != null ? sorting.sortingLayerID : 0;
            int order = (sorting != null ? sorting.sortingOrder : 0) + 1;
            bubbles = new SpriteRenderer[count];
            origins = new Vector2[count];
            sizes = new float[count];
            rises = new float[count];
            lives = new float[count];
            ages = new float[count];
            waits = new float[count];
            for (int i = 0; i < count; i++)
            {
                GameObject bubble = new("Bubble");
                bubble.transform.SetParent(transform, false);
                SpriteRenderer renderer = bubble.AddComponent<SpriteRenderer>();
                renderer.sprite = SharedSprite();
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order;
                renderer.enabled = false;
                bubbles[i] = renderer;
                // Staggered first appearance so they never rise together.
                waits[i] = Range(0f, MaxWait);
                ages[i] = -1f;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || bubbles == null) return;
            float delta = Time.deltaTime;
            for (int i = 0; i < bubbles.Length; i++) Step(i, delta);
        }

        private void Step(int i, float delta)
        {
            SpriteRenderer bubble = bubbles[i];
            if (ages[i] < 0f)
            {
                waits[i] -= delta;
                if (waits[i] > 0f) return;
                Respawn(i);
            }

            ages[i] += delta;
            float age = ages[i], life = lives[i];
            if (age >= life + PopDuration)
            {
                bubble.enabled = false;
                ages[i] = -1f;
                waits[i] = Range(MinWait, MaxWait);
                return;
            }

            float scale, opacity;
            float t = Mathf.Clamp01(age / life);
            if (age < life)
            {
                // Swells quickly from nothing, then keeps a slow growth while it rises.
                scale = sizes[i] * Mathf.Lerp(0f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.3f)) * Mathf.Lerp(0.9f, 1.05f, t);
                opacity = 1f;
            }
            else
            {
                // Pop: a quick widen and fade.
                float pop = (age - life) / PopDuration;
                scale = sizes[i] * Mathf.Lerp(1.05f, 1.45f, pop);
                opacity = 1f - pop;
            }

            float rise = rises[i] * (1f - (1f - t) * (1f - t));
            bubble.transform.localPosition = new Vector3(origins[i].x, origins[i].y + rise, 0f);
            bubble.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = BubbleColor;
            color.a *= opacity * alpha;
            bubble.color = color;
            bubble.enabled = color.a > 0.001f && scale > 0.001f;
        }

        private void Respawn(int i)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Range(0f, 1f)) * InnerFraction;
            origins[i] = new Vector2(Mathf.Cos(angle) * halfExtents.x, Mathf.Sin(angle) * halfExtents.y) * radius;
            sizes[i] = Range(MinSize, MaxSize);
            rises[i] = Range(MinRise, MaxRise);
            lives[i] = Range(MinLife, MaxLife);
            ages[i] = 0f;
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        // A thin bright rim around a faint body with a small glint, so it reads as a bubble at any size.
        private static Sprite SharedSprite()
        {
            if (sharedSprite != null) return sharedSprite;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Poison bubble (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = Mathf.Clamp01((0.95f - distance) / 0.08f);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.82f) / 0.14f);
                    float glintDistance = Mathf.Sqrt((dx + 0.32f) * (dx + 0.32f) + (dy - 0.34f) * (dy - 0.34f));
                    float glint = Mathf.Clamp01(1f - glintDistance / 0.2f);
                    float a = Mathf.Max(inside * 0.28f, Mathf.Max(rim * 0.9f, glint)) * inside;
                    // The glint is whiter than the tinted body.
                    byte light = (byte)Mathf.RoundToInt(Mathf.Lerp(215f, 255f, glint));
                    pixels[y * TextureSize + x] = new Color32(light, light, light, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                TextureSize);
            sharedSprite.name = "Poison bubble";
            sharedSprite.hideFlags = HideFlags.DontSave;
            return sharedSprite;
        }
    }
}
