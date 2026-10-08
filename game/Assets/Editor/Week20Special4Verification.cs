using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special4Verification
    {
        private const int SeedCount = 512;

        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Special-4 Shop")]
        public static void SetupAndVerifyBatch()
        {
            Week20Special4Setup.Setup();
            string[] paths =
            {
                Week20Special4Setup.ShopRoomPrefabPath, Week20Special4Setup.CatalogPath,
                Week20Special4Setup.ShopDefinitionPath, Week13FrontendSetup.GameScenePath,
            };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week20Special4Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Special-4 setup changed the shop Prefab, catalog, definition, or Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Special-4 Shop")]
        public static void Verify()
        {
            (ShopCatalog catalog, ShopRoom prefab, RoomDefinition definition) = ValidateAssets();
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = generator != null ? generator.GetComponent<RoomGraphAssembler>() : null;
            Assert(generator != null && assembler != null && assembler.Progress != null &&
                   generator.RoomDefinitions.Count(candidate => candidate == definition) == 1 &&
                   generator.RoomDefinitions.Count(candidate => candidate.RoomType == RoomType.Shop) == 1 &&
                   assembler.ShopRoomPrefab == prefab && assembler.ShopCatalog == catalog,
                "Special-4 requires the Game Scene generator to own the shop definition and the assembler its shop.");
            Assert(generator.RoomContentVersion >= Week20Special4Setup.RoomContentVersion &&
                   generator.EncounterContentVersion >= Week20Special4Setup.EncounterContentVersion,
                "Special-4 must raise the Room and Encounter content versions for the shop door.");

            int runtimeSeed = ValidateGeneration(generator);
            ValidateStockBuilder(catalog, assembler.SelectionRewardPool);
            ValidateRuntimePurchasesAndReload(assembler, catalog, runtimeSeed);
            Debug.Log("Special-4 verification passed: floors reproduce a 60% key-locked shop end room (at most one) " +
                      "beside the start or an intermediate room without changing existing rooms or the boss route, " +
                      "stock rolls three distinct Items at rarity prices and three distinct consumables, entry takes " +
                      "one " +
                      "key, purchases spend gold exactly once, Items go to the inventory, consumables drop as floor " +
                      "pickups, and stock, sold slots, and the open lock survive floor rebuilds.");
        }

        private static (ShopCatalog, ShopRoom, RoomDefinition) ValidateAssets()
        {
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            string error = null;
            Assert(catalog != null && catalog.TryValidate(out error), error ?? "The shop catalog is missing.");
            Assert(catalog.GetItemPrice(ItemRarity.Common) == 10 && catalog.GetItemPrice(ItemRarity.Uncommon) == 15 &&
                   catalog.GetItemPrice(ItemRarity.Rare) == 20 && catalog.GetItemPrice(ItemRarity.Epic) == 25,
                "Item prices must be 10/15/20/25 gold by rarity.");
            Assert(catalog.Consumables.Select(entry => (entry.ConsumableId, entry.Price)).SequenceEqual(new[]
                   {
                       ("heart", 3), ("key", 5), ("bomb", 5),
                   }),
                "Consumable prices must be heart 3, key 5, bomb 5 gold.");
            Assert(catalog.Consumables[0].PickupPrefab.GetComponent<HealthPickup>() != null &&
                   catalog.Consumables[1].PickupPrefab.GetComponent<RunResourcePickup>()?.ResourceType ==
                   RunResourceType.Key &&
                   catalog.Consumables[2].PickupPrefab.GetComponent<RunResourcePickup>()?.ResourceType ==
                   RunResourceType.Bomb,
                "Shop consumables must drop the existing heart, key, and bomb floor pickups.");

            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath);
            ShopRoom prefab = prefabObject != null ? prefabObject.GetComponent<ShopRoom>() : null;
            Assert(prefab != null && prefabObject.GetComponentsInChildren<Rigidbody2D>(true).Length == 0 &&
                   prefabObject.GetComponentsInChildren<Transform>(true).All(part =>
                       !part.name.StartsWith("Stall ", StringComparison.Ordinal) &&
                       GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(part.gameObject) == 0),
                "The ShopRoom Prefab must have no physics bodies, legacy stalls, or missing scripts.");
            ShopKeeper keeper = prefab.Keeper;
            ShopKeeper[] keepers = prefabObject.GetComponentsInChildren<ShopKeeper>(true);
            Collider2D[] colliders = keeper != null ? keeper.GetComponents<Collider2D>() : Array.Empty<Collider2D>();
            Assert(keepers.Length == 1 && keepers[0] == keeper &&
                   keeper.gameObject.layer == LayerMask.NameToLayer("Pickup") && colliders.Length == 1 &&
                   colliders[0] is BoxCollider2D && colliders[0].isTrigger &&
                   keeper.Portrait != null && keeper.Portrait.sprite != null && keeper.NameLabel != null &&
                   keeper.NameLabel.text == ShopKeeper.DisplayName && keeper.Prompt != null &&
                   keeper.Prompt.text == ShopKeeper.OpenPrompt && !keeper.Prompt.gameObject.activeSelf &&
                   (Vector2)keeper.transform.localPosition == Week20Special4Setup.KeeperPosition,
                "The ShopRoom Prefab needs one Pickup-layer shopkeeper trigger with portrait, name, and hidden prompt.");

            RoomDefinition definition =
                AssetDatabase.LoadAssetAtPath<RoomDefinition>(Week20Special4Setup.ShopDefinitionPath);
            RoomTemplateDefinition basic =
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room1Setup.BasicTemplatePath);
            Assert(definition != null && definition.TryValidate(out error) &&
                   definition.RoomDefinitionId == Week20Special4Setup.ShopDefinitionId &&
                   definition.RoomType == RoomType.Shop && basic != null && basic.SupportsRoomType(RoomType.Shop),
                error ?? "The shop definition and Basic template must support RoomType.Shop.");
            return (catalog, prefab, definition);
        }

        private static int ValidateGeneration(FloorGenerator generator)
        {
            GameObject holder = new("Special-4 generator without shop");
            try
            {
                FloorGenerator withoutShop = holder.AddComponent<FloorGenerator>();
                withoutShop.Configure(generator.FloorCount, generator.MinimumRoomsPerFloor,
                    generator.MaximumRoomsPerFloor, generator.MinimumBossDistance, generator.GenerationRetryLimit,
                    generator.RoomDefinitions.Where(candidate => candidate.RoomType != RoomType.Shop).ToArray());
                // Legacy generation appends shops without changing the base rooms. Floor-1 budgets the total first,
                // so adding a shop deliberately removes one regular room; test the old append contract separately.
                FloorGenerator legacyWithShop = holder.AddComponent<FloorGenerator>();
                legacyWithShop.Configure(generator.FloorCount, generator.MinimumRoomsPerFloor,
                    generator.MaximumRoomsPerFloor, generator.MinimumBossDistance, generator.GenerationRetryLimit,
                    generator.RoomDefinitions.ToArray());

                int floors = 0, shops = 0, runtimeSeed = 0;
                HashSet<GeneratedRoomRole> parentRoles = new();
                for (int seed = 1; seed <= SeedCount; seed++)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                    Assert(withoutShop.TryGenerateForSeed(seed, out GeneratedFloorGraph baseline, out error), error);
                    Assert(legacyWithShop.TryGenerateForSeed(seed, out GeneratedFloorGraph legacy, out error), error);
                    foreach (GeneratedFloor floor in first.Floors)
                    {
                        floors++;
                        GeneratedRoomNode[] shopRooms =
                            floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Shop).ToArray();
                        Assert(shopRooms.Length <= 1 &&
                               Signature(floor, true) == Signature(repeated.FindFloor(floor.FloorNumber), true),
                            $"Seed {seed} floor {floor.FloorNumber} must reproduce at most one shop.");
                        Assert(Signature(legacy.FindFloor(floor.FloorNumber), false) ==
                               Signature(baseline.FindFloor(floor.FloorNumber), false),
                            $"Seed {seed} floor {floor.FloorNumber} shop changed an existing room or passage.");
                        if (shopRooms.Length == 0) continue;

                        shops++;
                        GeneratedRoomNode shop = shopRooms[0];
                        Assert(shop.RoomNumber == floor.Nodes.Count && shop.RoomType == RoomType.Shop &&
                               shop.RequiresKey && shop.Encounter == null && shop.Template != null &&
                               shop.Template.SupportsRoomType(RoomType.Shop) &&
                               shop.DirectionalConnections.Count == 1 && !shop.DirectionalConnections[0].IsSecret,
                            $"Seed {seed} floor {floor.FloorNumber} shop must be the last key-locked end room.");
                        GeneratedRoomNode parent = floor.Nodes.Single(node =>
                            node.RoomId == shop.DirectionalConnections[0].DestinationRoomId);
                        Assert(parent.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Intermediate &&
                               parent.DirectionalConnections.Count(connection =>
                                   connection.DestinationRoomId == shop.RoomId && !connection.IsSecret) == 1,
                            $"Seed {seed} floor {floor.FloorNumber} shop must hang off the start or an intermediate room.");
                        Assert(floor.Nodes.All(node => node.Role != GeneratedRoomRole.Secret ||
                                                       !node.ConnectedRoomIds.Contains(shop.RoomId)),
                            $"Seed {seed} floor {floor.FloorNumber} secret room must not bypass the shop lock.");
                        parentRoles.Add(parent.Role);
                        if (runtimeSeed == 0 && floor.FloorNumber == 1) runtimeSeed = seed;
                    }
                }

                float ratio = shops / (float)floors;
                Assert(ratio >= 0.5f && ratio <= 0.7f,
                    $"Shops should appear on about 60% of floors; observed {ratio:P1} of {floors}.");
                Assert(runtimeSeed > 0 && parentRoles.Contains(GeneratedRoomRole.Intermediate),
                    "Seeds must include a floor-1 shop and shops beside intermediate rooms.");
                Debug.Log($"Special-4 generation: {shops}/{floors} floors ({ratio:P1}) have a shop. " +
                          $"Runtime seed {runtimeSeed}.");
                return runtimeSeed;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        // Topology signature; without the shop it must match a generator that has no shop definition at all.
        private static string Signature(GeneratedFloor floor, bool includeShop)
        {
            HashSet<string> shopIds = new(floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Shop)
                .Select(node => node.RoomId), StringComparer.Ordinal);
            return string.Join("|", floor.Nodes
                .Where(node => includeShop || !shopIds.Contains(node.RoomId))
                .Select(node => $"{node.RoomId}@{node.GridPosition}:{node.Role}:{node.ContentSeed}:" +
                                $"{node.Definition.RoomDefinitionId}:{node.RequiresKey}(" +
                                string.Join(",", node.DirectionalConnections
                                    .Where(connection => includeShop || !shopIds.Contains(connection.DestinationRoomId))
                                    .Select(connection =>
                                        $"{connection.Direction}>{connection.DestinationRoomId}:{connection.IsSecret}")) +
                                ")"));
        }

        private static void ValidateStockBuilder(ShopCatalog catalog, IReadOnlyList<ItemDefinition> pool)
        {
            Assert(pool != null && pool.Count >= ShopCatalog.ItemOfferCount,
                "The shop Item pool must be the treasure reward pool.");
            HashSet<string> distinctStocks = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 256; seed++)
            {
                ShopStockState stock = ShopStockBuilder.Build("floor-01-room-09", seed, catalog, pool, null);
                ShopStockState repeated = ShopStockBuilder.Build("floor-01-room-09", seed, catalog, pool, null);
                string signature = string.Join(",", stock.Offers.Select(offer => $"{offer.OfferId}={offer.Price}"));
                Assert(signature == string.Join(",", repeated.Offers.Select(offer => $"{offer.OfferId}={offer.Price}")),
                    $"Shop stock seed {seed} must reproduce the same offers.");
                distinctStocks.Add(signature);
                Assert(stock.ShopId == "floor-01-room-09:shop" && stock.Offers.Count == ShopCatalog.OfferCount &&
                       stock.Offers.Select((offer, index) => offer.SlotIndex == index).All(valid => valid) &&
                       stock.Offers.Select(offer => offer.OfferId).Distinct(StringComparer.Ordinal).Count() ==
                       ShopCatalog.OfferCount,
                    $"Shop stock seed {seed} must fill six distinct slots in order.");
                ShopOffer[] items = stock.Offers.Take(ShopCatalog.ItemOfferCount).ToArray();
                ShopOffer[] consumables = stock.Offers.Skip(ShopCatalog.ItemOfferCount).ToArray();
                Assert(items.All(offer => offer.Kind == ShopOfferKind.Item && pool.Contains(offer.Item) &&
                                          offer.Price == catalog.GetItemPrice(offer.Item.Rarity)) &&
                       consumables.All(offer => offer.Kind == ShopOfferKind.Consumable &&
                                                offer.Price == offer.Consumable.Price),
                    $"Shop stock seed {seed} must sell three Items at rarity price and three consumables at list price.");
            }

            Assert(distinctStocks.Count >= 8, "Different shop seeds must roll different stocks.");
            ItemDefinition single = pool.First(definition => definition != null && definition.IsValid);
            ShopStockState thin = ShopStockBuilder.Build("floor-01-room-09", 7, catalog, new[] { single }, null);
            Assert(thin.Offers.Count(offer => offer.Kind == ShopOfferKind.Item) == 1 &&
                   thin.Offers.Count(offer => offer.Kind == ShopOfferKind.Consumable) == catalog.Consumables.Count &&
                   thin.Offers.Count <= ShopCatalog.OfferCount,
                "Missing eligible Items must be filled with the remaining consumables, leaving the rest empty.");
        }

        private static void ValidateRuntimePurchasesAndReload(RoomGraphAssembler assembler, ShopCatalog catalog,
            int seed)
        {
            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode shopNode = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Shop);
            GeneratedRoomConnection back = shopNode.DirectionalConnections[0];
            RoomPrefab parentRoom = FindRuntimeRoom(assembler, back.DestinationRoomId);
            RoomPrefab shopRoom = FindRuntimeRoom(assembler, shopNode.RoomId);
            RoomRunState shopState = progress.GetRoomState(shopNode.RoomId);
            ShopRoom shop = shopRoom.GetComponentInChildren<ShopRoom>(true);
            ShopStockState stock = progress.GetShopStock(ShopStockBuilder.BuildShopId(shopNode.RoomId));
            Assert(shop != null && stock != null && shop.Stock == stock &&
                   shop.transform.parent == shopRoom.Node.ContentRoot.transform &&
                   shop.Keeper != null && shop.Session == assembler.ShopSession &&
                   stock.Offers.Count == ShopCatalog.OfferCount &&
                   shopRoom.Controller.State == RoomState.Cleared &&
                   (shopRoom.RewardRoom == null || !shopRoom.RewardRoom.gameObject.activeSelf),
                "The shop room must be a safe room showing its stored stock without a treasure reward.");

            RoomDoorSlot door = parentRoom.FindSlot(GeneratedFloorGraph.Opposite(back.Direction));
            parentRoom.Controller.BindRunState(progress.GetRoomState(parentRoom.Node.RoomId), true);
            Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), parentRoom.Node,
                assembler.Graph.Player, out error), error);
            Assert(door.Doorway.RequiresKey && door.Blocker.IsLocked &&
                   door.Blocker.VisualKind == DoorVisualKind.Shop,
                "The shop door must use its coin identity while retaining the key lock.");
            Assert(!door.Doorway.TryEnter(assembler.Graph.Player) && !shopState.IsKeyLockOpen &&
                   assembler.Graph.CurrentNode == parentRoom.Node,
                "The shop must reject entry without a key.");
            Assert(progress.TryAddResource(RunResourceType.Key, 1) == 1 && door.Doorway.TryEnter(assembler.Graph.Player) &&
                   shopState.IsKeyLockOpen && progress.GetResourceCount(RunResourceType.Key) == 0 &&
                   assembler.Graph.CurrentNode == shopRoom.Node,
                "The first shop entry must consume exactly one key.");

            PlayerInventory inventory = assembler.Graph.Player.GetComponent<PlayerInventory>();
            Assert(inventory != null, "Runtime shop verification requires the player inventory.");
            // Edit mode skips Awake; an Artifact purchase applies its effect through the inventory's PlayerStats.
            Component stats = inventory.GetComponent("PlayerStats");
            stats?.GetType().GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(stats, null);
            typeof(PlayerInventory).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(inventory, null);
            ShopOffer item = stock.Offers.First(offer => offer.Kind == ShopOfferKind.Item);
            int stacks = inventory.GetStackCount(item.Item.ItemId);
            Assert(shop.TryPurchase(item.SlotIndex, inventory) == ShopPurchaseResult.NotEnoughGold &&
                   !stock.IsPurchased(item.SlotIndex) && progress.GetResourceCount(RunResourceType.Gold) == 0 &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks,
                "Without enough gold a purchase must change nothing.");

            int budget = stock.Offers.Sum(offer => offer.Price);
            Assert(progress.TryAddResource(RunResourceType.Gold, budget) == budget, "Verification gold must fit.");
            Assert(shop.TryPurchase(item.SlotIndex, inventory) == ShopPurchaseResult.Purchased &&
                   stock.IsPurchased(item.SlotIndex) && inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == budget - item.Price &&
                   shop.LastDroppedPickup == null &&
                   shop.GetStatus(item.SlotIndex, inventory) == ShopOfferStatus.SoldOut,
                "An Item purchase must spend its price once, go to the inventory, and sell out its slot.");
            Assert(shop.TryPurchase(item.SlotIndex, inventory) == ShopPurchaseResult.SoldOut &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == budget - item.Price,
                "A sold slot must not be bought again.");

            int spent = item.Price;
            HashSet<Vector2> dropPoints = new();
            foreach (ShopOffer consumable in stock.Offers.Where(offer => offer.Kind == ShopOfferKind.Consumable))
            {
                Assert(shop.TryPurchase(consumable.SlotIndex, inventory) == ShopPurchaseResult.Purchased &&
                       stock.IsPurchased(consumable.SlotIndex) &&
                       progress.GetResourceCount(RunResourceType.Gold) == budget - spent - consumable.Price,
                    $"Buying {consumable.OfferId} must spend its price once.");
                spent += consumable.Price;
                GameObject drop = shop.LastDroppedPickup;
                bool rightPickup = consumable.Consumable.ConsumableId switch
                {
                    "heart" => drop != null && drop.GetComponent<HealthPickup>() != null,
                    "key" => drop != null && drop.GetComponent<RunResourcePickup>()?.ResourceType == RunResourceType.Key,
                    "bomb" => drop != null && drop.GetComponent<RunResourcePickup>()?.ResourceType == RunResourceType.Bomb,
                    _ => false,
                };
                Assert(rightPickup && drop.transform.parent == shopRoom.Node.ContentRoot.transform &&
                       Vector2.Distance(drop.transform.position, shop.GetDropPosition(consumable.SlotIndex)) < 0.01f &&
                       Mathf.Abs(drop.transform.position.y -
                                 (shop.Keeper.transform.position.y + ShopRoom.DropOffsetY)) < 0.01f &&
                       dropPoints.Add(drop.transform.position),
                    $"Buying {consumable.OfferId} must drop its floor pickup at its own spot in front of the shopkeeper.");
            }

            Assert(progress.GetResourceCount(RunResourceType.Key) == 0 &&
                   progress.GetResourceCount(RunResourceType.Bomb) == 0,
                "Bought consumables must wait on the floor instead of being granted directly.");

            int goldBeforeReload = progress.GetResourceCount(RunResourceType.Gold);
            Assert(assembler.TryLoadFloor(2, null, out error) && assembler.TryLoadFloor(1, null, out error), error);
            RoomPrefab rebuiltRoom = FindRuntimeRoom(assembler, shopNode.RoomId);
            ShopRoom rebuilt = rebuiltRoom.GetComponentInChildren<ShopRoom>(true);
            Assert(rebuilt != null && rebuilt != shop && rebuilt.Stock == stock &&
                   progress.GetShopStock(stock.ShopId) == stock &&
                   stock.Offers.Count(offer => rebuilt.IsSold(offer)) == 1 + ShopCatalog.ConsumableOfferCount &&
                   progress.GetRoomState(shopNode.RoomId).IsKeyLockOpen &&
                   progress.GetResourceCount(RunResourceType.Gold) == goldBeforeReload &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks + 1,
                "Floor rebuilds must keep the same stock, sold slots, open lock, and spent gold.");
            ShopOffer remaining = stock.Offers.First(offer => !stock.IsPurchased(offer.SlotIndex));
            Assert(stock.Offers.Count(offer => !stock.IsPurchased(offer.SlotIndex)) == ShopCatalog.ItemOfferCount - 1 &&
                   rebuilt.GetStatus(remaining.SlotIndex, inventory) == ShopOfferStatus.Available &&
                   rebuilt.GetPrice(remaining) == remaining.Price,
                "An unsold offer must still be on sale at its price after a rebuild.");

            progress.StopProgression();
            Assert(rebuilt.TryPurchase(remaining.SlotIndex, inventory) == ShopPurchaseResult.Unavailable &&
                   !stock.IsPurchased(remaining.SlotIndex),
                "Shops must not sell after the Run stops.");
            progress.ResetProgress();
            Assert(progress.GetShopStock(stock.ShopId) == null && progress.GetResourceCount(RunResourceType.Gold) == 0,
                "A new Run must forget shop stock and gold.");
        }

        private static RoomPrefab FindRuntimeRoom(RoomGraphAssembler assembler, string roomId)
        {
            foreach (RoomNode node in assembler.Graph.Nodes)
                if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
            throw new InvalidOperationException($"Runtime room {roomId} is missing.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
