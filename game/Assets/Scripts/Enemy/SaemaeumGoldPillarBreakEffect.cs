using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Survives the obstacle's immediate deactivation; never carries a collider or gameplay damage.
    public sealed class SaemaeumGoldPillarBreakEffect : MonoBehaviour
    {
        public const float FrameDuration = 0.1f;
        public const float Duration = FrameDuration * 3f;
        private SpriteRenderer visual;
        private float age;
        public SpriteRenderer Visual => visual;

        public static SaemaeumGoldPillarBreakEffect Create(SpriteRenderer source, Transform room)
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
            effect.RenderAt(0f);
            return effect;
        }

        public void RenderAt(float elapsed)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / FrameDuration), 0, 2);
            visual.sprite = SaemaeumGoldPillarArtwork.Frame(index + 1);
            float alpha = elapsed < FrameDuration * 2f ? 1f :
                Mathf.Clamp01((Duration - elapsed) / FrameDuration);
            visual.color = new Color(1f, 1f, 1f, alpha);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
