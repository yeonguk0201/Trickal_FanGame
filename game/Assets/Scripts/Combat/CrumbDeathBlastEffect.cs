using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: the pink burst around a crumb minion's death blast (EnemyDeathBlast). It surrounds the existing
    // effect-crumb-death drawing in the same candy pink: a short flash, a cartoon pink cloud that opens from the
    // center, a pink shockwave whose outer edge stops at the damage radius, goo droplets and cake crumbs that fly
    // out and fall, and a little pink smoke. Damage, timing and targets stay in EnemyDeathBlast.
    // Deterministic: never consumes UnityEngine.Random.
    public sealed class CrumbDeathBlastEffect : MonoBehaviour
    {
        public const float Duration = 0.6f;

        public const float FlashEnd = 0.06f;
        public const float PuffEnd = 0.34f;
        public const float PuffReach = 0.56f;
        private const float PuffGrow = 0.12f;
        private const float HoleStart = 0.08f;
        public const float RingStart = 0.02f;
        public const float RingEnd = 0.36f;
        public const int DropletCount = 12;
        public const int CrumbCount = 8;
        public const float ParticleReach = 0.95f;
        public const int SmokeCount = 6;
        public const float SmokeStart = 0.06f;

        private static int nextSeed;

        private Mesh quad;
        private MeshRenderer flash, puff, ring;
        private ParticleSystem droplets, crumbs, smoke;
        private Material dropletMaterial, crumbMaterial, smokeMaterial;
        private MaterialPropertyBlock properties;
        private Transform source;
        private float radius = 1f;
        private float age;
        private int seed;
        private bool burstEmitted, smokeEmitted;
        private bool runtime;

        public float Radius => radius;
        public float RingOuterRadius { get; private set; }
        public float PuffHalfSize { get; private set; }
        public float HoleRadius { get; private set; }
        public MeshRenderer Flash => flash;
        public MeshRenderer Puff => puff;
        public MeshRenderer Ring => ring;
        public ParticleSystem Droplets => droplets;
        public ParticleSystem Crumbs => crumbs;
        public ParticleSystem Smoke => smoke;

        // owner: the blast's room; the effect is dropped as soon as it is deactivated.
        public static CrumbDeathBlastEffect Play(Vector2 center, float damageRadius, Transform owner,
            Renderer sortingSource)
        {
            if (!Application.isPlaying) return null;
            CrumbDeathBlastEffect effect = Create(center, damageRadius, sortingSource, nextSeed++);
            if (effect == null) return null;
            effect.source = owner;
            effect.runtime = true;
            effect.droplets.Play();
            effect.crumbs.Play();
            effect.smoke.Play();
            effect.EmitAt(0f);
            return effect;
        }

        public static CrumbDeathBlastEffect Create(Vector2 center, float damageRadius, Renderer sorting = null,
            int seed = 0)
        {
            Material material = CombatEffectArtwork.Material("CrumbDeathBlast");
            if (material == null) return null;
            var root = new GameObject("VFX crumb death blast");
            root.transform.position = center;
            var effect = root.AddComponent<CrumbDeathBlastEffect>();
            effect.Initialize(material, sorting, Mathf.Max(0.05f, damageRadius), seed);
            effect.RenderAt(0f);
            return effect;
        }

        private void Initialize(Material material, Renderer sorting, float damageRadius, int effectSeed)
        {
            radius = damageRadius;
            seed = effectSeed;
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Crumb death blast quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            int layer = sorting != null ? sorting.sortingLayerID : 0;
            int order = sorting != null ? sorting.sortingOrder : 0;
            // The effect-crumb-death drawing sits at +3 (FairyKingdomSpriteEffect): smoke, ring and cloud
            // under it, crumbs and droplets flying out over it, the flash on top.
            smoke = CreateParticles("Smoke", material, 5, layer, order, out smokeMaterial);
            ring = CreateQuad("Shockwave ring", material, layer, order + 1);
            puff = CreateQuad("Pink cloud", material, layer, order + 2);
            crumbs = CreateParticles("Crumbs", material, 4, layer, order + 4, out crumbMaterial);
            droplets = CreateParticles("Droplets", material, 3, layer, order + 5, out dropletMaterial);
            flash = CreateQuad("Core flash", material, layer, order + 6);
            ConfigureDroplets();
            ConfigureCrumbs();
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
            system.randomSeed = 67;
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
            instance = new Material(material) { name = "Crumb death blast " + name };
            instance.SetFloat("_Mode", mode);
            instance.SetFloat("_Seed", seed % 17);
            renderer.sharedMaterial = instance;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            return system;
        }

        // A flying start that slows; integrates to about 0.56 of the lifetime at full speed.
        private static ParticleSystem.MinMaxCurve Drag() => new(1f, new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -2.2f), new Keyframe(1f, 0.12f)));

        private void ConfigureDroplets()
        {
            var main = droplets.main;
            main.maxParticles = DropletCount;
            // Splashes arc down a little as they slow.
            main.gravityModifier = 0.18f * radius;
            var speed = droplets.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = Drag();
            var color = droplets.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = droplets.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f));
            var renderer = droplets.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.022f;
        }

        private void ConfigureCrumbs()
        {
            var main = crumbs.main;
            main.maxParticles = CrumbCount;
            main.gravityModifier = 0.24f * radius;
            var speed = crumbs.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = Drag();
            // No colour fade: the shader reads the start alpha as each crumb's shape seed. Crumbs shrink away.
            var size = crumbs.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.65f, 0.9f), new Keyframe(1f, 0f)));
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
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));
            var speed = smoke.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
        }

        // Quads are a pure function of time; particles come from EmitAt and the particle simulation.
        public void RenderAt(float seconds)
        {
            RenderFlash(seconds);
            RenderPuff(seconds);
            RenderRing(seconds);
        }

        private void RenderFlash(float seconds)
        {
            flash.enabled = seconds >= 0f && seconds < FlashEnd;
            if (!flash.enabled) return;
            float size = radius * 0.5f * (0.7f + 0.3f * EaseOut(seconds / 0.03f));
            flash.transform.localScale = new Vector3(size, size, 1f);
            properties.Clear();
            properties.SetFloat("_Mode", 0f);
            properties.SetFloat("_Opacity", 0.9f * (1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.015f, FlashEnd, seconds))));
            flash.SetPropertyBlock(properties);
        }

        private void RenderPuff(float seconds)
        {
            puff.enabled = seconds >= 0f && seconds < PuffEnd;
            if (!puff.enabled)
            {
                PuffHalfSize = 0f;
                HoleRadius = 1f;
                return;
            }
            float growth = 0.4f + 0.6f * EaseOut(seconds / PuffGrow);
            PuffHalfSize = radius * PuffReach * growth;
            puff.transform.localScale = new Vector3(PuffHalfSize, PuffHalfSize, 1f);
            float hollow = Mathf.InverseLerp(HoleStart, PuffEnd, seconds);
            HoleRadius = hollow * hollow * 1.2f;
            properties.Clear();
            properties.SetFloat("_Mode", 1f);
            properties.SetFloat("_Age", seconds / PuffEnd);
            properties.SetFloat("_Hole", HoleRadius);
            properties.SetFloat("_Seed", seed % 17);
            properties.SetFloat("_Opacity", 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(PuffEnd - 0.08f, PuffEnd, seconds)));
            puff.SetPropertyBlock(properties);
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
            float outer = Mathf.Lerp(0.35f, 1f, 1f - Mathf.Pow(1f - progress, 3f));
            float thickness = Mathf.Lerp(0.08f, 0.02f, progress);
            RingOuterRadius = outer * radius;
            // The quad is exactly the damage circle; the band's outer edge sits at `outer` inside it.
            ring.transform.localScale = new Vector3(radius, radius, 1f);
            properties.Clear();
            properties.SetFloat("_Mode", 2f);
            properties.SetFloat("_Ring", outer - thickness);
            properties.SetFloat("_Thickness", thickness);
            properties.SetFloat("_Erode", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, progress)) * 0.9f);
            properties.SetFloat("_Seed", seed % 17);
            properties.SetFloat("_Opacity", 0.95f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress))));
            ring.SetPropertyBlock(properties);
        }

        // Shared by Play and the deterministic Editor resimulation.
        public void EmitAt(float seconds)
        {
            if (!burstEmitted && seconds >= 0f)
            {
                burstEmitted = true;
                EmitDroplets();
                EmitCrumbs();
            }
            if (!smokeEmitted && seconds >= SmokeStart)
            {
                smokeEmitted = true;
                EmitSmoke();
            }
        }

        private void EmitDroplets()
        {
            Vector2 center = transform.position;
            float scale = radius / 1.2f;
            for (int i = 0; i < DropletCount; i++)
            {
                float angle = (i + Hash(i, 1) * 0.8f) * (360f / DropletCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float lifetime = 0.32f + Hash(i, 2) * 0.2f;
                float start = radius * 0.15f;
                // Gravity pulls a downward droplet further, so the planned travel leaves room for it.
                float travel = radius * 0.8f * Mathf.Lerp(0.65f, 1f, Hash(i, 3)) - start;
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * start,
                    velocity = direction * (travel / (lifetime * 0.56f)),
                    startLifetime = lifetime,
                    startSize = (0.14f + Hash(i, 4) * 0.08f) * scale,
                    startColor = Color.Lerp(new Color(1f, 0.72f, 0.82f), new Color(0.96f, 0.56f, 0.72f), Hash(i, 5))
                };
                droplets.Emit(particle, 1);
            }
        }

        private void EmitCrumbs()
        {
            Vector2 center = transform.position;
            float scale = radius / 1.2f;
            for (int i = 0; i < CrumbCount; i++)
            {
                float angle = (i + 0.5f + Hash(i, 13) * 0.7f) * (360f / CrumbCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float lifetime = 0.38f + Hash(i, 14) * 0.2f;
                float start = radius * 0.12f;
                float travel = radius * 0.74f * Mathf.Lerp(0.6f, 1f, Hash(i, 15)) - start;
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * start,
                    velocity = direction * (travel / (lifetime * 0.56f)),
                    startLifetime = lifetime,
                    startSize = (0.13f + Hash(i, 16) * 0.08f) * scale,
                    rotation = Hash(i, 17) * 360f,
                    angularVelocity = (Hash(i, 18) - 0.5f) * 720f,
                    // Alpha is the shape seed, not opacity.
                    startColor = new Color(1f, 0.88f, 0.55f, 0.2f + Hash(i, 19) * 0.8f)
                };
                crumbs.Emit(particle, 1);
            }
        }

        private void EmitSmoke()
        {
            Vector2 center = transform.position;
            float scale = radius / 1.2f;
            for (int i = 0; i < SmokeCount; i++)
            {
                float angle = (i + Hash(i, 6) * 0.6f) * (360f / SmokeCount) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float tint = Hash(i, 7);
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + direction * (radius * (0.15f + Hash(i, 8) * 0.15f)),
                    velocity = direction * (0.45f + Hash(i, 9) * 0.45f) * scale + Vector2.up * 0.3f * scale,
                    startLifetime = Duration - SmokeStart - 0.01f - Hash(i, 10) * 0.08f,
                    startSize = (0.36f + Hash(i, 11) * 0.2f) * scale,
                    rotation = Hash(i, 12) * 360f,
                    startColor = new Color(1f, Mathf.Lerp(0.7f, 0.8f, tint), Mathf.Lerp(0.8f, 0.87f, tint), 0.4f)
                };
                smoke.Emit(particle, 1);
            }
        }

        // Editor preview and render probe: replay particles from the fixed seed so scrubbing back leaves none stale.
        public void SimulateTo(float seconds)
        {
            ParticleSystem[] systems = { droplets, crumbs, smoke };
            foreach (ParticleSystem system in systems)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Restart first: the first Simulate on a stopped system would otherwise drop the burst.
                system.Simulate(0f, false, true, false);
            }
            burstEmitted = smokeEmitted = false;
            const float step = 1f / 60f;
            float simulated = 0f;
            EmitAt(0f);
            while (simulated < seconds)
            {
                float dt = Mathf.Min(step, seconds - simulated);
                foreach (ParticleSystem system in systems) system.Simulate(dt, false, false, false);
                simulated += dt;
                EmitAt(simulated);
            }
            RenderAt(seconds);
        }

        public void SetPreviewLayers(bool showFlash, bool showPuff, bool showRing, bool showDroplets,
            bool showCrumbs, bool showSmoke)
        {
            flash.enabled &= showFlash;
            puff.enabled &= showPuff;
            ring.enabled &= showRing;
            droplets.GetComponent<ParticleSystemRenderer>().enabled = showDroplets;
            crumbs.GetComponent<ParticleSystemRenderer>().enabled = showCrumbs;
            smoke.GetComponent<ParticleSystemRenderer>().enabled = showSmoke;
        }

        private void Update()
        {
            if (!runtime) return;
            // A room left behind or removed takes its leftover burst with it.
            if (source != null && !source.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
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
            CombatEffectArtwork.Release(dropletMaterial);
            CombatEffectArtwork.Release(crumbMaterial);
            CombatEffectArtwork.Release(smokeMaterial);
        }
    }
}
