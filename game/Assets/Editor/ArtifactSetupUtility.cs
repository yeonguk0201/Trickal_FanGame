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
    // Shared steps of the artifact setups: an idempotent item asset that keeps its GUID, the selection reward
    // pool of the Game Scene and the glyphs the item's name and description need.
    public static class ArtifactSetupUtility
    {
        public static string ItemPath(string itemId) => $"Assets/Items/{itemId}.asset";

        public static void RequireEditMode(string piece)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"Exit Play Mode before {piece} setup.");
        }

        public static ItemDefinition ConfigureArtifact(string itemId, string displayName, ItemRarity rarity,
            int maxStacks, params ItemEffectEntry[] effects)
        {
            string path = ItemPath(itemId);
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

            definition.ConfigureContract(itemId, displayName, ItemKind.Artifact, rarity, true, maxStacks, effects);
            if (!definition.IsValid)
                throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // Leaves the Scene untouched when the pool already holds every item.
        public static void AddToSelectionPool(string piece, params ItemDefinition[] items)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            if (items.All(pool.Contains)) return;

            // Same order as the Reward-3 setup that builds the whole pool.
            ItemDefinition[] extended = pool.Union(items)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal).ToArray();
            Undo.RecordObject(assembler, $"Add {piece} artifacts to the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, extended);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Game Scene save failed during {piece} setup.");
        }

        // The acquisition toast, reward card and pause list show the name and effect description.
        public static void EnsureGlyphs(string piece, params ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException($"{piece} requires the Frontend TMP font.");
            Week13Hud3BSetup.EnsureDescriptionGlyphs(font);
            string characters = string.Concat(items.Select(item =>
                item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException($"Missing {piece} glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
