using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Code-native UI shapes: no extra font, raster artwork or scene references are needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RunLoadingScreenGraphic : MaskableGraphic
    {
        public enum Shape { RoundedBox, Circle, Pattern, DashedBorder, Tail }
        public Shape Kind { get; set; }
        public float Radius { get; set; } = 16f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (Kind == Shape.Pattern) { Pattern(vh, rect); return; }
            if (Kind == Shape.DashedBorder) { Dashes(vh, rect); return; }
            if (Kind == Shape.Tail)
            {
                Triangle(vh, new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), color);
                return;
            }
            Vector2[] points;
            if (Kind == Shape.Circle)
            {
                points = new Vector2[96];
                for (int i = 0; i < points.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / points.Length;
                    points[i] = rect.center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), rect.size * 0.5f);
                }
            }
            else
            {
                points = new Vector2[36];
                float r = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * 0.5f);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 center = new(corner is 0 or 3 ? rect.xMax - r : rect.xMin + r,
                        corner < 2 ? rect.yMax - r : rect.yMin + r);
                    for (int j = 0; j < 9; j++)
                    {
                        float angle = (corner * 90f + j * 90f / 8f) * Mathf.Deg2Rad;
                        points[corner * 9 + j] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    }
                }
            }
            for (int i = 0; i < points.Length; i++)
                Triangle(vh, rect.center, points[i], points[(i + 1) % points.Length], color);
        }

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero);
            vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Quad(VertexHelper vh, Rect rect, Color tint)
        {
            Vector2 a = new(rect.xMin, rect.yMin), b = new(rect.xMin, rect.yMax);
            Vector2 c = new(rect.xMax, rect.yMax), d = new(rect.xMax, rect.yMin);
            Triangle(vh, a, b, c, tint);
            Triangle(vh, a, c, d, tint);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = (b - a).normalized;
            Vector2 side = new Vector2(-d.y, d.x) * (width * 0.5f);
            Triangle(vh, a - side, a + side, b + side, tint);
            Triangle(vh, a - side, b + side, b - side, tint);
        }

        private void Pattern(VertexHelper vh, Rect rect)
        {
            const float cell = 150f;
            Color check = new(0.40f, 0.61f, 0.27f, 0.055f);
            Color tree = new(0.91f, 1f, 0.83f, 0.22f);
            Color star = new(0.51f, 0.74f, 0.40f, 0.23f);
            int columns = Mathf.CeilToInt(rect.width / cell), rows = Mathf.CeilToInt(rect.height / cell);
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                Vector2 origin = new(rect.xMin + x * cell, rect.yMax - y * cell);
                if ((x + y) % 2 == 0)
                    Quad(vh, new Rect(origin.x, Mathf.Max(rect.yMin, origin.y - cell),
                        Mathf.Min(cell, rect.xMax - origin.x), Mathf.Min(cell, origin.y - rect.yMin)), check);
                Vector2 center = origin + new Vector2(75f, -75f);
                if (center.x + 38f > rect.xMax || center.y - 40f < rect.yMin) continue;
                if ((x + y) % 2 == 0)
                {
                    // Rounded tree outline with a trunk and three branches.
                    Vector2[] outline = {
                        new(-6,-39),new(-6,-19),new(-24,-21),new(-33,-14),new(-30,-3),
                        new(-38,5),new(-37,15),new(-26,22),new(-23,33),new(-10,34),
                        new(0,39),new(13,35),new(23,28),new(25,18),new(34,12),
                        new(36,1),new(29,-7),new(31,-17),new(22,-24),new(6,-20),new(6,-39)
                    };
                    for (int i = 0; i < outline.Length; i++)
                        Line(vh, center + outline[i], center + outline[(i + 1) % outline.Length], 4f, tree);
                    Line(vh, center + new Vector2(0,-21), center + new Vector2(0,12), 4f, tree);
                    Line(vh, center, center + new Vector2(-12,10), 4f, tree);
                    Line(vh, center, center + new Vector2(13,11), 4f, tree);
                }
                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (90f + i * 36f) * Mathf.Deg2Rad;
                        float b = (90f + (i + 1) * 36f) * Mathf.Deg2Rad;
                        Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? 14f : 7f);
                        Vector2 q = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (i % 2 == 0 ? 7f : 14f);
                        Triangle(vh, center, p, q, star);
                    }
                }
            }
        }

        private void Dashes(VertexHelper vh, Rect rect)
        {
            float inset = 10f;
            for (float x = rect.xMin + 20f; x < rect.xMax - 20f; x += 18f)
            {
                float w = Mathf.Min(10f, rect.xMax - 20f - x);
                Quad(vh, new Rect(x, rect.yMin + inset, w, 2f), color);
                Quad(vh, new Rect(x, rect.yMax - inset - 2f, w, 2f), color);
            }
            for (float y = rect.yMin + 22f; y < rect.yMax - 22f; y += 18f)
            {
                float h = Mathf.Min(10f, rect.yMax - 22f - y);
                Quad(vh, new Rect(rect.xMin + inset, y, 2f, h), color);
                Quad(vh, new Rect(rect.xMax - inset - 2f, y, 2f, h), color);
            }
        }
    }
}
