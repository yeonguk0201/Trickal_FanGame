using System;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Spell-0: creates the 아로마 테라피 and 명상의 시간 single-use spell assets. Re-running updates the same assets
    // (and keeps their GUIDs) instead of adding more. They join no reward pool here; chests connect them later.
    public static class Week21Spell0Setup
    {
        public const string AromaTherapyId = "single-spell-aroma-therapy";
        public const string MeditationTimeId = "single-spell-meditation-time";
        public const string AromaTherapyPath = "Assets/Items/" + AromaTherapyId + ".asset";
        public const string MeditationTimePath = "Assets/Items/" + MeditationTimeId + ".asset";
        public const int AromaOvercharge = 1;
        public const int MeditationHalvesPerTick = 1;
        public const float MeditationIntervalSeconds = 1f;
        public const float MeditationDurationSeconds = 12f;

        [MenuItem("Trickal Fan Game/Week 21/Setup Spell-0 SP Spells")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-0 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-0 SP Spells");

            Configure(AromaTherapyPath, AromaTherapyId, "아로마 테라피", ItemRarity.Uncommon,
                new ItemEffectEntry(ItemEffectType.RestoreAllSPWithOvercharge,
                    configuredIntegerAmount: AromaOvercharge));
            Configure(MeditationTimePath, MeditationTimeId, "명상의 시간", ItemRarity.Uncommon,
                new ItemEffectEntry(ItemEffectType.RegenerateSPHalvesOverTime,
                    configuredIntegerAmount: MeditationHalvesPerTick,
                    configuredIntervalSeconds: MeditationIntervalSeconds,
                    configuredDurationSeconds: MeditationDurationSeconds));

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-0 setup complete: 아로마 테라피 and 명상의 시간 single-use spell assets configured.");
        }

        private static void Configure(string path, string itemId, string displayName, ItemRarity rarity,
            ItemEffectEntry effect)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = itemId;
                AssetDatabase.CreateAsset(definition, path);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + itemId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + itemId);
            }

            definition.ConfigureContract(itemId, displayName, ItemKind.SingleUseSpell, rarity, true, 1, effect);
            if (!definition.IsValid) throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
        }
    }
}
