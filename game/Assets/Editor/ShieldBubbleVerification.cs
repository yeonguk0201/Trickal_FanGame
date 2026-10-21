using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // 날먹보호막 bubble (ShieldBubble) and how PlayerPowerEffects follows the power's part of the shield. The power's
    // rules (two hearts, refused at the limit) stay in Week27Power1Verification.
    public static class ShieldBubbleVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Shield Bubble With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week27Power1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Shield bubble regressions passed: bubble, Power-1 rules and development panel.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Shield Bubble")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("ShieldBubble");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "The shield bubble shader is missing or failed compilation.");
            Sprite sprite = DeathWardGlowVerification.LoadPlayerSprite();
            Require(sprite != null, "The Erpin frame must stay in Resources.");
            Random.InitState(6161);
            Random.State randomBefore = Random.state;
            VerifyView(sprite);
            VerifyTimings();
            Require(Random.state.Equals(randomBefore), "The shield bubble must not consume UnityEngine.Random.");
            VerifyPowerLink(sprite);
            Debug.Log("Shield bubble verification passed.");
        }

        private static void VerifyView(Sprite sprite)
        {
            var player = new GameObject("Shield bubble verification player").AddComponent<SpriteRenderer>();
            try
            {
                player.sprite = sprite;
                player.sortingOrder = 20;
                player.flipX = true;
                player.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                ShieldBubble bubble = ShieldBubble.Create(player);
                Require(bubble != null && ShieldBubble.Create(player) == bubble &&
                        player.GetComponentsInChildren<MeshRenderer>(true).Length == 1,
                    "The bubble must be created once per player.");
                Require(!bubble.Bubble.enabled, "The bubble must stay hidden until it is rendered.");
                bubble.RenderShield(1f, 1f, false, -1f);
                Require(bubble.Bubble.enabled && bubble.Bubble.sortingLayerID == player.sortingLayerID &&
                        bubble.Bubble.sortingOrder == player.sortingOrder + ShieldBubble.OrderOffset,
                    "The bubble must draw just over the player.");
                float spriteSize = Mathf.Max(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit;
                float worldRadius = spriteSize * ShieldBubble.RadiusShare * player.transform.lossyScale.x;
                Bounds bounds = bubble.Bubble.bounds;
                float bubbleRadius = bounds.extents.x * ShieldBubble.BubbleRadius;
                Require(Mathf.Abs(bounds.extents.x - bounds.extents.y) < 1e-3f &&
                        Mathf.Abs(bubbleRadius - worldRadius) < 1e-3f,
                    $"The bubble must be a circle around the whole drawing ({bubbleRadius} vs {worldRadius}).");
                Vector2 spriteCentre = (Vector2)player.transform.position +
                    (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit * 1.5f;
                Require(Vector2.Distance(bounds.center, spriteCentre) < 1e-3f,
                    "The bubble must be centred on the drawing.");
                Require(bubbleRadius >= spriteSize * 1.5f * 0.45f,
                    "The bubble must be large enough to hold the player.");
                Require(bubble.RippleProgress < 0f && bubble.PopProgress < 0f, "No ripple or pop may show by itself.");
                bubble.RenderShield(1f, 1f, false, ShieldBubble.RippleSeconds * 0.5f);
                Require(Mathf.Abs(bubble.RippleProgress - 0.5f) < 1e-4f, "A hit must start the ripple.");
                bubble.RenderShield(1f, 1f, false, ShieldBubble.RippleSeconds);
                Require(bubble.RippleProgress < 0f, "The ripple must end after its duration.");
                bubble.RenderPop(ShieldBubble.PopSeconds * 0.5f, 2f);
                Require(bubble.Bubble.enabled && Mathf.Abs(bubble.PopProgress - 0.5f) < 1e-4f,
                    "The pop must play after the shield is used up.");
                bubble.RenderPop(ShieldBubble.PopSeconds, 2f);
                Require(!bubble.Bubble.enabled, "The bubble must be gone after the pop.");
                bubble.RenderShield(1f, 1f, false, -1f);
                player.enabled = false;
                bubble.RenderShield(1f, 1.1f, false, -1f);
                Require(!bubble.Bubble.enabled, "The bubble must hide with the player's drawing.");
                bubble.Stop();
                Require(player.transform.childCount == 0, "Stopping the bubble must remove it.");
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        private static void VerifyTimings()
        {
            Require(ShieldBubble.OpacityAt(0f, 0f, false) == 0f &&
                    Mathf.Approximately(ShieldBubble.OpacityAt(ShieldBubble.AppearSeconds, 0f, false), 1f),
                "The bubble must fade in when the power is used.");
            Require(ShieldBubble.ScaleAt(0f) < 0.7f && ShieldBubble.ScaleAt(ShieldBubble.AppearSeconds * 0.7f) > 1f &&
                    Mathf.Approximately(ShieldBubble.ScaleAt(ShieldBubble.AppearSeconds), 1f),
                "The bubble must blow up with a small overshoot and settle at full size.");
            float low = 1f, high = 0f;
            for (int i = 0; i < 200; i++)
            {
                float opacity = ShieldBubble.OpacityAt(5f, i / 200f / ShieldBubble.WarningFrequency, true);
                low = Mathf.Min(low, opacity);
                high = Mathf.Max(high, opacity);
            }
            Require(Mathf.Approximately(ShieldBubble.OpacityAt(5f, 0f, true), 1f) &&
                    Mathf.Abs(low - ShieldBubble.WarningLow) < 0.02f && high > 0.99f,
                $"The last heart must blink between {ShieldBubble.WarningLow} and full, starting bright ({low}-{high}).");
            Require(ShieldBubble.WarningFrequency >= 2f && ShieldBubble.WarningFrequency <= 6f,
                "The blink must read as a warning without flickering.");
        }

        private static void VerifyPowerLink(Sprite sprite)
        {
            ItemDefinition power = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                $"Assets/Items/{Week27Power1Setup.FreeloaderShieldId}.asset");
            Require(power != null, "날먹보호막 must exist.");
            var player = new GameObject("Shield bubble verification player", typeof(SpriteRenderer),
                typeof(Rigidbody2D), typeof(Health), typeof(PlayerSP), typeof(PlayerStats));
            try
            {
                player.GetComponent<Rigidbody2D>().gravityScale = 0f;
                player.GetComponent<SpriteRenderer>().sprite = sprite;
                Health health = player.GetComponent<Health>();
                health.EnableHealthUnits();
                Invoke(health, "Awake");
                Invoke(player.GetComponent<PlayerSP>(), "Awake");
                Invoke(player.GetComponent<PlayerStats>(), "Awake");
                PlayerPowerEffects effects = player.AddComponent<PlayerPowerEffects>();
                Invoke(effects, "Awake");
                int unit = HealthUnits.UnitsPerHeart;

                Update(effects, 0f);
                Require(effects.Bubble == null, "No bubble may appear before the power is used.");

                // One heart of shield from elsewhere is held first: it is not the power's and outlasts the bubble.
                health.SetShield(unit);
                effects.Execute(power);
                Require(Mathf.Approximately(effects.FreeloaderShieldGranted, 2 * unit) &&
                        Mathf.Approximately(effects.FreeloaderShieldRemaining, 2 * unit) && !effects.IsFreeloaderShieldLow,
                    "날먹보호막 must follow its own two hearts on top of the shield already held.");
                Update(effects, 0.1f);
                Require(effects.Bubble != null && effects.Bubble.Bubble.enabled &&
                        effects.Bubble.transform.parent == player.transform && effects.Bubble.RippleProgress < 0f,
                    "The bubble must appear around the player when the power is used.");

                Hit(health, unit);
                Update(effects, 1f);
                Require(Mathf.Approximately(effects.FreeloaderShieldRemaining, unit) && effects.IsFreeloaderShieldLow &&
                        effects.Bubble.RippleProgress >= 0f,
                    "A hit on the shield must ripple the bubble and leave the last heart blinking.");

                // A second use while the bubble is up grows the same bubble.
                effects.Execute(power);
                Update(effects, 1.5f);
                Require(Mathf.Approximately(effects.FreeloaderShieldRemaining, 3 * unit) && !effects.IsFreeloaderShieldLow &&
                        Mathf.Approximately(effects.Bubble.Opacity, 1f),
                    "Using the power again must add to the bubble without blowing a new one.");

                Hit(health, 3 * unit);
                Update(effects, 2f);
                Require(!effects.IsFreeloaderShieldActive && effects.Bubble.PopProgress >= 0f &&
                        Mathf.Approximately(health.CurrentShield, unit),
                    "Using up the power's shield must pop the bubble and keep the other shield.");
                Update(effects, 2f + ShieldBubble.PopSeconds + 0.01f);
                Require(!effects.Bubble.Bubble.enabled, "The bubble must be gone after the pop.");

                effects.Execute(power);
                Update(effects, 3f);
                Require(effects.Bubble.Bubble.enabled, "The bubble must come back on the next use.");
                Hit(health, 1000);
                Require(health.IsDead, "The verification player must die from the lethal hit.");
                Update(effects, 3.1f);
                Require(!effects.Bubble.Bubble.enabled && effects.Bubble.PopProgress < 0f,
                    "The bubble must disappear without a pop when the player dies.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        // Damage itself may roll; only the bubble update is checked for Random use.
        private static void Update(PlayerPowerEffects effects, float now)
        {
            Random.State before = Random.state;
            effects.UpdateShieldBubble(now);
            Require(Random.state.Equals(before), "The shield bubble must not consume UnityEngine.Random.");
        }

        private static void Hit(Health health, int units) =>
            health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, units));

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[ShieldBubbleVerification] " + message);
        }
    }
}
