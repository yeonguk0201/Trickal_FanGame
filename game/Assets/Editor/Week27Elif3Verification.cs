using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TrickalFanGame.Editor.ArtifactVerificationFixtures;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Elif-3 (contract 27 §7.1): the shop side of the permanent elif. Checks the upgrade catalog against the Backend,
    // elif prices and the 25% elif offer (same Items, only one price changes), discounts, extra slots fixed by shop
    // and slot, the Run's upgrade purchases and their save request, the start balance with pending Runs, purchases
    // in elif, and the invest screen.
    public static class Week27Elif3Verification
    {
        private const int ElifOfferSamples = 4000;
        private const string RoomId = "floor-01-room-09";

        [MenuItem("Trickal Fan Game/Week 27/Verify Elif-3 Shop Upgrades")]
        public static void Verify()
        {
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            ShopRoom shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath)
                ?.GetComponent<ShopRoom>();
            Assert(catalog != null && shopPrefab != null, "Run Special-4 setup first: the shop catalog or Prefab is missing.");
            ItemDefinition[] pool = LoadSelectionPool();

            ValidateCatalog();
            ValidatePrices(catalog);
            ValidateElifOffers(catalog, pool);
            ValidateExtraSlots(catalog, pool);
            ValidateRunUpgrades();
            ValidateElifStart();
            ValidateSaveRequest();
            ValidateOrderedSubmission();
            ValidateImmediateStart();
            ValidateShopPurchases(catalog, pool, shopPrefab);
            ValidateScreen();
            Debug.Log("Week 27 Elif-3 verification passed: the upgrade catalog matches the Backend, about 25% of " +
                      "seeded shops sell one artifact for gold ÷ 10 (rounded up) elif without changing the Items, " +
                      "discounts follow max(1, floor(price × (1 − 10% × level))) on gold only, extra slots open at " +
                      "once with offers fixed by shop and slot, purchases are recorded in order and saved, the start " +
                      "subtracts pending Runs' spending, a Run that bought upgrades is sent after the pending Runs " +
                      "(offline play keeps working), and the invest screen buys upgrades.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week27Elif3Setup.Setup();
            Verify();
        }

        // The shop rules, prices, the Run save request, pending storage and the dev panel share the changed paths.
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week20Special4Verification.Verify();
            Week22Shop0Verification.Verify();
            Week23Shop1Verification.Verify();
            Week22Spell4Verification.Verify();
            Week26Spell6Verification.Verify();
            Week27Power2Verification.Verify();
            Week21Gold0Verification.Verify();
            Week13Flow5Verification.Verify();
            PhaseHMetaProgressionVerification.Verify();
            Week27Elif2Verification.Verify();
            Week20DevPanelVerification.Verify();
            Verify();
            Debug.Log("Week 27 Elif-3 regression verification passed.");
        }

        private static ItemDefinition[] LoadSelectionPool()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.Where(item => item != null).ToArray();
            Assert(pool.Length >= ShopCatalog.ItemOfferCount + ShopStockState.MaxExtraSlots,
                "The shop Item pool must hold enough Items for three offers and three extra slots.");
            return pool;
        }

        private static void ValidateCatalog()
        {
            Assert(ShopUpgrades.Catalog.Select(upgrade => upgrade.UpgradeId)
                       .SequenceEqual(new[] { ShopUpgrades.ExtraSlotId, ShopUpgrades.DiscountId }) &&
                   ShopUpgrades.ExtraSlotId == "shop-upgrade-extra-slot" &&
                   ShopUpgrades.DiscountId == "shop-upgrade-discount" &&
                   ShopUpgrades.Find(ShopUpgrades.ExtraSlotId).LevelCosts.SequenceEqual(new[] { 2, 3, 5 }) &&
                   ShopUpgrades.Find(ShopUpgrades.DiscountId).LevelCosts.SequenceEqual(new[] { 2, 3, 4 }),
                "The shop upgrades must keep their stable IDs and the contract 27 §4.2 costs.");
            Assert(ShopUpgrades.Find(ShopUpgrades.ExtraSlotId).MaxLevel == ShopStockState.MaxExtraSlots &&
                   ShopStockState.FirstExtraSlot == ShopCatalog.OfferCount &&
                   ShopUpgrades.ElifOfferPercent == 25 && ShopUpgrades.DiscountPercentPerLevel == 10 &&
                   ShopUpgrades.GetCost(ShopUpgrades.DiscountId, 0) == -1 &&
                   ShopUpgrades.GetCost(ShopUpgrades.DiscountId, 4) == -1 && ShopUpgrades.GetCost("unknown", 1) == -1,
                "상품 칸 추가 must fill the empty last grid row, and costs outside 1..max must not exist.");
            Assert((int)ShopPurchaseResult.Unavailable == 4 && (int)ShopPurchaseResult.NotEnoughElif == 5 &&
                   (int)ShopPurchaseResult.ElifLocked == 6 && (int)ShopOfferStatus.SoldOut == 5 &&
                   (int)ShopOfferStatus.NotEnoughElif == 6 && (int)ShopOfferStatus.ElifLocked == 7,
                "Elif-3 must append its shop results and statuses without renumbering earlier ones.");

            // contract-drift-check: backend/src/contracts/permanent-elif.ts SHOP_UPGRADE_CATALOG.
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "backend", "src",
                "contracts", "permanent-elif.ts"));
            Assert(File.Exists(backendPath), $"The Backend upgrade catalog is missing at {backendPath}.");
            string source = File.ReadAllText(backendPath, System.Text.Encoding.UTF8);
            MatchCollection matches = Regex.Matches(source,
                @"\{\s*upgradeId:\s*'(?<id>[^']+)',\s*levelCosts:\s*\[(?<costs>[^\]]*)\]\s*\}");
            string backend = string.Join("|", matches.Cast<Match>().Select(match =>
                $"{match.Groups["id"].Value}={Regex.Replace(match.Groups["costs"].Value, @"\s", string.Empty)}"));
            string unity = string.Join("|", ShopUpgrades.Catalog.Select(upgrade =>
                $"{upgrade.UpgradeId}={string.Join(",", upgrade.LevelCosts)}"));
            Assert(backend == unity, $"Unity upgrade catalog '{unity}' must match the Backend catalog '{backend}'.");
            Assert(Regex.IsMatch(source, @"ELIF_RUN_GAIN_MAX\s*=\s*" + PermanentElif.RunGainMax + ";") &&
                   Regex.IsMatch(source, @"ELIF_BALANCE_MAX\s*=\s*" + PermanentElif.BalanceMax + ";"),
                "Unity's elif limits must match the Backend.");
        }

        private static void ValidatePrices(ShopCatalog catalog)
        {
            int[] gold =
            {
                catalog.GetItemPrice(ItemRarity.Common), catalog.GetItemPrice(ItemRarity.Uncommon),
                catalog.GetItemPrice(ItemRarity.Rare), catalog.GetItemPrice(ItemRarity.Epic),
            };
            Assert(gold.SequenceEqual(new[] { 10, 15, 20, 25 }) &&
                   gold.Select(ShopUpgrades.ElifPriceOf).SequenceEqual(new[] { 1, 2, 2, 3 }) &&
                   ShopUpgrades.ElifPriceOf(0) == 0 && ShopUpgrades.ElifPriceOf(1) == 1 &&
                   ShopUpgrades.ElifPriceOf(11) == 2,
                "Elif prices must be gold ÷ 10 rounded up: Common 1, Uncommon 2, Rare 2, Epic 3.");

            int[] prices = { 10, 15, 20, 25, 3, 5 };
            int[][] expected =
            {
                new[] { 10, 15, 20, 25, 3, 5 },
                new[] { 9, 13, 18, 22, 2, 4 },
                new[] { 8, 12, 16, 20, 2, 4 },
                new[] { 7, 10, 14, 17, 2, 3 },
            };
            for (int level = 0; level < expected.Length; level++)
                Assert(prices.Select(price => ShopUpgrades.ApplyDiscount(price, level)).SequenceEqual(expected[level]),
                    $"Discount level {level} must follow the contract 27 §4.2 examples.");
            Assert(ShopUpgrades.ApplyDiscount(1, 3) == 1 && ShopUpgrades.ApplyDiscount(0, 3) == 0 &&
                   ShopUpgrades.ApplyDiscount(25, 9) == 17 && ShopUpgrades.DiscountPercent(9) == 30,
                "A discount never makes a price free and stops at level 3.");
        }

        private static void ValidateElifOffers(ShopCatalog catalog, ItemDefinition[] pool)
        {
            int withElif = 0;
            for (int seed = 1; seed <= ElifOfferSamples; seed++)
            {
                ShopStockState plain = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null);
                ShopStockState rolled = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null, ShopKind.General,
                    null, true);
                Assert(plain.Offers.Count == rolled.Offers.Count &&
                       plain.Offers.Zip(rolled.Offers, (left, right) =>
                           left.OfferId == right.OfferId && left.Price == right.Price &&
                           left.SlotIndex == right.SlotIndex).All(same => same) &&
                       plain.Offers.All(offer => !offer.IsElifPriced),
                    $"Shop seed {seed}: the elif roll must not change which Items are sold or their gold prices.");
                ShopOffer[] elif = rolled.Offers.Where(offer => offer.IsElifPriced).ToArray();
                bool hit = ShopStockBuilder.RollElifOffer(ShopStockBuilder.StockSeedOf(seed),
                    rolled.Offers.Count(offer => offer.Kind == ShopOfferKind.Item && !offer.Item.IsActivePower),
                    out int index);
                Assert(elif.Length == (hit ? 1 : 0), $"Shop seed {seed} must have at most one elif offer.");
                if (!hit) continue;
                withElif++;
                ShopOffer offer = elif[0];
                Assert(offer.SlotIndex == index && offer.Kind == ShopOfferKind.Item && !offer.Item.IsActivePower &&
                       offer.ElifPrice == ShopUpgrades.ElifPriceOf(offer.Price) &&
                       offer.Price == catalog.GetItemPrice(offer.Item.Rarity),
                    $"Shop seed {seed}: the elif offer must be an artifact at gold ÷ 10 elif.");
                ShopStockState repeated = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null, ShopKind.General,
                    null, true);
                Assert(repeated.Offers.Single(candidate => candidate.IsElifPriced).SlotIndex == offer.SlotIndex,
                    "The same shop seed must replay its elif offer.");
            }

            float rate = withElif / (float)ElifOfferSamples;
            Assert(Mathf.Abs(rate - ShopUpgrades.ElifOfferPercent / 100f) <= 0.03f,
                $"About 25% of shops must sell an elif offer; got {rate:P1}.");
        }

        private static void ValidateExtraSlots(ShopCatalog catalog, ItemDefinition[] pool)
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                ShopStockState all = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null);
                Assert(all.GetOffer(ShopStockState.FirstExtraSlot) == null && all.ExtraSlotCount == 0,
                    "A shop without the upgrade must leave the last row empty.");
                Assert(all.EnsureExtraSlots(ShopStockState.MaxExtraSlots) &&
                       all.ExtraSlotCount == ShopStockState.MaxExtraSlots &&
                       !all.EnsureExtraSlots(9) && !all.EnsureExtraSlots(1) &&
                       all.Offers.Count == ShopCatalog.OfferCount + ShopStockState.MaxExtraSlots,
                    $"Shop seed {seed} must open exactly three extra slots and never close them.");
                ShopOffer[] extras = Enumerable.Range(ShopStockState.FirstExtraSlot, ShopStockState.MaxExtraSlots)
                    .Select(all.GetOffer).ToArray();
                HashSet<string> baseItems = new(all.Offers.Take(ShopCatalog.OfferCount)
                    .Where(offer => offer.Kind == ShopOfferKind.Item).Select(offer => offer.Item.ItemId));
                Assert(extras.All(offer => offer != null && offer.Kind == ShopOfferKind.Item &&
                                           !offer.IsElifPriced && !baseItems.Contains(offer.Item.ItemId) &&
                                           offer.Price == catalog.GetItemPrice(offer.Item.Rarity)) &&
                       extras.Select(offer => offer.Item.ItemId).Distinct().Count() == extras.Length,
                    $"Shop seed {seed}: extra slots must sell new distinct artifacts at gold price.");

                ShopStockState stepwise = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null);
                for (int count = 1; count <= ShopStockState.MaxExtraSlots; count++)
                    Assert(stepwise.EnsureExtraSlots(count) && stepwise.ExtraSlotCount == count,
                        "Each upgrade level must open one more slot.");
                Assert(Enumerable.Range(ShopStockState.FirstExtraSlot, ShopStockState.MaxExtraSlots)
                        .All(slot => stepwise.GetOffer(slot).OfferId == all.GetOffer(slot).OfferId),
                    $"Shop seed {seed}: an extra slot's offer must depend only on the shop and the slot.");
            }

            ItemDefinition[] better = pool.Where(item => item.Rarity >= ShopStockBuilder.GoldiMinimumRarity).ToArray();
            if (better.Length < ShopCatalog.ItemOfferCount + ShopStockState.MaxExtraSlots) return;
            for (int seed = 1; seed <= 50; seed++)
            {
                ShopStockState goldi = ShopStockBuilder.Build(RoomId, seed, catalog, pool, null, ShopKind.Goldi);
                goldi.EnsureExtraSlots(ShopStockState.MaxExtraSlots);
                Assert(goldi.ExtraSlotCount == ShopStockState.MaxExtraSlots &&
                       goldi.Offers.Skip(ShopCatalog.OfferCount)
                           .All(offer => offer.Item.Rarity >= ShopStockBuilder.GoldiMinimumRarity),
                    "골디's extra slots must sell Rare or better artifacts.");
            }
        }

        private static void ValidateRunUpgrades()
        {
            GameObject holder = new("Elif-3 Upgrades");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                int changes = 0;
                progress.ShopUpgradesChanged += () => changes++;
                Assert(progress.IsElifSpendingAllowed &&
                       ShopUpgrades.Catalog.All(upgrade => progress.GetShopUpgradeLevel(upgrade.UpgradeId) == 0),
                    "A new Run must start with no upgrades.");
                progress.ConfigureElifStart(10, new Dictionary<string, int> { [ShopUpgrades.DiscountId] = 1 }, null,
                    true);
                progress.RecordRoomEntry(2, 1);
                Assert(progress.AvailableElif == 10 && progress.GetShopUpgradeLevel(ShopUpgrades.DiscountId) == 1 &&
                       progress.GetShopUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Available,
                    "The Run must start from the saved balance and levels.");
                changes = 0;
                Assert(progress.TryPurchaseShopUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Purchased &&
                       progress.ElifSpent == 3 && progress.GetShopUpgradeLevel(ShopUpgrades.DiscountId) == 2 &&
                       changes == 1 && progress.ShopUpgradePurchases.Count == 1,
                    "Buying the next discount level must spend its cost once and apply at once.");
                ShopUpgradePurchase first = progress.ShopUpgradePurchases[0];
                Assert(first.UpgradeId == ShopUpgrades.DiscountId && first.Level == 2 && first.Floor == 2 &&
                       first.Order == 1, "A purchase must record its level, floor and order.");
                Assert(progress.TryPurchaseShopUpgrade(ShopUpgrades.ExtraSlotId) == ShopUpgradeStatus.Purchased &&
                       progress.TryPurchaseShopUpgrade(ShopUpgrades.ExtraSlotId) == ShopUpgradeStatus.Purchased &&
                       progress.AvailableElif == 2 && progress.ShopUpgradePurchases[2].Order == 3 &&
                       progress.ShopUpgradePurchases[2].Level == 2,
                    "Upgrades must be bought level by level in order.");
                Assert(progress.TryPurchaseShopUpgrade(ShopUpgrades.ExtraSlotId) == ShopUpgradeStatus.NotEnoughElif &&
                       progress.ElifSpent == 8 && progress.ShopUpgradePurchases.Count == 3,
                    "Without enough elif an upgrade must not be bought or spent.");
                progress.TryAddResource(RunResourceType.Elif, 10);
                Assert(progress.TryPurchaseShopUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Purchased &&
                       progress.GetShopUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.MaxLevel &&
                       progress.TryPurchaseShopUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.MaxLevel,
                    "An upgrade at its last level must not sell again.");

                RunElifDto request = RunSession.BuildRunElif(progress);
                Assert(request.spent == progress.ElifSpent && request.gained == 10 &&
                       request.upgradePurchases.Select(purchase =>
                               $"{purchase.upgradeId}:{purchase.level}:{purchase.floor}:{purchase.order}")
                           .SequenceEqual(new[]
                           {
                               "shop-upgrade-discount:2:2:1", "shop-upgrade-extra-slot:1:2:2",
                               "shop-upgrade-extra-slot:2:2:3", "shop-upgrade-discount:3:2:4",
                           }),
                    "The save request must list the bought levels in buying order with the elif spent.");

                progress.SetDevelopmentShopUpgradeLevel(ShopUpgrades.ExtraSlotId, 3);
                Assert(progress.GetShopUpgradeLevel(ShopUpgrades.ExtraSlotId) == 3 &&
                       progress.GetSavedShopUpgradeLevel(ShopUpgrades.ExtraSlotId) == 2 &&
                       progress.TryPurchaseShopUpgrade(ShopUpgrades.ExtraSlotId) ==
                       ShopUpgradeStatus.DevelopmentOverride &&
                       RunSession.BuildRunElif(progress).upgradePurchases.Length == 4,
                    "A development level must apply for this Run only, block buying and never be saved.");
                progress.SetDevelopmentShopUpgradeLevel(ShopUpgrades.ExtraSlotId, -1);
                Assert(progress.GetShopUpgradeLevel(ShopUpgrades.ExtraSlotId) == 2,
                    "Clearing the development level must restore the saved level.");

                int spent = progress.ElifSpent;
                progress.RefundElif(2);
                Assert(progress.ElifSpent == spent - 2 && progress.ElifGained == 10,
                    "A refund must give back spent elif without counting a gain.");

                progress.ResetProgress();
                progress.ConfigureElifStart(10, null, null, false);
                Assert(progress.GetShopUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.ElifLocked &&
                       !progress.TrySpendResource(RunResourceType.Elif, 1) &&
                       progress.TryPurchaseShopUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.ElifLocked &&
                       progress.ElifSpent == 0 && progress.AvailableElif == 10,
                    "A Run without a fresh server balance must not spend elif (contract 27 §4.3).");
                progress.SetDevelopmentElifSpendingAllowed(true);
                Assert(progress.TrySpendResource(RunResourceType.Elif, 1), "The development toggle must unlock spending.");

                progress.ResetProgress();
                progress.ConfigureElifStart(10, new Dictionary<string, int> { [ShopUpgrades.ExtraSlotId] = 1 },
                    new[] { ShopUpgrades.ExtraSlotId }, true);
                Assert(progress.GetShopUpgradeStatus(ShopUpgrades.ExtraSlotId) == ShopUpgradeStatus.PendingSave &&
                       progress.GetShopUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Available &&
                       progress.GetShopUpgradeLevel(ShopUpgrades.ExtraSlotId) == 1,
                    "An upgrade a pending Run bought must apply but not sell until that Run is saved.");

                progress.ResetProgress();
                Assert(progress.IsElifSpendingAllowed && progress.ShopUpgradePurchases.Count == 0 &&
                       ShopUpgrades.Catalog.All(upgrade => progress.GetShopUpgradeLevel(upgrade.UpgradeId) == 0 &&
                                                           !progress.HasDevelopmentUpgradeLevel(upgrade.UpgradeId)),
                    "Resetting the Run must clear its upgrade record.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateElifStart()
        {
            ElifMetaDto meta = new()
            {
                elifBalance = 10,
                shopUpgrades = new[] { new ShopUpgradeLevelDto { upgradeId = ShopUpgrades.DiscountId, level = 1 } },
            };
            CreateRunRequest[] pending =
            {
                Request("a", 3, Purchase(ShopUpgrades.ExtraSlotId, 1, 1)),
                Request("b", 2, Purchase(ShopUpgrades.DiscountId, 2, 1), Purchase(ShopUpgrades.ExtraSlotId, 2, 2)),
                new() { clientRunId = "c", userId = "u", characterId = "erpin", elif = null },
            };
            pending[0].elif.gained = 20;
            RunSession.ElifRunStart start = RunSession.ComputeElifStart(meta, pending);
            Assert(start.Balance == 5 &&
                   start.UpgradeLevels[ShopUpgrades.DiscountId] == 2 && start.UpgradeLevels[ShopUpgrades.ExtraSlotId] == 2,
                "The start must subtract pending spending, ignore pending gains and raise levels pending Runs bought.");
            RunSession.ElifRunStart offline = RunSession.ComputeElifStart(null, null);
            Assert(offline.Balance == 0 && offline.UpgradeLevels.Count == 0,
                "A profile that never reached the server starts at 0 elif and no upgrades.");
            RunSession.ElifRunStart cached = RunSession.ComputeElifStart(meta, new[] { Request("d", 50) });
            Assert(cached.Balance == 0 && cached.UpgradeLevels[ShopUpgrades.DiscountId] == 1,
                "The start never goes below 0.");

            string user = "elif3-verify-" + Guid.NewGuid().ToString("N");
            CreateRunRequest first = Request(Guid.NewGuid().ToString(), 1);
            CreateRunRequest second = Request(Guid.NewGuid().ToString(), 2);
            CreateRunRequest other = Request(Guid.NewGuid().ToString(), 4);
            first.userId = second.userId = user;
            other.userId = user + "-other";
            try
            {
                Assert(LocalPendingRunStorage.Save(first) && LocalPendingRunStorage.Save(second) &&
                       LocalPendingRunStorage.Save(other), "The pending requests must be queued.");
                Assert(LocalPendingRunStorage.LoadAll(user).Select(request => request.clientRunId)
                        .SequenceEqual(new[] { first.clientRunId, second.clientRunId }) &&
                       LocalPendingRunStorage.LoadAll(user)[1].elif.spent == 2,
                    "Pending requests must be read oldest first for one profile, with their elif.");
            }
            finally
            {
                LocalPendingRunStorage.Remove(first.clientRunId);
                LocalPendingRunStorage.Remove(second.clientRunId);
                LocalPendingRunStorage.Remove(other.clientRunId);
            }

            string nickname = "elif3-verify-" + Guid.NewGuid().ToString("N");
            try
            {
                Assert(PlayerProgressClient.LoadCachedElifMeta(nickname) == null, "A new profile has no cached elif.");
                PlayerProgressClient.StoreConfirmedElifMeta(nickname, meta);
                ElifMetaDto loaded = PlayerProgressClient.LoadCachedElifMeta(nickname);
                Assert(loaded != null && loaded.elifBalance == 10 && loaded.shopUpgrades.Length == 1 &&
                       loaded.shopUpgrades[0].upgradeId == ShopUpgrades.DiscountId && loaded.shopUpgrades[0].level == 1,
                    "A confirmed balance and levels must be cached for an offline start.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(PlayerProgressClient.ElifCachePrefix + nickname);
                PlayerPrefs.Save();
            }
        }

        private static void ValidateSaveRequest()
        {
            CreateRunRequest request = Request("00000000-0000-4000-8000-0000000000e3", 4,
                Purchase(ShopUpgrades.DiscountId, 1, 1));
            request.isCleared = true;
            request.items = Array.Empty<RunItemDto>();
            MethodInfo serialize = typeof(ApiClient).GetMethod("SerializeRunRequest",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert(serialize != null, "ApiClient.SerializeRunRequest was not found.");
            string json = (string)serialize.Invoke(null, new object[] { request });
            Assert(json.Contains("\"elif\":{\"gained\":0,\"spent\":4,\"upgradePurchases\":[{\"upgradeId\":" +
                                 "\"shop-upgrade-discount\",\"level\":1,\"floor\":1,\"order\":1}]}"),
                "The save request must carry elif.upgradePurchases (contract 27 §5.2). Got: " + json);
        }

        // Offline Runs pile up in the pending queue. A Run that bought upgrades must reach the server after them.
        private static void ValidateOrderedSubmission()
        {
            string user = "elif3-order-" + Guid.NewGuid().ToString("N");
            CreateRunRequest older = Request(Guid.NewGuid().ToString(), 2, Purchase(ShopUpgrades.DiscountId, 1, 1));
            CreateRunRequest plain = Request(Guid.NewGuid().ToString(), 0);
            CreateRunRequest current = Request(Guid.NewGuid().ToString(), 3, Purchase(ShopUpgrades.DiscountId, 2, 1));
            older.userId = plain.userId = current.userId = user;
            try
            {
                Assert(!RunSubmission.NeedsOrderedDelivery(current), "With nothing pending a Run is sent at once.");
                LocalPendingRunStorage.Save(older);
                Assert(!RunSubmission.NeedsOrderedDelivery(plain) && RunSubmission.NeedsOrderedDelivery(current),
                    "Only a Run that bought upgrades waits for the pending Runs.");

                FakeApiClient failing = new() { FailClientRunId = older.clientRunId };
                string error = null;
                RunSubmission.Post(failing, current, null, _ => Assert(false, "A failed queue must not succeed."),
                    message => error = message);
                Assert(error != null && failing.Posted.SequenceEqual(new[] { older.clientRunId }) &&
                       LocalPendingRunStorage.LoadAll(user).Select(request => request.clientRunId)
                           .SequenceEqual(new[] { older.clientRunId, current.clientRunId }),
                    "When an earlier Run fails, the upgrade Run must stay queued behind it and not be sent.");

                FakeApiClient online = new();
                CreateRunResponse saved = null;
                RunSubmission.Post(online, current, null, response => saved = response,
                    message => Assert(false, "The ordered send failed: " + message));
                Assert(saved != null && online.Posted.SequenceEqual(new[] { older.clientRunId, current.clientRunId }) &&
                       LocalPendingRunStorage.LoadAll(user).Select(request => request.clientRunId)
                           .SequenceEqual(new[] { current.clientRunId }),
                    "The pending Run must be saved and removed before the upgrade Run is sent; the caller removes " +
                    "its own request on success.");

                LocalPendingRunStorage.Save(older);
                FakeApiClient direct = new();
                RunSubmission.Post(direct, plain, null, _ => { }, _ => { });
                Assert(direct.Posted.SequenceEqual(new[] { plain.clientRunId }),
                    "A Run without upgrade purchases must be sent at once even with Runs pending.");
            }
            finally
            {
                LocalPendingRunStorage.Remove(older.clientRunId);
                LocalPendingRunStorage.Remove(plain.clientRunId);
                LocalPendingRunStorage.Remove(current.clientRunId);
            }
        }

        // The Run must start before the profile lookup answers (offline first), from the cached elif; the server's
        // balance replaces it when it arrives, and a failed lookup keeps the cache.
        private static void ValidateImmediateStart()
        {
            foreach (bool answers in new[] { true, false })
            {
                string nickname = "elif3-start-" + Guid.NewGuid().ToString("N");
                GameObject root = new("Elif-3 Start");
                try
                {
                    RunProgress progress = root.AddComponent<RunProgress>();
                    PlayerProgressClient client = root.AddComponent<PlayerProgressClient>();
                    DeferredApiClient api = new();
                    client.SetApiClient(api);
                    RunSession session = root.AddComponent<RunSession>();
                    session.Configure(null, progress, null);
                    session.ConfigureMetaProgression(client);
                    session.SetApiClient(api);
                    Assert(session.ConfigureLaunchIdentity(Guid.NewGuid().ToString(), nickname, "erpin"),
                        "The verification Run needs a launch identity.");
                    PlayerProgressClient.StoreConfirmedElifMeta(nickname, new ElifMetaDto { elifBalance = 4 });

                    bool? started = null;
                    Assert(session.PrepareAndBeginRun("erpin", result => started = result) && started == true &&
                           session.HasStarted && api.PendingSuccess != null && progress.ElifStartingBalance == 4,
                        "The Run must start at once from the cached elif while the profile lookup is still waiting.");
                    progress.TryAddResource(RunResourceType.Elif, 1);
                    if (answers)
                    {
                        api.PendingSuccess(new UserProfileResponse
                        {
                            success = true,
                            data = new UserProfileData
                            {
                                id = "00000000-0000-4000-8000-0000000000e4",
                                nickname = nickname,
                                characterProgress = new[]
                                {
                                    new CharacterProgressDto
                                    {
                                        characterId = "erpin", level = 1, lowGradeSkillLevel = 1,
                                        highGradeSkillLevel = 1,
                                    },
                                },
                                meta = new ElifMetaDto { elifBalance = 6 },
                            },
                        });
                        Assert(progress.ElifStartingBalance == 6 && progress.AvailableElif == 7 &&
                               session.UserId == "00000000-0000-4000-8000-0000000000e4",
                            "The server's balance must replace the cached one and keep the elif gained meanwhile.");
                    }
                    else
                    {
                        api.PendingError("offline");
                        Assert(progress.ElifStartingBalance == 4 && progress.AvailableElif == 5 && session.HasStarted,
                            "A failed lookup must leave the Run running on the cached elif.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    PlayerPrefs.DeleteKey(PlayerProgressClient.ElifCachePrefix + nickname);
                    PlayerPrefs.DeleteKey($"TrickalFanGame.CharacterProgress.{nickname}.erpin");
                    PlayerPrefs.Save();
                }
            }
        }

        private sealed class DeferredApiClient : IGameApiClient
        {
            public Action<UserProfileResponse> PendingSuccess;
            public Action<string> PendingError;

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                PendingSuccess = onSuccess;
                PendingError = onError;
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess,
                Action<string> onError) => onError("offline");

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess,
                Action<string> onError) => onError("offline");
        }

        private sealed class FakeApiClient : IGameApiClient
        {
            public readonly List<string> Posted = new();
            public string FailClientRunId;

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError) =>
                onError("offline");

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                Posted.Add(request.clientRunId);
                if (request.clientRunId == FailClientRunId) onError("offline");
                else onSuccess(new CreateRunResponse { success = true, data = new RunData { runId = request.clientRunId } });
            }

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess,
                Action<string> onError) => onError("offline");
        }

        private static void ValidateShopPurchases(ShopCatalog catalog, ItemDefinition[] pool, ShopRoom shopPrefab)
        {
            int seed = Enumerable.Range(1, 400).First(candidate =>
            {
                ShopStockState rolled = ShopStockBuilder.Build(RoomId, candidate, catalog, pool, null,
                    ShopKind.General, null, true);
                return rolled.Offers.Any(offer => offer.IsElifPriced) && rolled.Offers.Count(offer =>
                    offer.Kind == ShopOfferKind.Item && !offer.Item.IsActivePower) >= 2;
            });
            GameObject root = new("Elif-3 Shop");
            try
            {
                GameObject player = CreatePlayer(root.transform, Vector2.zero, typeof(RunProgress));
                RunProgress progress = player.GetComponent<RunProgress>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                ShopStockState stock = progress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(RoomId),
                    () => ShopStockBuilder.Build(RoomId, seed, catalog, pool, null, ShopKind.General, null, true));
                ShopRoom shop = Object.Instantiate(shopPrefab, root.transform);
                shop.Configure(progress, stock, root.transform);
                progress.ConfigureElifStart(0, null, null, true);

                ShopOffer elif = stock.Offers.Single(offer => offer.IsElifPriced);
                Assert(shop.GetPrice(elif) == elif.ElifPrice && ShopRoom.CurrencyOf(elif) == RunResourceType.Elif &&
                       shop.GetStatus(elif.SlotIndex, inventory) == ShopOfferStatus.NotEnoughElif,
                    "An elif offer must cost its elif price and need elif.");
                progress.TryAddResource(RunResourceType.Gold, RunResourceWallet.MaxCount);
                Assert(shop.TryPurchase(elif.SlotIndex, inventory) == ShopPurchaseResult.NotEnoughElif &&
                       progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount &&
                       !stock.IsPurchased(elif.SlotIndex),
                    "Gold must never pay for an elif offer.");
                progress.TryAddResource(RunResourceType.Elif, elif.ElifPrice);
                int stacks = inventory.GetStackCount(elif.Item.ItemId);
                Assert(shop.GetStatus(elif.SlotIndex, inventory) == ShopOfferStatus.Available &&
                       shop.TryPurchase(elif.SlotIndex, inventory) == ShopPurchaseResult.Purchased &&
                       progress.ElifSpent == elif.ElifPrice && progress.AvailableElif == 0 &&
                       progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount &&
                       inventory.GetStackCount(elif.Item.ItemId) == stacks + 1,
                    "Buying an elif offer must spend elif once and grant the Item.");

                ShopOffer locked = stock.Offers.First(offer => offer.Kind == ShopOfferKind.Item &&
                                                               !offer.Item.IsActivePower && !offer.IsElifPriced);
                Assert(stock.TryConvertToElifPrice(locked.SlotIndex) && !stock.TryConvertToElifPrice(locked.SlotIndex),
                    "The development conversion must make one gold artifact an elif offer once.");
                locked = stock.GetOffer(locked.SlotIndex);
                progress.TryAddResource(RunResourceType.Elif, 20);
                progress.SetDevelopmentElifSpendingAllowed(false);
                Assert(shop.GetStatus(locked.SlotIndex, inventory) == ShopOfferStatus.ElifLocked &&
                       shop.TryPurchase(locked.SlotIndex, inventory) == ShopPurchaseResult.ElifLocked &&
                       shop.GetUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.ElifLocked &&
                       progress.ElifSpent == elif.ElifPrice,
                    "A Run that cannot spend elif must refuse elif offers and upgrades.");
                progress.SetDevelopmentElifSpendingAllowed(true);

                ShopOffer consumable = stock.Offers.First(offer => offer.Kind == ShopOfferKind.Consumable);
                Assert(shop.SellsUpgrades && shop.GetPrice(consumable) == consumable.Price,
                    "Without the discount a gold offer costs its listed price.");
                Assert(shop.TryPurchaseUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Purchased &&
                       shop.DiscountLevel == 1 &&
                       shop.GetPrice(consumable) == ShopUpgrades.ApplyDiscount(consumable.Price, 1) &&
                       shop.GetPrice(locked) == locked.ElifPrice,
                    "상점 할인 must lower gold prices at once and leave elif prices alone.");
                int gold = progress.GetResourceCount(RunResourceType.Gold);
                Assert(shop.TryPurchase(consumable.SlotIndex, inventory) == ShopPurchaseResult.Purchased &&
                       progress.GetResourceCount(RunResourceType.Gold) ==
                       gold - ShopUpgrades.ApplyDiscount(consumable.Price, 1),
                    "A gold purchase must pay the discounted price.");

                int extraSlot = ShopStockState.FirstExtraSlot;
                Assert(shop.GetStatus(extraSlot, inventory) == ShopOfferStatus.Empty &&
                       shop.TryPurchaseUpgrade(ShopUpgrades.ExtraSlotId) == ShopUpgradeStatus.Purchased &&
                       stock.GetOffer(extraSlot) != null && stock.ExtraSlotCount == 1 &&
                       shop.GetStatus(extraSlot, inventory) != ShopOfferStatus.Empty,
                    "상품 칸 추가 must open a last-row offer in the open shop at once.");
                ShopOffer extra = stock.GetOffer(extraSlot);
                Assert(shop.GetPrice(extra) == ShopUpgrades.ApplyDiscount(extra.Price, 1),
                    "An extra offer is a gold offer and takes the discount.");

                // A shop built later in the Run opens its extra slots when it is configured.
                const string laterRoom = "floor-02-room-04";
                ShopStockState later = progress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(laterRoom),
                    () => ShopStockBuilder.Build(laterRoom, seed + 1, catalog, pool, null));
                Object.Instantiate(shopPrefab, root.transform).Configure(progress, later, root.transform);
                Assert(later.ExtraSlotCount == 1, "A shop built later must open the extra slots already bought.");

                const string goldiRoom = "floor-02-room-07";
                ShopStockState goldi = progress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(goldiRoom),
                    () => ShopStockBuilder.Build(goldiRoom, seed + 2, catalog, pool, null, ShopKind.Goldi));
                ShopRoom goldiShop = Object.Instantiate(shopPrefab, root.transform);
                goldiShop.Configure(progress, goldi, root.transform);
                ShopOffer goldiConsumable = goldi.Offers.First(offer => offer.Kind == ShopOfferKind.Consumable);
                Assert(!goldiShop.SellsUpgrades &&
                       goldiShop.GetUpgradeStatus(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Unavailable &&
                       goldiShop.TryPurchaseUpgrade(ShopUpgrades.DiscountId) == ShopUpgradeStatus.Unavailable &&
                       goldiShop.GetPrice(goldiConsumable) == ShopUpgrades.ApplyDiscount(goldiConsumable.Price, 1),
                    "골디's shop applies the upgrades but does not sell them.");

                Assert(stock.TryMakeFree() && shop.GetPrice(locked) == 0 &&
                       shop.GetStatus(locked.SlotIndex, inventory) == ShopOfferStatus.Free,
                    "멤버십카드 must make an elif offer free too.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateScreen()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            try
            {
                ShopView view = FindAll<ShopView>(scene).Single();
                ShopSession session = view.Session;
                Assert(session != null && view.ElifText != null && view.InvestButton != null &&
                       view.InvestButtonLabel != null && view.InvestPanel != null && view.InvestNoticeText != null &&
                       view.InvestRows.Length == ShopUpgrades.Catalog.Length,
                    "Run Elif-3 setup first: the shop screen needs the elif line, the 투자 button and the invest list.");
                Assert(view.InvestPanel.alpha == 0f && !view.InvestPanel.blocksRaycasts &&
                       view.InvestNoticeText.text == ShopView.InvestNotice &&
                       view.InvestPanel.transform.GetSiblingIndex() >
                       view.Cells.Max(cell => cell.transform.GetSiblingIndex()),
                    "The invest list must be saved hidden, above the grid cells, with the save notice.");
                for (int index = 0; index < view.InvestRows.Length; index++)
                {
                    ShopUpgradeRowView row = view.InvestRows[index];
                    Assert(row != null && row.RowIndex == index && row.Button != null && row.FocusFrame != null &&
                           row.NameText != null && row.LevelText != null && row.EffectText != null &&
                           row.CostText != null &&
                           row.Button.navigation.mode == UnityEngine.UI.Navigation.Mode.None,
                        $"Invest row {index + 1} must have its texts, focus frame and a button.");
                }

                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
                Assert(font != null && font.HasCharacters(Week27Elif3Setup.RequiredGlyphs()),
                    "The UI font must hold every Elif-3 glyph.");

                RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
                RunProgress progress = assembler.Progress;
                progress.ResetProgress();
                int seed = Enumerable.Range(1, 512).First(candidate =>
                    assembler.Generator.TryGenerateForSeed(candidate, out GeneratedFloorGraph graph, out _) &&
                    graph.FindFloor(1).Nodes.Any(node => node.Role == GeneratedRoomRole.Shop));
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                GeneratedRoomNode shopNode = assembler.GeneratedGraph.FindFloor(1).Nodes
                    .Single(node => node.Role == GeneratedRoomRole.Shop);
                ShopRoom shop = assembler.CurrentFloorRoot.GetComponentsInChildren<ShopRoom>(true)
                    .Single(candidate => candidate.Stock != null &&
                                         candidate.Stock.ShopId == ShopStockBuilder.BuildShopId(shopNode.RoomId));
                GameObject player = assembler.Graph.Player.gameObject;
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Invoke(player.GetComponent<Health>(), "Awake");
                Invoke(player.GetComponent<PlayerStats>(), "Awake");
                Invoke(inventory, "Awake");
                Invoke(view, "OnEnable");
                foreach (ShopOfferCellView cell in view.Cells) Invoke(cell, "OnEnable");
                foreach (ShopUpgradeRowView row in view.InvestRows) Invoke(row, "OnEnable");

                progress.ConfigureElifStart(10, null, null, true);
                progress.TryAddResource(RunResourceType.Gold, RunResourceWallet.MaxCount);
                Assert(session.TryOpen(shop, inventory) && view.IsVisible && !view.IsInvesting &&
                       view.InvestButton.gameObject.activeSelf && view.InvestButtonLabel.text == ShopView.InvestLabel &&
                       view.ElifText.text == ShopView.FormatElifBalance(10, true),
                    "The general shop screen must show the elif balance and the 투자 button.");
                Assert(view.Cells[ShopStockState.FirstExtraSlot].Status == ShopOfferStatus.Empty,
                    "The last row must start empty.");

                Assert(view.ToggleInvesting() && view.IsInvesting && view.InvestPanel.alpha == 1f &&
                       view.InvestPanel.blocksRaycasts && view.InvestButtonLabel.text == ShopView.InvestBackLabel &&
                       view.InvestFocusedIndex == 0 && view.InvestRows[0].IsFocused &&
                       view.InvestRows[0].NameText.text == "상품 칸 추가" &&
                       view.InvestRows[0].LevelText.text == ShopView.FormatUpgradeLevel(0, 3) &&
                       view.InvestRows[0].CostText.text == "2 엘리프" &&
                       view.DetailNameText.text == "상품 칸 추가" && view.BuyButton.interactable &&
                       view.BuyButtonLabel.text == ShopView.InvestBuyLabel &&
                       view.DetailEffectText.text.Contains(ShopView.InvestNotice),
                    "투자 must open the invest list with the next level, its cost and the save notice.");
                Assert(view.TryPurchaseFocused() == ShopPurchaseResult.Unavailable,
                    "While investing the grid must not sell.");
                Assert(view.TryInvestFocused() == ShopUpgradeStatus.Purchased &&
                       view.FeedbackText.text == ShopView.InvestPurchasedMessage &&
                       view.ElifText.text == ShopView.FormatElifBalance(8, true) &&
                       view.InvestRows[0].LevelText.text == ShopView.FormatUpgradeLevel(1, 3) &&
                       shop.Stock.GetOffer(ShopStockState.FirstExtraSlot) != null,
                    "Investing must spend elif, raise the level and open the extra slot at once.");
                view.MoveFocus(0, 1);
                Assert(view.InvestFocusedIndex == 1 && view.DetailNameText.text == "상점 할인" &&
                       view.TryInvestFocused() == ShopUpgradeStatus.Purchased && shop.DiscountLevel == 1,
                    "Down must focus 상점 할인 and buy it.");

                Assert(view.SetInvesting(false) && !view.IsInvesting && view.InvestPanel.alpha == 0f,
                    "Leaving the invest list must show the grid again.");
                ShopOffer extra = shop.Stock.GetOffer(ShopStockState.FirstExtraSlot);
                ShopOfferCellView extraCell = view.Cells[ShopStockState.FirstExtraSlot];
                ShopOffer firstGold = shop.Stock.Offers.First(offer => !offer.IsElifPriced);
                Assert(extraCell.Offer == extra && extraCell.Content.alpha == 1f &&
                       extraCell.PriceText.text == $"{ShopUpgrades.ApplyDiscount(extra.Price, 1)} 골드" &&
                       view.Cells[firstGold.SlotIndex].PriceText.text ==
                       $"{ShopUpgrades.ApplyDiscount(firstGold.Price, 1)} 골드",
                    "The grid must show the new extra offer and the discounted gold prices.");
                session.Close();
                Assert(!view.IsVisible && !view.IsInvesting, "Closing the shop must close the invest list.");
            }
            finally
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
        }

        private static CreateRunRequest Request(string clientRunId, int spent,
            params RunElifUpgradePurchaseDto[] purchases) => new()
        {
            clientRunId = clientRunId,
            userId = "00000000-0000-4000-8000-000000000001",
            characterId = "erpin",
            elif = new RunElifDto { spent = spent, upgradePurchases = purchases },
        };

        private static RunElifUpgradePurchaseDto Purchase(string upgradeId, int level, int order) => new()
        {
            upgradeId = upgradeId,
            level = level,
            floor = 1,
            order = order,
        };

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Invoke(object target, string methodName)
        {
            if (target == null) return;
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
