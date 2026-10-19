using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: what a placed bomb leaves around its drawn explosion. A faint shockwave ring whose outer edge
    // stops at the damage radius, embers that keep drifting after the 0.15 s explosion drawing is gone, and a
    // scorch mark on the floor that cools and crumbles away. Shares the MuffinExplosion shader's ring mode and
    // adds its ember and scorch modes. Deterministic: never consumes UnityEngine.Random.
    public sealed class BombBurstEffect : MonoBehaviour
    {
        public const float Duration = 0.8f;
        public const float RingStart = 0.03f;
        public const float RingEnd = 0.34f;
        public const float RingOpacity = 0.8f;
        public const int EmberCount = 18;
        public const float EmberReach = 0.9f;
        // The scorch appears under the explosion drawing, so it is already there when the drawing disappears.
        public const float ScorchStart = 0.04f;
        public const float ScorchFull = 0.16f;
        public const float ScorchCooled = 0.9f;
        public const float ScorchFadeStart = 1.6f;
        public const float Lifetime = 2.6f;
        public const float ScorchSize = 0.58f;
        public const float ScorchOpacity = 1f;
        // Above room floors (-100), under ground shadows (-50) and everything standing on the floor.
        public const int ScorchSortingOrder = -60;

        private static int nextSeed;

        private Mesh quad;
        private MeshRenderer ring;
        private MeshRenderer scorch;
        private ParticleSystem embers;
        private Material emberMaterial;
        private MaterialPropertyBlock properties;
        private Transform source;
        private Vector3 sourcePosition;
        private float radius = 1f;
        private float age;
        private int seed;
        private bool embersEmitted;
        private bool runtime;

        public float Radius => radius;
        public float RingOuterRadius { get; private set; }
        public MeshRenderer Ring => ring;
        public MeshRenderer Scorch => scorch;
        public float ScorchOpacityNow { get; private set; }
        public ParticleSystem Embers => embers;

        // owner: followed only to drop the effect on a room teleport; exploding obstacles pass null.
        public static BombBurstEffect Play(Vector2 center, float damageRadius, Transform owner,
            Renderer sortingSource)
        {
            if (!Application.isPlaying) return null;
            BombBurstEffect effect = Create(center, damageRadius, sortingSource, nextSeed++);
            if (effect == null) return null;
            effect.source = owner;
            effect.sourcePosition = owner != null ? owner.position : Vector3.zero;
            effect.runtime = true;
            effect.embers.Play();
            effect.EmitAt(0f);
            return effect;
        }

        public static BombBurstEffect Create(Vector2 center, float damageRadius, Renderer sorting = null,
            int seed = 0)
        {
            Material material = CombatEffectArtwork.Material("MuffinExplosion");
            if (material == null) return null;
            var root = new GameObject("VFX bomb burst");
            root.transform.position = center;
            var effect = root.AddComponent<BombBurstEffect>();
            effect.Initialize(material, sorting, Mathf.Max(0.05f, damageRadius), seed);
            effect.RenderAt(0f);
            return effect;
        }

        private void Initialize(Material material, Renderer sorting, float damageRadius, int effectSeed)
        {
            radius = damageRadius;
            seed = effectSeed;
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Bomb burst quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            int layer = sorting != null ? sorting.sortingLayerID : 0;
            int order = sorting != null ? sorting.sortingOrder : 0;

            // Both sit under the explosion drawing: the ring shows as it widens past it, and the embers are
            // what is left glowing once the drawing disappears.
            var ringOwner = new GameObject("Shockwave ring");
            ringOwner.transform.SetParent(transform, false);
            ringOwner.AddComponent<MeshFilter>().sharedMesh = quad;
            ring = ringOwner.AddComponent<MeshRenderer>();
            ring.sharedMaterial = material;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sortingLayerID = layer;
            ring.sortingOrder = order - 2;

            var scorchOwner = new GameObject("Scorch mark");
            scorchOwner.transform.SetParent(transform, false);
            scorchOwner.transform.localScale = new Vector3(radius * ScorchSize, radius * ScorchSize, 1f);
            scorchOwner.AddComponent<MeshFilter>().sharedMesh = quad;
            scorch = scorchOwner.AddComponent<MeshRenderer>();
            scorch.sharedMaterial = material;
            scorch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            scorch.receiveShadows = false;
            scorch.sortingLayerID = layer;
            scorch.sortingOrder = ScorchSortingOrder;

            var emberOwner = new GameObject("Embers");
            emberOwner.transform.SetParent(transform, false);
            embers = emberOwner.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            embers.useAutoRandomSeed = false;
            embers.randomSeed = 61;
            var main = embers.main;
            main.duration = Duration;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            // Embers float up a little as they cool.
            main.gravityModifier = -0.06f;
            main.maxParticles = EmberCount;
            var emission = embers.emission; emission.enabled = false;
            var shape = embers.shape; shape.enabled = false;
            var speed = embers.velocityOverLifetime;
            speed.enabled = true;
            // A hard kick, then a slow drift: the streaks shorten into glowing dots.
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -6f), new Keyframe(0.3f, 0.2f, -0.6f, -0.1f), new Keyframe(1f, 0.12f)));
            var color = embers.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.35f), 0.35f),
                    new GradientColorKey(new Color(0.9f, 0.32f, 0.12f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = embers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.55f));
            var renderer = embers.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.03f;
            emberMaterial = new Material(material) { name = "Bomb burst embers" };
            emberMaterial.SetFloat("_Mode", 5f);
            renderer.sharedMaterial = emberMaterial;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order - 1;
        }

        public void RenderAt(float seconds)
        {
            RenderScorchAt(seconds);
            ring.enabled = seconds >= RingStart && seconds < RingEnd;
            if (!ring.enabled)
            {
                RingOuterRadius = 0f;
                return;
            }
            float progress = Mathf.InverseLerp(RingStart, RingEnd, seconds);
            float outer = Mathf.Lerp(0.45f, 1f, 1f - Mathf.Pow(1f - progress, 3f));
            float thickness = Mathf.Lerp(0.06f, 0.016f, progress);
            RingOuterRadius = outer * radius;
            ring.transform.localScale = new Vector3(radius, radius, 1f);
            properties.Clear();
            properties.SetFloat("_Mode", 2f);
            properties.SetFloat("_Ring", outer - thickness);
            properties.SetFloat("_Thickness", thickness);
            properties.SetFloat("_Erode", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, progress)) * 0.9f);
            properties.SetFloat("_Seed", seed % 17);
            properties.SetFloat("_Opacity", RingOpacity *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, progress))));
            ring.SetPropertyBlock(properties);
        }

        private void RenderScorchAt(float seconds)
        {
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ScorchStart, ScorchFull, seconds));
            float fade = Mathf.InverseLerp(ScorchFadeStart, Lifetime, seconds);
            ScorchOpacityNow = seconds < Lifetime ? ScorchOpacity * appear * (1f - fade * fade) : 0f;
            scorch.enabled = ScorchOpacityNow > 0.001f;
            if (!scorch.enabled) return;
            properties.Clear();
            properties.SetFloat("_Mode", 6f);
            properties.SetFloat("_Age", Mathf.InverseLerp(ScorchFull, ScorchCooled, seconds));
            properties.SetFloat("_Erode", Mathf.SmoothStep(0f, 1f, fade));
            properties.SetFloat("_Seed", seed % 13);
            properties.SetFloat("_Opacity", ScorchOpacityNow);
            scorch.SetPropertyBlock(properties);
        }

        public void EmitAt(float seconds)
        {
            if (embersEmitted || seconds < 0f) return;
            embersEmitted = true;
            Vector2 center = transform.position;
            float scale = radius / 2f;
            for (int i = 0; i < EmberCount; i++)
            {
                float angle = (i + Hash(i, 1) * 0.9f) * (360f / EmberCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float lifetime = 0.45f + Hash(i, 2) * (Duration - 0.47f);
                float start = radius * 0.2f;
                // The speed curve integrates to about 0.26 of the lifetime at full speed; drift and buoyancy stay inside EmberReach.
                float travel = radius * 0.72f * Mathf.Lerp(0.4f, 1f, Hash(i, 3)) - start;
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * start,
                    velocity = direction * (travel / (lifetime * 0.26f)),
                    startLifetime = lifetime,
                    startSize = (0.09f + Hash(i, 4) * 0.07f) * scale,
                    startColor = Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.5f, 0.12f), Hash(i, 5))
                };
                embers.Emit(particle, 1);
            }
        }

        // Editor preview and render probe: replay from the fixed seed so scrubbing back leaves no stale embers.
        public void SimulateTo(float seconds)
        {
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Restart first: the first Simulate on a stopped system would otherwise drop the burst.
            embers.Simulate(0f, false, true, false);
            embersEmitted = false;
            EmitAt(0f);
            const float step = 1f / 60f;
            for (float simulated = 0f; simulated < seconds;)
            {
                float dt = Mathf.Min(step, seconds - simulated);
                embers.Simulate(dt, false, false, false);
                simulated += dt;
            }
            RenderAt(seconds);
        }

        public void SetPreviewLayers(bool showRing, bool showEmbers, bool showScorch = true)
        {
            ring.enabled &= showRing;
            scorch.enabled &= showScorch;
            embers.GetComponent<ParticleSystemRenderer>().enabled = showEmbers;
        }

        private void Update()
        {
            if (!runtime) return;
            if (source != null)
            {
                // A room teleport must not carry old embers into the next room.
                if (!source.gameObject.activeInHierarchy ||
                    (source.position - sourcePosition).sqrMagnitude > 16f) { Destroy(gameObject); return; }
                sourcePosition = source.position;
            }
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            RenderAt(age);
        }

        private float Hash(int index, int channel)
        {
            float value = Mathf.Sin(index * 73.17f + channel * 19.31f + seed * 41.73f) * 437.13f;
            return value - Mathf.Floor(value);
        }

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(quad);
            CombatEffectArtwork.Release(emberMaterial);
        }
    }
}
