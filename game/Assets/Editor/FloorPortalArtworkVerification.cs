using System;
using System.Reflection;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class FloorPortalArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Boss Room Portal")]
        public static void Verify()
        {
            GameObject root = new("Portal verification");
            root.transform.position = new Vector3(1000f, 1000f, 0f);
            try
            {
                root.AddComponent<BoxCollider2D>().isTrigger = true;
                RoomController room = root.AddComponent<RoomController>();
                GameObject portalObject = new("Portal");
                portalObject.transform.SetParent(root.transform, false);
                var trigger = portalObject.AddComponent<BoxCollider2D>();
                var visual = portalObject.AddComponent<SpriteRenderer>();
                var portal = portalObject.AddComponent<FloorAdvancePortal>();
                portal.Configure(null, room, 2, visual);
                Require(!visual.enabled && !trigger.enabled, "Portal must be entirely hidden before clear.");
                Require(visual.sprite != null, "Portal sheet was not loaded.");
                var frames = (Sprite[])Field("frames").GetValue(portal);
                Require(frames.Length == 4 && frames[0].rect.y > frames[2].rect.y,
                    "Portal must contain four frames in top-left reading order.");
                room.BindRunState(new RoomRunState("portal-test"), true);
                portal.Configure(null, room, 2, visual);
                Require(portal.IsUnlocked && visual.color.a == 0f, "Clear must start from transparent.");
                Field("visibleTime").SetValue(portal, FloorAdvancePortal.RevealDuration / 2f);
                typeof(FloorAdvancePortal).GetMethod("ApplyVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(portal, null);
                Require(visual.color.a > 0f && visual.color.a < 1f, "Reveal must fade gradually.");
                GameObject drop = new("Overlapping drop");
                drop.transform.SetParent(root.transform, false);
                var dropCollider = drop.AddComponent<CircleCollider2D>();
                dropCollider.radius = 0.25f;
                dropCollider.isTrigger = true;
                drop.AddComponent<ItemPickup>();
                portal.ClearOverlappingPickups();
                Require(Vector2.Distance(drop.transform.position, portal.transform.position) >
                    FloorAdvancePortal.PickupClearanceRadius + 0.25f, "Drop must leave portal range.");
                Vector3 displaced = drop.transform.position;
                portal.ClearOverlappingPickups();
                Require(drop.transform.position == displaced, "Cleared drops must remain stable.");
                // A later spawn at the center must also be displaced.
                drop.transform.position = portal.transform.position;
                portal.ClearOverlappingPickups();
                Require(drop.transform.position != portal.transform.position, "Late drops must be displaced.");
                Debug.Log("Boss room portal verification passed: hidden, four frames, fade, pickup clearance.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week16Reward3Verification.Verify();
        }

        private static FieldInfo Field(string name) => typeof(FloorAdvancePortal)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
