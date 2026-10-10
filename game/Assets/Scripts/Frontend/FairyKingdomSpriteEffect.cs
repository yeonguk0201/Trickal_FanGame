using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Temporary single-frame effects. Their lifetimes do not add, repeat or delay gameplay damage.
    public sealed class FairyKingdomSpriteEffect : MonoBehaviour
    {
        private SpriteRenderer visual;
        private float age;
        private float lifetime;
        public static FairyKingdomSpriteEffect Play(string id, Vector3 position, float width, float seconds,
            Renderer sortingSource = null)
        {
            if (!Application.isPlaying) return null;
            Sprite sprite = FairyKingdomArtwork.Fit(id, width);
            if (sprite == null) return null;
            GameObject owner = new("Artwork " + id);
            owner.transform.position = position;
            SpriteRenderer renderer = owner.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (sortingSource != null) renderer.sortingLayerID = sortingSource.sortingLayerID;
            renderer.sortingOrder = (sortingSource != null ? sortingSource.sortingOrder : 0) + 3;
            FairyKingdomSpriteEffect effect = owner.AddComponent<FairyKingdomSpriteEffect>();
            effect.visual = renderer;
            effect.lifetime = Mathf.Max(0.01f, seconds);
            return effect;
        }
        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }
            visual.color = new Color(1f, 1f, 1f, Mathf.Clamp01((lifetime - age) / (lifetime * 0.4f)));
        }
    }
}
