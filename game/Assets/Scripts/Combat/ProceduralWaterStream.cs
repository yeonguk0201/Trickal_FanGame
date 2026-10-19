using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Comparison prototype: a world-space strip, animated water shader and independent droplets.
    // This component never performs damage queries and never consumes UnityEngine.Random.
    public sealed class ProceduralWaterStream : MonoBehaviour
    {
        private const int Segments = 80;
        // Extra strip width past the water edge for beads clinging to it; must match _Halo in the shader.
        private const float BeadMargin = 1.18f;
        public const int MaxMist = 32;
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector2[] uvs = new Vector2[(Segments + 1) * 2];
        private readonly Vector3[] splashVertices = new Vector3[4];
        private readonly Vector3[] muzzleVertices = new Vector3[4];
        private Mesh mesh;
        private MeshRenderer body;
        private Mesh splashMesh;
        private MeshRenderer splash;
        private Material splashMaterial;
        private Mesh muzzleMesh;
        private MeshRenderer muzzle;
        private Material muzzleMaterial;
        private ParticleSystem droplets;
        private Material dropMaterial;
        private ParticleSystem mist;
        private Material mistMaterial;
        private MaterialPropertyBlock properties;
        private GameObject source;
        private Health sourceHealth;
        private bool transient;
        private bool atWall;
        private float started, ends, width;
        private Vector2 start, end, previousSource;
        private int emission, mistEmission, emissionTick;
        private float nextEmission;
        private bool impactEmitted;

        public bool BodyVisible => body != null && body.enabled;
        public Vector2 StartPoint => start;
        public Vector2 EndPoint => end;
        public Mesh BodyMesh => mesh;
        public ParticleSystem Droplets => droplets;
        public bool SplashVisible => splash != null && splash.enabled;
        public Mesh SplashMesh => splashMesh;
        public bool MuzzleVisible => muzzle != null && muzzle.enabled;
        public Mesh MuzzleMesh => muzzleMesh;
        public ParticleSystem Mist => mist;

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

            splash = CreateQuad("Contact water fan", material, "_Splash", body.sortingOrder + 1,
                "Shasha contact fan", out splashMesh, out splashMaterial);
            // The gush leaving the jar mouth. Drawn under the body so the stream stays continuous.
            muzzle = CreateQuad("Nozzle water gush", material, "_Muzzle", body.sortingOrder - 1,
                "Shasha nozzle gush", out muzzleMesh, out muzzleMaterial);

            droplets = CreateParticles("World water droplets", 37, 96, material, "_Droplet",
                body.sortingOrder + 1, out dropMaterial);
            var main = droplets.main;
            main.startLifetime = 0.24f;
            main.startSize3D = true;
            var color = droplets.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.9f, 0.45f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var sizeOverLifetime = droplets.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1,
                AnimationCurve.EaseInOut(0, 1, 1, 0.25f));

            // Fine spray haze: soft, slow and short-lived, independent from the bead droplets.
            mist = CreateParticles("World water mist", 71, MaxMist, material, "_Mist",
                body.sortingOrder - 1, out mistMaterial);
            var mistMain = mist.main;
            mistMain.startLifetime = 0.45f;
            var mistColor = mist.colorOverLifetime;
            mistColor.enabled = true;
            var haze = new Gradient();
            haze.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f),
                    new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0, 1) });
            mistColor.color = haze;
            var mistSize = mist.sizeOverLifetime;
            mistSize.enabled = true;
            mistSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.8f, 1, 1.6f));
        }

        private MeshRenderer CreateQuad(string name, Material material, string mode, int order, string meshName,
            out Mesh quad, out Material quadMaterial)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(transform, false);
            quad = new Mesh { name = meshName };
            quad.vertices = new Vector3[4];
            quad.uv = new[] { new Vector2(0, -1), new Vector2(0, 1),
                new Vector2(1, -1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            owner.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = owner.AddComponent<MeshRenderer>();
            quadMaterial = new Material(material) { name = meshName };
            quadMaterial.SetFloat(mode, 1);
            renderer.sharedMaterial = quadMaterial;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private ParticleSystem CreateParticles(string name, uint seed, int limit, Material material, string mode,
            int order, out Material particleMaterial)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(transform, false);
            var system = owner.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = seed;
            var main = system.main;
            main.duration = 0.01f;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emit = system.emission; emit.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            particleMaterial = new Material(material) { name = name };
            particleMaterial.SetFloat(mode, 1);
            renderer.sharedMaterial = particleMaterial;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = order;
            return system;
        }

        public void Begin(Vector2 from, Vector2 to, float lineWidth, float now, float duration, bool hitsWall)
        {
            started = now;
            ends = now + Mathf.Max(0.001f, duration);
            width = Mathf.Max(0.001f, lineWidth);
            nextEmission = now;
            impactEmitted = false;
            emissionTick = 0;
            SetPath(from, to, hitsWall);
            RenderAt(now);
            if (!Application.isPlaying) return;
            droplets.Play();
            mist.Play();
            EmitAt(now);
        }

        public void SetPath(Vector2 from, Vector2 to, bool hitsWall)
        {
            start = from; end = to; atWall = hitsWall;
            // Keep splash decoration on the playable side of the current wall plane.
            Vector2 forward = (end - start).normalized;
            var plane = new Vector4(end.x, end.y, forward.x, forward.y);
            dropMaterial.SetVector("_EndPlane", plane);
            dropMaterial.SetFloat("_AtWall", atWall ? 1 : 0);
            mistMaterial.SetVector("_EndPlane", plane);
            mistMaterial.SetFloat("_AtWall", atWall ? 1 : 0);
        }

        public void RenderAt(float now)
        {
            Vector2 offset = end - start;
            float length = offset.magnitude;
            body.enabled = now >= started && now < ends && length > 0.001f;
            splash.enabled = body.enabled && atWall;
            // Cutter/bounce links start on an enemy, not at the jar.
            muzzle.enabled = body.enabled && !transient;
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
                // Pump pulses travel outwards. Pin both ends and bound decoration by the hit width.
                float phase = x * 7.5f - now * 32f;
                float ripple = 1 + 0.20f * Mathf.Sin(phase) +
                               0.07f * Mathf.Sin(x * 19 - now * 57);
                float nozzle = 0.72f + 0.28f * Mathf.SmoothStep(0, 1, Mathf.Min(1, x / 0.18f));
                float radius = width * 0.72f * BeadMargin * ripple * nozzle * (0.4f + 0.6f * envelope);
                float pin = Mathf.SmoothStep(0, 1, Mathf.Min(x, length - x) / 0.24f);
                float wave = width * 0.32f * pin * (Mathf.Sin(phase) +
                    0.25f * Mathf.Sin(x * 12 - now * 43));
                Vector2 center = start + forward * x + side * wave;
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
            properties.SetFloat("_BurstAge", age);
            properties.SetFloat("_AtWall", atWall ? 1 : 0);
            properties.SetFloat("_Width", width);
            body.SetPropertyBlock(properties);
            if (splash.enabled)
            {
                // All vertices stay on the incoming side of the endpoint plane, even on short paths.
                // A fast impact opens into a broken crown, then sheds fading droplets.
                // Scale depth separately so a nearby wall cannot push the fan behind the nozzle.
                float expansion = 0.45f + 0.55f * (1 - Mathf.Pow(1 - age, 3));
                float depth = Mathf.Min(length, width * 3.2f * expansion);
                float spread = width * 3.1f * expansion;
                Vector2 back = end - forward * depth;
                splashVertices[0] = back - side * spread;
                splashVertices[1] = back + side * spread;
                splashVertices[2] = end - side * spread;
                splashVertices[3] = end + side * spread;
                splashMesh.vertices = splashVertices;
                splashMesh.RecalculateBounds();
                splash.SetPropertyBlock(properties);
            }
            if (muzzle.enabled)
            {
                // Opens hard on the first frames, then breathes with the pump pulse. The forward reach
                // is limited by the path so a wall right in front cannot push the gush through it.
                float burst = 1 + 0.35f * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 0.3f)));
                float breathe = 1 + 0.12f * Mathf.Sin(now * 32f);
                float reach = Mathf.Min(length, width * 2.3f * burst);
                float behind = Mathf.Min(width * 0.25f, reach);
                float spread = width * 1.45f * burst * breathe;
                Vector2 rear = start - forward * behind;
                Vector2 front = start + forward * reach;
                muzzleVertices[0] = rear - side * spread;
                muzzleVertices[1] = rear + side * spread;
                muzzleVertices[2] = front - side * spread;
                muzzleVertices[3] = front + side * spread;
                muzzleMesh.vertices = muzzleVertices;
                muzzleMesh.RecalculateBounds();
                muzzle.SetPropertyBlock(properties);
            }
        }

        public void HideBody()
        {
            if (body != null) body.enabled = false;
            if (splash != null) splash.enabled = false;
            if (muzzle != null) muzzle.enabled = false;
            ends = float.NegativeInfinity;
        }

        public void SetPreviewLayers(bool showBody, bool showDrops, bool showSplash, bool showMuzzle = true,
            bool showMist = true)
        {
            body.enabled &= showBody;
            droplets.GetComponent<ParticleSystemRenderer>().enabled = showDrops;
            splash.enabled &= showSplash;
            muzzle.enabled &= showMuzzle;
            mist.GetComponent<ParticleSystemRenderer>().enabled = showMist;
        }

        // Shared fixed-step emission for Play and deterministic Editor resimulation.
        public void EmitAt(float now)
        {
            if (now < started || now >= ends || now < nextEmission) return;
            nextEmission = now + 1f / 60f;
            EmitDrops(false);
            bool firstImpact = atWall && !impactEmitted;
            if (atWall)
            {
                EmitDrops(true, firstImpact);
                impactEmitted = true;
            }
            EmitMist(firstImpact);
            emissionTick++;
        }

        public void ResetPreviewParticles()
        {
            droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emission = 0;
            mistEmission = 0;
            emissionTick = 0;
            nextEmission = started;
            impactEmitted = false;
        }

        public void Clear()
        {
            HideBody();
            if (droplets != null) droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (mist != null) mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            EmitAt(Time.time);
            if (transient && !BodyVisible && !droplets.IsAlive() && !mist.IsAlive()) Destroy(gameObject);
        }

        private void EmitDrops(bool impact, bool burst = false)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.02f) return;
            Vector2 forward = delta / length;
            Vector2 side = new(-forward.y, forward.x);
            int count = impact ? (burst ? 18 : 3) : Mathf.Clamp(Mathf.CeilToInt(length * 0.65f), 1, 5);
            for (int i = 0; i < count; i++)
            {
                float seed = Seed(++emission);
                float sign = (i & 1) == 0 ? 1 : -1;
                Vector2 point = impact ? end - forward * Mathf.Min(length * 0.5f, width * 0.3f) :
                    start + forward * (length * (i + 0.5f) / count);
                point += side * width * sign * 0.35f;
                Vector2 velocity = impact ? (-forward * (0.6f + seed * 1.8f) +
                    side * sign * (1.4f + Seed(emission + 53) * 2.5f)) * (burst ? 1.4f : 0.75f) :
                    forward * (1.8f + seed * 2) + side * sign * (0.3f + seed * 0.7f);
                // Impact spray stays fine: large beads at the wall read as soap bubbles.
                float size = width * (impact ? 0.14f + seed * 0.28f : 0.18f + seed * 0.22f);
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

        private void EmitMist(bool firstImpact)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.02f) return;
            Vector2 forward = delta / length;
            Vector2 side = new(-forward.y, forward.x);
            // Wall haze is the main cloud; the body sheds a thin trail and the nozzle a puff on the first tick.
            if (atWall && (firstImpact || emissionTick % 2 == 0))
                for (int i = 0, count = firstImpact ? 6 : 1; i < count; i++)
                {
                    float seed = Seed(++mistEmission);
                    float sign = (i & 1) == 0 ? 1 : -1;
                    Vector2 velocity = -forward * (0.25f + seed * 0.55f) +
                                       side * sign * (0.35f + Seed(mistEmission + 19) * 0.9f);
                    EmitHaze(end - forward * Mathf.Min(length * 0.5f, width * 0.6f), velocity,
                        width * (1.4f + seed * 1.1f), seed);
                }
            if (emissionTick % 3 == 1 && length > width * 3)
            {
                float seed = Seed(++mistEmission);
                float sign = seed > 0.5f ? 1 : -1;
                Vector2 point = start + forward * (length * (0.2f + 0.7f * Seed(mistEmission + 7))) +
                                side * sign * width * 0.4f;
                EmitHaze(point, forward * (0.4f + seed * 0.5f) + side * sign * (0.2f + seed * 0.3f),
                    width * (0.9f + seed * 0.6f), seed);
            }
            if (emissionTick == 0 && !transient)
                for (int i = 0; i < 2; i++)
                {
                    float seed = Seed(++mistEmission);
                    float sign = i == 0 ? 1 : -1;
                    EmitHaze(start + forward * Mathf.Min(length, width * 1.2f),
                        forward * (0.5f + seed * 0.4f) + side * sign * (0.45f + seed * 0.35f),
                        width * (1.1f + seed * 0.5f), seed);
                }
        }

        private void EmitHaze(Vector2 point, Vector2 velocity, float size, float seed)
        {
            mist.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                velocity = velocity,
                startLifetime = 0.32f + seed * 0.22f,
                startSize = size,
                rotation = seed * 360f,
                startColor = Color.white
            }, 1);
        }

        private static float Seed(int i) => Mathf.Repeat(Mathf.Sin(i * 78.233f) * 43758.5453f, 1);

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(mesh);
            CombatEffectArtwork.Release(splashMesh);
            CombatEffectArtwork.Release(splashMaterial);
            CombatEffectArtwork.Release(muzzleMesh);
            CombatEffectArtwork.Release(muzzleMaterial);
            CombatEffectArtwork.Release(dropMaterial);
            CombatEffectArtwork.Release(mistMaterial);
        }
    }
}
