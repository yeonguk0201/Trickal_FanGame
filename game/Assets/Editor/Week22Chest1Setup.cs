using System;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Chest-1: builds the chest content table and gives it to the Game Scene assembler, so combat room clears roll a
    // chest instead of the old single-pickup drop. The old table asset stays for comparison. Re-running updates the
    // same asset and keeps its GUID.
    public static class Week22Chest1Setup
    {
        public const string ChestContentTablePath = Week17Resource3Setup.DropTableFolder + "/chest-content-table.asset";

        // §3.1: 33% of clears, then 75/20/5. D2 (2026-10-03): normal 1~3 (60/30/10) with a 5% spell, golden 2~4,
        // diamond 4~6, consumable weights kept from the old clear drop.
        public const float ChestChance = 0.33f;
        public const float NormalSpellChance = 0.05f;

        // New table, new stable IDs; the old table's "elif" candidate is the gold pickup.
        public static readonly (string Id, string OldId, int Weight)[] ConsumableWeights =
        {
            ("heart", "heart", 30),
            ("sp", "sp", 30),
            ("gold", "elif", 20),
            ("key", "key", 12),
            ("bomb", "bomb", 8),
        };

        public static ChestKindRule[] BuildKindRules() => new[]
        {
            new ChestKindRule(ChestKind.Normal, 75, 1, new[] { 60, 30, 10 }, NormalSpellChance),
            new ChestKindRule(ChestKind.Golden, 20, 2, new[] { 1, 1, 1 }, 0f),
            new ChestKindRule(ChestKind.Diamond, 5, 4, new[] { 1, 1, 1 }, 0f),
        };

        [MenuItem("Trickal Fan Game/Week 22/Setup Chest-1 Clear Chests")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Chest-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ChestContentTable table = EnsureTable();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler[] assemblers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).ToArray();
            if (assemblers.Length != 1)
                throw new InvalidOperationException($"Game Scene requires one RoomGraphAssembler; found {assemblers.Length}.");

            Undo.RecordObject(assemblers[0], "Setup Chest-1 Clear Chests");
            assemblers[0].ConfigureChestContents(table);
            EditorUtility.SetDirty(assemblers[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Chest-1 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Chest-1 setup complete: combat room clears roll {ChestContentTablePath} " +
                      "(33% chest, 75/20/5 normal/golden/diamond) instead of the old clear drop.");
        }

        public static ChestContentTable EnsureTable()
        {
            TreasureChest chestPrefab = Week22Chest0Setup.EnsureChestPrefab();
            SingleUseItemPickup spellPickup = AssetDatabase.LoadAssetAtPath<GameObject>(Week21Slot0Setup.PickupPrefabPath)
                ?.GetComponent<SingleUseItemPickup>();
            if (spellPickup == null)
                throw new InvalidOperationException($"Run Slot-0 setup first: {Week21Slot0Setup.PickupPrefabPath}.");

            ResourceDropTable oldTable =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week17Resource3Setup.RoomClearDropTablePath);
            if (oldTable == null)
                throw new InvalidOperationException($"Run Resource-3 setup first: {Week17Resource3Setup.RoomClearDropTablePath}.");
            ResourceDropEntry[] consumables = ConsumableWeights.Select(weight =>
            {
                ResourceDropEntry old = oldTable.Entries.FirstOrDefault(entry => entry.DropId == weight.OldId);
                if (old?.Prefab == null)
                    throw new InvalidOperationException($"The old clear drop table has no '{weight.OldId}' pickup.");
                return new ResourceDropEntry(weight.Id, old.Prefab, weight.Weight);
            }).ToArray();

            ItemDefinition[] spells = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.Kind == ItemKind.SingleUseSpell && item.IsActive && item.IsValid)
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();

            ChestContentTable table = AssetDatabase.LoadAssetAtPath<ChestContentTable>(ChestContentTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ChestContentTable>();
                AssetDatabase.CreateAsset(table, ChestContentTablePath);
            }

            table.Configure(ChestChance, BuildKindRules(), consumables, spells, chestPrefab, spellPickup);
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Chest-1 built an invalid chest content table. {error}");
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }
    }
}
