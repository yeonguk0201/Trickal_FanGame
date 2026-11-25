using System;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Balance-1 (2026-11-24): the 3-floor beta hands out builds faster. A normal chest holds an artifact 20% and a
    // spell 20% of the time, a golden chest always holds an artifact (golden exclusive first, treasure pool after),
    // and three combat clears in a row without a chest item guarantee one on the next clear. The rates live in a
    // per-Run-length row so longer Runs get their own.
    public static class Balance1ChestRewardVerification
    {
        private const int Samples = 40000;
        private const int SeedCount = 512;

        [MenuItem("Trickal Fan Game/Beta/Verify Balance-1 Chest Rewards")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.Generator != null && assembler.Progress != null &&
                   assembler.ChestContentTable != null,
                "Balance-1 requires the Game Scene assembler, generator, Run progress and chest table.");
            ChestContentTable table = assembler.ChestContentTable;
            ChestRewardRates rates = VerifyTable(table, assembler.Generator.FloorCount);
            string shares = VerifyShares(table, rates);
            VerifyPityCounter();
            VerifyForcedClearChest(assembler, rates);
            Debug.Log($"Balance-1 chest reward verification passed: {shares}; golden chests always hold an " +
                      $"artifact; {rates.ItemPityClears} item-less clears guarantee a chest item on the next clear.");
        }

        private static ChestRewardRates VerifyTable(ChestContentTable table, int runFloorCount)
        {
            Assert(table.TryValidate(out string error), error);
            ChestRewardRates beta = table.RatesFor(runFloorCount);
            Assert(runFloorCount == 3 && beta != null && beta.FloorCount == 3 &&
                   Mathf.Approximately(beta.NormalArtifactChance, 0.2f) &&
                   Mathf.Approximately(beta.NormalSpellChance, 0.2f) && beta.ItemPityClears == 3,
                "The 3-floor beta must use the 3-floor row: 20% artifact, 20% spell, pity after 3 clears.");
            Assert(table.RatesFor(0) == beta && table.RatesFor(12) != null,
                "An unknown or longer Run length must still find a reward row.");
            Assert(table.RewardRates.Count == Drop0DropRateSetup.ChestItemRates.Length &&
                   Mathf.Approximately(table.FindRule(ChestKind.Golden).SpecialRewardChance, 1f),
                "The chest table must hold the Balance-1 rows and an always-artifact golden chest.");
            return beta;
        }

        private static string VerifyShares(ChestContentTable table, ChestRewardRates rates)
        {
            int artifacts = 0, spells = 0;
            for (int index = 0; index < Samples; index++)
            {
                int seed = RoomClearRewardSpawner.DeriveChestSeed(index * 7919 + 11);
                ChestContents contents = table.RollContents(seed, ChestKind.Normal, rates, false);
                ChestContents replay = table.RollContents(seed, ChestKind.Normal, rates, false);
                Assert(contents.HasArtifact == replay.HasArtifact && contents.Spell == replay.Spell,
                    "The same seed must roll the same normal chest item.");
                Assert(!(contents.HasArtifact && contents.Spell != null) &&
                       (!contents.HasArtifact || contents.FallbackSpell != null),
                    "A normal chest holds one item at most, and an artifact keeps a fallback spell.");
                if (contents.HasArtifact) artifacts++;
                if (contents.Spell != null) spells++;
                Assert(table.RollContents(seed, ChestKind.Normal, rates, true).HasItem,
                    "A pity chest must always hold an artifact or a spell.");
                Assert(table.RollContents(seed, ChestKind.Golden, rates, false).HasSpecialReward,
                    "A golden chest must always hold its artifact.");
            }

            float artifactShare = artifacts / (float)Samples;
            float spellShare = spells / (float)Samples;
            Assert(Mathf.Abs(artifactShare - rates.NormalArtifactChance) < 0.01f &&
                   Mathf.Abs(spellShare - rates.NormalSpellChance) < 0.01f,
                $"Normal chests rolled an artifact {artifactShare:P1} and a spell {spellShare:P1} of the time.");
            return $"normal chest artifact {artifactShare:P1}, spell {spellShare:P1} over {Samples} seeds";
        }

        private static void VerifyPityCounter()
        {
            ResourcePity pity = new();
            for (int clear = 0; clear < 3; clear++)
            {
                Assert(!pity.ShouldGuaranteeItem(3), $"Clear {clear + 1} must not be guaranteed yet.");
                pity.RecordItemClear(false);
            }

            Assert(pity.ShouldGuaranteeItem(3) && pity.ItemDroughtClears == 3,
                "After three item-less clears the next clear must be guaranteed.");
            pity.RecordItemClear(true);
            Assert(!pity.ShouldGuaranteeItem(3) && pity.ItemDroughtClears == 0,
                "A chest item must reset the item pity.");
            pity.RecordItemClear(false);
            pity.Clear();
            Assert(pity.ItemDroughtClears == 0, "A new Run must start the item pity at zero.");
        }

        // A clear whose roll gives no chest still places a normal chest holding an item once the pity is due; the
        // flag is recorded, so the rebuilt floor restores the same chest and the pity starts over.
        private static void VerifyForcedClearChest(RoomGraphAssembler assembler, ChestRewardRates rates)
        {
            ChestContentTable table = assembler.ChestContentTable;
            RunProgress progress = assembler.Progress;
            try
            {
                (int seed, string roomId) = FindMissedRoll(assembler.Generator, table);
                progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                PlayerMovement player = assembler.Graph.Player;
                progress.Pity.SetItemDroughtForDevelopment(rates.ItemPityClears);
                RoomClearRewardSpawner spawner = Spawner(assembler, roomId);
                RoomRunState state = progress.GetRoomState(roomId);
                Assert(spawner.TrySpawn() && spawner.LastSpawnedChest != null &&
                       spawner.LastSpawnedChest.Kind == ChestKind.Normal && spawner.LastClearForcedItem &&
                       state.GetChest(RoomClearRewardSpawner.ClearChestId)?.ForcesItem == true,
                    $"Room {roomId} rolled no chest and must place a pity chest that holds an item.");
                Assert(progress.Pity.ItemDroughtClears == 0, "The pity chest must reset the item pity.");

                ChestContents expected = table.RollContents(spawner.ChestSeed, ChestKind.Normal, rates, true);
                Assert(expected.HasItem, "The pity chest's contents must hold an item.");
                Assert(assembler.TryLoadFloor(1, player, out error), error);
                TreasureChest restored = Room(assembler, roomId).Node.ContentRoot
                    .GetComponentsInChildren<TreasureChest>(true)
                    .Single(chest => chest.ChestId == RoomClearRewardSpawner.ClearChestId);
                Assert(restored.IsClosed && restored.Kind == ChestKind.Normal &&
                       state.GetChest(RoomClearRewardSpawner.ClearChestId).ForcesItem,
                    "A floor rebuild must restore the closed pity chest.");

                Room(assembler, roomId).Node.SetVisible(true);
                Transform content = Room(assembler, roomId).Node.ContentRoot.transform;
                int before = content.childCount;
                Assert(restored.TryOpenByTouch(player) && restored.IsOpened, "The pity chest must open by touch.");
                bool droppedItem = content.GetComponentsInChildren<ItemPickup>(true).Any(pickup =>
                                       pickup.name.Contains(restored.ChestId)) ||
                                   content.GetComponentsInChildren<SingleUseItemPickup>(true).Any(pickup =>
                                       pickup.name.Contains(restored.ChestId));
                Assert(droppedItem && content.childCount - before == expected.Consumables.Count + 1,
                    "Opening the pity chest must drop its consumables and one artifact or spell.");
            }
            finally
            {
                progress.ResetProgress();
            }
        }

        private static (int seed, string roomId) FindMissedRoll(FloorGenerator generator, ChestContentTable table)
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                GeneratedRoomNode room = graph.FindFloor(1)?.Nodes.FirstOrDefault(node =>
                    node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null &&
                    !table.TryRollChest(RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed), out _));
                if (room != null) return (seed, room.RoomId);
            }

            throw new InvalidOperationException("No seed has a floor-1 combat room whose clear rolls no chest.");
        }

        private static RoomPrefab Room(RoomGraphAssembler assembler, string roomId) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == roomId);

        private static RoomClearRewardSpawner Spawner(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Controller.GetComponent<RoomClearRewardSpawner>();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
