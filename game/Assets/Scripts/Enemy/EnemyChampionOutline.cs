using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-1 (docs/28 §8): a champion's silhouette outline (3 screen pixels) with a weak glow (6 screen pixels,
    // softly pulsing) in its property's colour. Copies of whichever renderer shows the body this frame are drawn
    // behind it with the ChampionOutline shader, offset in eight directions by a fixed number of screen pixels, so
    // the ring keeps its width at every zoom and body size. The visible body is the enemy's sprite, or the movement
    // mesh that hides it (hop and 산사모-순수 walk), whose deformed mesh the copies share.
    [DefaultExecutionOrder(10001)]
    [DisallowMultipleComponent]
    public sealed class EnemyChampionOutline : MonoBehaviour
    {
        public const string CopyPrefix = "Champion Outline";
        public const string ShaderName = "ChampionOutline";
        public const float OutlinePixels = 3f;
        public const float GlowPixels = 6f;
        public const float GlowAlpha = 0.3f;
        public const float GlowPulsePeriod = 1.2f;
        private const float FallbackWorldPerPixel = 0.01f;
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static readonly Vector2[] Directions =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized, new Vector2(-1f, -1f).normalized,
        };

        // Eight outline copies, then eight glow copies.
        public static int CopyCount => Directions.Length * 2;

        [SerializeField] private Color color = Color.white;

        private readonly List<MeshRenderer> meshCandidates = new();
        private Health health;
        private SpriteRenderer source;
        private SpriteRenderer[] spriteCopies;
        private MeshRenderer[] meshCopies;
        private MeshFilter[] meshCopyFilters;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock bodyProperties;

        public Color Color => color;
        // The renderer the outline followed on the last refresh; null when hidden.
        public Renderer CurrentBody { get; private set; }
        public int VisibleCopyCount { get; private set; }
        public float LastWorldPerPixel { get; private set; }
        public IReadOnlyList<SpriteRenderer> SpriteCopies => spriteCopies;
        public IReadOnlyList<MeshRenderer> MeshCopies => meshCopies;

        public void Configure(Color configuredColor)
        {
            color = configuredColor;
        }

        private void LateUpdate()
        {
            Refresh(Camera.main, Time.time);
        }

        // Public so Editor verification can render one frame without a running Game view.
        public void Refresh(Camera view, float now)
        {
            if (health == null) health = GetComponent<Health>();
            if (source == null) source = ResolveSource();
            Material material = CombatEffectArtwork.Material(ShaderName);
            Renderer body = health != null && health.IsDead ? null : ResolveBody();
            CurrentBody = material != null ? body : null;
            VisibleCopyCount = 0;
            LastWorldPerPixel = WorldPerPixel(view);
            if (CurrentBody is SpriteRenderer sprite) ShowSpriteCopies(sprite, material, now);
            else SetEnabled(spriteCopies, false);
            if (CurrentBody is MeshRenderer mesh) ShowMeshCopies(mesh, material, now);
            else SetEnabled(meshCopies, false);
        }

        public static float WorldPerPixel(Camera view) =>
            view != null && view.orthographic && view.pixelHeight > 0
                ? view.orthographicSize * 2f / view.pixelHeight
                : FallbackWorldPerPixel;

        private SpriteRenderer ResolveSource()
        {
            TrickalFanGame.Frontend.FairyKingdomArtworkView artwork =
                GetComponent<TrickalFanGame.Frontend.FairyKingdomArtworkView>();
            if (artwork != null && artwork.Visual != null) return artwork.Visual;
            if (TryGetComponent(out SpriteRenderer own)) return own;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
                if (!candidate.name.StartsWith(CopyPrefix)) return candidate;
            return null;
        }

        private Renderer ResolveBody()
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) return null;
            if (!source.forceRenderingOff) return source.sprite != null ? source : null;
            // A movement mesh drawn in the sprite's place, at the same sorting position.
            GetComponentsInChildren(false, meshCandidates);
            foreach (MeshRenderer candidate in meshCandidates)
            {
                if (!candidate.enabled || candidate.name.StartsWith(CopyPrefix) ||
                    candidate.sortingLayerID != source.sortingLayerID ||
                    candidate.sortingOrder != source.sortingOrder ||
                    !candidate.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;
                return candidate;
            }

            return null;
        }

        private void ShowSpriteCopies(SpriteRenderer body, Material material, float now)
        {
            spriteCopies ??= new SpriteRenderer[CopyCount];
            for (int index = 0; index < CopyCount; index++)
            {
                SpriteRenderer copy = spriteCopies[index];
                if (copy == null)
                {
                    copy = spriteCopies[index] = CreateCopy(index, body.transform).AddComponent<SpriteRenderer>();
                    copy.sharedMaterial = material;
                }

                Place(copy.transform, body.transform, index);
                copy.sprite = body.sprite;
                copy.flipX = body.flipX;
                copy.flipY = body.flipY;
                copy.drawMode = body.drawMode;
                copy.size = body.size;
                Style(copy, body, body.color.a, now, index, null);
            }
        }

        private void ShowMeshCopies(MeshRenderer body, Material material, float now)
        {
            meshCopies ??= new MeshRenderer[CopyCount];
            meshCopyFilters ??= new MeshFilter[CopyCount];
            Mesh mesh = body.GetComponent<MeshFilter>().sharedMesh;
            bodyProperties ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(bodyProperties);
            Texture texture = bodyProperties.GetTexture(MainTexId);
            if (texture == null && body.sharedMaterial != null && body.sharedMaterial.HasProperty(MainTexId))
                texture = body.sharedMaterial.GetTexture(MainTexId);
            for (int index = 0; index < CopyCount; index++)
            {
                MeshRenderer copy = meshCopies[index];
                if (copy == null)
                {
                    GameObject owner = CreateCopy(index, body.transform);
                    meshCopyFilters[index] = owner.AddComponent<MeshFilter>();
                    copy = meshCopies[index] = owner.AddComponent<MeshRenderer>();
                    copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    copy.receiveShadows = false;
                    copy.sharedMaterial = material;
                }

                Place(copy.transform, body.transform, index);
                meshCopyFilters[index].sharedMesh = mesh;
                Style(copy, body, source != null ? source.color.a : 1f, now, index, texture);
            }
        }

        private GameObject CreateCopy(int index, Transform parent)
        {
            GameObject owner = new($"{CopyPrefix} {(index < Directions.Length ? "Line" : "Glow")} {index}")
            {
                layer = parent.gameObject.layer,
            };
            owner.transform.SetParent(parent, false);
            return owner;
        }

        private void Place(Transform copy, Transform body, int index)
        {
            if (copy.parent != body) copy.SetParent(body, false);
            bool glow = index >= Directions.Length;
            Vector2 world = Directions[index % Directions.Length] *
                            ((glow ? GlowPixels : OutlinePixels) * LastWorldPerPixel);
            Vector3 local = body.InverseTransformVector(world);
            local.z = 0f;
            copy.localPosition = local;
            copy.localRotation = Quaternion.identity;
            copy.localScale = Vector3.one;
        }

        private void Style(Renderer copy, Renderer body, float bodyAlpha, float now, int index, Texture texture)
        {
            bool glow = index >= Directions.Length;
            float alpha = glow
                ? GlowAlpha * (0.75f + 0.25f * Mathf.Sin(now * Mathf.PI * 2f / GlowPulsePeriod))
                : color.a;
            properties ??= new MaterialPropertyBlock();
            copy.GetPropertyBlock(properties);
            properties.SetColor(OutlineColorId, new Color(color.r, color.g, color.b, alpha * bodyAlpha));
            if (texture != null) properties.SetTexture(MainTexId, texture);
            copy.SetPropertyBlock(properties);
            copy.sortingLayerID = body.sortingLayerID;
            // Behind the body; the glow behind the line.
            copy.sortingOrder = body.sortingOrder - (glow ? 2 : 1);
            copy.enabled = true;
            VisibleCopyCount++;
        }

        private static void SetEnabled<T>(T[] copies, bool enabled) where T : Renderer
        {
            if (copies == null) return;
            foreach (T copy in copies)
                if (copy != null) copy.enabled = enabled;
        }

        private void OnDisable()
        {
            SetEnabled(spriteCopies, false);
            SetEnabled(meshCopies, false);
            VisibleCopyCount = 0;
            CurrentBody = null;
        }

        private void OnDestroy()
        {
            Release(spriteCopies);
            Release(meshCopies);
        }

        private static void Release<T>(T[] copies) where T : Renderer
        {
            if (copies == null) return;
            foreach (T copy in copies)
                if (copy != null) CombatEffectArtwork.Release(copy.gameObject);
        }
    }
}
