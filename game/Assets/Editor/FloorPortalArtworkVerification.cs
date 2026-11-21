using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
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
                Require(visual.sharedMaterial.shader.name == "TrickalFanGame/PortalInterior" &&
                    !ShaderUtil.ShaderHasError(visual.sharedMaterial.shader), "Portal shader is missing or invalid.");
                PortalAurora aurora = portal.GetComponent<PortalAurora>();
                Require(aurora != null && aurora.Glow != null && !aurora.Glow.enabled,
                    "Aurora must exist but remain hidden before clear.");
                Require(aurora.Glow.sortingLayerID == visual.sortingLayerID &&
                    aurora.Glow.sortingOrder < visual.sortingOrder &&
                    !ShaderUtil.ShaderHasError(aurora.Glow.sharedMaterial.shader),
                    "Aurora must render behind the portal with a valid shader.");
                Sprite fixedSprite = visual.sprite;
                Bounds fixedBounds = visual.bounds;
                for (int sample = 0; sample < 16; sample++)
                {
                    Field("visibleTime").SetValue(portal, sample * 0.125f);
                    typeof(FloorAdvancePortal).GetMethod("ApplyVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(portal, null);
                    Require(visual.sprite == fixedSprite && visual.bounds == fixedBounds,
                        "Portal silhouette must not switch frames or move.");
                }
                room.BindRunState(new RoomRunState("portal-test"), true);
                portal.Configure(null, room, 2, visual);
                Require(portal.IsUnlocked && visual.color.a == 0f, "Clear must start from transparent.");
                Field("visibleTime").SetValue(portal, FloorAdvancePortal.RevealDuration / 2f);
                typeof(FloorAdvancePortal).GetMethod("ApplyVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(portal, null);
                Require(visual.color.a > 0f && visual.color.a < 1f, "Reveal must fade gradually.");
                Require(aurora.Glow.enabled && aurora.Glow.sharedMaterial.GetFloat("_Opacity") > 0f,
                    "Aurora must fade in with the portal.");
                Require(portal.GetComponentsInChildren<MeshRenderer>().Length == 1,
                    "Reconfiguration must not duplicate aurora layers.");
                // Portal-E: no solid body, a trigger zone for the E prompt, and drawn over a player behind its base.
                Require(trigger.isTrigger && trigger.size == FloorAdvancePortal.InteractionZoneSize &&
                        trigger.offset == FloorAdvancePortal.InteractionZoneOffset &&
                        portal.GetComponents<Collider2D>().All(shape => shape.isTrigger),
                    "Portal must only carry its E interaction trigger, with no solid collider.");
                Require(FloorAdvancePortal.EnterPrompt == "[E] 이동", "Portal prompt must read [E] 이동.");
                Require(portal.BaseY < portal.transform.position.y && portal.BaseY > visual.bounds.min.y,
                    "Portal base must sit inside the bottom half of the drawn frame.");
                Require(FloorAdvancePortal.SortingOrderFor(false, false) < PlayerMovement.BodySortingOrder &&
                        FloorAdvancePortal.SortingOrderFor(true, false) > PlayerMovement.BodySortingOrder &&
                        FloorAdvancePortal.SortingOrderFor(true, true) >
                        PlayerMovement.BodySortingOrder + PlayerFlight.FlyingSortingOrderBoost,
                    "Portal must draw under a player in front and over a walking or flying player behind.");
                Require(!portal.IsPlayerInRange && !portal.CanInteract,
                    "Portal must not offer entry before the player stands in its zone.");
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

        public static void VerifyAndCaptureBatch()
        {
            Verify();
            var preview = new PreviewRenderUtility();
            Texture2D capture = null;
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameObject root = new("Portal capture");
                root.AddComponent<BoxCollider2D>();
                var sprite = root.AddComponent<SpriteRenderer>();
                root.AddComponent<FloorAdvancePortal>().Configure(null, null, 2, sprite);
                preview.AddSingleGO(root);
                sprite.enabled = true;
                sprite.color = Color.white;
                sprite.sharedMaterial.SetFloat("_PortalPhase", 1.3f);
                root.GetComponent<PortalAurora>().Evaluate(1.3f, 1f);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = 2.8f;
                preview.camera.transform.position = new Vector3(0, 0, -10);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.16f, 0.24f, 0.12f);
                preview.BeginPreview(new Rect(0,0,512,512), GUIStyle.none);
                preview.camera.Render();
                Texture rendered = preview.EndPreview();
                target = RenderTexture.GetTemporary(512,512,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(rendered, target);
                RenderTexture.active = target;
                capture = new Texture2D(512,512,TextureFormat.RGBA32,false);
                capture.ReadPixels(new Rect(0,0,512,512),0,0);
                capture.Apply();
                // Inspect the rendered result, not just shader compilation: no rectangular halo fringe.
                Renderer glow = root.GetComponent<PortalAurora>().Glow;
                Vector4 radius = glow.sharedMaterial.GetVector("_PortalRadius");
                Vector4 size = glow.sharedMaterial.GetVector("_GlowSize");
                float cutoff = Mathf.Min(size.x / (2f * radius.x), size.y / (2f * radius.y)) * 0.9f;
                Color[] pixels = capture.GetPixels();
                Color background = pixels[0];
                for (int y = 0; y < 512; y += 4)
                    for (int x = 0; x < 512; x += 4)
                    {
                        Vector2 world = new Vector2((x + 0.5f) / 512f - 0.5f,
                            (y + 0.5f) / 512f - 0.5f) * (2f * preview.camera.orthographicSize);
                        Vector2 offset = world - (Vector2)glow.transform.position;
                        float radial = new Vector2(offset.x / radius.x, offset.y / radius.y).magnitude;
                        if (radial <= cutoff + 0.04f) continue;
                        Color pixel = pixels[y * 512 + x];
                        Require(Mathf.Abs(pixel.r - background.r) < 0.012f &&
                            Mathf.Abs(pixel.g - background.g) < 0.012f &&
                            Mathf.Abs(pixel.b - background.b) < 0.012f,
                            "Rendered light must vanish outside its ellipse, without rectangular fringes.");
                    }
                Debug.Log("Portal rendered ellipse boundary verification passed.");
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/portal-aurora-preview.png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                preview.Cleanup();
            }
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
