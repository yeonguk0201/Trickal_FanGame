using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class CombatSpriteEffect : MonoBehaviour
    {
        private SpriteRenderer visual;
        private Sprite[] frames;
        private float elapsed;
        private float duration;
        private Transform source;
        private Vector3 initialSourcePosition;
        private bool follow;
        private float delay;

        public static CombatSpriteEffect Play(string artwork, Vector3 position, float width, float height,
            float seconds, Transform source, Renderer sortingSource, bool follow = false,
            float angle = 0f, Vector2? pivot = null, bool flipX = false, Rect? clipBounds = null, float delay = 0f)
        {
            if (!Application.isPlaying) return null;
            Sprite[] frames = CombatEffectArtwork.Frames(artwork, 4, 2, pivot);
            if (frames.Length == 0) return null;
            var owner = new GameObject("VFX " + artwork);
            owner.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            var effect = owner.AddComponent<CombatSpriteEffect>();
            effect.visual = CombatEffectArtwork.Renderer(owner, frames[0], sortingSource, 2);
            effect.visual.flipX = flipX;
            if (clipBounds.HasValue)
            {
                effect.visual.sharedMaterial = CombatEffectArtwork.Material("GroundSlam");
                Rect bounds = clipBounds.Value;
                var properties = new MaterialPropertyBlock();
                properties.SetVector("_ClipRect", new Vector4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax));
                effect.visual.SetPropertyBlock(properties);
            }
            effect.frames = frames;
            effect.duration = Mathf.Max(0.01f, seconds);
            effect.source = source;
            effect.initialSourcePosition = source != null ? source.position : position;
            effect.follow = follow;
            effect.delay = Mathf.Max(0f, delay);
            effect.visual.enabled = effect.delay <= 0f;
            CombatEffectArtwork.Size(effect.visual, width, height);
            return effect;
        }

        private void Update()
        {
            if (source == null || !source.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 movement = source.position - initialSourcePosition;
            if (movement.sqrMagnitude > 16f) { Destroy(gameObject); return; }
            if (follow)
            {
                // A room teleport must not carry an old effect into the next room.
                transform.position += movement;
            }
            initialSourcePosition = source.position;
            elapsed += Time.deltaTime;
            if (elapsed < delay) return;
            visual.enabled = true;
            float animationAge = elapsed - delay;
            if (animationAge >= duration) { Destroy(gameObject); return; }
            visual.sprite = frames[Mathf.Min(frames.Length - 1, (int)(animationAge / duration * frames.Length))];
        }
    }
}
