using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Contract-0 §2.2: each legacy always-on spell leaves the selection reward pool (and the shop stock that shares
    // it) once its single-use replacement exists. The legacy asset, ID and Backend catalog entry stay active for
    // past Run records and the item test room.
    public static class LegacySpellRetirement
    {
        public static readonly IReadOnlyList<string> RetiredItemIds = new[]
        {
            "spell-catch-that-one", // Spell-1: replaced by single-spell-catch-that-one.
            "spell-afterimage", // Spell-2: replaced by single-spell-afterimage.
            "spell-final-sprint", // Spell-3: replaced by single-spell-final-sprint.
        };

        public static bool IsRetired(ItemDefinition definition)
        {
            return definition != null && definition.Kind == ItemKind.Spell &&
                   RetiredItemIds.Contains(definition.ItemId, StringComparer.Ordinal);
        }

        // Removes every retired spell from the Game Scene selection reward pool. An already retired pool stays
        // unchanged, so re-running a setup does not dirty the scene.
        public static void RetireFromGameSceneRewardPool(string undoLabel)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            ItemDefinition[] retained = pool.Where(item => !IsRetired(item)).ToArray();
            if (retained.Length == pool.Length) return;

            Undo.RecordObject(assembler, undoLabel);
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, retained);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Game Scene save failed during {undoLabel}.");
        }
    }
}
