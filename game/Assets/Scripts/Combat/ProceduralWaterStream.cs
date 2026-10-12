using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Comparison prototype: a world-space strip, animated water shader and independent droplets.
    // This component never performs damage queries and never consumes UnityEngine.Random.
    public sealed class ProceduralWaterStream : MonoBehaviour
    {
        private const int Segments = 80;
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector2[] uvs = new Vector2[(Segments + 1) * 2];
        private Mesh mesh;
        private MeshRenderer body;
        private ParticleSystem droplets;
        private Material dropMaterial;
        private MaterialPropertyBlock properties;
        private GameObject source;
        private Health sourceHealth;
        private bool transient;
        private bool atWall;
        private float started, ends, width;
        private Vector2 start, end, previousSource;
        private int emission;

        public bool BodyVisible => body != null && body.enabled;
        public Vector2 StartPoint => start;
        public Vector2 EndPoint => end;
        public Mesh BodyMesh => mesh;
        public ParticleSystem Droplets => droplets;

        public static ProceduralWaterStream Create(GameObject owner, bool isTransient = false)
        {
            Material material = CombatEffectArtwork.Material("ProceduralWaterStream");
            if (material == null) return null;
            GameObject root = new("Shasha realtime water");
            var effect = root.AddComponent<ProceduralWaterStream>();
            effect.source = owner;
            effect.sourceHealth = owner.GetComponent<Health>();
            effect.previousSource = owner.transform.position;
            effect.transient = isTransient;
            effect.Initialize(material, owner.GetComponentInChildren<SpriteRenderer>());
            return effect;
        }

        private void Initialize(Material material, SpriteRenderer sorting)
        {
            properties = new MaterialPropertyBlock();
            mesh = new Mesh { name = "Shasha water strip" };
            mesh.MarkDynamic();
            var triangles = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            body = gameObject.AddComponent<MeshRenderer>();
            body.sharedMaterial = material;
            body.sortingLayerID = sorting != null ? sorting.sortingLayerID : 0;
            body.sortingOrder = (sorting != null ? sorting.sortingOrder : 0) - 1;
            body.enabled = false;

            var dropOwner = new GameObject("World water droplets");
            dropOwner.transform.SetParent(transform, false);
            droplets = dropOwner.AddComponent<ParticleSystem>();
            droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            droplets.useAutoRandomSeed = false;
            droplets.randomSeed = 37;
            var main = droplets.main;
            main.duration = 0.01f;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = 0.24f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.gravityModifier = 0f;
            var emit = droplets.emission; emit.enabled = false;
            var shape = droplets.shape; shape.enabled = false;
            var color = droplets.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.9f, 0.45f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var dropRenderer = droplets.GetComponent<ParticleSystemRenderer>();
            dropMaterial = new Material(material) { name = "Shasha water droplets" };
            dropMaterial.SetFloat("_Droplet", 1);
            dropRenderer.sharedMaterial = dropMaterial;
            dropRenderer.sortingLayerID = body.sortingLayerID;
            dropRenderer.sortingOrder = body.sortingOrder + 1;
        }

        public void Begin(Vector2 from, Vector2 to, float lineWidth, float now, float duration, bool hitsWall)
        {
            started = now;
            ends = now + Mathf.Max(0.001f, duration);
            width = Mathf.Max(0.001f, lineWidth);
            SetPath(from, to, hitsWall);
            RenderAt(now);
            if (!Application.isPlaying) return;
            droplets.Play();
            EmitDrops(false);
            if (atWall) EmitDrops(true);
        }

        public void SetPath(Vector2 from, Vector2 to, bool hitsWall)
        {
            start = from; end = to; atWall = hitsWall;
            // Keep splash decoration on the playable side of the current wall plane.
            Vector2 forward = (end - start).normalized;
            dropMaterial.SetVector("_EndPlane", new Vector4(end.x, end.y, forward.x, forward.y));
            dropMaterial.SetFloat("_AtWall", atWall ? 1 : 0);
        }

        public void RenderAt(float now)
        {
            Vector2 offset = end - start;
            float length = offset.magnitude;
            body.enabled = now >= started && now < ends && length > 0.001f;
            if (!body.enabled) return;
            Vector2 forward = offset / length;
            Vector2 side = new(-forward.y, forward.x);
            float age = (now - started) / (ends - started);
            float envelope = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 0.10f)) *
                             (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - 0.65f) / 0.35f)));
            for (int i = 0; i <= Segments; i++)
            {
                float fraction = i / (float)Segments;
                float x = fraction * length;
                // Wave travels toward the endpoint; centerline stays on the straight hit path.
                float ripple = 1 + 0.10f * Mathf.Sin(x * 18 - now * 42) +
                               0.06f * Mathf.Sin(x * 31 - now * 65);
                float nozzle = 0.72f + 0.28f * Mathf.SmoothStep(0, 1, Mathf.Min(1, x / 0.18f));
                float radius = width * 0.72f * ripple * nozzle * (0.4f + 0.6f * envelope);
                Vector2 center = start + forward * x;
                vertices[i * 2] = center - side * radius;
                vertices[i * 2 + 1] = center + side * radius;
                uvs[i * 2] = new Vector2(x, -1);
                uvs[i * 2 + 1] = new Vector2(x, 1);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            properties.SetFloat("_Clock", now);
            properties.SetFloat("_Opacity", envelope);
            properties.SetFloat("_Length", length);
            body.SetPropertyBlock(properties);
        }

        public void HideBody() { if (body != null) body.enabled = false; ends = float.NegativeInfinity; }

        public void Clear()
        {
            HideBody();
            if (droplets != null) droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (source == null || !source.activeInHierarchy || (sourceHealth != null && sourceHealth.IsDead))
            { Destroy(gameObject); return; }
            Vector2 position = source.transform.position;
            if ((position - previousSource).sqrMagnitude > 16f) Clear();
            previousSource = position;
            if (Time.timeScale <= 0f) return;
            RenderAt(Time.time);
            if (transient && !BodyVisible && !droplets.IsAlive()) Destroy(gameObject);
        }

        private void EmitDrops(bool impact)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.02f) return;
            Vector2 forward = delta / length;
            Vector2 side = new(-forward.y, forward.x);
            int count = impact ? 10 : Mathf.Clamp(Mathf.CeilToInt(length * 2), 3, 18);
            for (int i = 0; i < count; i++)
            {
                float seed = Seed(++emission);
                float sign = (i & 1) == 0 ? 1 : -1;
                Vector2 point = impact ? end - forward * 0.03f :
                    start + forward * (length * (i + 0.5f) / count);
                point += side * width * sign * 0.35f;
                Vector2 velocity = impact ? -forward * (0.4f + seed * 1.1f) + side * sign * (0.5f + seed * 1.5f) :
                    forward * (1.8f + seed * 2) + side * sign * (0.3f + seed * 0.7f);
                float size = width * (0.22f + seed * 0.25f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = velocity,
                    startLifetime = 0.16f + seed * 0.15f,
                    startSize3D = new Vector3(size * 1.7f, size, 1),
                    rotation = -Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg,
                    startColor = Color.white
                };
                droplets.Emit(particle, 1);
            }
        }

        private static float Seed(int i) => Mathf.Repeat(Mathf.Sin(i * 78.233f) * 43758.5453f, 1);

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(mesh);
            CombatEffectArtwork.Release(dropMaterial);
        }
    }
}
