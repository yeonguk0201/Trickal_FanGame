using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Visual only: the 광기의 가면 damage aura (PlayerDamageAura) as a pale red field on the floor around the player.
    // Its edge wobbles like disturbed water but stays inside the damage radius, faint ripples flow outward, and a
    // brighter ring runs out on every damage tick. Damage, radius and tick timing stay in PlayerDamageAura.
    // Deterministic: never consumes UnityEngine.Random.
    [DisallowMultipleComponent]
    public sealed class DamageAuraField : MonoBehaviour
    {
        public const string ObjectName = "VFX damage aura field";
        // Over the room floor (-100) and scorch marks (-60), under ground shadows (-50) and every body.
        public const int SortingOrder = -55;
        public const float FadeSeconds = 0.25f;
        public const float PulseDuration = 0.8f;
        public const float Wobble = 0.09f;
        public const int MaxStacks = 3;

        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int EnergyId = Shader.PropertyToID("_Energy");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int TickSeedId = Shader.PropertyToID("_TickSeed");
        private static readonly int LayersId = Shader.PropertyToID("_Layers");

        private Mesh quad;
        private MeshRenderer field;
        private MaterialPropertyBlock properties;
        private Vector4 layers = Vector4.one;

        public MeshRenderer Field => field;
        public float DrawnRadius { get; private set; }
        public float Opacity { get; private set; }
        public float PulseProgress { get; private set; } = -1f;

        // Also the Editor preview and verifier entry point: nothing shows until RenderAt is called.
        public static DamageAuraField Create(Transform owner, int sortingLayerId)
        {
            if (owner == null) return null;
            Material material = CombatEffectArtwork.Material("DamageAura");
            if (material == null) return null;
            Transform existing = owner.Find(ObjectName);
            DamageAuraField aura = existing != null ? existing.GetComponent<DamageAuraField>() : null;
            if (aura == null)
            {
                var host = new GameObject(ObjectName);
                host.transform.SetParent(owner, false);
                aura = host.AddComponent<DamageAuraField>();
            }
            if (aura.field == null) aura.Initialize(material);
            aura.field.sortingLayerID = sortingLayerId;
            return aura;
        }

        private void Initialize(Material material)
        {
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "Damage aura quad" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(-1, 1), new Vector3(1, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
            field = gameObject.AddComponent<MeshRenderer>();
            field.sharedMaterial = material;
            field.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            field.receiveShadows = false;
            field.sortingOrder = SortingOrder;
            field.enabled = false;
        }

        // pulseAge: seconds since the last damage tick (negative or large: no ring).
        public void RenderAt(float time, float radius, int stacks, float opacity, float pulseAge)
        {
            if (field == null) return;
            Opacity = radius > 0f ? Mathf.Clamp01(opacity) : 0f;
            field.enabled = Opacity > 0.001f;
            DrawnRadius = 0f;
            PulseProgress = -1f;
            if (!field.enabled) return;

            // A world-space circle of the damage radius whatever the owner's scale or flip.
            Transform owner = transform.parent;
            Vector3 parentScale = owner != null ? owner.lossyScale : Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(radius / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                radius / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            if (owner != null) transform.rotation = Quaternion.identity;
            DrawnRadius = radius;

            if (pulseAge >= 0f && pulseAge < PulseDuration) PulseProgress = pulseAge / PulseDuration;
            float energy = Mathf.Clamp01((stacks - 1f) / (MaxStacks - 1f));
            properties.Clear();
            properties.SetFloat(ClockId, time);
            properties.SetFloat(OpacityId, Opacity);
            properties.SetFloat(WobbleId, Wobble);
            properties.SetFloat(EnergyId, energy);
            properties.SetFloat(PulseId, PulseProgress);
            // The tick's start time keeps one brush shape for the whole sweep and a new one every tick.
            properties.SetFloat(TickSeedId, PulseProgress >= 0f ? Mathf.Repeat(time - pulseAge, 97f) : 0f);
            properties.SetVector(LayersId, layers);
            field.SetPropertyBlock(properties);
        }

        public void SetPreviewLayers(bool fill, bool ripples, bool rim, bool tickRing)
        {
            layers = new Vector4(fill ? 1 : 0, ripples ? 1 : 0, rim ? 1 : 0, tickRing ? 1 : 0);
        }

        public void Hide()
        {
            if (field != null) field.enabled = false;
            Opacity = 0f;
            DrawnRadius = 0f;
        }

        public void Stop() => CombatEffectArtwork.Release(gameObject);

        private void OnDestroy() => CombatEffectArtwork.Release(quad);
    }
}
