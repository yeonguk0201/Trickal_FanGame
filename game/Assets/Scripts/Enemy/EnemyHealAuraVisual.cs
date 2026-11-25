using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// 산사모-우울's heal (2026-11-24), presentation only: a glowing field on the floor at the real heal radius, a link
    /// of flowing light to each healed ally, and a glow with rising crosses on those allies. Each is its own layer.
    /// </summary>
    public sealed class EnemyHealAuraVisual
    {
        public const string ShaderName = "HealingAura";
        private const int LinkPoints = 14;
        private const float FadeSeconds = 0.2f;
        private const float PulseSeconds = 0.4f;
        private const float LinkWidth = 0.2f;
        private const float LinkSway = 0.07f;
        private static readonly Vector2 ReceiveSize = new(1.5f, 1.8f);
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static Mesh quad;

        private readonly Transform owner;
        private readonly SpriteRenderer body;
        private readonly MaterialPropertyBlock properties = new();
        private readonly Transform[] targets = new Transform[EnemyBurrowHealerController.MaximumLinks];
        private readonly LineRenderer[] links = new LineRenderer[EnemyBurrowHealerController.MaximumLinks];
        private readonly MeshRenderer[] receivers = new MeshRenderer[EnemyBurrowHealerController.MaximumLinks];
        private MeshRenderer field;
        private float opacity;
        private float pulse;

        public EnemyHealAuraVisual(Transform owner, SpriteRenderer body)
        {
            this.owner = owner;
            this.body = body;
        }

        public void Pulse() => pulse = 1f;

        public void Render(Vector2 center, float radius, IReadOnlyList<Health> healed, bool active, float deltaTime,
            float clock)
        {
            Material material = CombatEffectArtwork.Material(ShaderName);
            if (material == null || owner == null) return;
            opacity = Mathf.MoveTowards(opacity, active ? 1f : 0f, deltaTime / FadeSeconds);
            pulse = Mathf.MoveTowards(pulse, 0f, deltaTime / PulseSeconds);
            if (active)
                for (int i = 0; i < targets.Length; i++)
                    targets[i] = i < healed.Count && healed[i] != null && !healed[i].IsDead ? healed[i].transform : null;

            bool visible = opacity > 0f;
            if (field == null && visible) field = CreateQuad("Healer Aura Field", material);
            if (field != null)
            {
                field.enabled = visible;
                if (visible)
                {
                    Place(field.transform, center, Vector2.one * (radius * 2f));
                    field.sortingLayerID = body.sortingLayerID;
                    field.sortingOrder = body.sortingOrder - 2;
                    Apply(field, 0f, clock, opacity * 0.95f, 1f, 0f);
                }
            }

            for (int i = 0; i < targets.Length; i++) RenderAlly(i, center, material, clock, visible);
        }

        private void RenderAlly(int index, Vector2 center, Material material, float clock, bool visible)
        {
            Transform target = targets[index];
            bool show = visible && target != null;
            if (links[index] == null && show) links[index] = CreateLine(material);
            if (receivers[index] == null && show) receivers[index] = CreateQuad("Healer Aura Receive", material);
            if (links[index] != null) links[index].enabled = show;
            if (receivers[index] != null) receivers[index].enabled = show;
            if (!show) return;

            float seed = index * 0.27f;
            Vector2 end = target.position;
            LineRenderer link = links[index];
            link.sortingLayerID = body.sortingLayerID;
            link.sortingOrder = body.sortingOrder - 1;
            Vector2 along = end - center;
            Vector2 across = along.sqrMagnitude > 0.0001f ? new Vector2(-along.y, along.x).normalized : Vector2.up;
            link.positionCount = LinkPoints;
            for (int i = 0; i < LinkPoints; i++)
            {
                float t = i / (LinkPoints - 1f);
                float sway = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * 7f - clock * 2.6f + seed * 6f) * LinkSway;
                link.SetPosition(i, center + along * t + across * sway);
            }

            Apply(link, 1f, clock, opacity, along.magnitude, seed);

            MeshRenderer receiver = receivers[index];
            SpriteRenderer allySprite = target.GetComponent<SpriteRenderer>();
            Place(receiver.transform, end, ReceiveSize);
            receiver.sortingLayerID = allySprite != null ? allySprite.sortingLayerID : body.sortingLayerID;
            receiver.sortingOrder = (allySprite != null ? allySprite.sortingOrder : body.sortingOrder) + 1;
            Apply(receiver, 2f, clock, opacity, 1f, seed);
        }

        private void Apply(Renderer renderer, float mode, float clock, float alpha, float length, float seed)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(ModeId, mode);
            properties.SetFloat(ClockId, clock);
            properties.SetFloat(OpacityId, alpha);
            properties.SetFloat(PulseId, pulse);
            properties.SetFloat(LengthId, length);
            properties.SetFloat(SeedId, seed);
            renderer.SetPropertyBlock(properties);
        }

        // World size and position, whatever the healer's own scale (elites are larger).
        private void Place(Transform item, Vector2 position, Vector2 size)
        {
            item.SetPositionAndRotation(new Vector3(position.x, position.y, owner.position.z), Quaternion.identity);
            Vector3 parent = owner.lossyScale;
            item.localScale = new Vector3(size.x / Mathf.Max(0.001f, Mathf.Abs(parent.x)),
                size.y / Mathf.Max(0.001f, Mathf.Abs(parent.y)), 1f);
        }

        private MeshRenderer CreateQuad(string name, Material material)
        {
            GameObject child = new(name) { layer = owner.gameObject.layer };
            child.transform.SetParent(owner, false);
            child.AddComponent<MeshFilter>().sharedMesh = SharedQuad();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            return renderer;
        }

        private LineRenderer CreateLine(Material material)
        {
            GameObject child = new("Healer Aura Link") { layer = owner.gameObject.layer };
            child.transform.SetParent(owner, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.startWidth = line.endWidth = LinkWidth;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            return line;
        }

        private static Mesh SharedQuad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "Healing aura quad", hideFlags = HideFlags.HideAndDontSave };
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
