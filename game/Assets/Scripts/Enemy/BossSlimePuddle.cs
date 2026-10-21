using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 새마음금고 슬라임 장판: a floor puddle that slows the player whose feet stand in it. It never blocks movement
    // and has no Health, so it cannot set off on-hit or on-kill item effects. Player attacks pop it after a fixed
    // number of hits, counted per attack like a destructible obstacle so attack power does not change the count.
    // Player projectiles pass through it after counting their hit, so puddles never shield the boss.
    [DisallowMultipleComponent]
    public sealed class BossSlimePuddle : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Slime Puddle";
        // Over the room floor (-100) and scorch marks (-60), under ground shadows (-50) and every body.
        public const int SortingOrder = -57;
        // The slow is renewed every frame and ends this long after the feet leave the puddle.
        public const float SlowLinger = 0.15f;
        public const int DefaultRequiredHits = 3;
        private const int TextureSize = 128;

        private static readonly List<BossSlimePuddle> live = new();
        private static Sprite sharedSprite;

        [SerializeField, Min(0.1f)] private float radius = 1.2f;
        [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.75f;
        [SerializeField, Min(1)] private int requiredHits = DefaultRequiredHits;

        private Transform target;
        private PlayerMovement player;
        private SpriteRenderer visual;
        private int hitsTaken;
        private bool popped;

        public static IReadOnlyList<BossSlimePuddle> Live
        {
            get
            {
                live.RemoveAll(item => item == null);
                return live;
            }
        }
        public float Radius => radius;
        public float SpeedMultiplier => speedMultiplier;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int HitsTaken => hitsTaken;
        public bool IsPopped => popped;
        public SpriteRenderer Visual => visual;
        public static Sprite PlaceholderSprite => SharedSprite();

        public static BossSlimePuddle Create(Vector2 position, float radius, float speedMultiplier, int requiredHits,
            Transform target, int sortingLayerId)
        {
            var host = new GameObject(ObjectName);
            host.transform.position = position;
            var puddle = host.AddComponent<BossSlimePuddle>();
            puddle.radius = Mathf.Max(0.1f, radius);
            puddle.speedMultiplier = Mathf.Clamp01(speedMultiplier);
            puddle.requiredHits = Mathf.Max(1, requiredHits);
            puddle.target = target;
            puddle.player = target != null ? target.GetComponentInParent<PlayerMovement>() : null;

            // Default layer: player projectiles reach it, while bodies, enemy shots and wall checks ignore it.
            CircleCollider2D trigger = host.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;

            puddle.visual = host.AddComponent<SpriteRenderer>();
            puddle.visual.sprite = SharedSprite();
            puddle.visual.sortingLayerID = sortingLayerId;
            puddle.visual.sortingOrder = SortingOrder;
            // The sprite is 1 unit wide, so the scale is the drawn diameter: the same circle that slows.
            host.transform.localScale = new Vector3(puddle.radius * 2f, puddle.radius * 2f, 1f);
            trigger.radius = 0.5f;
            puddle.RefreshVisual();
            // Registered here too: Edit Mode tools create puddles without OnEnable running.
            if (!live.Contains(puddle)) live.Add(puddle);
            return puddle;
        }

        public bool Contains(Vector2 point) => ((Vector2)transform.position - point).sqrMagnitude <= radius * radius;

        // One player attack touched the puddle. Returns whether the hit counted.
        public bool RegisterPlayerHit()
        {
            if (popped || !isActiveAndEnabled) return false;
            hitsTaken++;
            RefreshVisual();
            if (hitsTaken >= RequiredHits) Pop();
            return true;
        }

        // Hits every puddle touched by a player attack area once.
        public static int HitInCircle(Vector2 center, float areaRadius, ISet<BossSlimePuddle> alreadyHit = null)
        {
            int hits = 0;
            live.RemoveAll(item => item == null);
            for (int index = live.Count - 1; index >= 0; index--)
            {
                BossSlimePuddle puddle = live[index];
                if (puddle == null) continue;
                float reach = puddle.radius + Mathf.Max(0f, areaRadius);
                if (((Vector2)puddle.transform.position - center).sqrMagnitude > reach * reach) continue;
                if (alreadyHit != null && !alreadyHit.Add(puddle)) continue;
                if (puddle.RegisterPlayerHit()) hits++;
            }
            return hits;
        }

        public void Pop()
        {
            if (popped) return;
            popped = true;
            live.Remove(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        public void Tick(float now)
        {
            if (popped || target == null || (player != null && IsFlying(player))) return;
            Vector2 feet = player != null ? player.FeetPosition : (Vector2)target.position;
            if (!Contains(feet)) return;
            EnvironmentSlow.Apply(player != null ? player.gameObject : target.gameObject, speedMultiplier, now,
                now + SlowLinger);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        private void OnEnable()
        {
            if (!popped && !live.Contains(this)) live.Add(this);
        }

        private void OnDisable() => live.Remove(this);

        private void RefreshVisual()
        {
            if (visual == null) return;
            // Each hit thins the puddle so the player can read how close it is to popping.
            float remaining = 1f - hitsTaken / (float)RequiredHits;
            visual.color = new Color(1f, 0.86f, 0.32f, Mathf.Lerp(0.38f, 0.78f, remaining));
        }

        private static bool IsFlying(PlayerMovement body) =>
            body.TryGetComponent(out PlayerFlight flight) && flight.IsFlying;

        // Placeholder art until a drawn slime puddle exists: a soft disc with a brighter rim and a few glossy spots.
        private static Sprite SharedSprite()
        {
            if (sharedSprite != null) return sharedSprite;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Saemaeum slime puddle (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // A slightly lumpy edge so it reads as goo rather than a perfect decal.
                    float angle = Mathf.Atan2(dy, dx);
                    float edge = 0.94f + 0.04f * Mathf.Sin(angle * 5f) + 0.02f * Mathf.Sin(angle * 9f + 1.3f);
                    float alpha = Mathf.Clamp01((edge - distance) / 0.06f);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(distance - edge + 0.08f) / 0.08f);
                    float gloss = Mathf.Clamp01(1f - new Vector2(dx + 0.35f, dy - 0.35f).magnitude / 0.18f);
                    float light = Mathf.Clamp01(0.62f + 0.3f * rim + 0.5f * gloss);
                    pixels[y * TextureSize + x] = new Color(light, light, light, alpha * (0.75f + 0.25f * rim));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                TextureSize);
            sharedSprite.name = "Saemaeum slime puddle";
            sharedSprite.hideFlags = HideFlags.DontSave;
            return sharedSprite;
        }
    }
}
