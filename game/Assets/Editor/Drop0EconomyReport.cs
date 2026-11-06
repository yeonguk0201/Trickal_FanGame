using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Drop-0: the expected keys, bombs and gold of each floor over many generated seeds, read from the Game Scene's
    // generator and drop tables. Expectations assume every combat room is cleared and every obstacle is broken; secret
    // rooms count too, so the numbers are an upper bound of what a careful player sees. The pity counter is not in
    // these numbers (it depends on the order the player clears rooms).
    public static class Drop0EconomyReport
    {
        public const int SeedCount = 512;

        public sealed class FloorTotals
        {
            public int Floors;
            public double FreeKeys, FreeBombs, FreeGold, GoldPickups;
            public double GoldenChestKeys, GoldenChestBombs, GoldenChestGold, GoldenChestPickups;
            public double DiamondChestKeys, DiamondChestBombs, DiamondChestGold, DiamondChestPickups;
            public double LockedRooms, GoldenChests, DiamondChests, CombatRooms;
            public double ClearKeys, ObstacleKeys, VariantKeys, ChestKeys, AuthoredKeys;
            public double ClearBombs, ObstacleBombs, VariantBombs, AuthoredBombs;
            public double PityKeys, PityBombs;

            public double PerFloor(double value) => Floors == 0 ? 0 : value / Floors;
        }

        private readonly struct Yield
        {
            public Yield(double keys, double bombs, double gold, double goldPickups)
            {
                Keys = keys; Bombs = bombs; Gold = gold; GoldPickups = goldPickups;
            }

            public double Keys { get; }
            public double Bombs { get; }
            public double Gold { get; }
            public double GoldPickups { get; }
            public static Yield operator +(Yield a, Yield b) =>
                new(a.Keys + b.Keys, a.Bombs + b.Bombs, a.Gold + b.Gold, a.GoldPickups + b.GoldPickups);
            public static Yield operator *(Yield a, double s) =>
                new(a.Keys * s, a.Bombs * s, a.Gold * s, a.GoldPickups * s);
        }

        [MenuItem("Trickal Fan Game/Week 28/Report Drop-0 Economy")]
        public static void ReportMenu() => Debug.Log(BuildReport(out _));

        public static void ReportBatch()
        {
            try
            {
                Debug.Log(BuildReport(out _));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // Applies the Drop-0 rates first, then reports (for tuning from the command line).
        public static void SetupAndReportBatch()
        {
            try
            {
                Drop0DropRateSetup.Setup();
                Debug.Log(BuildReport(out _));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string BuildReport(out Dictionary<int, FloorTotals> totals)
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || assembler.ChestContentTable == null)
                throw new InvalidOperationException("Drop-0 report needs the Game Scene generator, assembler and chest table.");

            totals = Measure(generator, assembler);
            StringBuilder report = new();
            report.AppendLine($"Drop-0 economy over {SeedCount} seeds (per floor, every room cleared and obstacle broken)");
            report.AppendLine($"tables: chest {assembler.ChestContentTable.name} {assembler.ChestContentTable.ChestChance:P0}, " +
                              $"elite {(assembler.EliteKillDropTable != null ? assembler.EliteKillDropTable.name : "none")}, " +
                              $"champion {(assembler.ChampionKillDropTable != null ? assembler.ChampionKillDropTable.name : "none")}, " +
                              $"pity {ResourcePity.KeyThreshold}/{ResourcePity.BombThreshold}, elif {PermanentElif.ConversionChance:P1}");
            report.AppendLine(
                "floor | combat | keys+pity | bombs+pity | keys free (clear/obst/variant/authored/elite+champ) | bombs free | gold free (pickups) | elif | locked rooms | golden | diamond | golden contents k/b/g | diamond contents k/b/g");
            foreach (KeyValuePair<int, FloorTotals> entry in totals.OrderBy(pair => pair.Key))
            {
                FloorTotals t = entry.Value;
                string F(double v) => t.PerFloor(v).ToString("0.00");
                double elif = t.PerFloor(t.GoldPickups) * PermanentElif.ConversionChance;
                report.AppendLine(
                    $"{entry.Key} | {F(t.CombatRooms)} | {F(t.FreeKeys + t.PityKeys)} | {F(t.FreeBombs + t.PityBombs)} | {F(t.FreeKeys)} ({F(t.ClearKeys)}/{F(t.ObstacleKeys)}/{F(t.VariantKeys)}/" +
                    $"{F(t.AuthoredKeys)}/{F(t.ChestKeys)}) | {F(t.FreeBombs)} ({F(t.ClearBombs)}/{F(t.ObstacleBombs)}/" +
                    $"{F(t.VariantBombs)}/{F(t.AuthoredBombs)}) | {F(t.FreeGold)} ({F(t.GoldPickups)}) | {elif:0.000} | " +
                    $"{F(t.LockedRooms)} | {F(t.GoldenChests)} | {F(t.DiamondChests)} | " +
                    $"{F(t.GoldenChestKeys)}/{F(t.GoldenChestBombs)}/{F(t.GoldenChestGold)} | " +
                    $"{F(t.DiamondChestKeys)}/{F(t.DiamondChestBombs)}/{F(t.DiamondChestGold)}");
            }

            return report.ToString();
        }

        public static Dictionary<int, FloorTotals> Measure(FloorGenerator generator, RoomGraphAssembler assembler)
        {
            ChestContentTable chests = assembler.ChestContentTable;
            ResourceDropTable eliteTable = assembler.EliteKillDropTable;
            ResourceDropTable championTable = assembler.ChampionKillDropTable;
            Dictionary<int, FloorTotals> totals = new();
            Dictionary<GameObject, (Yield basic, Yield variant)> prefabCache = new();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error))
                    throw new InvalidOperationException($"Seed {seed} failed to generate. {error}");
                HashSet<int> floorsSeen = new();
                // Pity: the player clears combat rooms floor by floor in room-number order; each room yields a key
                // (bomb) with its expected count as the chance. Obstacles count toward the room they sit in.
                List<(int floor, int room, double keys, double bombs)> clears = new();
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    double keysBefore = 0, bombsBefore = 0;
                    if (totals.TryGetValue(node.FloorNumber, out FloorTotals before))
                    {
                        keysBefore = before.FreeKeys;
                        bombsBefore = before.FreeBombs;
                    }

                    if (!totals.TryGetValue(node.FloorNumber, out FloorTotals t))
                        totals[node.FloorNumber] = t = new FloorTotals();
                    if (floorsSeen.Add(node.FloorNumber)) t.Floors++;
                    if (node.RequiresKey) t.LockedRooms++;

                    GameObject prefab = node.Template != null ? node.Template.RoomPrefabAsset : null;
                    if (prefab != null)
                    {
                        if (!prefabCache.TryGetValue(prefab, out (Yield basic, Yield variant) obstacles))
                            prefabCache[prefab] = obstacles = MeasureObstacles(prefab);
                        AddFree(t, obstacles.basic);
                        AddFree(t, obstacles.variant);
                        t.ObstacleKeys += obstacles.basic.Keys;
                        t.VariantKeys += obstacles.variant.Keys;
                        t.ObstacleBombs += obstacles.basic.Bombs;
                        t.VariantBombs += obstacles.variant.Bombs;
                    }

                    if (node.Role != GeneratedRoomRole.Intermediate || node.ResolvedEncounterWaves.Count == 0) continue;
                    t.CombatRooms++;

                    // Clear chest: a normal chest is free, golden costs a key, diamond a bomb. Drop-1: the key-free
                    // floor turns golden into normal.
                    bool keyFree = graph.FindFloor(node.FloorNumber)?.IsKeyFree == true;
                    double kindTotal = chests.Kinds.Sum(rule => rule.Weight);
                    foreach (ChestKindRule rolled in chests.Kinds)
                    {
                        ChestKindRule rule = chests.FindRule(ChestContentTable.ResolveKindForFloor(rolled.Kind, keyFree));
                        double chance = chests.ChestChance * rolled.Weight / kindTotal;
                        Yield contents = ChestYield(chests, rule) * chance;
                        AddChest(t, rule.Kind, contents, chance);
                        if (rule.Kind == ChestKind.Normal)
                        {
                            t.ClearKeys += contents.Keys;
                            t.ClearBombs += contents.Bombs;
                        }
                    }

                    if (node.Template != null)
                        foreach (RoomTemplateChest authored in node.Template.AuthoredChests)
                        {
                            ChestKind kind = ChestContentTable.ResolveKindForFloor(authored.Kind, keyFree);
                            ChestKindRule rule = chests.FindRule(kind);
                            if (rule == null) continue;
                            Yield contents = ChestYield(chests, rule);
                            AddChest(t, kind, contents, 1);
                            if (kind == ChestKind.Normal)
                            {
                                t.AuthoredKeys += contents.Keys;
                                t.AuthoredBombs += contents.Bombs;
                            }
                        }

                    if (eliteTable != null && EnemyVariantRules.CountElites(node.ResolvedEncounterWaves) > 0)
                    {
                        Yield elite = TableYield(eliteTable, 1f, 1);
                        AddFree(t, elite);
                        t.ChestKeys += elite.Keys;
                    }

                    int champions = EnemyVariantRules.CountChampions(node.ResolvedEncounterWaves);
                    if (championTable != null && champions > 0)
                    {
                        Yield champion = TableYield(championTable,
                            ChampionKillReward.DropPercent(node.FloorNumber) / 100.0, champions);
                        AddFree(t, champion);
                        t.ChestKeys += champion.Keys;
                    }

                    clears.Add((node.FloorNumber, node.RoomNumber, t.FreeKeys - keysBefore, t.FreeBombs - bombsBefore));
                }

                SimulatePity(seed, clears, totals);
            }

            return totals;
        }

        // Mirrors ResourcePity: at each first combat clear the count either guarantees (at the threshold) or grows;
        // any gain in the room resets it.
        private static void SimulatePity(int seed, List<(int floor, int room, double keys, double bombs)> clears,
            Dictionary<int, FloorTotals> totals)
        {
            System.Random random = new(seed * 7919 + 13);
            int keyDrought = 0, bombDrought = 0;
            foreach ((int floor, int room, double keys, double bombs) in clears.OrderBy(c => c.floor).ThenBy(c => c.room))
            {
                FloorTotals t = totals[floor];
                if (keyDrought >= ResourcePity.KeyThreshold) { t.PityKeys++; keyDrought = 0; }
                else keyDrought++;
                if (bombDrought >= ResourcePity.BombThreshold) { t.PityBombs++; bombDrought = 0; }
                else bombDrought++;
                if (random.NextDouble() < keys) keyDrought = 0;
                if (random.NextDouble() < bombs) bombDrought = 0;
            }
        }

        private static void AddFree(FloorTotals t, Yield y)
        {
            t.FreeKeys += y.Keys;
            t.FreeBombs += y.Bombs;
            t.FreeGold += y.Gold;
            t.GoldPickups += y.GoldPickups;
        }

        private static void AddChest(FloorTotals t, ChestKind kind, Yield contents, double chance)
        {
            switch (kind)
            {
                case ChestKind.Normal:
                    AddFree(t, contents);
                    break;
                case ChestKind.Golden:
                    t.GoldenChests += chance;
                    t.GoldenChestKeys += contents.Keys;
                    t.GoldenChestBombs += contents.Bombs;
                    t.GoldenChestGold += contents.Gold;
                    t.GoldenChestPickups += contents.GoldPickups;
                    break;
                case ChestKind.Diamond:
                    t.DiamondChests += chance;
                    t.DiamondChestKeys += contents.Keys;
                    t.DiamondChestBombs += contents.Bombs;
                    t.DiamondChestGold += contents.Gold;
                    t.DiamondChestPickups += contents.GoldPickups;
                    break;
            }
        }

        // Basic obstacles and their share of the room's single special variant (Obstacle-1): the special obstacle
        // replaces one variant slot, chosen evenly, at the table's room chance.
        private static (Yield basic, Yield variant) MeasureObstacles(GameObject prefab)
        {
            Yield basic = default, variant = default;
            DestructibleObstacle[] obstacles = prefab.GetComponentsInChildren<DestructibleObstacle>(true);
            RoomObstacleVariantSlot[] slots = prefab.GetComponentsInChildren<RoomObstacleVariantSlot>(true);
            ObstacleVariantTable table = slots.Length > 0 ? slots[0].VariantTable : null;
            double specialPerSlot = table != null && slots.Length > 0 ? table.SpecialRoomChance / slots.Length : 0;
            foreach (DestructibleObstacle obstacle in obstacles)
            {
                ObstacleVariantDefinition fixedVariant = obstacle.FixedVariant;
                ResourceDropTable dropTable = fixedVariant != null ? fixedVariant.DropTable : obstacle.DropTable;
                int count = fixedVariant != null ? fixedVariant.DropCount : 1;
                Yield own = dropTable != null ? TableYield(dropTable, dropTable.DropChance, count) : default;
                bool isSlot = obstacle.GetComponent<RoomObstacleVariantSlot>() != null && fixedVariant == null;
                basic += isSlot ? own * (1 - specialPerSlot) : own;
            }

            if (table != null && slots.Length > 0)
            {
                double weightTotal = table.Entries.Sum(entry => (double)entry.Weight);
                foreach (ObstacleVariantEntry entry in table.Entries)
                {
                    ObstacleVariantDefinition definition = entry.Variant;
                    if (definition == null || definition.DropTable == null) continue;
                    variant += TableYield(definition.DropTable, definition.DropTable.DropChance, definition.DropCount) *
                               (table.SpecialRoomChance * entry.Weight / weightTotal);
                }
            }

            return (basic, variant);
        }

        private static Yield ChestYield(ChestContentTable chests, ChestKindRule rule) =>
            EntriesYield(chests.Consumables) * rule.ExpectedCount;

        private static Yield TableYield(ResourceDropTable table, double chance, int count) =>
            EntriesYield(table.Entries) * (chance * count);

        // The expected resources of one weighted pick.
        private static Yield EntriesYield(IReadOnlyList<ResourceDropEntry> entries)
        {
            double total = entries.Sum(entry => (double)entry.Weight);
            Yield result = default;
            if (total <= 0) return result;
            foreach (ResourceDropEntry entry in entries)
            {
                if (entry.Prefab == null || !entry.Prefab.TryGetComponent(out RunResourcePickup pickup)) continue;
                double share = entry.Weight / total;
                result += pickup.ResourceType switch
                {
                    RunResourceType.Key => new Yield(pickup.Amount * share, 0, 0, 0),
                    RunResourceType.Bomb => new Yield(0, pickup.Amount * share, 0, 0),
                    RunResourceType.Gold => new Yield(0, 0, pickup.Amount * share, share),
                    _ => default,
                };
            }

            return result;
        }
    }
}
