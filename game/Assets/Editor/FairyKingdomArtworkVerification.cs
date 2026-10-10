using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class FairyKingdomArtworkVerification
    {
        public static void VerifyWithCombatRegressionsBatch()
        {
            Verify();
            Week16Artifact1Verification.Verify();
            Week23Artifact2Verification.Verify();
            Week23Passive4Verification.Verify();
            Debug.Log("Fairy Kingdom artwork combat regressions passed: lightning, burn/shock and water-stream rules.");
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Fairy Kingdom 45 Sprites")]
        public static void Verify()
        {
            FairyKingdomArtworkCatalog catalog = AssetDatabase.LoadAssetAtPath<FairyKingdomArtworkCatalog>(
                FairyKingdomArtworkSetup.CatalogPath);
            Require(catalog != null && catalog.Entries.Count == 45, "Missing 45-entry build catalog.");
            Require(catalog.Entries.Select(entry => entry.id).Distinct().Count() == 45, "Duplicate artwork IDs.");
            foreach (var entry in catalog.Entries)
            {
                Require(entry.sprite != null && entry.visibleRect.width > 0 && entry.visibleRect.height > 0,
                    "Missing sprite or visible bounds: " + entry.id);
                Sprite sprite = FairyKingdomArtwork.Fit(entry.id, 1.25f);
                Require(sprite != null && Mathf.Abs(sprite.bounds.size.x - 1.25f) < 0.001f,
                    "Visible-size fit failed: " + entry.id);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    FairyKingdomArtworkSetup.DisplayRoot + "/" + entry.id + ".prefab");
                Require(prefab != null && prefab.GetComponent<FairyKingdomArtworkView>()?.ArtworkId == entry.id,
                    "Missing display prefab binding: " + entry.id);
                Require(prefab.GetComponentInChildren<Collider2D>() == null && prefab.GetComponent<Health>() == null,
                    "Artwork previews must not invent gameplay: " + entry.id);
            }
            foreach (string name in new[] { "ChargingEnemy", "RangedEnemy", "QuickRangedFairy", "HighBloodSugarFairy" })
                VerifyKeepsOriginalArt(name);
            VerifyExisting("JyubiEnemy", "jyubi");
            VerifyExisting("KeyPickup", "pickup-key");
            VerifyExisting("BombPickup", "pickup-bomb");
            VerifyExisting("SecretPit", "terrain-secret-pit");
            VerifyExisting("RoomPit", "terrain-pit");
            VerifyCrumbs();
            VerifyObstacleChanges();
            VerifyTrees();
            VerifyChestChanges();
            VerifyBombChanges();
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(FairyKingdomArtworkSetup.PreviewScenePath) != null,
                "Missing Unity artwork preview scene.");
            Debug.Log("Fairy Kingdom artwork verification passed: 45 sprites/display prefabs; 3 crumb variants; " +
                "dynamic obstacle kinds, 6 chest states, bomb/explosion; collider and root scale preserved. " +
                "Play art review and animation frames remain pending.");
        }

        private static void VerifyExisting(string name, string expected)
        {
            GameObject root = Instantiate(name);
            try
            {
                FairyKingdomArtworkView view = root.GetComponent<FairyKingdomArtworkView>();
                Require(view != null && view.ArtworkId == expected, "Wrong binding: " + name);
                Vector3 rootScale = root.transform.localScale;
                Collider2D collider = root.GetComponent<Collider2D>();
                Bounds oldBounds = collider != null ? collider.bounds : default;
                Vector2 oldOffset = collider != null ? collider.offset : default;
                view.Refresh();
                Require(view.Visual.sprite != null && view.Visual.sprite.name == expected, "Wrong sprite: " + name);
                Require(root.transform.localScale == rootScale &&
                    (collider == null || (collider.bounds == oldBounds && collider.offset == oldOffset)),
                    "Artwork changed gameplay geometry: " + name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyKeepsOriginalArt(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            Require(prefab != null, "Missing existing gameplay prefab: " + name);
            Require(prefab.GetComponent<FairyKingdomArtworkView>() == null,
                "Draft artwork must not replace existing enemy art: " + name);
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Require(renderer != null && renderer.sprite != null && !AssetDatabase.GetAssetPath(renderer.sprite)
                    .StartsWith(FairyKingdomArtworkSetup.ArtRoot + "/", StringComparison.Ordinal),
                "Enemy lost its existing sprite: " + name);
            foreach (Behaviour animator in prefab.GetComponents<Behaviour>())
                if (animator is TrickalFanGame.Enemy.EnemyMovementAnimator ||
                    animator is TrickalFanGame.Enemy.EnemyAttackArtwork)
                    Require(animator.enabled, "Existing enemy animation is disabled: " + name);
        }

        private static void VerifyCrumbs()
        {
            GameObject root = Instantiate("BuseureogiCrumbMinion");
            try
            {
                FairyKingdomArtworkView view = root.GetComponent<FairyKingdomArtworkView>();
                Require(view != null && view.Mode == FairyKingdomArtworkMode.Crumb, "Missing crumb death artwork.");
                foreach (string color in new[] { "pink", "cream", "chocolate" })
                {
                    view.SetArtwork("crumb-minion-" + color);
                    Require(view.Visual.sprite.name == "crumb-minion-" + color, "Missing crumb variant.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyObstacleChanges()
        {
            GameObject root = Instantiate("DestructibleObstacle");
            try
            {
                var view = root.GetComponent<FairyKingdomArtworkView>();
                var obstacle = root.GetComponent<DestructibleObstacle>();
                Require(view != null, "Destructible obstacle is not bound.");
                foreach (string guid in AssetDatabase.FindAssets("t:ObstacleVariantDefinition"))
                {
                    var variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (FairyKingdomArtwork.Fit("obstacle-" + variant.VariantId, 1f) == null) continue;
                    obstacle.ApplyVariant(variant);
                    view.Refresh();
                    Require(view.Visual.sprite.name == "obstacle-" + variant.VariantId,
                        "Seeded obstacle did not switch artwork: " + variant.VariantId);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyTrees()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!prefab.GetComponentsInChildren<DestructibleObstacle>(true)
                        .Any(value => value.VariantId == DestructibleObstacle.TreeVariantId)) continue;
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    foreach (DestructibleObstacle tree in instance.GetComponentsInChildren<DestructibleObstacle>(true))
                    {
                        if (tree.VariantId != DestructibleObstacle.TreeVariantId) continue;
                        count++;
                        var view = tree.GetComponent<FairyKingdomArtworkView>();
                        var box = tree.GetComponent<BoxCollider2D>();
                        Require(view != null && box != null, "Tree artwork or collider missing.");
                        view.Refresh();
                        Physics2D.SyncTransforms();
                        Require(tree.GetComponentsInChildren<Collider2D>(true).Length == 1 &&
                            box.size == Vector2.one && box.offset == Vector2.zero && !box.isTrigger &&
                            tree.transform.localScale == Vector3.one, "Tree must only collide on its lower cell.");
                        Vector3 size = view.Visual.bounds.size;
                        Require(Mathf.Abs(size.x - 1f) < 0.001f && Mathf.Abs(size.y - 2f) < 0.001f &&
                            Mathf.Abs(view.Visual.bounds.min.y - box.bounds.min.y) < 0.001f,
                            "Tree must draw 1x2 cells from the bottom of its 1x1 collider.");
                        Require(!box.OverlapPoint((Vector2)tree.transform.position + Vector2.up),
                            "Tree canopy cell must remain passable.");
                    }
                }
                finally { Object.DestroyImmediate(instance); }
            }
            Require(count > 0, "No authored trees found for geometry verification.");
        }

        private static void VerifyChestChanges()
        {
            foreach (ChestKind kind in Enum.GetValues(typeof(ChestKind)))
            {
                GameObject root = Instantiate("TreasureChest");
                try
                {
                    var chest = root.GetComponent<TreasureChest>();
                    var view = root.GetComponent<FairyKingdomArtworkView>();
                    Require(view != null, "Chest is not bound.");
                    chest.Configure("artwork-test", kind);
                    var state = new RoomRunState("artwork-room");
                    ChestRunState record = state.RegisterChest(chest.ChestId, kind);
                    typeof(TreasureChest).GetField("record", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(chest, record);
                    view.Refresh();
                    Require(view.Visual.sprite.name == "chest-" + kind.ToString().ToLowerInvariant() + "-closed",
                        "Closed chest artwork mismatch.");
                    float baseY = view.Visual.sprite.bounds.min.y;
                    state.TryOpenChest(chest.ChestId);
                    view.Refresh();
                    Require(view.Visual.sprite.name == "chest-" + kind.ToString().ToLowerInvariant() + "-open",
                        "Opened chest artwork mismatch.");
                    Require(Mathf.Abs(view.Visual.sprite.bounds.min.y - baseY) < 0.001f,
                        "Opening chest must preserve its floor baseline.");
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        private static void VerifyBombChanges()
        {
            GameObject root = Instantiate("PlacedBomb");
            try
            {
                var view = root.GetComponent<FairyKingdomArtworkView>();
                Require(view != null, "Bomb is not bound.");
                view.Refresh();
                Require(view.Visual.sprite.name == "pickup-bomb", "Bomb armed sprite mismatch.");
                // State-only presentation check: do not detonate a bomb against the user's open scene.
                typeof(PlacedBomb).GetField("hasExploded", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(root.GetComponent<PlacedBomb>(), true);
                view.Refresh();
                Require(view.Visual.sprite.name == "effect-bomb-explosion", "Bomb explosion sprite mismatch.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Instantiate(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            Require(prefab != null, "Missing existing gameplay prefab: " + name);
            return Object.Instantiate(prefab);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
