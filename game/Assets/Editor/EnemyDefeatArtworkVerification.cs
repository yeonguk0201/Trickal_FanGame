using System;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Defeat-0: every bound enemy carries its four bottom-centred defeat frames, and the picture it leaves has no
    // collider, starts where and as tall as the body stood, is knocked away (never through a wall), plays the
    // frames in order, fades out and removes itself.
    public static class EnemyDefeatArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Enemy Defeat Frames")]
        public static void Verify()
        {
            foreach (var (prefabName, frames) in EnemyDefeatArtworkSetup.Bindings)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefabName + ".prefab");
                Require(prefab != null, "Missing enemy Prefab: " + prefabName);
                Require(prefab.GetComponents<EnemyDefeatAnimation>().Length == 1,
                    prefabName + " must carry exactly one defeat animation.");
                Sprite[] sprites = prefab.GetComponent<EnemyDefeatAnimation>().Frames;
                Require(sprites.Length == EnemyDefeatArtworkSetup.FrameCount, prefabName + " must have four defeat frames.");
                for (int i = 0; i < sprites.Length; i++)
                {
                    Require(sprites[i] == AssetDatabase.LoadAssetAtPath<Sprite>(EnemyDefeatArtworkSetup.FramePath(frames, i)),
                        $"{prefabName} defeat frame {i} must be {frames}_Death_{i}.");
                    Require(Mathf.Abs(sprites[i].pivot.y) < 0.5f && Mathf.Abs(sprites[i].pivot.x - sprites[i].rect.width / 2f) < 1f,
                        $"{frames}_Death_{i} must stand on a bottom-centre pivot.");
                }
                CheckPicture(prefab, prefabName);
            }
            Debug.Log($"Defeat-0 verification passed: {EnemyDefeatArtworkSetup.Bindings.Length} enemy Prefabs leave a " +
                "collider-free defeat picture that starts at the body's feet and height, is knocked away from the hit " +
                "and stops short of walls, mirrors when knocked left, plays four frames, fades out and removes itself.");
        }

        private static void CheckPicture(GameObject prefab, string name)
        {
            GameObject room = new("Defeat verification room");
            GameObject wall = null;
            try
            {
                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
                enemy.transform.position = new Vector3(500f, 500f, 0f); // away from any open Scene
                var defeat = enemy.GetComponent<EnemyDefeatAnimation>();
                SpriteRenderer body = BodyOf(enemy);
                Require(body != null, name + " must have a body drawing to measure.");
                Bounds bounds = body.bounds;

                EnemyDefeatView left = defeat.SpawnView(Vector2.left);
                Require(left != null && left.transform.parent == room.transform, name + ": the picture must stay in the room.");
                Require(left.GetComponentInChildren<Collider2D>() == null && left.GetComponent<Rigidbody2D>() == null,
                    name + ": the defeat picture must not collide.");
                var view = left.GetComponent<SpriteRenderer>();
                Require(view.flipX, name + ": a defeat knocked left must be mirrored.");
                Require(view.sortingLayerID == body.sortingLayerID && view.sortingOrder == body.sortingOrder,
                    name + ": the picture must draw where the body drew.");
                float multiplier = defeat.SizeMultiplier;
                Require(Mathf.Abs(multiplier - EnemyDefeatArtworkSetup.SizeMultiplier(prefab, defeat.Frames[0])) < 0.001f &&
                    multiplier > 0.5f && multiplier < 2f, name + ": the size multiplier must be the measured one.");
                // The visible fallen character is as tall as the visible standing one.
                float standing = bounds.size.y * EnemyDefeatArtworkSetup.VisibleShare(body.sprite);
                float fallen = view.bounds.size.y * EnemyDefeatArtworkSetup.VisibleShare(defeat.Frames[0]);
                Require(Mathf.Abs(view.bounds.min.y - bounds.min.y) < 0.02f && Mathf.Abs(fallen - standing) < 0.02f,
                    name + ": the first frame must stand on the body's feet as tall as the visible body.");
                Require(Mathf.Abs(left.KnockOffset.x + EnemyDefeatView.KnockDistance) < 0.001f,
                    name + ": an open floor must allow the full knock distance.");

                Vector3 start = left.transform.position;
                Sprite[] frames = defeat.Frames;
                left.Advance(EnemyDefeatView.FrameSeconds * 1.5f);
                Require(view.sprite == frames[1], name + ": the second frame must follow the first.");
                left.Advance(EnemyDefeatView.FrameSeconds * (frames.Length - 1.5f));
                Require(view.sprite == frames[^1] && (left.transform.position - (start + left.KnockOffset)).sqrMagnitude < 0.0001f,
                    name + ": the knock must end on the last frame at its full offset.");
                Require(view.color.a > 0.99f, name + ": the picture must stay opaque until the fade.");
                left.Advance(EnemyDefeatView.HoldSeconds + EnemyDefeatView.FadeSeconds * 0.5f);
                Require(view.color.a < 0.6f && view.color.a > 0.4f, name + ": the picture must fade out.");
                left.Advance(EnemyDefeatView.FadeSeconds);
                Require(left == null, name + ": the picture must remove itself after fading.");

                // A wall just to the right stops the knock short of it.
                wall = new GameObject("Defeat verification wall", typeof(BoxCollider2D)) { layer = 0 };
                wall.transform.position = enemy.transform.position + new Vector3(0.5f + 0.25f, 0f, 0f);
                wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 4f);
                bool wallBlocks = (Physics2D.GetLayerCollisionMask(enemy.layer) & 1) != 0;
                Physics2D.SyncTransforms();
                EnemyDefeatView right = defeat.SpawnView(Vector2.right);
                Require(!right.GetComponent<SpriteRenderer>().flipX, name + ": a defeat knocked right keeps the drawing.");
                if (wallBlocks)
                    Require(right.KnockOffset.x < 0.5f, name + ": the knock must stop short of a wall.");
                Object.DestroyImmediate(right.gameObject);
            }
            finally
            {
                if (wall != null) Object.DestroyImmediate(wall);
                Object.DestroyImmediate(room);
            }
        }

        private static SpriteRenderer BodyOf(GameObject enemy)
        {
            var artwork = enemy.GetComponent<TrickalFanGame.Frontend.FairyKingdomArtworkView>();
            if (artwork != null && artwork.Visual != null) return artwork.Visual;
            SpriteRenderer own = enemy.GetComponent<SpriteRenderer>();
            if (own != null && own.sprite != null) return own;
            foreach (SpriteRenderer candidate in enemy.GetComponentsInChildren<SpriteRenderer>())
                if (candidate.sprite != null) return candidate;
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
