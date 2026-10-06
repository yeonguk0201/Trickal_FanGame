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
    // Spell-4: 멤버십카드 makes every offer still on sale in the shop the player stands in free. It is refused and kept
    // outside a shop, each free offer is taken once, the free state survives a shop rebuild, other shops keep their
    // prices, and a sold-out or already free shop still spends the card (D5).
    public static class Week22Spell4Verification
    {
        private const int ShopRoomNumber = 6;
        private const int OtherShopRoomNumber = 7;

        public static void SetupAndVerifyBatch()
        {
            Week22Spell4Setup.Setup();
            string[] paths =
            {
                Week22Spell4Setup.ItemPath, Week22Chest1Setup.ChestContentTablePath,
                Week20Special4Setup.ShopRoomPrefabPath, Week13FrontendSetup.GameScenePath,
            };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week22Spell4Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Spell-4 setup changed an item, chest table, shop Prefab or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the shop, its gold texts, the shared slot, the other single-use items on the same executor,
        // the chest pools and the development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // Gold-0 runs the Special-4 shop verification itself.
            Week21Gold0Verification.Verify();
            Week21Slot0Verification.Verify();
            Week22Spell5Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Spell-4 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Spell-4 Membership Card")]
        public static void Verify()
        {
            ItemDefinition card = ValidateContract();
            string[] poolPaths = ValidateSceneAndPools(card);
            // The runtime checks spawn a shop and pickups; keep them out of the Game Scene. Closing it unloads the
            // assets only it referenced, so the card and the shop Item pool are loaded again.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ItemDefinition[] pool = poolPaths.Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).ToArray();
            Assert(pool.Length >= ShopCatalog.ItemOfferCount && pool.All(item => item != null && item.IsValid),
                "The shop Item pool must be the valid selection reward pool.");
            ValidateShopUse(AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Spell4Setup.ItemPath), pool);
            Debug.Log("Spell-4 verification passed: 멤버십카드 is refused and kept outside a shop, makes every offer " +
                      "still on sale in the current shop free, each free offer is taken once without spending gold, " +
                      "the free and sold state survives a shop rebuild, other shops keep their prices, a sold-out " +
                      "shop spends the card without effect, and chests hold it.");
        }

        private static ItemDefinition ValidateContract()
        {
            Assert((int)ItemEffectType.FreeCurrentShopOffers == 42 &&
                   (int)ItemEffectType.DuplicateRoomChestsAndPickups == 41,
                "Spell-4 must add effect type 42 after the existing effects without renumbering them.");
            Assert(new ItemEffectEntry(ItemEffectType.FreeCurrentShopOffers).TryValidate(out _),
                "FreeCurrentShopOffers uses no value fields.");

            ItemDefinition card = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Spell4Setup.ItemPath);
            Assert(card != null, $"Run Spell-4 setup first: {Week22Spell4Setup.ItemPath} is missing.");
            Assert(card.ItemId == Week22Spell4Setup.MembershipCardId &&
                   card.DisplayName == Week22Spell4Setup.MembershipCardName && card.Kind == ItemKind.SingleUseSpell &&
                   card.Rarity == ItemRarity.Rare && card.IsActive && card.MaxStacks == 1 && card.IsValid &&
                   card.IsSingleUse &&
                   ItemDefinition.IsItemIdValidForKind(card.ItemId, ItemKind.SingleUseSpell),
                "멤버십카드 must be a valid active Rare single-use spell.");
            Assert(card.Effects.Count == 1 && card.Effects[0].EffectType == ItemEffectType.FreeCurrentShopOffers,
                "멤버십카드 must carry exactly the free shop effect.");
            string built = ArtifactEffectDescription.Build(card);
            Assert(built == "현재 상점의 남은 상품을 모두 무료로 변경 (상점 밖에서는 사용 불가)",
                $"Unexpected 멤버십카드 description: {built}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(card.DisplayName + built + ShopView.FreeLabel +
                                                      ShopView.TakeLabel),
                "The Frontend font is missing a 멤버십카드 or free shop glyph.");
            return card;
        }

        private static string[] ValidateSceneAndPools(ItemDefinition card)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out string error), "The chest content table is missing or invalid.");
            Assert(table.Spells.Contains(card) && !table.JjangsemSpells.Contains(card) &&
                   assembler.ChestContentTable == table,
                "Chests must draw 멤버십카드 from the single-use spell list.");
            Assert(assembler.SelectionRewardPool.All(item => item != null && !item.IsSingleUse),
                "Single-use spells must not join the selection reward pool or the shop stock (Contract-0 §3).");
            Assert(assembler.Graph.Player.GetComponent<PlayerSingleUseEffects>() != null,
                "The Game Scene player must have the single-use executor.");
            return assembler.SelectionRewardPool.Select(AssetDatabase.GetAssetPath).ToArray();
        }

        private static void ValidateShopUse(ItemDefinition card, ItemDefinition[] pool)
        {
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            ShopRoom shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath)
                ?.GetComponent<ShopRoom>();
            Assert(catalog != null && shopPrefab != null, "Run Special-4 setup first: the shop catalog or Prefab is missing.");

            using TestPlayer test = new();
            RunProgress progress = test.Progress;
            string roomId = FloorGenerator.BuildRoomId(1, ShopRoomNumber);
            string otherRoomId = FloorGenerator.BuildRoomId(1, OtherShopRoomNumber);
            ShopStockState stock = progress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(roomId),
                () => ShopStockBuilder.Build(roomId, 2204, catalog, pool, test.Inventory));
            ShopStockState other = progress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(otherRoomId),
                () => ShopStockBuilder.Build(otherRoomId, 2205, catalog, pool, test.Inventory));
            Assert(stock.Offers.Count == ShopCatalog.OfferCount &&
                   stock.Offers.Count(offer => offer.Kind == ShopOfferKind.Item) == ShopCatalog.ItemOfferCount,
                $"The verification shop must hold the current six offers; pool {pool.Length}, got " +
                string.Join(",", stock.Offers.Select(offer => offer.OfferId)) + ".");
            int[] listed = stock.Offers.Select(offer => offer.Price).ToArray();

            ShopRoom shop = Object.Instantiate(shopPrefab, test.Floor.transform);
            shop.Configure(progress, stock, test.Floor.transform);
            ShopOffer item = stock.Offers.First(offer => offer.Kind == ShopOfferKind.Item);
            ShopOffer[] consumables = stock.Offers.Where(offer => offer.Kind == ShopOfferKind.Consumable).ToArray();

            // Outside a shop the card is refused and stays in the slot.
            test.Give(card, "spell4-card-1");
            foreach (int room in new[] { 1, 2, 4, 5 })
            {
                progress.RecordRoomEntry(1, room);
                Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == card &&
                       !stock.IsFree && !other.IsFree,
                    $"멤버십카드 must be refused and kept in room {room}, outside a shop.");
            }

            progress.RecordRoomEntry(1, ShopRoomNumber);
            Assert(progress.GetResourceCount(RunResourceType.Gold) == 0 && !shop.CanAfford(item) &&
                   shop.TryPurchase(item.SlotIndex, test.Inventory) == ShopPurchaseResult.NotEnoughGold &&
                   shop.GetStatus(item.SlotIndex, test.Inventory) == ShopOfferStatus.NotEnoughGold &&
                   ShopView.FormatPriceLabel(ShopOfferStatus.NotEnoughGold, shop.GetPrice(item)) ==
                   $"{item.Price} 골드",
                "Before the card the shop must ask its listed gold prices.");
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem && !stock.IsFree,
                "멤버십카드 must not be used while paused.");
            Time.timeScale = 1f;

            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem && stock.IsFree,
                "멤버십카드 must be consumed in a shop and make its stock free.");
            Assert(stock.Offers.All(offer => stock.GetPrice(offer.SlotIndex) == 0 && shop.GetPrice(offer) == 0 &&
                                             shop.CanAfford(offer)) &&
                   stock.Offers.Select(offer => offer.Price).SequenceEqual(listed),
                "Every offer on sale must cost nothing while the listed prices stay recorded.");
            Assert(!other.IsFree && other.Offers.All(offer => other.GetPrice(offer.SlotIndex) == offer.Price &&
                                                              offer.Price > 0),
                "Another shop must keep its prices.");
            Assert(stock.Offers.All(offer =>
                       shop.GetStatus(offer.SlotIndex, test.Inventory) == ShopOfferStatus.Free &&
                       ShopView.FormatPriceLabel(ShopOfferStatus.Free, shop.GetPrice(offer)) == ShopView.FreeLabel),
                "Free offers must show the free label instead of a gold price and be takable with no gold.");

            // Each free offer is taken once and spends no gold, with or without gold in the wallet.
            int stacks = test.Inventory.GetStackCount(item.Item.ItemId);
            Assert(shop.TryPurchase(item.SlotIndex, test.Inventory) == ShopPurchaseResult.Purchased &&
                   stock.IsPurchased(item.SlotIndex) && test.Inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == 0 &&
                   shop.GetStatus(item.SlotIndex, test.Inventory) == ShopOfferStatus.SoldOut,
                "A free Item must go to the inventory once without gold.");
            Assert(shop.TryPurchase(item.SlotIndex, test.Inventory) == ShopPurchaseResult.SoldOut &&
                   test.Inventory.GetStackCount(item.Item.ItemId) == stacks + 1,
                "A free offer must not be taken twice.");
            Assert(progress.TryAddResource(RunResourceType.Gold, 50) == 50, "Verification gold must fit.");
            Assert(shop.TryPurchase(consumables[0].SlotIndex, test.Inventory) == ShopPurchaseResult.Purchased &&
                   shop.LastDroppedPickup != null && progress.GetResourceCount(RunResourceType.Gold) == 50,
                "A free consumable must drop its pickup and leave the wallet untouched.");

            // A second card in the same shop is spent without changing anything (D5: no protection).
            test.Give(card, "spell4-card-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem && stock.IsFree &&
                   stock.Offers.Count(offer => stock.IsPurchased(offer.SlotIndex)) == 2,
                "A second 멤버십카드 in an already free shop must be spent without effect.");

            // Revisit: the rebuilt shop room shows the same stock, still free, with the same sold slots.
            progress.RecordRoomEntry(1, 1);
            Object.DestroyImmediate(shop.gameObject);
            progress.RecordRoomEntry(1, ShopRoomNumber);
            ShopRoom rebuilt = Object.Instantiate(shopPrefab, test.Floor.transform);
            rebuilt.Configure(progress, progress.GetShopStock(stock.ShopId), test.Floor.transform);
            Assert(rebuilt.Stock == stock && stock.IsFree &&
                   stock.Offers.Count(offer => rebuilt.IsSold(offer)) == 2 &&
                   stock.Offers.Where(offer => !rebuilt.IsSold(offer)).All(offer =>
                       rebuilt.GetStatus(offer.SlotIndex, test.Inventory) == ShopOfferStatus.Free),
                "A revisit must keep the free prices and the sold slots.");
            Assert(rebuilt.TryPurchase(item.SlotIndex, test.Inventory) == ShopPurchaseResult.SoldOut,
                "A revisit must not hand out a taken offer again.");

            foreach (ShopOffer offer in stock.Offers.Where(offer => !stock.IsPurchased(offer.SlotIndex)).ToArray())
            {
                Assert(rebuilt.TryPurchase(offer.SlotIndex, test.Inventory) == ShopPurchaseResult.Purchased,
                    $"The free offer {offer.OfferId} must be taken after the revisit.");
            }

            Assert(stock.Offers.All(offer => stock.IsPurchased(offer.SlotIndex)) &&
                   progress.GetResourceCount(RunResourceType.Gold) == 50,
                "Every free offer must be taken exactly once without spending gold.");

            // Sold out (D5, 2026-10-05): still usable, spent without effect.
            test.Give(card, "spell4-card-3");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   progress.GetResourceCount(RunResourceType.Gold) == 50,
                "A sold-out shop must spend 멤버십카드 without effect.");

            // The other shop was never freed: its own card use frees only it.
            progress.RecordRoomEntry(1, OtherShopRoomNumber);
            Assert(!other.IsFree, "Entering another shop must not carry the free state over.");
            test.Give(card, "spell4-card-4");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && other.IsFree,
                "멤버십카드 must free the shop it is used in.");

            progress.ResetProgress();
            Assert(progress.GetShopStock(stock.ShopId) == null, "A new Run must forget the free shop stock.");
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // A player with the real slot, executor and inventory, and a small generated graph: floor 1 has a start room
        // (1), combat rooms (2, 3), a treasure room (4), a boss room (5) and two shop rooms (6, 7).
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly SingleUseItemPickup prefab;
            private readonly float previousTimeScale;

            public GameObject Player { get; }
            public GameObject Floor { get; }
            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerInventory Inventory { get; }
            public PlayerSpellSlot Slot { get; }

            public TestPlayer()
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Spell-4 Progress", typeof(RunProgress));
                Floor = new GameObject("Spell-4 Floor");
                Progress = progressObject.GetComponent<RunProgress>();
                ConfigureTestGraph(Progress);
                Player = new GameObject("Spell-4 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Player.transform.position = new Vector3(9000f, 9000f, 0f);
                Health = Player.GetComponent<Health>();
                Slot = Player.GetComponent<PlayerSpellSlot>();
                Inventory = Player.GetComponent<PlayerInventory>();
                typeof(PlayerInventory).GetField("runProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(Inventory, Progress);
                InvokeLifecycle(Health, "Awake");
                Health.EnableHealthUnits();
                InvokeLifecycle(Player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(Player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(Inventory, "Awake");
                Slot.Configure(Progress, prefab);
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Player.GetComponent<PlayerSingleUseEffects>(), "Awake");
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(prefab, Floor.transform);
                pickup.Configure(item, instanceId, false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(Player);
                Object.DestroyImmediate(Floor);
                Object.DestroyImmediate(progressObject);
            }

            private static void ConfigureTestGraph(RunProgress progress)
            {
                GeneratedRoomNode[] nodes =
                {
                    Node(1, GeneratedRoomRole.Start),
                    Node(2, GeneratedRoomRole.Intermediate),
                    Node(3, GeneratedRoomRole.Intermediate),
                    Node(4, GeneratedRoomRole.Treasure),
                    Node(5, GeneratedRoomRole.Boss),
                    Node(ShopRoomNumber, GeneratedRoomRole.Shop),
                    Node(OtherShopRoomNumber, GeneratedRoomRole.Shop),
                };
                GeneratedFloor first = new(1, 0, 0, 0, nodes[0].RoomId, nodes[4].RoomId, nodes);
                FieldInfo graphField = typeof(RunProgress).GetField("<GeneratedGraph>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (graphField == null) throw new InvalidOperationException("RunProgress graph storage was not found.");
                graphField.SetValue(progress, new GeneratedFloorGraph(new[] { first }, 1));

                FieldInfo statesField = typeof(RunProgress).GetField("roomStates",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, RoomRunState> states =
                    (Dictionary<string, RoomRunState>)statesField?.GetValue(progress);
                if (states == null) throw new InvalidOperationException("RunProgress room state storage was not found.");
                foreach (GeneratedRoomNode node in nodes) states.Add(node.RoomId, new RoomRunState(node.RoomId));
            }

            private static GeneratedRoomNode Node(int roomNumber, GeneratedRoomRole role) =>
                new(FloorGenerator.BuildRoomId(1, roomNumber), 1, roomNumber, role, null);
        }
    }
}
