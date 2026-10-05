using System;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Spell-4: creates the single-use spell 멤버십카드, adds the glyphs its description and the free shop cell texts
    // need, and rebuilds the chest content table so chests can hold it. Re-running updates the same asset, keeping
    // its GUID. No scene or Prefab changes.
    public static class Week22Spell4Setup
    {
        public const string MembershipCardId = "single-spell-membership-card";
        public const string MembershipCardName = "멤버십카드";

        public static string ItemPath => "Assets/Items/" + MembershipCardId + ".asset";

        [MenuItem("Trickal Fan Game/Week 22/Setup Spell-4 Membership Card")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-4 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-4 Membership Card");

            ItemDefinition card = ConfigureCard();
            EnsureGlyphs(card);
            AssetDatabase.SaveAssets();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();

            Undo.CollapseUndoOperations(group);
            Debug.Log($"Spell-4 setup complete: {MembershipCardName} configured; chests draw from " +
                      $"{table.Spells.Count} single-use spells.");
        }

        private static ItemDefinition ConfigureCard()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = MembershipCardId;
                AssetDatabase.CreateAsset(definition, ItemPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + MembershipCardId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + MembershipCardId);
            }

            definition.ConfigureContract(MembershipCardId, MembershipCardName, ItemKind.SingleUseSpell, ItemRarity.Rare,
                true, 1, new ItemEffectEntry(ItemEffectType.FreeCurrentShopOffers));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{MembershipCardId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // The slot HUD and pickup description show the name and effect description; a free shop cell shows the
        // free label and the take button.
        private static void EnsureGlyphs(ItemDefinition card)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Spell-4 requires the Frontend TMP font.");
            string characters = card.DisplayName + ArtifactEffectDescription.Build(card) + ShopView.FreeLabel +
                                ShopView.TakeLabel;
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Spell-4 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
