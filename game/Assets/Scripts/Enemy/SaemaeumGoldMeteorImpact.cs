using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Visual only: damage and obstacle destruction remain in the meteor's single Land call.
    public sealed class SaemaeumGoldMeteorImpact : MonoBehaviour
    {
        public const float Duration = 0.34f;
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
            int index = elapsed < 0.04f ? 0 : elapsed < 0.12f ? 1 : elapsed < 0.22f ? 2 : 3;
            visual.sprite = SaemaeumGoldMeteorArtwork.ImpactFrame(index);
            visual.color = new Color(1f, 1f, 1f,
                elapsed < 0.22f ? 1f : Mathf.Clamp01((Duration - elapsed) / 0.12f));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
