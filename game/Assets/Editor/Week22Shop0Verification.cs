using System;
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

namespace TrickalFanGame.Editor
{
    // Shop-0: the shop sells three Items and three consumables through a 3×3 screen opened at the shopkeeper. The
    // screen shows the stored stock, moves its focus inside the grid, buys through the same ShopRoom rules, keeps
    // sold and free (멤버십카드) state across closing, reopening and floor rebuilds, and blocks the player meanwhile.
    public static class Week22Shop0Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week22Shop0Setup.Setup();
            string[] paths =
            {
                Week20Special4Setup.ShopRoomPrefabPath, Week20Special4Setup.CatalogPath,
                Week20Special4Setup.ShopDefinitionPath, Week13FrontendSetup.GameScenePath,
            };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week22Shop0Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Shop-0 setup changed the shop Prefab, catalog, definition, or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the shop generation and purchase rules, their gold texts, 멤버십카드, the reward selection
        // screen that shares the HUD raycaster, and the development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // Gold-0 runs the Special-4 shop verification itself.
            Week21Gold0Verification.Verify();
            Week22Spell4Verification.Verify();
            Week21Slot0Verification.Verify();
            Week16Reward3Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Shop-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Shop-0 Shop UI")]
        public static void Verify()
        {
            ValidateContract();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            (RoomGraphAssembler assembler, ShopSession session, ShopView view) = ValidateScene(scene);
            try
            {
                ValidateRuntime(assembler, session, view);
            }
            finally
            {
                // The runtime checks build a floor in the Game Scene; discard it without saving.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            Debug.Log("Shop-0 verification passed: the shop sells three Items and three consumables in a 3x3 screen " +
                      "opened at the shopkeeper, the focus stays inside the grid, purchases spend gold once and " +
                      "sell out their cell, consumables drop in front of the shopkeeper, the player is blocked " +
                      "while the screen is open, and sold and free state survive closing, reopening and a floor " +
                      "rebuild.");
        }

        private static void ValidateContract()
        {
            Assert(ShopCatalog.ItemOfferCount == 3 && ShopCatalog.ConsumableOfferCount == 3 &&
                   ShopCatalog.OfferCount == 6 && ShopCatalog.GridColumns == 3 && ShopCatalog.GridSlotCount == 9,
                "Shop-0 sells three Items and three consumables in a 3x3 grid (D5).");
            Assert(ShopView.FormatPrice(10) == "10 골드" && ShopView.FormatPrice(0) == ShopView.FreeLabel &&
                   ShopView.FormatPriceLabel(ShopOfferStatus.SoldOut, 10) == ShopView.SoldOutLabel &&
                   ShopView.FormatPriceLabel(ShopOfferStatus.ItemUnavailable, 10) == ShopView.ItemUnavailableLabel &&
                   ShopView.FormatPriceLabel(ShopOfferStatus.Empty, 10) == string.Empty,
                "Shop cells must show a gold price, the free label, sold out, the stack limit, or nothing.");

            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(catalog != null && catalog.TryValidate(out _) &&
                   catalog.Consumables.Count >= ShopCatalog.ConsumableOfferCount,
                "Run Shop-0 setup first: the shop catalog is missing or too small.");
            Assert(font != null && font.HasCharacters(Week22Shop0Setup.RequiredGlyphs(catalog)),
                "The Frontend font is missing a shop screen glyph.");
        }

        private static (RoomGraphAssembler, ShopSession, ShopView) ValidateScene(Scene scene)
        {
            ShopSession[] sessions = FindAll<ShopSession>(scene);
            ShopView[] views = FindAll<ShopView>(scene);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            Assert(sessions.Length == 1 && views.Length == 1 && views[0].gameObject == sessions[0].gameObject &&
                   views[0].name == Week22Shop0Setup.OverlayName,
                "Run Shop-0 setup first: the Game Scene needs exactly one shop overlay with its session and view.");
            ShopSession session = sessions[0];
            ShopView view = views[0];
            PlayerInventory inventory = FindAll<PlayerInventory>(scene).Single();
            Assert(view.Session == session && assembler.ShopSession == session &&
                   session.PlayerActionState == inventory.GetComponent<PlayerActionState>() &&
                   session.PlayerHealth == inventory.GetComponent<Health>() && session.PlayerActionState != null &&
                   session.PlayerHealth != null,
                "The shop view, the room assembler, and the player must share the one shop session.");
            Assert(view.Overlay != null && view.Overlay.alpha == 0f && !view.Overlay.interactable &&
                   !view.Overlay.blocksRaycasts && view.GraphicRaycaster != null && view.KeeperPortrait != null &&
                   view.GoldText != null && view.DetailNameText != null && view.DetailKindText != null &&
                   view.DetailEffectText != null && view.DetailPriceText != null && view.FeedbackText != null &&
                   view.BuyButton != null && view.BuyButtonLabel != null && view.CloseButton != null,
                "The shop overlay must be saved hidden with every text and button connected.");
            Assert(view.Cells.Length == ShopCatalog.GridSlotCount &&
                   view.Cells.Distinct().Count() == ShopCatalog.GridSlotCount,
                "The shop screen needs nine distinct grid cells.");
            for (int index = 0; index < view.Cells.Length; index++)
            {
                ShopOfferCellView cell = view.Cells[index];
                Assert(cell != null && cell.CellIndex == index && cell.Button != null && cell.Content != null &&
                       cell.FocusFrame != null && cell.Icon != null && cell.IconText != null &&
                       cell.NameText != null && cell.PriceText != null &&
                       cell.Button.navigation.mode == UnityEngine.UI.Navigation.Mode.None &&
                       ((RectTransform)cell.transform).anchoredPosition == Week22Shop0Setup.CellPosition(index),
                    $"Shop cell {index + 1} must sit at its grid position with icon, name, price, and focus frame.");
            }

            for (int row = 0; row < ShopCatalog.GridColumns; row++)
            for (int column = 1; column < ShopCatalog.GridColumns; column++)
            {
                Vector2 left = Week22Shop0Setup.CellPosition(row * ShopCatalog.GridColumns + column - 1);
                Vector2 right = Week22Shop0Setup.CellPosition(row * ShopCatalog.GridColumns + column);
                Assert(right.x - left.x >= Week22Shop0Setup.CellSize.x && Mathf.Approximately(left.y, right.y),
                    "Shop grid cells must not overlap.");
            }

            ShopRoom prefab = assembler.ShopRoomPrefab;
            Assert(prefab != null && prefab.Keeper != null &&
                   prefab.GetComponentsInChildren<Transform>(true).All(part =>
                       GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(part.gameObject) == 0),
                "The ShopRoom Prefab must have the shopkeeper and no missing scripts.");
            return (assembler, session, view);
        }

        private static void ValidateRuntime(RoomGraphAssembler assembler, ShopSession session, ShopView view)
        {
            // Edit mode skips Awake/OnEnable; the view subscribes to its session there.
            InvokeLifecycle(view, "OnEnable");
            foreach (ShopOfferCellView cell in view.Cells) InvokeLifecycle(cell, "OnEnable");

            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            int seed = FindFloorOneShopSeed(assembler.Generator);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            GeneratedRoomNode shopNode = assembler.GeneratedGraph.FindFloor(1).Nodes
                .Single(node => node.Role == GeneratedRoomRole.Shop);
            GeneratedRoomConnection back = shopNode.DirectionalConnections[0];
            RoomPrefab parentRoom = FindRuntimeRoom(assembler, back.DestinationRoomId);
            RoomPrefab shopRoom = FindRuntimeRoom(assembler, shopNode.RoomId);
            ShopRoom shop = shopRoom.GetComponentInChildren<ShopRoom>(true);
            ShopStockState stock = progress.GetShopStock(ShopStockBuilder.BuildShopId(shopNode.RoomId));
            Assert(shop != null && stock != null && shop.Stock == stock && shop.Session == session &&
                   shop.Keeper != null && stock.Offers.Count == ShopCatalog.OfferCount &&
                   stock.Offers.Take(ShopCatalog.ItemOfferCount).All(offer => offer.Kind == ShopOfferKind.Item) &&
                   stock.Offers.Skip(ShopCatalog.ItemOfferCount).All(offer => offer.Kind == ShopOfferKind.Consumable),
                "The shop room must hold three Items then three consumables and share the scene's shop session.");

            GameObject player = assembler.Graph.Player.gameObject;
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            Health health = player.GetComponent<Health>();
            PlayerActionState actionState = player.GetComponent<PlayerActionState>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(inventory, "Awake");
            ShopKeeper keeper = shop.Keeper;

            // Outside the trigger the shop does not open.
            Assert(!keeper.CanInteract && !keeper.TryOpen() && !session.IsOpen && !view.IsVisible,
                "The shop must not open while the player is away from the shopkeeper.");

            parentRoom.Controller.BindRunState(progress.GetRoomState(parentRoom.Node.RoomId), true);
            Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), parentRoom.Node,
                assembler.Graph.Player, out error), error);
            RoomDoorSlot door = parentRoom.FindSlot(GeneratedFloorGraph.Opposite(back.Direction));
            Assert(progress.TryAddResource(RunResourceType.Key, 1) == 1 && door.Doorway.TryEnter(assembler.Graph.Player) &&
                   assembler.Graph.CurrentNode == shopRoom.Node,
                "Entering the shop must still take one key.");

            keeper.SetPlayerPresence(true, inventory, health, actionState);
            Assert(keeper.CanInteract && keeper.Prompt.gameObject.activeSelf && actionState.CanMove,
                "Next to the shopkeeper the open prompt must show.");
            Assert(keeper.TryOpen() && session.IsOpen && session.Shop == shop && session.Inventory == inventory &&
                   view.IsVisible && view.Overlay.alpha == 1f && view.Overlay.blocksRaycasts &&
                   view.GraphicRaycaster.enabled && session.BlocksPause,
                "E at the shopkeeper must open the shop screen.");
            Assert(actionState.IsShopBlocked && !actionState.CanMove && !actionState.CanBasicAttack &&
                   !actionState.CanUseLowerGradeSkill && !actionState.CanStartUltimate && !actionState.CanTransition &&
                   !keeper.CanInteract && !keeper.Prompt.gameObject.activeSelf && !keeper.TryOpen(),
                "While the shop screen is open the player must not move, attack, change rooms, or open it again.");

            // The grid shows the stored stock; the last row is empty.
            Assert(progress.GetResourceCount(RunResourceType.Gold) == 0 && view.FocusedIndex == 0 &&
                   view.GoldText.text == "보유 0 골드",
                "The shop screen must open on the first offer and show the wallet.");
            for (int index = 0; index < view.Cells.Length; index++)
            {
                ShopOfferCellView cell = view.Cells[index];
                if (index >= stock.Offers.Count)
                {
                    Assert(cell.Offer == null && cell.Status == ShopOfferStatus.Empty && cell.Content.alpha == 0f,
                        $"Shop cell {index + 1} has no offer and must be empty.");
                    continue;
                }

                ShopOffer offer = stock.Offers[index];
                Assert(cell.Offer == offer && cell.Status == ShopOfferStatus.NotEnoughGold &&
                       cell.Content.alpha == 1f && cell.NameText.text == offer.DisplayName &&
                       cell.PriceText.text == $"{offer.Price} 골드" && cell.IsFocused == (index == 0) &&
                       cell.FocusFrame.enabled == (index == 0),
                    $"Shop cell {index + 1} must show {offer.OfferId} at its gold price.");
                Assert(offer.Kind == ShopOfferKind.Item
                        ? cell.Icon.sprite == null && cell.IconText.text ==
                          ArtifactHudSlotView.GetShortStableId(offer.Item.ItemId)
                        : cell.Icon.sprite != null && cell.IconText.text == string.Empty,
                    $"Shop cell {index + 1} must show the Item placeholder or the consumable's pickup sprite.");
            }

            // Focus moves one cell at a time and stops at the edges.
            view.MoveFocus(-1, 0);
            view.MoveFocus(0, -1);
            Assert(view.FocusedIndex == 0, "The focus must stop at the top-left edge.");
            view.MoveFocus(1, 0);
            Assert(view.FocusedIndex == 1 && view.Cells[1].IsFocused && !view.Cells[0].IsFocused,
                "Right must focus the next cell.");
            view.MoveFocus(0, 1);
            Assert(view.FocusedIndex == 4 && view.DetailNameText.text == stock.Offers[4].DisplayName &&
                   view.DetailKindText.text == ShopView.ConsumableKind,
                "Down must focus the consumable row and describe it.");
            view.MoveFocus(1, 0);
            view.MoveFocus(1, 0);
            view.MoveFocus(0, 1);
            view.MoveFocus(0, 1);
            Assert(view.FocusedIndex == 8 && view.Cells[8].IsFocused && !view.BuyButton.interactable &&
                   view.DetailEffectText.text == ShopView.EmptyCellMessage &&
                   view.TryPurchaseFocused() == ShopPurchaseResult.Unavailable &&
                   view.FeedbackText.text == ShopView.EmptyCellMessage,
                "The focus must stop at the bottom-right edge, and an empty cell sells nothing.");

            // An Item: refused without gold, then bought once.
            ShopOffer item = stock.Offers[0];
            view.SetFocus(item.SlotIndex);
            int stacks = inventory.GetStackCount(item.Item.ItemId);
            Assert(view.FeedbackText.text == string.Empty && view.DetailNameText.text == item.DisplayName &&
                   view.DetailKindText.text.Contains(ItemKindText.GetDisplayName(item.Item.Kind)) &&
                   view.DetailEffectText.text.StartsWith(ArtifactEffectDescription.Build(item.Item),
                       StringComparison.Ordinal) &&
                   view.DetailPriceText.text == $"{item.Price} 골드" && !view.BuyButton.interactable &&
                   view.BuyButtonLabel.text == ShopView.BuyLabel,
                "The detail column must describe the focused Item and its price.");
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.NotEnoughGold &&
                   view.FeedbackText.text == ShopView.NotEnoughGoldMessage && !stock.IsPurchased(item.SlotIndex) &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks,
                "Without enough gold the shop screen must refuse and change nothing.");

            int budget = stock.Offers.Sum(offer => offer.Price);
            Assert(progress.TryAddResource(RunResourceType.Gold, budget) == budget, "Verification gold must fit.");
            view.Refresh();
            Assert(view.GoldText.text == $"보유 {budget} 골드" && view.BuyButton.interactable &&
                   view.Cells[item.SlotIndex].Status == ShopOfferStatus.Available,
                "Gained gold must show at once and make the focused offer buyable.");
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.Purchased &&
                   view.FeedbackText.text == ShopView.PurchasedMessage && stock.IsPurchased(item.SlotIndex) &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == budget - item.Price &&
                   view.GoldText.text == $"보유 {budget - item.Price} 골드" &&
                   view.Cells[item.SlotIndex].Status == ShopOfferStatus.SoldOut &&
                   view.Cells[item.SlotIndex].PriceText.text == ShopView.SoldOutLabel &&
                   !view.BuyButton.interactable && view.FocusedIndex == item.SlotIndex && session.IsOpen,
                "Buying an Item must spend its price once, sell out its cell, and keep the screen open.");
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.SoldOut &&
                   view.FeedbackText.text == ShopView.SoldOutMessage &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == budget - item.Price,
                "A sold cell must not be bought again.");

            // A consumable drops in front of the shopkeeper.
            ShopOffer consumable = stock.Offers[ShopCatalog.ItemOfferCount];
            view.SetFocus(consumable.SlotIndex);
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.Purchased && shop.LastDroppedPickup != null &&
                   shop.LastDroppedPickup.transform.parent == shopRoom.Node.ContentRoot.transform &&
                   Vector2.Distance(shop.LastDroppedPickup.transform.position,
                       shop.GetDropPosition(consumable.SlotIndex)) < 0.01f &&
                   progress.GetResourceCount(RunResourceType.Gold) == budget - item.Price - consumable.Price,
                "Buying a consumable must drop its floor pickup in front of the shopkeeper.");

            // Closing gives control back; reopening shows the same stock on the first offer still on sale.
            view.Close();
            Assert(!session.IsOpen && !view.IsVisible && view.Overlay.alpha == 0f && !view.Overlay.blocksRaycasts &&
                   !view.GraphicRaycaster.enabled && !actionState.IsShopBlocked && actionState.CanMove &&
                   session.BlocksPause && keeper.CanInteract,
                "Closing the shop screen must give control back, and the closing Escape must not open the pause menu.");
            Assert(keeper.TryOpen() && view.IsVisible && view.FocusedIndex == 1 &&
                   view.Cells[item.SlotIndex].Status == ShopOfferStatus.SoldOut &&
                   view.Cells[consumable.SlotIndex].Status == ShopOfferStatus.SoldOut &&
                   view.Cells.Count(cell => cell.Status == ShopOfferStatus.Available) == ShopCatalog.OfferCount - 2,
                "Reopening must show the same stock with its sold cells.");

            // 멤버십카드 while the screen is open: every offer on sale becomes free at once.
            int goldBeforeFree = progress.GetResourceCount(RunResourceType.Gold);
            Assert(stock.TryMakeFree(), "The verification shop must accept the free state.");
            view.Refresh();
            ShopOffer freeItem = stock.Offers[1];
            Assert(view.Cells.Where(cell => cell.Offer != null && !stock.IsPurchased(cell.Offer.SlotIndex)).All(cell =>
                       cell.Status == ShopOfferStatus.Free && cell.PriceText.text == ShopView.FreeLabel) &&
                   view.Cells[item.SlotIndex].PriceText.text == ShopView.SoldOutLabel &&
                   view.DetailPriceText.text == ShopView.FreeLabel && view.BuyButton.interactable &&
                   view.BuyButtonLabel.text == ShopView.TakeLabel,
                "Free offers must show the free label and the take button; sold cells stay sold.");
            int freeStacks = inventory.GetStackCount(freeItem.Item.ItemId);
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.Purchased &&
                   inventory.GetStackCount(freeItem.Item.ItemId) == freeStacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == goldBeforeFree &&
                   view.TryPurchaseFocused() == ShopPurchaseResult.SoldOut,
                "A free offer must be taken once without spending gold.");

            // A floor rebuild destroys the room: the screen closes, and the rebuilt shop shows the same state.
            Assert(assembler.TryLoadFloor(2, null, out error), error);
            // Edit mode skips the room's OnDestroy; the session's own check closes it on the next frame either way.
            InvokeLifecycle(session, "Update");
            Assert(!session.IsOpen && !view.IsVisible && !actionState.IsShopBlocked,
                "Leaving the floor must close the shop screen and unblock the player.");
            Assert(assembler.TryLoadFloor(1, null, out error), error);
            ShopRoom rebuilt = FindRuntimeRoom(assembler, shopNode.RoomId).GetComponentInChildren<ShopRoom>(true);
            Assert(rebuilt != null && rebuilt != shop && rebuilt.Stock == stock && rebuilt.Session == session,
                "A rebuilt shop room must reuse its stock and the shop session.");
            rebuilt.Keeper.SetPlayerPresence(true, inventory, health, actionState);
            Assert(rebuilt.Keeper.TryOpen() && session.Shop == rebuilt && view.IsVisible &&
                   view.Cells.Count(cell => cell.Status == ShopOfferStatus.SoldOut) == 3 &&
                   view.Cells.Count(cell => cell.Status == ShopOfferStatus.Free) == ShopCatalog.OfferCount - 3 &&
                   view.FocusedIndex == 2,
                "A revisit must keep the sold cells and the free prices.");

            // The Run ending closes the screen and keeps it closed.
            progress.StopProgression();
            InvokeLifecycle(session, "Update");
            Assert(!session.IsOpen && !view.IsVisible && !actionState.IsShopBlocked && !rebuilt.Keeper.TryOpen(),
                "A stopped Run must close the shop screen and keep it closed.");
            progress.ResetProgress();
        }

        private static int FindFloorOneShopSeed(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 512; seed++)
            {
                if (generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _) &&
                    graph.FindFloor(1).Nodes.Any(node => node.Role == GeneratedRoomRole.Shop))
                    return seed;
            }

            throw new InvalidOperationException("No seed with a floor-1 shop was found.");
        }

        private static RoomPrefab FindRuntimeRoom(RoomGraphAssembler assembler, string roomId)
        {
            foreach (RoomNode node in assembler.Graph.Nodes)
                if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
            throw new InvalidOperationException($"Runtime room {roomId} is missing.");
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
