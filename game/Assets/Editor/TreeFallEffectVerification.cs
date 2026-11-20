using System;
using System.Collections.Generic;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // A broken tree falls in four drawn frames away from the hit, holds the last frame 0.7s, fades, and never
    // carries a collider.
    public static class TreeFallEffectVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Tree Fall Effect")]
        public static void Verify()
        {
            VerifySheet(false);
            VerifySheet(true);
            VerifyTiming();
            // Melee center left of the tree: default fall to the right.
            VerifyFall(false, root => HitAll(root, new Vector2(-1f, 0.2f)), false);
            // A shot or attack from the right mirrors the fall.
            VerifyFall(false, root => HitAll(root, new Vector2(1.2f, 0f)), true);
            // An unknown hit side keeps the drawn direction.
            VerifyFall(false, root => HitAll(root, null), false);
            // A burning tree uses the burning sheet.
            VerifyFall(true, root => HitAll(root, new Vector2(0.9f, 0.4f)), true);
            // An attack area passes its center through HitInCircle.
            VerifyFall(false, root =>
            {
                Physics2D.SyncTransforms();
                Vector2 center = (Vector2)root.transform.position + new Vector2(0.7f, 0f);
                var hit = new HashSet<DestructibleObstacle>();
                for (int i = 0; i < 4; i++)
                {
                    hit.Clear();
                    Require(DestructibleObstacle.HitInCircle(center, 0.5f, hit) == 1, "Attack area missed the tree.");
                }
            }, true);
            // A bomb left of the tree pushes it right.
            VerifyFall(false, root =>
            {
                Physics2D.SyncTransforms();
                Require(DestructibleObstacle.DestroyByBombInCircle(
                    (Vector2)root.transform.position + Vector2.left * 0.8f, 1f) == 1, "Bomb missed the tree.");
            }, false);
            VerifyShakeFrames();
            VerifyHit(new Vector2(-1f, 0.3f), false);
            VerifyHit(new Vector2(1f, 0.3f), true);
            Debug.Log("Tree fall verification passed: both sheets share one stump pivot and scale, the standing " +
                      "frame is two cells tall, the fall mirrors away from the hit side (melee, shot, area, bomb), " +
                      "the last frame holds 0.7s before fading, and the effect has no collider. Tree hit passed: " +
                      "Hit fires only on non-breaking hits, shake frames stand on the intact drawing's bounds, and " +
                      "the leaves mirror by hit side, sit above the canopy, and fade.");
        }

        private static void VerifySheet(bool burning)
        {
            var seen = new HashSet<Sprite>();
            Sprite first = TreeFallEffect.Frame(burning, 0);
            Require(first != null, "Missing tree fall sheet (burning: " + burning + ").");
            for (int i = 0; i < TreeFallEffect.FrameCount; i++)
            {
                Sprite sprite = TreeFallEffect.Frame(burning, i);
                Require(sprite != null && seen.Add(sprite), "Missing/duplicate tree fall frame " + i);
                Require(Mathf.Approximately(sprite.pixelsPerUnit, first.pixelsPerUnit) &&
                        sprite.pivot == first.pivot && sprite.rect.size == first.rect.size,
                    "Tree fall frames must share one stump pivot and scale.");
                Require(sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 &&
                        sprite.rect.xMax <= sprite.texture.width && sprite.rect.yMax <= sprite.texture.height,
                    "Tree fall frame outside its texture.");
                Require(sprite.name.StartsWith(burning ? TreeFallEffect.BurningSheet : TreeFallEffect.Sheet + "-"),
                    "Tree fall frame from the wrong sheet.");
            }
            // The root bottom sits on the pivot, the drawing reaches about two cells up, and the fallen tree lies to
            // the right of the stump.
            Bounds standing = first.bounds;
            Require(standing.min.y < 0f && standing.min.y > -0.1f && standing.max.y > 1.95f && standing.max.y < 2.15f,
                "Standing frame must be two cells tall from its root bottom.");
            Bounds fallen = TreeFallEffect.Frame(burning, TreeFallEffect.FrameCount - 1).bounds;
            Require(fallen.max.x > 1.5f && fallen.min.x > -0.8f, "The drawn fall must go to the right.");
            Require(ReadAlpha(first, true) > 0 && ReadAlpha(TreeFallEffect.Frame(burning, 3), false) > 0,
                "Frames must not be empty.");
        }

        // Counts opaque pixels in the stump's column of the standing frame, or anywhere in the fallen frame.
        private static int ReadAlpha(Sprite sprite, bool stumpColumn)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            var pixels = new Texture2D(2, 2);
            try
            {
                pixels.LoadImage(System.IO.File.ReadAllBytes(path));
                int count = 0;
                Rect rect = sprite.rect;
                int fromX = stumpColumn ? (int)(rect.x + sprite.pivot.x) - 2 : (int)rect.x;
                int toX = stumpColumn ? fromX + 4 : (int)rect.xMax;
                for (int y = (int)rect.y; y < (int)rect.yMax; y += 2)
                for (int x = fromX; x < toX; x += stumpColumn ? 1 : 3)
                    if (pixels.GetPixel(x, y).a > 0.5f) count++;
                return count;
            }
            finally { Object.DestroyImmediate(pixels); }
        }

        private static void VerifyTiming()
        {
            for (int i = 0; i < TreeFallEffect.FrameCount; i++)
                Require(TreeFallEffect.FrameAt((i + 0.5f) * TreeFallEffect.FrameDuration) == i, "Frame order changed.");
            float lastFrameAt = TreeFallEffect.FrameDuration * (TreeFallEffect.FrameCount - 1);
            Require(Mathf.Abs(TreeFallEffect.FadeStartsAt - lastFrameAt - 0.7f) < 0.0001f,
                "The last frame must hold 0.7s before the fade.");
            Require(TreeFallEffect.FrameAt(TreeFallEffect.FadeStartsAt + 0.1f) == TreeFallEffect.FrameCount - 1,
                "The fade must keep the last frame.");
            Require(Mathf.Approximately(TreeFallEffect.AlphaAt(TreeFallEffect.FadeStartsAt - 0.01f), 1f) &&
                    TreeFallEffect.AlphaAt(TreeFallEffect.FadeStartsAt + TreeFallEffect.FadeDuration * 0.5f) is > 0f and < 1f &&
                    TreeFallEffect.AlphaAt(TreeFallEffect.Duration) == 0f,
                "Fade curve changed.");
        }

        // Every shake frame shares one pivot and stands its root bottom on the intact drawing's bottom edge, with the
        // standing pose as tall as the intact drawing, so the tree renderer's transform never changes.
        private static void VerifyShakeFrames()
        {
            Sprite intact = FairyKingdomArtwork.Fit("obstacle-tree", 1f);
            Require(intact != null, "Missing intact tree drawing.");
            var seen = new HashSet<Sprite>();
            Sprite first = TreeHitEffect.ShakeFrame(0, intact);
            Require(first != null, "Missing tree shake sheet.");
            for (int i = 0; i < TreeHitEffect.FrameCount; i++)
            {
                Sprite frame = TreeHitEffect.ShakeFrame(i, intact);
                Require(frame != null && seen.Add(frame) && frame.pivot == first.pivot &&
                        Mathf.Approximately(frame.pixelsPerUnit, first.pixelsPerUnit), "Shake frames must share one pivot.");
                Require(TreeHitEffect.ShakeFrameAt((i + 0.5f) * TreeHitEffect.ShakeFrameDuration) == i,
                    "Shake frame order changed.");
                Sprite leaf = TreeHitEffect.LeafFrame(i);
                Require(leaf != null && seen.Add(leaf), "Missing/duplicate leaf frame " + i);
                Require(TreeHitEffect.LeafFrameAt((i + 0.5f) * TreeHitEffect.LeafFrameDuration) == i,
                    "Leaf frame order changed.");
            }
            TreeSheetArtwork.Manifest shake = TreeSheetArtwork.Load(TreeHitEffect.ShakeSheet);
            float ppu = first.pixelsPerUnit, pad = shake.pivotY / ppu;
            Bounds bounds = first.bounds;
            Require(Mathf.Abs(bounds.min.y + pad - intact.bounds.min.y) < 0.001f &&
                    Mathf.Abs(shake.intactHeight / ppu - intact.bounds.size.y) < 0.001f,
                "The standing shake frame must match the intact drawing's height and bottom edge.");
            Require(TreeHitEffect.ShakeDuration < 0.3f && TreeHitEffect.LeafAlphaAt(0f) == 1f &&
                    TreeHitEffect.LeafAlphaAt(TreeHitEffect.LeafDuration) == 0f &&
                    TreeHitEffect.LeafFrameAt(TreeHitEffect.LeafFadeStartsAt + 0.05f) == TreeHitEffect.FrameCount - 1,
                "Shake/leaf timing changed.");
        }

        private static void VerifyHit(Vector2 offset, bool expectLeft)
        {
            var room = new GameObject("Tree hit verification room");
            try
            {
                room.transform.position = new Vector3(2600f, 2400f, 0f);
                var root = new GameObject("Tree") { layer = LayerMask.NameToLayer("Environment") };
                root.transform.SetParent(room.transform, false);
                var renderer = new GameObject("Visual").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false);
                root.AddComponent<BoxCollider2D>();
                var obstacle = root.AddComponent<DestructibleObstacle>();
                obstacle.Configure("tree-hit-probe", 4, null, renderer);
                obstacle.ConfigureFixedKind(DestructibleObstacle.TreeVariantId, Color.white, Color.gray, true, 2,
                    Color.red);
                int hits = 0, broken = 0;
                TreeHitEffect leaves = null;
                // Play spawns this from FairyKingdomArtworkView on Hit; edit mode builds it the same way here.
                obstacle.Hit += hit => { hits++; leaves = TreeHitEffect.CreateLeaves(hit, renderer); };
                obstacle.Broken += _ => broken++;
                Vector2 point = (Vector2)root.transform.position + offset;
                Require(obstacle.RegisterPlayerHit(point) && hits == 1 && leaves != null, "A hit must drop leaves.");
                Require(leaves.DriftsLeft == expectLeft, "Leaves must mirror by hit side.");
                Require(leaves.Visual.sortingOrder > FairyKingdomArtworkView.TreeCanopySortingOrder &&
                        leaves.transform.parent == room.transform &&
                        leaves.GetComponentsInChildren<Collider2D>(true).Length == 0,
                    "Leaves must draw above the canopy in the room without a collider.");
                Bounds first = leaves.Visual.bounds;
                Require(first.max.y > root.transform.position.y + 1f && first.min.y > root.transform.position.y - 0.6f,
                    "Leaves must start in the canopy above the tree cell.");
                leaves.RenderAt(TreeHitEffect.LeafDuration - 0.01f);
                Require(leaves.Visual.color.a < 0.05f, "Leaves must fade out.");
                Object.DestroyImmediate(leaves.gameObject);
                obstacle.RegisterPlayerHit(point);
                obstacle.RegisterPlayerHit(point);
                Require(hits == 3 && broken == 0, "Each standing hit must raise Hit.");
                obstacle.RegisterPlayerHit(point);
                Require(hits == 3 && broken == 1, "The breaking hit must raise Broken, not Hit.");
            }
            finally { Object.DestroyImmediate(room); }
        }

        private static void HitAll(GameObject root, Vector2? offset)
        {
            var obstacle = root.GetComponent<DestructibleObstacle>();
            Vector2? point = offset.HasValue ? (Vector2)root.transform.position + offset.Value : null;
            for (int i = 0; i < 4; i++) Require(obstacle.RegisterPlayerHit(point), "Tree hit rejected.");
        }

        private static void VerifyFall(bool burning, Action<GameObject> breakTree, bool expectLeft)
        {
            var room = new GameObject("Tree fall verification room");
            try
            {
                room.transform.position = new Vector3(2400f, 2400f, 0f);
                var root = new GameObject("Tree") { layer = LayerMask.NameToLayer("Environment") };
                root.transform.SetParent(room.transform, false);
                root.transform.localPosition = new Vector3(3f, 2f, 0f);
                var stats = root.AddComponent<PlayerStats>();
                if (burning) stats.AddBurnSource();
                var renderer = new GameObject("Visual").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false);
                renderer.sortingOrder = 3;
                root.AddComponent<BoxCollider2D>();
                var obstacle = root.AddComponent<DestructibleObstacle>();
                obstacle.Configure("tree-fall-probe", 4, null, renderer);
                obstacle.ConfigureFixedKind(DestructibleObstacle.TreeVariantId, Color.white, Color.gray, true, 2,
                    Color.red);
                obstacle.BindBurnSource(stats);
                TreeFallEffect effect = null;
                // Play spawns this from FairyKingdomArtworkView on Broken; edit mode builds it the same way here.
                obstacle.Broken += broken => effect = TreeFallEffect.Create(broken, renderer);
                breakTree(root);
                Require(obstacle.IsBroken && !root.activeSelf && effect != null, "The tree must break into a fall effect.");
                Require(effect.FallsLeft == expectLeft,
                    $"Fall direction wrong (expected {(expectLeft ? "left" : "right")}).");
                Require(effect.Burning == burning && effect.Visual.sprite == TreeFallEffect.Frame(burning, 0),
                    "The fall must start on the matching sheet's first frame.");
                Require(effect.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                        effect.GetComponent<Rigidbody2D>() == null, "The fall effect must not collide.");
                Require(effect.transform.parent == room.transform &&
                        Vector3.Distance(effect.transform.position, root.transform.position + Vector3.down * 0.5f) < 0.001f,
                    "The fall must stand on the tree cell's bottom edge in the room.");
                Require(effect.Visual.sortingOrder == renderer.sortingOrder &&
                        effect.Visual.sortingLayerID == renderer.sortingLayerID, "The fall must keep the tree's sorting.");
                effect.RenderAt(TreeFallEffect.FadeStartsAt - 0.01f);
                Require(effect.Visual.sprite == TreeFallEffect.Frame(burning, 3) &&
                        Mathf.Approximately(effect.Visual.color.a, 1f), "The last frame must hold fully visible.");
                effect.RenderAt(TreeFallEffect.Duration - 0.01f);
                Require(effect.Visual.color.a < 0.05f, "The fallen tree must fade out.");
            }
            finally { Object.DestroyImmediate(room); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Tree fall verification failed: " + message);
        }
    }
}
