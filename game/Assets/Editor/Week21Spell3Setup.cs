using System;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Spell-3: creates the single-use 막판 스퍼트 asset and retires the legacy spell-final-sprint, the last legacy
    // spell, from the selection reward pool. Re-running updates the same asset (keeping its GUID) and leaves an already
    // retired pool unchanged.
    public static class Week21Spell3Setup
    {
        public const string FinalSprintId = "single-spell-final-sprint";
        public const string FinalSprintPath = "Assets/Items/" + FinalSprintId + ".asset";
        public const string LegacyFinalSprintId = "spell-final-sprint";
        public const string LegacyFinalSprintPath = "Assets/Items/" + LegacyFinalSprintId + ".asset";
        public const float AttackSpeedPercent = 0.30f;
        public const float MoveSpeedPercent = 0.05f;

        [MenuItem("Trickal Fan Game/Week 21/Setup Spell-3 Final Sprint")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-3 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-3 Final Sprint");

            ConfigureItem();
            LegacySpellRetirement.RetireFromGameSceneRewardPool("Retire legacy spells from the reward pool");

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-3 setup complete: 막판 스퍼트 single-use spell configured and the legacy " +
                      "spell-final-sprint removed from the selection reward pool.");
        }

        private static void ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(FinalSprintPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = FinalSprintId;
                AssetDatabase.CreateAsset(definition, FinalSprintPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + FinalSprintId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + FinalSprintId);
            }

            definition.ConfigureContract(FinalSprintId, "막판 스퍼트", ItemKind.SingleUseSpell, ItemRarity.Rare,
                true, 1, new ItemEffectEntry(ItemEffectType.CurrentBossRoomSpeedPercent,
                    configuredMagnitude: AttackSpeedPercent, configuredSecondaryMagnitude: MoveSpeedPercent));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{FinalSprintId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
        }
    }
}
