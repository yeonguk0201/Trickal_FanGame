using System;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Jjangsem-0: creates the first jjangsem spell, 빅우드의 열매, and rebuilds the chest content table so diamond chests
    // can hold it (Chest-2 gathers every active, valid jjangsem spell). Re-running updates the same asset and table,
    // keeping their GUIDs.
    public static class Week22Jjangsem0Setup
    {
        public const string BigwoodFruitId = "jjangsem-bigwood-fruit";
        public const string BigwoodFruitName = "빅우드의 열매";
        public const string BigwoodFruitPath = "Assets/Items/" + BigwoodFruitId + ".asset";

        // D10 (2026-10-05). Health values are half-heart units: every hit is half a heart weaker for 10 seconds, and
        // each hit that still costs HP is healed 2 seconds later by that damage plus one heart.
        public const float DamageReductionUnits = 1f;
        public const int RecoveryBonusUnits = 2;
        public const float RecoveryDelaySeconds = 2f;
        public const float DurationSeconds = 10f;

        [MenuItem("Trickal Fan Game/Week 22/Setup Jjangsem-0 Bigwood Fruit")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Jjangsem-0 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Jjangsem-0 Bigwood Fruit");

            ItemDefinition fruit = ConfigureItem();
            EnsureGlyphs(fruit);
            AssetDatabase.SaveAssets();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();

            Undo.CollapseUndoOperations(group);
            Debug.Log($"Jjangsem-0 setup complete: {BigwoodFruitName} ({BigwoodFruitId}) is a jjangsem spell and one " +
                      $"of {table.JjangsemSpells.Count} jjangsem spell(s) a diamond chest can hold.");
        }

        private static ItemDefinition ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(BigwoodFruitPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = BigwoodFruitId;
                AssetDatabase.CreateAsset(definition, BigwoodFruitPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + BigwoodFruitId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + BigwoodFruitId);
            }

            definition.ConfigureContract(BigwoodFruitId, BigwoodFruitName, ItemKind.JjangsemSpell, ItemRarity.Rare, true,
                1, new ItemEffectEntry(ItemEffectType.ReduceAndRecoverDamageTaken,
                    configuredMagnitude: DamageReductionUnits,
                    configuredIntegerAmount: RecoveryBonusUnits,
                    configuredIntervalSeconds: RecoveryDelaySeconds,
                    configuredDurationSeconds: DurationSeconds));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{BigwoodFruitId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // The slot HUD and pickup description show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition fruit)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Jjangsem-0 requires the Frontend TMP font.");
            string characters = fruit.DisplayName + ArtifactEffectDescription.Build(fruit);
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Jjangsem-0 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
