using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Combat
{
    // Visual only: the burst where a basic attack shot lands. A jagged star pops on the impact point, a ring
    // snaps outward and streaks spray back. Deterministic geometry never consumes gameplay Random or schedules
    // damage.
    public sealed class HitSpark : MonoBehaviour
    {
        public const float Duration = 0.22f;
        public const float DefaultRadius = 0.7f;
        // A spark is born this old, so the frame a critical hit freezes on already shows the star near its pop.
        public const float LeadTime = 0.025f;
        public const int StreakCount = 7;
        private const float SpreadDegrees = 150f;
        private const float SpreadJitterDegrees = 20f;

        // Star: overshoots, settles, holds, then its spikes thin out as it shrinks.
        private const int SpikeCount = 8;
        private const float StarSize = 0.56f;
        private const float StarPopPeak = 0.03f;
        private const float StarSettle = 0.07f;
        private const float StarHoldEnd = 0.1f;
        private const float StarEnd = 0.19f;
        private const float StarOvershoot = 1.25f;

        private const int RingSegments = 24;
        private const float RingStart = 0.015f;
        private const float RingEnd = 0.15f;

        // Vertex layout: streaks first (4 each), then the star (centre + rim), then the ring (inner/outer pairs).
        private const int StarFirst = StreakCount * 4;
        private const int StarRim = SpikeCount * 2;
        private const int RingFirst = StarFirst + 1 + StarRim;
        private const int VertexCount = RingFirst + (RingSegments + 1) * 2;

        // The yellows of 에르핀's basic attack orb (Erpin_Basic_Orb.png).
        private static readonly Color Core = new(1f, 1f, 0.95f);
        private static readonly Color Mid = new(0.99f, 0.91f, 0.32f);
        private static readonly Color Deep = new(0.99f, 0.82f, 0.075f);

        private static int nextSeed;

        private MeshRenderer visual;
        private Mesh mesh;
        private readonly Vector3[] vertices = new Vector3[VertexCount];
        private readonly Color[] colors = new Color[VertexCount];
        private Vector2 back = Vector2.left;
        private float radius = DefaultRadius;
        private float age = LeadTime;
        private int seed;
        private bool runtime;

        public MeshRenderer Visual => visual;
        public Mesh Mesh => mesh;

        // incoming: the direction the shot was flying. The streaks spray back from it. target is the enemy or
        // obstacle that was hit; an obstacle the hit just broke is already inactive, so its sprite is looked up
        // among inactive children too.
        public static void Play(Vector3 position, Vector2 incoming, Component target)
        {
            if (!Application.isPlaying || target == null) return;
            HitSpark effect = Create(position, incoming, DefaultRadius,
                target.GetComponentInChildren<SpriteRenderer>(true));
            if (effect == null) return;
            SceneManager.MoveGameObjectToScene(effect.gameObject, target.gameObject.scene);
            effect.seed = nextSeed++;
            effect.runtime = true;
            effect.RenderAt(effect.age);
        }

        public static HitSpark Create(Vector3 position, Vector2 incoming, float size, Renderer sorting = null)
        {
            Material material = CombatEffectArtwork.Material("HitSpark");
            if (material == null) return null;
            var root = new GameObject("Hit spark");
            root.transform.position = position;
            var effect = root.AddComponent<HitSpark>();
            effect.mesh = new Mesh { name = "Hit spark mesh" };
            effect.mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            effect.visual = root.AddComponent<MeshRenderer>();
            effect.visual.sharedMaterial = material;
            effect.visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effect.visual.receiveShadows = false;
            effect.visual.sortingLayerID = sorting != null ? sorting.sortingLayerID : 0;
            effect.visual.sortingOrder = (sorting != null ? sorting.sortingOrder : 0) + 3;
            effect.BuildTopology();
            effect.Configure(incoming, size);
            effect.RenderAt(LeadTime);
            return effect;
        }

        public void Configure(Vector2 incoming, float size)
        {
            back = incoming.sqrMagnitude > 0.0001f ? -incoming.normalized : Vector2.left;
            radius = Mathf.Max(0.05f, size);
        }

        public void RenderAt(float seconds)
        {
            visual.enabled = seconds >= 0f && seconds < Duration;
            if (!visual.enabled) return;
            RenderStreaks(seconds / Duration);
            RenderStar(seconds);
            RenderRing(seconds);
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        // The heads shoot out and slow down; the tails catch up, so each streak shrinks into its tip.
        private void RenderStreaks(float t)
        {
            float reach = 1f - (1f - t) * (1f - t);
            float tail = reach * Mathf.SmoothStep(0f, 1f, t);
            Color color = Mid;
            color.a = 1f - Mathf.SmoothStep(0.5f, 1f, t);
            for (int i = 0; i < StreakCount; i++)
            {
                float angle = (i / (float)(StreakCount - 1) - 0.5f) * SpreadDegrees +
                              (Hash(i, 1) - 0.5f) * SpreadJitterDegrees;
                Vector2 direction = Rotate(back, angle);
                float length = radius * Mathf.Lerp(0.6f, 1f, Hash(i, 2));
                Vector2 from = direction * (length * tail), to = direction * (length * reach);
                Vector2 normal = new Vector2(-direction.y, direction.x) * (radius * 0.055f * (1f - 0.6f * t));
                Vector2 middle = (from + to) * 0.5f;
                int v = i * 4;
                vertices[v] = from;
                vertices[v + 1] = middle + normal;
                vertices[v + 2] = middle - normal;
                vertices[v + 3] = to;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
            }
        }

        private void RenderStar(float seconds)
        {
            float dissolve = Mathf.InverseLerp(StarHoldEnd, StarEnd, seconds);
            float scale = seconds < StarPopPeak
                ? StarOvershoot * (1f - Mathf.Pow(1f - seconds / StarPopPeak, 2f))
                : seconds < StarSettle
                    ? Mathf.Lerp(StarOvershoot, 1f, Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(StarPopPeak, StarSettle, seconds)))
                    : 1f - 0.85f * dissolve * dissolve;
            float alpha = 1f - Mathf.SmoothStep(0.75f, 1f, dissolve);
            float spin = Hash(0, 3) * 360f + seconds / Duration * 18f;
            float size = radius * StarSize * scale;
            vertices[StarFirst] = Vector3.zero;
            colors[StarFirst] = WithAlpha(Core, alpha);
            for (int k = 0; k < StarRim; k++)
            {
                bool tip = k % 2 == 0;
                // Long and short spikes alternate; the notches between them deepen as the star dissolves.
                float reach = tip
                    ? (k / 2 % 2 == 0 ? 1f : 0.72f) - Hash(k, 4) * 0.12f
                    : 0.42f * (1f - 0.65f * dissolve);
                vertices[StarFirst + 1 + k] = Rotate(Vector2.right, spin + k * (180f / SpikeCount)) * (size * reach);
                colors[StarFirst + 1 + k] = WithAlpha(tip ? Deep : Mid, alpha);
            }
        }

        // A thin ring that snaps outward and thins away.
        private void RenderRing(float seconds)
        {
            float progress = Mathf.InverseLerp(RingStart, RingEnd, seconds);
            float ringRadius = radius * Mathf.Lerp(0.3f, 0.95f, 1f - (1f - progress) * (1f - progress));
            float halfWidth = radius * Mathf.Lerp(0.07f, 0.012f, progress);
            Color color = WithAlpha(Deep, seconds < RingStart ? 0f : 0.85f * (1f - progress));
            for (int j = 0; j <= RingSegments; j++)
            {
                Vector2 direction = Rotate(Vector2.right, j * (360f / RingSegments));
                int v = RingFirst + j * 2;
                vertices[v] = direction * (ringRadius - halfWidth);
                vertices[v + 1] = direction * (ringRadius + halfWidth);
                colors[v] = colors[v + 1] = color;
            }
        }

        // uv.x runs across a soft-edged shape (0.5 on its spine); uv.y is how far its spine whitens.
        private void BuildTopology()
        {
            var uv = new Vector2[VertexCount];
            var triangles = new int[(RingSegments * 2 + StreakCount * 2 + StarRim) * 3];
            int t = 0;
            // Drawn in this order: ring, streaks, star on top.
            for (int j = 0; j <= RingSegments; j++)
            {
                int v = RingFirst + j * 2;
                uv[v] = new Vector2(0f, 0.5f);
                uv[v + 1] = new Vector2(1f, 0.5f);
                if (j == RingSegments) break;
                triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = v + 2;
                triangles[t++] = v + 1; triangles[t++] = v + 3; triangles[t++] = v + 2;
            }
            for (int i = 0; i < StreakCount; i++)
            {
                int v = i * 4;
                uv[v] = uv[v + 3] = new Vector2(0.5f, 1f);
                uv[v + 1] = new Vector2(0f, 1f);
                uv[v + 2] = new Vector2(1f, 1f);
                triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = v + 2;
                triangles[t++] = v + 1; triangles[t++] = v + 3; triangles[t++] = v + 2;
            }
            // The star keeps a crisp edge and its own vertex colours.
            for (int k = 0; k <= StarRim; k++) uv[StarFirst + k] = new Vector2(0.5f, 0f);
            for (int k = 0; k < StarRim; k++)
            {
                triangles[t++] = StarFirst;
                triangles[t++] = StarFirst + 1 + k;
                triangles[t++] = StarFirst + 1 + (k + 1) % StarRim;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private float Hash(int index, int channel)
        {
            float value = Mathf.Sin(index * 73.17f + channel * 19.31f + seed * 41.73f) * 437.13f;
            return value - Mathf.Floor(value);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        private void Update()
        {
            if (!runtime) return;
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }

        private void OnDestroy() => CombatEffectArtwork.Release(mesh);
    }
}
