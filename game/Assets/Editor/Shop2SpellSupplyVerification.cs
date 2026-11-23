using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Drop-2 and Shop-2 (docs/28 §10.3): normal chests hold a spell 10% of the time, and each consumable slot of a
    // seeded Run's shop may sell a distinct spell without changing the shop's Items.
    public static class Shop2SpellSupplyVerification
    {
        private const string RoomId = "floor-01-room-09";
        private const int ChestSamples = 20000;
        private const int ShopSamples = 4096;
        private const int ExpectedSpellPrice = 10;
        private const int ExpectedSpellSlotPercent = 25;

        [MenuItem("Trickal Fan Game/Beta/Verify Drop-2 and Shop-2 Spell Supply")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.ChestContentTable != null && assembler.ShopCatalog != null,
                "The Game Scene needs its chest table and shop catalog.");
            ValidateChest(assembler.ChestContentTable);
            ValidateStock(assembler);
            ValidatePurchase(assembler);
            ValidateAssembledShop(assembler);
            Debug.Log("Drop-2 and Shop-2 verification passed.");
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week22Chest1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week20Special4Verification.Verify();
            Week22Shop0Verification.Verify();
            Week23Shop1Verification.Verify();
            Week22Spell4Verification.Verify();
            Week27Elif3Verification.Verify();
            Week27Power2Verification.Verify();
            Debug.Log("Drop-2 and Shop-2 regression verification passed.");
        }

        private static void ValidateChest(ChestContentTable table)
        {
            ChestKindRule normal = table.FindRule(ChestKind.Normal);
            Assert(Mathf.Approximately(normal.SpellChance, 0.1f), "A normal chest must hold a spell 10% of the time.");
            Assert(Mathf.Approximately(table.FindRule(ChestKind.Golden).SpellChance, 0f) &&
                   Mathf.Approximately(table.FindRule(ChestKind.Diamond).SpellChance, 1f),
                "Drop-2 must not change golden or diamond chest spells.");
            int hits = Enumerable.Range(1, ChestSamples)
                .Count(seed => table.RollContents(seed, ChestKind.Normal).Spell != null);
            float share = hits / (float)ChestSamples;
            Assert(Mathf.Abs(share - 0.1f) < 0.01f, $"Normal chests rolled a spell {share:P2} of the time.");
            Debug.Log($"Drop-2: normal chest spell share {share:P2} over {ChestSamples} seeds.");
        }

        private static void ValidateStock(RoomGraphAssembler assembler)
        {
            ShopCatalog catalog = assembler.ShopCatalog;
            ChestContentTable table = assembler.ChestContentTable;
            Assert(catalog.SpellPrice == ExpectedSpellPrice && catalog.SpellSlotPercent == ExpectedSpellSlotPercent,
                "Shop spells must start at 10 gold and a 25% chance per consumable slot.");
            ValidateNoMoneyItems(assembler);

            int total = 0;
            int[] perShop = new int[ShopCatalog.ConsumableOfferCount + 1];
            foreach (ShopKind kind in new[] { ShopKind.General, ShopKind.Goldi })
            {
                for (int seed = 1; seed <= ShopSamples; seed++)
                {
                    ShopStockState plain = Build(assembler, seed, kind, false);
                    ShopStockState mixed = Build(assembler, seed, kind, true);
                    ShopStockState repeated = Build(assembler, seed, kind, true);
                    Assert(plain.Offers.All(offer => offer.Kind != ShopOfferKind.Spell),
                        "A shop without a spell pool must not sell spells.");
                    Assert(Ids(mixed) == Ids(repeated), $"Seed {seed} must replay the same stock.");
                    Assert(mixed.Offers.Count == ShopCatalog.OfferCount, $"Seed {seed} must fill every base slot.");
                    Assert(Ids(mixed, ShopCatalog.ItemOfferCount) == Ids(plain, ShopCatalog.ItemOfferCount),
                        $"Seed {seed}: spells must not change the shop's Items.");

                    ShopOffer[] spells = mixed.Offers.Where(offer => offer.Kind == ShopOfferKind.Spell).ToArray();
                    Assert(spells.All(offer => offer.SlotIndex >= ShopCatalog.ItemOfferCount &&
                                               offer.Price == catalog.SpellPrice &&
                                               offer.SpellPickupPrefab == table.SpellPickupPrefab &&
                                               ShopStockBuilder.IsSellableSpell(offer.Item) &&
                                               table.Spells.Contains(offer.Item)),
                        $"Seed {seed} sold an invalid spell offer.");
                    Assert(spells.Select(offer => offer.Item.ItemId).Distinct().Count() == spells.Length,
                        $"Seed {seed} sold the same spell twice.");
                    total += spells.Length;
                    perShop[spells.Length]++;
                }
            }

            float expected = total / (float)(ShopSamples * 2);
            Assert(Mathf.Abs(expected - 0.75f) < 0.05f, $"Shops must average about 0.75 spells, not {expected:F3}.");
            Assert(perShop.All(count => count > 0), "Shops must be able to sell 0, 1, 2 or 3 spells.");
            Debug.Log($"Shop-2: {expected:F3} spells per shop, 0/1/2/3 spells = {string.Join("/", perShop)}.");
        }

        // User decision (2026-11-23): shops never sell money spells or artifacts, even if one reaches their pools.
        private static void ValidateNoMoneyItems(RoomGraphAssembler assembler)
        {
            ChestContentTable table = assembler.ChestContentTable;
            string[] moneySpells = table.Spells.Where(ShopStockBuilder.IsMoneyItem).Select(spell => spell.ItemId)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert(moneySpells.SequenceEqual(new[]
                {
                    "single-spell-membership-card", "single-spell-netti-map", "single-spell-random-coin",
                }),
                $"The money spells must be the membership card, Netti's map and the random coin: " +
                $"{string.Join(", ", moneySpells)}.");
            Assert(moneySpells.All(id => !ShopStockBuilder.IsSellableSpell(table.Spells.First(s => s.ItemId == id))),
                "Money spells must not be sellable.");

            ItemDefinition wallet = table.GoldenExclusiveArtifacts.FirstOrDefault(item =>
                item.ItemId == "artifact-aisia-wallet");
            Assert(wallet != null && ShopStockBuilder.IsMoneyItem(wallet), "Aisia's wallet must count as a money item.");
            Assert(!assembler.SelectionRewardPool.Any(ShopStockBuilder.IsMoneyItem),
                "The shop's artifact pool must hold no money artifact.");

            ItemDefinition[] poisoned = assembler.SelectionRewardPool.Append(wallet).ToArray();
            foreach (ShopKind kind in new[] { ShopKind.General, ShopKind.Goldi })
            {
                for (int seed = 1; seed <= 512; seed++)
                {
                    ShopStockState stock = ShopStockBuilder.Build(RoomId, seed, assembler.ShopCatalog, poisoned, null,
                        kind, null, false, table.Spells, table.SpellPickupPrefab);
                    stock.EnsureExtraSlots(ShopStockState.MaxExtraSlots);
                    Assert(stock.Offers.All(offer => !ShopStockBuilder.IsMoneyItem(offer.Item)),
                        $"Seed {seed} sold a money item.");
                }
            }
        }

        private static void ValidatePurchase(RoomGraphAssembler assembler)
        {
            int seed = Enumerable.Range(1, ShopSamples).First(candidate =>
                Build(assembler, candidate, ShopKind.General, true).Offers.Any(offer => offer.Kind == ShopOfferKind.Spell));
            GameObject root = new("Shop-2 Purchase");
            try
            {
                RunProgress progress = root.AddComponent<RunProgress>();
                ShopRoom shop = root.AddComponent<ShopRoom>();
                ShopStockState stock = Build(assembler, seed, ShopKind.General, true);
                shop.Configure(progress, stock, root.transform);
                ShopOffer offer = stock.Offers.First(candidate => candidate.Kind == ShopOfferKind.Spell);

                Assert(shop.TryPurchase(offer.SlotIndex, null) == ShopPurchaseResult.NotEnoughGold &&
                       !stock.IsPurchased(offer.SlotIndex) && shop.LastDroppedPickup == null,
                    "A spell must not be sold without enough gold.");
                progress.TryAddResource(RunResourceType.Gold, offer.Price + 1);
                Assert(shop.TryPurchase(offer.SlotIndex, null) == ShopPurchaseResult.Purchased &&
                       progress.GetResourceCount(RunResourceType.Gold) == 1 && stock.IsPurchased(offer.SlotIndex),
                    "Buying a spell must spend its price once and sell out its slot.");
                SingleUseItemPickup pickup = shop.LastDroppedPickup != null
                    ? shop.LastDroppedPickup.GetComponent<SingleUseItemPickup>()
                    : null;
                Assert(pickup != null && pickup.Definition == offer.Item && pickup.WaitsForPlayerExit &&
                       pickup.transform.parent == root.transform &&
                       Vector2.Distance(pickup.transform.position, shop.GetDropPosition(offer.SlotIndex)) < 0.01f,
                    "A bought spell must drop as its floor pickup at the slot's spot.");
                Assert(shop.TryPurchase(offer.SlotIndex, null) == ShopPurchaseResult.SoldOut &&
                       progress.GetResourceCount(RunResourceType.Gold) == 1,
                    "A sold spell must not be bought again.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The real assembler sells spells in a seeded Run and keeps Editor fixtures without a Run seed spell-free.
        private static void ValidateAssembledShop(RoomGraphAssembler assembler)
        {
            RunProgress progress = assembler.Progress;
            bool found = false;
            try
            {
                // ResetProgress keeps the Run seed, so it is set once; each loop only rebuilds a generated graph.
                Assert(progress.HasRunSeed || progress.TryInitializeRunSeed(1, out _), "The Run seed must initialize.");
                for (int seed = 1; seed <= 128 && !found; seed++)
                {
                    if (!assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                    GeneratedRoomNode node = graph.FindFloor(1).Nodes
                        .FirstOrDefault(candidate => candidate.Role == GeneratedRoomRole.Shop);
                    if (node == null) continue;
                    progress.ResetProgress();
                    Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                    ShopStockState stock = progress.GetShopStock(ShopStockBuilder.BuildShopId(node.RoomId));
                    Assert(stock != null, $"Seed {seed} built no shop stock.");
                    found = stock.Offers.Any(offer => offer.Kind == ShopOfferKind.Spell);
                }
            }
            finally
            {
                progress.ResetProgress();
                // Reload without saving: later verifiers need the Scene's unseeded RunProgress and no built rooms.
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }

            Assert(found, "No seeded Run's floor-1 shop sold a spell.");
        }

        private static ShopStockState Build(RoomGraphAssembler assembler, int seed, ShopKind kind, bool withSpells) =>
            ShopStockBuilder.Build(RoomId, seed, assembler.ShopCatalog, assembler.SelectionRewardPool, null, kind, null,
                false, withSpells ? assembler.ChestContentTable.Spells : null,
                withSpells ? assembler.ChestContentTable.SpellPickupPrefab : null);

        private static string Ids(ShopStockState stock, int count = int.MaxValue) =>
            string.Join(",", stock.Offers.Take(count).Select(offer => offer.OfferId));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
