using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Survives the obstacle's immediate deactivation; never carries a collider or gameplay damage.
    // Cells 3, 4, 5, 6 play once, the last cell rests on the floor for a moment, then the remnants and their ground
    // shadow fade out together (2026-11-20: the remnants used to vanish 0.1s after the last cell appeared).
    public sealed class SaemaeumGoldPillarBreakEffect : MonoBehaviour
    {
        public const float FrameDuration = 0.12f;
        public const int BreakFrameCount = SaemaeumGoldPillarArtwork.FrameCount - 2;
        // Extra time the last cell stays fully visible after its own frame.
        public const float HoldDuration = 0.35f;
        public const float FadeDuration = 0.45f;
        public const float FadeStartsAt = FrameDuration * BreakFrameCount + HoldDuration;
        public const float Duration = FadeStartsAt + FadeDuration;
        private SpriteRenderer visual;
        private SpriteRenderer shadow;
        private float shadowAlpha;
        private float age;
        public SpriteRenderer Visual => visual;
        public SpriteRenderer Shadow => shadow;

        public static SaemaeumGoldPillarBreakEffect Create(SpriteRenderer source, Transform room,
            SpriteRenderer groundShadow = null)
        {
            var root = new GameObject("Saemaeum gold pillar breakup");
            root.transform.SetParent(room, false);
            root.transform.position = source.transform.position;
            root.transform.rotation = source.transform.rotation;
            root.transform.localScale = source.transform.lossyScale;
            if (room != null)
            {
                Vector3 scale = room.lossyScale;
                root.transform.localScale = new Vector3(source.transform.lossyScale.x / scale.x,
                    source.transform.lossyScale.y / scale.y, source.transform.lossyScale.z / scale.z);
            }
            var effect = root.AddComponent<SaemaeumGoldPillarBreakEffect>();
            effect.visual = root.AddComponent<SpriteRenderer>();
            effect.visual.sharedMaterial = source.sharedMaterial;
            effect.visual.sortingLayerID = source.sortingLayerID;
            effect.visual.sortingOrder = source.sortingOrder;
            if (groundShadow != null && groundShadow.enabled && groundShadow.sprite != null)
            {
                // A still copy of the standing pillar's shadow; the pillar itself is deactivated at once.
                var copy = new GameObject("Breakup shadow", typeof(SpriteRenderer));
                copy.transform.SetParent(root.transform, false);
                copy.transform.position = groundShadow.transform.position;
                copy.transform.rotation = groundShadow.transform.rotation;
                Vector3 parent = root.transform.lossyScale, size = groundShadow.transform.lossyScale;
                copy.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)),
                    size.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
                effect.shadow = copy.GetComponent<SpriteRenderer>();
                effect.shadow.sprite = groundShadow.sprite;
                effect.shadow.color = groundShadow.color;
                effect.shadow.sortingLayerID = groundShadow.sortingLayerID;
                effect.shadow.sortingOrder = groundShadow.sortingOrder;
                effect.shadowAlpha = groundShadow.color.a;
            }
            effect.RenderAt(0f);
            return effect;
        }

        public static float AlphaAt(float elapsed)
        {
            if (elapsed >= Duration) return 0f;
            float fade = Mathf.Clamp01((elapsed - FadeStartsAt) / FadeDuration);
            return 1f - fade * fade * (3f - 2f * fade);
        }

        public void RenderAt(float elapsed)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / FrameDuration), 0, BreakFrameCount - 1);
            visual.sprite = SaemaeumGoldPillarArtwork.Frame(index + 2);
            float alpha = AlphaAt(elapsed);
            visual.color = new Color(1f, 1f, 1f, alpha);
            if (shadow != null)
            {
                Color color = shadow.color;
                color.a = shadowAlpha * alpha;
                shadow.color = color;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
