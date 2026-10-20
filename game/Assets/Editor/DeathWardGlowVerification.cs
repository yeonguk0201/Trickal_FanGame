using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // 레비의 단도 invulnerability display (DeathWardGlow) and the grey spent icon. The ward's rules (cancelled hit,
    // 5 s invulnerability, enemy pass-through only during the hit's own window) stay in Week23Artifact2Verification.
    public static class DeathWardGlowVerification
    {
        public const string PlayerSheet = "Characters/Erpin_Walking";
        public const string PlayerFrame = "Erpin_Walk_Down_0";
        public const float Duration = 5f;

        [MenuItem("Trickal Fan Game/Effects/Verify Dagger Ward Glow With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            UserArtworkVerification.Verify();
            Week23Artifact2Verification.Verify();
            Debug.Log("Dagger ward glow regressions passed: glow, grey icon, item artwork and Artifact-2 rules.");
        }

        public static Sprite LoadPlayerSprite() =>
            Resources.LoadAll<Sprite>(PlayerSheet).FirstOrDefault(sprite => sprite.name == PlayerFrame);

        [MenuItem("Trickal Fan Game/Effects/Verify Dagger Ward Glow")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("DeathWard");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "The dagger ward shader is missing or failed compilation.");
            Sprite sprite = LoadPlayerSprite();
            Require(sprite != null, $"{PlayerFrame} must stay in Resources/{PlayerSheet}.");
            var player = new GameObject("Dagger ward verification player").AddComponent<SpriteRenderer>();
            try
            {
                player.sprite = sprite;
                player.sortingOrder = 20;
                Require(DeathWardGlow.Play(player, 0f, Duration) == null, "Edit Mode must not start the runtime glow.");
                Random.InitState(4747);
                Random.State randomBefore = Random.state;
                DeathWardGlow glow = DeathWardGlow.Create(player, Duration);
                Require(glow != null && DeathWardGlow.Create(player, Duration) == glow &&
                        player.GetComponentsInChildren<MeshRenderer>(true).Length == 3 + DeathWardGlow.SparkleCount,
                    "The glow must be created once per player with an outline, a flash, a ring and the sparkles.");
                VerifyOutlineTiming();
                VerifyOutlineGeometry(glow, player, sprite);
                VerifyBurst(glow);
                glow.RenderAt(Duration + 0.01f);
                Require(player.GetComponentsInChildren<MeshRenderer>().All(renderer => !renderer.enabled),
                    "Nothing may show after the invulnerability ends.");
                player.enabled = false;
                glow.RenderAt(1f);
                Require(!glow.Outline.enabled, "A hidden player drawing must hide the outline.");
                Require(Random.state.Equals(randomBefore), "The dagger ward glow must not consume UnityEngine.Random.");
                glow.Stop();
                Require(player.GetComponent<DeathWardGlow>() == null && player.transform.childCount == 0,
                    "Stopping the glow must remove all of its objects.");
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
            VerifySpentIcon();
            Debug.Log("Dagger ward glow verification passed.");
        }

        private static void VerifyOutlineTiming()
        {
            Require(DeathWardGlow.OutlineOpacityAt(-0.01f, Duration) == 0f &&
                    DeathWardGlow.OutlineOpacityAt(Duration, Duration) == 0f &&
                    DeathWardGlow.OutlineOpacityAt(0.01f, Duration) < DeathWardGlow.OutlineOpacityAt(0.1f, Duration),
                "The outline must fade in at the start and be gone when the invulnerability ends.");
            for (float t = DeathWardGlow.FadeIn; t < Duration - DeathWardGlow.WarningTime; t += 0.01f)
                Require(DeathWardGlow.OutlineOpacityAt(t, Duration) >= DeathWardGlow.MaxOpacity * 0.69f,
                    $"The outline must stay clearly visible before the warning ({t:0.00}s).");
            int dips = 0;
            bool low = false;
            float peak = 0f;
            for (float t = Duration - DeathWardGlow.WarningTime; t < Duration; t += 0.002f)
            {
                float opacity = DeathWardGlow.OutlineOpacityAt(t, Duration);
                peak = Mathf.Max(peak, opacity);
                bool dark = opacity < DeathWardGlow.MaxOpacity * 0.35f;
                if (dark && !low) dips++;
                low = dark;
            }
            Require(dips >= 5 && peak > DeathWardGlow.MaxOpacity * 0.9f,
                $"The outline must blink fast in the last second ({dips} dips).");
        }

        private static void VerifyOutlineGeometry(DeathWardGlow glow, SpriteRenderer player, Sprite sprite)
        {
            glow.RenderAt(1f);
            Require(glow.Outline.enabled && glow.Outline.sortingOrder == player.sortingOrder + 1 &&
                    glow.Outline.sortingLayerID == player.sortingLayerID,
                "The outline must draw just over the player drawing.");
            Mesh mesh = glow.Outline.GetComponent<MeshFilter>().sharedMesh;
            // The whole frame rect, not sprite.bounds: a Tight sprite mesh can report a smaller box.
            Vector2 frameSize = sprite.rect.size / sprite.pixelsPerUnit;
            Bounds drawing = new((Vector3)((sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit),
                frameSize);
            Bounds outline = mesh.bounds;
            float pad = DeathWardGlow.OutlineWidth;
            Require(outline.min.x <= drawing.min.x - pad && outline.max.x >= drawing.max.x + pad &&
                    outline.min.y <= drawing.min.y - pad && outline.max.y >= drawing.max.y + pad &&
                    outline.size.x <= drawing.size.x + pad * 4f,
                "The outline quad must cover the drawing plus room for the outline, and not much more.");
            var block = new MaterialPropertyBlock();
            glow.Outline.GetPropertyBlock(block);
            Texture texture = sprite.texture;
            Vector4 rect = block.GetVector("_UvRect");
            Require(block.GetTexture("_MainTex") == texture &&
                    Mathf.Abs(rect.x - sprite.rect.xMin / texture.width) < 1e-4f &&
                    Mathf.Abs(rect.w - sprite.rect.yMax / texture.height) < 1e-4f,
                "The outline must sample only the current frame of the player sheet.");
            float right = mesh.vertices.Max(vertex => vertex.x);
            player.flipX = true;
            glow.RenderAt(1f);
            float flippedLeft = glow.Outline.GetComponent<MeshFilter>().sharedMesh.vertices.Min(vertex => vertex.x);
            player.flipX = false;
            Require(Mathf.Abs(flippedLeft + right) < 1e-4f, "The outline must mirror with a flipped drawing.");
        }

        private static void VerifyBurst(DeathWardGlow glow)
        {
            bool ringReached = false;
            for (float t = 0f; t <= DeathWardGlow.BurstDuration + 0.0001f; t += 0.005f)
            {
                glow.RenderAt(t);
                Require(glow.RingOuterRadius <= glow.Reach + 1e-4f && glow.FarthestSparkle <= glow.Reach + 1e-4f,
                    $"The burst must stay inside its reach ({t:0.000}s).");
                ringReached |= glow.RingOuterRadius >= glow.Reach * 0.97f;
                Require(glow.Flash.enabled == t < DeathWardGlow.FlashEnd, $"The flash must last {DeathWardGlow.FlashEnd} s.");
                if (glow.Ring.enabled)
                    Require(glow.Ring.sortingOrder == glow.Source.sortingOrder - 1,
                        "The ring must stay behind the player drawing.");
                if (glow.Flash.enabled)
                    Require(glow.Flash.sortingOrder > glow.Source.sortingOrder &&
                            glow.Sparkles.Where(sparkle => sparkle.enabled)
                                .All(sparkle => sparkle.sortingOrder > glow.Source.sortingOrder),
                        "The flash and sparkles must draw over the player.");
            }
            Require(ringReached, "The ring must open to its full reach.");
            glow.RenderAt(0.2f);
            Require(glow.Sparkles.Count(sparkle => sparkle.enabled) == DeathWardGlow.SparkleCount,
                "Every sparkle must fly in the middle of the burst.");
            glow.RenderAt(DeathWardGlow.BurstDuration);
            Require(!glow.Flash.enabled && !glow.Ring.enabled && glow.Sparkles.All(sparkle => !sparkle.enabled) &&
                    glow.Outline.enabled, "Only the outline may stay after the burst.");
        }

        private static void VerifySpentIcon()
        {
            Material spent = UserArtwork.ItemMaterial(false, true);
            Require(spent != null && spent != UserArtwork.ItemMaterial(false) &&
                    Mathf.Approximately(spent.GetFloat("_Spent"), 1f) &&
                    Mathf.Approximately(UserArtwork.ItemMaterial(false).GetFloat("_Spent"), 0f),
                "Spent artifacts must use their own grey item material.");
            ItemDefinition dagger = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                Week23Artifact2Setup.ItemPath(Week23Artifact2Setup.DaggerId));
            Require(dagger != null, "레비의 단도 must exist.");
            var slotObject = new GameObject("Dagger slot verification", typeof(RectTransform), typeof(Image));
            try
            {
                Image icon = slotObject.GetComponent<Image>();
                ArtifactHudSlotView slot = slotObject.AddComponent<ArtifactHudSlotView>();
                slot.ConfigureVisuals(icon, null, null, null);
                slot.Bind(dagger, 1);
                Material ready = icon.material;
                Color readyColor = icon.color;
                slot.Bind(dagger, 1, true);
                Require(slot.IsSpent && (icon.sprite != null
                        ? icon.material == spent && ready == UserArtwork.ItemMaterial(false)
                        : icon.color == UserArtwork.SpentPlaceholderColor && readyColor != icon.color),
                    "The HUD must show a spent dagger grey.");
            }
            finally
            {
                Object.DestroyImmediate(slotObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[DeathWardGlowVerification] " + message);
        }
    }
}
