using UnityEngine;

namespace TrickalFanGame.Combat
{
    public enum PlayerRecoveryKind { Heal, SP }

    /// <summary>
    /// The player's heal and SP pickup effects, made in Unity (2026-11-24).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRecoveryEffect : MonoBehaviour
    {
        public const string HealShaderName = "HealingAura";
        public const string SpShaderName = "SpAbsorb";
        public const float HealDuration = 1.1f;
        public const float SpDuration = 0.8f;
        private const float HealPulseSeconds = 0.45f;
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static Mesh quad;

        private PlayerRecoveryKind kind;
        private Transform player;
        private SpriteRenderer visual;
        private MeshRenderer body;
        private MaterialPropertyBlock properties;
        private float elapsed;
        private float seed;

        public static PlayerRecoveryEffect Play(PlayerRecoveryKind kind, Transform player, SpriteRenderer visual)
        {
            if (!Application.isPlaying || player == null || visual == null) return null;
            foreach (PlayerRecoveryEffect running in player.GetComponentsInChildren<PlayerRecoveryEffect>())
            {
                if (running.kind != kind) continue;
                running.elapsed = 0f;
                return running;
            }

            Material material = CombatEffectArtwork.Material(kind == PlayerRecoveryKind.Heal ? HealShaderName : SpShaderName);
            if (material == null) return null;
            GameObject owner = new(kind == PlayerRecoveryKind.Heal ? "VFX player heal (Unity)" : "VFX player SP (Unity)")
            {
                layer = player.gameObject.layer,
            };
            owner.transform.SetParent(player, false);
            PlayerRecoveryEffect effect = owner.AddComponent<PlayerRecoveryEffect>();
            effect.kind = kind;
            effect.player = player;
            effect.visual = visual;
            effect.properties = new MaterialPropertyBlock();
            effect.seed = Random.value;
            effect.body = effect.CreateQuad("Body", material);
            effect.Evaluate(0f, Time.time);
            return effect;
        }

        private void LateUpdate()
        {
            if (player == null || visual == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= (kind == PlayerRecoveryKind.Heal ? HealDuration : SpDuration))
            {
                Destroy(gameObject);
                return;
            }

            Evaluate(elapsed, Time.time);
        }

        // Public so a preview can show any moment of the effect.
        public void Evaluate(float time, float clock)
        {
            Bounds bounds = visual.bounds;
            if (kind == PlayerRecoveryKind.Heal) EvaluateHeal(bounds, time, clock);
            else EvaluateSp(bounds, time);
        }

        private void EvaluateHeal(Bounds bounds, float time, float clock)
        {
            float opacity = Mathf.Clamp01(time / 0.1f) * (1f - Mathf.SmoothStep(0f, 1f, (time - 0.7f) / (HealDuration - 0.7f)));
            float pulse = Mathf.Clamp01(1f - time / HealPulseSeconds);
            Place(body, bounds.center, new Vector2(Mathf.Max(1.25f, bounds.size.x * 1.5f),
                Mathf.Max(1.5f, bounds.size.y * 1.25f)));
            Sort(body, 2);
            ApplyHeal(body, 2f, clock, opacity, pulse);
        }

        private void EvaluateSp(Bounds bounds, float time)
        {
            float progress = Mathf.Clamp01(time / SpDuration);
            float size = Mathf.Clamp(bounds.size.y * 1.9f, 1.7f, 2.6f);
            Place(body, bounds.center, Vector2.one * size);
            Sort(body, 2);
            body.GetPropertyBlock(properties);
            properties.SetFloat(ProgressId, progress);
            properties.SetFloat(OpacityId, 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.15f));
            properties.SetFloat(SeedId, seed);
            body.SetPropertyBlock(properties);
        }

        private void ApplyHeal(Renderer target, float mode, float clock, float opacity, float pulse)
        {
            target.GetPropertyBlock(properties);
            properties.SetFloat(ModeId, mode);
            properties.SetFloat(ClockId, clock);
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(PulseId, pulse);
            properties.SetFloat(LengthId, 1f);
            properties.SetFloat(SeedId, seed);
            target.SetPropertyBlock(properties);
        }

        private void Sort(Renderer target, int offset)
        {
            target.sortingLayerID = visual.sortingLayerID;
            target.sortingOrder = visual.sortingOrder + offset;
        }

        private void Place(Renderer target, Vector2 position, Vector2 size)
        {
            Transform item = target.transform;
            item.SetPositionAndRotation(new Vector3(position.x, position.y, player.position.z), Quaternion.identity);
            Vector3 parent = transform.lossyScale;
            item.localScale = new Vector3(size.x / Mathf.Max(0.001f, Mathf.Abs(parent.x)),
                size.y / Mathf.Max(0.001f, Mathf.Abs(parent.y)), 1f);
        }

        private MeshRenderer CreateQuad(string quadName, Material material)
        {
            GameObject child = new(quadName) { layer = gameObject.layer };
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = SharedQuad();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Mesh SharedQuad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "Player recovery quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
