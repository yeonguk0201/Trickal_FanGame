using TrickalFanGame.Combat;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    public enum FairyKingdomArtworkMode { Fixed, Obstacle, Chest, Bomb, Crumb }

    // Presentation only: sprite dimensions never rescale a collider, Rigidbody or gameplay root.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class FairyKingdomArtworkView : MonoBehaviour
    {
        [SerializeField] private string artworkId;
        [SerializeField] private FairyKingdomArtworkMode mode;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField, Min(0.01f)] private float localWidth = 1f;
        private DestructibleObstacle obstacle;
        private TreasureChest chest;
        private PlacedBomb bomb;
        private Health health;
        private string shown;
        private Sprite fitted;
        private SpriteRenderer burningOverlay;
        private bool referencesCached;
        public string ArtworkId => artworkId;
        public FairyKingdomArtworkMode Mode => mode;
        public SpriteRenderer Visual => visual;
        public float LocalWidth => localWidth;

        public void Configure(string id, FairyKingdomArtworkMode value, SpriteRenderer renderer, float width)
        {
            artworkId = id; mode = value; visual = renderer; localWidth = Mathf.Max(0.01f, width);
            shown = null; Cache();
            if (Application.isPlaying) Refresh();
        }

        private void Awake() { Cache(); Refresh(); }
        private void OnEnable()
        {
            Cache();
            if (health != null && mode == FairyKingdomArtworkMode.Crumb) health.Died += OnDied;
        }
        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }
        private void Cache()
        {
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            obstacle = GetComponent<DestructibleObstacle>();
            chest = GetComponent<TreasureChest>();
            bomb = GetComponent<PlacedBomb>();
            health = GetComponent<Health>();
            referencesCached = true;
        }
        public void SetArtwork(string id) { artworkId = id; shown = null; Refresh(); }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (!referencesCached) Cache();
            if (GetComponent<RoomPit>() != null)
            {
                PitTileArtwork tiles = GetComponent<PitTileArtwork>();
                if (tiles == null && Application.isPlaying) tiles = gameObject.AddComponent<PitTileArtwork>();
                if (tiles != null && tiles.isActiveAndEnabled && tiles.HasArtwork)
                {
                    if (visual != null) visual.enabled = false;
                    return;
                }
                if (visual != null) visual.enabled = true;
            }
            if (visual == null) return;
            string id = artworkId;
            if (mode == FairyKingdomArtworkMode.Obstacle && obstacle != null)
                id = "obstacle-" + obstacle.VariantId;
            else if (mode == FairyKingdomArtworkMode.Chest && chest != null)
                id = "chest-" + chest.Kind.ToString().ToLowerInvariant() + (chest.IsOpened ? "-open" : "-closed");
            else if (mode == FairyKingdomArtworkMode.Bomb && bomb != null)
                id = bomb.HasExploded ? "effect-bomb-explosion" : "pickup-bomb";
            if (shown != id)
            {
                fitted = FairyKingdomArtwork.Fit(id, id == "obstacle-tree" ? 1f : localWidth);
                shown = id;
            }
            if (fitted == null) return;
            visual.sprite = fitted;
            if (id == "obstacle-tree" && visual.transform != transform)
            {
                // The root marks the lower grid cell. Only the drawing reaches into the cell above it.
                ConfigureTreeVisual(visual);
            }
            Rigidbody2D movingBody = GetComponent<Rigidbody2D>();
            if ((mode == FairyKingdomArtworkMode.Fixed || mode == FairyKingdomArtworkMode.Crumb) &&
                GetComponent<TrickalFanGame.Enemy.TestEnemy>() != null && movingBody != null &&
                Mathf.Abs(movingBody.linearVelocity.x) > 0.001f)
                visual.flipX = movingBody.linearVelocity.x > 0f;
            // Legacy placeholder palettes must not recolor authored art. Hit feedback owns fixed enemy colors.
            if (mode == FairyKingdomArtworkMode.Obstacle)
            {
                float shade = obstacle != null ? Mathf.Lerp(1f, 0.7f,
                    obstacle.HitsTaken / (float)obstacle.RequiredHits) : 1f;
                visual.color = new Color(shade, shade, shade, 1f);
                if (obstacle != null && obstacle.IsBurning && burningOverlay == null)
                {
                    GameObject child = new("Burning obstacle artwork");
                    child.transform.SetParent(visual.transform, false);
                    burningOverlay = child.AddComponent<SpriteRenderer>();
                    burningOverlay.sprite = FairyKingdomArtwork.Fit("effect-burn", localWidth * 0.55f);
                    burningOverlay.sortingLayerID = visual.sortingLayerID;
                    burningOverlay.sortingOrder = visual.sortingOrder + 2;
                }
            }
            else if (mode == FairyKingdomArtworkMode.Chest || mode == FairyKingdomArtworkMode.Bomb)
                visual.color = Color.white;
        }

        public static void ConfigureTreeVisual(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector3 size = renderer.sprite.bounds.size;
            renderer.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            renderer.transform.localScale = new Vector3(1f / size.x, 2f / size.y, 1f);
        }

        private void OnDied()
        {
            if (visual != null)
                FairyKingdomSpriteEffect.Play("effect-crumb-death", transform.position,
                    Mathf.Max(0.3f, visual.bounds.size.x * 1.2f), 0.24f, visual);
        }
    }
}
