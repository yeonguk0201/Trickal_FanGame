using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Effect-0 (docs/28): visual only. The dust kicked up where something heavy lands, such as 부스러기's thrown
    // obstacles. Soft puffs burst sideways along the ground from the landing base, then slow, swell and fade.
    // Puffs on the far side are drawn behind what landed and the near side in front, so it sits in the dust.
    // Uses the MuffinExplosion shader's smoke mode. Deterministic: never consumes UnityEngine.Random.
    public sealed class LandingDustEffect : MonoBehaviour
    {
        public const float Duration = 0.75f;
        public const int PuffCount = 14;
        // Ground is seen from above at an angle, so sideways spread is flattened vertically.
        public const float GroundFlattening = 0.38f;
        public static readonly Color DustColor = new(0.86f, 0.78f, 0.64f, 0.85f);

        private static int nextSeed;

        private ParticleSystem back;
        private ParticleSystem front;
        private Material backMaterial;
        private Material frontMaterial;
        private Transform source;
        private Vector3 sourcePosition;
        private float width = 1f;
        private float age;
        private int seed;

        public ParticleSystem Back => back;
        public ParticleSystem Front => front;
        public float Width => width;

        // baseCenter: the ground point under what landed. width: how wide its base is.
        // owner: followed only to drop the effect on a room teleport.
        public static LandingDustEffect Play(Vector2 baseCenter, float width, Transform owner, Renderer sortingSource)
        {
            if (!Application.isPlaying) return null;
            Material material = CombatEffectArtwork.Material("MuffinExplosion");
            if (material == null) return null;
            var root = new GameObject("VFX landing dust");
            root.transform.position = baseCenter;
            var effect = root.AddComponent<LandingDustEffect>();
            effect.Initialize(material, sortingSource, Mathf.Max(0.2f, width), nextSeed++);
            effect.source = owner;
            effect.sourcePosition = owner != null ? owner.position : Vector3.zero;
            effect.back.Play();
            effect.front.Play();
            effect.Emit();
            return effect;
        }

        private void Initialize(Material material, Renderer sorting, float baseWidth, int effectSeed)
        {
            width = baseWidth;
            seed = effectSeed;
            int layer = sorting != null ? sorting.sortingLayerID : 0;
            int order = sorting != null ? sorting.sortingOrder : 0;
            back = CreatePuffs("Dust behind", material, layer, order - 1, out backMaterial);
            front = CreatePuffs("Dust in front", material, layer, order + 1, out frontMaterial);
        }

        private ParticleSystem CreatePuffs(string name, Material material, int layer, int order, out Material instance)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(transform, false);
            var system = owner.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = 71;
            var main = system.main;
            main.duration = Duration;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = PuffCount;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;

            // A hard outward kick that stalls quickly, like dust pushed out from under a heavy landing.
            var speed = system.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -5f), new Keyframe(0.35f, 0.18f, -0.5f, -0.1f), new Keyframe(1f, 0.05f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.55f, 0.6f),
                    new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.35f));

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            instance = new Material(material) { name = "Landing dust " + name };
            instance.SetFloat("_Mode", 4f);
            instance.SetFloat("_Seed", seed % 17);
            renderer.sharedMaterial = instance;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            return system;
        }

        private void Emit()
        {
            Vector2 center = transform.position;
            float scale = width / 1.4f;
            for (int i = 0; i < PuffCount; i++)
            {
                float angle = (i + Hash(i, 1) * 0.7f) * (360f / PuffCount) * Mathf.Deg2Rad;
                Vector2 around = new(Mathf.Cos(angle), Mathf.Sin(angle) * GroundFlattening);
                // Sideways puffs are pushed hardest: dust squeezes out from the left and right of the base.
                float push = Mathf.Lerp(0.75f, 1.15f, Mathf.Abs(Mathf.Cos(angle))) * Mathf.Lerp(0.8f, 1.2f, Hash(i, 2));
                float shade = Mathf.Lerp(0.92f, 1.04f, Hash(i, 3));
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + around * (width * 0.42f),
                    velocity = around * (3.4f * push * scale) + Vector2.up * (0.35f + Hash(i, 4) * 0.35f) * scale,
                    startLifetime = Duration - Hash(i, 5) * 0.25f,
                    startSize = (0.34f + Hash(i, 6) * 0.2f) * scale,
                    rotation = Hash(i, 7) * 360f,
                    startColor = new Color(DustColor.r * shade, DustColor.g * shade, DustColor.b * shade, DustColor.a),
                };
                (Mathf.Sin(angle) > 0f ? back : front).Emit(particle, 1);
            }
        }

        private void Update()
        {
            if (source != null)
            {
                if (!source.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
                // A room teleport must not carry old dust into the next room.
                if ((source.position - sourcePosition).sqrMagnitude > 16f) { Destroy(gameObject); return; }
                sourcePosition = source.position;
            }
            age += Time.deltaTime;
            if (age >= Duration) Destroy(gameObject);
        }

        private float Hash(int index, int channel)
        {
            float value = Mathf.Sin(index * 73.17f + channel * 19.31f + seed * 41.73f) * 437.13f;
            return value - Mathf.Floor(value);
        }

        private void OnDestroy()
        {
            CombatEffectArtwork.Release(backMaterial);
            CombatEffectArtwork.Release(frontMaterial);
        }
    }
}
