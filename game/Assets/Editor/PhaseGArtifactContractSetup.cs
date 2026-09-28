using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    internal sealed class PhaseGArtifactSpec
    {
        public PhaseGArtifactSpec(
            string itemId,
            string displayName,
            ItemRarity rarity,
            bool isActive,
            int maxStacks,
            params ItemEffectEntry[] effects)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Rarity = rarity;
            IsActive = isActive;
            MaxStacks = maxStacks;
            Effects = effects;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public ItemRarity Rarity { get; }
        public bool IsActive { get; }
        public int MaxStacks { get; }
        public ItemEffectEntry[] Effects { get; }
    }

    internal static class PhaseGArtifactCatalog
    {
        public static IReadOnlyList<PhaseGArtifactSpec> All => new[]
        {
            Spec("item-01", "급조한 목검", ItemRarity.Common, true, 5,
                Effect(ItemEffectType.AttackDamagePercent, magnitude: 0.05f)),
            Spec("item-12", "녹슨 송곳", ItemRarity.Common, true, 5,
                Effect(ItemEffectType.CriticalChance, magnitude: 0.03f)),
            Spec("item-04", "낡은 화살", ItemRarity.Uncommon, true, 3,
                Effect(ItemEffectType.AttackSpeedPercent, magnitude: 0.10f)),
            Spec("item-14", "광기의 가면", ItemRarity.Uncommon, true, 3,
                Effect(ItemEffectType.AttackDamageAura, magnitude: 0.20f, radius: 2.5f, intervalSeconds: 1f)),
            Spec("item-02", "풍선 갑옷", ItemRarity.Rare, true, 2,
                Effect(ItemEffectType.MaxHealthFlat, magnitude: 2f),
                Effect(ItemEffectType.ShieldOnAcquireMaxHealthPercent, magnitude: 0.5f)),
            Spec("item-03", "도깨비 감투", ItemRarity.Rare, true, 2,
                Effect(ItemEffectType.MoveSpeedPercentBelowHealth, magnitude: 0.25f, healthThreshold: 0.30f)),
            Spec("item-08", "코미의 베개", ItemRarity.Rare, true, 2,
                Effect(ItemEffectType.HealOnKillEveryN, magnitude: 1f, integerAmount: 2)),
            Spec("item-15", "장난감 망원경", ItemRarity.Epic, true, 1,
                Effect(ItemEffectType.AttackDamagePercent, magnitude: 0.15f),
                Effect(ItemEffectType.DistanceDamage, magnitude: 0.40f, minimumDistance: 2f, maximumDistance: 6f)),
            Spec("item-11", "다야의 다이아몬드 커터", ItemRarity.Epic, true, 1,
                Effect(ItemEffectType.Pierce, integerAmount: 1),
                Effect(ItemEffectType.SplitAfterPierce, secondaryMagnitude: 0.30f, integerAmount: 3,
                    maximumDistance: 3f, spreadAngleDegrees: 15f, scaleMultiplier: 0.60f)),
            Spec("item-13", "에르핀의 지팡이", ItemRarity.Epic, true, 1,
                Effect(ItemEffectType.MaxSP, integerAmount: 1),
                Effect(ItemEffectType.SkillProjectileBonusAtSP, healthThreshold: 1f, integerAmount: 2)),

            // This legacy ID keeps its original multi-shot meaning but is not a Phase G reward candidate.
            Spec("item-06", "다중 투사체", ItemRarity.Rare, false, 2,
                Effect(ItemEffectType.MultiShot, integerAmount: 1)),
        };

        public static IReadOnlyList<PhaseGArtifactSpec> Active => All.Where(spec => spec.IsActive).ToArray();

        private static PhaseGArtifactSpec Spec(
            string itemId,
            string displayName,
            ItemRarity rarity,
            bool isActive,
            int maxStacks,
            params ItemEffectEntry[] effects)
        {
            return new PhaseGArtifactSpec(itemId, displayName, rarity, isActive, maxStacks, effects);
        }

        private static ItemEffectEntry Effect(
            ItemEffectType effectType,
            float magnitude = 0f,
            float healthThreshold = 0f,
            float secondaryMagnitude = 0f,
            int integerAmount = 0,
            float minimumDistance = 0f,
            float maximumDistance = 0f,
            float radius = 0f,
            float intervalSeconds = 0f,
            float spreadAngleDegrees = 0f,
            float scaleMultiplier = 0f)
        {
            return new ItemEffectEntry(
                effectType,
                magnitude,
                healthThreshold,
                secondaryMagnitude,
                integerAmount,
                minimumDistance,
                maximumDistance,
                radius,
                intervalSeconds,
                spreadAngleDegrees,
                scaleMultiplier);
        }
    }

    public static class PhaseGArtifactContractSetup
    {
        private const string ItemFolder = "Assets/Items";

        [MenuItem("Trickal Fan Game/Setup Phase G-1 Artifact Contracts")]
        public static void Setup()
        {
            EnsureFolder();

            foreach (PhaseGArtifactSpec spec in PhaseGArtifactCatalog.All)
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
                    Undo.RecordObject(definition, "Configure Phase G artifact contract");
                }

                definition.ConfigureContract(
                    spec.ItemId,
                    spec.DisplayName,
                    ItemKind.Artifact,
                    spec.Rarity,
                    spec.IsActive,
                    spec.MaxStacks,
                    spec.Effects);
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Phase G-1 artifact contracts configured: 10 active artifacts and inactive legacy item-06.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ItemFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Items");
            }
        }
    }
}
