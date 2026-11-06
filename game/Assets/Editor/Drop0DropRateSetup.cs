using System;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Drop-0 (docs/28 §5.4): the drop rates toward the per-floor targets (keys 1.5, bombs 1.5, gold about 8 on a
    // 6-combat-room floor). The values live here and the older setups that build the same assets read them, so a
    // regression rerun of Chest-1, Variant-0 or Resource-1 keeps them. The pity counter and champion drop chances are
    // runtime rules (ResourcePity, ChampionKillReward); this only configures assets and the Game Scene references.
    public static class Drop0DropRateSetup
    {
        public const string ChampionDropTablePath =
            Week17Resource3Setup.DropTableFolder + "/champion-kill-drop-table.asset";

        // Clear chests: 33% → 50% of clears, normal chests hold 1~3 at 40/40/20 (was 60/30/10).
        public const float ClearChestChance = 0.5f;
        public static readonly int[] NormalChestCountWeights = { 40, 40, 20 };

        // Chest consumables: heart and SP give way to gold, keys and bombs (was 30/30/20/12/8).
        public static readonly (string Id, int Weight)[] ChestConsumableWeights =
        {
            ("heart", 22),
            ("sp", 22),
            ("gold", 26),
            ("key", 15),
            ("bomb", 15),
        };

        // Elite and champion kill drops share one candidate list ("elif" is the gold pickup's old drop ID).
        public static readonly (string DropId, int Weight)[] KillDropWeights =
        {
            ("heart", 20),
            ("sp", 20),
            ("key", 18),
            ("bomb", 18),
            ("elif", 24),
        };

        // One floor gold pickup (ElifPickup.prefab) is worth this much gold (was 1).
        public const int GoldPickupAmount = 3;

        public static int ChestConsumableWeight(string id) =>
            ChestConsumableWeights.Single(entry => entry.Id == id).Weight;

        [MenuItem("Trickal Fan Game/Week 28/Setup Drop-0 Drop Rates")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Drop-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // The scene first: opening it unloads assets loaded before, which would leave the tables below destroyed.
            Week18Obstacle2Setup.OpenGameScene();
            EnsureGoldPickupAmount();
            ChestContentTable chests = AssetDatabase.LoadAssetAtPath<ChestContentTable>(
                Week22Chest1Setup.ChestContentTablePath);
            if (chests == null)
                throw new InvalidOperationException($"Run Chest-1 setup first: {Week22Chest1Setup.ChestContentTablePath}.");
            ConfigureChestTable(chests);

            ResourceDropTable elite = Week17Resource3Setup.EnsureTable(Week27Variant0Setup.EliteDropTablePath, 1f,
                KillDropWeights.Select(entry => (entry.DropId, entry.Weight)).ToArray(),
                "Configure Drop-0 elite drop table");
            // The champion table's own chance is unused: ChampionKillReward.DropPercent rolls by floor first.
            ResourceDropTable champion = Week17Resource3Setup.EnsureTable(ChampionDropTablePath, 1f,
                KillDropWeights.Select(entry => (entry.DropId, entry.Weight)).ToArray(),
                "Configure Drop-0 champion drop table");
            foreach (ResourceDropTable table in new[] { elite, champion })
                if (!table.TryValidate(out string error))
                    throw new InvalidOperationException($"Drop-0 built an invalid drop table. {error}");

            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (assembler == null)
                throw new InvalidOperationException("Drop-0 requires the Game Scene RoomGraphAssembler.");
            if (assembler.ChampionKillDropTable != champion || assembler.ChestContentTable != chests ||
                assembler.EliteKillDropTable != elite)
            {
                Undo.RecordObject(assembler, "Configure Drop-0 drop tables");
                assembler.ConfigureChampionKillDrop(champion);
                assembler.ConfigureEliteKillDrop(elite);
                assembler.ConfigureChestContents(chests);
                EditorUtility.SetDirty(assembler);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                    throw new InvalidOperationException("Game Scene save failed during Drop-0 setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Drop-0 ready: clear chest {ClearChestChance:P0}, normal chest 1~3 at " +
                      $"{string.Join("/", NormalChestCountWeights)}, consumables " +
                      $"{string.Join(" / ", ChestConsumableWeights.Select(entry => $"{entry.Id} {entry.Weight}"))}, " +
                      $"gold pickup {GoldPickupAmount}, champion drop table {ChampionDropTablePath}, pity after " +
                      $"{ResourcePity.KeyThreshold}/{ResourcePity.BombThreshold} rooms, elif conversion " +
                      $"{PermanentElif.ConversionChance:P1}.");
        }

        // Keeps every Chest-1/Chest-2 setting (spells, prefabs, special rewards) and rewrites the rates only.
        private static void ConfigureChestTable(ChestContentTable table)
        {
            ResourceDropEntry[] consumables = table.Consumables.Select(entry =>
                new ResourceDropEntry(entry.DropId, entry.Prefab, ChestConsumableWeight(entry.DropId))).ToArray();
            Undo.RecordObject(table, "Configure Drop-0 chest rates");
            table.Configure(ClearChestChance, Week22Chest1Setup.BuildKindRules(), consumables, table.Spells.ToArray(),
                table.ChestPrefab, table.SpellPickupPrefab);
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Drop-0 built an invalid chest content table. {error}");
            EditorUtility.SetDirty(table);
        }

        private static void EnsureGoldPickupAmount()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource1Setup.ElifPrefabPath);
            RunResourcePickup pickup = prefab != null ? prefab.GetComponent<RunResourcePickup>() : null;
            if (pickup == null || pickup.ResourceType != RunResourceType.Gold)
                throw new InvalidOperationException($"Run Resource-1 setup first: {Week17Resource1Setup.ElifPrefabPath}.");
            if (pickup.Amount == GoldPickupAmount) return;
            SerializedObject serialized = new(pickup);
            serialized.FindProperty("amount").intValue = GoldPickupAmount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }
}
