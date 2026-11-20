using System;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Presentation only: a broken tree falls over in four drawn frames, rests on the last one, then fades out. It
    // never carries a collider; the obstacle itself is deactivated the moment it breaks.
    // The sheets are drawn falling right. A hit from the right side mirrors the fall so the tree falls away from it.
    // Each sheet is 2x2 equal cells aligned on the stump: every cell shares one pivot at the root bottom center.
    public sealed class TreeFallEffect : MonoBehaviour
    {
        public const string Sheet = "tree-fall-sheet";
        public const string BurningSheet = "tree-burning-fall-sheet";
        public const int FrameCount = TreeSheetArtwork.FrameCount;
        public const float FrameDuration = 0.1f;
        // The last frame stays fully visible this long after it appears, then the fallen tree fades.
        public const float HoldAfterLastFrame = 0.7f;
        public const float FadeDuration = 0.4f;
        public const float FadeStartsAt = FrameDuration * (FrameCount - 1) + HoldAfterLastFrame;
        public const float Duration = FadeStartsAt + FadeDuration;
        // The standing tree drawing spans this many cells above its root cell bottom (FairyKingdomArtworkView).
        public const float StandingHeight = 2f;

        private SpriteRenderer visual;
        private float age;
        public SpriteRenderer Visual => visual;
        public bool FallsLeft => visual != null && visual.flipX;
        public bool Burning { get; private set; }

        // True when the hit came from the right of the tree, so it falls left (mirrored).
        public static bool FallsLeftFrom(DestructibleObstacle obstacle) =>
            obstacle != null && obstacle.HasLastHitPoint && obstacle.LastHitPoint.x > obstacle.transform.position.x;

        public static Sprite Frame(bool burning, int index)
        {
            string id = burning ? BurningSheet : Sheet;
            TreeSheetArtwork.Manifest m = TreeSheetArtwork.Load(id);
            if (m == null) return null;
            if (m.intactHeight <= 0) throw new InvalidOperationException("Tree fall sheet has no standing height: " + id);
            // Frame 1 (the standing tree) is exactly StandingHeight tall, like the intact obstacle drawing.
            return TreeSheetArtwork.Frame(id, index, m.intactHeight / StandingHeight, new Vector2(m.pivotX, m.pivotY));
        }

        public static int FrameAt(float elapsed) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, elapsed) / FrameDuration), 0, FrameCount - 1);

        public static float AlphaAt(float elapsed)
        {
            if (elapsed >= Duration) return 0f;
            float fade = Mathf.Clamp01((elapsed - FadeStartsAt) / FadeDuration);
            return 1f - fade * fade * (3f - 2f * fade);
        }

        // obstacle: the tree's root marks its lower grid cell; source: its drawing, for sorting and material.
        public static TreeFallEffect Create(DestructibleObstacle obstacle, SpriteRenderer source)
        {
            if (obstacle == null || Frame(obstacle.IsBurning, 0) == null) return null;
            Transform root = obstacle.transform;
            Transform room = root.parent;
            var effectObject = new GameObject("Tree fall") { layer = source != null ? source.gameObject.layer : 0 };
            effectObject.transform.SetParent(room, false);
            // The root cell is one unit tall; the drawing stands on its bottom edge.
            effectObject.transform.position = root.TransformPoint(new Vector3(0f, -0.5f, 0f));
            Vector3 scale = root.lossyScale;
            if (room != null)
            {
                Vector3 parent = room.lossyScale;
                scale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z);
            }
            effectObject.transform.localScale = scale;

            var effect = effectObject.AddComponent<TreeFallEffect>();
            effect.Burning = obstacle.IsBurning;
            effect.visual = effectObject.AddComponent<SpriteRenderer>();
            effect.visual.flipX = FallsLeftFrom(obstacle);
            if (source != null)
            {
                effect.visual.sharedMaterial = source.sharedMaterial;
                effect.visual.sortingLayerID = source.sortingLayerID;
                effect.visual.sortingOrder = source.sortingOrder;
            }
            effect.RenderAt(0f);
            return effect;
        }

        public void RenderAt(float elapsed)
        {
            visual.sprite = Frame(Burning, FrameAt(elapsed));
            visual.color = new Color(1f, 1f, 1f, AlphaAt(elapsed));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            RenderAt(age);
        }
    }
}
