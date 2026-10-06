using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Jjangsem-1: creates 멜룬카드, gives the Game Scene player the chest content table its copied chests roll from, and
    // rebuilds the chest content table so diamond chests can hold the card. Re-running updates the same asset and
    // table, keeping their GUIDs, and leaves a configured scene unchanged.
    public static class Week22Jjangsem1Setup
    {
        public const string MeluneCardId = "jjangsem-melune-card";
        public const string MeluneCardName = "멜룬카드";
        public const string MeluneCardPath = "Assets/Items/" + MeluneCardId + ".asset";
        // Not fixed by the plan; Rare like 빅우드의 열매 until Balance-0.
        public const ItemRarity MeluneCardRarity = ItemRarity.Rare;

        [MenuItem("Trickal Fan Game/Week 22/Setup Jjangsem-1 Melune Card")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Jjangsem-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Jjangsem-1 Melune Card");

            ItemDefinition card = ConfigureItem();
            EnsureGlyphs(card);
            AssetDatabase.SaveAssets();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();
            ConfigurePlayerChestContents(table);

            Undo.CollapseUndoOperations(group);
            Debug.Log($"Jjangsem-1 setup complete: {MeluneCardName} ({MeluneCardId}) is a jjangsem spell and one " +
                      $"of {table.JjangsemSpells.Count} jjangsem spell(s) a diamond chest can hold.");
        }

        private static ItemDefinition ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(MeluneCardPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = MeluneCardId;
                AssetDatabase.CreateAsset(definition, MeluneCardPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + MeluneCardId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + MeluneCardId);
            }

            definition.ConfigureContract(MeluneCardId, MeluneCardName, ItemKind.JjangsemSpell, MeluneCardRarity, true,
                1, new ItemEffectEntry(ItemEffectType.DuplicateRoomChestsAndPickups));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{MeluneCardId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // Copied chests roll their contents from the same table as the room clear chests.
        private static void ConfigurePlayerChestContents(ChestContentTable table)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphController[] graphs = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphController>(true)).ToArray();
            if (graphs.Length != 1)
                throw new InvalidOperationException($"Game Scene requires one RoomGraphController; found {graphs.Length}.");
            PlayerSingleUseEffects effects = graphs[0].Player != null
                ? graphs[0].Player.GetComponent<PlayerSingleUseEffects>()
                : null;
            if (effects == null)
                throw new InvalidOperationException("Run Slot-0 setup first: the room graph player has no " +
                                                    "PlayerSingleUseEffects.");
            if (effects.ChestContentTable == table) return;

            Undo.RecordObject(effects, "Configure Jjangsem-1 chest contents");
            effects.ConfigureChestContents(table);
            EditorUtility.SetDirty(effects);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Jjangsem-1 setup.");
        }

        // The slot HUD and pickup description show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition card)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Jjangsem-1 requires the Frontend TMP font.");
            string characters = card.DisplayName + ArtifactEffectDescription.Build(card);
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Jjangsem-1 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
