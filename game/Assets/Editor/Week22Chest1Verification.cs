using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Chest-1: combat room clears roll a chest (33%, then normal/golden/diamond 75/20/5) instead of the old
    // single-pickup drop, never both. Opening drops the rolled contents once: normal 1~3 consumables (60/30/10) with a
    // 5% spell, golden 2~4, diamond 4~6, consumables weighted like the old drop. Rolls replay per seed, a rebuilt room
    // restores its chest without dropping again, and the development panel can force each kind.
    public static class Week22Chest1Verification
    {
        private const int DistributionSamples = 200000;
        private const int ContentSamples = 60000;
        private const int MaximumSeedSearch = 4096;

        // Expected pickups per combat room clear, old drop vs chests (docs/21 Chest-1 comparison table).
        private static readonly (string Id, float Old, float Chest)[] ExpectedPerClear =
        {
            ("heart", 0.099f, 0.195525f),
            ("sp", 0.099f, 0.195525f),
            ("gold", 0.066f, 0.13035f),
            ("key", 0.0396f, 0.07821f),
            ("bomb", 0.0264f, 0.05214f),
        };
        private const float ExpectedKeyCostPerClear = 0.066f;
        private const float ExpectedBombCostPerClear = 0.0165f;
        // Normal 5% plus the diamond chest's guaranteed spell (Chest-2).
        private const float ExpectedSpellsPerClear = 0.028875f;

        public static void SetupAndVerifyBatch()
        {
            Week22Chest1Setup.Setup();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week22Chest1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(tableGuid) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Chest-1 setup changed the chest content table or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the verifiers whose clear-reward checks now follow the chest path, the development panel and
        // the chest opening rules.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week14Room8Verification.Verify();
            Week17Resource3Verification.Verify();
            Week22Chest0Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Chest-1 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Chest-1 Clear Chests")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ChestContentTable table = AssetDatabase.LoadAssetAtPath<ChestContentTable>(
                Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null, $"Run Chest-1 setup first: {Week22Chest1Setup.ChestContentTablePath} is missing.");
            Assert(assembler.ChestContentTable == table,
                "The Game Scene assembler must roll clear chests from the chest content table.");

            ValidateTable(table);
            ValidateDistribution(table);
            ValidateComparison(table);
            ValidateEncounterRoomsUseChests(assembler);
            ValidateClearChest(assembler, table);
            ValidateDevelopmentChests(assembler, table);
            Debug.Log("Chest-1 verification passed: combat room clears roll a 33% chest (75/20/5) from the room seed " +
                      "instead of the old drop, opening drops normal 1~3 (60/30/10, 5% spell), golden 2~4 and " +
                      "diamond 4~6 consumables once, revisits and rebuilds keep the chest without dropping again, " +
                      "and the development panel forces each kind.");
        }

        private static void ValidateTable(ChestContentTable table)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(Mathf.Approximately(table.ChestChance, 0.33f), "Chests must appear on 33% of clears (§3.1).");
            AssertRule(table.FindRule(ChestKind.Normal), 75, 1, new[] { 60, 30, 10 }, 0.05f);
            AssertRule(table.FindRule(ChestKind.Golden), 20, 2, new[] { 1, 1, 1 }, 0f);
            AssertRule(table.FindRule(ChestKind.Diamond), 5, 4, new[] { 1, 1, 1 }, 1f);

            ResourceDropTable oldTable =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week17Resource3Setup.RoomClearDropTablePath);
            Assert(oldTable != null, "The old clear drop table must stay for comparison.");
            Assert(table.Consumables.Select(entry => entry.DropId)
                       .SequenceEqual(Week22Chest1Setup.ConsumableWeights.Select(weight => weight.Id)),
                "Chest consumables must be heart, SP, gold, key and bomb.");
            foreach ((string id, string oldId, int weight) in Week22Chest1Setup.ConsumableWeights)
            {
                ResourceDropEntry entry = table.Consumables.Single(candidate => candidate.DropId == id);
                ResourceDropEntry old = oldTable.Entries.Single(candidate => candidate.DropId == oldId);
                Assert(entry.Weight == weight && entry.Weight == old.Weight && entry.Prefab == old.Prefab,
                    $"Chest consumable '{id}' must keep the old clear drop weight and pickup (D2).");
            }

            ItemDefinition[] expectedSpells = Week22Chest1Setup.FindSingleUseItems(ItemKind.SingleUseSpell);
            Assert(expectedSpells.Length > 0 && table.Spells.SequenceEqual(expectedSpells) &&
                   table.Spells.All(spell => spell.Kind != ItemKind.JjangsemSpell && spell.Kind != ItemKind.Spell),
                "Normal chests must draw from every implemented single-use spell and never a jjangsem or legacy spell.");
            Assert(table.ChestPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(Week22Chest0Setup.ChestPrefabPath)
                       ?.GetComponent<TreasureChest>() &&
                   table.SpellPickupPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(
                       Week21Slot0Setup.PickupPrefabPath)?.GetComponent<SingleUseItemPickup>(),
                "The chest table must use the Chest-0 chest Prefab and the Slot-0 pickup Prefab.");

            ChestContentTable broken = ScriptableObject.CreateInstance<ChestContentTable>();
            try
            {
                broken.Configure(0.33f, Week22Chest1Setup.BuildKindRules().Take(2).ToArray(),
                    table.Consumables.ToArray(), table.Spells.ToArray(), table.ChestPrefab, table.SpellPickupPrefab);
                Assert(!broken.TryValidate(out _) && !broken.TryRollChest(1, out _),
                    "A table missing a chest kind must be rejected and roll nothing.");
                broken.Configure(0.33f, Week22Chest1Setup.BuildKindRules(), table.Consumables.ToArray(),
                    Array.Empty<ItemDefinition>(), table.ChestPrefab, table.SpellPickupPrefab);
                Assert(!broken.TryValidate(out _), "A spell chance without spells must be rejected.");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        private static void ValidateDistribution(ChestContentTable table)
        {
            int chests = 0;
            Dictionary<ChestKind, int> kinds = new()
            {
                [ChestKind.Normal] = 0,
                [ChestKind.Golden] = 0,
                [ChestKind.Diamond] = 0,
            };
            for (int index = 0; index < DistributionSamples; index++)
            {
                int seed = RoomClearRewardSpawner.DeriveChestSeed(index * 7919 + 17);
                bool rolled = table.TryRollChest(seed, out ChestKind kind);
                Assert(rolled == table.TryRollChest(seed, out ChestKind again) && kind == again,
                    "The chest roll must replay for the same seed.");
                if (!rolled) continue;
                chests++;
                kinds[kind]++;
            }

            AssertNear(chests / (float)DistributionSamples, 0.33f, 0.005f, "chest chance");
            AssertNear(kinds[ChestKind.Normal] / (float)chests, 0.75f, 0.01f, "normal share");
            AssertNear(kinds[ChestKind.Golden] / (float)chests, 0.20f, 0.01f, "golden share");
            AssertNear(kinds[ChestKind.Diamond] / (float)chests, 0.05f, 0.005f, "diamond share");

            foreach (ChestKind kind in new[] { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond })
            {
                ChestKindRule rule = table.FindRule(kind);
                int[] counts = new int[rule.CountWeights.Count];
                Dictionary<string, int> consumables = table.Consumables.ToDictionary(entry => entry.DropId, _ => 0);
                int consumableTotal = 0;
                int spells = 0;
                for (int index = 0; index < ContentSamples; index++)
                {
                    int seed = RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 3);
                    ChestContents contents = table.RollContents(seed, kind);
                    ChestContents replay = table.RollContents(seed, kind);
                    Assert(contents.Kind == kind && contents.Spell == replay.Spell &&
                           contents.Consumables.SequenceEqual(replay.Consumables),
                        $"{kind} contents must replay for the same seed.");
                    int count = contents.Consumables.Count;
                    Assert(count >= rule.MinimumCount && count <= rule.MaximumCount &&
                           contents.Consumables.All(entry => entry?.Prefab != null),
                        $"{kind} chest held {count} consumables outside {rule.MinimumCount}~{rule.MaximumCount}.");
                    counts[count - rule.MinimumCount]++;
                    foreach (ResourceDropEntry entry in contents.Consumables) consumables[entry.DropId]++;
                    consumableTotal += count;
                    if (contents.Spell == null) continue;
                    Assert(table.Spells.Contains(contents.Spell) || table.JjangsemSpells.Contains(contents.Spell),
                        $"{kind} chest rolled a spell outside the table.");
                    spells++;
                }

                int weightTotal = rule.CountWeights.Sum();
                for (int index = 0; index < counts.Length; index++)
                    AssertNear(counts[index] / (float)ContentSamples, rule.CountWeights[index] / (float)weightTotal,
                        0.012f, $"{kind} {rule.MinimumCount + index}-item share");
                int consumableWeights = table.Consumables.Sum(entry => entry.Weight);
                foreach (ResourceDropEntry entry in table.Consumables)
                    AssertNear(consumables[entry.DropId] / (float)consumableTotal,
                        entry.Weight / (float)consumableWeights, 0.01f, $"{kind} {entry.DropId} share");
                AssertNear(spells / (float)ContentSamples, rule.SpellChance, 0.004f, $"{kind} spell chance");
            }
        }

        // Expected pickups per combat room clear, old drop against chests, by resource. Logged for the plan table.
        private static void ValidateComparison(ChestContentTable table)
        {
            ResourceDropTable oldTable =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week17Resource3Setup.RoomClearDropTablePath);
            int oldWeights = oldTable.Entries.Sum(entry => entry.Weight);
            int newWeights = table.Consumables.Sum(entry => entry.Weight);
            float itemsPerChestRoll = table.Kinds.Sum(rule => table.KindChancePerRoll(rule.Kind) * rule.ExpectedCount);
            List<string> lines = new() { "Chest-1 expected pickups per combat room clear (old drop -> chest):" };
            foreach ((string id, float expectedOld, float expectedChest) in ExpectedPerClear)
            {
                string oldId = Week22Chest1Setup.ConsumableWeights.Single(weight => weight.Id == id).OldId;
                float oldValue = oldTable.DropChance * oldTable.Entries.Single(entry => entry.DropId == oldId).Weight /
                                 oldWeights;
                float chestValue = itemsPerChestRoll *
                                   table.Consumables.Single(entry => entry.DropId == id).Weight / newWeights;
                AssertNear(oldValue, expectedOld, 0.0001f, $"old {id} per clear");
                AssertNear(chestValue, expectedChest, 0.0001f, $"chest {id} per clear");
                lines.Add($"  {id}: {oldValue:F4} -> {chestValue:F4}");
            }

            float keyCost = table.KindChancePerRoll(ChestKind.Golden);
            float bombCost = table.KindChancePerRoll(ChestKind.Diamond);
            float spellsPerClear = table.Kinds.Sum(rule => table.KindChancePerRoll(rule.Kind) * rule.SpellChance);
            AssertNear(keyCost, ExpectedKeyCostPerClear, 0.0001f, "golden key cost per clear");
            AssertNear(bombCost, ExpectedBombCostPerClear, 0.0001f, "diamond bomb cost per clear");
            AssertNear(spellsPerClear, ExpectedSpellsPerClear, 0.0001f, "spells per clear");
            lines.Add($"  key spent opening golden chests: {keyCost:F4}, bomb spent on diamond chests: {bombCost:F4}");
            lines.Add($"  spells: 0 -> {spellsPerClear:F4}");
            Debug.Log(string.Join("\n", lines));
        }

        // Every Encounter room on every floor gets a chest-mode clear reward from its content seed, never the old table.
        public static void ValidateEncounterRoomsUseChests(RoomGraphAssembler assembler)
        {
            ChestContentTable table = assembler.ChestContentTable;
            Assert(table != null, "The assembler has no chest content table.");
            int seed = FindSeed(assembler.Generator, _ => true);
            try
            {
                PrepareRun(assembler, seed);
                int encounterRooms = 0;
                FieldInfo field = typeof(RoomController).GetField("clearRewardSpawner",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(field != null, "RoomController clear reward storage was not found.");
                foreach (GeneratedFloor floor in assembler.GeneratedGraph.Floors)
                {
                    Assert(assembler.TryLoadFloor(floor.FloorNumber, assembler.Graph.Player, out string error), error);
                    foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
                    {
                        GeneratedRoomNode node = floor.Nodes.Single(candidate => candidate.RoomId == room.Node.RoomId);
                        if (node.Encounter == null) continue;
                        encounterRooms++;
                        RoomClearRewardSpawner spawner = room.Controller.GetComponent<RoomClearRewardSpawner>();
                        Assert(spawner != null && ReferenceEquals(field.GetValue(room.Controller), spawner) &&
                               spawner.ChestTable == table && spawner.DropTable == null &&
                               spawner.ChestSeed == RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed) &&
                               room.Controller.GetComponents<RoomClearRewardSpawner>().Length == 1,
                            $"Encounter room {node.RoomId} must roll only a seeded chest on clear.");
                    }
                }

                Assert(encounterRooms > 0, "The verification Run must contain Encounter rooms.");
            }
            finally
            {
                assembler.Progress.ResetProgress();
            }
        }

        private static void ValidateClearChest(RoomGraphAssembler assembler, ChestContentTable table)
        {
            int seed = FindSeed(assembler.Generator, floor =>
                CombatRooms(floor).Any(node => RollsChest(table, node)) &&
                CombatRooms(floor).Any(node => !RollsChest(table, node)));
            try
            {
                PrepareRun(assembler, seed);
                GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
                GeneratedRoomNode chestNode = CombatRooms(floor).First(node => RollsChest(table, node));
                GeneratedRoomNode emptyNode = CombatRooms(floor).First(node => !RollsChest(table, node));
                PlayerMovement player = assembler.Graph.Player;

                // A clear without a chest roll uses up the room's roll and drops nothing.
                RoomClearRewardSpawner emptySpawner = Spawner(assembler, emptyNode.RoomId);
                RoomRunState emptyState = assembler.Progress.GetRoomState(emptyNode.RoomId);
                Assert(!emptySpawner.TrySpawn() && emptyState.HasGrantedClearReward && emptyState.Chests.Count == 0 &&
                       emptySpawner.LastSpawnedReward == null && !emptySpawner.TrySpawn() &&
                       ContentRoot(assembler, emptyNode.RoomId).GetComponentsInChildren<TreasureChest>(true).Length == 0,
                    $"{emptyNode.RoomId} rolled no chest and must drop nothing, then never roll again.");

                // A chest roll places one chest of the seeded kind; a second clear does nothing.
                Assert(table.TryRollChest(RoomClearRewardSpawner.DeriveChestSeed(chestNode.ContentSeed),
                    out ChestKind kind), "The chosen room must roll a chest.");
                RoomClearRewardSpawner spawner = Spawner(assembler, chestNode.RoomId);
                RoomRunState state = assembler.Progress.GetRoomState(chestNode.RoomId);
                // The player stands on the preferred point when the room clears; the chest must not land on them.
                RoomPrefab chestRoom = Room(assembler, chestNode.RoomId);
                Vector3 playerStart = player.transform.position;
                player.transform.position = chestRoom.Controller.transform.position;
                Assert(spawner.TrySpawn() && spawner.LastSpawnedChest != null &&
                       spawner.LastSpawnedReward == spawner.LastSpawnedChest.gameObject &&
                       spawner.LastSpawnedChest.Kind == kind &&
                       spawner.LastSpawnedChest.ChestId == RoomClearRewardSpawner.ClearChestId &&
                       spawner.LastSpawnedChest.IsClosed && state.HasGrantedClearReward &&
                       state.GetChest(RoomClearRewardSpawner.ClearChestId)?.Kind == kind,
                    $"Clearing {chestNode.RoomId} must place one closed {kind} chest.");
                Assert(!spawner.TrySpawn() && Chests(assembler, chestNode.RoomId).Length == 1 &&
                       Drops(assembler, chestNode.RoomId).Length == 0,
                    "A second clear must not place another chest or any old clear drop.");
                Vector3 position = spawner.LastSpawnedChest.transform.position;
                Assert(Vector2.Distance(position, player.transform.position) >= ChestPlacement.AvoidClearance - 0.001f &&
                       state.GetChest(RoomClearRewardSpawner.ClearChestId).HasPosition,
                    "A clear chest must avoid the player and record where it was placed.");
                player.transform.position = playerStart;

                // Rebuilding the same floor restores the closed chest at the same point.
                Assert(assembler.TryLoadFloor(1, player, out string error), error);
                TreasureChest restored = Chests(assembler, chestNode.RoomId).Single();
                Assert(restored.IsClosed && restored.Kind == kind && restored.transform.position == position &&
                       state.Chests.Count == 1,
                    "A floor rebuild must restore the closed chest where it was.");

                // Opening drops the seeded contents once around the chest.
                ChestContents expected = table.RollContents(RoomClearRewardSpawner.DeriveChestSeed(chestNode.ContentSeed),
                    kind);
                // Chests open only in the room the player is in, whose content is shown.
                Room(assembler, chestNode.RoomId).Node.SetVisible(true);
                Open(restored, assembler.Progress, player);
                GameObject[] drops = Drops(assembler, chestNode.RoomId);
                GameObject[] spells = Spells(assembler, chestNode.RoomId);
                Assert(restored.IsOpened &&
                       drops.Select(DropId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                           expected.Consumables.Select(entry => entry.DropId).OrderBy(id => id, StringComparer.Ordinal)) &&
                       spells.Length == (expected.Spell != null ? 1 : 0),
                    $"Opening the {kind} chest must drop its seeded contents.");
                Physics2D.SyncTransforms();
                Assert(drops.Concat(spells).All(drop =>
                        Vector2.Distance(drop.transform.position, restored.transform.position) >=
                        ChestPlacement.InnerContentRadius - 0.001f &&
                        Vector2.Distance(drop.transform.position, restored.transform.position) <=
                        ChestPlacement.OuterContentRadius + 0.001f &&
                        Physics2D.OverlapCircle(drop.transform.position, ChestPlacement.MaximumPickupRadius,
                            DestructibleObstacle.ObstacleMask) == null),
                    "Chest contents must drop around the solid chest, clear of walls and obstacles.");
                foreach (GameObject drop in drops)
                {
                    RunResourcePickup resource = drop.GetComponent<RunResourcePickup>();
                    Assert(resource == null || resource.CanCollect(assembler.Progress),
                        "Run resources from a chest must be collectible into the current Run.");
                }

                Open(restored, assembler.Progress, player);
                Assert(Drops(assembler, chestNode.RoomId).Length == drops.Length,
                    "Opening an opened chest again must drop nothing.");

                // A rebuild after opening restores the opened chest and drops nothing new.
                Assert(assembler.TryLoadFloor(1, player, out error), error);
                TreasureChest reopened = Chests(assembler, chestNode.RoomId).Single();
                Assert(reopened.IsOpened && Drops(assembler, chestNode.RoomId).Length == 0 &&
                       Spells(assembler, chestNode.RoomId).Length == 0 && !Spawner(assembler, chestNode.RoomId).TrySpawn(),
                    "A rebuilt room must keep its opened chest without dropping its contents again.");

                // Leaving the floor takes an unopened chest with it.
                RoomChestSite site = Site(assembler, emptyNode);
                TreasureChest extra = RoomChestSpawner.SpawnDevelopmentChest(table, site, emptyNode.ContentSeed,
                    ChestKind.Golden, out error);
                Assert(extra != null, error);
                ChestRunState extraRecord = extra.Record;
                Assert(assembler.TryLoadFloor(2, player, out error), error);
                Assert(extraRecord.IsDiscarded && state.GetChest(RoomClearRewardSpawner.ClearChestId).IsOpened,
                    "Leaving the floor must discard unopened chests only.");
                Assert(assembler.TryLoadFloor(1, player, out error), error);
                Assert(Chests(assembler, emptyNode.RoomId).Length == 0,
                    "A discarded chest must not return when its floor is rebuilt.");
            }
            finally
            {
                assembler.Progress.ResetProgress();
            }
        }

        private static void ValidateDevelopmentChests(RoomGraphAssembler assembler, ChestContentTable table)
        {
            int seed = FindSeed(assembler.Generator, _ => true);
            try
            {
                PrepareRun(assembler, seed);
                GeneratedRoomNode start = assembler.GeneratedGraph.FindFloor(1).Nodes
                    .Single(node => node.Role == GeneratedRoomRole.Start);
                RoomChestSite site = Site(assembler, start);
                ChestKind[] kinds = { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond };
                List<TreasureChest> chests = new();
                foreach (ChestKind kind in kinds)
                {
                    TreasureChest chest = RoomChestSpawner.SpawnDevelopmentChest(table, site, start.ContentSeed, kind,
                        out string error);
                    Assert(chest != null && chest.Kind == kind && chest.IsClosed, error);
                    chests.Add(chest);
                }

                Assert(chests.Select(chest => chest.ChestId)
                           .SequenceEqual(new[] { "dev-chest-01", "dev-chest-02", "dev-chest-03" }) &&
                       site.State.Chests.Count == 3,
                    "Development chests must get the next free dev-chest ID and a room state record.");
                for (int first = 0; first < chests.Count; first++)
                for (int second = first + 1; second < chests.Count; second++)
                    Assert(Vector2.Distance(chests[first].transform.position, chests[second].transform.position) >=
                           ChestPlacement.MinimumChestSpacing - 0.001f,
                        "Development chests must not stack on each other.");

                foreach (TreasureChest chest in chests) Open(chest, assembler.Progress, assembler.Graph.Player);
                Assert(chests.All(chest => chest.IsOpened) && Drops(assembler, start.RoomId).Length >= 1 + 2 + 4,
                    "Forced chests of every kind must open and drop their contents.");

                // A forced spell drop goes through the normal slot pickup with a Run-unique instance ID.
                List<GameObject> forced = RoomChestSpawner.SpawnContents(table, chests[0],
                    new ChestContents(ChestKind.Normal, Array.Empty<ResourceDropEntry>(), table.Spells[0]), site);
                SingleUseItemPickup spell = forced.Single().GetComponent<SingleUseItemPickup>();
                Assert(spell != null && spell.Definition == table.Spells[0] &&
                       spell.InstanceId == $"{start.RoomId}-dev-chest-01-spell" && spell.WaitsForPlayerExit,
                    "A chest spell must drop as a slot pickup that waits for the player to step off.");
            }
            finally
            {
                assembler.Progress.ResetProgress();
            }
        }

        private static void Open(TreasureChest chest, RunProgress progress, PlayerMovement player)
        {
            switch (chest.Kind)
            {
                case ChestKind.Golden:
                    if (chest.IsClosed) progress.TryAddResource(RunResourceType.Key, 1);
                    chest.TryOpenByTouch(player);
                    break;
                case ChestKind.Diamond:
                    chest.TryOpenByBomb();
                    break;
                default:
                    chest.TryOpenByTouch(player);
                    break;
            }
        }

        private static void PrepareRun(RoomGraphAssembler assembler, int seed)
        {
            assembler.Progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
        }

        private static int FindSeed(FloorGenerator generator, Func<GeneratedFloor, bool> wanted)
        {
            for (int seed = 1; seed <= MaximumSeedSearch; seed++)
            {
                if (generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _) &&
                    graph.FindFloor(2) != null && wanted(graph.FindFloor(1)))
                    return seed;
            }

            throw new InvalidOperationException("No seed produced the Chest-1 verification floor.");
        }

        private static IEnumerable<GeneratedRoomNode> CombatRooms(GeneratedFloor floor) =>
            floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null);

        private static bool RollsChest(ChestContentTable table, GeneratedRoomNode node) =>
            table.TryRollChest(RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed), out _);

        private static RoomPrefab Room(RoomGraphAssembler assembler, string roomId) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == roomId);

        private static RoomChestSite Site(RoomGraphAssembler assembler, GeneratedRoomNode node) =>
            new(Room(assembler, node.RoomId), node.Template, assembler.Progress.GetRoomState(node.RoomId),
                assembler.Progress);

        private static RoomClearRewardSpawner Spawner(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Controller.GetComponent<RoomClearRewardSpawner>();

        private static Transform ContentRoot(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Node.ContentRoot.transform;

        private static TreasureChest[] Chests(RoomGraphAssembler assembler, string roomId) =>
            ContentRoot(assembler, roomId).GetComponentsInChildren<TreasureChest>(true)
                .Where(chest => chest.gameObject.activeSelf).ToArray();

        private static GameObject[] Drops(RoomGraphAssembler assembler, string roomId) =>
            Children(assembler, roomId, "Chest Drop ").Concat(Children(assembler, roomId, "Clear Drop ")).ToArray();

        private static GameObject[] Spells(RoomGraphAssembler assembler, string roomId) =>
            Children(assembler, roomId, "Chest Spell ").ToArray();

        private static IEnumerable<GameObject> Children(RoomGraphAssembler assembler, string roomId, string prefix) =>
            ContentRoot(assembler, roomId).Cast<Transform>().Select(child => child.gameObject)
                .Where(child => child.name.StartsWith(prefix, StringComparison.Ordinal));

        private static string DropId(GameObject drop)
        {
            string name = drop.name.Substring("Chest Drop ".Length);
            return name.Substring(0, name.IndexOf(' '));
        }

        private static void AssertRule(ChestKindRule rule, int weight, int minimum, int[] countWeights,
            float spellChance)
        {
            Assert(rule != null && rule.Weight == weight && rule.MinimumCount == minimum &&
                   rule.CountWeights.SequenceEqual(countWeights) && Mathf.Approximately(rule.SpellChance, spellChance),
                $"Chest rule {rule?.Kind} must be weight {weight}, {minimum}+ items {string.Join("/", countWeights)}, " +
                $"spell {spellChance:P0} (D2, 2026-10-03).");
        }

        private static void AssertNear(float actual, float expected, float tolerance, string label)
        {
            Assert(Mathf.Abs(actual - expected) <= tolerance,
                $"Chest-1 {label} was {actual:F4}; expected {expected:F4} ± {tolerance:F4}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
