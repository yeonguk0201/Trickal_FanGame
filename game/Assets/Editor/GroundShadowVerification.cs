using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class GroundShadowVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Common Ground Shadows")]
        public static void Verify()
        {
            VerifyTraps();
            GameObject root = new("Ground shadow verification");
            try
            {
                GameObject tree = Body(root.transform, "Two-cell tree");
                var box = tree.GetComponent<BoxCollider2D>();
                var drawing = tree.GetComponentInChildren<SpriteRenderer>();
                drawing.transform.localPosition = Vector3.up * 0.5f;
                drawing.transform.localScale = new Vector3(1f, 2f, 1f);
                GroundShadow shadow = GroundShadow.Ensure(tree);
                Physics2D.SyncTransforms();
                Bounds colliderBefore = box.bounds;
                shadow.Refresh();
                Require(shadow.Visual != null && shadow.Visual.enabled, "Missing common shadow.");
                Require(GroundShadow.Ensure(tree) == shadow && tree.GetComponents<GroundShadow>().Length == 1 &&
                    tree.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                    "Repeated attachment created duplicate shadows.");
                Require(box.bounds == colliderBefore && box.size == Vector2.one && box.offset == Vector2.zero &&
                    tree.transform.localScale == Vector3.one, "Shadow changed tree collision geometry.");
                Require(shadow.Visual.transform.parent == tree.transform &&
                    shadow.Visual.transform.parent != drawing.transform &&
                    Mathf.Abs(shadow.Visual.bounds.size.x - 0.95f) < 0.001f &&
                    Mathf.Abs(shadow.Visual.transform.position.y - (box.bounds.min.y + 0.08f)) < 0.001f,
                    "Tree shadow must follow its lower cell, independently of its two-cell artwork.");
                Require(shadow.Visual.sortingOrder > -100 && shadow.Visual.sortingOrder < drawing.sortingOrder &&
                    shadow.Visual.GetComponent<Collider2D>() == null, "Shadow must draw on floor without collision.");
                Vector3 shadowPosition = shadow.Visual.transform.position;
                drawing.transform.localPosition += Vector3.up;
                drawing.color = Color.red;
                drawing.forceRenderingOff = true; // Mesh animation replaces the source sprite.
                shadow.Refresh();
                Require(shadow.Visual.enabled && shadow.Visual.color.r == 0f &&
                    shadow.Visual.transform.position == shadowPosition,
                    "Body animation or hit tint must not move or recolor the ground shadow.");

                tree.transform.position += new Vector3(2f, 3f, 0f);
                tree.transform.localScale = Vector3.one * 2f;
                Physics2D.SyncTransforms();
                shadow.Refresh();
                Require(Mathf.Abs(shadow.Visual.bounds.size.x - 1.9f) < 0.001f &&
                    Mathf.Abs(shadow.Visual.transform.position.x - box.bounds.center.x) < 0.001f,
                    "Moving/scaling a body must update its footprint once, without double scaling.");
                drawing.enabled = false;
                shadow.Refresh();
                Require(!shadow.Visual.enabled, "Hidden bodies must not leave visible shadows.");
                drawing.enabled = true;

                GameObject actor = Body(root.transform, "Actor");
                Health health = actor.AddComponent<Health>();
                health.ResetHealth();
                GroundShadow actorShadow = GroundShadow.Ensure(actor);
                actorShadow.Refresh();
                Require(actorShadow.Visual.sprite == shadow.Visual.sprite, "Shadows must share one sprite/texture.");
                actor.AddComponent<Rigidbody2D>().gravityScale = 0f;
                PlayerFlight flight = actor.AddComponent<PlayerFlight>();
                Require(flight.TryStartFlying(), "Flight probe did not start.");
                actorShadow.Refresh();
                Require(!actorShadow.Visual.enabled && flight.Shadow != null &&
                    flight.Shadow.sprite == GroundShadow.SharedSprite,
                    "Flight must reuse the common mask and suppress the grounded shadow.");
                health.TakeDamage(health.MaxHealth);
                actorShadow.Refresh();
                Require(!actorShadow.Visual.enabled, "Dead actors must not leave common shadows.");

                GameObject mortal = Body(root.transform, "Mortal actor");
                Health mortalHealth = mortal.AddComponent<Health>();
                mortalHealth.ResetHealth();
                GroundShadow mortalShadow = GroundShadow.Ensure(mortal);
                mortalShadow.Refresh();
                Require(mortalShadow.Visual.enabled, "Living actor shadow missing.");
                mortalHealth.TakeDamage(mortalHealth.MaxHealth);
                mortalShadow.Refresh();
                Require(!mortalShadow.Visual.enabled, "Dead actor shadow must disappear.");
                Debug.Log("Common ground shadow verification passed: shared mask, idempotent attachment, " +
                    "lower tree footprint, sorting, body-scale/movement, independent animation/tint, death and flight.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyTraps()
        {
            foreach (TrickalFanGame.Room.RoomTrapKind kind in Enum.GetValues(typeof(TrickalFanGame.Room.RoomTrapKind)))
            {
                GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week25Trap0Setup.PrefabPath(kind)));
                try
                {
                    var trap = root.GetComponent<TrickalFanGame.Room.RoomTrap>();
                    root.GetComponent<FairyKingdomArtworkView>().Refresh();
                    Vector2 hitSize = trap.HitSize;
                    Collider2D collider = root.GetComponent<Collider2D>();
                    Physics2D.SyncTransforms();
                    Bounds collision = collider != null ? collider.bounds : default;
                    GroundShadow shadow = GroundShadow.Ensure(root);
                    shadow.Refresh();
                    Require(shadow.Visual != null && shadow.Visual.enabled &&
                        shadow.Visual.sortingOrder < trap.Visual.sortingOrder, "Trap shadow missing/above artwork: " + kind);
                    Require(GroundShadowProfiles.ResolveId(root, null) == TrickalFanGame.Room.RoomTrap.ArtworkIdOf(kind),
                        "Trap must select its individual profile: " + kind);
                    Bounds contact = shadow.Visual.bounds;
                    if (kind == TrickalFanGame.Room.RoomTrapKind.PeriodicSpikes)
                    {
                        trap.RefreshPeriodicArtwork(TrickalFanGame.Room.PeriodicSpikeState.Lowered); shadow.Refresh();
                        Require(shadow.Visual.bounds == contact, "Lowered spikes moved the shadow.");
                        trap.RefreshPeriodicArtwork(TrickalFanGame.Room.PeriodicSpikeState.Raised); shadow.Refresh();
                        Require(shadow.Visual.bounds == contact, "Raised spikes moved the shadow.");
                    }
                    Require(trap.HitSize == hitSize && (collider == null ? root.GetComponent<Collider2D>() == null : collider.bounds == collision),
                        "Shadow changed trap hit area/collision: " + kind);
                    Require(GroundShadow.Ensure(root) == shadow && root.GetComponents<GroundShadow>().Length == 1,
                        "Trap shadow attachment duplicated components: " + kind);
                    root.SetActive(false);
                    Require(!shadow.Visual.gameObject.activeInHierarchy, "Disabled trap left a visible shadow.");
                }
                finally { Object.DestroyImmediate(root); }
            }
            Debug.Log("Trap ground shadows passed: seven profiles, stable spike states, unchanged hit areas/colliders, idempotent attachment and disable.");
        }

        private static GameObject Body(Transform parent, string name)
        {
            GameObject body = new(name, typeof(BoxCollider2D));
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(600f, 600f, 0f);
            GameObject child = new("Visual", typeof(SpriteRenderer));
            child.transform.SetParent(body.transform, false);
            child.GetComponent<SpriteRenderer>().sprite = GroundShadow.SharedSprite;
            return body;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
