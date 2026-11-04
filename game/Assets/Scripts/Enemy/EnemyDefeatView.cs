using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>The knocked-away picture left by <see cref="EnemyDefeatAnimation"/>. It has no collider.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDefeatView : MonoBehaviour
    {
        public const string ObjectName = "Enemy Defeat";
        public const string ShadowObjectName = "Enemy Defeat Shadow";
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
        // A floor shadow beside the picture (not under its hop or scale), with no collider either.
        private SpriteRenderer shadow;
        private Vector3 shadowOffset;
        private float shadowAlpha;

        public float Age => age;
        public Vector3 KnockOffset => knock;
        public SpriteRenderer Shadow => shadow;
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
            view.CreateShadow(owner, body, room);
            return view;
        }

        // Stop short of a wall so the picture never slides into one.
        private static float ClearDistance(EnemyDefeatAnimation owner, Vector2 direction)
        {
            int mask = Physics2D.GetLayerCollisionMask(owner.gameObject.layer);
            float reach = KnockDistance * owner.KnockScale;
            float distance = reach;
            if (reach <= 0f) return 0f;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(owner.transform.position, direction, reach, mask))
            {
                if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(owner.transform) ||
                    hit.collider.GetComponentInParent<Health>() != null) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - WallSkin));
            }
            return distance;
        }

        // Takes over the body's last ground shadow (its size, place and darkness); a body without one gets the
        // default ground shadow under its drawing.
        private void CreateShadow(EnemyDefeatAnimation owner, SpriteRenderer body, Transform room)
        {
            GroundShadow source = owner.GetComponent<GroundShadow>();
            SpriteRenderer last = source != null ? source.Visual : null;
            GameObject child = new(ShadowObjectName, typeof(SpriteRenderer));
            child.layer = gameObject.layer;
            child.transform.SetParent(room, false);
            shadow = child.GetComponent<SpriteRenderer>();
            shadow.sprite = GroundShadow.SharedSprite;
            Vector3 position, size;
            if (last != null)
            {
                position = last.transform.position;
                size = last.transform.lossyScale;
                shadowAlpha = last.color.a;
                shadow.sortingLayerID = last.sortingLayerID;
                shadow.sortingOrder = last.sortingOrder;
            }
            else
            {
                Bounds bounds = body.bounds;
                float width = Mathf.Max(0.08f, bounds.size.x * 0.95f);
                position = new Vector3(bounds.center.x, bounds.min.y + Mathf.Min(0.08f, bounds.size.y * 0.1f), 0f);
                size = new Vector3(width, width * 0.28f, 1f);
                shadowAlpha = 0.28f;
                shadow.sortingLayerID = body.sortingLayerID;
                shadow.sortingOrder = Mathf.Min(GroundShadow.FloorSortingOrder, body.sortingOrder - 1);
            }
            position.z = start.z;
            shadowOffset = position - start;
            child.transform.position = position;
            Vector3 parentScale = room != null ? room.lossyScale : Vector3.one;
            child.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                size.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
            shadow.color = new Color(0f, 0f, 0f, shadowAlpha);
        }

        // Also on room unload or any other removal of the picture.
        private void OnDestroy() => RemoveShadow();

        private void RemoveShadow()
        {
            if (shadow == null) return;
            if (Application.isPlaying) Destroy(shadow.gameObject);
            else DestroyImmediate(shadow.gameObject);
            shadow = null;
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
            if (shadow != null)
            {
                // The shadow slides with the knock but stays on the floor during the hop.
                shadow.transform.position = start + knock * eased + shadowOffset;
                shadow.color = new Color(0f, 0f, 0f, shadowAlpha * (1f - fade));
            }
            if (age < Lifetime(frames.Length)) return;
            RemoveShadow();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
