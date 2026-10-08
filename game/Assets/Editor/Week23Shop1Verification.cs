using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Character;
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
    // Shop-1: about one in five secret rooms holds the 골디 shop instead of the treasure-style reward. It sells three
    // Items of Rare or better (filled from the rest only when too few are eligible) and three consumables at the
    // general prices through the same 3×3 screen, shown with 골디's portrait, texts and a gold panel. Sold and free
    // (멤버십카드) state survive closing, reopening and a floor rebuild, and the general shop keeps its own look.
    public static class Week23Shop1Verification
    {
        private const int SeedCount = 1000;

        public static void SetupAndVerifyBatch()
        {
            // Shop-1 adds the title, keeper texts and panel references to the Shop-0 screen; no new asset is created.
            Week22Shop0Setup.Setup();
            Verify();
        }

        // Also re-runs the secret room, the general shop and its screen, 멤버십카드, and the development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week20Special3Verification.Verify();
            // Shop-0 runs the Special-4, Gold-0, 멤버십카드, slot, reward selection and development panel checks.
            Week22Shop0Verification.VerifyWithRegressionsBatch();
            Debug.Log("Shop-1 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Shop-1 Goldi Shop")]
        public static void Verify()
        {
            ValidateContract();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            ShopSession session = FindAll<ShopSession>(scene).Single();
            ShopView view = FindAll<ShopView>(scene).Single();
            Assert(view.TitleText != null && view.KeeperNameText != null && view.KeeperLineText != null &&
                   view.PanelImage != null && view.PanelOutline != null && view.KeeperPortrait != null &&
                   view.TitleText.text == ShopView.Title && view.KeeperNameText.text == ShopKeeper.DisplayName &&
                   view.KeeperLineText.text == ShopView.KeeperLine,
                "Run Shop-0 setup again: the shop screen must have its title, keeper texts and panel connected and " +
                "be saved with the general shop's texts.");
            try
            {
                (int goldiSeed, int generalSeed) = ValidateGeneration(assembler.Generator);
                ValidateStock(assembler);
                ValidateRuntime(assembler, session, view, goldiSeed, generalSeed);
            }
            finally
            {
                // The runtime checks build a floor in the Game Scene; discard it without saving.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            Debug.Log("Shop-1 verification passed: about 20% of secret rooms hold the Goldi shop without changing the " +
                      "floor layout, it sells Rare-or-better Items and three consumables at the general prices, the " +
                      "screen shows Goldi's portrait, texts and gold panel, 멤버십카드 works there, sold and free " +
                      "state survive a floor rebuild, and the general shop keeps its own look.");
        }

        private static void ValidateContract()
        {
            Assert(FloorGenerator.GoldiShopPercent == 20 && ShopStockBuilder.GoldiMinimumRarity == ItemRarity.Rare,
                "Shop-1: 20% of secret rooms hold the Goldi shop, which sells Rare or better (2026-10-08).");
            Assert(ShopKeeper.GetDisplayName(ShopKind.General) == "시스트" &&
                   ShopKeeper.GetDisplayName(ShopKind.Goldi) == "골디" &&
                   ShopView.GetTitle(ShopKind.General) == ShopView.Title &&
                   ShopView.GetTitle(ShopKind.Goldi) == "골디의 상점" &&
                   ShopView.GetKeeperLine(ShopKind.Goldi) != ShopView.GetKeeperLine(ShopKind.General) &&
                   ShopView.GetConsumableEffect(ShopKind.Goldi).Contains(ShopKeeper.GoldiDisplayName) &&
                   ShopView.GetConsumableEffect(ShopKind.General).Contains(ShopKeeper.DisplayName),
                "The two shops must name their own keeper in the title, the name and the consumable text.");
            Assert(ShopView.GoldiPanelColor != ShopView.PanelColor &&
                   ShopView.GoldiPanelOutlineColor != ShopView.PanelOutlineColor &&
                   ShopView.GoldiPanelOutlineDistance.x > ShopView.PanelOutlineDistance.x &&
                   ShopView.GoldiTitleColor != ShopView.TitleColor,
                "The Goldi shop screen must look different from the general shop screen.");

            Sprite goldi = UserArtwork.Load(ShopKeeper.GoldiArtworkKey);
            Sprite sist = UserArtwork.Load(ShopKeeper.ArtworkKey);
            Assert(goldi != null && sist != null && goldi != sist, "The Goldi and 시스트 artwork must both exist.");

            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(catalog != null && catalog.TryValidate(out _), "Run Shop-0 setup first: the shop catalog is missing.");
            Assert(font != null && font.HasCharacters(Week22Shop0Setup.RequiredGlyphs(catalog)),
                "Run Shop-0 setup again: the Frontend font is missing a Goldi shop glyph.");
        }

        private static (int GoldiSeed, int GeneralSeed) ValidateGeneration(FloorGenerator generator)
        {
            int secrets = 0, goldiShops = 0, goldiSeed = 0, generalSeed = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                foreach (GeneratedFloor floor in first.Floors)
                {
                    GeneratedFloor repeatedFloor = repeated.FindFloor(floor.FloorNumber);
                    Assert(floor.Nodes.Select(node => node.IsGoldiShop)
                            .SequenceEqual(repeatedFloor.Nodes.Select(node => node.IsGoldiShop)),
                        $"Seed {seed} floor {floor.FloorNumber} must reproduce its Goldi shop.");
                    foreach (GeneratedRoomNode node in floor.Nodes)
                    {
                        if (node.Role == GeneratedRoomRole.Secret) secrets++;
                        if (!node.IsGoldiShop) continue;
                        goldiShops++;
                        // It stays a secret room in every other way: reward-type definition, no key, hidden passages.
                        Assert(node.Role == GeneratedRoomRole.Secret && node.RoomType == RoomType.Reward &&
                               !node.RequiresKey && node.Encounter == null && node.DirectionalConnections.Count > 0 &&
                               node.DirectionalConnections.All(connection => connection.IsSecret),
                            $"Seed {seed} room {node.RoomId}: the Goldi shop must be an ordinary secret room.");
                        if (goldiSeed == 0 && floor.FloorNumber == 1 &&
                            floor.Nodes.Any(other => other.Role == GeneratedRoomRole.Shop))
                            goldiSeed = seed;
                    }

                    if (generalSeed == 0 && floor.FloorNumber == 1 && !floor.Nodes.Any(node => node.IsGoldiShop) &&
                        floor.Nodes.Any(node => node.Role == GeneratedRoomRole.Shop))
                        generalSeed = seed;
                }
            }

            float ratio = secrets == 0 ? 0f : goldiShops / (float)secrets;
            Assert(ratio >= 0.15f && ratio <= 0.25f,
                $"About 20% of secret rooms should hold the Goldi shop; observed {ratio:P1} of {secrets}.");
            Assert(goldiSeed > 0 && generalSeed > 0,
                "Seeds must include a floor 1 with both a Goldi shop and a general shop, and one with only a general shop.");
            Debug.Log($"Shop-1 generation: {goldiShops}/{secrets} secret rooms ({ratio:P1}) hold the Goldi shop. " +
                      $"Runtime seeds: Goldi {goldiSeed}, general {generalSeed}.");
            return (goldiSeed, generalSeed);
        }

        private static void ValidateStock(RoomGraphAssembler assembler)
        {
            ShopCatalog catalog = assembler.ShopCatalog;
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            int better = pool.Where(item => ArtifactRewardSelector.IsEligible(item, null))
                .Select(item => item.ItemId).Distinct()
                .Count(id => pool.First(item => item != null && item.ItemId == id).Rarity >=
                             ShopStockBuilder.GoldiMinimumRarity);
            int expectedBetter = Mathf.Min(ShopCatalog.ItemOfferCount, better);
            Assert(better > 0, "The selection reward pool needs at least one Rare or better Item for the Goldi shop.");

            bool sawLowerInGeneral = false;
            for (int roomSeed = 1; roomSeed <= 64; roomSeed++)
            {
                string roomId = FloorGenerator.BuildRoomId(1, 9);
                ShopStockState goldi = ShopStockBuilder.Build(roomId, roomSeed, catalog, pool, null, ShopKind.Goldi);
                ShopStockState repeated = ShopStockBuilder.Build(roomId, roomSeed, catalog, pool, null, ShopKind.Goldi);
                ShopStockState general = ShopStockBuilder.Build(roomId, roomSeed, catalog, pool, null);
                ShopOffer[] items = goldi.Offers.Where(offer => offer.Kind == ShopOfferKind.Item).ToArray();
                Assert(goldi.Kind == ShopKind.Goldi && general.Kind == ShopKind.General &&
                       goldi.ShopId == general.ShopId && goldi.Offers.Count == ShopCatalog.OfferCount &&
                       goldi.Offers.Select(offer => offer.OfferId).SequenceEqual(
                           repeated.Offers.Select(offer => offer.OfferId)) &&
                       goldi.Offers.Select(offer => offer.OfferId).Distinct().Count() == goldi.Offers.Count &&
                       goldi.Offers.Select((offer, index) => offer.SlotIndex == index).All(matches => matches),
                    $"Room seed {roomSeed}: the Goldi stock must be six distinct, reproducible offers in slot order.");
                Assert(items.Length == ShopCatalog.ItemOfferCount &&
                       goldi.Offers.Take(items.Length).All(offer => offer.Kind == ShopOfferKind.Item) &&
                       goldi.Offers.Skip(items.Length).All(offer => offer.Kind == ShopOfferKind.Consumable),
                    $"Room seed {roomSeed}: the Goldi shop must sell three Items then three consumables.");
                Assert(items.Take(expectedBetter).All(offer =>
                           offer.Item.Rarity >= ShopStockBuilder.GoldiMinimumRarity) &&
                       items.Skip(expectedBetter).All(offer =>
                           offer.Item.Rarity < ShopStockBuilder.GoldiMinimumRarity),
                    $"Room seed {roomSeed}: the Goldi shop must sell Rare or better first and fill only what is left.");
                Assert(items.All(offer => offer.Price == catalog.GetItemPrice(offer.Item.Rarity)) &&
                       goldi.Offers.Where(offer => offer.Kind == ShopOfferKind.Consumable)
                           .All(offer => offer.Price == offer.Consumable.Price),
                    $"Room seed {roomSeed}: the Goldi shop must use the general prices.");
                sawLowerInGeneral |= general.Offers.Any(offer =>
                    offer.Kind == ShopOfferKind.Item && offer.Item.Rarity < ShopStockBuilder.GoldiMinimumRarity);
            }

            Assert(sawLowerInGeneral, "The general shop must still sell Items below Rare.");
        }

        private static void ValidateRuntime(RoomGraphAssembler assembler, ShopSession session, ShopView view,
            int goldiSeed, int generalSeed)
        {
            // Edit mode skips Awake/OnEnable; the view subscribes to its session there.
            InvokeLifecycle(view, "OnEnable");
            foreach (ShopOfferCellView cell in view.Cells) InvokeLifecycle(cell, "OnEnable");

            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(goldiSeed, out string error), error);
            GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode goldiNode = floor.Nodes.Single(node => node.IsGoldiShop);
            GeneratedRoomNode generalNode = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Shop);
            RoomPrefab goldiRoom = FindRuntimeRoom(assembler, goldiNode.RoomId);
            ShopRoom shop = goldiRoom.GetComponentInChildren<ShopRoom>(true);
            ShopStockState stock = progress.GetShopStock(ShopStockBuilder.BuildShopId(goldiNode.RoomId));
            ShopRoom generalShop = FindRuntimeRoom(assembler, generalNode.RoomId).GetComponentInChildren<ShopRoom>(true);
            Sprite goldiSprite = UserArtwork.Load(ShopKeeper.GoldiArtworkKey);
            Sprite sistSprite = UserArtwork.Load(ShopKeeper.ArtworkKey);

            Assert(shop != null && stock != null && shop.Stock == stock && shop.Kind == ShopKind.Goldi &&
                   stock.Kind == ShopKind.Goldi && shop.Session == session &&
                   stock.Offers.Count == ShopCatalog.OfferCount &&
                   goldiRoom.Controller.State == RoomState.Cleared &&
                   (goldiRoom.RewardRoom == null || !goldiRoom.RewardRoom.gameObject.activeSelf),
                "The Goldi secret room must be a safe room with the Goldi stock and no treasure-style reward.");
            Assert(shop.Keeper != null && shop.Keeper.NameLabel.text == ShopKeeper.GoldiDisplayName &&
                   shop.Keeper.Portrait.sprite == goldiSprite,
                "The Goldi shop's keeper must show Goldi's name and artwork.");
            AssertStandsLikeNpc(shop.Keeper, assembler.Graph.Player);
            AssertStandsLikeNpc(generalShop.Keeper, assembler.Graph.Player);
            Assert(generalShop != null && generalShop.Kind == ShopKind.General && generalShop.Stock != stock &&
                   generalShop.Keeper.NameLabel.text == ShopKeeper.DisplayName &&
                   generalShop.Keeper.Portrait.sprite == sistSprite,
                "A general shop on the same floor must stay 시스트's shop with its own stock.");
            Assert(goldiNode.DirectionalConnections.All(connection =>
                    goldiRoom.FindSlot(connection.Direction).Seal.activeSelf),
                "The Goldi shop's hidden passages must start sealed like any secret room's.");

            GameObject player = assembler.Graph.Player.gameObject;
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            Health health = player.GetComponent<Health>();
            PlayerActionState actionState = player.GetComponent<PlayerActionState>();
            PlayerSingleUseEffects effects = player.GetComponent<PlayerSingleUseEffects>();
            InvokeLifecycle(health, "Awake");
            // A bought Item can change the player's stats (max health, for one).
            InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
            InvokeLifecycle(inventory, "Awake");
            InvokeLifecycle(effects, "Awake");

            // 멤버십카드 is refused outside a shop and accepted in the Goldi secret room.
            Assert(!ResolvesCurrentShop(effects, out _), "The start room is not a shop.");
            Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), goldiRoom.Node,
                assembler.Graph.Player, out error), error);
            Assert(assembler.Graph.CurrentNode == goldiRoom.Node &&
                   ResolvesCurrentShop(effects, out ShopStockState resolved) && resolved == stock,
                "멤버십카드 must find the Goldi shop's stock inside its secret room.");

            ShopKeeper keeper = shop.Keeper;
            AssertGreets(keeper, generalShop.Keeper, inventory, health, actionState);
            keeper.SetPlayerPresence(true, inventory, health, actionState);
            Assert(keeper.TryOpen() && session.IsOpen && session.Shop == shop && view.IsVisible,
                "E at Goldi must open the shop screen.");
            Assert(!keeper.SpeechBubble.IsShown, "The greeting must hide while the shop screen is open.");
            Assert(view.ShownKind == ShopKind.Goldi && view.TitleText.text == ShopView.GoldiTitle &&
                   view.TitleText.color == ShopView.GoldiTitleColor &&
                   view.KeeperNameText.text == ShopKeeper.GoldiDisplayName &&
                   view.KeeperLineText.text == ShopView.GoldiKeeperLine &&
                   view.KeeperPortrait.sprite == goldiSprite && view.PanelImage.color == ShopView.GoldiPanelColor &&
                   view.PanelOutline.effectColor == ShopView.GoldiPanelOutlineColor &&
                   view.PanelOutline.effectDistance == ShopView.GoldiPanelOutlineDistance,
                "The Goldi shop screen must show Goldi's title, name, line, portrait and gold panel.");
            for (int index = 0; index < stock.Offers.Count; index++)
                Assert(view.Cells[index].Offer == stock.Offers[index] &&
                       view.Cells[index].PriceText.text == $"{stock.Offers[index].Price} 골드",
                    $"Goldi shop cell {index + 1} must show its stored offer at its gold price.");
            view.SetFocus(ShopCatalog.ItemOfferCount);
            Assert(view.DetailEffectText.text == ShopView.GoldiConsumableEffect,
                "A Goldi shop consumable must say it drops in front of Goldi.");

            // A purchase follows the general rules: refused without gold, then bought once.
            ShopOffer item = stock.Offers[0];
            view.SetFocus(item.SlotIndex);
            int stacks = inventory.GetStackCount(item.Item.ItemId);
            Assert(view.TryPurchaseFocused() == ShopPurchaseResult.NotEnoughGold && !stock.IsPurchased(item.SlotIndex),
                "Without enough gold the Goldi shop must refuse.");
            Assert(progress.TryAddResource(RunResourceType.Gold, item.Price) == item.Price &&
                   view.TryPurchaseFocused() == ShopPurchaseResult.Purchased && stock.IsPurchased(item.SlotIndex) &&
                   inventory.GetStackCount(item.Item.ItemId) == stacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == 0 &&
                   view.TryPurchaseFocused() == ShopPurchaseResult.SoldOut,
                "Buying a Goldi shop Item must spend its price once and sell out its cell.");

            // 멤버십카드: the remaining offers become free; the general shop on the floor keeps its prices.
            Assert(stock.TryMakeFree() && !generalShop.Stock.IsFree, "Only the Goldi shop must become free.");
            view.Refresh();
            ShopOffer freeItem = stock.Offers[1];
            view.SetFocus(freeItem.SlotIndex);
            int freeStacks = inventory.GetStackCount(freeItem.Item.ItemId);
            Assert(view.Cells[freeItem.SlotIndex].Status == ShopOfferStatus.Free &&
                   view.TryPurchaseFocused() == ShopPurchaseResult.Purchased &&
                   inventory.GetStackCount(freeItem.Item.ItemId) == freeStacks + 1 &&
                   progress.GetResourceCount(RunResourceType.Gold) == 0,
                "A free Goldi shop offer must be taken once without gold.");

            // A floor rebuild keeps the kind, the sold cells and the free prices.
            Assert(assembler.TryLoadFloor(2, null, out error), error);
            InvokeLifecycle(session, "Update");
            Assert(!session.IsOpen && !view.IsVisible, "Leaving the floor must close the Goldi shop screen.");
            Assert(assembler.TryLoadFloor(1, null, out error), error);
            RoomPrefab rebuiltRoom = FindRuntimeRoom(assembler, goldiNode.RoomId);
            ShopRoom rebuilt = rebuiltRoom.GetComponentInChildren<ShopRoom>(true);
            Assert(rebuilt != null && rebuilt != shop && rebuilt.Stock == stock && rebuilt.Kind == ShopKind.Goldi &&
                   stock.IsFree && stock.Offers.Count(offer => stock.IsPurchased(offer.SlotIndex)) == 2 &&
                   rebuilt.Keeper.NameLabel.text == ShopKeeper.GoldiDisplayName &&
                   rebuilt.Keeper.Portrait.sprite == goldiSprite &&
                   (rebuiltRoom.RewardRoom == null || !rebuiltRoom.RewardRoom.gameObject.activeSelf) &&
                   goldiNode.DirectionalConnections.All(connection =>
                       !rebuiltRoom.FindSlot(connection.Direction).Seal.activeSelf),
                "A revisit must keep the Goldi shop, its sold and free state, and the passages opened by entering.");
            rebuilt.Keeper.SetPlayerPresence(true, inventory, health, actionState);
            Assert(rebuilt.Keeper.TryOpen() && view.ShownKind == ShopKind.Goldi &&
                   view.Cells.Count(cell => cell.Status == ShopOfferStatus.SoldOut) == 2 &&
                   view.Cells.Count(cell => cell.Status == ShopOfferStatus.Free) == ShopCatalog.OfferCount - 2,
                "A reopened Goldi shop must show the same sold cells and free prices.");
            view.Close();

            // The same screen then shows a general shop with its own look again.
            ShopRoom rebuiltGeneral = FindRuntimeRoom(assembler, generalNode.RoomId).GetComponentInChildren<ShopRoom>(true);
            rebuiltGeneral.Keeper.SetPlayerPresence(true, inventory, health, actionState);
            Assert(rebuiltGeneral.Keeper.TryOpen() && session.Shop == rebuiltGeneral, "The general shop must open.");
            AssertGeneralLook(view, sistSprite);
            Assert(view.Cells.All(cell => cell.Status != ShopOfferStatus.Free),
                "The general shop must not inherit the Goldi shop's free prices.");
            view.Close();

            // A floor without a Goldi shop: its secret room, if any, keeps the treasure-style reward.
            progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(generalSeed, out error), error);
            GeneratedFloor plainFloor = assembler.GeneratedGraph.FindFloor(1);
            foreach (GeneratedRoomNode secret in plainFloor.Nodes.Where(node => node.Role == GeneratedRoomRole.Secret))
            {
                RoomPrefab secretRoom = FindRuntimeRoom(assembler, secret.RoomId);
                Assert(secretRoom.GetComponentInChildren<ShopRoom>(true) == null && secretRoom.RewardRoom != null &&
                       secretRoom.RewardRoom.gameObject.activeSelf,
                    "A plain secret room must keep its treasure-style reward and have no shop.");
            }

            progress.ResetProgress();
        }

        // The keeper is drawn at the player's size and has a solid body the player's feet collide with, inside a
        // larger talking range.
        private static void AssertStandsLikeNpc(ShopKeeper keeper, PlayerMovement player)
        {
            SpriteRenderer playerSprite = player.GetComponent<SpriteRenderer>();
            Assert(keeper.Portrait.sortingLayerID == playerSprite.sortingLayerID &&
                   keeper.Portrait.sortingOrder == NpcPresentation.BodySortingOrder &&
                   keeper.Portrait.sortingOrder < playerSprite.sortingOrder - 1,
                $"{keeper.NameLabel.text} must be drawn below the player and the player's shadow.");
            Vector2 playerSize = playerSprite.sprite.bounds.size * (Vector2)player.transform.lossyScale;
            Vector2 keeperSize = keeper.Portrait.sprite.bounds.size * (Vector2)keeper.Portrait.transform.lossyScale;
            Assert(Mathf.Abs(Mathf.Max(keeperSize.x, keeperSize.y) - Mathf.Max(playerSize.x, playerSize.y)) < 0.01f,
                $"{keeper.NameLabel.text} must be drawn at the player's size; keeper {keeperSize}, player {playerSize}.");

            Transform body = keeper.transform.Find(ShopKeeper.BodyObjectName);
            CircleCollider2D solid = body != null ? body.GetComponent<CircleCollider2D>() : null;
            BoxCollider2D range = keeper.GetComponent<BoxCollider2D>();
            Assert(solid != null && !solid.isTrigger && solid.GetComponentInParent<Rigidbody2D>() == null &&
                   body.gameObject.layer == LayerMask.NameToLayer(ShopKeeper.BodyLayerName) &&
                   PlayerFeet.CollisionLayers.Contains(ShopKeeper.BodyLayerName) &&
                   Mathf.Approximately(solid.radius * body.lossyScale.x, PlayerFeet.BodyRadius),
                $"{keeper.NameLabel.text} needs a solid, player-sized body on a layer the player's feet collide with.");
            Assert(range != null && range.isTrigger &&
                   Mathf.Min(range.size.x, range.size.y) * 0.5f >=
                   ShopKeeper.BodyRadius + PlayerFeet.Radius + PlayerFeet.BodyRadius,
                $"{keeper.NameLabel.text}'s talking range must reach a player standing against the body.");
        }

        // Near a keeper its greeting shows in a white bubble with a black outline, sized to the line; away it hides.
        private static void AssertGreets(ShopKeeper goldi, ShopKeeper sist, PlayerInventory inventory, Health health,
            PlayerActionState actionState)
        {
            Assert(ShopKeeper.GetGreetingLine(ShopKind.Goldi) == "안녕하세요!" &&
                   ShopKeeper.GetGreetingLine(ShopKind.General) == "뭐 하나 사시려구요?",
                "Goldi and 시스트 must greet with their own lines.");
            foreach ((ShopKeeper keeper, ShopKind kind) in new[] { (goldi, ShopKind.Goldi), (sist, ShopKind.General) })
            {
                NpcSpeechBubble bubble = keeper.SpeechBubble;
                Assert(bubble != null && !bubble.IsShown && bubble.Fill != null && bubble.Outline != null &&
                       bubble.TailFill != null && bubble.TailOutline != null && bubble.Text != null,
                    $"{keeper.NameLabel.text} needs a hidden speech bubble with box, outline, tail and text.");
                keeper.SetPlayerPresence(true, inventory, health, actionState);
                Vector2 textSize = bubble.Text.GetPreferredValues(bubble.Line);
                Assert(bubble.IsShown && bubble.Line == ShopKeeper.GetGreetingLine(kind) &&
                       bubble.Fill.color == Color.white && bubble.Outline.color == Color.black &&
                       bubble.TailFill.color == Color.white && bubble.TailOutline.color == Color.black &&
                       bubble.Text.color == Color.black && textSize.x > 0.5f &&
                       bubble.Fill.size.x >= textSize.x && bubble.Fill.size.y >= textSize.y &&
                       Mathf.Approximately(bubble.Outline.size.x - bubble.Fill.size.x, NpcSpeechBubble.OutlineWidth * 2f) &&
                       bubble.Outline.sortingOrder < bubble.Fill.sortingOrder &&
                       bubble.Fill.sortingOrder < bubble.TailFill.sortingOrder &&
                       bubble.TailFill.sortingOrder < bubble.Text.sortingOrder &&
                       bubble.Fill.transform.position.y - bubble.Fill.size.y * 0.5f >
                       keeper.NameLabel.transform.position.y,
                    $"{keeper.NameLabel.text} must greet in a white, black-outlined bubble that fits its line above " +
                    $"the name label; text {textSize}, box {bubble.Fill.size}.");
                keeper.SetPlayerPresence(false, null, null);
                Assert(!bubble.IsShown, $"{keeper.NameLabel.text}'s greeting must hide when the player leaves.");
            }
        }

        private static void AssertGeneralLook(ShopView view, Sprite sistSprite)
        {
            Assert(view.ShownKind == ShopKind.General && view.TitleText.text == ShopView.Title &&
                   view.TitleText.color == ShopView.TitleColor &&
                   view.KeeperNameText.text == ShopKeeper.DisplayName &&
                   view.KeeperLineText.text == ShopView.KeeperLine && view.KeeperPortrait.sprite == sistSprite &&
                   view.PanelImage.color == ShopView.PanelColor &&
                   view.PanelOutline.effectColor == ShopView.PanelOutlineColor &&
                   view.PanelOutline.effectDistance == ShopView.PanelOutlineDistance,
                "The general shop screen must show 시스트's title, name, line, portrait and panel again.");
        }

        private static bool ResolvesCurrentShop(PlayerSingleUseEffects effects, out ShopStockState stock)
        {
            MethodInfo method = typeof(PlayerSingleUseEffects).GetMethod("TryResolveCurrentShop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "PlayerSingleUseEffects.TryResolveCurrentShop is missing.");
            object[] arguments = { null, null };
            bool resolved = (bool)method.Invoke(effects, arguments);
            stock = (ShopStockState)arguments[0];
            return resolved;
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
