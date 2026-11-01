using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Effect-0 (docs/28): visual only. The obstacle 부스러기 throws drops out of the sky onto its landing mark during
    // the end of the telegraph, touching down exactly when the obstacle is placed and its impact is dealt. A ground
    // shadow grows under it as it comes down. Damage, timing and placement stay with BuseureogiBossPatternRuntime.
    public sealed class BossFallingObstacleView : MonoBehaviour
    {
        public const float FallDuration = 0.42f;
        public const float FallHeight = 4f;
        // Above anything standing on the floor, so the obstacle reads as in the air.
        public const int AirSortingOffset = 20;

        private SpriteRenderer body;
        private SpriteRenderer shadow;
        private Vector2 landing;
        private float landsAt;
        private float shadowWidth;
        private float shadowHeight;

        public float LandsAt => landsAt;
        public Vector2 Landing => landing;
        public SpriteRenderer Body => body;
        public SpriteRenderer Shadow => shadow;
        public float CurrentHeight { get; private set; }

        public static BossFallingObstacleView Create(Transform parent, GameObject obstaclePrefab, Vector2 landing,
            float landsAt)
        {
            SpriteRenderer source = obstaclePrefab != null ? obstaclePrefab.GetComponent<SpriteRenderer>() : null;
            if (source == null || source.sprite == null) return null;

            var root = new GameObject("Falling obstacle");
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = landing;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            root.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            var view = root.AddComponent<BossFallingObstacleView>();
            view.landing = landing;
            view.landsAt = landsAt;

            var bodyOwner = new GameObject("Obstacle");
            bodyOwner.transform.SetParent(root.transform, false);
            bodyOwner.transform.localScale = obstaclePrefab.transform.localScale;
            view.body = bodyOwner.AddComponent<SpriteRenderer>();
            view.body.sprite = source.sprite;
            view.body.color = source.color;
            view.body.sortingLayerID = source.sortingLayerID;
            view.body.sortingOrder = source.sortingOrder + AirSortingOffset;

            Sprite[] shadowFrames = CombatEffectArtwork.Frames("landing-shadow");
            if (shadowFrames.Length > 0)
            {
                Bounds bounds = source.sprite.bounds;
                var shadowOwner = new GameObject("Fall shadow");
                shadowOwner.transform.SetParent(root.transform, false);
                shadowOwner.transform.localPosition = BaseOffset(source.sprite, obstaclePrefab.transform.localScale);
                // Over the orange landing mark (sorting order 1).
                view.shadow = CombatEffectArtwork.Renderer(shadowOwner, shadowFrames[0], source, 2);
                view.shadowWidth = bounds.size.x * Mathf.Abs(obstaclePrefab.transform.localScale.x) * 0.8f;
                view.shadowHeight = view.shadowWidth * 0.3f;
            }

            view.Render(Time.time);
            return view;
        }

        // The ground point under a resting obstacle: most of the drawings' lower halves are the obstacle itself.
        public static Vector3 BaseOffset(Sprite sprite, Vector3 scale) =>
            sprite == null ? Vector3.zero : new Vector3(0f, -sprite.bounds.extents.y * 0.62f * Mathf.Abs(scale.y), 0f);

        public void Render(float now)
        {
            float progress = Mathf.Clamp01(1f - (landsAt - now) / FallDuration);
            bool falling = now >= landsAt - FallDuration;
            // Gravity: slow at the top, fastest at touchdown.
            CurrentHeight = falling ? FallHeight * (1f - progress * progress) : FallHeight;
            if (body != null)
            {
                body.enabled = falling;
                body.transform.localPosition = Vector3.up * CurrentHeight;
                Color color = body.color;
                color.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.22f, progress));
                body.color = color;
            }

            if (shadow == null) return;
            shadow.enabled = falling;
            float grow = Mathf.Lerp(0.35f, 1f, progress);
            CombatEffectArtwork.Size(shadow, shadowWidth * grow, shadowHeight * grow);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.45f, progress));
        }

        private void Update() => Render(Time.time);
    }
}
