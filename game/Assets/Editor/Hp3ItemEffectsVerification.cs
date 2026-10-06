using System;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Hp3ItemEffectsVerification
    {
        private const string BalloonArmorDescription = "최대 HP +1칸 · 획득 시 최대 HP 50% 방어막";
        private const string PillowDescription = "적 2마리 처치마다 HP 반 칸 회복 (스택마다 필요 처치 -1)";
        private const string MaskDescription = "반경 2.5m 적에게 1초마다 공격력의 20% 피해";

        [MenuItem("Trickal Fan Game/HP/Setup HP-3 Item Effects")]
        public static void Setup()
        {
            PhaseGArtifactContractSetup.Setup();
            Week13Hud3BSetup.EnsureDescriptionGlyphs(LoadFont());
            AssetDatabase.SaveAssets();
            Debug.Log("HP-3 setup complete: item-08 and item-14 use effects 26 and 27, and description glyphs are cached.");
        }

        [MenuItem("Trickal Fan Game/HP/Verify HP-3 Item Effects")]
        public static void Verify()
        {
            ValidateAssets();
            ValidateDescriptions();
            ValidateLegacyEffectsStillApply();
            Debug.Log("HP-3 item effect verification passed: Pillow heals half a heart every 2 kills (-1 per stack), " +
                      "Mask deals 20% attack damage per second, Balloon Armor reads as +1 heart, descriptions render " +
                      "with cached glyphs, and legacy effect numbers 10 and 14 keep their original formulas.");
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            Setup();
            Verify();
            PhaseGArtifactContractVerification.Verify();
            Hp4LifeGemVerification.Verify();
            Hp1HealthUnitsVerification.VerifyRegressionBatch();
        }

        private static void ValidateAssets()
        {
            ItemEffectEntry pillow = LoadSingleEffect("item-08");
            Assert(pillow.EffectType == ItemEffectType.HealOnKillEveryN && (int)pillow.EffectType == 26 &&
                   Mathf.Approximately(pillow.Magnitude, 1f) && pillow.IntegerAmount == 2,
                "item-08 must heal one half-heart unit every 2 kills through effect 26.");

            ItemEffectEntry mask = LoadSingleEffect("item-14");
            Assert(mask.EffectType == ItemEffectType.AttackDamageAura && (int)mask.EffectType == 27 &&
                   Mathf.Approximately(mask.Magnitude, 0.2f) && Mathf.Approximately(mask.Radius, 2.5f) &&
                   Mathf.Approximately(mask.IntervalSeconds, 1f),
                "item-14 must deal 20% attack damage every second within 2.5m through effect 27.");

            ItemDefinition balloon = Load("item-02");
            Assert(balloon.Effects.Count == 2 && balloon.Effects[0].EffectType == ItemEffectType.MaxHealthFlat &&
                   Mathf.Approximately(balloon.Effects[0].Magnitude, 2f),
                "item-02 must keep MaxHealthFlat 2, now read as two half-heart units (+1 heart).");
        }

        private static void ValidateDescriptions()
        {
            AssertDescription("item-02", BalloonArmorDescription);
            AssertDescription("item-08", PillowDescription);
            AssertDescription("item-14", MaskDescription);
            TMP_FontAsset font = LoadFont();
            string all = BalloonArmorDescription + PillowDescription + MaskDescription;
            Assert(font.HasCharacters(all), "The Frontend font must already contain every HP-3 description glyph.");
        }

        private static void ValidateLegacyEffectsStillApply()
        {
            GameObject player = new("HP-3 Legacy Player");
            try
            {
                player.AddComponent<Rigidbody2D>().gravityScale = 0f;
                Health health = player.AddComponent<Health>();
                PlayerStats stats = player.AddComponent<PlayerStats>();
                player.AddComponent<PlayerCombatEvents>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(stats, "Awake");

                stats.AddHealOnKillMaxHealthPercent(0.05f);
                Assert(Mathf.Approximately(stats.RegisterKillAndGetHealAmount(), 1f) &&
                       stats.PeriodicKillHealInterval == 0,
                    "Legacy effect 14 must keep its max-HP ratio heal without enabling the periodic counter.");

                PlayerDamageAura aura = player.AddComponent<PlayerDamageAura>();
                Assert(aura.AddStack(0.01f, 2.5f, 1f) && aura.AddAttackDamageStack(0.2f, 2.5f, 1f) &&
                       Mathf.Approximately(aura.MaxHealthDamagePercent, 0.01f) &&
                       Mathf.Approximately(aura.AttackDamagePercent, 0.2f) && aura.StackCount == 2 &&
                       aura.ShowDebugRadius,
                    "Legacy effect 10 and effect 27 must accumulate into separate aura ratios.");
                Assert(!aura.AddAttackDamageStack(0f, 2.5f, 1f),
                    "An attack aura stack without a positive ratio must be rejected.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void AssertDescription(string itemId, string expected)
        {
            string actual = ArtifactEffectDescription.Build(Load(itemId));
            Assert(actual == expected, $"{itemId} description must be '{expected}' but was '{actual}'.");
        }

        private static ItemEffectEntry LoadSingleEffect(string itemId)
        {
            ItemDefinition definition = Load(itemId);
            Assert(definition.IsValid && definition.Effects.Count == 1, $"{itemId} must have exactly one valid effect.");
            return definition.Effects[0];
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            Assert(definition != null, $"Missing item definition: {itemId}");
            return definition;
        }

        private static TMP_FontAsset LoadFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null, "HP-3 requires the Frontend TMP font.");
            return font;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }
            method.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
