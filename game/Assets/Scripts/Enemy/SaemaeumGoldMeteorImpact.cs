using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Visual only: damage and obstacle destruction remain in the meteor's single Land call.
    public sealed class SaemaeumGoldMeteorImpact : MonoBehaviour
    {
        // The dust cell rests briefly and then fades (2026-11-20), sooner than the gold pillar's remnants.
        public const float LastFrameAt = 0.22f;
        public const float HoldDuration = 0.1f;
        public const float FadeDuration = 0.25f;
        public const float FadeStartsAt = LastFrameAt + HoldDuration;
        public const float Duration = FadeStartsAt + FadeDuration;
        private SpriteRenderer visual;
        private float age;
        public SpriteRenderer Visual => visual;

        public static SaemaeumGoldMeteorImpact Create(Vector2 position, SpriteRenderer sortingSource, GameObject owner)
        {
            var root = new GameObject("Saemaeum meteor impact");
            root.transform.position = position;
            var effect = root.AddComponent<SaemaeumGoldMeteorImpact>();
            effect.visual = root.AddComponent<SpriteRenderer>();
            effect.visual.sortingLayerID = sortingSource.sortingLayerID;
            effect.visual.sortingOrder = sortingSource.sortingOrder;
            effect.RenderAt(0f);
            // The impact survives the falling sprite but is cleared with the boss/room.
            if (Application.isPlaying && owner != null)
                owner.GetComponent<BossController>()?.RegisterOwnedObject(root);
            return effect;
        }

        public void RenderAt(float elapsed)
        {
            int index = elapsed < 0.04f ? 0 : elapsed < 0.12f ? 1 : elapsed < LastFrameAt ? 2 : 3;
            visual.sprite = SaemaeumGoldMeteorArtwork.ImpactFrame(index);
            float fade = elapsed >= Duration ? 1f : Mathf.Clamp01((elapsed - FadeStartsAt) / FadeDuration);
            // Eases out so the dust thins smoothly instead of cutting off.
            visual.color = new Color(1f, 1f, 1f, 1f - fade * fade * (3f - 2f * fade));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
