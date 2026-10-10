using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // A presentation child only: never changes the body's sprite, transform, collider or damage rules.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class GroundShadow : MonoBehaviour
    {
        public const string ObjectName = "Ground Shadow";
        public const int FloorSortingOrder = -50; // Room floors are -100; bodies and obstacles start at 0.
        private static Sprite sharedSprite;

        [SerializeField, Min(0.01f)] private float widthMultiplier = 1f;
        [SerializeField, Range(0.05f, 1f)] private float thickness = 0.28f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.28f;
        [SerializeField] private Vector2 offset;
        [SerializeField] private string profileId;
        [SerializeField] private bool useSharedProfile = true;
        private string appliedProfile;
        private bool defaultsCached;
        private float defaultWidth, defaultThickness, defaultOpacity;
        private Vector2 defaultOffset;
        private SpriteRenderer shadow;
        private SpriteRenderer source;
        private Collider2D footprint;
        private Health health;
        private PlacedBomb bomb;
        private PlayerFlight flight;
        private bool floating;
        private bool useSpriteFootprint;
        private bool pickup;
        public SpriteRenderer Visual => shadow;
        public string ProfileId => profileId;

        public void SetProfileId(string id) { profileId = id; appliedProfile = null; }

        // Called by physical world entities when enabled, including spawned pickups and rebuilt rooms.
        public static void AttachDuringPlay(GameObject owner)
        {
            if (Application.isPlaying) Ensure(owner);
        }

        public static GroundShadow Ensure(GameObject owner)
        {
            if (owner == null || owner.GetComponentInParent<Canvas>() != null) return null;
            GroundShadow result = owner.GetComponent<GroundShadow>();
            if (result == null) result = owner.AddComponent<GroundShadow>();
            result.Cache();
            return result;
        }

        private void Awake() => Cache();
        private void Start() => Cache();
        private void OnEnable() { Cache(); Refresh(); }
        private void OnDisable() { if (shadow != null) shadow.enabled = false; }
        private void LateUpdate() => Refresh();

        private void Cache()
        {
            health = GetComponent<Health>();
            bomb = GetComponent<PlacedBomb>();
            flight = GetComponent<PlayerFlight>();
            floating = GetComponent<TrickalFanGame.Enemy.EnemyFlight>() != null;
            pickup = GetComponent<RunResourcePickup>() != null || GetComponent<HealthPickup>() != null ||
                GetComponent<SPPickup>() != null || GetComponent<TrickalFanGame.Item.ItemPickup>() != null ||
                GetComponent<TrickalFanGame.Item.SingleUseItemPickup>() != null;
            useSpriteFootprint = pickup || GetComponent<TrickalFanGame.Shop.ShopKeeper>() != null;
            PlayerFeet feet = GetComponentInChildren<PlayerFeet>(true);
            footprint = feet != null ? feet.Collider : GetComponent<Collider2D>();
        }

        private SpriteRenderer FindSource()
        {
            var artwork = GetComponent<FairyKingdomArtworkView>();
            if (artwork != null && artwork.Visual != null) return artwork.Visual;
            var keeper = GetComponent<TrickalFanGame.Shop.ShopKeeper>();
            if (keeper != null && keeper.Portrait != null) return keeper.Portrait;
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body != null) return body;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
                if (candidate != shadow && candidate.gameObject.name != PlayerFlight.ShadowObjectName)
                    return candidate;
            return null;
        }

        public void Refresh()
        {
            if (useSharedProfile)
            {
                if (!defaultsCached)
                {
                    defaultWidth = widthMultiplier; defaultThickness = thickness;
                    defaultOpacity = opacity; defaultOffset = offset; defaultsCached = true;
                }
                string id = GroundShadowProfiles.ResolveId(gameObject, profileId);
                if (id != appliedProfile)
                {
                    appliedProfile = id;
                    if (GroundShadowProfiles.TryGet(id, out var settings))
                    {
                        widthMultiplier = settings.widthMultiplier;
                        thickness = settings.thickness;
                        opacity = settings.opacity;
                        offset = new Vector2(settings.offsetX, settings.offsetY);
                    }
                    else
                    {
                        widthMultiplier = defaultWidth; thickness = defaultThickness;
                        opacity = defaultOpacity; offset = defaultOffset;
                    }
                }
            }
            if (source == null) source = FindSource();
            if (source == null || source.sprite == null) return;
            if (shadow == null)
            {
                GameObject child = new(ObjectName, typeof(SpriteRenderer));
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                shadow = child.GetComponent<SpriteRenderer>();
                shadow.sprite = SharedSprite;
            }
            // The existing flight shadow retains its elevated sorting and bobbing; never draw two shadows.
            if (flight == null) flight = GetComponent<PlayerFlight>();
            shadow.enabled = isActiveAndEnabled && source.enabled && source.gameObject.activeInHierarchy &&
                (health == null || !health.IsDead) && (bomb == null || !bomb.HasExploded) &&
                (flight == null || !flight.IsFlying);
            if (!shadow.enabled) return;

            bool hasFootprint = footprint != null && footprint.enabled;
            Bounds floor = hasFootprint ? footprint.bounds : source.bounds;
            // Use collision footprint, not tree height, animation frame or the shop's talking range.
            if (useSpriteFootprint || !hasFootprint) floor = source.bounds;
            float width = Mathf.Max(0.08f, floor.size.x * (pickup ? 0.8f : 0.95f)) * widthMultiplier;
            float y = floor.min.y + Mathf.Min(0.08f, floor.size.y * 0.1f);
            if (floating) y -= 0.1f;
            shadow.transform.position = new Vector3(floor.center.x + offset.x, y + offset.y, transform.position.z);
            shadow.transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            shadow.transform.localScale = new Vector3(width / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                width * thickness / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            shadow.color = new Color(0f, 0f, 0f, floating ? opacity * 0.8f : opacity);
            shadow.sortingLayerID = source.sortingLayerID;
            shadow.sortingOrder = Mathf.Min(FloorSortingOrder, source.sortingOrder - 1);
        }

        // One smooth unit-square mask, shared by every ground and player-flight shadow.
        public static Sprite SharedSprite
        {
            get
            {
                if (sharedSprite != null) return sharedSprite;
                const int size = 64;
                Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Shared ground shadow mask", filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dx * dx - dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                sharedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                    size, 0, SpriteMeshType.FullRect);
                sharedSprite.name = "Shared ground shadow";
                sharedSprite.hideFlags = HideFlags.HideAndDontSave;
                return sharedSprite;
            }
        }
    }
}
