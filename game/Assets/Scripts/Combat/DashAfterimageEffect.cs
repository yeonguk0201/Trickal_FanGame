using UnityEngine;

namespace TrickalFanGame.Combat
{
    // A row of fading copies of a sprite along a very fast move, so the path reads even when it takes two or three frames.
    public sealed class DashAfterimageEffect : MonoBehaviour
    {
        private const int Copies = 5;
        private const float Seconds = 0.2f;
        private const float StartAlpha = 0.55f;
        private static readonly Color Tint = new Color(0.82f, 0.88f, 1f, 1f);

        private SpriteRenderer[] images;
        private float age;

        public static void Play(SpriteRenderer source, Vector2 offsetToEnd)
        {
            if (!Application.isPlaying || source == null || source.sprite == null) return;
            var owner = new GameObject("VFX dash afterimage");
            var effect = owner.AddComponent<DashAfterimageEffect>();
            effect.images = new SpriteRenderer[Copies];
            for (int index = 0; index < Copies; index++)
            {
                var child = new GameObject("Afterimage " + index);
                child.transform.SetParent(owner.transform, false);
                child.transform.SetPositionAndRotation(
                    source.transform.position + (Vector3)(offsetToEnd * (index / (float)Copies)),
                    source.transform.rotation);
                child.transform.localScale = source.transform.lossyScale;
                SpriteRenderer image = CombatEffectArtwork.Renderer(child, source.sprite, source, -1);
                image.flipX = source.flipX;
                image.flipY = source.flipY;
                effect.images[index] = image;
            }
            effect.Render();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds) { Destroy(gameObject); return; }
            Render();
        }

        private void Render()
        {
            float life = 1f - age / Seconds;
            for (int index = 0; index < images.Length; index++)
            {
                // Copies nearer the end of the move stay stronger, so the trail points where the boss went.
                float strength = (index + 1f) / images.Length;
                images[index].color = new Color(Tint.r, Tint.g, Tint.b, StartAlpha * strength * life);
            }
        }
    }
}
