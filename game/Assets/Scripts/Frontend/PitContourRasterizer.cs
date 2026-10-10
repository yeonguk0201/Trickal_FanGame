using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // One continuous drawing per union of rectangles. Texture coordinates follow the rounded perimeter,
    // not the authoring cells, so subdivision and joins cannot introduce a different sprite edge.
    public static class PitContourRasterizer
    {
        public const float CornerRadius = 0.30f;
        public const float WallWidth = 0.40f;
        public const int PixelsPerUnit = 128;
        private const float BucketSize = 0.5f;
        public readonly struct Result
        {
            public readonly Texture2D Texture;
            public readonly Rect Bounds;
            public readonly float Ppu;
            public readonly int ContourCount;
            public Result(Texture2D texture, Rect bounds, float ppu, int contours)
            { Texture = texture; Bounds = bounds; Ppu = ppu; ContourCount = contours; }
        }
        private sealed class Edge
        {
            public Vector2Int A, B;
            public bool Used;
        }
        private readonly struct Segment
        {
            public readonly Vector2 A, Delta;
            public readonly float Length, Start, Total;
            public Segment(Vector2 a, Vector2 b, float start, float total)
            { A = a; Delta = b - a; Length = Delta.magnitude; Start = start; Total = total; }
        }
        private sealed class Image
        {
            private readonly Color32[] pixels;
            private readonly int width, height;
            public Image(Texture2D texture) { pixels = texture.GetPixels32(); width = texture.width; height = texture.height; }
            public Color Sample(float u, float v)
            {
                float x = Mathf.Clamp01(u) * (width - 1), y = Mathf.Clamp01(v) * (height - 1);
                int ix = (int)x, iy = (int)y, nx = Mathf.Min(ix + 1, width - 1), ny = Mathf.Min(iy + 1, height - 1);
                return Color.Lerp(Color.Lerp(pixels[iy * width + ix], pixels[iy * width + nx], x - ix),
                    Color.Lerp(pixels[ny * width + ix], pixels[ny * width + nx], x - ix), y - iy);
            }
        }

        public static List<List<Vector2>> Contours(IReadOnlyList<Rect> rectangles)
        {
            if (rectangles == null || rectangles.Count == 0) return new();
            float Snap(float x) => Mathf.Round(x * 1000f) / 1000f;
            float[] xs = rectangles.SelectMany(r => new[] { Snap(r.xMin), Snap(r.xMax) }).Distinct().OrderBy(x => x).ToArray();
            float[] ys = rectangles.SelectMany(r => new[] { Snap(r.yMin), Snap(r.yMax) }).Distinct().OrderBy(y => y).ToArray();
            bool[,] occupied = new bool[xs.Length - 1, ys.Length - 1];
            for (int y = 0; y < ys.Length - 1; y++) for (int x = 0; x < xs.Length - 1; x++)
            {
                Vector2 center = new((xs[x] + xs[x + 1]) * 0.5f, (ys[y] + ys[y + 1]) * 0.5f);
                occupied[x, y] = rectangles.Any(r => r.Contains(center));
            }
            bool Filled(int x, int y) => x >= 0 && y >= 0 && x < xs.Length - 1 && y < ys.Length - 1 && occupied[x, y];
            List<Edge> edges = new();
            Dictionary<Vector2Int, List<Edge>> outgoing = new();
            void Add(int ax, int ay, int bx, int by)
            {
                Edge edge = new() { A = new(ax, ay), B = new(bx, by) };
                edges.Add(edge);
                if (!outgoing.TryGetValue(edge.A, out List<Edge> list)) outgoing[edge.A] = list = new();
                list.Add(edge);
            }
            for (int y = 0; y < ys.Length - 1; y++) for (int x = 0; x < xs.Length - 1; x++)
            {
                if (!Filled(x, y)) continue;
                if (!Filled(x, y - 1)) Add(x, y, x + 1, y);
                if (!Filled(x + 1, y)) Add(x + 1, y, x + 1, y + 1);
                if (!Filled(x, y + 1)) Add(x + 1, y + 1, x, y + 1);
                if (!Filled(x - 1, y)) Add(x, y + 1, x, y);
            }
            List<List<Vector2>> loops = new();
            foreach (Edge first in edges)
            {
                if (first.Used) continue;
                List<Vector2> loop = new();
                Edge edge = first;
                do
                {
                    if (edge == null || edge.Used) throw new InvalidOperationException("Unclosed pit boundary.");
                    edge.Used = true;
                    loop.Add(new Vector2(xs[edge.A.x], ys[edge.A.y]));
                    if (edge.B == first.A) break;
                    Vector2Int direction = edge.B - edge.A;
                    // At a diagonal-only contact turn left: preserve two independent loops.
                    edge = outgoing[edge.B].Where(e => !e.Used).OrderByDescending(e =>
                    {
                        Vector2Int next = e.B - e.A;
                        int cross = direction.x * next.y - direction.y * next.x;
                        return cross > 0 ? 3 : cross == 0 && direction.x * next.x + direction.y * next.y > 0 ? 2 : 1;
                    }).FirstOrDefault();
                } while (true);
                List<Vector2> corners = new();
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector2 incoming = (loop[i] - loop[(i + loop.Count - 1) % loop.Count]).normalized;
                    Vector2 following = (loop[(i + 1) % loop.Count] - loop[i]).normalized;
                    if (Vector2.Dot(incoming, following) < 0.9999f) corners.Add(loop[i]);
                }
                List<Vector2> rounded = new();
                for (int i = 0; i < corners.Count; i++)
                {
                    Vector2 c = corners[i], previous = corners[(i + corners.Count - 1) % corners.Count], next = corners[(i + 1) % corners.Count];
                    float radius = Mathf.Min(CornerRadius, Vector2.Distance(c, previous) * 0.45f, Vector2.Distance(c, next) * 0.45f);
                    Vector2 a = c + (previous - c).normalized * radius, b = c + (next - c).normalized * radius;
                    for (int step = 0; step <= 8; step++)
                    {
                        float t = step / 8f;
                        rounded.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
                    }
                }
                loops.Add(rounded);
            }
            return loops;
        }

        private static float Mirror(float x) => 1f - Mathf.Abs(Mathf.Repeat(x, 2f) - 1f);
        private static Vector2Int Bucket(Vector2 p) => new(Mathf.FloorToInt(p.x / BucketSize), Mathf.FloorToInt(p.y / BucketSize));

        public static Result Bake(IReadOnlyList<Rect> rectangles, Texture2D ribbon, Texture2D depth)
        {
            List<List<Vector2>> loops = Contours(rectangles);
            if (loops.Count == 0) throw new ArgumentException("Pit drawing requires a non-empty footprint.");
            Rect bounds = Rect.MinMaxRect(rectangles.Min(r => r.xMin), rectangles.Min(r => r.yMin),
                rectangles.Max(r => r.xMax), rectangles.Max(r => r.yMax));
            float ppu = Mathf.Min(PixelsPerUnit, 2048f / Mathf.Max(bounds.width, bounds.height));
            int width = Mathf.CeilToInt(bounds.width * ppu), height = Mathf.CeilToInt(bounds.height * ppu);
            List<Segment> segments = new();
            foreach (List<Vector2> loop in loops)
            {
                float length = 0;
                for (int i = 0; i < loop.Count; i++) length += Vector2.Distance(loop[i], loop[(i + 1) % loop.Count]);
                float start = 0;
                for (int i = 0; i < loop.Count; i++)
                {
                    Segment segment = new(loop[i], loop[(i + 1) % loop.Count], start, length);
                    segments.Add(segment); start += segment.Length;
                }
            }
            Dictionary<Vector2Int, List<int>> buckets = new();
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                Vector2Int a = Bucket(Vector2.Min(s.A, s.A + s.Delta) - Vector2.one * WallWidth);
                Vector2Int b = Bucket(Vector2.Max(s.A, s.A + s.Delta) + Vector2.one * WallWidth);
                for (int y = a.y; y <= b.y; y++) for (int x = a.x; x <= b.x; x++)
                {
                    Vector2Int key = new(x, y);
                    if (!buckets.TryGetValue(key, out List<int> list)) buckets[key] = list = new();
                    list.Add(i);
                }
            }
            Image wall = new(ribbon), interior = new(depth);
            Color32[] output = new Color32[width * height];
            List<float> crossings = new();
            for (int y = 0; y < height; y++)
            {
                float py = bounds.yMin + (y + 0.5f) / ppu;
                crossings.Clear();
                foreach (Segment s in segments)
                {
                    Vector2 end = s.A + s.Delta;
                    if ((s.A.y > py) == (end.y > py)) continue;
                    crossings.Add(s.A.x + (py - s.A.y) / s.Delta.y * s.Delta.x);
                }
                crossings.Sort();
                for (int span = 0; span + 1 < crossings.Count; span += 2)
                {
                    int from = Mathf.Clamp(Mathf.CeilToInt((crossings[span] - bounds.xMin) * ppu - 0.5f), 0, width);
                    int to = Mathf.Clamp(Mathf.CeilToInt((crossings[span + 1] - bounds.xMin) * ppu - 0.5f), 0, width);
                    for (int x = from; x < to; x++)
                    {
                        Vector2 p = new(bounds.xMin + (x + 0.5f) / ppu, py);
                        // Continuous world/room-local coordinates, independent of cell and rectangle splits.
                        Color baseColor = interior.Sample(Mirror(p.x / 2.4f), Mirror(p.y / 2.4f));
                        baseColor = Color.Lerp(baseColor, interior.Sample(Mirror(p.y / 3.7f + 0.37f), Mirror(p.x / 3.7f + 0.61f)), 0.22f);
                        baseColor *= 0.65f; baseColor.a = 1f;
                        float nearest = WallWidth * WallWidth, along = 0, perimeter = 1;
                        Vector2 normal = Vector2.up;
                        if (buckets.TryGetValue(Bucket(p), out List<int> candidates))
                        {
                            foreach (int index in candidates)
                            {
                                Segment s = segments[index];
                                float t = Mathf.Clamp01(Vector2.Dot(p - s.A, s.Delta) / (s.Length * s.Length));
                                Vector2 delta = p - (s.A + s.Delta * t);
                                if (delta.sqrMagnitude >= nearest) continue;
                                nearest = delta.sqrMagnitude; along = s.Start + t * s.Length;
                                perimeter = s.Total; normal = delta.normalized;
                            }
                        }
                        float distance = Mathf.Sqrt(nearest);
                        float progress = distance / WallWidth;
                        if (progress < 1f)
                        {
                            // Mirrored middle section closes exactly at the perimeter origin and avoids
                            // relying on AI-painted image ends being pixel-identical.
                            float repeats = 2 * Mathf.Max(1, Mathf.RoundToInt(perimeter / 5f));
                            float u = 0.2f + 0.6f * Mirror(along / perimeter * repeats);
                            // Give the grassy lip/rounded stones enough screen-space thickness; the source
                            // ribbon devotes most of its canvas to the descending cliff.
                            float sourceDepth = progress < 0.4f ? progress * 0.7f : 0.28f + (progress - 0.4f) * 1.2f;
                            Color rim = wall.Sample(u, 1f - sourceDepth);
                            float light = 1f + (-normal.x + normal.y) * 0.065f;
                            rim.r *= light; rim.g *= light; rim.b *= light;
                            float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 1f, progress));
                            baseColor = Color.Lerp(baseColor, rim, blend * rim.a);
                            baseColor.a = Mathf.Min(Mathf.Clamp01(distance * ppu), progress < 0.12f ? rim.a : 1f);
                        }
                        output[y * width + x] = baseColor;
                    }
                }
            }
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            { name = "Continuous pit contour", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(output); texture.Apply(false, false);
            return new Result(texture, bounds, ppu, loops.Count);
        }
    }
}
