using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Power-2: verifies that floor 1's treasure room is always an unlocked end room next to the start room and gives
    // one of its 3택1 cards to a 교주의 권능, that secret rooms (30%) and shops (25%, one Item slot at the Rare price)
    // offer one from the room's seed, that the same seed rebuilds the same offers and revisits keep them, and that a
    // taken or bought 권능 goes to the slot and drops the held one where the reward was given.
    public static class Week27Power2Verification
    {
        private const int SeedCount = 512;
        private const int RunSeed = 20261015;

        public static void SetupAndVerifyBatch()
        {
            Week27Power2Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week27Power2Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Power-2 setup changed the Game Scene GUID.");
            Verify();
        }

        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            // The 권능 slot, the floor and treasure-lock rules that floor 1 changed, the shops and the 3택1
            // (Shop-1 runs Special-3, Shop-0, Special-4, Reward-3, Slot-0 and the development panel), and the room
            // Layouts the new Room content version reselects.
            Regression(Week27Power1Verification.Verify);
            Regression(Week20Floor1Verification.Verify);
            Regression(Week20Special1Verification.Verify);
            Regression(Week23Shop1Verification.VerifyWithRegressionsBatch);
            Regression(Week14Room0RegressionVerification.Verify);
            Regression(Week25Trap1Verification.Verify);
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Debug.Log("Power-2 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Power-2 Power Offers")]
        public static void Verify()
        {
            ValidateSceneConfiguration();
            ValidateGeneration();
            ValidateCandidates();
            ValidateShopStock();
            ValidateTreasureRuntime();
            ValidateSecretRuntime();
            ValidateShopRuntime();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Debug.Log("Power-2 verification passed: floor 1's treasure room is an unlocked end room next to the " +
                      "start with one 권능 card in its 3택1, secret rooms offer one at 30% and shops sell one at 25% " +
                      "for the Rare price, the same seed rebuilds the same offers, revisits keep them, and a taken " +
                      "or bought 권능 fills the slot and drops the held one with its gauge.");
        }

        private static void Regression(Action verification)
        {
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            verification();
        }

        private static void ValidateSceneConfiguration()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            string[] expected = Week27Power1Setup.Specs.Select(spec => spec.Id).OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Assert(assembler.PowerRewardPool.Count == expected.Length &&
                   assembler.PowerRewardPool.All(power => power != null && power.IsValid && power.IsActivePower) &&
                   assembler.PowerRewardPool.Select(power => power.ItemId).OrderBy(id => id, StringComparer.Ordinal)
                       .SequenceEqual(expected) &&
                   assembler.PowerRewardPool.All(power =>
                       AssetDatabase.GetAssetPath(power) == $"Assets/Items/{power.ItemId}.asset"),
                "Run Power-2 setup: the room assembler must offer the six 권능 assets.");
            Assert(assembler.SelectionRewardPool.All(item => item == null || !item.IsActivePower),
                "권능 must stay out of the shared artifact pool and come only from the 권능 offer pool.");
            Assert(assembler.Generator != null &&
                   assembler.Generator.RoomContentVersion >= Week27Power2Setup.RoomContentVersion &&
                   assembler.Generator.FloorSettings.Count == assembler.Generator.FloorCount,
                $"Power-2 raises the Room content version to {Week27Power2Setup.RoomContentVersion} on the " +
                "configured floor-settings generator.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(GamePowerHudView.OfferFixedText),
                "The Frontend font must contain the 권능 offer line glyphs.");
            Assert(GamePowerHudView.BuildOfferLine(Load(Week27Power1Setup.PoppinStarId)) == "충전 2방 · [R] 사용 · 1개만 보유",
                "A 권능 card shows its gauge, key and single-slot rule instead of artifact stacks.");
        }

        private static void ValidateGeneration()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = FindAll<RoomGraphAssembler>(scene).Single().Generator;
            int lockedLater = 0, farLater = 0, secrets = 0, secretPowers = 0, shops = 0, shopPowers = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                foreach (GeneratedFloor floor in graph.Floors)
                {
                    GeneratedFloor again = repeated.FindFloor(floor.FloorNumber);
                    Assert(Signature(floor) == Signature(again),
                        $"Seed {seed} floor {floor.FloorNumber} did not rebuild the same rooms and offers.");
                    GeneratedRoomNode start = floor.Nodes.Single(node => node.RoomId == floor.StartingRoomId);
                    GeneratedRoomNode treasure = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Treasure);
                    GeneratedRoomConnection[] regular = treasure.DirectionalConnections
                        .Where(connection => !connection.IsSecret).ToArray();
                    bool nextToStart = regular.Any(connection => connection.DestinationRoomId == start.RoomId);
                    if (floor.FloorNumber == FloorGenerator.StartTreasureFloor)
                    {
                        Assert(!treasure.RequiresKey && regular.Length == 1 && nextToStart,
                            $"Seed {seed}: floor 1's treasure room must be an unlocked end room next to the start.");
                        Assert(ActivePowerOffers.IsOfferedInRewardRoom(treasure),
                            $"Seed {seed}: floor 1's treasure room must guarantee a 권능 card.");
                    }
                    else
                    {
                        lockedLater += treasure.RequiresKey ? 1 : 0;
                        farLater += nextToStart ? 0 : 1;
                        Assert(!ActivePowerOffers.IsOfferedInRewardRoom(treasure),
                            $"Seed {seed}: floor {floor.FloorNumber}'s treasure room must not offer a 권능 yet.");
                    }

                    foreach (GeneratedRoomNode node in floor.Nodes)
                    {
                        if (node.Role == GeneratedRoomRole.Secret && !node.IsGoldiShop)
                        {
                            secrets++;
                            secretPowers += ActivePowerOffers.IsOfferedInRewardRoom(node) ? 1 : 0;
                        }
                        else if (node.Role == GeneratedRoomRole.Secret)
                        {
                            Assert(!ActivePowerOffers.IsOfferedInRewardRoom(node),
                                $"Seed {seed}: a Goldi shop secret room has no 3택1 to hold a 권능.");
                        }

                        if (node.Role == GeneratedRoomRole.Shop || node.IsGoldiShop)
                        {
                            shops++;
                            shopPowers += ActivePowerOffers.RollShop(node.ContentSeed) ? 1 : 0;
                        }
                        if (node.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Intermediate or
                            GeneratedRoomRole.Boss or GeneratedRoomRole.Shop)
                            Assert(!ActivePowerOffers.IsOfferedInRewardRoom(node),
                                $"Seed {seed}: only treasure and secret rooms hold a 3택1 권능.");
                    }
                }
            }

            Assert(lockedLater > 0 && farLater > 0,
                "Floors after the first must keep their own treasure placement and key locks.");
            float secretRate = secretPowers / (float)secrets, shopRate = shopPowers / (float)shops;
            Assert(secrets > 200 && secretRate >= 0.22f && secretRate <= 0.38f,
                $"Secret rooms should offer a 권능 about 30% of the time; observed {secretRate:P1} of {secrets}.");
            Assert(shops > 200 && shopRate >= 0.17f && shopRate <= 0.33f,
                $"Shops should sell a 권능 about 25% of the time; observed {shopRate:P1} of {shops}.");
        }

        private static void ValidateCandidates()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            IReadOnlyList<ItemDefinition> artifacts = assembler.SelectionRewardPool;
            IReadOnlyList<ItemDefinition> powers = assembler.PowerRewardPool;
            Rig rig = Rig.Create();
            try
            {
                HashSet<string> seen = new(StringComparer.Ordinal);
                for (int index = 0; index < 128; index++)
                {
                    string rewardId = $"floor-01-room-{index:00}:treasure";
                    IReadOnlyList<ItemRewardCandidate> offered = ArtifactRewardSelector.BuildCandidates(
                        artifacts, rig.Inventory, RunSeed, rewardId, powers);
                    Assert(offered.Count == 3 && offered[2].IsItem && offered[2].Definition.IsActivePower &&
                           offered.Take(2).All(candidate => candidate.Definition == null ||
                                                            !candidate.Definition.IsActivePower),
                        $"{rewardId}: a 권능 3택1 must hold two artifact cards and one 권능 card last.");
                    Assert(Ids(offered) == Ids(ArtifactRewardSelector.BuildCandidates(
                               artifacts, rig.Inventory, RunSeed, rewardId, powers)),
                        $"{rewardId}: the same seed and reward must roll the same 권능 3택1.");
                    IReadOnlyList<ItemRewardCandidate> plain = ArtifactRewardSelector.BuildCandidates(
                        artifacts, rig.Inventory, RunSeed, rewardId);
                    Assert(plain.Count == 3 && plain.All(candidate => candidate.Definition == null ||
                                                                      !candidate.Definition.IsActivePower) &&
                           Ids(plain) == Ids(ArtifactRewardSelector.BuildCandidates(
                               artifacts, rig.Inventory, RunSeed, rewardId, Array.Empty<ItemDefinition>())),
                        $"{rewardId}: a 3택1 without a 권능 offer keeps three artifact cards.");
                    seen.Add(offered[2].Definition.ItemId);
                }

                Assert(seen.Count == powers.Count, "Every 권능 must be able to appear in a 3택1.");

                ItemDefinition star = Load(Week27Power1Setup.PoppinStarId);
                Assert(rig.Slot.TryGrant(star, Vector3.zero, rig.Root.transform) && rig.Slot.HeldDefinition == star,
                    "The rig must hold 팝핀 스타.");
                for (int index = 0; index < 128; index++)
                {
                    IReadOnlyList<ItemRewardCandidate> offered = ArtifactRewardSelector.BuildCandidates(
                        artifacts, rig.Inventory, RunSeed, $"floor-01-room-{index:00}:secret", powers);
                    Assert(offered[2].Definition != star, "A 3택1 must not offer the 권능 the player holds.");
                }
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateShopStock()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            ShopCatalog catalog = assembler.ShopCatalog;
            Assert(catalog != null && catalog.TryValidate(out string catalogError), "The Game Scene needs a shop catalog.");
            IReadOnlyList<ItemDefinition> powers = assembler.PowerRewardPool;
            int rolled = Enumerable.Range(1, 200).First(ActivePowerOffers.RollShop);
            int plain = Enumerable.Range(1, 200).First(seed => !ActivePowerOffers.RollShop(seed));
            int price = catalog.GetItemPrice(ItemRarity.Rare);
            Rig rig = Rig.Create();
            GameObject shopObject = new("Power-2 Shop");
            try
            {
                foreach (ShopKind kind in new[] { ShopKind.General, ShopKind.Goldi })
                {
                    ShopStockState stock = ShopStockBuilder.Build("floor-01-room-09", rolled, catalog,
                        assembler.SelectionRewardPool, rig.Inventory, kind, powers);
                    ShopOffer[] powerOffers = stock.Offers.Where(IsPower).ToArray();
                    Assert(powerOffers.Length == 1 && powerOffers[0].Price == price &&
                           stock.Offers.Count(offer => offer.Kind == ShopOfferKind.Item) == ShopCatalog.ItemOfferCount &&
                           stock.Offers.Count == ShopCatalog.OfferCount,
                        $"A {kind} shop whose roll hits must sell exactly one 권능 in an Item slot at the Rare price.");
                    Assert(OfferIds(stock) == OfferIds(ShopStockBuilder.Build("floor-01-room-09", rolled, catalog,
                               assembler.SelectionRewardPool, rig.Inventory, kind, powers)),
                        $"A {kind} shop must rebuild the same 권능 stock from its seed.");
                    Assert(!ShopStockBuilder.Build("floor-01-room-09", plain, catalog, assembler.SelectionRewardPool,
                                rig.Inventory, kind, powers).Offers.Any(IsPower) &&
                           !ShopStockBuilder.Build("floor-01-room-09", rolled, catalog, assembler.SelectionRewardPool,
                                rig.Inventory, kind).Offers.Any(IsPower),
                        $"A {kind} shop sells no 권능 when its roll misses or no 권능 pool is given.");
                }

                // Buying: the 권능 fills the slot; a held one drops in front of the shopkeeper with its gauge.
                ShopStockState first = ShopStockBuilder.Build("floor-01-room-09", rolled, catalog,
                    assembler.SelectionRewardPool, rig.Inventory, ShopKind.General, powers);
                ShopRoom shop = shopObject.AddComponent<ShopRoom>();
                shop.Configure(rig.Progress, first, shopObject.transform);
                rig.Progress.TryAddResource(RunResourceType.Gold, price * 3);
                ShopOffer bought = first.Offers.Single(IsPower);
                Assert(shop.GetStatus(bought.SlotIndex, rig.Inventory) == ShopOfferStatus.Available &&
                       shop.TryPurchase(bought.SlotIndex, rig.Inventory) == ShopPurchaseResult.Purchased &&
                       rig.Slot.HeldDefinition == bought.Item && rig.Slot.Charge == 0 &&
                       rig.Progress.GetResourceCount(RunResourceType.Gold) == price * 2 &&
                       shop.GetStatus(bought.SlotIndex, rig.Inventory) == ShopOfferStatus.SoldOut,
                    "Buying a 권능 must spend its price and put it in the slot with an empty gauge.");
                Assert(rig.Inventory.AcquiredDefinitions.All(item => !item.IsActivePower),
                    "A bought 권능 must not join the artifact inventory.");

                ShopStockState held = ShopStockBuilder.Build("floor-01-room-10", rolled, catalog,
                    Array.Empty<ItemDefinition>(), null, ShopKind.General, new[] { bought.Item });
                shop.Configure(rig.Progress, held, shopObject.transform);
                ShopOffer same = held.Offers.Single(IsPower);
                Assert(shop.GetStatus(same.SlotIndex, rig.Inventory) == ShopOfferStatus.ItemUnavailable &&
                       shop.TryPurchase(same.SlotIndex, rig.Inventory) == ShopPurchaseResult.ItemUnavailable &&
                       rig.Progress.GetResourceCount(RunResourceType.Gold) == price * 2,
                    "The 권능 the player holds cannot be bought again.");

                ShopStockState second = ShopStockBuilder.Build("floor-01-room-11", rolled, catalog,
                    assembler.SelectionRewardPool, rig.Inventory, ShopKind.General, powers);
                shop.Configure(rig.Progress, second, shopObject.transform);
                ShopOffer swap = second.Offers.Single(IsPower);
                Assert(swap.Item != bought.Item, "A shop must not roll the 권능 the player holds.");
                rig.Slot.AddCharge(1);
                Assert(shop.TryPurchase(swap.SlotIndex, rig.Inventory) == ShopPurchaseResult.Purchased &&
                       rig.Slot.HeldDefinition == swap.Item && rig.Slot.Charge == 0,
                    "Buying another 권능 must swap it in with an empty gauge.");
                ActivePowerPickup[] dropped = shopObject.GetComponentsInChildren<ActivePowerPickup>(true);
                Assert(dropped.Length == 1 && dropped[0].Definition == bought.Item && dropped[0].StoredCharge == 1 &&
                       dropped[0].WaitsForPlayerExit &&
                       dropped[0].transform.position == shop.GetDropPosition(swap.SlotIndex),
                    "The swapped-out 권능 must drop at the offer's spot in the shop with its gauge.");
            }
            finally
            {
                Object.DestroyImmediate(shopObject);
                rig.Dispose();
            }
        }

        private static void ValidateTreasureRuntime()
        {
            Runtime runtime = Runtime.Open(RunSeed);
            GeneratedFloor floor = runtime.Assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode treasure = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Treasure);
            GeneratedRoomConnection door = floor.Nodes.Single(node => node.RoomId == floor.StartingRoomId)
                .DirectionalConnections.Single(connection => connection.DestinationRoomId == treasure.RoomId);
            RoomDoorSlot slot = runtime.Room(floor.StartingRoomId).FindSlot(door.Direction);
            Assert(slot != null && !slot.Doorway.RequiresKey && !slot.Blocker.IsLocked,
                "The start room's door to the floor-1 treasure room must open without a key.");

            RewardRoom reward = runtime.Room(treasure.RoomId).RewardRoom;
            Assert(reward != null && reward.gameObject.activeSelf && reward.OffersPower &&
                   reward.PowerOfferPool.Count == runtime.Assembler.PowerRewardPool.Count,
                "The floor-1 treasure room must hold a 3택1 that offers a 권능.");
            PlayerPowerSlot powerSlot = runtime.PowerSlot;
            ItemDefinition shield = Load(Week27Power1Setup.FreeloaderShieldId);
            Assert(powerSlot.TryGrant(shield, Vector3.zero, runtime.Assembler.CurrentFloorRoot.transform) &&
                   powerSlot.AddCharge(2) == 2,
                "The player must hold a charged 날먹보호막 before the treasure room.");

            reward.SetPlayerPresenceForVerification(true, runtime.Inventory);
            Assert(reward.TryInteract() && runtime.Session.IsOpen && runtime.Session.Candidates.Count == 3,
                "The floor-1 treasure room must open its 3택1.");
            ItemRewardCandidate offered = runtime.Session.Candidates[2];
            string signature = Ids(runtime.Session.Candidates);
            Assert(offered.Definition.IsActivePower && offered.Definition != shield &&
                   runtime.Session.Candidates.Count(candidate => candidate.Definition?.IsActivePower == true) == 1,
                "Exactly one card must be a 권능 other than the held one.");
            Assert(runtime.Session.TrySelect(2) && reward.HasRewarded && powerSlot.HeldDefinition == offered.Definition &&
                   powerSlot.Charge == 0,
                "Taking the 권능 card must put it in the slot with an empty gauge.");
            ActivePowerPickup[] dropped = reward.transform.parent.GetComponentsInChildren<ActivePowerPickup>(true);
            Assert(dropped.Length == 1 && dropped[0].Definition == shield && dropped[0].StoredCharge == 2 &&
                   dropped[0].transform.position == reward.transform.position + ActivePowerOffers.RewardDropOffset,
                "The held 권능 must drop by the treasure reward with its gauge.");

            Assert(runtime.Assembler.TryLoadFloor(2, runtime.Player, out string error), error);
            GeneratedRoomNode laterTreasure = runtime.Assembler.GeneratedGraph.FindFloor(2).Nodes
                .Single(node => node.Role == GeneratedRoomRole.Treasure);
            Assert(!runtime.Room(laterTreasure.RoomId).RewardRoom.OffersPower,
                "Later floors' treasure rooms must not offer a 권능 yet.");
            Assert(runtime.Assembler.TryLoadFloor(1, runtime.Player, out error), error);
            RewardRoom rebuilt = runtime.Room(treasure.RoomId).RewardRoom;
            rebuilt.SetPlayerPresenceForVerification(true, runtime.Inventory);
            ItemRewardSelectionState state = runtime.Progress.GetRewardSelection(reward.RewardId);
            Assert(rebuilt.HasRewarded && !rebuilt.TryOpenRewardSelection(runtime.Inventory) &&
                   state != null && state.IsCompleted && Ids(state.Candidates) == signature &&
                   powerSlot.HeldDefinition == offered.Definition,
                "A revisited floor-1 treasure room must keep its completed 권능 3택1 without granting again.");
        }

        private static void ValidateSecretRuntime()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = FindAll<RoomGraphAssembler>(scene).Single().Generator;
            int offeringSeed = FindSeed(generator, node => node.Role == GeneratedRoomRole.Secret && !node.IsGoldiShop &&
                                                           ActivePowerOffers.IsOfferedInRewardRoom(node));
            int plainSeed = FindSeed(generator, node => node.Role == GeneratedRoomRole.Secret && !node.IsGoldiShop &&
                                                        !ActivePowerOffers.IsOfferedInRewardRoom(node));

            Runtime plain = Runtime.Open(plainSeed);
            GeneratedRoomNode plainSecret = plain.Assembler.GeneratedGraph.FindFloor(1).Nodes
                .Single(node => node.Role == GeneratedRoomRole.Secret);
            Assert(!plain.Room(plainSecret.RoomId).RewardRoom.OffersPower,
                "A secret room whose 권능 roll misses must keep an artifact 3택1.");

            Runtime runtime = Runtime.Open(offeringSeed);
            GeneratedRoomNode secret = runtime.Assembler.GeneratedGraph.FindFloor(1).Nodes
                .Single(node => node.Role == GeneratedRoomRole.Secret);
            RewardRoom reward = runtime.Room(secret.RoomId).RewardRoom;
            Assert(reward.gameObject.activeSelf && reward.OffersPower, "A secret room whose roll hits must offer a 권능.");
            reward.SetPlayerPresenceForVerification(true, runtime.Inventory);
            Assert(reward.TryOpenRewardSelection(runtime.Inventory) && runtime.Session.Candidates.Count == 3 &&
                   runtime.Session.Candidates[2].Definition?.IsActivePower == true,
                "A secret room's 권능 3택1 must hold one 권능 card.");
            string signature = Ids(runtime.Session.Candidates);
            Assert(runtime.Session.TryCancel(), "The secret room 3택1 must be cancellable.");
            Assert(runtime.Assembler.TryLoadFloor(2, runtime.Player, out string error) &&
                   runtime.Assembler.TryLoadFloor(1, runtime.Player, out error), error);
            RewardRoom rebuilt = runtime.Room(secret.RoomId).RewardRoom;
            rebuilt.SetPlayerPresenceForVerification(true, runtime.Inventory);
            Assert(rebuilt.TryOpenRewardSelection(runtime.Inventory) && Ids(runtime.Session.Candidates) == signature,
                "A revisited secret room must reopen the same 권능 3택1.");
            runtime.Session.TryCancel();
        }

        private static void ValidateShopRuntime()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = FindAll<RoomGraphAssembler>(scene).Single().Generator;
            int seed = FindSeed(generator, node => (node.Role == GeneratedRoomRole.Shop || node.IsGoldiShop) &&
                                                   ActivePowerOffers.RollShop(node.ContentSeed));
            Runtime runtime = Runtime.Open(seed);
            GeneratedRoomNode node = runtime.Assembler.GeneratedGraph.FindFloor(1).Nodes
                .First(candidate => (candidate.Role == GeneratedRoomRole.Shop || candidate.IsGoldiShop) &&
                                    ActivePowerOffers.RollShop(candidate.ContentSeed));
            ShopRoom shop = FindShop(runtime, node.RoomId);
            ShopOffer offer = shop.Stock.Offers.Single(IsPower);
            string stock = OfferIds(shop.Stock);
            Assert(runtime.Assembler.TryLoadFloor(2, runtime.Player, out string error) &&
                   runtime.Assembler.TryLoadFloor(1, runtime.Player, out error), error);
            shop = FindShop(runtime, node.RoomId);
            Assert(OfferIds(shop.Stock) == stock, "A revisited shop must keep its 권능 stock.");
            runtime.Progress.TryAddResource(RunResourceType.Gold, offer.Price);
            Assert(shop.TryPurchase(offer.SlotIndex, runtime.Inventory) == ShopPurchaseResult.Purchased &&
                   runtime.PowerSlot.HeldDefinition == offer.Item,
                "Buying the shop's 권능 must put it in the slot.");
            Assert(runtime.Assembler.TryLoadFloor(2, runtime.Player, out error) &&
                   runtime.Assembler.TryLoadFloor(1, runtime.Player, out error), error);
            shop = FindShop(runtime, node.RoomId);
            Assert(shop.Stock.IsPurchased(offer.SlotIndex) &&
                   shop.TryPurchase(offer.SlotIndex, runtime.Inventory) == ShopPurchaseResult.SoldOut,
                "A bought 권능 stays sold after a revisit.");
        }

        private static ShopRoom FindShop(Runtime runtime, string roomId) => runtime.Assembler.CurrentFloorRoot
            .GetComponentsInChildren<ShopRoom>(true)
            .Single(shop => shop.Stock != null && shop.Stock.ShopId == ShopStockBuilder.BuildShopId(roomId));

        private static int FindSeed(FloorGenerator generator, Func<GeneratedRoomNode, bool> floorOneRoom)
        {
            for (int seed = 1; seed <= SeedCount * 4; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (graph.FindFloor(1).Nodes.Any(floorOneRoom)) return seed;
            }

            throw new InvalidOperationException("Power-2 found no seed with the floor-1 room it needs.");
        }

        private static string Signature(GeneratedFloor floor) => string.Join(";", floor.Nodes.Select(node =>
            $"{node.RoomId}:{node.Role}:{node.GridPosition}:{node.RequiresKey}:{node.IsGoldiShop}:" +
            $"{ActivePowerOffers.IsOfferedInRewardRoom(node)}:{ActivePowerOffers.RollShop(node.ContentSeed)}"));

        private static bool IsPower(ShopOffer offer) => offer.Kind == ShopOfferKind.Item && offer.Item.IsActivePower;

        private static string OfferIds(ShopStockState stock) => string.Join("|", stock.Offers.Select(offer =>
            $"{offer.OfferId}@{offer.Price}"));

        private static string Ids(IEnumerable<ItemRewardCandidate> candidates) =>
            string.Join("|", candidates.Select(candidate => candidate.StableId));

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            Assert(definition != null, $"Run Power-1 setup first: {itemId} is missing.");
            return definition;
        }

        // The Game Scene with a fresh Run on one seed and the player ready to take rewards.
        private sealed class Runtime
        {
            public RoomGraphAssembler Assembler;
            public RunProgress Progress;
            public ItemRewardSelectionSession Session;
            public PlayerMovement Player;
            public PlayerInventory Inventory;
            public PlayerPowerSlot PowerSlot;

            public static Runtime Open(int seed)
            {
                Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
                Runtime runtime = new() { Assembler = FindAll<RoomGraphAssembler>(scene).Single() };
                runtime.Progress = runtime.Assembler.Progress;
                runtime.Session = runtime.Assembler.RewardSelectionSession;
                runtime.Player = runtime.Assembler.Graph.Player;
                runtime.Inventory = runtime.Player.GetComponent<PlayerInventory>();
                runtime.PowerSlot = runtime.Player.GetComponent<PlayerPowerSlot>();
                Assert(runtime.Progress != null && runtime.Session != null && runtime.Inventory != null &&
                       runtime.PowerSlot != null && runtime.PowerSlot.PickupPrefab != null,
                    "Power-2 needs the Game Scene Run progress, 3택1 session, inventory and 권능 slot.");
                Health health = runtime.Player.GetComponent<Health>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(runtime.Player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(runtime.Player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(runtime.Inventory, "Awake");
                runtime.Session.Configure(runtime.Progress, runtime.Inventory, health,
                    runtime.Player.GetComponent<PlayerActionState>());
                runtime.PowerSlot.Configure(runtime.Progress, runtime.PowerSlot.PickupPrefab);
                runtime.Progress.ResetProgress();
                Assert(runtime.Progress.TryInitializeRunSeed(seed, out string error), error);
                Assert(runtime.Assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);
                return runtime;
            }

            public RoomPrefab Room(string roomId) => Assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node != null && room.Node.RoomId == roomId);
        }

        // A bare player with an inventory and a 권능 slot, outside any scene.
        private sealed class Rig : IDisposable
        {
            public GameObject Root;
            public RunProgress Progress;
            public PlayerInventory Inventory;
            public PlayerPowerSlot Slot;

            public static Rig Create()
            {
                Rig rig = new() { Root = new GameObject("Power-2 Verification") };
                GameObject progressObject = new("Progress", typeof(RunProgress));
                progressObject.transform.SetParent(rig.Root.transform);
                rig.Progress = progressObject.GetComponent<RunProgress>();
                GameObject player = new("Player", typeof(Rigidbody2D), typeof(Health), typeof(PlayerSP),
                    typeof(PlayerStats), typeof(PlayerInventory));
                player.GetComponent<Rigidbody2D>().gravityScale = 0f;
                player.transform.SetParent(rig.Root.transform);
                rig.Slot = player.AddComponent<PlayerPowerSlot>();
                rig.Inventory = player.GetComponent<PlayerInventory>();
                Health health = player.GetComponent<Health>();
                health.EnableHealthUnits();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(rig.Inventory, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerPowerEffects>(), "Awake");
                rig.Slot.Configure(rig.Progress,
                    AssetDatabase.LoadAssetAtPath<ActivePowerPickup>(Week27Power1Setup.PickupPrefabPath));
                InvokeLifecycle(rig.Slot, "Awake");
                Assert(rig.Slot.PickupPrefab != null, "Run Power-1 setup first: the 권능 pickup Prefab is missing.");
                return rig;
            }

            public void Dispose() => Object.DestroyImmediate(Root);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            if (target == null) return;
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
