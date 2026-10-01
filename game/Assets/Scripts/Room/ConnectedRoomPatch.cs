using UnityEngine;

namespace TrickalFanGame.Room
{
    // Keeps painted leaf/flower scale on resized spans, while preserving registered edge pixels.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ConnectedRoomPatch : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Vector2 textureRepeat = Vector2.one;
        [SerializeField] private float foregroundCutoff = 1f;
        [SerializeField] private Texture2D foregroundMask;
        public float ForegroundCutoff => foregroundCutoff;
        public Texture2D ForegroundMask => foregroundMask;
        private MaterialPropertyBlock properties;

        public void Configure(SpriteRenderer renderer, Vector2 repeat, float cutoff = 1f, Texture2D mask = null)
        {
            artwork = renderer;
            textureRepeat = repeat;
            foregroundCutoff = cutoff;
            foregroundMask = mask;
            Apply();
        }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void SetForegroundMask(Texture2D mask)
        {
            if (foregroundMask == mask) return;
            foregroundMask = mask;
            Apply();
        }

        private void Apply()
        {
            if (artwork == null) artwork = GetComponent<SpriteRenderer>();
            if (artwork == null || artwork.sprite == null) return;
            Sprite sprite = artwork.sprite;
            Rect rect = sprite.textureRect;
            Vector2 dimensions = new Vector2(sprite.texture.width, sprite.texture.height);
            if (properties == null) properties = new MaterialPropertyBlock();
            artwork.GetPropertyBlock(properties);
            properties.SetVector("_PatchRegion", new Vector4(rect.x / dimensions.x, rect.y / dimensions.y,
                rect.width / dimensions.x, rect.height / dimensions.y));
            properties.SetVector("_TextureRepeat", new Vector4(textureRepeat.x, textureRepeat.y, 24f / rect.width, 24f / rect.height));
            properties.SetFloat("_ForegroundCutoff", foregroundCutoff);
            properties.SetFloat("_UseForegroundMask", foregroundMask != null ? 1f : 0f);
            if (foregroundMask != null) properties.SetTexture("_ForegroundMask", foregroundMask);
            artwork.SetPropertyBlock(properties);
        }
    }
}
