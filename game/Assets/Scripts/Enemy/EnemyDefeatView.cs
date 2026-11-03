using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>The knocked-away picture left by <see cref="EnemyDefeatAnimation"/>. It has no collider.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDefeatView : MonoBehaviour
    {
        public const string ObjectName = "Enemy Defeat";
        public const float FrameSeconds = 0.1f;
        public const float HoldSeconds = 0.35f;
        public const float FadeSeconds = 0.45f;
        public const float KnockDistance = 0.9f;
        public const float HopHeight = 0.18f;
        private const float WallSkin = 0.05f;

        private SpriteRenderer view;
        private Sprite[] frames;
        private Vector3 start;
        private Vector3 knock;
        private float age;
        private float baseAlpha = 1f;

        public float Age => age;
        public Vector3 KnockOffset => knock;
        public static float Lifetime(int frameCount) => frameCount * FrameSeconds + HoldSeconds + FadeSeconds;

        public static EnemyDefeatView Spawn(EnemyDefeatAnimation owner, SpriteRenderer body, Vector2 direction)
        {
            Sprite[] frames = owner.Frames;
            GameObject root = new(ObjectName, typeof(SpriteRenderer), typeof(EnemyDefeatView));
            // Stay with the room, so leaving it also clears the picture.
            Transform room = owner.transform.parent;
            root.transform.SetParent(room, false);
            Bounds bounds = body.bounds;
            root.transform.position = new Vector3(bounds.center.x, bounds.min.y, owner.transform.position.z);
            // The first frame stands as tall as the body did, including elite size.
            float height = frames[0].bounds.size.y;
            float scale = (height > 0.0001f ? bounds.size.y / height : 1f) * owner.SizeMultiplier;
            float parentScale = room != null ? Mathf.Max(0.0001f, Mathf.Abs(room.lossyScale.x)) : 1f;
            root.transform.localScale = new Vector3(scale / parentScale, scale / parentScale, 1f);

            var view = root.GetComponent<EnemyDefeatView>();
            view.view = root.GetComponent<SpriteRenderer>();
            view.view.sprite = frames[0];
            view.view.sortingLayerID = body.sortingLayerID;
            view.view.sortingOrder = body.sortingOrder;
            view.view.flipX = direction.x < -0.01f;
            view.baseAlpha = body.color.a;
            view.view.color = new Color(1f, 1f, 1f, view.baseAlpha);
            view.frames = frames;
            view.start = root.transform.position;
            view.knock = (Vector3)(direction * ClearDistance(owner, direction));
            return view;
        }

        // Stop short of a wall so the picture never slides into one.
        private static float ClearDistance(EnemyDefeatAnimation owner, Vector2 direction)
        {
            int mask = Physics2D.GetLayerCollisionMask(owner.gameObject.layer);
            float distance = KnockDistance;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(owner.transform.position, direction, KnockDistance, mask))
            {
                if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(owner.transform) ||
                    hit.collider.GetComponentInParent<Health>() != null) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - WallSkin));
            }
            return distance;
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            age += deltaTime;
            float motion = frames.Length * FrameSeconds;
            int index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age / FrameSeconds));
            view.sprite = frames[index];
            float t = Mathf.Clamp01(age / motion);
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = start + knock * eased + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight);
            float fade = Mathf.Clamp01((age - motion - HoldSeconds) / FadeSeconds);
            Color color = view.color;
            color.a = baseAlpha * (1f - fade);
            view.color = color;
            if (age < Lifetime(frames.Length)) return;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
