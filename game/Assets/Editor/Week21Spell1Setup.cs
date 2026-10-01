using System;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Spell-1: creates the single-use 저놈 잡아라 asset and retires the legacy spell-catch-that-one from the selection
    // reward pool. Re-running updates the same asset (keeping its GUID) and leaves an already retired pool unchanged.
    public static class Week21Spell1Setup
    {
        public const string CatchThatOneId = "single-spell-catch-that-one";
        public const string CatchThatOnePath = "Assets/Items/" + CatchThatOneId + ".asset";
        public const string LegacyCatchThatOneId = "spell-catch-that-one";
        public const string LegacyCatchThatOnePath = "Assets/Items/" + LegacyCatchThatOneId + ".asset";
        public const float BasicAttackDamagePercent = 0.10f;

        [MenuItem("Trickal Fan Game/Week 21/Setup Spell-1 Catch That One")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-1 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-1 Catch That One");

            ConfigureItem();
            RetireLegacyFromRewardPool();

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-1 setup complete: 저놈 잡아라 single-use spell configured and the legacy " +
                      "spell-catch-that-one removed from the selection reward pool.");
        }

        private static void ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CatchThatOnePath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = CatchThatOneId;
                AssetDatabase.CreateAsset(definition, CatchThatOnePath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + CatchThatOneId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + CatchThatOneId);
            }

            definition.ConfigureContract(CatchThatOneId, "저놈 잡아라", ItemKind.SingleUseSpell, ItemRarity.Uncommon,
                true, 1, new ItemEffectEntry(ItemEffectType.CurrentRoomBasicAttackDamagePercent,
                    configuredMagnitude: BasicAttackDamagePercent));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{CatchThatOneId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
        }

        private static void RetireLegacyFromRewardPool()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            ItemDefinition[] retained = pool.Where(item => !LegacySpellRetirement.IsRetired(item)).ToArray();
            if (retained.Length == pool.Length) return;

            Undo.RecordObject(assembler, "Retire legacy spells from the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, retained);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Spell-1 setup.");
        }
    }
}
