using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
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
    // Chest-2: a golden chest rolls a 25% special reward (within the 20~30% of §3.1) that drops one golden exclusive
    // artifact the opening player can still acquire, and nothing when every candidate is at its stack limit. A diamond
    // chest always holds one spell, a jjangsem spell 25% of the time once one is implemented. Only valid pool items
    // drop, the Chest-1 consumable rolls are unchanged, and a reopened or rebuilt chest never drops again.
    public static class Week22Chest2Verification
    {
        private const int ContentSamples = 60000;
        private const int MaximumSeedSearch = 4096;
        private const string ArtifactPrefix = "Chest Artifact ";
        private const string DropPrefix = "Chest Drop ";
        private const string SpellPrefix = "Chest Spell ";

        public static void SetupAndVerifyBatch()
        {
            Week22Chest2Setup.Setup();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            Week22Chest2Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(tableGuid) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath),
                "Chest-2 setup changed the chest content table GUID.");
            Verify();
        }

        // Also re-runs the verifiers that share the chest table, the chest opening rules, the exclusive pool and the
        // development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week22Chest1Verification.Verify();
            Week22Chest0Verification.Verify();
            Week20DevPanelVerification.Verify();
            Week22Flight0Verification.Verify();
            Debug.Log("Chest-2 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Chest-2 Special Rewards")]
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
            ValidateJjangsemShare(table);
            ValidateClearGoldenChest(assembler, table);
            ValidateOpenedContents(assembler, table);
            Debug.Log("Chest-2 verification passed: golden chests roll a 25% special reward that drops one golden " +
                      "exclusive artifact the player can still acquire (none at the stack limit), diamond chests " +
                      "always hold one spell with a 25% jjangsem share once one is implemented, only valid pool " +
                      "items drop, and a reopened or rebuilt chest drops nothing again.");
        }

        private static void ValidateTable(ChestContentTable table)
        {
            Assert(table.TryValidate(out string error), error);
            ChestKindRule normal = table.FindRule(ChestKind.Normal);
            ChestKindRule golden = table.FindRule(ChestKind.Golden);
            ChestKindRule diamond = table.FindRule(ChestKind.Diamond);
            Assert(golden.SpecialRewardChance >= 0.2f && golden.SpecialRewardChance <= 0.3f &&
                   Mathf.Approximately(golden.SpecialRewardChance, 0.25f) &&
                   Mathf.Approximately(golden.SpellChance, 0f) && Mathf.Approximately(golden.JjangsemShare, 0f),
                "A golden chest must roll a 25% special reward (20~30%, §3.1) and no spell.");
            Assert(Mathf.Approximately(diamond.SpellChance, 1f) && Mathf.Approximately(diamond.JjangsemShare, 0.25f) &&
                   Mathf.Approximately(diamond.SpecialRewardChance, 0f),
                "A diamond chest must always hold one spell with a 25% jjangsem share and no special reward (D2).");
            Assert(Mathf.Approximately(normal.SpellChance, 0.05f) && Mathf.Approximately(normal.JjangsemShare, 0f) &&
                   Mathf.Approximately(normal.SpecialRewardChance, 0f),
                "A normal chest must keep its 5% spell without a jjangsem spell or special reward.");

            // Contract-0 §3: valid IDs of the allowed kind only, per acquisition path.
            Assert(table.GoldenExclusiveArtifacts.Count > 0 && table.ArtifactPickupPrefab != null &&
                   table.GoldenExclusiveArtifacts.All(artifact =>
                       artifact != null && artifact.IsValid && artifact.IsActive &&
                       artifact.Kind == ItemKind.Artifact && GoldenChestExclusivePool.IsExclusive(artifact)),
                "The golden special reward pool must hold only valid, active golden exclusive artifacts.");
            Assert(table.Spells.Count > 0 && table.Spells.All(spell =>
                       spell.IsValid && spell.IsActive && spell.Kind == ItemKind.SingleUseSpell),
                "Chest spells must be valid, active single-use spells.");
            Assert(table.JjangsemSpells.SequenceEqual(Week22Chest1Setup.FindSingleUseItems(ItemKind.JjangsemSpell)) &&
                   table.JjangsemSpells.All(spell => spell.IsValid && spell.IsActive),
                "Diamond chests must draw from exactly the implemented jjangsem spells.");

            ChestContentTable broken = Object.Instantiate(table);
            ItemDefinition jjangsem = CreateJjangsem("jjangsem-chest2-check");
            try
            {
                Configure(broken, table, new ChestKindRule(ChestKind.Normal, 75, 1, new[] { 60, 30, 10 }, 0.05f, 0.25f),
                    golden, diamond);
                Assert(!broken.TryValidate(out _), "A special reward on a normal chest must be rejected.");
                Configure(broken, table, normal,
                    new ChestKindRule(ChestKind.Golden, 20, 2, new[] { 1, 1, 1 }, 1f, 0.25f, 0.25f), diamond);
                Assert(!broken.TryValidate(out _), "A jjangsem share on a golden chest must be rejected.");
                Configure(broken, table, normal, golden,
                    new ChestKindRule(ChestKind.Diamond, 5, 4, new[] { 1, 1, 1 }, 1f, 0.25f, 0.25f));
                Assert(!broken.TryValidate(out _), "A special reward on a diamond chest must be rejected.");
                Configure(broken, table, normal, golden, diamond);
                Assert(broken.TryValidate(out error), error);

                broken.ConfigureJjangsemSpells(new[] { table.Spells[0] });
                Assert(!broken.TryValidate(out _), "A single-use spell in the jjangsem list must be rejected.");
                broken.ConfigureJjangsemSpells(new[] { jjangsem, jjangsem });
                Assert(!broken.TryValidate(out _), "A duplicated jjangsem spell must be rejected.");
                broken.ConfigureJjangsemSpells(new[] { jjangsem });
                Assert(broken.TryValidate(out error), error);
            }
            finally
            {
                Object.DestroyImmediate(broken);
                Object.DestroyImmediate(jjangsem);
            }
        }

        private static void ValidateDistribution(ChestContentTable table)
        {
            foreach (ChestKind kind in new[] { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond })
            {
                ChestKindRule rule = table.FindRule(kind);
                int specials = 0;
                int spells = 0;
                int jjangsems = 0;
                Dictionary<ItemDefinition, int> artifacts =
                    table.GoldenExclusiveArtifacts.ToDictionary(artifact => artifact, _ => 0);
                for (int index = 0; index < ContentSamples; index++)
                {
                    int seed = Seed(index);
                    ChestContents contents = table.RollContents(seed, kind);
                    ChestContents replay = table.RollContents(seed, kind);
                    Assert(contents.HasSpecialReward == replay.HasSpecialReward &&
                           Mathf.Approximately(contents.SpecialRewardPick, replay.SpecialRewardPick) &&
                           contents.Spell == replay.Spell,
                        $"{kind} special reward and spell must replay for the same seed.");
                    Assert(contents.Consumables.Count >= rule.MinimumCount &&
                           contents.Consumables.Count <= rule.MaximumCount,
                        $"{kind} chest must keep {rule.MinimumCount}~{rule.MaximumCount} consumables.");

                    ItemDefinition reward = table.ResolveSpecialReward(contents, null);
                    Assert((reward != null) == contents.HasSpecialReward &&
                           (reward == null || table.IsGoldenExclusive(reward)),
                        $"{kind} special reward must be a golden exclusive artifact exactly when the roll hits.");
                    if (reward != null)
                    {
                        specials++;
                        artifacts[reward]++;
                    }

                    if (contents.Spell == null) continue;
                    spells++;
                    bool isJjangsem = contents.Spell.Kind == ItemKind.JjangsemSpell;
                    Assert(isJjangsem ? table.JjangsemSpells.Contains(contents.Spell)
                            : table.Spells.Contains(contents.Spell),
                        $"{kind} chest rolled a spell outside the table.");
                    if (isJjangsem) jjangsems++;
                }

                AssertNear(specials / (float)ContentSamples, rule.SpecialRewardChance, 0.006f, $"{kind} special reward");
                AssertNear(spells / (float)ContentSamples, rule.SpellChance, 0.004f, $"{kind} spell chance");
                if (kind != ChestKind.Golden)
                    Assert(specials == 0, $"{kind} chests must never roll the golden special reward.");
                if (kind == ChestKind.Diamond)
                    Assert(spells == ContentSamples, "Every diamond chest must hold exactly one spell.");
                else
                    Assert(jjangsems == 0, $"{kind} chests must never hold a jjangsem spell (Contract-0 §3).");
                if (kind == ChestKind.Golden)
                    foreach (KeyValuePair<ItemDefinition, int> pair in artifacts)
                        AssertNear(pair.Value / (float)specials, 1f / artifacts.Count, 0.02f,
                            $"golden special reward share of {pair.Key.ItemId}");
                if (spells > 0)
                    AssertNear(jjangsems / (float)spells, table.JjangsemSpells.Count > 0 ? rule.JjangsemShare : 0f,
                        0.006f, $"{kind} jjangsem share");
            }

            float specialPerClear = table.KindChancePerRoll(ChestKind.Golden) *
                                    table.FindRule(ChestKind.Golden).SpecialRewardChance;
            float diamondSpellPerClear = table.KindChancePerRoll(ChestKind.Diamond) *
                                         table.FindRule(ChestKind.Diamond).SpellChance;
            AssertNear(specialPerClear, 0.0165f, 0.0001f, "golden special rewards per clear");
            AssertNear(diamondSpellPerClear, 0.0165f, 0.0001f, "diamond spells per clear");
            Debug.Log($"Chest-2 expected per combat room clear: golden special reward {specialPerClear:F4}, " +
                      $"diamond spell {diamondSpellPerClear:F4}.");
        }

        // No jjangsem spell is implemented before Jjangsem-0, so the 25% share is checked on a copy with a temporary
        // one. Adding jjangsem spells must not change any chest's consumables or special reward.
        private static void ValidateJjangsemShare(ChestContentTable table)
        {
            ChestContentTable copy = Object.Instantiate(table);
            ItemDefinition jjangsem = CreateJjangsem("jjangsem-chest2-share");
            try
            {
                copy.ConfigureJjangsemSpells(table.JjangsemSpells.Append(jjangsem).ToArray());
                Assert(copy.TryValidate(out string error), error);
                int jjangsems = 0;
                for (int index = 0; index < ContentSamples; index++)
                {
                    int seed = Seed(index);
                    foreach (ChestKind kind in new[] { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond })
                    {
                        ChestContents original = table.RollContents(seed, kind);
                        ChestContents changed = copy.RollContents(seed, kind);
                        // The copy holds its own entry instances, so consumables compare by drop ID.
                        Assert(original.Consumables.Select(entry => entry.DropId)
                                   .SequenceEqual(changed.Consumables.Select(entry => entry.DropId)) &&
                               (original.Spell != null) == (changed.Spell != null) &&
                               (kind == ChestKind.Diamond || original.Spell == changed.Spell) &&
                               original.HasSpecialReward == changed.HasSpecialReward,
                            $"Jjangsem spells must not change a {kind} chest's consumables, spell hit or reward.");
                        Assert(kind == ChestKind.Diamond || changed.Spell == null ||
                               changed.Spell.Kind == ItemKind.SingleUseSpell,
                            $"{kind} chests must never hold a jjangsem spell.");
                        if (kind == ChestKind.Diamond && changed.Spell.Kind == ItemKind.JjangsemSpell) jjangsems++;
                    }
                }

                AssertNear(jjangsems / (float)ContentSamples, Week22Chest2Setup.DiamondJjangsemShare, 0.006f,
                    "diamond jjangsem share with an implemented jjangsem spell");
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(jjangsem);
            }
        }

        // The real clear path: a combat room whose seed rolls a golden chest with the special reward drops its
        // consumables and one exclusive artifact once, and never again after reopening or rebuilding the floor.
        private static void ValidateClearGoldenChest(RoomGraphAssembler assembler, ChestContentTable table)
        {
            int seed = FindSeed(assembler.Generator, floor => CombatRooms(floor).Any(node => RollsGoldenReward(table, node)));
            try
            {
                PrepareRun(assembler, seed);
                GeneratedRoomNode node = CombatRooms(assembler.GeneratedGraph.FindFloor(1))
                    .First(candidate => RollsGoldenReward(table, candidate));
                PlayerMovement player = assembler.Graph.Player;
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                int chestSeed = RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed);
                ChestContents expected = table.RollContents(chestSeed, ChestKind.Golden);
                ItemDefinition expectedReward = table.ResolveSpecialReward(expected, inventory);
                Assert(expectedReward != null, "A fresh Run must be able to acquire the golden special reward.");

                RoomClearRewardSpawner spawner = Spawner(assembler, node.RoomId);
                Assert(spawner.TrySpawn() && spawner.LastSpawnedChest != null &&
                       spawner.LastSpawnedChest.Kind == ChestKind.Golden,
                    $"Clearing {node.RoomId} must place its golden chest.");
                TreasureChest chest = spawner.LastSpawnedChest;
                Room(assembler, node.RoomId).Node.SetVisible(true);
                Assert(Children(assembler, node.RoomId, ArtifactPrefix).Length == 0,
                    "A closed golden chest must not drop its special reward.");
                Open(chest, assembler.Progress, player);

                GameObject[] artifacts = Children(assembler, node.RoomId, ArtifactPrefix);
                Assert(chest.IsOpened && artifacts.Length == 1 &&
                       artifacts[0].GetComponent<ItemPickup>()?.Definition == expectedReward &&
                       table.IsGoldenExclusive(expectedReward),
                    "A golden chest whose roll hits must drop exactly one golden exclusive artifact pickup.");
                Assert(Children(assembler, node.RoomId, DropPrefix).Length == expected.Consumables.Count &&
                       Children(assembler, node.RoomId, SpellPrefix).Length == 0,
                    "The special reward must come on top of the golden chest's seeded consumables.");
                float distance = Vector2.Distance(artifacts[0].transform.position, chest.transform.position);
                Assert(distance >= ChestPlacement.InnerContentRadius - 0.001f &&
                       distance <= ChestPlacement.OuterContentRadius + 0.001f,
                    "The special reward must drop around the chest like its other contents.");

                Open(chest, assembler.Progress, player);
                Assert(Children(assembler, node.RoomId, ArtifactPrefix).Length == 1 &&
                       Children(assembler, node.RoomId, DropPrefix).Length == expected.Consumables.Count,
                    "Opening an opened golden chest again must drop nothing.");

                Assert(assembler.TryLoadFloor(1, player, out string error), error);
                TreasureChest rebuilt = Room(assembler, node.RoomId).Node.ContentRoot
                    .GetComponentsInChildren<TreasureChest>(true).Single(candidate => candidate.gameObject.activeSelf);
                Assert(rebuilt.IsOpened && Children(assembler, node.RoomId, ArtifactPrefix).Length == 0 &&
                       Children(assembler, node.RoomId, DropPrefix).Length == 0 &&
                       !Spawner(assembler, node.RoomId).TrySpawn(),
                    "A rebuilt room must keep its opened golden chest without dropping the reward or consumables again.");
            }
            finally
            {
                assembler.Progress.ResetProgress();
            }
        }

        // Forced chests in the start room: a missed roll, a hit whose artifact the player already holds at its stack
        // limit, and a diamond chest's guaranteed spell.
        private static void ValidateOpenedContents(RoomGraphAssembler assembler, ChestContentTable table)
        {
            int runSeed = FindSeed(assembler.Generator, _ => true);
            GameObject progressObject = new("Chest-2 progress", typeof(RunProgress));
            GameObject owner = new("Chest-2 owner", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health),
                typeof(PlayerSP), typeof(PlayerStats), typeof(PlayerInventory));
            try
            {
                PrepareRun(assembler, runSeed);
                GeneratedRoomNode start = assembler.GeneratedGraph.FindFloor(1).Nodes
                    .Single(node => node.Role == GeneratedRoomRole.Start);
                PlayerMovement player = assembler.Graph.Player;
                RoomPrefab room = Room(assembler, start.RoomId);
                RoomRunState state = assembler.Progress.GetRoomState(start.RoomId);
                RoomChestSite site = new(room, start.Template, state, assembler.Progress, player.transform);

                // A missed roll drops consumables only.
                int missSeed = FindContentSeed(table, ChestKind.Golden, contents => !contents.HasSpecialReward);
                TreasureChest miss = Spawn(table, site, "chest-reward-miss", ChestKind.Golden, missSeed);
                Open(miss, assembler.Progress, player);
                Assert(miss.IsOpened && Children(assembler, start.RoomId, ArtifactPrefix).Length == 0 &&
                       ChestChildren(assembler, start.RoomId, DropPrefix, miss).Length ==
                       table.RollContents(missSeed, ChestKind.Golden).Consumables.Count,
                    "A golden chest whose roll misses must drop only its consumables.");

                // A hit whose artifact is already at its stack limit drops no pickup the player cannot take.
                int ownedSeed = FindContentSeed(table, ChestKind.Golden, contents =>
                    contents.HasSpecialReward && table.ResolveSpecialReward(contents, null).MaxStacks > 0);
                ChestContents ownedContents = table.RollContents(ownedSeed, ChestKind.Golden);
                ItemDefinition held = table.ResolveSpecialReward(ownedContents, null);
                owner.layer = LayerMask.NameToLayer("Player");
                PlayerInventory inventory = owner.GetComponent<PlayerInventory>();
                SetField(inventory, "runProgress", progressObject.GetComponent<RunProgress>());
                InvokeLifecycle(owner.GetComponent<Health>(), "Awake");
                InvokeLifecycle(owner.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(owner.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(inventory, "Awake");
                for (int stack = 0; stack < held.MaxStacks; stack++)
                    Assert(inventory.TryAcquire(held), $"The verification player must acquire {held.ItemId}.");
                ItemDefinition replacement = table.ResolveSpecialReward(ownedContents, inventory);
                Assert(replacement != held && (replacement == null || ArtifactRewardSelector.IsEligible(replacement, inventory)),
                    "The special reward must skip an artifact held at its stack limit.");
                Assert(table.GoldenExclusiveArtifacts.Count > 1 || replacement == null,
                    "With every exclusive artifact at its stack limit there must be no special reward.");

                RoomChestSite ownerSite = new(room, start.Template, state, assembler.Progress, owner.transform);
                TreasureChest owned = Spawn(table, ownerSite, "chest-reward-owned", ChestKind.Golden, ownedSeed);
                Open(owned, assembler.Progress, player);
                GameObject[] ownedArtifacts = ChestChildren(assembler, start.RoomId, ArtifactPrefix, owned);
                Assert(owned.IsOpened && ownedArtifacts.Length == (replacement != null ? 1 : 0) &&
                       ownedArtifacts.All(artifact => artifact.GetComponent<ItemPickup>().Definition == replacement) &&
                       ChestChildren(assembler, start.RoomId, DropPrefix, owned).Length ==
                       ownedContents.Consumables.Count,
                    "A golden chest must keep its consumables and never drop an artifact held at its stack limit.");

                // A diamond chest always drops one spell pickup on top of its 4~6 consumables.
                int diamondSeed = FindContentSeed(table, ChestKind.Diamond, _ => true);
                ChestContents diamondContents = table.RollContents(diamondSeed, ChestKind.Diamond);
                TreasureChest diamond = Spawn(table, site, "chest-diamond-spell", ChestKind.Diamond, diamondSeed);
                Open(diamond, assembler.Progress, player);
                GameObject[] spells = ChestChildren(assembler, start.RoomId, SpellPrefix, diamond);
                SingleUseItemPickup spell = spells.Length == 1 ? spells[0].GetComponent<SingleUseItemPickup>() : null;
                Assert(diamond.IsOpened && spell != null && spell.Definition == diamondContents.Spell &&
                       spell.Definition.IsValid && spell.Definition.IsSingleUse && spell.WaitsForPlayerExit &&
                       spell.InstanceId == $"{start.RoomId}-{diamond.ChestId}-spell",
                    "A diamond chest must drop its one seeded spell as a slot pickup with a Run-unique instance ID.");
                int consumables = ChestChildren(assembler, start.RoomId, DropPrefix, diamond).Length;
                Assert(consumables == diamondContents.Consumables.Count && consumables >= 4 && consumables <= 6 &&
                       ChestChildren(assembler, start.RoomId, ArtifactPrefix, diamond).Length == 0,
                    "A diamond chest must drop 4~6 consumables with its spell and no special reward.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(progressObject);
                assembler.Progress.ResetProgress();
            }
        }

        private static void Configure(ChestContentTable target, ChestContentTable source, params ChestKindRule[] rules)
        {
            target.Configure(source.ChestChance, rules, source.Consumables.ToArray(), source.Spells.ToArray(),
                source.ChestPrefab, source.SpellPickupPrefab);
        }

        private static ItemDefinition CreateJjangsem(string itemId)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, "검증용 짱셈스펠", ItemKind.JjangsemSpell, ItemRarity.Common, true, 1,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
            Assert(definition.IsValid, $"{itemId} must be a valid jjangsem spell for the check.");
            return definition;
        }

        private static TreasureChest Spawn(ChestContentTable table, RoomChestSite site, string chestId, ChestKind kind,
            int contentSeed)
        {
            TreasureChest chest = RoomChestSpawner.Spawn(table, site, chestId, kind, contentSeed, out string error);
            Assert(chest != null && chest.IsClosed, error);
            return chest;
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

        private static int Seed(int index) => RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 3);

        private static int FindContentSeed(ChestContentTable table, ChestKind kind, Func<ChestContents, bool> wanted)
        {
            for (int index = 0; index < MaximumSeedSearch; index++)
                if (wanted(table.RollContents(Seed(index), kind))) return Seed(index);
            throw new InvalidOperationException($"No seed produced the wanted {kind} chest contents.");
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

            throw new InvalidOperationException("No seed produced the Chest-2 verification floor.");
        }

        private static IEnumerable<GeneratedRoomNode> CombatRooms(GeneratedFloor floor) =>
            floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null);

        private static bool RollsGoldenReward(ChestContentTable table, GeneratedRoomNode node)
        {
            int seed = RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed);
            return table.TryRollChest(seed, out ChestKind kind) && kind == ChestKind.Golden &&
                   table.RollContents(seed, kind).HasSpecialReward;
        }

        private static RoomPrefab Room(RoomGraphAssembler assembler, string roomId) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == roomId);

        private static RoomClearRewardSpawner Spawner(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Controller.GetComponent<RoomClearRewardSpawner>();

        private static GameObject[] Children(RoomGraphAssembler assembler, string roomId, string prefix) =>
            Room(assembler, roomId).Node.ContentRoot.transform.Cast<Transform>().Select(child => child.gameObject)
                .Where(child => child.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

        // Content names end with the chest ID they came from.
        private static GameObject[] ChestChildren(RoomGraphAssembler assembler, string roomId, string prefix,
            TreasureChest chest) =>
            Children(assembler, roomId, prefix)
                .Where(child => child.name.EndsWith(" " + chest.ChestId, StringComparison.Ordinal)).ToArray();

        private static void InvokeLifecycle(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void AssertNear(float actual, float expected, float tolerance, string label)
        {
            Assert(Mathf.Abs(actual - expected) <= tolerance,
                $"Chest-2 {label} was {actual:F4}; expected {expected:F4} ± {tolerance:F4}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
