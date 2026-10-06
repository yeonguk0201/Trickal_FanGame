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
    // Passive-2: creates 거대화 물약 (bigger body, more basic attack damage and health, slower movement) and adds it to the selection
    // reward pool. Re-running updates the same asset, keeps its GUID and leaves the pool unchanged.
    public static class Week23Passive2Setup
    {
        public const string PotionId = "artifact-giant-potion";
        public const string PotionName = "거대화 물약";
        public const float SizeBonus = 0.3f;
        public const float BasicAttackDamageBonus = 0.2f;
        // Half-heart units: 6 = 3 hearts.
        public const float MaxHealthBonus = 6f;
        public const float MoveSpeedPenalty = 0.2f;
        public const int PotionMaxStacks = 2;

        public static string PotionPath => $"Assets/Items/{PotionId}.asset";

        [MenuItem("Trickal Fan Game/Week 23/Setup Passive-2 Giant Potion")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Passive-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Passive-2 Giant Potion");

            ItemDefinition potion = ConfigureItem();
            AddToSelectionPool(potion);
            EnsureGlyphs(potion);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Passive-2 setup complete: {PotionName} ({PotionId}) is in the selection reward pool.");
        }

        private static ItemDefinition ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = PotionId;
                AssetDatabase.CreateAsset(definition, PotionPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + PotionId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + PotionId);
            }

            definition.ConfigureContract(PotionId, PotionName, ItemKind.Artifact, ItemRarity.Epic, true,
                PotionMaxStacks,
                new ItemEffectEntry(ItemEffectType.PlayerSizePercent, SizeBonus),
                new ItemEffectEntry(ItemEffectType.BasicAttackDamagePercent, BasicAttackDamageBonus),
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, MaxHealthBonus),
                new ItemEffectEntry(ItemEffectType.MoveSpeedPenaltyPercent, MoveSpeedPenalty));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{PotionId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AddToSelectionPool(ItemDefinition potion)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            if (pool.Contains(potion)) return;

            // Same order as the Reward-3 setup that builds the whole pool.
            ItemDefinition[] extended = pool.Append(potion)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal).ToArray();
            Undo.RecordObject(assembler, "Add Passive-2 artifact to the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, extended);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Passive-2 setup.");
        }

        // The acquisition toast, reward card and pause list show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition potion)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Passive-2 requires the Frontend TMP font.");
            Week13Hud3BSetup.EnsureDescriptionGlyphs(font);
            string characters = potion.DisplayName + ArtifactEffectDescription.Build(potion);
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Passive-2 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
