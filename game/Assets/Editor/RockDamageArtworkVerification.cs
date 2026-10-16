using System;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class RockDamageArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Rock Damage Frames")]
        public static void Verify()
        {
            foreach (string id in new[] { "obstacle-rock", "obstacle-gold-rock", "trap-spiked-rock" })
                VerifyRock(id);
            Require(!RockDamageArtwork.Supports("obstacle-tree") && !RockDamageArtwork.Supports("trap-fire"),
                "Non-rock artwork must keep its existing presentation.");
            Require(RockDamageArtwork.Stage(0, 4) == 0 && RockDamageArtwork.Stage(1, 4) == 1 &&
                RockDamageArtwork.Stage(2, 4) == 2 && RockDamageArtwork.Stage(3, 4) == 3,
                "Four-hit stage mapping changed.");
            Debug.Log("Rock damage artwork verification passed: 3 kinds, 9 frames, white tint, stable ground " +
                "baseline/collider, fourth-hit destruction. Play art review remains separate.");
        }

        private static void VerifyRock(string id)
        {
            GameObject root = new("Rock damage verification - " + id);
            try
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.85f, 0.65f);
                var renderer = root.AddComponent<SpriteRenderer>();
                var obstacle = root.AddComponent<DestructibleObstacle>();
                obstacle.Configure("damage-probe", 4, null, renderer);
                if (id == "obstacle-gold-rock")
                {
                    var variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(
                        "Assets/Rooms/ObstacleVariants/gold-rock.asset");
                    Require(variant != null, "Missing gold rock variant.");
                    obstacle.ApplyVariant(variant);
                }
                if (id == "trap-spiked-rock")
                    root.AddComponent<RoomTrap>().Configure("damage-probe", RoomTrapKind.SpikedRock, false, renderer);
                var view = root.AddComponent<FairyKingdomArtworkView>();
                view.Configure(id, FairyKingdomArtworkMode.Obstacle, renderer, 1f);
                view.Refresh();
                Sprite intact = renderer.sprite;
                Require(intact != null, "Missing intact art: " + id);
                var seen = new System.Collections.Generic.HashSet<Sprite> { intact };
                for (int hit = 1; hit <= 3; hit++)
                {
                    Require(obstacle.RegisterPlayerHit(), "Hit rejected: " + id);
                    view.Refresh();
                    Sprite sprite = renderer.sprite;
                    Require(sprite != null && sprite.name == id + "-damage-" + hit && seen.Add(sprite),
                        "Missing/distinct damage frame: " + id + "/" + hit);
                    Require(renderer.color == Color.white, "Damage tint remains: " + id);
                    Require(Mathf.Abs(sprite.bounds.size.x - 1f) < 0.001f &&
                        Mathf.Abs(sprite.bounds.min.y - intact.bounds.min.y) < 0.001f,
                        "Frame changed width/ground baseline: " + id);
                    Require(collider.size == new Vector2(0.85f, 0.65f) && collider.enabled &&
                        root.transform.localScale == Vector3.one && !obstacle.IsBroken,
                        "Damage art changed gameplay geometry or broke early: " + id);
                    view.Refresh();
                    Require(renderer.sprite == sprite, "Frame cache failed: " + id);
                }
                // Remove the variant drop table from this isolated fourth-hit probe.
                obstacle.Configure("damage-probe", 4, null, renderer);
                Require(obstacle.RegisterPlayerHit() && obstacle.IsBroken && !root.activeSelf,
                    "Fourth-hit destruction changed: " + id);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
