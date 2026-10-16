using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: 폭발 머핀's small explosion. A core flash, a fireball that hollows from the center, a shockwave
    // ring whose outer edge stops at the damage radius, world-space sparks and a little smoke. Damage, timing and
    // targets stay in PlayerBasicAttackExplosion. Deterministic: never consumes UnityEngine.Random.
    public sealed class MuffinExplosionEffect : MonoBehaviour
    {
        public const float Duration = 0.4f;

        public const float FlashEnd = 0.07f;
        public const float FireballEnd = 0.26f;
        public const float FireballReach = 0.5f;
        private const float FireballGrow = 0.11f;
        private const float HoleStart = 0.06f;
        public const float RingStart = 0.015f;
        public const float RingEnd = 0.3f;
        public const int SparkCount = 14;
        public const float SparkReach = 0.92f;
        public const int SmokeCount = 6;
        public const float SmokeStart = 0.07f;

        private static int nextSeed;

        private Mesh quad;
        private MeshRenderer flash, fireball, ring;
        private ParticleSystem sparks, smoke;
        private Material sparkMaterial, smokeMaterial;
        private MaterialPropertyBlock properties;
        private Transform source;
        private Vector3 sourcePosition;
        private float radius = 1f;
        private float age;
        private int seed;
        private bool sparksEmitted, smokeEmitted;
        private bool runtime;

        public float Radius => radius;
        public float RingOuterRadius { get; private set; }
        public float FireballHalfSize { get; private set; }
        public float HoleRadius { get; private set; }
        public MeshRenderer Flash => flash;
        public MeshRenderer Fireball => fireball;
        public MeshRenderer Ring => ring;
        public ParticleSystem Sparks => sparks;
        public ParticleSystem Smoke => smoke;

        public static MuffinExplosionEffect Play(Vector2 center, float damageRadius, Transform owner,
            Renderer sortingSource)
        {
            if (!Application.isPlaying) return null;
            MuffinExplosionEffect effect = Create(center, damageRadius, sortingSource, nextSeed++);
            if (effect == null) return null;
            effect.source = owner;
            effect.sourcePosition = owner != null ? owner.position : (Vector3)center;
            effect.runtime = true;
            effect.sparks.Play();
            effect.smoke.Play();
            effect.EmitAt(0f);
            return effect;
        }

        public static MuffinExplosionEffect Create(Vector2 center, float damageRadius, Renderer sorting = null,
            int seed = 0)
        {
            Material material = CombatEffectArtwork.Material("MuffinExplosion");
            if (material == null) return null;
            var root = new GameObject("VFX muffin explosion");
            root.transform.position = center;
            var effect = root.AddComponent<MuffinExplosionEffect>();
            effect.Initialize(material, sorting, Mathf.Max(0.05f, damageRadius), seed);
            effect.RenderAt(0f);
            return effect;
        }

        private void Initialize(Material material, Renderer sorting, float damageRadius, int effectSeed)
        {
            radius = damageRadius;
            seed = effectSeed;
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Muffin explosion quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            int layer = sorting != null ? sorting.sortingLayerID : 0;
            int order = sorting != null ? sorting.sortingOrder : 0;
            // Smoke under the ring, under the fireball; sparks and the flash on top.
            smoke = CreateParticles("Smoke", material, 4, layer, order + 1, out smokeMaterial);
            ring = CreateQuad("Shockwave ring", material, layer, order + 2);
            fireball = CreateQuad("Fireball", material, layer, order + 3);
            sparks = CreateParticles("Sparks", material, 3, layer, order + 4, out sparkMaterial);
            flash = CreateQuad("Core flash", material, layer, order + 5);
            ConfigureSparks();
            ConfigureSmoke();
        }

        private MeshRenderer CreateQuad(string name, Material material, int layer, int order)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(transform, false);
            owner.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private ParticleSystem CreateParticles(string name, Material material, int mode, int layer, int order,
            out Material instance)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(transform, false);
            var system = owner.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = 53;
            var main = system.main;
            main.duration = Duration;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            instance = new Material(material) { name = "Muffin explosion " + name };
            instance.SetFloat("_Mode", mode);
            instance.SetFloat("_Seed", seed % 17);
            renderer.sharedMaterial = instance;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            return system;
        }

        private void ConfigureSparks()
        {
            var main = sparks.main;
            main.maxParticles = SparkCount;
            var speed = sparks.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2.2f), new Keyframe(1f, 0.12f)));
            var color = sparks.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.62f, 0.32f), 0.55f),
                    new GradientColorKey(new Color(0.85f, 0.3f, 0.12f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f));
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.6f;
            renderer.velocityScale = 0.05f;
        }

        private void ConfigureSmoke()
        {
            var main = smoke.main;
            main.maxParticles = SmokeCount;
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.25f));
            var speed = smoke.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
        }

        // Quads are a pure function of time; particles come from EmitAt and the particle simulation.
        public void RenderAt(float seconds)
        {
            RenderFlash(seconds);
            RenderFireball(seconds);
            RenderRing(seconds);
        }

        private void RenderFlash(float seconds)
        {
            flash.enabled = seconds >= 0f && seconds < FlashEnd;
            if (!flash.enabled) return;
            float size = radius * 0.62f * (0.7f + 0.3f * EaseOut(seconds / 0.03f));
            flash.transform.localScale = new Vector3(size, size, 1f);
            properties.Clear();
            properties.SetFloat("_Mode", 0f);
            properties.SetFloat("_Opacity", 0.95f * (1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.02f, FlashEnd, seconds))));
            flash.SetPropertyBlock(properties);
        }

        private void RenderFireball(float seconds)
        {
            fireball.enabled = seconds >= 0f && seconds < FireballEnd;
            if (!fireball.enabled)
            {
                FireballHalfSize = 0f;
                HoleRadius = 1f;
                return;
            }
            float growth = 0.35f + 0.65f * EaseOut(seconds / FireballGrow);
            FireballHalfSize = radius * FireballReach * growth;
            fireball.transform.localScale = new Vector3(FireballHalfSize, FireballHalfSize, 1f);
            float hollow = Mathf.InverseLerp(HoleStart, FireballEnd, seconds);
            HoleRadius = hollow * hollow * 1.15f;
            properties.Clear();
            properties.SetFloat("_Mode", 1f);
            properties.SetFloat("_Age", seconds / FireballEnd);
            properties.SetFloat("_Hole", HoleRadius);
            properties.SetFloat("_Seed", seed % 17);
            properties.SetFloat("_Opacity", 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(FireballEnd - 0.06f, FireballEnd, seconds)));
            fireball.SetPropertyBlock(properties);
        }

        private void RenderRing(float seconds)
        {
            ring.enabled = seconds >= RingStart && seconds < RingEnd;
            if (!ring.enabled)
            {
                RingOuterRadius = 0f;
                return;
            }
            float progress = Mathf.InverseLerp(RingStart, RingEnd, seconds);
            float outer = Mathf.Lerp(0.3f, 1f, 1f - Mathf.Pow(1f - progress, 3f));
            float thickness = Mathf.Lerp(0.06f, 0.014f, progress);
            RingOuterRadius = outer * radius;
            // The quad is exactly the damage circle; the band's outer edge sits at `outer` inside it.
            ring.transform.localScale = new Vector3(radius, radius, 1f);
            properties.Clear();
            properties.SetFloat("_Mode", 2f);
            properties.SetFloat("_Ring", outer - thickness);
            properties.SetFloat("_Thickness", thickness);
            properties.SetFloat("_Erode", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, progress)) * 0.9f);
            properties.SetFloat("_Seed", seed % 17);
            properties.SetFloat("_Opacity", 0.9f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress))));
            ring.SetPropertyBlock(properties);
        }

        // Shared by Play and the deterministic Editor resimulation.
        public void EmitAt(float seconds)
        {
            if (!sparksEmitted && seconds >= 0f)
            {
                sparksEmitted = true;
                EmitSparks();
            }
            if (!smokeEmitted && seconds >= SmokeStart)
            {
                smokeEmitted = true;
                EmitSmoke();
            }
        }

        private void EmitSparks()
        {
            Vector2 center = transform.position;
            float scale = radius / 1.5f;
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = (i + Hash(i, 1) * 0.8f) * (360f / SparkCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float lifetime = 0.17f + Hash(i, 2) * 0.13f;
                // Average speed modifier is about 0.53, so this keeps every spark inside SparkReach * radius.
                float travel = radius * SparkReach * Mathf.Lerp(0.5f, 1f, Hash(i, 3)) - radius * 0.12f;
                float speed = travel / (lifetime * 0.56f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * (radius * 0.12f),
                    velocity = direction * speed,
                    startLifetime = lifetime,
                    startSize = (0.1f + Hash(i, 4) * 0.07f) * scale,
                    startColor = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.7f, 0.25f), Hash(i, 5))
                };
                sparks.Emit(particle, 1);
            }
        }

        private void EmitSmoke()
        {
            Vector2 center = transform.position;
            float scale = radius / 1.5f;
            for (int i = 0; i < SmokeCount; i++)
            {
                float angle = (i + Hash(i, 6) * 0.6f) * (360f / SmokeCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float shade = 0.4f + Hash(i, 7) * 0.12f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * (radius * (0.12f + Hash(i, 8) * 0.14f)),
                    velocity = direction * (0.5f + Hash(i, 9) * 0.5f) * scale + Vector2.up * 0.35f * scale,
                    startLifetime = Duration - SmokeStart - 0.01f - Hash(i, 10) * 0.06f,
                    startSize = (0.42f + Hash(i, 11) * 0.22f) * scale,
                    rotation = Hash(i, 12) * 360f,
                    startColor = new Color(shade, shade * 0.93f, shade * 0.88f, 0.5f)
                };
                smoke.Emit(particle, 1);
            }
        }

        // Editor preview: replay particles from the fixed seed so scrubbing back never leaves stale ones.
        public void SimulateTo(float seconds)
        {
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Restart first: the first Simulate on a stopped system would otherwise drop the impact burst.
            sparks.Simulate(0f, false, true, false);
            smoke.Simulate(0f, false, true, false);
            sparksEmitted = smokeEmitted = false;
            const float step = 1f / 60f;
            float simulated = 0f;
            EmitAt(0f);
            while (simulated < seconds)
            {
                float dt = Mathf.Min(step, seconds - simulated);
                sparks.Simulate(dt, false, false, false);
                smoke.Simulate(dt, false, false, false);
                simulated += dt;
                EmitAt(simulated);
            }
            RenderAt(seconds);
        }

        public void SetPreviewLayers(bool showFlash, bool showFireball, bool showRing, bool showSparks, bool showSmoke)
        {
            flash.enabled &= showFlash;
            fireball.enabled &= showFireball;
            ring.enabled &= showRing;
            sparks.GetComponent<ParticleSystemRenderer>().enabled = showSparks;
            smoke.GetComponent<ParticleSystemRenderer>().enabled = showSmoke;
        }

        private void Update()
        {
            if (!runtime) return;
            if (source == null || !source.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            // A room teleport must not carry an old explosion into the next room.
            if ((source.position - sourcePosition).sqrMagnitude > 16f) { Destroy(gameObject); return; }
            sourcePosition = source.position;
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            EmitAt(age);
            RenderAt(age);
        }

        private float Hash(int index, int channel)
        {
            float value = Mathf.Sin(index * 73.17f + channel * 19.31f + seed * 41.73f) * 437.13f;
            return value - Mathf.Floor(value);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(quad);
            CombatEffectArtwork.Release(sparkMaterial);
            CombatEffectArtwork.Release(smokeMaterial);
        }
    }
}
