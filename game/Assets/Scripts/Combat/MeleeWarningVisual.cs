using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Floor telegraph for a short melee hit. The outlined area fills up until the hit lands and flashes when the
    // direction stops following the target. The bound line stays the source of the area and is not drawn itself.
    [DefaultExecutionOrder(10000)]
    public sealed class MeleeWarningVisual : MonoBehaviour
    {
        private const float LockFlashSeconds = 0.14f;
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int StateId = Shader.PropertyToID("_State");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static Sprite unitSprite;

        private LineRenderer path;
        private SpriteRenderer visual;
        private MaterialPropertyBlock block;
        private float cornerRadius;
        private float nearFade;
        private int orderOffset;
        private float startsAt;
        private float endsAt;
        private float lockedAt;
        private bool locked;

        // cornerRadius rounds the area, nearFade thins the end next to the attacker; both are world units.
        public static MeleeWarningVisual Bind(LineRenderer line, float cornerRadius, float nearFade, int orderOffset)
        {
            if (!Application.isPlaying || line == null) return null;
            var warning = line.GetComponent<MeleeWarningVisual>();
            if (warning == null) warning = line.gameObject.AddComponent<MeleeWarningVisual>();
            warning.path = line;
            warning.cornerRadius = Mathf.Max(0f, cornerRadius);
            warning.nearFade = Mathf.Max(0f, nearFade);
            warning.orderOffset = orderOffset;
            line.forceRenderingOff = true;
            return warning;
        }

        public void SetTiming(float remaining, float duration, bool directionLocked)
        {
            endsAt = Time.time + Mathf.Max(0f, remaining);
            startsAt = endsAt - Mathf.Max(0.01f, duration);
            if (directionLocked && !locked) lockedAt = Time.time;
            locked = directionLocked;
        }

        private void LateUpdate()
        {
            if (path == null || !path.enabled || path.positionCount < 2)
            { if (visual != null) visual.enabled = false; return; }
            if (visual == null)
            {
                Material material = CombatEffectArtwork.Material("MeleeWarning");
                if (material == null) { path.forceRenderingOff = false; return; }
                var owner = new GameObject("VFX melee warning");
                owner.transform.SetParent(transform, false);
                visual = CombatEffectArtwork.Renderer(owner, UnitSprite(), path, orderOffset);
                visual.sharedMaterial = material;
                block = new MaterialPropertyBlock();
            }
            Vector3 start = path.GetPosition(0), end = path.GetPosition(1);
            Vector3 delta = end - start;
            float length = delta.magnitude, width = path.startWidth;
            visual.enabled = length > 0.03f && width > 0.03f;
            if (!visual.enabled) return;
            visual.transform.SetPositionAndRotation((start + end) * 0.5f,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
            CombatEffectArtwork.Size(visual, length, width);
            float flash = locked ? Mathf.Clamp01(1f - (Time.time - lockedAt) / LockFlashSeconds) : 0f;
            visual.GetPropertyBlock(block);
            block.SetVector(ShapeId, new Vector4(length, width,
                Mathf.Min(cornerRadius, Mathf.Min(length, width) * 0.5f), nearFade));
            block.SetVector(StateId, new Vector4(Mathf.InverseLerp(startsAt, endsAt, Time.time), flash));
            block.SetColor(TintId, path.startColor);
            visual.SetPropertyBlock(block);
        }

        private void OnEnable()
        {
            if (path != null) path.forceRenderingOff = true;
        }

        private void OnDisable()
        {
            if (visual != null) visual.enabled = false;
            if (path != null) path.forceRenderingOff = false;
        }

        private static Sprite UnitSprite()
        {
            if (unitSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                unitSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    Vector2.one * 0.5f, texture.width, 0, SpriteMeshType.FullRect);
                unitSprite.name = "melee-warning-quad";
            }
            return unitSprite;
        }
    }
}
