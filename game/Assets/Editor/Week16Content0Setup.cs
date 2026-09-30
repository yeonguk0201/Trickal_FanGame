using System.Collections.Generic;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    internal sealed class Week16Content0Spec
    {
        public Week16Content0Spec(string itemId, string displayName, ItemKind kind, ItemRarity rarity,
            int maxStacks, params ItemEffectEntry[] effects)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Kind = kind;
            Rarity = rarity;
            MaxStacks = maxStacks;
            Effects = effects;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public ItemKind Kind { get; }
        public ItemRarity Rarity { get; }
        public int MaxStacks { get; }
        public ItemEffectEntry[] Effects { get; }
    }

    internal static class Week16Content0Catalog
    {
        public static IReadOnlyList<Week16Content0Spec> All => new[]
        {
            Spec("spell-catch-that-one", "저놈 잡아라", ItemKind.Spell, ItemRarity.Uncommon, 1,
                Effect(ItemEffectType.NextCombatRoomAttackDamagePercent, magnitude: 0.10f)),
            Spec("spell-final-sprint", "막판 스퍼트", ItemKind.Spell, ItemRarity.Rare, 1,
                Effect(ItemEffectType.BossRoomAttackSpeedPercent, magnitude: 0.30f),
                Effect(ItemEffectType.BossRoomMoveSpeedPercent, magnitude: 0.05f)),
            Spec("spell-afterimage", "그건 내 잔상", ItemKind.Spell, ItemRarity.Uncommon, 3,
                Effect(ItemEffectType.MoveSpeedPercent, magnitude: 0.10f),
                Effect(ItemEffectType.AttackSpeedPercent, magnitude: 0.05f)),
            Spec("artifact-life-gem", "생명의 보석", ItemKind.Artifact, ItemRarity.Rare, 1,
                Effect(ItemEffectType.HealOverTimeBelowHealthOnce, magnitude: 0.45f,
                    healthThreshold: 0.30f, durationSeconds: 3f)),
            Spec("artifact-30kg-kettlebell", "30KG 케틀벨", ItemKind.Artifact, ItemRarity.Rare, 2,
                Effect(ItemEffectType.SkillDamagePercent, magnitude: 0.25f),
                Effect(ItemEffectType.MoveSpeedPenaltyPercent, magnitude: 0.10f)),
            Spec("artifact-clear-weather-card", "날씨는 맑음 카드", ItemKind.Artifact, ItemRarity.Epic, 1,
                Effect(ItemEffectType.BasicAttackHitLightning, magnitude: 1.50f, integerAmount: 10)),
        };

        private static Week16Content0Spec Spec(string itemId, string displayName, ItemKind kind,
            ItemRarity rarity, int maxStacks, params ItemEffectEntry[] effects) =>
            new(itemId, displayName, kind, rarity, maxStacks, effects);

        private static ItemEffectEntry Effect(ItemEffectType effectType, float magnitude = 0f,
            float healthThreshold = 0f, int integerAmount = 0, float durationSeconds = 0f) =>
            new(effectType, configuredMagnitude: magnitude, configuredHealthThreshold: healthThreshold,
                configuredIntegerAmount: integerAmount, configuredDurationSeconds: durationSeconds);
    }

    public static class Week16Content0Setup
    {
        private const string ItemFolder = "Assets/Items";

        [MenuItem("Trickal Fan Game/Week 16/Setup Content-0 First Item Contracts")]
        public static void Setup()
        {
            Week16Item0Setup.Setup();
            foreach (Week16Content0Spec spec in Week16Content0Catalog.All)
            {
                string path = $"{ItemFolder}/{spec.ItemId}.asset";
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ItemDefinition>();
                    definition.name = spec.ItemId;
                    AssetDatabase.CreateAsset(definition, path);
                }
                else
                {
                    Undo.RecordObject(definition, "Configure Week 16 Content-0 item contract");
                }

                definition.ConfigureContract(spec.ItemId, spec.DisplayName, spec.Kind, spec.Rarity, true,
                    spec.MaxStacks, spec.Effects);
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 16 Content-0 setup complete: three spells and three artifacts configured.");
        }
    }
}
