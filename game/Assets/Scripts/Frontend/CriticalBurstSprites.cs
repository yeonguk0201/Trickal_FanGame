using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Number-0: spiky star shapes drawn behind critical damage numbers. A few shapes with different tip counts and
    // lengths are drawn once at runtime; each critical number picks one and turns and stretches it a little.
    public static class CriticalBurstSprites
    {
        public const int VariantCount = 6;
        // Darker orange rim around a lighter orange inside. The colours are drawn into the texture, so the
        // renderer tint only fades it.
        public static readonly Color32 FillColor = new(255, 170, 74, 255);
        public static readonly Color32 EdgeColor = new(236, 112, 26, 255);
        private const int TextureSize = 128;
        // Rim width in the -1..1 shape space (about 6 texture pixels).
        private const float EdgeWidth = 0.1f;
        private static Sprite[] sprites;

        public static Sprite Get(int index)
        {
            if (sprites == null) Build();
            return sprites[Mathf.Abs(index) % VariantCount];
        }

        private static void Build()
        {
            sprites = new Sprite[VariantCount];
            for (int i = 0; i < VariantCount; i++)
            {
                Vector2[] outline = Outline(new System.Random(7919 * (i + 1)));
                Texture2D texture = Rasterize(outline);
                texture.name = "Critical Burst " + i;
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                    new Vector2(0.5f, 0.5f), TextureSize);
                sprites[i].name = texture.name;
                sprites[i].hideFlags = HideFlags.HideAndDontSave;
            }
        }

        // Alternating tips and valleys around the centre, in -1..1. Tips vary in length and angle so no two
        // shapes look the same.
        private static Vector2[] Outline(System.Random random)
        {
            int tips = 7 + random.Next(5);
            float inner = 0.42f + (float)random.NextDouble() * 0.14f;
            float step = Mathf.PI * 2f / tips;
            float start = (float)random.NextDouble() * step;
            Vector2[] points = new Vector2[tips * 2];
            for (int i = 0; i < tips; i++)
            {
                float tipAngle = start + step * i + ((float)random.NextDouble() - 0.5f) * step * 0.35f;
                float tipRadius = 0.74f + (float)random.NextDouble() * 0.24f;
                float valleyAngle = start + step * (i + 0.5f) + ((float)random.NextDouble() - 0.5f) * step * 0.2f;
                float valleyRadius = inner * (0.85f + (float)random.NextDouble() * 0.3f);
                points[i * 2] = new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle)) * tipRadius;
                points[i * 2 + 1] = new Vector2(Mathf.Cos(valleyAngle), Mathf.Sin(valleyAngle)) * valleyRadius;
            }

            return points;
        }

        private static Texture2D Rasterize(Vector2[] polygon)
        {
            Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            float pixelSize = 2f / TextureSize;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    Vector2 point = new((x + 0.5f) * pixelSize - 1f, (y + 0.5f) * pixelSize - 1f);
                    float distance = EdgeDistance(polygon, point);
                    float signedPixels = (Inside(polygon, point) ? distance : -distance) / pixelSize;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(signedPixels + 0.5f) * 255f);
                    float fill = Mathf.Clamp01(signedPixels - EdgeWidth / pixelSize + 0.5f);
                    Color32 color = Color32.Lerp(EdgeColor, FillColor, fill);
                    color.a = alpha;
                    pixels[y * TextureSize + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        private static float EdgeDistance(Vector2[] polygon, Vector2 point)
        {
            float best = float.MaxValue;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[j];
                Vector2 edge = polygon[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
                best = Mathf.Min(best, (point - (a + edge * t)).sqrMagnitude);
            }

            return Mathf.Sqrt(best);
        }
    }
}
