using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Presentation only: a counted hit on a standing, unburnt tree shakes the tree drawing through four frames and
    // drops a separate layer of falling leaves over it. Neither touches the collider or the hit count.
    // Both sheets are drawn swaying/drifting right; a hit from the right mirrors them (TreeFallEffect.FallsLeftFrom).
    // The shake frames replace the tree's own sprite (FairyKingdomArtworkView), so the canopy-over-actors split keeps
    // working; the leaves are their own object above the canopy and fade on their last frame.
    public sealed class TreeHitEffect : MonoBehaviour
    {
        public const string ShakeSheet = "tree-shake-sheet";
        public const string LeafSheet = "tree-leaf-fall-sheet";
        public const int FrameCount = TreeSheetArtwork.FrameCount;
        public const float ShakeFrameDuration = 0.06f;
        public const float ShakeDuration = ShakeFrameDuration * FrameCount;
        public const float LeafFrameDuration = 0.1f;
        // The last leaf frame stays this long, then fades.
        public const float LeafHold = 0.15f;
        public const float LeafFadeDuration = 0.2f;
        public const float LeafFadeStartsAt = LeafFrameDuration * (FrameCount - 1) + LeafHold;
        public const float LeafDuration = LeafFadeStartsAt + LeafFadeDuration;
        // The leaf cell's top edge above the tree cell's bottom, in tree heights of TreeFallEffect.StandingHeight:
        // the first leaves start in the canopy and the last ones land near the roots.
        public const float LeafCellTop = 2.05f;
        // Over the canopy that covers actors (FairyKingdomArtworkView.TreeCanopySortingOrder).
        public const int LeafSortingOrder = FairyKingdomArtworkView.TreeCanopySortingOrder + 1;

        private SpriteRenderer visual;
        private float age;
        public SpriteRenderer Visual => visual;
        public bool DriftsLeft => visual != null && visual.flipX;

        public static int ShakeFrameAt(float elapsed) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, elapsed) / ShakeFrameDuration), 0, FrameCount - 1);

        public static int LeafFrameAt(float elapsed) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, elapsed) / LeafFrameDuration), 0, FrameCount - 1);

        public static float LeafAlphaAt(float elapsed)
        {
            if (elapsed >= LeafDuration) return 0f;
            float fade = Mathf.Clamp01((elapsed - LeafFadeStartsAt) / LeafFadeDuration);
            return 1f - fade * fade * (3f - 2f * fade);
        }

        // A shake frame for the tree renderer, in the units of the intact drawing it stands in for: the standing
        // pose is as tall as `intact` and its root bottom sits on the intact drawing's bottom edge, so the tree
        // renderer's transform (FairyKingdomArtworkView.ConfigureTreeVisual) stays as it is.
        public static Sprite ShakeFrame(int index, Sprite intact)
        {
            TreeSheetArtwork.Manifest m = TreeSheetArtwork.Load(ShakeSheet);
            if (m == null || intact == null || m.intactHeight <= 0) return null;
            Bounds bounds = intact.bounds;
            float ppu = m.intactHeight / bounds.size.y;
            // Root bottom at bounds.min.y and root center at bounds.center.x, measured from the sprite pivot.
            var pivot = new Vector2(m.pivotX - bounds.center.x * ppu, m.pivotY - bounds.min.y * ppu);
            return TreeSheetArtwork.Frame(ShakeSheet, index, ppu, pivot);
        }

        public static Sprite LeafFrame(int index)
        {
            TreeSheetArtwork.Manifest shake = TreeSheetArtwork.Load(ShakeSheet);
            TreeSheetArtwork.Manifest leaves = TreeSheetArtwork.Load(LeafSheet);
            if (shake == null || leaves == null || shake.intactHeight <= 0) return null;
            // Same source scale as the shake drawing; the pivot is the cell's top center.
            return TreeSheetArtwork.Frame(LeafSheet, index, shake.intactHeight / TreeFallEffect.StandingHeight,
                new Vector2(leaves.cellWidth * 0.5f, leaves.cellHeight));
        }

        public static TreeHitEffect CreateLeaves(DestructibleObstacle obstacle, SpriteRenderer source)
        {
            if (obstacle == null || LeafFrame(0) == null) return null;
            Transform root = obstacle.transform;
            Transform room = root.parent;
            var leafObject = new GameObject("Tree hit leaves") { layer = source != null ? source.gameObject.layer : 0 };
            leafObject.transform.SetParent(room, false);
            // The root cell is one unit tall; the drawing stands on its bottom edge.
            leafObject.transform.position = root.TransformPoint(new Vector3(0f, LeafCellTop - 0.5f, 0f));
            Vector3 scale = root.lossyScale;
            if (room != null)
            {
                Vector3 parent = room.lossyScale;
                scale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z);
            }
            leafObject.transform.localScale = scale;
            var effect = leafObject.AddComponent<TreeHitEffect>();
            effect.visual = leafObject.AddComponent<SpriteRenderer>();
            effect.visual.flipX = TreeFallEffect.FallsLeftFrom(obstacle);
            if (source != null)
            {
                effect.visual.sharedMaterial = source.sharedMaterial;
                effect.visual.sortingLayerID = source.sortingLayerID;
            }
            effect.visual.sortingOrder = LeafSortingOrder;
            effect.RenderAt(0f);
            return effect;
        }

        public void RenderAt(float elapsed)
        {
            visual.sprite = LeafFrame(LeafFrameAt(elapsed));
            visual.color = new Color(1f, 1f, 1f, LeafAlphaAt(elapsed));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= LeafDuration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
