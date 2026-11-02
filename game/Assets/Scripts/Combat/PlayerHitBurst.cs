using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Effect-0 (docs/28): visual only. The cartoon "앗 따가!" burst when the player is hit: one big yellow-white star
    // pops on the side the hit came from, and a few small stars fly out and fall away. A heavy hit makes it bigger
    // with more small stars. Never changes damage, timing or invulnerability. Deterministic: never consumes
    // UnityEngine.Random. Created only during Play.
    public sealed class PlayerHitBurst : MonoBehaviour
    {
        public const float Duration = 0.42f;
        public const float StarLife = 0.26f;
        public const float StarSize = 0.55f;
        public const float HeavyScale = 1.4f;
        public const int SmallStarCount = 3;
        public const int HeavySmallStarCount = 5;
        public const float SmallStarSize = 0.16f;
        public const float SmallStarSpeed = 2.4f;
        public const float SmallStarGravity = 6f;
        // How far toward the hit side the big star sits, as a share of the body's half size.
        public const float SideOffset = 0.55f;
        public static readonly Color StarColor = new(1f, 0.86f, 0.3f, 1f);
        private const int TextureSize = 64;
        private const float TeleportDistance = 3f;

        private static Sprite sharedSprite;
        private static int nextSeed = 1;

        private SpriteRenderer star;
        private SpriteRenderer[] smallStars;
        private Vector2[] velocities;
        private float[] spins;
        private Transform owner;
        private Vector3 ownerStart;
        private Vector3 starOffset;
        private float scale = 1f;
        private float starSpin;
        private float age;

        public SpriteRenderer Star => star;
        public int SmallStars => smallStars != null ? smallStars.Length : 0;
        public float Scale => scale;
        public Vector3 StarOffset => starOffset;

        // body: the player's visible sprite. hitFrom: where the hit came from (null = straight in front, up a little).
        public static PlayerHitBurst Play(Transform player, SpriteRenderer body, Vector2? hitFrom, bool heavy)
        {
            if (!Application.isPlaying || player == null || body == null) return null;
            var root = new GameObject("VFX player hit burst");
            var effect = root.AddComponent<PlayerHitBurst>();
            effect.Initialize(player, body, hitFrom, heavy, nextSeed++);
            return effect;
        }

        private void Initialize(Transform player, SpriteRenderer body, Vector2? hitFrom, bool heavy, int seed)
        {
            owner = player;
            ownerStart = player.position;
            scale = heavy ? HeavyScale : 1f;
            var random = new System.Random(seed * 7919);
            Bounds bounds = body.bounds;
            Vector2 center = bounds.center;
            Vector2 direction = hitFrom.HasValue ? hitFrom.Value - center : Vector2.up;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            // Kept inside the body ellipse so the star reads as "on" the player, not floating beside them.
            Vector2 side = new(direction.x * bounds.extents.x * SideOffset, direction.y * bounds.extents.y * SideOffset);
            Vector3 world = new(center.x + side.x, center.y + side.y, player.position.z);
            starOffset = world - player.position;
            transform.position = world;

            int layer = body.sortingLayerID;
            int order = body.sortingOrder + 3;
            star = CreateStar("Star", layer, order);
            starSpin = (float)(random.NextDouble() * 2.0 - 1.0) * 40f;
            star.transform.localRotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 72f);

            int count = heavy ? HeavySmallStarCount : SmallStarCount;
            smallStars = new SpriteRenderer[count];
            velocities = new Vector2[count];
            spins = new float[count];
            // Small stars spray away from the hit, spread over a half circle around that side.
            float baseAngle = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < count; i++)
            {
                float spread = count > 1 ? (i / (count - 1f) - 0.5f) * Mathf.PI * 0.9f : 0f;
                float jitter = (float)(random.NextDouble() - 0.5) * 0.35f;
                float angle = baseAngle + spread + jitter;
                float speed = SmallStarSpeed * scale * Mathf.Lerp(0.75f, 1.15f, (float)random.NextDouble());
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + Vector2.up * 0.8f;
                spins[i] = (float)(random.NextDouble() * 2.0 - 1.0) * 360f;
                smallStars[i] = CreateStar("Small star", layer, order);
                smallStars[i].transform.localScale = Vector3.one * SmallStarSize * scale;
            }

            Render();
        }

        private SpriteRenderer CreateStar(string name, int layer, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = SharedSprite();
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            renderer.color = Color.white;
            return renderer;
        }

        private void Update()
        {
            // A room teleport drops the burst instead of leaving it in the old room.
            if (owner == null || (owner.position - ownerStart).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                Destroy(gameObject);
                return;
            }

            float delta = Time.deltaTime;
            age += delta;
            if (age >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < smallStars.Length; i++)
            {
                velocities[i] += Vector2.down * SmallStarGravity * delta;
                velocities[i] *= 1f - Mathf.Clamp01(2.5f * delta);
                Transform t = smallStars[i].transform;
                t.localPosition += (Vector3)(velocities[i] * delta);
                t.localRotation *= Quaternion.Euler(0f, 0f, spins[i] * delta);
            }

            star.transform.localRotation *= Quaternion.Euler(0f, 0f, starSpin * delta);
            Render();
        }

        private void Render()
        {
            // The big star follows the player; the small ones fly free in the world.
            if (owner != null) star.transform.position = owner.position + starOffset;

            // Pops past full size, settles back, then shrinks away.
            float s;
            float t = age / StarLife;
            if (age < 0.06f) s = Mathf.Lerp(0.3f, 1.2f, age / 0.06f);
            else if (age < 0.12f) s = Mathf.Lerp(1.2f, 1f, (age - 0.06f) / 0.06f);
            else s = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((age - 0.12f) / (StarLife - 0.12f)));
            star.transform.localScale = Vector3.one * StarSize * scale * s;
            Color color = Color.white;
            color.a = t < 0.55f ? 1f : Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
            star.color = color;
            star.enabled = color.a > 0.001f;

            float smallFade = Mathf.Clamp01(1f - Mathf.Max(0f, age - Duration * 0.5f) / (Duration * 0.5f));
            for (int i = 0; i < smallStars.Length; i++)
            {
                Color small = Color.white;
                small.a = smallFade;
                smallStars[i].color = small;
            }
        }

        // A plump five-point star: white core, warm rim and a soft dark outline so it reads on light floors.
        private static Sprite SharedSprite()
        {
            if (sharedSprite != null) return sharedSprite;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Player hit star (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            const float outer = 0.92f, inner = 0.46f, outline = 0.1f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // Star radius at this angle: points straight up, rounded valleys between points.
                    float angle = Mathf.Atan2(dx, dy);
                    float wave = Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 2f / 5f) + 0.5f, 1f) - 0.5f) * 2f;
                    float radius = Mathf.Lerp(outer, inner, Mathf.SmoothStep(0f, 1f, wave));
                    float edge = radius - distance;
                    float body = Mathf.Clamp01(edge / 0.04f);
                    float shape = Mathf.Clamp01((edge + outline) / 0.04f);
                    if (shape <= 0f)
                    {
                        pixels[y * TextureSize + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // White in the middle, warm yellow toward the rim. Colours are baked; renderers only fade alpha.
                    float core = Mathf.Clamp01(1f - distance / (radius * 0.7f));
                    Color fill = Color.Lerp(StarColor, Color.white, core * core);
                    Color dark = new(0.55f, 0.32f, 0.12f);
                    Color c = Color.Lerp(dark, fill, body);
                    c.a = Mathf.Max(body, shape * 0.75f);
                    pixels[y * TextureSize + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                TextureSize);
            sharedSprite.name = "Player hit star";
            sharedSprite.hideFlags = HideFlags.DontSave;
            return sharedSprite;
        }
    }
}
